using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Neurotec.Biometrics;
using Neurotec.Application.Interfaces;
using Neurotec.Application.DTOs;
using Neurotec.Domain.Configuration;
using Neurotec.Domain.Enums;
using Neurotec.Infrastructure.Biometrics;
using FluentAssertions;
using Xunit;
using Neurotec.Devices;

namespace Neurotec.Tests.Biometrics;

public class NeurotecScannerTests
{
    private readonly Mock<IOptions<NeurotecSettings>> _mockOptions;
    private readonly Mock<ILogger<NeurotecScanner>> _mockLogger;
    private readonly Mock<INeurotecService> _mockNeurotecService;
    private readonly NeurotecSettings _settings;

    public NeurotecScannerTests()
    {
        _mockOptions = new Mock<IOptions<NeurotecSettings>>();
        _mockLogger = new Mock<ILogger<NeurotecScanner>>();
        _mockNeurotecService = new Mock<INeurotecService>();

        _settings = new NeurotecSettings
        {
            NeurotecSdk = new NeurotecSdkSettings
            {
                LicenseServer = "/local",
                Components = new List<string> { "FingerExtraction", "FingerScanners" },
                CaptureSettings = new CaptureSettings { QualityThreshold = 70, TimeoutMs = 5000 }
            }
        };

        _mockOptions.Setup(o => o.Value).Returns(_settings);
    }

    #region Initialization & Lifecycle Tests

    [Fact]
    public void Constructor_WithValidArgs_ShouldInitializeSuccessfully()
    {
        _mockNeurotecService.Setup(s => s.ObtainLicenses(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var scanner = CreateScanner();
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        Action act = () => new NeurotecScanner(null!, _mockLogger.Object, _mockNeurotecService.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        Action act = () => new NeurotecScanner(_mockOptions.Object, null!, _mockNeurotecService.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        using var scanner = CreateScanner();
        scanner.Dispose();
        Action act = () => scanner.Dispose();
        act.Should().NotThrow();
    }

    #endregion

    #region Hardware Discovery Tests

    [Fact]
    public void GetDevices_WhenNoHardwareConnected_ShouldReturnNoScannersDetected()
    {
        _mockNeurotecService.Setup(s => s.GetDeviceNames()).Returns(Enumerable.Empty<string>());
        using var scanner = CreateScanner();
        var devices = scanner.GetDevices();
        devices.Should().Contain("No scanners detected");
    }

    [Fact]
    public void GetDevices_WithConnectedHardware_ShouldReturnDeviceNames()
    {
        var expectedDevices = new List<string> { "Scanner A" };
        _mockNeurotecService.Setup(s => s.GetDeviceNames()).Returns(expectedDevices);
        using var scanner = CreateScanner();
        var devices = scanner.GetDevices();
        devices.Should().Contain("Scanner A");
    }

    #endregion

    #region Workflow Tests

    [Fact]
    public async Task CaptureAsync_WhenSpecificDeviceRequestedButMissing_ShouldReturnHardwareMissing()
    {
        _mockNeurotecService.Setup(s => s.TrySetScanner(It.IsAny<string>())).Returns(false);
        using var scanner = CreateScanner();
        var result = await scanner.CaptureAsync(FingerCaptureMode.RightThumb, "Missing Device");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Hardware Missing");
    }

    [Fact]
    public async Task CaptureAsync_WhenCanceled_ShouldReturnError()
    {
        _mockNeurotecService.Setup(s => s.TrySetScanner(It.IsAny<string>())).Returns(true);
        _mockNeurotecService.Setup(s => s.CreateTemplateAsync(It.IsAny<NSubject>()))
            .Returns(Task.FromResult(NBiometricStatus.Canceled));
            
        using var scanner = CreateScanner();
        var result = await scanner.CaptureAsync(FingerCaptureMode.PlainRightFourFingers);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CaptureAsync_WhenSuccess_ShouldReturnData()
    {
        // Arrange
        _mockNeurotecService.Setup(s => s.TrySetScanner(It.IsAny<string>())).Returns(true);
        _mockNeurotecService.Setup(s => s.CreateTemplateAsync(It.IsAny<NSubject>()))
            .Returns(Task.FromResult(NBiometricStatus.Ok));
        _mockNeurotecService.Setup(s => s.ExtractData(It.IsAny<NSubject>()))
            .Returns(new BiometricData { QualityScore = 80, CapturedAt = DateTime.UtcNow });

        using var scanner = CreateScanner();

        // Act
        var result = await scanner.CaptureAsync(FingerCaptureMode.RightThumb);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.QualityScore.Should().Be(80);
    }

    [Fact]
    public async Task CaptureAsync_WhenExtractionFails_ShouldReturnFailure()
    {
        // Arrange
        _mockNeurotecService.Setup(s => s.TrySetScanner(It.IsAny<string>())).Returns(true);
        _mockNeurotecService.Setup(s => s.CreateTemplateAsync(It.IsAny<NSubject>()))
            .Returns(Task.FromResult(NBiometricStatus.Ok));
        _mockNeurotecService.Setup(s => s.ExtractData(It.IsAny<NSubject>()))
            .Returns((BiometricData?)null); // Simulate no finger found

        using var scanner = CreateScanner();

        // Act
        var result = await scanner.CaptureAsync(FingerCaptureMode.RightThumb);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No finger data");
    }

    #endregion

    #region Helpers

    private NeurotecScanner CreateScanner()
    {
        return new NeurotecScanner(_mockOptions.Object, _mockLogger.Object, _mockNeurotecService.Object);
    }

    #endregion
}
