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
import {
  NavigationData,
  EnvironmentData,
  AlarmData,
  WebSocketMessage,
  SetCourseCommand,
  SetSpeedCommand,
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

  useEffect(() => {
    const socket = new WebSocket("ws://localhost/ws/nav");

    socket.onmessage = (event) => {
      const message: WebSocketMessage<any> = JSON.parse(event.data);

      switch (message.topic) {
        case "sim.sensors.nav":
          setNavigation(message.data);
          break;
        case "sim.sensors.env":
          setEnvironment(message.data);
          break;
        case "alarm.triggers":
          setAlarms((prev) => [...prev, message.data]);
          break;
      }
    };

    return () => {
      socket.close();
    };
  }, []);

  const handleSetCourse = async (course: number) => {
    try {
      const response = await fetch("/api/simulator/course", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ course }),
      });
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error("Failed to set course:", error);
    }
  };

  const handleSetSpeed = async (speed: number) => {
    try {
      const response = await fetch("/api/simulator/speed", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ speed }),
      });
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error("Failed to set speed:", error);
    }
  };

  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
        <Grid container spacing={3}>
          <Grid item xs={12} md={8}>
            <VesselMap navigation={navigation} />
          </Grid>
          <Grid item xs={12} md={4}>
            <Grid container spacing={3}>
              <Grid item xs={12}>
                <ControlPanel onSetCourse={handleSetCourse} onSetSpeed={handleSetSpeed} />
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

  const handleSetCourse = async (course: number) => {
    try {
      const response = await fetch("/api/simulator/course", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ course }),
      });
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error("Failed to set course:", error);
    }
  };

  const handleSetSpeed = async (speed: number) => {
    try {
      const response = await fetch("/api/simulator/speed", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ speed }),
      });
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
    } catch (error) {
      console.error("Failed to set speed:", error);
    }
  };
        },
        body: JSON.stringify({ course }),
      });

      if (!response.ok) {
        throw new Error("Failed to set course");
      }
    } catch (error) {
      console.error("Error setting course:", error);
    }
  };

  const handleSetSpeed = async (speed: number) => {
    try {
      const response = await fetch("/api/simulator/speed", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ speed }),
      });

      if (!response.ok) {
        throw new Error("Failed to set speed");
      }
    } catch (error) {
      console.error("Error setting speed:", error);
    }
  };

  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <Container maxWidth={false} sx={{ height: "100vh", py: 2 }}>
        <Grid container spacing={2} sx={{ height: "100%" }}>
          <Grid item xs={12} md={8} sx={{ height: "70vh" }}>
            <VesselMap navigation={navigation} />
          </Grid>
          <Grid item xs={12} md={4} sx={{ height: "70vh", overflow: "auto" }}>
            <Grid container spacing={2}>
              <Grid item xs={12}>
                <EnvironmentPanel environment={environment} />
              </Grid>
              <Grid item xs={12}>
                <ControlPanel
                  onSetCourse={handleSetCourse}
                  onSetSpeed={handleSetSpeed}
                />
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
