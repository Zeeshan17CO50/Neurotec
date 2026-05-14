using Neurotec.Biometrics;
using Neurotec.Application.DTOs;

namespace Neurotec.Application.Interfaces;

/// <summary>
/// Abstraction for the Neurotec native SDK to allow for unit testing without hardware.
/// </summary>
public interface INeurotecService : IDisposable
{
    bool ObtainLicenses(string server, string components);
    Task<NBiometricStatus> CreateTemplateAsync(NSubject subject);
    IEnumerable<string> GetDeviceNames();
    bool TrySetScanner(string? deviceName);
    void Cancel();
    void SetQualityThreshold(byte threshold);
    BiometricData? ExtractData(NSubject subject);
}
