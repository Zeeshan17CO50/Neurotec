using Neurotec.Biometrics;
using Neurotec.Biometrics.Client;
using Neurotec.Devices;
using Neurotec.Application.Interfaces;
using Neurotec.Application.DTOs;
using System.Drawing;
using System.Drawing.Imaging;

namespace Neurotec.Infrastructure.Biometrics;

public class NeurotecService : INeurotecService
{
    private readonly NBiometricClient _client;

    public NeurotecService()
    {
        _client = new NBiometricClient { UseDeviceManager = true };
        _client.DeviceManager.DeviceTypes = NDeviceType.FingerScanner;
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
        _client.DeviceManager.Initialize();
        return _client.DeviceManager.Devices.Select(d => d.DisplayName);
    }

    public bool TrySetScanner(string? deviceName)
    {
        _client.DeviceManager.Initialize();
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
            string groupName = finger.Position.ToString().ToLower().Replace("plain", "").Trim();
            
            // Check if this is a slap group (4 fingers or 2 thumbs)
            if (groupName.Contains("fourfingers") || groupName.Contains("thumbs"))
            {
                // The SDK provides a quality for the entire slap image
                groupQuality = finger.Objects.FirstOrDefault()?.Quality ?? 0;
                
                if (!data.FingerScores.ContainsKey(groupName))
                {
                    data.FingerScores[groupName] = groupQuality;
                }
            }

            foreach (var obj in finger.Objects)
            {
                string posName = obj.Position.ToString().ToLower().Replace("plain", "").Trim();
                int quality = obj.Quality;

                string displayName = posName;
                if (posName.Contains("index")) displayName = "index";
                else if (posName.Contains("middle")) displayName = "middle";
                else if (posName.Contains("ring")) displayName = "ring";
                else if (posName.Contains("little")) displayName = "little";
                else if (posName.Contains("thumb")) 
                {
                    if (posName.Contains("right")) displayName = "right thumb";
                    else if (posName.Contains("left")) displayName = "left thumb";
                    else continue; 
                }

                if (!data.FingerScores.ContainsKey(displayName))
                {
                    data.FingerScores[displayName] = quality;
                    totalIndividualQuality += quality;
                    fingerCount++;
                }
            }
        }

        // PRECEDENCE RULE:
        // 1. Use the SDK's calculated Group Quality if available (e.g., the 82 in your screenshot)
        // 2. Fall back to manual average of individual fingers if no group score exists
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
        _client?.Dispose();
    }
}
