using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimulatorService;

namespace SimulatorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly SimulatorEngine _engine;
        private readonly AutopilotService _autopilot;
        private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(10); // 10 ms tick for rask simulering

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _engine = new SimulatorEngine();
            _autopilot = new AutopilotService(_engine);

            // Sett startposisjon (f.eks. Horten)
            _engine.SetInitialPosition(59.4067, 10.4899);

            // Sett miljøforhold (vind, strøm)
            _engine.SetEnvironment(
                windSpeed: 2.0,
                windDirection: 180.0,
                currentSpeed: 1.0,
                currentDirection: 90.0);

            // Sett destinasjon via autopilot
            _autopilot.SetDestination(59.4344, 10.6574);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _autopilot.Update();
                _engine.Update(_tickInterval);


                // Logg posisjon, heading, fart
                _logger.LogInformation("Tid: {time}", DateTimeOffset.Now);
                _logger.LogInformation("Posisjon: {lat:F5}, {lon:F5}", _engine.Latitude, _engine.Longitude);
                _logger.LogInformation("Heading: {heading:F1}°  Fart: {speed:F1} knop", _engine.Heading, _engine.Speed);

                // Logg mål og distanse til mål hvis aktivt
                var autopilotType = _autopilot.GetType();
                var targetLatField = autopilotType.GetField("_targetLatitude", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var targetLonField = autopilotType.GetField("_targetLongitude", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var hasTargetField = autopilotType.GetField("_hasTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hasTargetField != null && targetLatField != null && targetLonField != null)
                {
                    var hasTargetObj = hasTargetField.GetValue(_autopilot);
                    if (hasTargetObj is bool hasTarget && hasTarget)
                    {
                        var latObj = targetLatField.GetValue(_autopilot);
                        var lonObj = targetLonField.GetValue(_autopilot);
                        if (latObj is double targetLat && lonObj is double targetLon)
                        {
                            double distNm = SimulatorService.AutopilotService
                                .GetDistanceNm(_engine.Latitude, _engine.Longitude, targetLat, targetLon);
                            _logger.LogInformation("Mål: {lat:F5}, {lon:F5}  |  Distanse til mål: {dist:F2} nm", targetLat, targetLon, distNm);
                        }
                    }
                }
                _logger.LogInformation("---------------------------------------------------");

                await Task.Delay(_tickInterval, stoppingToken);
            }
        }
    }
}
