using System.Runtime.Versioning;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Neurotec.Domain.Configuration;
using Neurotec.Domain.Interfaces;
using Neurotec.Infrastructure.Biometrics;

[assembly: SupportedOSPlatform("windows")]

// Ensure the service uses its installation directory for path resolution
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Add detailed logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
// Log to a file we can actually check
string logPath = Path.Combine(AppContext.BaseDirectory, "service_debug.log");
File.AppendAllText(logPath, $"\n\n--- Service Started at {DateTime.Now} ---\n");

// Bind Configuration
builder.Services.Configure<NeurotecSettings>(builder.Configuration);
var settings = builder.Configuration.Get<NeurotecSettings>() ?? new NeurotecSettings();

// Configure Windows Service
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = settings.ServiceSettings.ServiceName;
});

// Register Biometric Scanner (Professional SDK)
builder.Services.AddSingleton<IBiometricScanner, NeurotecScanner>();

// Use configured port (Default to 3000 if not set)
builder.WebHost.UseUrls($"http://*:{settings.ServiceSettings.HttpPort}");

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

try
{
    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // 2. Global Exception Handler Middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var error = new { 
            status = "Error", 
            message = ex.Message, 
            stackTrace = ex.StackTrace,
            innerException = ex.InnerException?.Message 
        };
        await context.Response.WriteAsJsonAsync(error);
    }
});

app.UseDefaultFiles();
    app.UseStaticFiles();

    // Default route: Show status if redirect fails
    app.MapGet("/", () => Results.Content("Neurotec Service is ALIVE. Dashboard is at <a href='/home'>/home</a>", "text/html"));

    // Scope Alignment: Map /home to index.html with explicit path checking
    app.MapGet("/home", () => {
        var filePath = Path.Combine(app.Environment.ContentRootPath, settings.ServiceSettings.WebRoot, "index.html");
        if (!File.Exists(filePath))
        {
            return Results.NotFound($"UI Files Missing! Searched at: {filePath}");
        }
        return Results.File(filePath, "text/html");
    });

    app.MapGet("/api/status", (IBiometricScanner scanner) =>
    {
        return Results.Ok(new
        {
            status = scanner.GetStatus().ToString(),
            isReady = scanner.GetStatus() == Neurotec.Domain.Enums.ScannerStatus.Ready,
            sdkVersion = "Neurotec Biometric 2025.2 (Pro)",
            hardwareDetected = scanner.GetStatus() != Neurotec.Domain.Enums.ScannerStatus.Error,
            servicePath = AppContext.BaseDirectory,
            timestamp = DateTime.UtcNow
        });
    });

    app.MapGet("/api/devices", (IBiometricScanner scanner) =>
    {
        var devices = scanner.GetDevices();
        return Results.Ok(new { devices });
    });

    app.MapPost("/api/capture", async (IBiometricScanner scanner, CancellationToken ct) =>
    {
        var result = await scanner.CaptureAsync(ct);
        
        if (!result.Success)
        {
            return Results.BadRequest(new { success = false, error = result.ErrorMessage });
        }

        return Results.Ok(new 
        { 
            success = true, 
            image = result.Data?.Base64Image, 
            quality = result.Data?.QualityScore,
            timestamp = result.Data?.CapturedAt
        });
    });

    app.Run();
}
catch (Exception ex)
{
    // Log fatal errors to the Event Log so they can be seen in Event Viewer
    if (WindowsServiceHelpers.IsWindowsService())
    {
        using var eventLog = new System.Diagnostics.EventLog("Application");
        eventLog.Source = "NeurotecBiometricService";
        eventLog.WriteEntry($"Service failed to start: {ex.Message}\n{ex.StackTrace}", System.Diagnostics.EventLogEntryType.Error);
    }
    throw;
}
