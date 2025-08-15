# O-Sim

O-Sim er et mikrotjeneste-basert skipssimuleringssystem designet for å simulere et autonomt fartøy i et virtuelt miljø. Systemet bruker NATS for meldingsutveksling og Traefik for API-ruting.

## Hurtigstart

1. Klon repositoriet
2. Bygg tjenestene: `docker-compose build`
3. Start systemet: `docker-compose up`
4. Åpne http://localhost:8080 for Traefik-dashboardet
5. Test systemet (se [Testing](#testing) nedenfor)

For detaljert arkitekturbeskrivelse, se [Docs/Systemarkitektur.md](Docs/Systemarkitektur.md).

## Systemkomponenter

Systemet består av følgende hovedkomponenter:

- **SimulatorService** - Håndterer selve skipssimuleringen
- **EnvironmentService** - Genererer miljødata (vind, strøm, bølger)
- **LoggerService** - Logger alle hendelser og data
- **AutopilotService** - Styrer skipet automatisk mot et mål
- **GatewayProxy** - WebSocket-proxy for sanntidsdata

### Portkonfigurasjoner

| Tjeneste           | Intern Port | API-endepunkt        |
| ------------------ | ----------- | -------------------- |
| SimulatorService   | 5001        | `/api/simulator/*`   |
| EnvironmentService | 5002        | `/api/environment/*` |
| LoggerService      | 5003        | `/api/logs/*`        |
| AlarmService       | 5004        | `/api/alarm/*`       |
| GatewayProxy       | 5000        | `/ws/*`              |
| WebDashboard       | 3000        | `/`                  |
| NATS               | 4222, 8222  | -                    |
| Traefik            | 80, 8080    | -                    |

### API-endepunkter

Alle API-endepunkter er tilgjengelige via Traefik på port 80. Tilgjengelige endepunkter:

- `GET /api/simulator/status` - Status for simulatoren
- `GET /api/environment/status` - Status for miljøtjenesten
- `GET /api/logs/status` - Status for loggetjenesten
- `GET /api/alarm/status` - Status for AlarmService
- `GET /api/alarm/active` - Aktive alarmer
- `WS /ws/nav` - WebSocket (nav-/env-/alarm-meldinger via samme endpoint)

## Oppsett og Kjøring

### Forutsetninger

- Docker og Docker Compose
- .NET 8.0 SDK (for utvikling)

### Kjøring

1. Bygg alle tjenester:

   ```bash
   docker-compose build
   ```

2. Start systemet:

   ```bash
   docker-compose up
   ```

3. Åpne Traefik dashboard:
   http://localhost:8080

4. Åpne NATS monitoring:
   http://localhost:8222

## Testing

Etter at systemet er startet kan du verifisere at alt fungerer:

1. Sjekk tjenestestatus:

   ```bash
   curl http://localhost/api/simulator/status
   curl http://localhost/api/environment/status
   curl http://localhost/api/logs/status
   ```

2. Se på NATS-meldingsstrømmen:

   - Åpne http://localhost:8222
   - Gå til "Connections" for å se aktive tilkoblinger
   - Sjekk "Monitoring" for meldingsstatistikk

3. Verifiser WebSocket-tilkobling:
   ```javascript
   // I nettleserens konsoll:
   ws = new WebSocket("ws://localhost/ws/nav");
   ws.onmessage = (msg) => console.log(JSON.parse(msg.data));
   ```

## Utvikling

### Teknologier

- .NET 8.0 for backend-tjenester
- React + Vite WebDashboard (erstatter initial WPF plan)
- NATS for meldingsutveksling
- Traefik for HTTP routing + GatewayProxy for WebSocket
- Docker Compose for orkestrering

### Lokal Utvikling

For å kjøre en enkelt tjeneste lokalt:

1. Start NATS og andre avhengigheter:

   ```bash
   docker-compose up nats
   ```

2. Kjør tjenesten lokalt:
   ```bash
   cd src/SimulatorService
   dotnet run
   ```

### Utviklingsressurser

- [Systemarkitektur](Docs/Systemarkitektur.md) - Detaljert arkitekturbeskrivelse
- [API Contracts](Docs/api-contracts.md) - Meldingsformater og API-definisjoner
- [MVP Plan](Docs/mvp-plan.md) - Utviklingsplan og milepæler
- [Idéer til fremtidige utvidelser og forbedringer](Docs/Idéer-til-fremtidige-utvidelser-og-forbedringer.md) - Forslag/roadmap utover MVP (inkl. ECDIS‑lite)
