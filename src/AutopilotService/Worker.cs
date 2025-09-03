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

    // PID-konstanter for kursregulering (justert for mer stabil navigasjon)
    private const double Kp = 0.8;  // Redusert fra 2.0 for mindre aggressiv respons
    private const double Ki = 0.05; // Redusert fra 0.1 for mindre integral wind-up
    private const double Kd = 0.2;  // Redusert fra 0.5 for mindre oscillasjon

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    private void ResetRouteState()
    {
        _routeWaypoints = null;
        _currentWaypointIndex = 0;
        _lastError = 0.0;
        _integralError = 0.0;
        _targetCourse = 0.0;
        _targetSpeed = 0.0;
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

            // Abonner på reset-kommandoer
            var resetSubscription = natsConnection.SubscribeAsync("sim.commands.reset", (sender, args) =>
            {
                try
                {
                    ResetRouteState();
                    _logger.LogInformation("AutopilotService: Mottok reset-kommando, nullstiller alle tilstandsvariabler");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"AutopilotService: Feil ved behandling av reset-kommando: {ex.Message}");
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
                            // Nullstill alle rutetilstandsvariabler for ny rute
                            ResetRouteState();
                            _routeWaypoints = waypoints;
                            _currentWaypointIndex = 0;

                            double currentLat = _lastNavData?.Latitude ?? 59.4135;  // Default til Horten havn
                            double currentLon = _lastNavData?.Longitude ?? 10.5017;

                            _targetCourse = CalculateBearing(currentLat, currentLon, waypoints[0].lat, waypoints[0].lon);
                            _logger.LogInformation($"AutopilotService: Mottok rute med {waypoints.Count} waypoints, setter kurs mot første waypoint: {_targetCourse:F1}°");

                            // Send umiddelbar kurs-kommando til SimulatorService
                            var courseCommand = new SetCourseCommand(DateTime.UtcNow, _targetCourse);
                            var courseJson = JsonSerializer.Serialize(courseCommand);
                            natsConnection.Publish("sim.commands.setcourse", System.Text.Encoding.UTF8.GetBytes(courseJson));
                            _logger.LogInformation($"AutopilotService: Sender umiddelbar kurs-kommando: {_targetCourse:F1}°");

                            // Heis ankeret umiddelbart når ny rute startes
                            SendAnchorCommand(natsConnection, false);
                            _logger.LogInformation("AutopilotService: Heiser anker for ny rute");
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

            // Abonner på destinasjons-kommandoer
            var destinationSubscription = natsConnection.SubscribeAsync("sim.commands.destination", (sender, args) =>
            {
                try
                {
                    string message = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                    var destCommand = JsonSerializer.Deserialize<JsonElement>(message);

                    if (destCommand.TryGetProperty("latitude", out var latElement) &&
                        destCommand.TryGetProperty("longitude", out var lonElement))
                    {
                        double destLat = latElement.GetDouble();
                        double destLon = lonElement.GetDouble();

                        // Nullstill alle rutetilstandsvariabler for ny destinasjon
                        ResetRouteState();
                        _routeWaypoints = new List<(double lat, double lon)> { (destLat, destLon) };
                        _currentWaypointIndex = 0;

                        double currentLat = _lastNavData?.Latitude ?? 59.4135;  // Default til Horten havn
                        double currentLon = _lastNavData?.Longitude ?? 10.5017;

                        _targetCourse = CalculateBearing(currentLat, currentLon, destLat, destLon);
                        _logger.LogInformation($"AutopilotService: Mottok destinasjon ({destLat:F5}, {destLon:F5}), setter kurs: {_targetCourse:F1}°");

                        // Send umiddelbar kurs-kommando til SimulatorService
                        var courseCommand = new SetCourseCommand(DateTime.UtcNow, _targetCourse);
                        var courseJson = JsonSerializer.Serialize(courseCommand);
                        natsConnection.Publish("sim.commands.setcourse", System.Text.Encoding.UTF8.GetBytes(courseJson));
                        _logger.LogInformation($"AutopilotService: Sender umiddelbar kurs-kommando: {_targetCourse:F1}°");

                        // Heis ankeret umiddelbart når ny destinasjon settes
                        SendAnchorCommand(natsConnection, false);
                        _logger.LogInformation("AutopilotService: Heiser anker for ny destinasjon");
                    }

                    if (destCommand.TryGetProperty("cruiseSpeedKnots", out var speedElement))
                    {
                        _targetSpeed = speedElement.GetDouble();
                        _logger.LogInformation($"AutopilotService: Setter cruise-fart til {_targetSpeed:F1} knop");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"AutopilotService: Feil ved behandling av destinasjons-kommando: {ex.Message}");
                }
            });

            // Hovedløkke for autopilot-regulering
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_lastNavData != null)
                {
                    // Simpel logikk: Hvis ingen aktiv rute, send anker-kommando
                    bool hasActiveRoute = _routeWaypoints != null && _currentWaypointIndex < _routeWaypoints.Count;

                    if (!hasActiveRoute)
                    {
                        // Send anker-kommando (SimulatorEngine bestemmer om den skal ignoreres)
                        SendAnchorCommand(natsConnection, true);
                    }
                    else
                    {
                        // Send frigjøring av anker
                        SendAnchorCommand(natsConnection, false);
                    }

                    // Sjekk waypoint-navigasjon
                    if (_routeWaypoints != null && _currentWaypointIndex < _routeWaypoints.Count)
                    {
                        var currentWaypoint = _routeWaypoints[_currentWaypointIndex];
                        double distanceToWaypoint = CalculateDistance(_lastNavData.Latitude, _lastNavData.Longitude,
                                                                     currentWaypoint.lat, currentWaypoint.lon);

                        // Hvis vi er nær nok waypoint (10 meter), gå til neste - redusert fra 50m for mer presis navigasjon
                        if (distanceToWaypoint < 10.0)
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
                                // Rute fullført - bruk reset-metoden
                                _logger.LogInformation("AutopilotService: Rute fullført!");
                                ResetRouteState();
                                // Anker-kommando sendes i hovedlogikken over
                            }
                        }
                        else
                        {
                            // Oppdater kurs mot nåværende waypoint
                            _targetCourse = CalculateBearing(_lastNavData.Latitude, _lastNavData.Longitude,
                                                           currentWaypoint.lat, currentWaypoint.lon);
                        }
                    }

                    // Send navigasjons-kommandoer hvis vi har aktiv rute
                    if (hasActiveRoute)
                    {
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
                    // SimulatorEngine håndterer all anchor-logikk

                }

                await Task.Delay(1000, stoppingToken); // 1 Hz regulering for mer stabil navigasjon
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
