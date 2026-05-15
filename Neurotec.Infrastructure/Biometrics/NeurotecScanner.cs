using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neurotec.Biometrics;
using Neurotec.Application.Interfaces;
using Neurotec.Application.Models;
using Neurotec.Domain.Configuration;
using Neurotec.Domain.Enums;
using System.Runtime.Versioning;

namespace Neurotec.Infrastructure.Biometrics;

/// <summary>
/// Enterprise-grade implementation of the Neurotec Biometric SDK (VeriFinger/MegaMatcher).
/// Handles hardware lifecycle, licensing, and high-precision biometric acquisition.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class NeurotecScanner : IBiometricScanner, IDisposable
{
    private readonly INeurotecService _neurotecService;
    private readonly NeurotecSettings _settings;
    private readonly ILogger<NeurotecScanner> _logger;
    private readonly PreviewState _previewState;
    private ScannerStatus _status = ScannerStatus.Ready;
    private bool _isDisposed;
    private bool _isLicensed;

    public event Action<ScannerStatus>? OnStatusChanged;

    public NeurotecScanner(
        IOptions<NeurotecSettings> settings, 
        ILogger<NeurotecScanner> logger,
        INeurotecService neurotecService,
        PreviewState previewState)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        if (neurotecService == null) throw new ArgumentNullException(nameof(neurotecService));
        if (previewState == null) throw new ArgumentNullException(nameof(previewState));

        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger;
        _neurotecService = neurotecService;
        _previewState = previewState;

        _neurotecService.OnPreviewFrameReceived += (base64) => 
        {
            _previewState.LatestFrame = base64;
        };

        // Subscribe to real-time hardware changes
        _neurotecService.OnDevicesChanged += HandleDevicesChanged;

        _logger.LogInformation("Initializing Neurotec Scanner Service at: {Path}", AppContext.BaseDirectory);
        
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
            
            _isLicensed = _neurotecService.ObtainLicenses(server, components);
            
            if (!_isLicensed)
            {
                _logger.LogError("CRITICAL: Failed to obtain required Neurotec licenses ({Components}). Hardware will be inaccessible.", components);
                _status = ScannerStatus.Error;
            }
            else
            {
                _logger.LogInformation("Neurotec licenses obtained successfully.");
            }
        }
        catch (Exception ex) when (ex.Message.Contains("already initialized"))
        {
            _logger.LogInformation("Neurotec SDK components already initialized in this process.");
            _isLicensed = true; 
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error during Neurotec SDK initialization.");
            _status = ScannerStatus.Error;
            _isLicensed = false;
        }
    }

    private void HandleDevicesChanged()
    {
        _logger.LogInformation("Real-time hardware configuration change detected. Updating status...");
        
        // If we are currently capturing and the device is removed, we should signal a stop
        if (_status == ScannerStatus.Capturing)
        {
            var devices = _neurotecService.GetDeviceNames();
            if (!devices.Any())
            {
                _logger.LogWarning("Active device disconnected during capture! Signaling cancellation.");
                _neurotecService.Cancel();
            }
        }

        // Trigger a status change event to notify subscribers (like the API/UI layer)
        OnStatusChanged?.Invoke(GetStatus());
    }

    public ScannerStatus GetStatus()
    {
        if (!_isLicensed) return ScannerStatus.Error;

        // Dynamic status check: If we think we are ready but no hardware is present, 
        // we are technically in a "waiting" or "ready" state, but the API layer 
        // uses GetDevices() to show the "Disconnected" UI.
        
        // However, if we were in an Error state (licensing) and now have devices, we self-heal.
        if (_status == ScannerStatus.Error)
        {
            try
            {
                if (_neurotecService.GetDeviceNames().Any())
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
            
            var devices = _neurotecService.GetDeviceNames().ToList();

            _logger.LogInformation("Detected {Count} biometric devices.", devices.Count);
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
                _neurotecService.Cancel();
            });

            // Execute Native Acquisition
            var status = await _neurotecService.CreateTemplateAsync(subject);

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
            _previewState.LatestFrame = null;
            _neurotecService.TrySetScanner(null);
            UpdateStatus(ScannerStatus.Ready);
        }
    }

    public void StopCapture()
    {
        try
        {
            _logger.LogInformation("Graceful stop requested by user. Cancelling active biometric operation...");
            _neurotecService.Cancel();
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
            if (_neurotecService.TrySetScanner(deviceName))
            {
                _logger.LogInformation("Target hardware assigned: {Device}", deviceName ?? "Auto-Selected");
                return true;
            }

            _logger.LogWarning("Failed to assign hardware.");
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
        _neurotecService.SetQualityThreshold((byte)(settings?.QualityThreshold ?? 30));
    }

    private BiometricResult ProcessCaptureResult(NSubject subject)
    {
        try
        {
            var data = _neurotecService.ExtractData(subject);
            
            if (data == null)
            {
                return BiometricResult.Fail("No finger data captured.");
            }

            return BiometricResult.Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process biometric capture result.");
            return BiometricResult.Fail("Data Extraction Error: " + ex.Message);
        }
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
        
        // Unsubscribe from events to prevent memory leaks
        _neurotecService.OnDevicesChanged -= HandleDevicesChanged;
        
        _neurotecService?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
