using Neurotec.Application.Models;
using Neurotec.Domain.Enums;

namespace Neurotec.Application.Interfaces;

public interface IBiometricScanner
{
    Task<BiometricResult> CaptureAsync(FingerCaptureMode mode, string? deviceName = null, CancellationToken ct = default);
    void StopCapture();
    List<string> GetDevices();
    ScannerStatus GetStatus();
    event Action<ScannerStatus> OnStatusChanged;
}
