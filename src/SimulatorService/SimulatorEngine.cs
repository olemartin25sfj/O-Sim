using System;

namespace SimulatorService
{
    public class SimulatorEngine
    {
        // Interne tilstandsfelt
        private double _latitude;
        private double _longitude;
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

        // Navigasjonsmål
        private double? _targetLat = null;
        private double? _targetLon = null;
        private const double ArrivalThresholdMeters = 25.0;

        // Autopilot status
        public bool HasDestination => _targetLat.HasValue && _targetLon.HasValue;
        public bool HasArrived { get; private set; } = false;
        public double? TargetLatitude => _targetLat;
        public double? TargetLongitude => _targetLon;

        // Konstanter
        private const double BaseTurnRate = 1.0;        // grader per sekund
        private const double TurnRatePerKnot = 0.5;     // ekstra grader/s per knop
        private const double Acceleration = 1.0;        // knop per sekund

        // Offentlig tilgjengelig tilstand (for UI/logging)
        public double Latitude => _latitude;
        public double Longitude => _longitude;
        public double Heading => _heading;
        public double Speed => _speed;

        // --- Initialisering ---
        public void SetInitialPosition(double latitude, double longitude)
        {
            _latitude = latitude;
            _longitude = longitude;
        }

        public void SetDesiredCourse(double course)
        {
            _desiredHeading = course;
        }

        public void SetDestination(double lat, double lon)
        {
            _targetLat = lat;
            _targetLon = lon;
            HasArrived = false;
        }

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

        // --- Hovedoppdatering ---
        public void Update(TimeSpan deltaTime)
        {
            double seconds = deltaTime.TotalSeconds;

            // AutopilotService eller annen ekstern logikk må nå sette ønsket heading

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

            var (dxEnv, dyEnv) = CalculateEnvironmentalInfluence(seconds);


            // Korrekt navigasjon: 0°=nord, 90°=øst
            double dyShip = distanceShip * Math.Cos(DegToRad(_heading)); // nord/sør
            double dxShip = distanceShip * Math.Sin(DegToRad(_heading)); // øst/vest

            double dxTotal = dxShip + dxEnv;
            double dyTotal = dyShip + dyEnv;

            _latitude += dyTotal / 60.0;    // nm til grader (nord/sør)
            _longitude += dxTotal / 60.0;   // nm til grader (øst/vest)

            // 4. Sjekk om vi har ankommet destinasjonen
            if (HasDestination && !HasArrived)
            {
                double distanceMeters = CalculateDistanceMeters(_latitude, _longitude, _targetLat!.Value, _targetLon!.Value);
                if (distanceMeters <= ArrivalThresholdMeters)
                {
                    HasArrived = true;
                    _speed = 0.0;
                    _desiredSpeed = 0.0;
                    Console.WriteLine($"✅ Fartøyet har nådd destinasjonen! ({_targetLat:F4}, {_targetLon:F4})");
                }
            }
        }

        private (double dx, double dy) CalculateEnvironmentalInfluence(double seconds)
        {
            double dx = 0.0, dy = 0.0;

            double distanceWind = _windSpeed * (seconds / 3600.0);
            double distanceCurrent = _currentSpeed * (seconds / 3600.0);
            // Samme koordinatkonvensjon som for skipet: 0° = nord (positiv dy), 90° = øst (positiv dx)
            dy += distanceWind * Math.Cos(DegToRad(_windDirection));
            dx += distanceWind * Math.Sin(DegToRad(_windDirection));

            dy += distanceCurrent * Math.Cos(DegToRad(_currentDirection));
            dx += distanceCurrent * Math.Sin(DegToRad(_currentDirection));

            return (dx, dy);
        }

        // --- Hjelpefunksjoner ---

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;

        private static double RadToDeg(double rad) => rad * 180.0 / Math.PI;

        private static double NormalizeAngle(double angle)
        {
            angle %= 360.0;
            return angle < 0 ? angle + 360.0 : angle;
        }

        public static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double φ1 = DegToRad(lat1);
            double φ2 = DegToRad(lat2);
            double Δλ = DegToRad(lon2 - lon1);

            double y = Math.Sin(Δλ) * Math.Cos(φ2);
            double x = Math.Cos(φ1) * Math.Sin(φ2) -
                       Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);

            double θ = Math.Atan2(y, x);
            return NormalizeAngle(RadToDeg(θ));
        }

        public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371000; // Jordens radius i meter
            double φ1 = DegToRad(lat1);
            double φ2 = DegToRad(lat2);
            double Δφ = DegToRad(lat2 - lat1);
            double Δλ = DegToRad(lon2 - lon1);

            double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                       Math.Cos(φ1) * Math.Cos(φ2) *
                       Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }
    }
}
