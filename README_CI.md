# README — CI/CD for O-Sim

Denne README-en forklarer hvordan CI/CD er satt opp for O-Sim, hvordan du kan trigge og feilsøke bygg lokalt og i GitHub Actions, og hvordan images publiseres til GitHub Container Registry (GHCR).

1. Hva gjør workflowen

---

- Fil: `.github/workflows/docker-build-push.yml`

  - Bygger Docker-images for alle tjenester (matrix per service).
  - Bruker Buildx for multi-arch (linux/amd64, linux/arm64).
  - Logger inn i GHCR (`ghcr.io`) ved hjelp av `GITHUB_TOKEN` fra Actions.
  - Tagger bilder med commit SHA og `latest`. Hvis workflow trigges av en git-tag (refs/tags/\*) tagges bildet også med tag-navnet (f.eks. `v1.2.3`).
  - Kjører en Trivy-scan av det pushede bildet. Jobben feiler hvis Trivy finner HIGH eller CRITICAL sårbarheter.

- Dockerfiles for .NET-tjenestene er gjort cache-vennlige (kopier .csproj -> restore -> copy resten -> publish).

2. Hvordan trigge CI

---

- Push til `master` eller lag en pull request mot `master`:

```powershell
git add .
git commit -m "Trigger CI"
git push origin master
```

- For release-tagging (slipp et image med versjon): opprett og push en git tag som `v1.0.0`:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Workflowen vil da også legge til taggen som image-tag.

3. Sjekk workflow-kjøring

---

- Gå til: GitHub → repository → Actions. Klikk på det siste run for `Build and Push Docker images`.
- Hvert steg (checkout, setup-buildx, login, build, scan) har logg — åpne for detaljer.

4. GHCR (GitHub Container Registry)

---

- Vi pusher images til `ghcr.io/<owner>/<image>`.
- Actions bruker `GITHUB_TOKEN` for autentisering; dette fungerer i Actions uten ekstra secrets (workflow trenger `permissions: packages: write`, som er satt).
- Lokalt må du bruke en Personal Access Token (PAT) med `write:packages` hvis du skal pushe eller pull private images.
  - Lag PAT: GitHub → Settings → Developer settings → Personal access tokens.
  - Lokalt login (PowerShell):

```powershell
docker login ghcr.io -u <github-username>
# lim inn PAT når du blir bedt om passord
```

- For å se pakkene: repo → Packages (eller https://github.com/<owner>/<repo>/packages)
- For å gjøre et package public: gå til pakken i GitHub UI og sett visibility til Public.

5. Kjøre og teste lokalt

---

- Bygg ett image med Docker (fra repo-roten):

```powershell
docker build -f src/SimulatorService/Dockerfile src -t o-sim-simulatorservice:local
```

- Eller bygg alle med docker compose:

```powershell
docker compose build
```

- Start hele miljøet lokalt:

```powershell
docker compose up
```

- Kjør en enkelt service:

```powershell
docker run --rm -p 5001:80 o-sim-simulatorservice:local
```

(Justér portmapping til tjenestens konfigurasjon.)

6. Hva å gjøre hvis CI feiler

---

- Åpne Actions-run og se loggen for steget som feilet (build -> dotnet restore/publish eller Trivy scan).
- Vanlige problemer og fikser:
  - NuGet restore-feil (mangler pakker eller private feeds): sørg for internettilgang i runner eller legg til feed-credentials.
  - Feil versjon på PackageReference: synk versjoner i .csproj (som NATS.Client i repoet tidligere).
  - Lokale `bin/obj` eller node_modules inkludert i build context: legg dem i `src/.dockerignore` (allerede lagt til).
  - Trivy feiler pga HIGH/CRITICAL: fix eller oppgrader base image/pakke; vurder å endre policy (se punkt 8).

7. Security & policy

---

- Trivy-scan er aktiv og feiler build ved HIGH eller CRITICAL funn. Dette er bevisst for å unngå publisering av sårbare images.
- Hvis dere ønsker at scanning kun gir varsler (ikke feiler), eller kun kjøre scan på tags/releases, si ifra så justerer jeg workflowen.

8. Forslag til videre forbedringer

---

- Automatisert tag/release pipeline: egne jobber som kun kjører når du pusher git tags, bruker semver, og eventuelt publiserer GitHub Releases.
- Integrasjonstester i CI: start `docker compose` i Actions og kjør et test-skript (smoke-tests / e2e tests).
- Image-scan policy: endre følsomhet, eller kjøre scan i en separat job som kun varsler.
- Push til produksjon: deploy via SSH (docker compose på VPS), eller bruk et container orchestrator (AKS, App Service, etc.).

9. Hjelp jeg kan gjøre nå

---

- Lage en release-jobb som kun kjører på git tags og pusher versjons-taggen (og eventuelt `stable`).
- Legge til en integrasjonstest-jobb som starter `docker compose` i Actions og kjører noen enkle dotnet-tests eller HTTP-sjekker.
- Endre Trivy-policy (varsel vs fail) eller flytte scanning til release-jobb.

Hvis du vil at jeg implementerer en av disse nå, gi beskjed hvilken (for eksempel: "lag release-jobb på git tags" eller "legg til integrasjonstester i CI").

---

Kort oppsummering: workflowen bygger og pusher til GHCR, Trivy beskytter mot alvorlige CVEs, og Dockerfiles er optimalisert for cache i CI. Fortell meg hvilket neste steg du vil at jeg gjør, så setter jeg det opp og validerer i repoet.
