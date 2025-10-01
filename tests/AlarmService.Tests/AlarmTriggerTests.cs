using Xunit;

namespace AlarmService.Tests;

public class AlarmTriggerTests
{
    [Fact]
    public void ShouldTriggerAlarm_WhenVesselSpeedExceedsThreshold()
    {
        // Arrange
        double speedThreshold = 10.0;
        double actualSpeed = 15.0;

        // Act
        bool shouldTrigger = AlarmService.AlarmEvaluator.ShouldTrigger(actualSpeed, speedThreshold);

        // Assert
        Assert.True(shouldTrigger, "Alarm should trigger when speed exceeds threshold");
    }
}