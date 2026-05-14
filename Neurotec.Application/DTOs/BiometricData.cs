namespace Neurotec.Application.DTOs;

public class BiometricData
{
    public string Base64Image { get; set; } = string.Empty;
    public int QualityScore { get; set; }
    public Dictionary<string, int> FingerScores { get; set; } = new();
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
