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
- **Beskrivelse:** Sanntids grunnleggende navigasjonsdata fra det simulerte fartøyet.
- **Publiseres av:** `SimulatorService`.
- **Abonneres av:** `AutopilotService`, `LoggerService`, `Frontend` (via GatewayProxy).
- **Record (C#):** `NavigationData(DateTime TimestampUtc, double Latitude, double Longitude, double SpeedKnots, double HeadingDegrees, double CourseOverGroundDegrees)`
- **JSON-felter:**

```json
{
  "timestampUtc": "2025-06-26T10:30:20Z",
  "latitude": 59.05,
  "longitude": 10.205,
  "speedKnots": 12.0,
  "headingDegrees": 126.2,
  "courseOverGroundDegrees": 125.0
}
```

Tillegg (framtidig utvidelse): ROT / Roll / Pitch / Heave kan innføres i egen utvidet melding `NavigationMotionData` for å holde basis lettvekts.

### 2.2 EnvironmentData

- **NATS-emne:** `sim.sensors.env`
- **Publiseres av:** `EnvironmentService`
- **Abonneres av:** `SimulatorService`, `AutopilotService`, `LoggerService`, `Frontend`.
- **Record (C#):** `EnvironmentData(DateTime TimestampUtc, EnvironmentMode Mode, double WindSpeedKnots, double WindDirectionDegrees, double CurrentSpeedKnots, double CurrentDirectionDegrees, double WaveHeightMeters, double WaveDirectionDegrees, double WavePeriodSeconds)`
- **Enums:** `EnvironmentMode { Static, Dynamic, Storm, Calm }`
- **JSON-eksempel:**

```json
{
  "timestampUtc": "2025-06-26T10:30:20Z",
  "mode": "Dynamic",
  "windSpeedKnots": 12.4,
  "windDirectionDegrees": 270.0,
  "currentSpeedKnots": 1.1,
  "currentDirectionDegrees": 95.0,
  "waveHeightMeters": 1.8,
  "waveDirectionDegrees": 210.0,
  "wavePeriodSeconds": 6.2
}
```

## 3. Kommandoer

### 3.1 SetCourseCommand

- **NATS-emne:** `sim.commands.setcourse`
- **Record:** `SetCourseCommand(DateTime TimestampUtc, double TargetCourseDegrees)`
- **JSON:** `{ "timestampUtc": "2025-06-26T10:30:25Z", "targetCourseDegrees": 135.0 }`

### 3.2 SetSpeedCommand

- **NATS-emne:** `sim.commands.setspeed`
- **Record:** `SetSpeedCommand(DateTime TimestampUtc, double TargetSpeedKnots)`
- **JSON:** `{ "timestampUtc": "2025-06-26T10:30:25Z", "targetSpeedKnots": 10.0 }`

### 3.3 RudderCommand

- **NATS.emne:** `sim.commands.rudder`
- **Beskrivelse:** Kommando for rorutslag til SimulatorService.
- **Publiseres av:** `AutopilotService` (og potensielt `FrontEnd GUI` for manuell kontroll).
- **Abonneres av:** `SimulatorService`, `LoggerService`.
- **Struktur:**

```json
{ "timestampUtc": "2025-06-26T10:30:26Z", "rudderAngleDegrees": 5.0 }
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:26Z",
  "RudderAngleDegrees": 5.0
}
```

### 3.4 ThrustCommand

- **NATS.emne:** `sim.commands.thrust`
- **Beskrivelse:** Kommando for thrust (fremdrift) til SimulatorService.
- **Publiseres av:** `AutopilotService` (og potensielt `FrontEnd GUI` for manuell kontroll).
- **Abonneres av:** `SimulatorService`, `LoggerService`.
- **Struktur:**

```json
{ "timestampUtc": "2025-06-26T10:30:26Z", "thrustPercent": 75.0 }
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:26Z",
  "ThrustPercentage": 75.0
}
```

### 3.5 SetEnvironmentModeCommand

- **NATS.emne:** `env.commands.setmode`
- **Beskrivelse:** Kommando for å sette en spesifikk miljømodus i `EnvirontmentService`.
- **Publiseres av:** `Frontend GUI` (via API Gateway).
- **Abonneres av:** `EnvironmentService`, `LoggerService`.
- **Struktur:**

```json
{ "timestampUtc": "2025-06-26T10:30:27Z", "mode": "Storm" }
```

Eksempel:

```JSON
{
  "Timestamp": "2025-06-26T10:30:27Z",
  "ModeName": "FullStorm"
}
```

## 4. Alarm- og Statusmeldinger

### 4.1 AlarmTriggered

- **NATS.emne:** `alarm.triggers`
- **Beskrivelse:** Melding som indikerer at en alarm har blitt utløst.
- **Publiseres av:** `AlarmSerivce`
- **Abonneres av:** `LoggerService`, `Frontend GUI` (via API Gateway).
- **Struktur:**

**NATS-emne:** `alarm.triggers`

**Record:** `AlarmTriggered(DateTime TimestampUtc, string AlarmType, string Message, AlarmSeverity Severity)`

**Enum:** `AlarmSeverity { Info, Warning, Critical }`

```json
{
  "timestampUtc": "2025-06-26T10:31:00Z",
  "alarmType": "OffCourseDeviation",
  "message": "Vessel deviated significantly from target course.",
  "severity": "Warning"
}
```

Utvidelser som terskelverdier / currentValue kan legges i egen melding `AlarmDetail` eller ved å endre record senere (breaking change) – anbefales som separat emne.
