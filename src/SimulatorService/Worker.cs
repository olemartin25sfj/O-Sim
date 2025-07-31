using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimulatorService.Services;
namespace SimulatorService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly SimulatorEngine _engine;
    private readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(1);

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _engine = new SimulatorEngine();

        // Startverdi for testformål
        _engine.SetDesiredHeading(45.0);
        _engine.SetDesiredSpeed(10.0);
        _engine.SetEnvironment(windSpeed: 2.0, windDirection: 180.0, currentSpeed: 1.0, currentDirection: 90.0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _engine.Update(_tickInterval);

            _logger.LogInformation("Time: {time}", DateTimeOffset.Now);
            _logger.LogInformation("Posisjon: {lat:F4}, {lon:F4}", _engine.Latitude, _engine.Longitude);
            _logger.LogInformation("Heading: {heading:F1}°  Fart: {speed:F1} knop", _engine.Heading, _engine.Speed);
            _logger.LogInformation("---------------------------------------------------");

            await Task.Delay(_tickInterval, stoppingToken);
        }
    }
}