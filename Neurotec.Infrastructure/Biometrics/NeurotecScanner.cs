using Microsoft.Extensions.Options;
using Neurotec.Biometrics;
using Neurotec.Biometrics.Client;
using Neurotec.Devices;
using Neurotec.Domain.Configuration;
using Neurotec.Domain.Entities;
using Neurotec.Domain.Enums;
using Neurotec.Domain.Interfaces;
using Neurotec.Licensing;
using Neurotec.Plugins;
using System.Drawing;
using System.Drawing.Imaging;
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
            NDeviceManager.PluginManager.PluginSearchPath = appRoot; 
            NDeviceManager.PluginManager.Refresh();
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
                //if (plugin.FileName.Contains("Mantra") || plugin.FileName.Contains("Tatvik") || 
                //    plugin.FileName.Contains("CrossMatchLScan") || plugin.FileName.Contains("CrossMatchLScan"))
                //{
                    string errorInfo = plugin.Error != null ? $" | Error: {plugin.Error.Message}" : "";
                    Console.WriteLine($"[Neurotec SDK]: Plugin: {plugin.FileName} | State: {plugin.State}{errorInfo}");
                //}
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
        var tempdevices = _biometricClient.DeviceManager.Devices;
        try
        {
            // Log exact plugin status on every API call for diagnostics
            foreach (var plugin in NDeviceManager.PluginManager.Plugins)
            {
                //if (plugin.FileName.Contains("Mantra") || plugin.FileName.Contains("Tatvik") || 
                //    plugin.FileName.Contains("CrossMatch") || plugin.FileName.Contains("CrossMatchLScan"))
                //{
                    // Only log if not plugged (to reduce noise) or for initial check
                    //if (plugin.State != NPluginState.Plugged)
                    //{
                        string errorInfo = plugin.Error != null ? $" | Error: {plugin.Error.Message}" : "";
                        Console.WriteLine($"[Neurotec SDK]: Plugin FOUND: {plugin.FileName} | State: {plugin.State}{errorInfo}");
                    //}
                //}
            }

            _biometricClient.BiometricTypes = NBiometricType.Finger;
            
            // Perform a "Deep Scan" by temporarily allowing all device types
            var originalTypes = _biometricClient.DeviceManager.DeviceTypes;
            _biometricClient.DeviceManager.DeviceTypes = NDeviceType.Any;
            
            var deviceList = _biometricClient.DeviceManager.Devices;
            
            if (deviceList.Count > 0)
            {
                Console.WriteLine($"[Neurotec SDK]: Hardware scan finished. Raw Count: {deviceList.Count}");
            }
            
            foreach (NDevice device in deviceList)
            {
                // Log detailed classification for diagnostics
                Console.WriteLine($"[Neurotec SDK]: DEVICE DETECTED -> {device.DisplayName} | Make: {device.Make} | Type: {device.DeviceType}");
                
                // Add to list if it's a finger scanner, or explicitly contains key brands
                // Fix: Include "Cross Match" with space to match actual hardware string
                bool isCompatible = device.DeviceType == NDeviceType.FingerScanner || 
                                   device.DisplayName.Contains("Mantra") || 
                                   device.DisplayName.Contains("CrossMatch") || 
                                   device.DisplayName.Contains("CrossMatchLScan") || 
                                   device.DisplayName.Contains("Patrol");

                if (isCompatible)
                {
                    devices.Add($"{device.DisplayName} ({device.Make})");
                }
            }

            // Restore original filtering
            _biometricClient.DeviceManager.DeviceTypes = originalTypes;
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("already initialized"))
                Console.WriteLine($"[Neurotec SDK Error]: {ex.Message}");
        }

        return devices.Count > 0 ? devices : new List<string> { "No Neurotec-compatible scanners detected" };
    }

    public async Task<BiometricResult> CaptureAsync(CancellationToken ct = default)
    {
        if (_status == ScannerStatus.Error)
        {
            return BiometricResult.Fail("SDK not initialized or license missing.");
        }

        UpdateStatus(ScannerStatus.Capturing);
        
        using var subject = new NSubject();
        using var finger = new NFinger();
        subject.Fingers.Add(finger);

        try
        {
            // Configure capture settings from appsettings
            _biometricClient.FingersReturnBinarizedImage = true;
            _biometricClient.FingersQualityThreshold = (byte)_settings.NeurotecSdk.CaptureSettings.QualityThreshold;

            // Start capture
            var status = await Task.Run(() => _biometricClient.CreateTemplate(subject), ct);

            if (status == NBiometricStatus.Ok)
            {
                // Extract the image (Neurotec NImage to Base64)
                using var nImage = finger.Image;
                if (nImage != null)
                {
                    using var bitmap = nImage.ToBitmap();
                    string base64 = BitmapToBase64(bitmap);

                    return BiometricResult.Ok(new BiometricData
                    {
                        Base64Image = base64,
                        QualityScore = finger.Objects[0].Quality,
                        CapturedAt = DateTime.UtcNow
                    });
                }
            }

            return BiometricResult.Fail($"Capture failed with status: {status}");
        }
        catch (OperationCanceledException)
        {
            return BiometricResult.Fail("Capture cancelled by user.");
        }
        catch (Exception ex)
        {
            return BiometricResult.Fail($"Neurotec SDK Error: {ex.Message}");
        }
        finally
        {
            UpdateStatus(ScannerStatus.Ready);
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
