import { useEffect, useState, useRef } from "react";
import {
  Container,
  Grid,
  CssBaseline,
  ThemeProvider,
  createTheme,
} from "@mui/material";
import { VesselMap } from "./components/VesselMap";
// Forenklet dashbord: vi fjerner miljø/alarmer/ruteredigering foreløpig
import { SimplifiedPanel } from "./components/SimplifiedPanel";
import {
  NavigationData,
  DestinationStatus,
  EnvironmentData,
} from "./types/messages";

const darkTheme = createTheme({
  palette: {
    mode: "dark",
  },
});

function App() {
  const [navigation, setNavigation] = useState<NavigationData | null>(null);
  const [destination, setDestination] = useState<DestinationStatus | null>(
    null
  );
  const [environment, setEnvironment] = useState<EnvironmentData | null>(null);
  // Hvis dashboardet åpnes direkte på port 3000 (bypasser Traefik), må vi sende API-kall til Traefik på port 80
  const apiBase =
    window.location.port === "3000" ? `http://${window.location.hostname}` : "";

  // start/end + cruise speed state
  const [startPoint, setStartPoint] = useState<[number, number] | null>(null);
  const [endPoint, setEndPoint] = useState<[number, number] | null>(null);
  const [running, setRunning] = useState(false);
  // Track of vessel positions while a journey is active
  const [journeyTrack, setJourneyTrack] = useState<[number, number][]>([]);
  const [arrivalPoint, setArrivalPoint] = useState<[number, number] | null>(
    null
  );
  const [activeRoutePoints, setActiveRoutePoints] = useState<
    [number, number][] | null
  >(null);
  // Når vi starter med et eksplisitt startpunkt, vil første nav-melding kunne være gammel posisjon.
  // Vi lagrer ønsket start for å filtrere bort ett "teleport" hopp.
  const plannedStartRef = useRef<[number, number] | null>(null);

  // Append to track when running
  useEffect(() => {
    if (!running) return;
    if (!navigation) return;
    setJourneyTrack((t) => {
      const last = t[t.length - 1];
      const next: [number, number] = [
        navigation.latitude,
        navigation.longitude,
      ];
      if (
        last &&
        Math.abs(last[0] - next[0]) < 1e-6 &&
        Math.abs(last[1] - next[1]) < 1e-6
      )
        return t; // ignore identical
      return [...t, next];
    });
  }, [navigation, running]);

  // Reset track when journey finishes
  useEffect(() => {
    if (destination?.hasArrived) {
      setRunning(false);
      if (!arrivalPoint && navigation) {
        setArrivalPoint([navigation.latitude, navigation.longitude]);
      }
    }
  }, [destination]);
  // Journey feedback fjernet i forenklet UI

  useEffect(() => {
    const wsBase = `${window.location.protocol === "https:" ? "wss" : "ws"}://${
      window.location.host
    }`;
    const socket = new WebSocket(`${wsBase}/ws/nav`);

    socket.onmessage = (event) => {
      try {
        const parsed = JSON.parse(event.data);
        // Format A: wrapped with topic + data (tidligere antatt struktur)
        if (parsed && parsed.topic === "sim.sensors.nav" && parsed.data) {
          const d = parsed.data;
          const nav: NavigationData = {
            timestampUtc: d.timestampUtc || d.timestamp || d.TimestampUtc,
            latitude: d.latitude ?? d.Latitude,
            longitude: d.longitude ?? d.Longitude,
            headingDegrees:
              d.headingDegrees ?? d.HeadingDegrees ?? d.heading ?? d.Heading,
            speedKnots: d.speedKnots ?? d.SpeedKnots ?? d.speed ?? d.Speed,
            courseOverGroundDegrees:
              d.courseOverGroundDegrees ??
              d.CourseOverGroundDegrees ??
              d.courseOverGround ??
              d.CourseOverGround ??
              d.headingDegrees ??
              d.HeadingDegrees,
          };
          // Filter: ignorer første gamle posisjon dersom vi nettopp har satt et manuelt startpunkt
          if (plannedStartRef.current && journeyTrack.length <= 1) {
            const ps = plannedStartRef.current;
            const off =
              Math.abs(nav.latitude - ps[0]) + Math.abs(nav.longitude - ps[1]);
            // Hvis meldingen ikke matcher ønsket start (gamle koordinater), hopp over
            if (off > 0.0001) return;
          }
          setNavigation(nav);
          if (plannedStartRef.current) plannedStartRef.current = null;
          return;
        }
        // Format B: Env (topic-wrapped) legacy
        if (parsed && parsed.topic === "sim.sensors.env" && parsed.data) {
          const e = parsed.data;
          const env: EnvironmentData = {
            timestampUtc: e.timestampUtc || e.TimestampUtc,
            mode: (e.mode || e.Mode || "Dynamic") as any,
            windSpeedKnots: e.windSpeedKnots ?? e.WindSpeedKnots ?? 0,
            windDirectionDegrees:
              e.windDirectionDegrees ?? e.WindDirectionDegrees ?? 0,
            currentSpeedKnots: e.currentSpeedKnots ?? e.CurrentSpeedKnots ?? 0,
            currentDirectionDegrees:
              e.currentDirectionDegrees ?? e.CurrentDirectionDegrees ?? 0,
            waveHeightMeters: e.waveHeightMeters ?? e.WaveHeightMeters ?? 0,
            waveDirectionDegrees:
              e.waveDirectionDegrees ?? e.WaveDirectionDegrees ?? 0,
            wavePeriodSeconds: e.wavePeriodSeconds ?? e.WavePeriodSeconds ?? 0,
          };
          setEnvironment(env);
          return;
        }

        // Format B2: raw PascalCase environment (no topic)
        if (
          typeof parsed?.WindSpeedKnots === "number" &&
          typeof parsed?.WindDirectionDegrees === "number" &&
          typeof parsed?.WaveHeightMeters === "number"
        ) {
          const e = parsed;
          const env: EnvironmentData = {
            timestampUtc: e.TimestampUtc || new Date().toISOString(),
            mode: (e.Mode || "Dynamic") as any,
            windSpeedKnots: e.WindSpeedKnots,
            windDirectionDegrees: e.WindDirectionDegrees,
            currentSpeedKnots: e.CurrentSpeedKnots ?? 0,
            currentDirectionDegrees: e.CurrentDirectionDegrees ?? 0,
            waveHeightMeters: e.WaveHeightMeters,
            waveDirectionDegrees: e.WaveDirectionDegrees ?? 0,
            wavePeriodSeconds: e.WavePeriodSeconds ?? 0,
          };
          setEnvironment(env);
          return;
        }

        // Format B: rå nav-record fra gateway (PascalCase)
        if (
          typeof parsed?.Latitude === "number" &&
          typeof parsed?.Longitude === "number"
        ) {
          const d = parsed;
          const nav: NavigationData = {
            timestampUtc: d.TimestampUtc || d.timestampUtc || d.timestamp,
            latitude: d.Latitude,
            longitude: d.Longitude,
            headingDegrees:
              d.HeadingDegrees ?? d.headingDegrees ?? d.heading ?? d.Heading,
            speedKnots: d.SpeedKnots ?? d.speedKnots ?? d.speed ?? d.Speed,
            courseOverGroundDegrees:
              d.CourseOverGroundDegrees ??
              d.courseOverGroundDegrees ??
              d.courseOverGround ??
              d.HeadingDegrees ??
              d.headingDegrees,
          };
          if (plannedStartRef.current && journeyTrack.length <= 1) {
            const ps = plannedStartRef.current;
            const off =
              Math.abs(nav.latitude - ps[0]) + Math.abs(nav.longitude - ps[1]);
            if (off > 0.0001) return; // skip stale
          }
          setNavigation(nav);
          if (plannedStartRef.current) plannedStartRef.current = null;
          return;
        }
        // Format C: Rå nav camelCase OG mulige env felt camelCase
        if (
          typeof parsed?.latitude === "number" &&
          typeof parsed?.longitude === "number"
        ) {
          const d = parsed;
          const nav: NavigationData = {
            timestampUtc: d.timestampUtc || d.timestamp,
            latitude: d.latitude,
            longitude: d.longitude,
            headingDegrees: d.headingDegrees ?? d.heading,
            speedKnots: d.speedKnots ?? d.speed,
            courseOverGroundDegrees:
              d.courseOverGroundDegrees ??
              d.courseOverGround ??
              d.headingDegrees,
          };
          if (plannedStartRef.current && journeyTrack.length <= 1) {
            const ps = plannedStartRef.current;
            const off =
              Math.abs(nav.latitude - ps[0]) + Math.abs(nav.longitude - ps[1]);
            if (off > 0.0001) return;
          }
          setNavigation(nav);
          if (typeof d.windSpeedKnots === "number") {
            const env: EnvironmentData = {
              timestampUtc: d.timestampUtc,
              mode: (d.mode || "Dynamic") as any,
              windSpeedKnots: d.windSpeedKnots,
              windDirectionDegrees: d.windDirectionDegrees || 0,
              currentSpeedKnots: d.currentSpeedKnots || 0,
              currentDirectionDegrees: d.currentDirectionDegrees || 0,
              waveHeightMeters: d.waveHeightMeters || 0,
              waveDirectionDegrees: d.waveDirectionDegrees || 0,
              wavePeriodSeconds: d.wavePeriodSeconds || 0,
            };
            setEnvironment(env);
          }
          if (plannedStartRef.current) plannedStartRef.current = null;
          return;
        }
      } catch {
        // ignorer parse-feil
      }
    };

    return () => {
      socket.close();
    };
  }, []);

  const handleStartPoint = (lat: number, lon: number) => {
    setStartPoint([lat, lon]);
  };
  const handleEndPoint = (lat: number, lon: number) => {
    setEndPoint([lat, lon]);
  };

  const canStartJourney =
    !!endPoint || !!(activeRoutePoints && activeRoutePoints.length > 0);
  const startJourney = async () => {
    if (!endPoint && !(activeRoutePoints && activeRoutePoints.length > 0))
      return;
    try {
      const payload: any = {
        // Hvis rute finnes, bruker vi kun routeWaypoints i stedet for endLatitude
        endLatitude: endPoint ? endPoint[0] : undefined,
        endLongitude: endPoint ? endPoint[1] : undefined,
        cruiseSpeedKnots: 20,
      };
      if (startPoint) {
        payload.startLatitude = startPoint[0];
        payload.startLongitude = startPoint[1];
      }
      if (activeRoutePoints && activeRoutePoints.length > 0) {
        // Smooth & sample rute for jevn styring
        const smoothed = smoothAndSample(activeRoutePoints);
        payload.routeWaypoints = smoothed.map((p) => ({
          latitude: p[0],
          longitude: p[1],
        }));
      }
      await fetch(`${apiBase}/api/simulator/journey`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      setRunning(true);
      if (startPoint) {
        // Seed track med startpunkt for å unngå linje fra gammel posisjon
        setJourneyTrack([[startPoint[0], startPoint[1]]]);
        plannedStartRef.current = startPoint;
        // Optimistisk flytt ikon umiddelbart
        setNavigation((prev) =>
          prev
            ? { ...prev, latitude: startPoint[0], longitude: startPoint[1] }
            : prev
        );
      } else {
        setJourneyTrack([]);
        plannedStartRef.current = null;
      }
      setArrivalPoint(null);
    } catch {}
  };

  const stopJourney = async () => {
    try {
      await fetch(`${apiBase}/api/simulator/stop`, { method: "POST" });
    } catch {}
    setRunning(false);
    setJourneyTrack([]);
    setArrivalPoint(null);
  };

  // Enkel polling av destinasjonsstatus (ETA/distanse)
  useEffect(() => {
    const id = setInterval(async () => {
      try {
        const r = await fetch(`${apiBase}/api/simulator/destination`);
        if (r.ok) {
          const json = await r.json();
          setDestination(json);
        }
      } catch {}
    }, 4000);
    return () => clearInterval(id);
  }, [apiBase]);

  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <Container maxWidth={false} sx={{ height: "100vh", py: 2 }}>
        <Grid container spacing={2} sx={{ height: "100%" }}>
          <Grid item xs={12} md={8} sx={{ height: "70vh" }}>
            <VesselMap
              navigation={navigation}
              onSelectStart={handleStartPoint}
              onSelectEnd={handleEndPoint}
              selectedStart={startPoint}
              selectedEnd={endPoint}
              onActiveRouteChange={setActiveRoutePoints}
              journeyStart={
                startPoint ||
                (navigation
                  ? [navigation.latitude, navigation.longitude]
                  : undefined)
              }
              journeyEnd={endPoint}
              journeyPlannedRoute={
                activeRoutePoints && activeRoutePoints.length > 1
                  ? activeRoutePoints
                  : undefined
              }
              journeyTrack={journeyTrack}
              isJourneyRunning={
                running &&
                !!destination?.hasDestination &&
                !destination?.hasArrived
              }
              hasArrived={!!destination?.hasArrived}
              arrivalPoint={arrivalPoint}
            />
          </Grid>
          <Grid item xs={12} md={4} sx={{ height: "70vh", overflow: "auto" }}>
            <SimplifiedPanel
              navigation={navigation}
              destination={destination}
              environment={environment}
              onStart={startJourney}
              onStop={stopJourney}
              running={
                running &&
                !!destination?.hasDestination &&
                !destination?.hasArrived
              }
              canStartJourney={canStartJourney}
            />
          </Grid>
        </Grid>
      </Container>
    </ThemeProvider>
  );
}

// Catmull-Rom smoothing + sampling (kopi av strategi i VesselMap, isolert)
function smoothAndSample(points: [number, number][], spacingMeters = 120) {
  if (points.length < 3) return points;
  const res: [number, number][] = [];
  const dup = (i: number) =>
    points[Math.min(points.length - 1, Math.max(0, i))];
  const toRad = (d: number) => (d * Math.PI) / 180;
  const distMeters = (a: [number, number], b: [number, number]) => {
    const R = 6371000;
    const dLat = toRad(b[0] - a[0]);
    const dLon = toRad(b[1] - a[1]);
    const lat1 = toRad(a[0]);
    const lat2 = toRad(b[0]);
    const h =
      Math.sin(dLat / 2) ** 2 +
      Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLon / 2) ** 2;
    return 2 * R * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
  };
  const catmull = (
    p0: [number, number],
    p1: [number, number],
    p2: [number, number],
    p3: [number, number],
    t: number
  ): [number, number] => {
    const t2 = t * t;
    const t3 = t2 * t;
    const x =
      0.5 *
      (2 * p1[1] +
        (-p0[1] + p2[1]) * t +
        (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 +
        (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3);
    const y =
      0.5 *
      (2 * p1[0] +
        (-p0[0] + p2[0]) * t +
        (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 +
        (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3);
    return [y, x];
  };
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = dup(i - 1);
    const p1 = dup(i);
    const p2 = dup(i + 1);
    const p3 = dup(i + 2);
    const segLen = distMeters(p1, p2);
    const steps = Math.max(2, Math.round(segLen / spacingMeters));
    for (let s = 0; s < steps; s++) {
      const t = s / steps;
      const c = catmull(p0, p1, p2, p3, t);
      if (res.length === 0 || distMeters(res[res.length - 1], c) > 15) {
        res.push(c);
      }
    }
  }
  const last = points[points.length - 1];
  if (
    !res.length ||
    res[res.length - 1][0] !== last[0] ||
    res[res.length - 1][1] !== last[1]
  )
    res.push(last);
  return res;
}
export default App;
