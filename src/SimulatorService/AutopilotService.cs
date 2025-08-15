using System;

namespace SimulatorService
{
    public class AutopilotService
    {
        // Gjør distanseberegning tilgjengelig for logging
        public static double GetDistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            return HaversineDistanceNm(lat1, lon1, lat2, lon2);
        }
        private readonly SimulatorEngine _engine;

        // Veipunkter (rute). Første element er aktivt mål.
        private readonly Queue<(double lat, double lon)> _waypoints = new();
        private bool _routeActive = false;
        private bool _routeCompleted = false;

        // Match engine arrival threshold (25 m -> nm)
        private const double ArrivalThresholdNm = 25.0 / 1852.0; // ~0.0135 nm (25 m)
        private double _cruisingSpeed = 15.0; // kan justeres via API

        public AutopilotService(SimulatorEngine engine)
        {
            _engine = engine;
        }

        // Bakoverkompatibel: enkel destinasjon (en-veipunkt rute)
        public void SetDestination(double latitude, double longitude)
        {
            _waypoints.Clear();
            _waypoints.Enqueue((latitude, longitude));
            _routeActive = true;
            _routeCompleted = false;
            _engine.SetDestination(latitude, longitude);
        }

        public void SetRoute(IEnumerable<(double lat, double lon)> points)
        {
            _waypoints.Clear();
            foreach (var p in points)
            {
                _waypoints.Enqueue(p);
            }
            _routeActive = _waypoints.Count > 0;
            _routeCompleted = false;
            if (_routeActive)
            {
                var first = _waypoints.Peek();
                _engine.SetDestination(first.lat, first.lon);
            }
            else
            {
                _engine.ClearDestination();
            }
        }

        public (bool hasTarget, double? distanceNm) GetDestinationStatus(double currentLat, double currentLon)
        {
            if (!_routeActive)
                return (false, null);
            if (_waypoints.Count == 0)
                return (_routeCompleted, 0);
            var (tlat, tlon) = _waypoints.Peek();
            double d = HaversineDistanceNm(currentLat, currentLon, tlat, tlon);
            return (true, d);
        }

        public void SetCruisingSpeed(double speedKnots)
        {
            if (speedKnots < 0) speedKnots = 0;
            _cruisingSpeed = speedKnots;
        }

        public void Update()
        {
            if (!_routeActive) return;
            if (_waypoints.Count == 0)
            {
                // Ferdig – hold stopp
                _engine.SetDesiredSpeed(0.0);
                return;
            }

            double currentLat = _engine.Latitude;
            double currentLon = _engine.Longitude;
            var (tlat, tlon) = _waypoints.Peek();
            double distanceNm = HaversineDistanceNm(currentLat, currentLon, tlat, tlon);

            if (distanceNm < ArrivalThresholdNm)
            {
                // Nådd aktivt veipunkt
                _waypoints.Dequeue();
                if (_waypoints.Count == 0)
                {
                    // Siste veipunkt nådd – stopp
                    _routeActive = false;
                    _routeCompleted = true;
                    _engine.SetDesiredSpeed(0.0);
                    return;
                }
                else
                {
                    // Sett neste veipunkt som engine-destinasjon
                    var next = _waypoints.Peek();
                    _engine.SetDestination(next.lat, next.lon);
                }
            }

            if (_routeActive && _waypoints.Count > 0)
            {
                var (clat, clon) = _waypoints.Peek();
                double desiredCourse = CalculateBearing(currentLat, currentLon, clat, clon);
                _engine.SetDesiredHeading(desiredCourse);

                // Adaptiv nedbremsing for siste etappe for å unngå overskyting
                if (_waypoints.Count == 1)
                {
                    // Estimér stoppdistanse basert på nåværende fart og antatt deselerasjon ~1 kn/s (matcher engine)
                    double currentSpeed = _engine.Speed; // kn
                    const double decelKnPerSec = 1.0; // må holdes i sync med Engine Acceleration
                    double timeToStopSec = currentSpeed / Math.Max(0.1, decelKnPerSec);
                    double stoppingDistanceNm = (currentSpeed / 2.0) * (timeToStopSec / 3600.0); // nm
                    double slowStartNm = Math.Max(0.2, stoppingDistanceNm * 2.0); // start bremsing litt før teoretisk stopp

                    double targetSpeed = _cruisingSpeed;
                    if (distanceNm <= slowStartNm)
                    {
                        // Lineær nedtrapping mot lav styrefart, og lavere helt nær mål
                        const double minSteerage = 2.0; // kn
                        double factor = Math.Clamp(distanceNm / slowStartNm, 0.0, 1.0);
                        targetSpeed = Math.Max(minSteerage, _cruisingSpeed * factor);
                        if (distanceNm < 0.02) // ~37 m
                        {
                            targetSpeed = Math.Min(targetSpeed, 1.0);
                        }
                    }
                    _engine.SetDesiredSpeed(targetSpeed);
                }
                else
                {
                    // Mellometapper: hold cruise for å sikre fremdrift mellom veipunkter
                    _engine.SetDesiredSpeed(_cruisingSpeed);
                }
            }
        }

        // Debug state record
        public record AutopilotDebugState(bool RouteActive, int RemainingWaypoints, bool RouteCompleted, double? TargetLatitude, double? TargetLongitude, double CruisingSpeed);

        public AutopilotDebugState GetDebugState()
        {
            double? tLat = null, tLon = null;
            if (_waypoints.Count > 0)
            {
                var (lat, lon) = _waypoints.Peek();
                tLat = lat; tLon = lon;
            }
            return new AutopilotDebugState(_routeActive, _waypoints.Count, _routeCompleted, tLat, tLon, _cruisingSpeed);
        }

        public void Cancel()
        {
            _waypoints.Clear();
            _routeActive = false;
            _routeCompleted = false;
            _engine.SetDesiredSpeed(0.0);
            _engine.ClearDestination();
        }

        private static double HaversineDistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R_nm = 3440.0; // Jordens radius i nautiske mil

            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a = Math.Pow(Math.Sin(dLat / 2), 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Pow(Math.Sin(dLon / 2), 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R_nm * c;
        }

        private static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double φ1 = ToRadians(lat1);
            double φ2 = ToRadians(lat2);
            double Δλ = ToRadians(lon2 - lon1);

            double y = Math.Sin(Δλ) * Math.Cos(φ2);
            double x = Math.Cos(φ1) * Math.Sin(φ2) -
                       Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);

            double θ = Math.Atan2(y, x);
            return (ToDegrees(θ) + 360.0) % 360.0;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
        private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
    }
}
