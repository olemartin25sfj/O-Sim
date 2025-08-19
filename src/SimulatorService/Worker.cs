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
        private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(1000); // 1 Hz sim oppdatering

        public Worker(ILogger<Worker> logger, SimulatorEngine engine)
        {
            _logger = logger;
            _engine = engine; // Bruk DI-singleton

            // Initialiser bare hvis posisjon ikke allerede er satt (lat = 0 && lon = 0 antas som ikke initialisert)
            if (Math.Abs(_engine.Latitude) < 0.0001 && Math.Abs(_engine.Longitude) < 0.0001)
            {
                _engine.SetInitialPosition(59.414903, 10.493791); // Horten havn
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
                            // fallback: forsøv å lese gammel struktur og mappe
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

                // Abonner på rudderkommandoer fra AutopilotService
                var rudderSubscription = natsConnection.SubscribeAsync("sim.commands.rudder", (sender, args) =>
                {
                    try
                    {
                        var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                        var command = JsonSerializer.Deserialize<RudderCommand>(json);
                        if (command != null)
                        {
                            _engine.SetRudderAngle(command.RudderAngleDegrees);
                            _logger.LogDebug($"Mottok rudderkommando: {command.RudderAngleDegrees}°");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Feil ved behandling av rudderkommando: {ex.Message}");
                    }
                });

                // Abonner på thrustkommandoer fra AutopilotService
                var thrustSubscription = natsConnection.SubscribeAsync("sim.commands.thrust", (sender, args) =>
                {
                    try
                    {
                        var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                        var command = JsonSerializer.Deserialize<ThrustCommand>(json);
                        if (command != null)
                        {
                            _engine.SetThrustPercent(command.ThrustPercent);
                            _logger.LogDebug($"Mottok thrustkommando: {command.ThrustPercent}%");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Feil ved behandling av thrustkommando: {ex.Message}");
                    }
                });

                // Abonner på ankerkommandoer
                var anchorSubscription = natsConnection.SubscribeAsync("sim.commands.anchor", (sender, args) =>
                {
                    try
                    {
                        var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                        var command = JsonSerializer.Deserialize<SetAnchorCommand>(json);
                        if (command != null)
                        {
                            _engine.SetAnchored(command.IsAnchored);
                            _logger.LogInformation($"Anker {(command.IsAnchored ? "senket" : "heist")}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Feil ved behandling av ankerkommando: {ex.Message}");
                    }
                });

                // Abonner på kurskommandoer
                var courseSubscription = natsConnection.SubscribeAsync("sim.commands.setcourse", (sender, args) =>
                {
                    try
                    {
                        var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                        var command = JsonSerializer.Deserialize<SetCourseCommand>(json);
                        if (command != null)
                        {
                            _engine.SetDesiredCourse(command.TargetCourseDegrees);
                            _logger.LogInformation($"Mottok kurskommando: {command.TargetCourseDegrees:F1}°");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Feil ved behandling av kurskommando: {ex.Message}");
                    }
                });

                int tick = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    _engine.Update(_tickInterval);

                    // Publish navigation data to NATS (bruk ny record-konstruktør)
                    var navigationData = new NavigationData(
                        TimestampUtc: DateTime.UtcNow,
                        Latitude: _engine.Latitude,
                        Longitude: _engine.Longitude,
                        SpeedKnots: _engine.Speed,
                        HeadingDegrees: _engine.Heading,
                        CourseOverGroundDegrees: _engine.Heading, // placeholder dersom COG ikke beregnes separat
                        IsAnchored: _engine.IsAnchored,
                        HasDestination: _engine.HasDestination,
                        TargetLatitude: _engine.TargetLatitude,
                        TargetLongitude: _engine.TargetLongitude,
                        HasArrived: _engine.HasArrived
                    );

                    var json = JsonSerializer.Serialize(navigationData);
                    natsConnection.Publish("sim.sensors.nav", System.Text.Encoding.UTF8.GetBytes(json));

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Nav Update: Pos({lat:F5}, {lon:F5}) Hdg:{heading:F1}° Spd:{speed:F1}kt",
                            _engine.Latitude, _engine.Longitude, _engine.Heading, _engine.Speed);
                    }
                    else if (tick % 5 == 0) // hvert 5. sekund gi en info-linje for synlighet
                    {
                        _logger.LogInformation("Posisjon: {lat:F5},{lon:F5} Heading:{heading:F0}° Fart:{speed:F1}kt", _engine.Latitude, _engine.Longitude, _engine.Heading, _engine.Speed);
                    }
                    tick++;

                    await Task.Delay(_tickInterval, stoppingToken);
                }
            }
        }
    }
}
