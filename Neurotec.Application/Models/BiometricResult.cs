using Neurotec.Application.DTOs;

namespace Neurotec.Application.Models;

public class BiometricResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public BiometricData? Data { get; set; }

    public static BiometricResult Ok(BiometricData data) => new() { Success = true, Data = data };
    public static BiometricResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
