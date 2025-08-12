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

        // Målet vi skal navigere til
        private double _targetLatitude;
        private double _targetLongitude;
        private bool _hasTarget;

        // Match engine arrival threshold (25 m -> nm)
        private const double ArrivalThresholdNm = 25.0 / 1852.0; // ~0.0135 nm (25 m)
        private double _cruisingSpeed = 15.0; // kan justeres via API

        public AutopilotService(SimulatorEngine engine)
        {
            _engine = engine;
        }

        public void SetDestination(double latitude, double longitude)
        {
            _targetLatitude = latitude;
            _targetLongitude = longitude;
            _hasTarget = true;
            // Synkroniser også motorens destinasjon slik at API status reflekterer målet
            _engine.SetDestination(latitude, longitude);
        }

        public (bool hasTarget, double? distanceNm) GetDestinationStatus(double currentLat, double currentLon)
        {
            if (!_hasTarget) return (false, null);
            double d = HaversineDistanceNm(currentLat, currentLon, _targetLatitude, _targetLongitude);
            return (true, d);
        }

        public void SetCruisingSpeed(double speedKnots)
        {
            if (speedKnots < 0) speedKnots = 0;
            _cruisingSpeed = speedKnots;
        }

        public void Update()
        {
            if (!_hasTarget) return;

            double currentLat = _engine.Latitude;
            double currentLon = _engine.Longitude;
            double distanceNm = HaversineDistanceNm(currentLat, currentLon, _targetLatitude, _targetLongitude);

            // Slow down when inside arrival zone but keep reporting target until engine flags HasArrived
            if (distanceNm < ArrivalThresholdNm)
            {
                _engine.SetDesiredSpeed(0.0);
                if (_engine.HasArrived)
                {
                    _hasTarget = false; // allow UI to clear destination after engine confirms arrival
                }
                return;
            }

            double desiredCourse = CalculateBearing(currentLat, currentLon, _targetLatitude, _targetLongitude);
            _engine.SetDesiredHeading(desiredCourse);
            _engine.SetDesiredSpeed(_cruisingSpeed);
        }

        // Debug state record
        public record AutopilotDebugState(bool HasTarget, double TargetLatitude, double TargetLongitude, double CruisingSpeed);

        public AutopilotDebugState GetDebugState()
        {
            return new AutopilotDebugState(_hasTarget, _targetLatitude, _targetLongitude, _cruisingSpeed);
        }

        public void Cancel()
        {
            _hasTarget = false;
            _engine.SetDesiredSpeed(0.0);
            // Fjern destinasjon i motoren slik at status-endepunkt viser ingen aktiv reise
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
