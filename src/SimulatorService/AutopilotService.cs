namespace SimulatorService
{
    public class AutopilotService
    {
        private readonly SimulatorEngine _engine;
        private double _targetLat;
        private double _targetLon;
        private bool _active;

        public AutopilotService(SimulatorEngine engine)
        {
            _engine = engine;
        }

        public void SetDestination(double latitude, double longitude)
        {
            _targetLat = latitude;
            _targetLon = longitude;
            _active = true;
        }
        public void Update()
        {
            if (!_active) return;

            // Beregn avstand og kurs til mål
            double dx = (_targetLon - _engine.Longitude) * 60.0;
            double dy = (_targetLat - _engine.Latitude) * 60.0;

            double distanceNm = Math.Sqrt(dx * dx + dy * dy);
            if (distanceNm < 0.1)
            {
                _engine.SetDesiredSpeed(0.0);
                _active = false;
                return;
            }

            double bearing = Math.Atan2(dy, dx) * (180.0 / Math.PI);
            if (bearing < 0) bearing += 360.0;

            _engine.SetDesiredHeading(bearing);
            _engine.SetDesiredSpeed(10.0); // konstant marsjfart
        }

        public bool IsActive => _active;
    }

}