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

        // Miljøpåvirkning

        private double _windDirection; // grader
        private double _windSpeed; // knop
        private double _currentDirection; // strøm
        private double _currentSpeed; // knop

        public void SetHeading(double heading) => _heading = heading;
        public void SetSpeed(double speed) => _speed = speed;

        public void SetWind(double directionDeg, double SpeedKnots)
        {
            _windDirection = directionDeg;
            _windSpeed = SpeedKnots;
        }


        public void SetCurrent(double directionDeg, double SpeedKnots)
        {
            _currentDirection = directionDeg;
            _currentSpeed = SpeedKnots;
        }

        public void Update(TimeSpan deltaTime)
        {
            double seconds = deltaTime.TotalSeconds;
            double distanceShip = _speed * (seconds / 3600.0);
            double distanceWind = _windSpeed * (seconds / 3600.0);
            double distanceCurrent = _currentSpeed * (seconds / 3600.0);

            // Totale bevegelser i X/Y-retning i grader

            double dx = 0.0;
            double dy = 0.0;

            // Fartøyets bevegelse
            dx += distanceShip * Math.Cos(DegToRad(_heading));
            dy += distanceShip * Math.Sin(DegToRad(_heading));

            // Vinddrift
            dx += distanceWind * Math.Cos(DegToRad(_windDirection));
            dy += distanceWind * Math.Sin(DegToRad(_windDirection));

            // Strømdrift
            dx += distanceCurrent * Math.Cos(DegToRad(_currentDirection));
            dy += distanceCurrent * Math.Sin(DegToRad(_currentDirection));

            // Endre posisjon (1° = ca 60 nm)
            _latitude += dy / 60.0;
            _longitude += dx / 60.0;
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

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;
    }
}