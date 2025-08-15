# Idéer til fremtidige utvidelser og forbedringer

Dette dokumentet samler forslag til videreutvikling av O‑Sim utover MVP. Hensikten er å ha et lett tilgjengelig sted for idéer, med kort begrunnelse, teknisk retning og grov prioritet.

## ECDIS‑lite for O‑Sim (P2)

Et pragmatisk kart- og rute‑lag inspirert av ECDIS, men tilpasset simulatoren. Ikke typegodkjenning eller lukket data (S‑63), men fokus på treningsverdi og sikker navigasjon i sim.

### Mål

- Bedre situasjonsforståelse langs planlagt rute.
- Tidlige varsler om avvik (XTE) og potensielle farer.
- Praktiske verktøy for ruteplanlegging, sving og verifisering.

### Foreslåtte funksjoner

- XTD‑korridor og XTE‑varsling
  - Tegn en buffer rundt ruten (f.eks. ±50–100 m) og varsle når fartøyet forlater korridoren.
  - Vis XTE som verdi og trend, med diskret alarmterskel.
- Svingparametre og wheel‑over‑punkter
  - Angi ønsket svingradius pr. legg og marker wheel‑over‑punkt.
  - Gi estimert ny kurs, COG og ETA for hvert legg.
- Rutesjekk (look‑ahead)
  - Enkel sjekk mot farer/forbudssoner langs korridoren (seamarks, områder).
  - Dybde‑look‑ahead basert på tilgjengelig bathymetri (visuelt i første omgang).
- Kartlag (overlay)
  - OpenStreetMap base + OpenSeaMap seamarks (allerede i bruk).
  - EMODnet Bathymetry WMTS (Europa) for dybdeskygge.
  - Kartverket WMS/WMTS (dersom tilgjengelig for prosjektet) og/eller NOAA ENC (for testing).
- Rute‑UI
  - Klikk‑på‑segment for å sette inn veipunkt, dra for å justere, høyreklikk for slett.
  - «Snapping» til eksisterende punkter/leder, enkel ruteversjonering.

### Tekniske byggeklosser

- Leaflet + React (eksisterende stack).
- Turf.js for buffer/linjeavstand (evt. enkel egenimplementasjon for små avstander).
- rbush/kdbush for rask spatial‑søk mot farer/områder.
- WMS/WMTS‑lag for sjøkart/bathymetri.

### Datakontrakter og integrasjon

- Rute som liste av lat/lon‑punkter (eksisterer).
- Telemetri for XTE‑verdi og ruteavvik; alarm‑topic for XTE / lav dybde / waypoint approach.

### Avhengigheter/risiko

- Lisens og bruksrett for kartlag (WMS/WMTS) må avklares per kilde.
- Nøyaktighet i dybde avhenger av kildedata; start med visuell støtte før automatisk «stopp».

### Estimat (grov)

- 1–2 uker for første «ECDIS‑visning»: XTD‑korridor, XTE‑beregning, EMODnet‑lag, segment‑innsetting, enkle alarmer.

### Neste steg (foreslåtte tasks)

- Legg til EMODnet Bathymetry som valgbart kartlag i WebDashboard.
- Tegn XTD‑korridor rundt glatt rute og beregn XTE live mot fartøyposisjon.
- Implementer klikk‑på‑segment for punktinnsetting og «snap» i ruteeditoren.
- Legg inn enkel alarm for XTE > terskel med visuell indikator.

## Andre idéer (kortliste)

- Replay/«ghost vessel»: Spill av tidligere seilas over kartet for sammenligning.
- Pilot book‑notater: Markører med lokale prosedyrer, sektorlys, moloer, etc.
- AIS‑simulator: Syntetiske trafikkmål som kan lastes fra scenario.
- Vær/strøm‑integrasjon: Enkle modeller eller eksterne feeds for realistisk sett/drift.
- Import/eksport av ruter (GPX, ev. RTZ S‑421 for fremtiden).
- Autopilot‑tuning UI (PID/parametre) med logging og forslag.
- Scenario‑runner og testrigg: Automatisk kjøring av ruter med mål på ETA, avvik, drivstoff.

## Referanser (åpne kilder)

- Leaflet docs: https://leafletjs.com/reference.html
- OpenSeaMap seamarks: https://www.openseamap.org/
- EMODnet Bathymetry: https://emodnet.ec.europa.eu/
- NOAA ENC (for testing): https://charts.noaa.gov/
- Turf.js: https://turfjs.org/

---

Forslag tas imot i Issues/PRs, eller utvid dette dokumentet med flere idéer og prioritet.
