using System;

namespace SimulatorService
{
    public enum VesselState
    {
        Drifting,       // Fri drift (kun miljøkrefter)
        Anchored,       // Ankret - ignorerer alle navigasjonskommandoer
        Navigating,     // Aktiv navigasjon mot mål
        Maneuvering     // Manuell kontroll (direktekommandoer)
    }

    public class SimulatorEngine
    {
        // Vessel State Management
        private VesselState _currentState = VesselState.Drifting;

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
        private const double MaxAnchorDrift = 5.0; // Redusert fra 10.0 for tettere hold

        // Autopilot status
        public bool HasDestination => _targetLat.HasValue && _targetLon.HasValue;
        public bool HasArrived { get; private set; } = false;
        public double? TargetLatitude => _targetLat;
        public double? TargetLongitude => _targetLon;
        public bool IsAnchored => _isAnchored;
        public VesselState CurrentState => _currentState;

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
            HasArrived = false; // Alltid nullstill ankomststatus ved ny destinasjon

            // State transition til Navigating hvis ikke ankret
            if (_currentState != VesselState.Anchored)
            {
                _currentState = VesselState.Navigating;
            }
        }

        public void ClearDestination()
        {
            _targetLat = null;
            _targetLon = null;
            HasArrived = false; // Nullstill ankomststatus når destinasjon fjernes

            // State transition til Drifting hvis ikke ankret
            if (_currentState == VesselState.Navigating)
            {
                _currentState = VesselState.Drifting;
            }
        }

        public void ResetNavigationState()
        {
            ClearDestination();
            _desiredHeading = _heading; // Behold nåværende heading som ønsket
            _desiredSpeed = 0.0; // Stopp
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
            // Kun prosesser rudder-kommandoer hvis ikke ankret
            if (_currentState != VesselState.Anchored)
            {
                _rudderAngle = Math.Clamp(angle, -35.0, 35.0);

                // Hvis vi får manuell rudder-kommando, bytt til Maneuvering modus
                if (Math.Abs(angle) > 0.1) // Ikke-null rudder input
                {
                    _currentState = VesselState.Maneuvering;
                }
            }
        }

        public void SetThrustPercent(double percent)
        {
            // Kun prosesser thrust-kommandoer hvis ikke ankret
            if (_currentState != VesselState.Anchored)
            {
                _thrustPercent = Math.Clamp(percent, 0.0, 100.0);

                // Hvis vi får manuell thrust-kommando, bytt til Maneuvering modus
                if (percent > 0.1) // Ikke-null thrust input
                {
                    _currentState = VesselState.Maneuvering;
                }
            }
        }

        public void SetAnchored(bool anchored)
        {
            _isAnchored = anchored;
            if (anchored)
            {
                // Sett ankerposisjon til gjeldende posisjon
                _anchorLat = _latitude;
                _anchorLon = _longitude;

                // State transition til Anchored
                _currentState = VesselState.Anchored;

                // Umiddelbart stopp
                _speed = 0.0;
                _desiredSpeed = 0.0;
                _thrustPercent = 0.0;
                _rudderAngle = 0.0;
            }
            else
            {
                // Frigir anker - gå til drifting eller navigering
                _currentState = HasDestination ? VesselState.Navigating : VesselState.Drifting;
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

            // Hvis ankret, reduser miljøpåvirkning drastisk og øk motstand
            if (_isAnchored)
            {
                // Kraftig reduksjon av miljøpåvirkning når ankret
                dxEnv *= 0.01; // Redusert fra 0.05 til 0.01 (1% av normal påvirkning)
                dyEnv *= 0.01;

                // Stopp skipet når ankret - sett thrust til 0 og reduser fart kraftig
                _thrustPercent = 0.0;
                _speed *= 0.95; // Reduser fart raskt når ankret

                // Sjekk om vi har driftet for langt fra ankerposisjon
                double currentDrift = CalculateDistanceMeters(_latitude, _longitude, _anchorLat, _anchorLon);
                if (currentDrift > MaxAnchorDrift)
                {
                    // Meget sterk tilbaketrekking mot ankerposisjon
                    double bearing = CalculateBearing(_latitude, _longitude, _anchorLat, _anchorLon);
                    double pullDistance = (currentDrift - MaxAnchorDrift) / 6371000.0 * 60.0 * 5.0; // Økt fra 2.0 til 5.0

                    double pullDy = pullDistance * Math.Cos(DegToRad(bearing));
                    double pullDx = pullDistance * Math.Sin(DegToRad(bearing));

                    dxEnv += pullDx;
                    dyEnv += pullDy;
                }

                // Sterkere generell anker-motstand som alltid drar mot ankerposisjon
                double anchorBearing = CalculateBearing(_latitude, _longitude, _anchorLat, _anchorLon);
                double distanceToAnchor = CalculateDistanceMeters(_latitude, _longitude, _anchorLat, _anchorLon);
                double anchorPull = Math.Min(0.0005 * (distanceToAnchor / MaxAnchorDrift), 0.002); // Progressiv pull, økt fra 0.0001

                double anchorDy = anchorPull * Math.Cos(DegToRad(anchorBearing));
                double anchorDx = anchorPull * Math.Sin(DegToRad(anchorBearing));

                dxEnv += anchorDx;
                dyEnv += anchorDy;
            }            // Korrekt navigasjon: 0°=nord, 90°=øst
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
