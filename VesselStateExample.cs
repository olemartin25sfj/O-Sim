// Eksempel på en bedre tilnærming med state machine

public enum VesselState
{
    Anchored,       // Ankret - ignorerer alle navigasjonskommandoer
    Navigating,     // Aktiv navigasjon mot mål
    Maneuvering,    // Manuell kontroll (direktekommandoer)
    Drifting        // Fri drift (kun miljøkrefter)
}

public class ImprovedSimulatorEngine
{
    private VesselState _currentState = VesselState.Drifting;
    private double _anchorLat, _anchorLon;

    public void SetAnchored(bool anchored, double? lat = null, double? lon = null)
    {
        if (anchored)
        {
            _currentState = VesselState.Anchored;
            _anchorLat = lat ?? _latitude;
            _anchorLon = lon ?? _longitude;
            _speed = 0; // Umiddelbart stopp
        }
        else
        {
            _currentState = VesselState.Drifting; // Eller Navigating hvis destination finnes
        }
    }

    public void ProcessRudderCommand(double angle)
    {
        // Kun prosesser hvis ikke ankret
        if (_currentState != VesselState.Anchored)
        {
            _rudderAngle = angle;
        }
    }

    public void ProcessThrustCommand(double percent)
    {
        // Kun prosesser hvis ikke ankret  
        if (_currentState != VesselState.Anchored)
        {
            _thrustPercent = percent;
        }
    }

    private void UpdateAnchored(double seconds)
    {
        // Kraftig motstand mot bevegelse
        _speed *= 0.9; // Rask bremsing

        // Anchor pull-back force
        double distance = CalculateDistance(_latitude, _longitude, _anchorLat, _anchorLon);
        if (distance > 3.0) // 3 meter anchor radius
        {
            double bearing = CalculateBearing(_latitude, _longitude, _anchorLat, _anchorLon);
            double pullForce = Math.Min(distance * 0.1, 0.001); // Progressiv kraft

            // Direkte posisjonsjustering
            double dy = pullForce * Math.Cos(DegToRad(bearing));
            double dx = pullForce * Math.Sin(DegToRad(bearing));

            _latitude += dy;
            _longitude += dx;
        }
    }
}

// AutopilotService blir mye enklere:
public class SimplifiedAutopilotService
{
    public void Update()
    {
        // Bare send navigasjonskommandoer - SimulatorEngine bestemmer om de skal ignoreres
        if (HasActiveRoute)
        {
            SendRudderCommand(CalculatePIDOutput());
            SendThrustCommand(CalculateTargetThrust());
        }
        else
        {
            // Send anchor-kommando når ingen aktiv rute
            SendAnchorCommand(true);
        }
    }
}
