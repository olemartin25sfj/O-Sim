using System;
using System.ComponentModel.DataAnnotations;
using OSim.Shared.Messages;

namespace SimulatorService
{
    public class SimulatorEngine
    {
        private double _latitude = 59.0;
        private double _longitude = 10.0;
        private double _heading = 0.0; // grader
        private double _speed = 10.0; // knop

        public SimulatorEngine()
        {

        }

        public void Update(TimeSpan deltaTime)
        {
            double seconds = deltaTime.TotalSeconds;
            double distanceNm = _speed * (seconds / 3600.0); // nm = knop * timer
            double distanceDeg = distanceNm / 60.0; // ca 1° = 60nm

            _latitude += distanceDeg * Math.Cos(_heading * Math.PI / 180.0);
            _longitude += distanceDeg * Math.Sin(_heading * Math.PI / 180.0);
        }

        public VesselState GetCurrentState()
        {
            return new VesselState
            {
                Timestamp = DateTime.UtcNow,
                Latitude = _latitude,
                Longitude = _longitude,
                Heading = _heading,
                Speed = _speed,
            };
        }

        public void SetHeading(double heading) => _heading = heading;
        public void SetSpeed(double speed) => _speed = speed;
    }
}