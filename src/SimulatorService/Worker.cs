using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using OSim.Shared.Messages;
using SimulatorService;

namespace SimulatorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly SimulatorEngine _engine;
        private readonly AutopilotService _autopilot;
        private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(1000);

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
            IConnection? natsConnection = null;
            int retries = 10;

            // Prøv å koble til NATS med retry
            for (int i = 0; i < retries && !stoppingToken.IsCancellationRequested; i++)
            {
                try
                {
                    var opts = ConnectionFactory.GetDefaultOptions();
                    opts.Url = "nats://nats:4222";
                    natsConnection = new ConnectionFactory().CreateConnection(opts);
                    _logger.LogInformation("Koblet til NATS-server");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"NATS-tilkobling feilet ({i + 1}/{retries}): {ex.Message}");
                    await Task.Delay(2000, stoppingToken);
                }
            }

            if (natsConnection == null)
            {
                _logger.LogError("Kunne ikke koble til NATS etter flere forsøk. Avslutter.");
                return;
            }

            using (natsConnection)
            {
                var envSubscription = natsConnection.SubscribeAsync("sim.sensors.env", (sender, args) =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                    var env = JsonSerializer.Deserialize<EnvironmentData>(json);
                    if (env != null)
                    {
                        _engine.SetEnvironment(
                            windSpeed: env.WindSpeedKnots,
                            windDirection: env.WindDirection,
                            currentSpeed: env.CurrentSpeed,
                            currentDirection: env.CurrentDirection
                        );
                        _logger.LogInformation($"Mottok miljødata: {json}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Feil ved behandling av miljødata: {ex.Message}");
                }
            });

                while (!stoppingToken.IsCancellationRequested)
                {
                    _autopilot.Update();
                    _engine.Update(_tickInterval);

                    // Publish navigation data to NATS
                    var navigationData = new NavigationData
                    {
                        Latitude = _engine.Latitude,
                        Longitude = _engine.Longitude,
                        Heading = _engine.Heading,
                        SpeedKnots = _engine.Speed,
                        Timestamp = DateTime.UtcNow
                    };

                    var json = JsonSerializer.Serialize(navigationData);
                    natsConnection.Publish("sim.sensors.nav", System.Text.Encoding.UTF8.GetBytes(json));

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Nav Update: Pos({lat:F5}, {lon:F5}) Hdg:{heading:F1}° Spd:{speed:F1}kt",
                            _engine.Latitude, _engine.Longitude, _engine.Heading, _engine.Speed);
                    }

                    await Task.Delay(_tickInterval, stoppingToken);
                }
            }
        }
    }
}
