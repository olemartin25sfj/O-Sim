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

        // Direktkontroll (for kommandoer fra AutopilotService)
        private double _rudderAngle = 0.0; // grader, -35 til +35
        private double _thrustPercent = 0.0; // 0-100%

        // Anker-modus
        private bool _isAnchored = false;
        private double _anchorLat = 0.0;
        private double _anchorLon = 0.0;
        private const double MaxAnchorDrift = 20.0; // meter fra ankerposisjon

        // Autopilot status
        public bool HasDestination => _targetLat.HasValue && _targetLon.HasValue;
        public bool HasArrived { get; private set; } = false;
        public double? TargetLatitude => _targetLat;
        public double? TargetLongitude => _targetLon;
        public bool IsAnchored => _isAnchored;

        // Konstanter
        private const double BaseTurnRate = 1.0;        // grader per sekund
        private const double TurnRatePerKnot = 0.5;     // ekstra grader/s per knop
        private const double Acceleration = 3.0;        // knop per sekund

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

        public void ClearDestination()
        {
            _targetLat = null;
            _targetLon = null;
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

        public void SetRudderAngle(double angle)
        {
            _rudderAngle = Math.Clamp(angle, -35.0, 35.0);
        }

        public void SetThrustPercent(double percent)
        {
            _thrustPercent = Math.Clamp(percent, 0.0, 100.0);
        }

        public void SetAnchored(bool anchored)
        {
            _isAnchored = anchored;
            if (anchored)
            {
                // Sett ankerposisjon til gjeldende posisjon
                _anchorLat = _latitude;
                _anchorLon = _longitude;
            }
        }

        // --- Hovedoppdatering ---
        public void Update(TimeSpan deltaTime)
        {
            double seconds = deltaTime.TotalSeconds;

            // 1. Heading – bruk ruddervinkel for svinging
            double rudderEffect = _rudderAngle * 0.5; // grader/sekund per grad rudder
            double headingChange = rudderEffect * seconds;
            _heading = NormalizeAngle(_heading + headingChange);

            // 2. Fart – bruk thrust for akselerasjon
            double targetSpeed = (_thrustPercent / 100.0) * 35.0; // max 35 knop ved 100% thrust
            double speedDelta = targetSpeed - _speed;
            double speedChange = Math.Clamp(speedDelta, -Acceleration * seconds, Acceleration * seconds);
            _speed += speedChange;

            // 3. Bevegelse – kombiner egne krefter + vind + strøm
            double distanceShip = _speed * (seconds / 3600.0);     // knop -> nm

            var (dxEnv, dyEnv) = CalculateEnvironmentalInfluence(seconds);

            // Hvis ankret, reduser miljøpåvirkning betydelig
            if (_isAnchored)
            {
                dxEnv *= 0.1; // Reduser drift til 10% av normal påvirkning
                dyEnv *= 0.1;

                // Sjekk om vi har driftet for langt fra ankerposisjon
                double currentDrift = CalculateDistanceMeters(_latitude, _longitude, _anchorLat, _anchorLon);
                if (currentDrift > MaxAnchorDrift)
                {
                    // Trekk tilbake mot ankerposisjon
                    double bearing = CalculateBearing(_latitude, _longitude, _anchorLat, _anchorLon);
                    double pullDistance = (currentDrift - MaxAnchorDrift) / 6371000.0 * 60.0; // meter -> nm

                    double pullDy = pullDistance * Math.Cos(DegToRad(bearing));
                    double pullDx = pullDistance * Math.Sin(DegToRad(bearing));

                    dxEnv += pullDx;
                    dyEnv += pullDy;
                }
            }

            // Korrekt navigasjon: 0°=nord, 90°=øst
            double dyShip = distanceShip * Math.Cos(DegToRad(_heading)); // nord/sør
            double dxShip = distanceShip * Math.Sin(DegToRad(_heading)); // øst/vest

            double dxTotal = dxShip + dxEnv;
            double dyTotal = dyShip + dyEnv;

            _latitude += dyTotal / 60.0;    // nm -> grader (nord/sør)
            // Korriger lengdegrad for breddegrad: 1° lon = 60 nm * cos(lat)
            double latRad = DegToRad(_latitude);
            double nmPerLonDeg = 60.0 * Math.Cos(latRad);
            if (nmPerLonDeg < 1e-6) nmPerLonDeg = 1e-6; // beskyttelse nær polene
            _longitude += dxTotal / nmPerLonDeg;   // nm -> grader (øst/vest)

            // 4. Sjekk om vi har ankommet destinasjonen
            if (HasDestination && !HasArrived)
            {
                double distanceMeters = CalculateDistanceMeters(_latitude, _longitude, _targetLat!.Value, _targetLon!.Value);
                if (distanceMeters <= ArrivalThresholdMeters)
                {
                    // Marker ankomst, men la Autopilot kontrollere fart/stopp ved fullført rute
                    HasArrived = true;
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
