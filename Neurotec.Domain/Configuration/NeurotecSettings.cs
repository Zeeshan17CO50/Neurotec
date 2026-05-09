namespace Neurotec.Domain.Configuration;

public class NeurotecSettings
{
    public ServiceSettings ServiceSettings { get; set; } = new();
    public NeurotecSdkSettings NeurotecSdk { get; set; } = new();
}

public class ServiceSettings
{
    public int HttpPort { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string WebRoot { get; set; } = string.Empty;
}

public class NeurotecSdkSettings
{
    public string LicenseKey { get; set; } = string.Empty;
    public string LicenseServer { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
    public string DatabasePassword { get; set; } = string.Empty;
    public List<string> Components { get; set; } = new();
    public CaptureSettings CaptureSettings { get; set; } = new();
    public DeviceSettings DeviceSettings { get; set; } = new();
}

public class CaptureSettings
{
    public int TimeoutMs { get; set; }
    public int QualityThreshold { get; set; }
    public int MaxRetries { get; set; }
}

public class DeviceSettings
{
    public string ScannerModel { get; set; } = string.Empty;
    public int Resolution { get; set; }
    public bool AutoDetect { get; set; }
    public int DefaultWidth { get; set; }
    public int DefaultHeight { get; set; }
}
