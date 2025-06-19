# Systemarkitektur

## 1. Innledning

Dette dokumentet beskriver systemarkitekturen for O-Sim, et personlig utviklingsprosjekt designet for å simulere et autonomt fartøy i et virtuelt miljø. Hovedformålet med O-Sim er å fungere som en praktisk plattform for egen læring og utforskning av moderne programvarearkitektur.

Gjennom dette prosjektet ønsker jeg å tilegne meg erfaring med:

- Modulær systemdesign: Hvordan dele et komplekst problem inn i mindre, håndterbare deler.
- Mikrotjenester: Forstå prinsipper for uavhengige tjenester, deres fordeler, utfordringer og eventuelle ulemper.
- Meldingsbasert kommunikasjon: Erfaring med asynkron kommunikasjon og publish/subscribe-mønstre via NATS.
- Praktisk utvikling: Fra konsept til implementering, inkludert testing og distribusjon med Docker.

O-Sim vil gi mulighet til å kontrollere fartøyet via autopilot, visualisere reisen på kart, og hente inn simulerte sensordata. De kommende seksjonene vil gi en oversikt over systemet oppbygning, beskrive de individuelle komponentene, og forklare hvordan de samhandler for å oppnå funksjonaliteten. Målet er å skape et solid fundament for videre læring og eksperimentering.

## 2. Overordnet Arkitekturprinsipp: Mikrotjenester

O-Sim er bygget rundt prinsippene for en mikrotjenestearkitektur. Dette valget er fundamentalt for prosjektets design og reflekterer et viktig læringsmål: å forstå hvordan komplekse systemer kan brytes ned og organiseres i mindre, uavhengige og håndterbare enheter.

### 2.1 Hva er Mikrotjenester?

En mikrotjenestearkitektur er en tilnærming til programvareutvikling der en enkelt applikasjon er bygget som en samling av små, løst koblede og uavhengige distribuerbare tjenester. Hver tjeneste fokuserer på å løse et spesifikt problem eller utføre én bestemt funksjon. I motsetning til den tradisjonelle monolittiske applikasjonen, hvor all funkasjonalitet er pakket inn i en enkelt, stor og tett koblet kodebase.

I en mikrotjenestearkitektur vil hver tjeneste:

- Ha et spesifikt, veldefinitert ansvar: Den forkuserer på å gjøre én ting, og gjøre den bra. (Single responsibility prinsippet)
- Kunne utvikles uavhengig: Ulike deler av systemet kan bearbeides uten direkte innvirkning på andre tjenester.
- Kunne distribueres uavhengig: En endring i én tjeneste krever ikke at hele applikasjonen må bygges og distribueres på nytt.
- Kommunisere via lette mekanismer: Ofte ved hjelp av veldefinerte API-er (som REST eller gRPC) eller asynkrone meldingskøer (NATS).
- Potensielt bruke ulike teknologier: Selv om O-Sim hovedsakelig bruker .NET, åpner mikrotjenester for å velge det best egnede språket eller rammeverket for en spesifikk tjeneste i fremtidige utgivelser/utvidelser.

### 2.2 Hvorfor Mikrotjenester for O-Sim?

Valget av mikrotjenestearkitektur for O-Sim er drevet av flere faktorer, som fokuset på læring og avveing mot alternative arkitekturer som modulær monolitt.

Mens en modulær monolitt (hvor koden er godt strukturert i separate moduler, men fortsatt kjører som én enkelt applikasjon) kunne vært et enklere utgangspunkt og et godt valg for et lite prosjekt, så falt valget på mikrotjenster av følgende grunner:

- Fokus på læring og utforsking: Hovedmålet med O-Sim er å få praktisk erfaring med å designe, utvikle og drifte et mikrotjenestesystem. Dette inkluderer å lære om inter-tjenestekommunikasjon, tjenestegrenser og uavhengig distribusjon, som er sentrale konsepter mikrotjenester utfordrer deg på. En modulær monolitt ville ikke gitt den samme dybden og innsikten på disse områdene.

- Klarere tjenestegrenser og kontrakter: Mikrotjenester tvinger frem en klar definisjon av API-er og meldingskontrakter mellom tjenester. I en monolitt kan det være lettere å "jukse" med direkte funksjonskall eller delte databaser, noe som kan føre til tette koblinger over tid. For O-Sim sikrer denne disiplinen at hver tjeneste er genuint uavhengig.

- Enklere testbarhet for isolerte komponenter: Med mikrotjenester kan hver tjeneste kjøres og testes fullstendig isolert fra de andre. Dette er en stor fordel for en Test Driven Development (TDD)-tilnærming, da det minimerer behovet for komplekse oppsett for å teste enkelte funksjonsområder.

- Uavhengig distribusjon og skalerbarhet: Selv om det ikke er et behov akkurat nå, gir mikrotjenester muligheten til uavhengig distribusjon av individuelle tjenester. Dette betyr at en endring i f.eks. AutopilotService ikke krever nedetid eller redeploy av SimulatorService. Det åpner også for selektiv skalering av tjenester som krever mer ressurser.

Til tross for økt kompleksitet i infrastruktur og drift som mikrotjenestesystemer ofte medfører(kanskje spesielt for et soloprosjekt), veier de pedagogiske fordelene og den langvarige fleksibiliteten opp.
