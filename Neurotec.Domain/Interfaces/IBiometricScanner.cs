using Neurotec.Domain.Entities;
using Neurotec.Domain.Enums;

namespace Neurotec.Domain.Interfaces;

public interface IBiometricScanner
{
    Task<BiometricResult> CaptureAsync(FingerCaptureMode mode, CancellationToken ct = default);
    void StopCapture();
    List<string> GetDevices();
    ScannerStatus GetStatus();
    event Action<ScannerStatus> OnStatusChanged;
}

public class BiometricResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public BiometricData? Data { get; set; }

    public static BiometricResult Ok(BiometricData data) => new() { Success = true, Data = data };
    public static BiometricResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
