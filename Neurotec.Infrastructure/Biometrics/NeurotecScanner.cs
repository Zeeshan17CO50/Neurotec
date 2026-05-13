using Microsoft.Extensions.Options;
using Neurotec.Biometrics;
using Neurotec.Biometrics.Client;
using Neurotec.Devices;
using Neurotec.Domain.Configuration;
using Neurotec.Domain.Entities;
using Neurotec.Domain.Enums;
using Neurotec.Domain.Interfaces;
using Neurotec.Infrastructure.Biometrics.Helpers;
using Neurotec.Licensing;
using Neurotec.Plugins;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq.Expressions;
using System.Runtime.Versioning;

namespace Neurotec.Infrastructure.Biometrics;

/// <summary>
/// Professional implementation using the actual Neurotec SDK (VeriFinger/MegaMatcher).
/// Requires Neurotec SDK DLLs in the libs/ folder and valid trial/pro licenses.
/// </summary>
[SupportedOSPlatform("windows")]
public class NeurotecScanner : IBiometricScanner, IDisposable
{
    private readonly NBiometricClient _biometricClient;
    private readonly NeurotecSettings _settings;
    private ScannerStatus _status = ScannerStatus.Ready;
    private bool _isDisposed;

    public event Action<ScannerStatus>? OnStatusChanged;

    public NeurotecScanner(IOptions<NeurotecSettings> settings)
    {
        _settings = settings.Value;
        
        // 1. FINAL FIX: Set search path to Root only (where we moved the DLLs)
        string appRoot = AppContext.BaseDirectory;

        try 
        { 
            //NDeviceManager.PluginManager.PluginSearchPath = appRoot; 
            //NDeviceManager.PluginManager.Refresh();
            Console.WriteLine($"[Neurotec SDK]: Plugin Search Path configured at: {appRoot}");
        } 
        catch (Exception ex) { Console.WriteLine($"[Neurotec SDK]: Plugin Path Error: {ex.Message}"); }

        _biometricClient = new NBiometricClient { UseDeviceManager = true };
        
        InitializeSdk();
    }

    private void InitializeSdk()
    {
        try
        {
            // Set status to ready early, as we'll handle minor init errors gracefully
            _status = ScannerStatus.Ready;

            string components = "Biometrics.FingerExtraction,Devices.FingerScanners,Biometrics.FingerQualityAssessment";
            string server = _settings.NeurotecSdk.LicenseServer ?? "/local";
            bool obtained = NLicense.ObtainComponents(server, 5000, components);
            
            Console.WriteLine($"[Neurotec SDK]: Licensing -> Server: {server} | Obtained: {obtained}");
            
            if (!obtained)
            {
                Console.WriteLine("[Neurotec SDK]: WARNING - Failed to obtain core licenses. Hardware detection will fail.");
            }
            
            // Log exactly what's happening with plugins
            foreach (var plugin in NDeviceManager.PluginManager.Plugins)
            {
                if (plugin.FileName.Contains("Mantra") || plugin.FileName.Contains("Tatvik"))
                {
                    Console.WriteLine($"[Neurotec SDK]: Plugin: {plugin.FileName} | State: {plugin.State}");
                }
            }
            
            // Pre-configure Device Manager - Wrap in try to avoid "Already Initialized" crash
            try { _biometricClient.DeviceManager.DeviceTypes = NDeviceType.FingerScanner; } catch { }
        }
        catch (Exception ex)
        {
            // If it's already initialized, we are actually good to go
            if (ex.Message.Contains("already initialized")) return;

            Console.WriteLine($"[Neurotec SDK Init Error]: {ex.Message}");
            _status = ScannerStatus.Error;
        }
    }

    public ScannerStatus GetStatus() => _status;

    public List<string> GetDevices()
    {
        var devices = new List<string>();

        try
        {
            Console.WriteLine("========== DEVICE SCAN ==========");

            // IMPORTANT
            _biometricClient.Initialize();

            // IMPORTANT
            _biometricClient.DeviceManager.Initialize();

            _biometricClient.DeviceManager.DeviceTypes =
                NDeviceType.FingerScanner;

            Console.WriteLine("========== PLUGINS ==========");

            foreach (var plugin in NDeviceManager.PluginManager.Plugins)
            {
                if (plugin.FileName.Contains("CrossMatch"))
                {
                    Console.WriteLine($"Plugin : {plugin.FileName}");
                    Console.WriteLine($"State  : {plugin.State}");

                    if (plugin.Error != null)
                    {
                        Console.WriteLine($"Error  : {plugin.Error.Message}");
                    }
                }
            }

            Console.WriteLine("========== DEVICES ==========");

            foreach (NDevice device in _biometricClient.DeviceManager.Devices)
            {
                Console.WriteLine($"Name : {device.DisplayName}");
                Console.WriteLine($"Type : {device.DeviceType}");
                Console.WriteLine($"Make : {device.Make}");

                devices.Add(device.DisplayName);
            }

            Console.WriteLine($"TOTAL DEVICES : {devices.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("DEVICE ERROR:");
            Console.WriteLine(ex.ToString());
        }

        return devices.Count > 0
            ? devices
            : new List<string> { "No scanners detected" };
    }

    public async Task<BiometricResult> CaptureAsync(FingerCaptureMode mode, CancellationToken ct = default)
    {
        if (_status == ScannerStatus.Error)
            return BiometricResult.Fail("SDK not initialized or licensing failed.");

        UpdateStatus(ScannerStatus.Capturing);

        // 1. Initialize Multimodal Subject based on Selected Mode
        using var subject = CreateSubjectForMode(mode);
        
        // 2. Setup high-precision timeout (40s for professional acquisition)
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(40));

        try
        {
            Console.WriteLine($"[DEBUG]: --- Initializing {mode} Acquisition Pipeline ---");

            // 3. Ensure Device Manager is refreshed
            _biometricClient.DeviceManager.Initialize();
            
            // 4. Assign hardware scanners
            ConfigureHardwareForCapture();

            // 5. Configure Professional Extraction & Quality Settings
            _biometricClient.FingersReturnBinarizedImage = true;
            _biometricClient.FingersQualityThreshold = 30;

            Console.WriteLine($"[DEBUG]: Calling CreateTemplate (Native Acquisition)...");
            
            // 6. Execute Native CreateTemplate
            var status = await Task.Run(() => _biometricClient.CreateTemplate(subject));

            Console.WriteLine($"[DEBUG]: Acquisition Finished. Status: {status}");

            if (status == NBiometricStatus.Ok)
            {
                // Process results - Return the first high-quality image found in the subject
                var fingerResult = subject.Fingers.FirstOrDefault(f => f.Status == NBiometricStatus.Ok);
                var palmResult = subject.Palms.FirstOrDefault(p => p.Status == NBiometricStatus.Ok);

                var imageSource = fingerResult?.Image ?? palmResult?.Image;
                var quality = fingerResult?.Objects.FirstOrDefault()?.Quality ?? palmResult?.Objects.FirstOrDefault()?.Quality ?? 0;

                if (imageSource != null)
                {
                    using var bitmap = imageSource.ToBitmap();
                    return BiometricResult.Ok(new BiometricData
                    {
                        Base64Image = BitmapToBase64(bitmap),
                        QualityScore = quality,
                        CapturedAt = DateTime.UtcNow
                    });
                }
            }

            return BiometricResult.Fail($"Capture failed or timed out. Status: {status}");
        }
        catch (OperationCanceledException)
        {
            return BiometricResult.Fail("Capture Timeout: No finger detected on sensor.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL]: SDK Pipeline Crash: {ex}");
            return BiometricResult.Fail($"SDK Error: {ex.Message}");
        }
        finally
        {
            _biometricClient.FingerScanner = null;
            _biometricClient.PalmScanner = null;
            UpdateStatus(ScannerStatus.Ready);
        }
    }

    private NSubject CreateSubjectForMode(FingerCaptureMode mode)
    {
        var subject = new NSubject();
        
        switch (mode)
        {
            case FingerCaptureMode.RightThumb:
                subject.Fingers.Add(new NFinger { Position = NFPosition.RightThumb, ImpressionType = NFImpressionType.LiveScanPlain });
                break;
            case FingerCaptureMode.LeftThumb:
                subject.Fingers.Add(new NFinger { Position = NFPosition.LeftThumb, ImpressionType = NFImpressionType.LiveScanPlain });
                break;
            case FingerCaptureMode.PlainLeftFourFingers:
                AddFingersToSubject(subject, new[] { NFPosition.PlainLeftFourFingers });
                break;
            case FingerCaptureMode.PlainRightFourFingers:
                AddFingersToSubject(subject, new[] { NFPosition.PlainRightFourFingers });
                break;
        }
        return subject;
    }

    private void AddFingersToSubject(NSubject subject, NFPosition[] positions)
    {
        foreach (var pos in positions)
        {
            subject.Fingers.Add(new NFinger { Position = pos, ImpressionType = NFImpressionType.LiveScanPlain });
        }
    }

    private void ConfigureHardwareForCapture()
    {
        var fingerScanner = _biometricClient.DeviceManager.Devices.OfType<NFingerScanner>().FirstOrDefault();
        var palmScanner = _biometricClient.DeviceManager.Devices.OfType<NPalmScanner>().FirstOrDefault();

        if (fingerScanner != null)
        {
            _biometricClient.FingerScanner = fingerScanner;
            Console.WriteLine($"[DEBUG]: Finger Scanner Attached: {fingerScanner.DisplayName}");
        }

        if (palmScanner != null)
        {
            _biometricClient.PalmScanner = palmScanner;
            Console.WriteLine($"[DEBUG]: Palm Scanner Attached: {palmScanner.DisplayName}");
        }
    }

    private string BitmapToBase64(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    private void UpdateStatus(ScannerStatus newStatus)
    {
        _status = newStatus;
        OnStatusChanged?.Invoke(_status);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _biometricClient?.Dispose();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
