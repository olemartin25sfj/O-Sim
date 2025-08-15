using System.Text.Json;
using NATS.Client;
using OSim.Shared.Messages;

namespace AutopilotService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private NavigationData? _lastNavData;
    private EnvironmentData? _lastEnvData;
    private double _targetCourse = 0.0;
    private double _targetSpeed = 0.0;
    private double _lastError = 0.0;
    private double _integralError = 0.0;
    private List<(double lat, double lon)>? _routeWaypoints;
    private int _currentWaypointIndex = 0;
    private bool _wasAnchored = false; // For å spore anker-tilstand

    // PID-konstanter for kursregulering
    private const double Kp = 2.0;
    private const double Ki = 0.1;
    private const double Kd = 0.5;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
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
                _logger.LogInformation("AutopilotService: Koblet til NATS-server");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"AutopilotService: NATS-tilkobling feilet ({i + 1}/{retries}): {ex.Message}");
                await Task.Delay(2000, stoppingToken);
            }
        }

        if (natsConnection == null)
        {
            _logger.LogError("AutopilotService: Kunne ikke koble til NATS etter flere forsøk. Avslutter.");
            return;
        }

        using (natsConnection)
        {
            // Abonner på navigasjonsdata
            var navSubscription = natsConnection.SubscribeAsync("sim.sensors.nav", (sender, args) =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                    _lastNavData = JsonSerializer.Deserialize<NavigationData>(json);
                    _logger.LogDebug("AutopilotService: Mottok navigasjonsdata");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"AutopilotService: Feil ved behandling av navigasjonsdata: {ex.Message}");
                }
            });

            // Abonner på miljødata
            var envSubscription = natsConnection.SubscribeAsync("sim.sensors.env", (sender, args) =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                    _lastEnvData = JsonSerializer.Deserialize<EnvironmentData>(json);
                    _logger.LogDebug("AutopilotService: Mottok miljødata");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"AutopilotService: Feil ved behandling av miljødata: {ex.Message}");
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
                        _targetCourse = command.TargetCourseDegrees;
                        _logger.LogInformation($"AutopilotService: Ny målkurs satt til {_targetCourse}°");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"AutopilotService: Feil ved behandling av kurskommando: {ex.Message}");
                }
            });

            // Abonner på fartskommandoer
            var speedSubscription = natsConnection.SubscribeAsync("sim.commands.setspeed", (sender, args) =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                    var command = JsonSerializer.Deserialize<SetSpeedCommand>(json);
                    if (command != null)
                    {
                        _targetSpeed = command.TargetSpeedKnots;
                        _logger.LogInformation($"AutopilotService: Ny målfart satt til {_targetSpeed} knop");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"AutopilotService: Feil ved behandling av fartskommando: {ex.Message}");
                }
            });

            // Abonner på stopp-kommandoer
            var stopSubscription = natsConnection.SubscribeAsync("sim.commands.stop", (sender, args) =>
            {
                try
                {
                    _targetSpeed = 0.0;
                    _logger.LogInformation("AutopilotService: Mottok stopp-kommando, setter målfart til 0 knop");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"AutopilotService: Feil ved behandling av stopp-kommando: {ex.Message}");
                }
            });

            // Abonner på rute-kommandoer
            var routeSubscription = natsConnection.SubscribeAsync("sim.commands.route", (sender, args) =>
            {
                try
                {
                    string message = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                    var routeCommand = JsonSerializer.Deserialize<JsonElement>(message);

                    if (routeCommand.TryGetProperty("waypoints", out var waypointsElement) &&
                        waypointsElement.ValueKind == JsonValueKind.Array)
                    {
                        var waypoints = new List<(double lat, double lon)>();
                        foreach (var wp in waypointsElement.EnumerateArray())
                        {
                            if (wp.TryGetProperty("latitude", out var lat) &&
                                wp.TryGetProperty("longitude", out var lon))
                            {
                                waypoints.Add((lat.GetDouble(), lon.GetDouble()));
                            }
                        }

                        if (waypoints.Count > 0)
                        {
                            _routeWaypoints = waypoints;
                            _currentWaypointIndex = 0;
                            _targetCourse = CalculateBearing(_lastNavData?.Latitude ?? 0, _lastNavData?.Longitude ?? 0,
                                                           waypoints[0].lat, waypoints[0].lon);
                            _logger.LogInformation($"AutopilotService: Mottok rute med {waypoints.Count} waypoints, setter kurs mot første waypoint: {_targetCourse:F1}°");
                        }
                    }

                    if (routeCommand.TryGetProperty("cruiseSpeedKnots", out var speedElement))
                    {
                        _targetSpeed = speedElement.GetDouble();
                        _logger.LogInformation($"AutopilotService: Setter cruise-fart til {_targetSpeed:F1} knop");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"AutopilotService: Feil ved behandling av rute-kommando: {ex.Message}");
                }
            });

            // Hovedløkke for autopilot-regulering
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_lastNavData != null)
                {
                    // Håndter anker-tilstand basert på om vi har aktiv rute
                    bool shouldBeAnchored = _routeWaypoints == null || _currentWaypointIndex >= _routeWaypoints.Count;

                    if (shouldBeAnchored && !_wasAnchored)
                    {
                        // Senk ankeret
                        SendAnchorCommand(natsConnection, true);
                        _wasAnchored = true;
                    }
                    else if (!shouldBeAnchored && _wasAnchored)
                    {
                        // Heis ankeret
                        SendAnchorCommand(natsConnection, false);
                        _wasAnchored = false;
                    }

                    // Sjekk waypoint-navigasjon
                    if (_routeWaypoints != null && _currentWaypointIndex < _routeWaypoints.Count)
                    {
                        var currentWaypoint = _routeWaypoints[_currentWaypointIndex];
                        double distanceToWaypoint = CalculateDistance(_lastNavData.Latitude, _lastNavData.Longitude,
                                                                     currentWaypoint.lat, currentWaypoint.lon);

                        // Hvis vi er nær nok waypoint (50 meter), gå til neste
                        if (distanceToWaypoint < 50.0)
                        {
                            _currentWaypointIndex++;
                            _logger.LogInformation($"AutopilotService: Nådde waypoint {_currentWaypointIndex}, {_routeWaypoints.Count - _currentWaypointIndex} waypoints igjen");

                            if (_currentWaypointIndex < _routeWaypoints.Count)
                            {
                                // Sett kurs mot neste waypoint
                                var nextWaypoint = _routeWaypoints[_currentWaypointIndex];
                                _targetCourse = CalculateBearing(_lastNavData.Latitude, _lastNavData.Longitude,
                                                               nextWaypoint.lat, nextWaypoint.lon);
                                _logger.LogInformation($"AutopilotService: Setter kurs mot waypoint {_currentWaypointIndex + 1}: {_targetCourse:F1}°");
                            }
                            else
                            {
                                // Rute fullført
                                _logger.LogInformation("AutopilotService: Rute fullført!");
                                _targetSpeed = 0.0; // Stopp
                                _routeWaypoints = null; // Nullstill rute
                            }
                        }
                        else
                        {
                            // Oppdater kurs mot nåværende waypoint
                            _targetCourse = CalculateBearing(_lastNavData.Latitude, _lastNavData.Longitude,
                                                           currentWaypoint.lat, currentWaypoint.lon);
                        }
                    }

                    // PID-regulering for kurs
                    double currentHeading = _lastNavData.HeadingDegrees;
                    double error = CalculateHeadingError(currentHeading, _targetCourse);

                    _integralError += error;
                    double derivative = error - _lastError;

                    double pidOutput = (Kp * error) + (Ki * _integralError) + (Kd * derivative);

                    // Begrens rudderutslag til ±35 grader
                    double rudderAngle = Math.Clamp(pidOutput, -35.0, 35.0);

                    // Publiser rudderkommando
                    var rudderCommand = new RudderCommand(
                        TimestampUtc: DateTime.UtcNow,
                        RudderAngleDegrees: rudderAngle
                    );

                    var rudderJson = JsonSerializer.Serialize(rudderCommand);
                    natsConnection.Publish("sim.commands.rudder", System.Text.Encoding.UTF8.GetBytes(rudderJson));

                    // Enkel fartsregulering (kan forbedres)
                    double currentSpeed = _lastNavData.SpeedKnots;
                    double speedError = _targetSpeed - currentSpeed;
                    double thrustPercent = Math.Clamp(50.0 + (speedError * 10.0), 0.0, 100.0);

                    // Publiser thrustkommando
                    var thrustCommand = new ThrustCommand(
                        TimestampUtc: DateTime.UtcNow,
                        ThrustPercent: thrustPercent
                    );

                    var thrustJson = JsonSerializer.Serialize(thrustCommand);
                    natsConnection.Publish("sim.commands.thrust", System.Text.Encoding.UTF8.GetBytes(thrustJson));

                    _lastError = error;

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug($"AutopilotService: Kurs={currentHeading:F1}° Mål={_targetCourse:F1}° Feil={error:F1}° Ror={rudderAngle:F1}° Thrust={thrustPercent:F1}%");
                    }
                }

                await Task.Delay(500, stoppingToken); // 2 Hz regulering
            }
        }
    }

    private static double CalculateHeadingError(double current, double target)
    {
        double error = target - current;

        // Normaliser til [-180, 180]
        if (error > 180.0)
            error -= 360.0;
        else if (error < -180.0)
            error += 360.0;

        return error;
    }

    private double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        // Konverter til radianer
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double lat1Rad = lat1 * Math.PI / 180.0;
        double lat2Rad = lat2 * Math.PI / 180.0;

        double y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) - Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

        double bearing = Math.Atan2(y, x) * 180.0 / Math.PI;

        // Normaliser til [0, 360]
        return (bearing + 360.0) % 360.0;
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Jordens radius i meter
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c; // Distanse i meter
    }

    private void SendAnchorCommand(IConnection natsConnection, bool isAnchored)
    {
        try
        {
            var anchorCommand = new SetAnchorCommand(
                TimestampUtc: DateTime.UtcNow,
                IsAnchored: isAnchored
            );

            var json = JsonSerializer.Serialize(anchorCommand);
            natsConnection.Publish("sim.commands.anchor", System.Text.Encoding.UTF8.GetBytes(json));

            _logger.LogInformation($"AutopilotService: Anker {(isAnchored ? "senket" : "heist")}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"AutopilotService: Feil ved sending av anker-kommando: {ex.Message}");
        }
    }
}
