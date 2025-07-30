# Systemarkitektur

## 1. Innledning

Dette dokumentet beskriver systemarkitekturen for O-Sim, et personlig utviklingsprosjekt designet for å simulere et autonomt fartøy i et virtuelt miljø. Hovedformålet med O-Sim er å fungere som en praktisk plattform for egen læring og utforskning av moderne programvarearkitektur.

Gjennom dette prosjektet ønsker jeg å tilegne meg erfaring med:

- **Modulær systemdesign**: Hvordan dele et komplekst problem inn i mindre, håndterbare deler.
- **Mikrotjenester**: Forstå prinsipper for uavhengige tjenester, deres fordeler, utfordringer og eventuelle ulemper.
- **Meldingsbasert kommunikasjon**: Erfaring med asynkron kommunikasjon og publish/subscribe-mønstre via NATS.
- **Praktisk utvikling**: Fra konsept til implementering, inkludert testing og distribusjon med Docker.

O-Sim vil gi mulighet til å kontrollere fartøyet via autopilot, visualisere reisen på kart, og hente inn simulerte sensordata. De kommende seksjonene vil gi en oversikt over systemets oppbygning, beskrive de individuelle komponentene, og forklare hvordan de samhandler for å oppnå funksjonaliteten. Målet er å skape et solid fundament for videre læring og eksperimentering.

---

## 2. Overordnet arkitekturprinsipp: Mikrotjenester

O-Sim er bygget rundt prinsippene for en mikrotjenestearkitektur. Dette valget er fundamentalt for prosjektets design og reflekterer et viktig læringsmål: å forstå hvordan komplekse systemer kan brytes ned og organiseres i mindre, uavhengige og håndterbare enheter.

### 2.1 Hva er mikrotjenester?

En mikrotjenestearkitektur er en tilnærming til programvareutvikling der en enkelt applikasjon er bygget som en samling av små, løst koblede og uavhengige distribuerbare tjenester. Hver tjeneste fokuserer på å løse et spesifikt problem eller utføre én bestemt funksjon.

I motsetning til den tradisjonelle monolittiske applikasjonen, hvor all funksjonalitet er pakket inn i en enkelt, stor og tett koblet kodebase, vil hver mikrotjeneste:

- Ha et spesifikt, veldefinert ansvar
- Kunne utvikles og distribueres uavhengig
- Kommunisere via lette mekanismer (REST, gRPC, meldingskøer som NATS)
- Potensielt bruke ulike teknologier

### 2.2 Hvorfor mikrotjenester for O-Sim?

Valget av mikrotjenestearkitektur for O-Sim er drevet av:

- **Fokus på læring og utforsking**: Gir erfaring med inter-tjenestekommunikasjon, tjenestegrenser og uavhengig distribusjon.
- **Klarere tjenestegrenser og kontrakter**: Fremtvinger disiplin rundt API-design og meldingsformater.
- **Testbarhet**: Tjenester kan testes og kjøres isolert.
- **Skalerbarhet og distribusjon**: Endringer i én tjeneste påvirker ikke andre.

Selv om det introduserer mer kompleksitet i infrastruktur og drift, gir det viktige pedagogiske fordeler.

---

## 3. Systemkomponenter

O-Sim består av løst koblede komponenter som sammen gir simulerings- og kontrollfunksjonalitet. Arkitekturen bygger på en meldingsbuss for asynkron kommunikasjon mellom tjenester.

![Systemarkitektur](Systemarkitektur_O-Sim.svg)

---

### 3.1 NATS

**Rolle og formål:**

- Sentral meldingsmekler
- Løs kobling mellom tjenester
- Asynkron kommunikasjon med publish/subscribe

**Valg av NATS fremfor Kafka:**

- Lav kompleksitet
- Sanntidsoptimalisering
- Ressursvennlig

**Kommunikasjonsmønstre:**

- **Publish/Subscribe**: En-til-mange kommunikasjon
- **Request/Reply**: For synkrone behov

**Eksempler på emner:**

- `sim.sensors.nav`
- `sim.sensors.env`
- `sim.commands.*`
- `log.entries`
- `alarm.triggers`
- `env.commands.setmode`

**Kontrakter:**

- Se `docs/api-contracts.md`
- Implementert som DTO-er i `shared/O-Sim.Shared.Messages`

---

### 3.2 API Gateway (Traefik)

**Rolle og formål:**

- Felles inngangspunkt for eksterne klienter
- Abstraherer mikrotjenester fra frontend

**Hvorfor Traefik:**

- Dynamisk tjenesteoppdagelse
- Docker-integrasjon
- Lettvekts og dashbord-støtte

**Nøkkelroller i O-Sim:**

- HTTP/WS-routing
- Lastbalansering

---

### 3.3 Mikrotjenester

Hver tjeneste har et tydelig ansvar og kommuniserer via NATS.

#### 3.3.1 SimulatorService

**Ansvar:**

- Simulerer fartøyets bevegelse og dynamikk

**Innkommende:**

- `sim.commands.*`

**Utgående:**

- `sim.sensors.nav`

**Nøkkelfunksjoner:**

- Modellering av posisjon, kurs, fart osv.
- Reaksjon på kommandoer

#### 3.3.2 EnvironmentService

**Ansvar:**

- Simulerer vind, strøm og bølger

**Innkommende:**

- `env.commands.setmode`

**Utgående:**

- `sim.sensors.env`

**Nøkkelfunksjoner:**

- Generering av miljødata
- Støtte for flere simuleringsmoduser

**Fremtidig utvidelse:**

- Siktforhold, dybde, eksterne API-er

#### 3.3.3 AutopilotService

**Ansvar:**

- Regulerer kurs og fart basert på settpunkter og sensordata

**Innkommende:**

- `sim.sensors.nav`
- `sim.sensors.env`
- `sim.commands.setcourse`
- `sim.commands.setspeed`

**Utgående:**

- `sim.commands.rudder`
- `sim.commands.thrust`

**Nøkkelfunksjoner:**

- PID-kontroll
- Kurs- og fartsregulering

#### 3.3.4 AlarmService

**Ansvar:**

- Detekterer og publiserer alarmer basert på innkommende data

**Innkommende:**

- `sim.sensors.nav`
- `sim.sensors.env`
- `log.entries`

**Utgående:**

- `alarm.triggers`

**Nøkkelfunksjoner:**

- Regeldrevet overvåking
- Alarmgenerering

#### 3.3.5 LoggerService

**Ansvar:**

- Sentral lagring av alle relevante meldinger

**Innkommende:**

- Abonnerer bredt (alle relevante emner)

**Utgående:**

- Persistens til CSV eller database

**Nøkkelfunksjoner:**

- Mottak og lagring av meldinger
- (Fremtidig) avspilling og analyse

---

### 3.4 Frontend GUI

**Rolle:**

- Brukergrensesnitt for kontroll og visualisering

**Implementasjon:**

- WPF først, deretter Next.js

**Kommunikasjon:**

- Via API Gateway (REST + WebSocket)

**Funksjoner:**

- Visning av sanntidsdata
- Kartvisning
- Input for kurs/fart
- Alarmvisning

---

## 4. Tverrgående aspekter

### 4.1 Teknologistack

- **Backend**: C# (.NET 9)
- **Frontend**: WPF (desktop), Next.js (web)
- **Kommunikasjon**: NATS
- **Gateway**: Traefik
- **Containere**: Docker Compose

### 4.2 Kommunikasjonskontrakter

- Definert i `docs/api-contracts.md`
- Implementert som DTO-er i `OSim.Shared.Messages`
- Type-sikkerhet og enkel serialisering

### 4.3 Konfigurasjonshåndtering

- **Miljøvariabler**: For runtime-konfigurasjon
- **appsettings.json**: For stabile innstillinger

### 4.4 Observabilitet

- **Logging**: Standard .NET-logging → `log.entries`
- **Health checks**: HTTP-endepunkt (`/health`) for hver tjeneste
- **Bruk i Traefik/Docker Compose**: Overvåk og restart ved behov

---
