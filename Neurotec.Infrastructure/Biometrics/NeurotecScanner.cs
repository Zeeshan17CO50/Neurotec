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

    public ScannerStatus GetStatus() => _status;

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

    public async Task<BiometricResult> CaptureAsync(FingerCaptureMode mode, CancellationToken ct = default)
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
            _logger.LogInformation("Starting {Mode} acquisition pipeline (Timeout: {Timeout}s)...", mode, timeoutSeconds);

            AssignHardwareToClient();
            ApplyExtractionSettings();

            // Execute Native Acquisition
            var status = await Task.Run(() => _biometricClient.CreateTemplate(subject), cts.Token);

            _logger.LogInformation("Acquisition completed with status: {Status}", status);

            if (status == NBiometricStatus.Ok)
            {
                return ProcessCaptureResult(subject);
            }

            return BiometricResult.Fail($"Capture failed: {status}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Capture operation timed out or was cancelled by the user.");
            return BiometricResult.Fail("Capture Timeout: No finger detected on sensor.");
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

    private void AssignHardwareToClient()
    {
        // Automatically select the first available finger scanner
        var scanner = _biometricClient.DeviceManager.Devices.OfType<NFingerScanner>().FirstOrDefault();
        if (scanner != null)
        {
            _biometricClient.FingerScanner = scanner;
            _logger.LogDebug("Target hardware assigned: {Device}", scanner.DisplayName);
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
        var finger = subject.Fingers.FirstOrDefault(f => f.Status == NBiometricStatus.Ok);
        var image = finger?.Image;
        var quality = finger?.Objects.FirstOrDefault()?.Quality ?? 0;

        if (image == null) return BiometricResult.Fail("Template created but no valid image found.");

        using var bitmap = image.ToBitmap();
        return BiometricResult.Ok(new BiometricData
        {
            Base64Image = ConvertBitmapToBase64(bitmap),
            QualityScore = quality,
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
