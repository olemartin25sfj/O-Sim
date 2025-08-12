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
        private readonly SimulatorEngine _engine; // Denne er nå den samme singletonen som API-et bruker
        private readonly AutopilotService _autopilot;
        private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(1000);

        public Worker(ILogger<Worker> logger, SimulatorEngine engine, AutopilotService autopilot)
        {
            _logger = logger;
            _engine = engine; // Bruk DI-singleton
            _autopilot = autopilot;

            // Initialiser bare hvis posisjon ikke allerede er satt (lat = 0 && lon = 0 antas som ikke initialisert)
            if (Math.Abs(_engine.Latitude) < 0.0001 && Math.Abs(_engine.Longitude) < 0.0001)
            {
                _engine.SetInitialPosition(59.4167, 10.4833); // Horten havn
            }

            // (Re)sett et tilfeldig miljø – API / env updates vil overskrive fortløpende
            _engine.SetEnvironment(
                windSpeed: Random.Shared.NextDouble() * 10.0,
                windDirection: Random.Shared.NextDouble() * 360.0,
                currentSpeed: Random.Shared.NextDouble() * 2.0,
                currentDirection: Random.Shared.NextDouble() * 360.0);
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

                        // Prøv ny struktur først
                        EnvironmentData? env = null;
                        try
                        {
                            env = JsonSerializer.Deserialize<EnvironmentData>(json);
                        }
                        catch
                        {
                            // fallback: forsøk å lese gammel struktur og mappe
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("WindSpeedKnots", out _))
                            {
                                env = new EnvironmentData(
                                    TimestampUtc: root.GetProperty("Timestamp").GetDateTime(),
                                    Mode: EnvironmentMode.Dynamic,
                                    WindSpeedKnots: root.GetProperty("WindSpeedKnots").GetDouble(),
                                    WindDirectionDegrees: root.GetProperty("WindDirection").GetDouble(),
                                    CurrentSpeedKnots: root.GetProperty("CurrentSpeed").GetDouble(),
                                    CurrentDirectionDegrees: root.GetProperty("CurrentDirection").GetDouble(),
                                    WaveHeightMeters: root.GetProperty("WaveHeight").GetDouble(),
                                    WaveDirectionDegrees: root.GetProperty("WaveDirection").GetDouble(),
                                    WavePeriodSeconds: root.GetProperty("WavePeriod").GetDouble()
                                );
                            }
                        }

                        if (env != null)
                        {
                            _engine.SetEnvironment(
                                windSpeed: env.WindSpeedKnots,
                                windDirection: env.WindDirectionDegrees,
                                currentSpeed: env.CurrentSpeedKnots,
                                currentDirection: env.CurrentDirectionDegrees
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

                    // Publish navigation data to NATS (bruk ny record-konstruktør)
                    var navigationData = new NavigationData(
                        TimestampUtc: DateTime.UtcNow,
                        Latitude: _engine.Latitude,
                        Longitude: _engine.Longitude,
                        SpeedKnots: _engine.Speed,
                        HeadingDegrees: _engine.Heading,
                        CourseOverGroundDegrees: _engine.Heading // placeholder dersom COG ikke beregnes separat
                    );

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
