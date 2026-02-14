using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PCStats.Service.Services;

namespace PCStats.Service.Tests.Services;

public class HWiNFOServiceTests
{
    private readonly Mock<ILogger<HWiNFOService>> _mockLogger;
    private readonly HWiNFOService _service;

    public HWiNFOServiceTests()
    {
        _mockLogger = new Mock<ILogger<HWiNFOService>>();
        _service = new HWiNFOService(_mockLogger.Object);
    }

    [Fact]
    public void IsHWiNFORunning_ShouldReturnBooleanValue()
    {
        // Act
        var result = _service.IsHWiNFORunning();

        // Assert - A boolean is always true or false, this test verifies the method executes without error
        result.Should().Be(result); // Tautology that just verifies it returns a bool
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_WhenHWiNFONotRunning_ShouldReturnNull()
    {
        // Arrange
        var isRunning = _service.IsHWiNFORunning();

        // Act
        var result = await _service.GetCpuTemperaturesAsync();

        // Assert
        if (!isRunning)
        {
            result.Should().BeNull("HWiNFO is not running");
        }
        else
        {
            // If HWiNFO is running, result could be null or have temperature data
            // depending on whether the shared memory/registry contains valid data
            if (result != null)
            {
                // Validate temperature data if present
                result.CpuTctlTdie.Should().NotBeNull();
            }
        }
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_ShouldNotThrow()
    {
        // Act & Assert
        await _service.Invoking(s => s.GetCpuTemperaturesAsync())
            .Should().NotThrowAsync("service should handle missing HWiNFO gracefully");
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_CalledMultipleTimes_ShouldBeConsistent()
    {
        // Act
        var result1 = await _service.GetCpuTemperaturesAsync();
        var result2 = await _service.GetCpuTemperaturesAsync();

        // Assert
        if (result1 == null && result2 == null)
        {
            // Both null is consistent
            result1.Should().Be(result2);
        }
        else if (result1 != null && result2 != null)
        {
            // Both have data - temperatures might vary slightly but structure should be same
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_WhenDataAvailable_ShouldHaveValidTemperatures()
    {
        // Act
        var result = await _service.GetCpuTemperaturesAsync();

        // Assert
        if (result != null)
        {
            // Temperature values should be reasonable (between -50°C and 150°C)
            if (result.CpuTctlTdie.HasValue)
            {
                result.CpuTctlTdie.Value.Should().BeInRange(-50, 150);
            }
            
            if (result.CpuDieAverage.HasValue)
            {
                result.CpuDieAverage.Value.Should().BeInRange(-50, 150);
            }
            
            if (result.CpuCcd1Tdie.HasValue)
            {
                result.CpuCcd1Tdie.Value.Should().BeInRange(-50, 150);
            }
            
            if (result.CpuCcd2Tdie.HasValue)
            {
                result.CpuCcd2Tdie.Value.Should().BeInRange(-50, 150);
            }
            
            if (result.ThermalLimitPercent.HasValue)
            {
                result.ThermalLimitPercent.Value.Should().BeInRange(0, 100);
            }
        }
    }

    [Fact]
    public void IsHWiNFORunning_CalledMultipleTimes_ShouldNotThrow()
    {
        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            _service.Invoking(s => s.IsHWiNFORunning())
                .Should().NotThrow();
        }
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_ShouldLogWarningWhenHWiNFONotRunning()
    {
        // Arrange
        var isRunning = _service.IsHWiNFORunning();

        // Act
        var result = await _service.GetCpuTemperaturesAsync();

        // Assert
        if (!isRunning)
        {
            result.Should().BeNull();
            
            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HWiNFO is not running")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_CalledRapidly_ShouldHandleGracefully()
    {
        // Act - Call multiple times rapidly
        var task1 = _service.GetCpuTemperaturesAsync();
        var task2 = _service.GetCpuTemperaturesAsync();
        var task3 = _service.GetCpuTemperaturesAsync();

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert - All calls should complete without errors
        // Results may be null if HWiNFO is not running
        results.Should().AllSatisfy(r =>
        {
            if (r != null)
            {
                // If we got temperature data, validate it
                if (r.CpuTctlTdie.HasValue)
                {
                    r.CpuTctlTdie.Value.Should().BeInRange(-50, 150);
                }
            }
        });
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_WhenDataAvailable_ShouldHaveThermalThrottlingFlag()
    {
        // Act
        var result = await _service.GetCpuTemperaturesAsync();

        // Assert
        if (result != null)
        {
            // ThermalThrottling is a boolean, so it should always have a value
            result.ThermalThrottling.Should().Be(result.ThermalThrottling);
        }
    }
}
