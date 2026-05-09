namespace Neurotec.Domain.Entities;

public class BiometricData
{
    public string Base64Image { get; set; } = string.Empty;
    public int QualityScore { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
