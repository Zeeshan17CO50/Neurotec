using System.Collections.Generic;

namespace Neurotec.Application.DTOs;

public class BiometricData
{
    public string Base64Image { get; set; } = string.Empty;
    public int QualityScore { get; set; }
    
    // Nested structure as requested: { "index": { "score": 80, "image": "..." } }
    public Dictionary<string, FingerDetail> Fingers { get; set; } = new();
    
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
