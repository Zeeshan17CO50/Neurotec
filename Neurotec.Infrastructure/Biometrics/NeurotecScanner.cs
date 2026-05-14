using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neurotec.Biometrics;
using Neurotec.Biometrics.Client;
using Neurotec.Devices;
using Neurotec.Domain.Configuration;
using Neurotec.Domain.Entities;
using Neurotec.Domain.Enums;
using Neurotec.Domain.Interfaces;
using Neurotec.Licensing;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace Neurotec.Infrastructure.Biometrics;

/// <summary>
/// Enterprise-grade implementation of the Neurotec Biometric SDK (VeriFinger/MegaMatcher).
/// Handles hardware lifecycle, licensing, and high-precision biometric acquisition.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class NeurotecScanner : IBiometricScanner, IDisposable
{
    private readonly NBiometricClient _biometricClient;
    private readonly NeurotecSettings _settings;
    private readonly ILogger<NeurotecScanner> _logger;
    private ScannerStatus _status = ScannerStatus.Ready;
    private bool _isDisposed;

    public event Action<ScannerStatus>? OnStatusChanged;

    public NeurotecScanner(IOptions<NeurotecSettings> settings, ILogger<NeurotecScanner> logger)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("Initializing Neurotec Scanner Service at: {Path}", AppContext.BaseDirectory);

        // Initialize the Biometric Client with Device Manager enabled
        _biometricClient = new NBiometricClient { UseDeviceManager = true };
        
        InitializeSdk();
    }

    private void InitializeSdk()
    {
        try
        {
            _status = ScannerStatus.Ready;

            // Configure Licensing
            var server = _settings.NeurotecSdk.LicenseServer ?? "/local";
            var components = string.Join(",", _settings.NeurotecSdk.Components);
            
            _logger.LogDebug("Requesting Neurotec licenses: {Components} from {Server}", components, server);
            
            bool obtained = NLicense.ObtainComponents(server, 5000, components);
            
            if (!obtained)
            {
                _logger.LogWarning("Failed to obtain all requested Neurotec licenses. Some hardware features may be unavailable.");
            }

            // Set Device Types early to optimize hardware polling
            _biometricClient.DeviceManager.DeviceTypes = NDeviceType.FingerScanner;
        }
        catch (Exception ex) when (ex.Message.Contains("already initialized"))
        {
            _logger.LogInformation("Neurotec SDK components already initialized in this process.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error during Neurotec SDK initialization.");
            _status = ScannerStatus.Error;
        }
    }

    public ScannerStatus GetStatus()
    {
        // Self-healing: If we were in an error state but we now see devices, we are ready.
        if (_status == ScannerStatus.Error)
        {
            try
            {
                // Simple check: do we have any devices?
                if (_biometricClient.DeviceManager.Devices.Count > 0)
                {
                    _logger.LogInformation("Self-healing: Devices detected, transitioning from Error to Ready.");
                    _status = ScannerStatus.Ready;
                }
            }
            catch
            {
                // Still in error
            }
        }
        return _status;
    }

    public List<string> GetDevices()
    {
        try
        {
            _logger.LogDebug("Refreshing hardware device list...");
            
            _biometricClient.Initialize();
            _biometricClient.DeviceManager.Initialize();

            var devices = _biometricClient.DeviceManager.Devices
                .Select(d => d.DisplayName)
                .ToList();

            _logger.LogInformation("Detected {Count} biometric devices: {DeviceName}.", devices.Count, _biometricClient.DeviceManager.Devices.Select(d => d.DisplayName));
            return devices.Count > 0 ? devices : new List<string> { "No scanners detected" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while scanning for biometric hardware.");
            return new List<string> { "Hardware discovery error" };
        }
    }

    public async Task<BiometricResult> CaptureAsync(FingerCaptureMode mode, string? deviceName = null, CancellationToken ct = default)
    {
        if (_status == ScannerStatus.Error)
            return BiometricResult.Fail("Biometric Engine is in an error state. Check licensing.");

        UpdateStatus(ScannerStatus.Capturing);

        using var subject = CreateSubjectForMode(mode);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        // Use timeout from configuration (defaults to 40s if not set)
        var timeoutSeconds = _settings.NeurotecSdk.CaptureSettings?.TimeoutMs / 1000 ?? 40;
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            _logger.LogInformation("Starting {Mode} acquisition pipeline for device {Device} (Timeout: {Timeout}s)...", mode, deviceName ?? "Auto", timeoutSeconds);

            if (!AssignHardwareToClient(deviceName))
            {
                _logger.LogWarning("Capture aborted: Target hardware '{Device}' not found or busy.", deviceName ?? "Any");
                return BiometricResult.Fail($"Hardware Missing: {deviceName ?? "No scanner detected"}. Please check connections.");
            }

            ApplyExtractionSettings();

            // CRITICAL: Link the cancellation token to the native SDK cancel method
            using var registration = cts.Token.Register(() => 
            {
                _logger.LogWarning("Cancellation triggered. Signaling native SDK to stop...");
                _biometricClient.Cancel();
            });

            // Execute Native Acquisition
            var status = await Task.Run(() => _biometricClient.CreateTemplate(subject), cts.Token);

            _logger.LogInformation("Acquisition completed with status: {Status}", status);

            if (status == NBiometricStatus.Ok)
            {
                return ProcessCaptureResult(subject);
            }

            // If status is Canceled, check if it was due to our internal timeout
            if (status == NBiometricStatus.Canceled)
            {
                if (cts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    return BiometricResult.Fail("Capture Timeout: No finger detected within the time limit.");
                }
                return BiometricResult.Fail("Operation Canceled");
            }

            // Return the raw SDK status name (e.g., "TooManyObjects", "PositionUnknown")
            return BiometricResult.Fail(status.ToString());
        }
        catch (OperationCanceledException)
        {
            return BiometricResult.Fail("Capture Timeout: Operation exceeded the allowed time.");
        }
        catch (AggregateException aggEx)
        {
            var inner = aggEx.Flatten().InnerException;
            _logger.LogError(inner, "SDK reported a fatal error during acquisition.");
            return BiometricResult.Fail($"SDK Fatal Error: {inner?.Message ?? "Unknown"}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure in the SDK acquisition pipeline.");
            return BiometricResult.Fail($"Internal SDK Error: {ex.Message}");
        }
        finally
        {
            _biometricClient.FingerScanner = null;
            UpdateStatus(ScannerStatus.Ready);
        }
    }

    public void StopCapture()
    {
        try
        {
            _logger.LogInformation("Graceful stop requested by user. Cancelling active biometric operation...");
            _biometricClient.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while attempting to stop the capture operation.");
        }
    }

    private bool AssignHardwareToClient(string? deviceName = null)
    {
        try
        {
            // Force a refresh of the device list
            _biometricClient.DeviceManager.Initialize();

            var scanners = _biometricClient.DeviceManager.Devices.OfType<NFingerScanner>().ToList();
            
            if (!scanners.Any())
            {
                _logger.LogWarning("No fingerprint scanners detected in DeviceManager.");
                _biometricClient.FingerScanner = null;
                return false;
            }

            NFingerScanner? selectedScanner = null;

            if (!string.IsNullOrEmpty(deviceName))
            {
                selectedScanner = scanners.FirstOrDefault(d => d.DisplayName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
                if (selectedScanner == null)
                {
                    _logger.LogWarning("Requested device '{Device}' not found. Available: {Available}", deviceName, string.Join(", ", scanners.Select(s => s.DisplayName)));
                }
            }

            // Fallback to first scanner if no specific device requested or found
            selectedScanner ??= scanners.FirstOrDefault();

            if (selectedScanner != null)
            {
                _biometricClient.FingerScanner = selectedScanner;
                _logger.LogInformation("Target hardware assigned: {Device}", selectedScanner.DisplayName);
                return true;
            }

            _biometricClient.FingerScanner = null;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign hardware to biometric client.");
            return false;
        }
    }

    private void ApplyExtractionSettings()
    {
        var settings = _settings.NeurotecSdk.CaptureSettings;
        _biometricClient.FingersReturnBinarizedImage = true;
        _biometricClient.FingersQualityThreshold = (byte)(settings?.QualityThreshold ?? 30);
    }

    private BiometricResult ProcessCaptureResult(NSubject subject)
    {
        // For slaps/multi-finger captures, we want the overall status and individual finger data
        var image = subject.Fingers.FirstOrDefault()?.Image;
        if (image == null) return BiometricResult.Fail("Template created but no valid image found.");

        var fingerScores = new Dictionary<string, int>();
        int totalQuality = 0;
        int fingerCount = 0;

        foreach (var finger in subject.Fingers)
        {
            var fingerQuality = finger.Objects.FirstOrDefault()?.Quality ?? 0;
            var positionName = finger.Position.ToString().ToLower().Replace("plain", "").Trim();
            
            // Clean up names for the UI
            if (positionName.Contains("index")) positionName = "index";
            else if (positionName.Contains("middle")) positionName = "middle";
            else if (positionName.Contains("ring")) positionName = "ring";
            else if (positionName.Contains("little")) positionName = "little";
            else if (positionName.Contains("thumb")) positionName = positionName.Contains("right") ? "right thumb" : "left thumb";

            if (!fingerScores.ContainsKey(positionName))
            {
                fingerScores[positionName] = fingerQuality;
                totalQuality += fingerQuality;
                fingerCount++;
            }
        }

        using var bitmap = image.ToBitmap();
        return BiometricResult.Ok(new BiometricData
        {
            Base64Image = ConvertBitmapToBase64(bitmap),
            QualityScore = fingerCount > 0 ? totalQuality / fingerCount : 0,
            FingerScores = fingerScores,
            CapturedAt = DateTime.UtcNow
        });
    }

    private static string ConvertBitmapToBase64(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    private NSubject CreateSubjectForMode(FingerCaptureMode mode)
    {
        var subject = new NSubject();
        var impressionType = NFImpressionType.LiveScanPlain;

        var position = mode switch
        {
            FingerCaptureMode.RightThumb => NFPosition.RightThumb,
            FingerCaptureMode.LeftThumb => NFPosition.LeftThumb,
            FingerCaptureMode.PlainLeftFourFingers => NFPosition.PlainLeftFourFingers,
            FingerCaptureMode.PlainRightFourFingers => NFPosition.PlainRightFourFingers,
            FingerCaptureMode.TwoThumbs => NFPosition.PlainThumbs,
            _ => NFPosition.Unknown
        };

        subject.Fingers.Add(new NFinger { Position = position, ImpressionType = impressionType });
        return subject;
    }

    private void UpdateStatus(ScannerStatus newStatus)
    {
        _status = newStatus;
        OnStatusChanged?.Invoke(_status);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _biometricClient?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
