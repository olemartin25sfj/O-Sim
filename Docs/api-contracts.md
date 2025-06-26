# API-kontrakter og meldingsformater for O-Sim

Dette dokumentet definerer de standardiserte meldingskontraktene som brukes for kommunikasjon mellom de ulike komponentene i O-Sim-systemet. Klare kontrakter er essensielle for et løst koblet mikrotjenestesystem.

Alle meldinger er i JSON-format.

## 1. Systemmeldinger (Generelle)

### 1.1 LogEntry

- **NATS-Emne:** `log.entries`
- **Beskrivelse:** Standard loggmelding fra enhver tjeneste for sentralisert logging.
- **Publiseres av:** Alle mikrotjenester.
- **Abonneres av:** `LoggerService`.
- **Struktur:**
  ```json
  {
    "Timestamp": "yyyy-MM-ddTHH:mm:ssZ", // ISO 8601 format (UTC)
    "Service": "string", // Navn på tjenesten som logget (f.eks. "SimulatorService")
    "Level": "string", // Loggnivå (f.eks. "Information", "Warning", "Error", "Debug")
    "Message": "string", // Loggmeldingstekst
    "Details": "object" // Valgfritt: Ekstra detaljer som et JSON-objekt
  }
  ```

Eksempel:

```JSON
{
"Timestamp": "2025-06-26T10:30:15Z",
"Service": "SimulatorService",
"Level": "Information",
"Message": "Vessel position updated.",
"Details": {
"Lat": 59.04944,
"Lon": 10.20333
}
}
```

## 2. Sensordata (simulator-generert)

### 2.1 NavigationData

- **NATS-emne:** `sim.sensors.nav`
- **Beskrivelse:** Sanntids navigasjonsdata fra det simulerte fartøyet.
- **Publiseres av:** `SimulatorService`.
- **Abonneres av:** `AutopilotService`, `LoggerService`, `Frontend GUI`(via API Gateway).
- **Struktur:**

```JSON
{
  "Timestamp": "yyyy-MM-ddTHH:mm:ssZ",
  "Latitude": "number",               // Breddegrad i desimalgrader
  "Longitude": "number",              // Lengdegrad i desimalgrader
  "SpeedOverGroundKnots": "number",   // Fart over grunn i knop
  "CourseOverGroundDegrees": "number",// Kurs over grunn i grader (0-359.9, True North)
  "HeadingDegrees": "number",         // Faktisk heading i grader (0-359.9, True North)
  "RotDegreesPerMinute": "number",    // Rate of Turn i grader per minutt (positiv for styrbord, negativ for babord)
  "RollDegrees": "number",            // Rulling i grader (positiv for styrbord, negativ for babord)
  "PitchDegrees": "number",           // Stamping i grader (positiv for baug opp, negativ for baug ned)
  "HeaveMeters": "number"             // Vertikal bevegelse (heving/senkning) i meter
}
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:20Z",
  "Latitude": 59.05000,
  "Longitude": 10.20500,
  "SpeedOverGroundKnots": 12.0,
  "CourseOverGroundDegrees": 125.0,
  "HeadingDegrees": 126.2,
  "RotDegreesPerMinute": 0.5,
  "RollDegrees": -1.2,
  "PitchDegrees": 0.8,
  "HeaveMeters": 0.1
}
```

### 2.2. EnvironmentData

- **NATS-emne:** `sim.sensors.env`
- **Beskrivelse:** Sanntids miljødata som påvirker fartøyet.
- **Publiseres av:** `EnvironmentService`
- **Abonneres av:** `AutopilotService`, `LoggerService`, `Frontend GUI`(via API Gateway).
- **Struktur:**

```JSON
{
  "Timestamp": "yyyy-MM-ddTHH:mm:ssZ",
  "WindSpeedKnots": "number",         // Vindhastighet i knop
  "WindDirectionDegrees": "number",   // Vindretning i grader (0-359.9, True Wind from)
  "CurrentSpeedKnots": "number",      // Strømhastighet i knop
  "CurrentDirectionDegrees": "number",// Strømretning i grader (0-359.9, True Current set)
  "WaveHeightMeters": "number",       // Signifikant bølgehøyde i meter
  "WaveDirectionDegrees": "number",   // Bølgeretning i grader (0-359.9, True Wave from)
  "WavePeriodSeconds": "number"       // Bølgeperiode i sekunder
}
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:20Z",
  "WindSpeedKnots": 15.0,
  "WindDirectionDegrees": 270.0,
  "CurrentSpeedKnots": 1.0,
  "CurrentDirectionDegrees": 90.0,
  "WaveHeightMeters": 2.5,
  "WaveDirectionDegrees": 200.0,
  "WavePeriodSeconds": 7.5
}
```

## 3. Kommandoer

### 3.1. SetCourseCommand

- **NATS.emne:** `sim.commands.setcourse`
- **Beskrivelse:** Kommando for å sette ønsket kurs for AutopilotService.
- **Publiseres av:** `Frontend GUI`(via API Gateway).
- **Abonneres av:** `AutopilotService`, `LoggerService`.
- **Struktur:**

```JSON
{
  "Timestamp": "yyyy-MM-ddTHH:mm:ssZ",
  "TargetCourseDegrees": "number"     // Ønsket kurs i grader (0-359.9)
}
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:25Z",
  "TargetCourseDegrees": 135.0
}
```

### 3.2. SetSpeedCommand

- **NATS.emne:** `sim.commands.setspeed`
- **Beskrivelse:** Kommando for å sette ønsket fart for AutopilotService.
- **Publiseres av:** `Frontend GUI`(via API Gateway).
- **Abonneres av:** `AutopilotService`, `LoggerService`.
- **Struktur:**

```JSON
{
  "Timestamp": "yyyy-MM-ddTHH:mm:ssZ",
  "TargetSpeedKnots": "number"        // Ønsket fart i knop
}
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:25Z",
  "TargetSpeedKnots": 10.0
}
```

### 3.3. RudderCommand

- **NATS.emne:** `sim.commands.rudder`
- **Beskrivelse:** Kommando for rorutslag til SimulatorService.
- **Publiseres av:** `AutopilotService` (og potensielt `FrontEnd GUI` for manuell kontroll).
- **Abonneres av:** `SimulatorService`, `LoggerService`.
- **Struktur:**

```JSON
{
  "Timestamp": "yyyy-MM-ddTHH:mm:ssZ",
  "RudderAngleDegrees": "number"      // Rorutslag i grader (f.eks. -35 til +35, negativ for babord, positiv for styrbord)
}
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:26Z",
  "RudderAngleDegrees": 5.0
}
```

### 3.4. ThrustCommand

- **NATS.emne:** `sim.commands.thrust`
- **Beskrivelse:** Kommando for thrust (fremdrift) til SimulatorService.
- **Publiseres av:** `AutopilotService` (og potensielt `FrontEnd GUI` for manuell kontroll).
- **Abonneres av:** `SimulatorService`, `LoggerService`.
- **Struktur:**

```JSON
{
  "Timestamp": "yyyy-MM-ddTHH:mm:ssZ",
  "ThrustPercentage": "number"        // Thrust som prosent av maks (f.eks. 0.0 til 100.0)
}
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:26Z",
  "ThrustPercentage": 75.0
}
```

### 3.5. EnvironmentModeCommand

- **NATS.emne:** `env.commands.setmode`
- **Beskrivelse:** Kommando for å sette en spesifikk miljømodus i `EnvirontmentService`.
- **Publiseres av:** `Frontend GUI` (via API Gateway).
- **Abonneres av:** `EnvironmentService`, `LoggerService`.
- **Struktur:**

```JSON
{
  "Timestamp": "yyyy-MM-ddTHH:mm:ssZ",
  "ModeName": "string"                // Navn på miljømodus (f.eks. "Calm", "WindyDay", "FullStorm")
}
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:27Z",
  "ModeName": "FullStorm"
}
```

## 4. Alarm- og Statusmeldinger

### 4.1. AlarmTriggered

- **NATS.emne:** `alarm.triggers`
- **Beskrivelse:** Melding som indikerer at en alarm har blitt utløst.
- **Publiseres av:** `AlarmSerivce`
- **Abonneres av:** `LoggerService`, `Frontend GUI` (via API Gateway).
- **Struktur:**

```JSON
{
  "Timestamp": "yyyy-MM-ddTHH:mm:ssZ",
  "AlarmId": "string",                  // Unik ID for alarmtypen (f.eks. "Overspeed", "OffCourseDeviation")
  "Severity": "string",                 // Alvorlighetsgrad (f.eks. "Warning", "Critical", "Informational")
  "Message": "string",                  // Beskrivelse av alarmen
  "CurrentValue": "number",             // Verdien som utløste alarmen
  "Threshold": "number",                // Grenseverdien som ble brutt
  "Unit": "string"                      // Måleenhet for verdi og grense (f.eks. "knots", "degrees")
}
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:31:00Z",
  "AlarmId": "OffCourseDeviation",
  "Severity": "Warning",
  "Message": "Vessel deviated significantly from target course.",
  "CurrentValue": 10.5,
  "Threshold": 5.0,
  "Unit": "degrees"
}
```
