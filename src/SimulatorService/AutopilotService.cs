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

        private const double ArrivalThresholdNm = 0.05; // 50 meter
        private const double CruisingSpeed = 15.0; // Høy fart for rask simulering

        public AutopilotService(SimulatorEngine engine)
        {
            _engine = engine;
        }

        public void SetDestination(double latitude, double longitude)
        {
            _targetLatitude = latitude;
            _targetLongitude = longitude;
            _hasTarget = true;
        }

        public void Update()
        {
            if (!_hasTarget) return;

            double currentLat = _engine.Latitude;
            double currentLon = _engine.Longitude;

            double distanceNm = HaversineDistanceNm(currentLat, currentLon, _targetLatitude, _targetLongitude);

            if (distanceNm < ArrivalThresholdNm)
            {
                _engine.SetDesiredSpeed(0.0);
                _hasTarget = false;
                return;
            }

            double desiredCourse = CalculateBearing(currentLat, currentLon, _targetLatitude, _targetLongitude);
            _engine.SetDesiredHeading(desiredCourse);
            _engine.SetDesiredSpeed(CruisingSpeed);
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
