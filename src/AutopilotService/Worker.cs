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

            // Hovedløkke for autopilot-regulering
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_lastNavData != null)
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
}
