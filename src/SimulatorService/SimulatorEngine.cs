using System;

namespace SimulatorService
{
    public class SimulatorEngine
    {
        // Interne tilstandsfelt
        private double _latitude = 59.4;
        private double _longitude = 10.5;
        private double _heading = 90.0; // grader (øst)
        private double _speed = 0.0;    // knop

        // Settpunkt
        private double _desiredHeading = 90.0;
        private double _desiredSpeed = 0.0;

        // Miljødata
        private double _windSpeed = 0.0;        // knop
        private double _windDirection = 0.0;    // grader
        private double _currentSpeed = 0.0;     // knop
        private double _currentDirection = 0.0; // grader

        // Konstanter
        private const double BaseTurnRate = 1.0;        // grader per sekund
        private const double TurnRatePerKnot = 0.5;     // ekstra grader/s per knop
        private const double Acceleration = 1.0;        // knop per sekund

        // Eksternt tilgjengelig tilstand (for evt. UI/logging)
        public double Latitude => _latitude;
        public double Longitude => _longitude;
        public double Heading => _heading;
        public double Speed => _speed;

        public void SetDesiredHeading(double heading)
        {
            _desiredHeading = NormalizeAngle(heading);
        }

        public void SetDesiredSpeed(double speed)
        {
            _desiredSpeed = Math.Max(0.0, speed); // kan ikke gå bakover
        }

        public void SetEnvironment(double windSpeed, double windDirection, double currentSpeed, double currentDirection)
        {
            _windSpeed = windSpeed;
            _windDirection = NormalizeAngle(windDirection);
            _currentSpeed = currentSpeed;
            _currentDirection = NormalizeAngle(currentDirection);
        }

        public void Update(TimeSpan deltaTime)
        {
            double seconds = deltaTime.TotalSeconds;

            // 1. Heading – dynamisk sving basert på hastighet
            double headingError = NormalizeAngle(_desiredHeading - _heading);
            if (headingError > 180.0) headingError -= 360.0;

            double effectiveTurnRate = BaseTurnRate + (_speed * TurnRatePerKnot);
            double maxHeadingChange = effectiveTurnRate * seconds;
            double headingChange = Math.Clamp(headingError, -maxHeadingChange, maxHeadingChange);

            _heading = NormalizeAngle(_heading + headingChange);

            // 2. Fart – akselerasjon mot ønsket fart
            double speedDelta = _desiredSpeed - _speed;
            double speedChange = Math.Clamp(speedDelta, -Acceleration * seconds, Acceleration * seconds);
            _speed += speedChange;

            // 3. Bevegelse – kombiner egne krefter + vind + strøm
            double distanceShip = _speed * (seconds / 3600.0);     // knop -> nm
            double distanceWind = _windSpeed * (seconds / 3600.0);
            double distanceCurrent = _currentSpeed * (seconds / 3600.0);

            double dx = 0.0;
            double dy = 0.0;

            dx += distanceShip * Math.Cos(DegToRad(_heading));
            dy += distanceShip * Math.Sin(DegToRad(_heading));

            dx += distanceWind * Math.Cos(DegToRad(_windDirection));
            dy += distanceWind * Math.Sin(DegToRad(_windDirection));

            dx += distanceCurrent * Math.Cos(DegToRad(_currentDirection));
            dy += distanceCurrent * Math.Sin(DegToRad(_currentDirection));

            _latitude += dy / 60.0;    // konverter nm til grader (nord/sør)
            _longitude += dx / 60.0;   // konverter nm til grader (øst/vest)
        }

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;

        private static double NormalizeAngle(double angle)
        {
            angle %= 360.0;
            return angle < 0 ? angle + 360.0 : angle;
        }
    }
}
