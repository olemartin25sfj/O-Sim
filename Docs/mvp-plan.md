# O-Sim MVP Utviklingsplan

## 1. Innledning og Formål

Dette dokumentet skisserer utviklingsplanen for en Minimum Viable Product (MVP) av O-Sim-systemet. O-Sim er et simuleringsprosjekt for et autonomt fartøy i et virtuelt miljø, utviklet med fokus på å utforske mikrotjenestearkitektur og meldingsbasert kommunikasjon.

Målet med denne MVP-en er å etablere en grunnleggende, men funksjonell, vertikal "slice" av systemet. Dette vil validere de valgte arkitekturprinsippene, sikre kjernekomponentenes samhandling, og legge et solid grunnlag for fremtidig utvidelse og ytterligere læring. MVP-en vil prioritere kjernefunksjonalitet som demonstrerer fartøyets bevegelse, miljøpåvirkning, grunnleggende kontroll og datalogging.

## 2. Definisjon av MVP-funksjonalitet

MVP-en av O-Sim vil omfatte følgende kjernefunksjonalitet:

- **Grunnleggende Fartøysimulering:** En `SimulatorService` som modellerer fartøyets bevegelse i 2D (posisjon, kurs, fart, heading) og genererer tilhørende navigasjonsdata.
- **Grunnleggende Miljøsimulering:** En `EnvironmentService` som genererer statiske eller enkelt dynamiske parametre for vind, strøm og bølger, og simulerer deres innvirkning på fartøyet.
- **Datakommunikasjon:** All intern systemkommunikasjon skal etableres og fungere via NATS, basert på forhåndsdefinerte meldingskontrakter.
- **Enkel Visualisering:** En grunnleggende grafisk brukergrensesnitt (GUI), i første omgang implementert i WPF, som viser fartøyets sanntidsposisjon på et kart og presenterer viktige navigasjons- og miljødata.
- **Grunnleggende Kontroll:** Mulighet for brukeren til å sende en enkel kommando (f.eks. "sett kurs") til `AutopilotService`, som deretter sender nødvendige ror- og/eller thrust-kommandoer til `SimulatorService` for å påvirke fartøyets bevegelse.
- **Logging og Datapersistens:** En `LoggerService` som fanger opp og persisterer sentrale meldinger og sensordata fra systemet for senere analyse og feilsøking.

## 3. Utviklingstrinn (Iterasjoner)

Utviklingen av MVP-en vil følge en trinnvis tilnærming, der hvert trinn bygger på funksjonaliteten etablert i det foregående. Målet er å ha et _kjørbart og funksjonelt_ system etter fullføring av hvert trinn, noe som muliggjør kontinuerlig testing og validering.

### Trinn 1: Kjerne-infrastruktur og Simulatormodell

**Mål:** Etablere den grunnleggende kommunikasjonsinfrastrukturen og en funksjonell simulatortjeneste som genererer data.

- **Oppsett og Miljø:**
  - Initialisere Git-repository og definere den overordnede prosjektstrukturen.
  - Sette opp og konfigurere NATS-serveren i `docker-compose.yml`. Verifisere at NATS-serveren er operasjonell og tilgjengelig for tilkobling.
- **`SimulatorService` Implementasjon:**
  - Opprette et nytt C# (.NET 9) prosjekt for `SimulatorService`.
  - Implementere en enkel 2D-fartøysmodell som genererer navigasjonsdata (posisjon, fart, kurs, heading) basert på en initial tilstand (f.eks., konstant fart og kurs, eller respons på et hardkodet rorutslag).
  - Integrere en NATS-klient i tjenesten.
  - Konfigurere tjenesten til å publisere `NavigationData` (til NATS-emnet `sim.sensors.nav`) med et fast, definert intervall (f.eks. 100ms).
  - Initialisere grunnleggende konsolllogging ved hjelp av `Microsoft.Extensions.Logging`.
- **Verifisering:**
  - Kjør `SimulatorService` isolert i en Docker-container.
  - Sjekk konsolllogger for bekreftelse av drift.
  - Bruk NATS CLI (`nats sub sim.sensors.nav`) for å bekrefte at `NavigationData`-meldinger blir korrekt publisert på NATS-bussen.

### Trinn 2: Miljøsimulering, Sentralisert Logging og Felles Kontrakter

**Mål:** Integrere miljødata i simulatoren og etablere en sentralisert logg- og datalagringsmekanisme.

- **Felles Meldingskontrakter:**
  - Opprette et delt C# bibliotekprosjekt (`shared/OSim.Shared.Messages`).
  - Definere C# Data Transfer Objects (DTOs) for alle sentrale meldinger (`NavigationData`, `EnvironmentData`, `LogEntry`, `SetCourseCommand`, `RudderCommand`, `ThrustCommand`, etc.) i henhold til `docs/api-contracts.md`.
- **`EnvironmentService` Implementasjon:**
  - Opprette et nytt C# (.NET 9) prosjekt for `EnvironmentService`.
  - Implementere generering av statiske eller enkelt dynamiske `WindData`, `CurrentData` og `WaveData` parametre.
  - Konfigurere tjenesten til å publisere `EnvironmentData` (til NATS-emnet `sim.sensors.env`) med et fast intervall.
- **`LoggerService` Implementasjon:**
  - Opprette et nytt C# (.NET 9) prosjekt for `LoggerService`.
  - Konfigurere tjenesten til å abonnere på relevante NATS-emner som `sim.sensors.nav`, `sim.sensors.env` og `log.entries`.
  - Implementere enkel datapersistens for de mottatte meldingene, f.eks. til CSV-filer eller en enkel SQLite-database.
- **Sentralisert Logging:**
  - Integrere NATS-basert loggpublisering i alle mikrotjenester (dvs. `SimulatorService` og `EnvironmentService`).
  - Konfigurere disse tjenestene til å sende sine logger til `LoggerService` via NATS-emnet `log.entries`.
- **Verifisering:**
  - Oppdatere `docker-compose.yml` for å inkludere `EnvironmentService` og `LoggerService`.
  - Kjør alle tjenester i Docker Compose.
  - Bekreft at miljødata publiseres fra `EnvironmentService` og at alle relevante data, inkludert systemlogger, persisteres korrekt av `LoggerService`.

### Trinn 3: Frontend GUI (WPF) og API Gateway

**Mål:** Etablere brukergrensesnittet og tilkoblingen til backend-tjenestene for sanntids visualisering.

- **API Gateway (Traefik) Konfigurasjon:**
  - Legge til Traefik-tjenesten i `docker-compose.yml` og konfigurere den som systemets API Gateway.
  - Konfigurere Traefik til å rute innkommende HTTP-forespørsler (f.eks. for kommandoer) og WebSocket-tilkoblinger (for sanntidsdata) til de korresponderende backend-tjenestene. Dette kan kreve en lettvekts-proxy-tjeneste i C# for å håndtere WebSocket-abonnementer fra NATS til Frontend.
- **Frontend GUI (WPF) Implementasjon:**
  - Opprette et nytt WPF-applikasjonsprosjekt.
  - Implementere en grunnleggende kartvisning. Dette kan involvere bruk av OpenStreetMap-fliser eller en forenklet grafisk representasjon av et sjøområde.
  - Integrere en WebSocket-klient for å abonnere på sanntids `NavigationData` og `EnvironmentData` via API Gateway.
  - Visualisere fartøyets posisjon, kurs og andre relevante data på kartet.
  - Inkludere enkle tekstfelter eller paneler for å vise numeriske verdier for fart, kurs, vind, strøm og bølger.
- **Verifisering:**
  - Kjør hele systemet via Docker Compose.
  - Bekreft at WPF-applikasjonen starter og mottar sanntidsdata fra backend, og at fartøyets posisjon og data oppdateres korrekt på kartet.

### Trinn 4: Grunnleggende Autopilot og Manuell Kontroll

**Mål:** Implementere kjernen av fartøyets kontrollsystem, muliggjøre interaktiv styring, og koble den til simulatoren.

- **`AutopilotService` Implementasjon:**
  - Opprette et nytt C# (.NET 9) prosjekt for `AutopilotService`.
  - Konfigurere tjenesten til å abonnere på `sim.sensors.nav` (for fartøyets nåværende tilstand) og `sim.sensors.env` (for miljøpåvirkninger).
  - Implementere abonnement på `sim.commands.setcourse` for å motta ønsket kurs fra GUI.
  - Implementere en **enkel kurs-holder** (f.eks. en proporsjonal P-kontroller) som beregner nødvendig rorutslag basert på avviket mellom ønsket og faktisk kurs.
  - Publisere `RudderCommand` (til NATS-emnet `sim.commands.rudder`) til `SimulatorService`.
  - _(Valgfritt, for utvidet funksjonalitet i MVP)_: Implementere en lignende mekanisme for `SetSpeedCommand` og `ThrustCommand`.
- **`SimulatorService` Utvidelse:**
  - Utvide `SimulatorService` til å abonnere på og behandle `RudderCommand` og `ThrustCommand` meldinger fra NATS. Disse kommandoene skal direkte påvirke fartøyets dynamikk.
- **Frontend GUI (WPF) Utvidelse:**
  - Legge til et input-felt for brukeren å angi et ønsket kurs, samt en knapp for å sende `SetCourseCommand` via API Gateway (HTTP POST) til `AutopilotService`.
  - _(Valgfritt)_: Legge til lignende kontroller for å sette fart.
- **Verifisering:**
  - Kjør hele systemet.
  - Endre ønsket kurs via GUI og observer at fartøyet endrer kurs i simuleringen, og at GUI reflekterer denne endringen. Bekreft at `RudderCommand` og `ThrustCommand` meldinger utveksles som forventet.

### Trinn 5: Enkel Alarm-håndtering

**Mål:** Demonstrere funksjonalitet for avviksdeteksjon og varsling.

- **`AlarmService` Implementasjon:**
  - Opprette et nytt C# (.NET 9) prosjekt for `AlarmService`.
  - Konfigurere tjenesten til å abonnere på relevante datastrømmer, f.eks., `sim.sensors.nav`.
  - Implementere en eller flere enkle alarmregler (f.eks., "fartøyets fart overstiger en definert grense", "kursavvik større enn X grader").
  - Publisere `AlarmTriggered` meldinger (til NATS-emnet `alarm.triggers`) når en alarmregel brytes.
- **`LoggerService` Utvidelse:**
  - Konfigurere `LoggerService` til å abonnere på `alarm.triggers` for å persistere alle utløste alarmer.
- **Frontend GUI (WPF) Utvidelse:**
  - Utvide WPF-applikasjonen til å abonnere på `alarm.triggers` via WebSocket fra API Gateway.
  - Implementere en enkel visuell representasjon av utløste alarmer, f.eks. en liste over aktive alarmer eller et popup-varsel.
- **Verifisering:**
  - Kjør hele systemet.
  - Fremprovoser en alarm (f.eks., ved å manuelt overstyre hastigheten til over en definert grense), og bekreft at alarmen detekteres av `AlarmService`, logges av `LoggerService`, og vises i Frontend GUI.

---
