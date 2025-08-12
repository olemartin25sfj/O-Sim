import { useEffect, useState } from "react";
import {
  Container,
  Grid,
  CssBaseline,
  ThemeProvider,
  createTheme,
} from "@mui/material";
import { VesselMap } from "./components/VesselMap";
import { EnvironmentPanel } from "./components/EnvironmentPanel";
import { ControlPanel } from "./components/ControlPanel";
import { AlarmPanel } from "./components/AlarmPanel";
import { RouteControls } from "./components/RouteControls";
import {
  NavigationData,
  EnvironmentData,
  AlarmData,
  WebSocketMessage,
} from "./types/messages";

const darkTheme = createTheme({
  palette: {
    mode: "dark",
  },
});

function App() {
  const [navigation, setNavigation] = useState<NavigationData | null>(null);
  const [environment, setEnvironment] = useState<EnvironmentData | null>(null);
  const [alarms, setAlarms] = useState<AlarmData[]>([]);
  // Hvis dashboardet åpnes direkte på port 3000 (bypasser Traefik), må vi sende API-kall til Traefik på port 80
  const apiBase =
    window.location.port === "3000" ? `http://${window.location.hostname}` : "";

  // start/end + cruise speed state
  const [startPoint, setStartPoint] = useState<[number, number] | null>(null);
  const [endPoint, setEndPoint] = useState<[number, number] | null>(null);
  const [cruiseSpeed, setCruiseSpeed] = useState<number>(12);
  const [journeyStarting, setJourneyStarting] = useState(false);
  const [lastJourneyMsg, setLastJourneyMsg] = useState<string | null>(null);

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
        case "sim.sensors.env": {
          const d = message.data;
          const env: EnvironmentData = {
            timestampUtc: d.timestampUtc || d.timestamp,
            mode: d.mode || "Dynamic",
            windSpeedKnots: d.windSpeedKnots,
            windDirectionDegrees: d.windDirectionDegrees ?? d.windDirection,
            currentSpeedKnots: d.currentSpeedKnots ?? d.currentSpeed,
            currentDirectionDegrees:
              d.currentDirectionDegrees ?? d.currentDirection,
            waveHeightMeters: d.waveHeightMeters ?? d.waveHeight,
            waveDirectionDegrees: d.waveDirectionDegrees ?? d.waveDirection,
            wavePeriodSeconds: d.wavePeriodSeconds ?? d.wavePeriod,
          };
          setEnvironment(env);
          break;
        }
        case "alarm.triggers": {
          const d = message.data;
          const alarm: AlarmData = {
            timestampUtc: d.timestampUtc || d.timestamp,
            alarmType: d.alarmType || d.type,
            message: d.message,
            severity: d.severity,
          };
          setAlarms((prev) => [...prev, alarm]);
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
    setJourneyStarting(true);
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
      const res = await fetch(`${apiBase}/api/simulator/journey`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      if (res.ok) {
        setLastJourneyMsg("Journey started");
      } else {
        setLastJourneyMsg("Journey failed");
      }
    } catch (e) {
      setLastJourneyMsg("Journey error");
    } finally {
      setJourneyStarting(false);
      setTimeout(() => setLastJourneyMsg(null), 4000);
    }
  };

  const handleSetCourse = async (course: number) => {
    try {
      await fetch(`${apiBase}/api/simulator/course`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          timestamp: new Date().toISOString(),
          targetCourseDegrees: course,
        }),
      });
    } catch (error) {
      console.error("Failed to set course:", error);
    }
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

  const handleSetPosition = async (latitude: number, longitude: number) => {
    try {
      await fetch(`${apiBase}/api/simulator/position`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          timestamp: new Date().toISOString(),
          latitude,
          longitude,
        }),
      });
    } catch (error) {
      console.error("Failed to set position:", error);
    }
  };

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
            <Grid container spacing={2}>
              <Grid item xs={12}>
                <ControlPanel
                  onSetCourse={handleSetCourse}
                  onSetPosition={handleSetPosition}
                  currentCourse={navigation?.headingDegrees}
                />
              </Grid>
              <Grid item xs={12}>
                <RouteControls
                  onSetSpeed={handleSetSpeed}
                  currentSpeed={navigation?.speedKnots}
                  cruiseSpeed={cruiseSpeed}
                  onCruiseSpeedChange={setCruiseSpeed}
                  canStartJourney={canStartJourney}
                  onStartJourney={startJourney}
                  journeyStarting={journeyStarting}
                  lastJourneyMsg={lastJourneyMsg}
                />
              </Grid>
              <Grid item xs={12}>
                <EnvironmentPanel environment={environment} />
              </Grid>
              <Grid item xs={12}>
                <AlarmPanel alarms={alarms} />
              </Grid>
            </Grid>
          </Grid>
        </Grid>
      </Container>
    </ThemeProvider>
  );
}

export default App;
