# Prosjektoversikt – O-Sim

---

## 1. Formål og målsetting

Dette prosjektet har som mål å utvikle et modulært og testbart system for å simulere et autonomt fartøy som seiler i et virtuelt, realistisk miljø. Systemet skal gi mulighet til å kontrollere fartøyet via autopilot, visualisere reisen på kart og hente inn sensordata fra et simuleringsmiljø. Prosjektet fungerer også som en plattform for utforskning av mikrotjenestearkitektur og meldingsbasert kommunikasjon.

## 2. Systemoversikt

Systemet er delt opp i separate tjenester, hver med ett tydelig ansvar. Tjenestene kommuniserer hovedsakelig via meldingskø (NATS), og alle kontrollgrensesnitt går via et felles API-lag (API Gateway). Det planlegges både WPF-basert GUI og en framtidig webapp.

## 3. Tjenestemodell

| Tjeneste               | Ansvar                                                                |
| ---------------------- | --------------------------------------------------------------------- |
| **SimulatorService**   | Representerer selve fartøyet (kurs, fart, posisjon, heading, respons) |
| **EnvironmentService** | Genererer vind, strøm og andre miljødata eller henter data fra API    |
| **AutopilotService**   | Regulerer fartøyets kurs og fart basert på settpunkter og sensordata  |
| **AlarmService**       | Detekterer avvik og varsler om feil eller uregelmessigheter           |
| **LoggerService**      | Logger alle relevante data til CSV eller database                     |
| **API Gateway**        | Felles inngangspunkt for brukergrensesnitt og eksterne klienter       |
| **Frontend GUI**       | WPF (først) og webapp (senere) for kontroll og visualisering          |

Alle tjenestene skal kunne kjøres som uavhengige Docker-containere.

## 4. Kommunikasjon og API

- **Intern kommunikasjon:** via NATS-emner (JSON)

  - `sim.sensors.nav`, `sim.sensors.env`, `sim.commands.*`, `log.entries`, `alarm.triggers`

- **API Gateway:** REST (og evt. WebSocket) for ekstern kommunikasjon

  - Eksempel-endepunkt: `POST /api/autopilot/set-course`

## 5. Trinnvis utviklingsplan

| Trinn | Innhold                                                               |
| ----- | --------------------------------------------------------------------- |
| **1** | SimulatorService med CLI-grensesnitt og sensordata publisering        |
| **2** | AutopilotService med enkel PID-regulering og manuell testing          |
| **3** | EnvironmentService og simulering av realistiske vind- og strømforhold |
| **4** | WPF-basert GUI for kontroll og visning                                |
| **5** | API Gateway som samler alt eksternt grensesnitt                       |
| **6** | AlarmService og LoggerService med full meldingsintegrasjon            |
| **7** | Webapp-dashboard i Next.js                                            |

## 6. Teknologivalg

- **Språk:** C# (.NET 9), TypeScript (frontend)
- **Kommunikasjon:** NATS (asynkron meldingsbasert)
- **Frontend:** WPF (desktop), Next.js (web)
- **Containere:** Docker Compose
- **API Gateway:** Traefik (evt. YARP ved behov for .NET-integrasjon)

## 7. Mulige utvidelser

- Autonom ruteplanlegging og kollisjonsunngåelse
- Full integrasjon mot K-Sim via eksisterende API
- Støtte for historisk avspillingsmodus
- Mobiltilpasset visning

## 8. Notater og tanker

- Tjenester bør være så uavhengige som mulig for å kunne utvikles og testes isolert.
- Test Driven Development anbefales brukt fra starten.
- Fokus på å lære mikrotjenestearkitektur like mye som å "få det ferdig".
- Dokumentasjon og tegninger håndtegnes og/eller lages digitalt parallelt med utvikling.

---
