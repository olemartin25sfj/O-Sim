using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimulatorService;
namespace SimulatorService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly SimulatorEngine _simulator;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _simulator = new SimulatorEngine();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)

    {
        var sw = new Stopwatch();
        sw.Start();

        var lastUpdate = sw.Elapsed;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = sw.Elapsed;
            var delta = now - lastUpdate;
            lastUpdate = now;

            _simulator.Update(delta);

            var state = _simulator.GetCurrentState();
            _logger.LogInformation("Lat: {Lat:F6}, Lon: {Lon:F6}, Head: {Head:F1}, Speed: {Speed:F1}",
            state.Latitude, state.Longitude, state.Heading, state.Speed);

            await Task.Delay(100, stoppingToken);
        }
    }
}
