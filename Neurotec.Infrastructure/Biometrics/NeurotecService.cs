using Neurotec.Biometrics;
using Neurotec.Biometrics.Client;
using Neurotec.Devices;
using Neurotec.Application.Interfaces;
using Neurotec.Application.DTOs;
using Neurotec.Application.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Specialized;

namespace Neurotec.Infrastructure.Biometrics;

public class NeurotecService : INeurotecService
{
    private readonly NBiometricClient _client;
    public event Action<string>? OnPreviewFrameReceived;
    public event Action? OnDevicesChanged;

    public NeurotecService()
    {
        _client = new NBiometricClient { UseDeviceManager = true };
        _client.DeviceManager.DeviceTypes = NDeviceType.FingerScanner;
        
        // Ensure DeviceManager is initialized to start monitoring hardware
        _client.DeviceManager.Initialize();

        // Real-time device detection via SDK CollectionChanged event
        _client.DeviceManager.Devices.CollectionChanged += OnDevicesCollectionChanged;

        // Hook into the client's property changes to catch the live preview
        _client.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "CurrentBiometric")
            {
                var finger = _client.CurrentBiometric as NFinger;
                if (finger != null)
                {
                    // Subscribe to the finger's image change
                    finger.PropertyChanged += (fs, fe) =>
                    {
                        if (fe.PropertyName == "Image" && finger.Image != null)
                        {
                            try
                            {
                                using var stream = new MemoryStream();
                                finger.Image.ToBitmap().Save(stream, ImageFormat.Png);
                                var base64 = Convert.ToBase64String(stream.ToArray());
                                OnPreviewFrameReceived?.Invoke(base64);
                            }
                            catch { /* Ignore framing errors during capture */ }
                        }
                    };
                }
            }
        };
    }

    private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // When a device is added or removed, notify subscribers immediately
        OnDevicesChanged?.Invoke();
    }

    public bool ObtainLicenses(string server, string components)
    {
        try 
        {
            return Neurotec.Licensing.NLicense.ObtainComponents(server, 5000, components);
        }
        catch { return false; }
    }

    public async Task<NBiometricStatus> CreateTemplateAsync(NSubject subject)
    {
        return await Task.Run(() => _client.CreateTemplate(subject));
    }

    public IEnumerable<string> GetDeviceNames()
    {
        // Re-initialize/Refresh is not strictly needed if we are event-driven, 
        // but it ensures the list is up-to-date for the current call.
        return _client.DeviceManager.Devices
            .Where(d => d.IsAvailable)
            .Select(d => d.DisplayName);
    }

    public bool TrySetScanner(string? deviceName)
    {
        if (deviceName == null)
        {
            _client.FingerScanner = null;
            return true;
        }

        var scanners = _client.DeviceManager.Devices.OfType<NFingerScanner>().ToList();
        
        if (!scanners.Any()) return false;

        NFingerScanner? selected = null;
        if (!string.IsNullOrEmpty(deviceName))
        {
            selected = scanners.FirstOrDefault(s => s.DisplayName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        }
        
        selected ??= scanners.FirstOrDefault();

        if (selected != null)
        {
            _client.FingerScanner = selected;
            return true;
        }

        return false;
    }

    public void Cancel()
    {
        _client.Cancel();
    }

    public void SetQualityThreshold(byte threshold)
    {
        _client.FingersQualityThreshold = threshold;
        _client.FingersReturnBinarizedImage = true;
    }

    public BiometricData? ExtractData(NSubject subject)
    {
        var mainFinger = subject.Fingers.FirstOrDefault();
        if (mainFinger == null) return null;

        var data = new BiometricData
        {
            CapturedAt = DateTime.UtcNow
        };

        if (mainFinger.Image != null)
        {
            using var stream = new MemoryStream();
            mainFinger.Image.ToBitmap().Save(stream, ImageFormat.Png);
            data.Base64Image = Convert.ToBase64String(stream.ToArray());
        }

        int totalIndividualQuality = 0;
        int fingerCount = 0;
        int groupQuality = 0;

        foreach (var finger in subject.Fingers)
        {
            string posName = finger.Position.ToString().ToLower().Replace("plain", "").Trim();
            
            // 1. Identify Slaps (Groups)
            bool isSlap = posName.Contains("fourfingers") || posName.Contains("thumbs");
            
            if (isSlap)
            {
                var attr = finger.Objects.FirstOrDefault();
                if (attr != null && attr.Quality > 0)
                {
                    groupQuality = attr.Quality;
                }
                
                if (string.IsNullOrEmpty(data.Base64Image) && finger.Image != null)
                {
                    using var stream = new MemoryStream();
                    finger.Image.ToBitmap().Save(stream, ImageFormat.Png);
                    data.Base64Image = Convert.ToBase64String(stream.ToArray());
                }
            }

            // 2. Identify individual/segmented fingers
            string handPrefix = posName.Contains("right") ? "right " : "left ";
            string displayName = "";

            if (posName.Contains("index")) displayName = handPrefix + "index";
            else if (posName.Contains("middle")) displayName = handPrefix + "middle";
            else if (posName.Contains("ring")) displayName = handPrefix + "ring";
            else if (posName.Contains("little")) displayName = handPrefix + "little";
            else if (posName.Contains("thumb") && !posName.Contains("thumbs")) 
            {
                displayName = handPrefix + "thumb";
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                var attr = finger.Objects.FirstOrDefault();
                int quality = attr?.Quality ?? 0;

                if (!data.Fingers.ContainsKey(displayName))
                {
                    var detail = new FingerDetail { Score = quality };
                    
                    if (finger.Image != null)
                    {
                        using var imgStream = new MemoryStream();
                        finger.Image.ToBitmap().Save(imgStream, ImageFormat.Png);
                        detail.Image = Convert.ToBase64String(imgStream.ToArray());
                    }

                    data.Fingers[displayName] = detail;
                    totalIndividualQuality += quality;
                    fingerCount++;
                }
            }
        }

        if (groupQuality > 0)
        {
            data.QualityScore = groupQuality;
        }
        else
        {
            data.QualityScore = fingerCount > 0 ? totalIndividualQuality / fingerCount : 0;
        }

        return data;
    }

    public void Dispose()
    {
        // Properly unsubscribe and dispose to prevent memory leaks
        _client.DeviceManager.Devices.CollectionChanged -= OnDevicesCollectionChanged;
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
