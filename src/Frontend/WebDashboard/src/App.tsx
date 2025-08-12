import { useEffect, useState } from "react";
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
  WebSocketMessage,
  DestinationStatus,
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
  // Hvis dashboardet åpnes direkte på port 3000 (bypasser Traefik), må vi sende API-kall til Traefik på port 80
  const apiBase =
    window.location.port === "3000" ? `http://${window.location.hostname}` : "";

  // start/end + cruise speed state
  const [startPoint, setStartPoint] = useState<[number, number] | null>(null);
  const [endPoint, setEndPoint] = useState<[number, number] | null>(null);
  const [cruiseSpeed, setCruiseSpeed] = useState<number>(12);
  // Journey feedback fjernet i forenklet UI

  useEffect(() => {
    const wsBase = `${window.location.protocol === "https:" ? "wss" : "ws"}://${
      window.location.host
    }`;
    const socket = new WebSocket(`${wsBase}/ws/nav`);

    socket.onmessage = (event) => {
      const message: WebSocketMessage<any> = JSON.parse(event.data);

      switch (message.topic) {
        case "sim.sensors.nav": {
          const d = message.data;
          // Adapter: støtt både gamle og nye feltnavn
          const nav: NavigationData = {
            timestampUtc: d.timestampUtc || d.timestamp,
            latitude: d.latitude,
            longitude: d.longitude,
            headingDegrees: d.headingDegrees ?? d.heading,
            speedKnots: d.speedKnots ?? d.speed,
            courseOverGroundDegrees:
              d.courseOverGroundDegrees ?? d.courseOverGround,
          };
          setNavigation(nav);
          break;
        }
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

  const canStartJourney = !!endPoint; // start optional (falls back to current or existing position)
  const startJourney = async () => {
    if (!endPoint) return;
    try {
      const payload: any = {
        endLatitude: endPoint[0],
        endLongitude: endPoint[1],
        cruiseSpeedKnots: cruiseSpeed,
      };
      if (startPoint) {
        payload.startLatitude = startPoint[0];
        payload.startLongitude = startPoint[1];
      }
      await fetch(`${apiBase}/api/simulator/journey`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
    } catch {}
  };

  const handleSetSpeed = async (speed: number) => {
    try {
      await fetch(`${apiBase}/api/simulator/speed`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          timestamp: new Date().toISOString(),
          targetSpeedKnots: speed,
        }),
      });
    } catch (error) {
      console.error("Failed to set speed:", error);
    }
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
            />
          </Grid>
          <Grid item xs={12} md={4} sx={{ height: "70vh", overflow: "auto" }}>
            <SimplifiedPanel
              navigation={navigation}
              destination={destination}
              onSetSpeed={handleSetSpeed}
              onStartJourney={startJourney}
              cruiseSpeed={cruiseSpeed}
              onCruiseSpeedChange={setCruiseSpeed}
              canStartJourney={canStartJourney}
            />
          </Grid>
        </Grid>
      </Container>
    </ThemeProvider>
  );
}

export default App;
