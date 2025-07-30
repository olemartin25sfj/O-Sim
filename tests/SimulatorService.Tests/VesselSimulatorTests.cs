using SimulatorService;
using Xunit;

public class VesselSimulatorTests
{
    [Fact]
    public void Tick_AdancesPosition_WhenHeadingIsZero()
    {
        var vessel = new VesselSimulatorTests();
        vessel.Tick(1.0);

        Assert.True(vessel.x > 0);
        Assert.Equal(0, vessel.Y, 3);
    }

    [Fact]
    public void ApplyRudder_ChangesHeading()
    {
        var vessel = new VesselSimulator();
        vessel.ApplyRudder(10.0);

        Assert.Equal(10.0, vessel.Heading);
    }
}