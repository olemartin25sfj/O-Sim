import React from "react";
import { Paper, Box, Typography, Button, Stack } from "@mui/material";
import {
  NavigationData,
  DestinationStatus,
  EnvironmentData,
} from "../types/messages";

interface SimplifiedPanelProps {
  navigation: NavigationData | null;
  destination: DestinationStatus | null;
  environment?: EnvironmentData | null;
  onStart: () => void;
  onStop: () => void;
  running: boolean;
  canStartJourney: boolean;
}

export const SimplifiedPanel: React.FC<SimplifiedPanelProps> = ({
  navigation,
  destination,
  environment,
  onStart,
  onStop,
  running,
  canStartJourney,
}) => {
  const speed = navigation?.speedKnots ?? 0;
  const heading = navigation?.headingDegrees ?? 0;
  const lat = navigation?.latitude?.toFixed(5);
  const lon = navigation?.longitude?.toFixed(5);

  return (
    <Paper sx={{ p: 2, display: "flex", flexDirection: "column", gap: 2 }}>
      <Typography variant="h6">Fartøy</Typography>
      {/* Rad 1: kjerne telemetri */}
      <Box sx={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
        <Info label="Fart" value={`${speed.toFixed(1)} kn`} />
        <Info label="Kurs" value={`${heading.toFixed(1)}°`} />
        <Info label="Posisjon" value={`${lat}, ${lon}`} />
        {destination?.hasArrived && <Info label="Status" value="Ankommet" />}
        {destination?.hasDestination && !destination?.hasArrived && (
          <Info
            label="Distanse"
            value={destination.distanceNm?.toFixed(2) + " nm"}
          />
        )}
        {destination?.etaMinutes && destination?.etaMinutes > 0 && (
          <Info label="ETA" value={formatEta(destination.etaMinutes)} />
        )}
      </Box>
      {/* Rad 2: miljø */}
      {environment && (
        <Box
          sx={{
            display: "flex",
            gap: 4,
            flexWrap: "wrap",
            alignItems: "center",
            position: "relative",
            width: "100%",
          }}
        >
          <Info
            label="Vind"
            value={`${environment.windSpeedKnots.toFixed(
              1
            )} kn @ ${environment.windDirectionDegrees.toFixed(0)}°`}
          />
          <Info
            label="Bølger"
            value={`${environment.waveHeightMeters.toFixed(
              1
            )} m / ${environment.wavePeriodSeconds.toFixed(0)}s`}
          />
          {/* Windsock helt til høyre */}
          <Box
            sx={{
              marginLeft: "auto",
              width: 90,
              height: 48,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              position: "relative",
            }}
          >
            <WindSock
              direction={environment.windDirectionDegrees}
              speed={environment.windSpeedKnots}
            />
          </Box>
        </Box>
      )}
      <Stack direction="row" spacing={2}>
        <Button
          variant="contained"
          color="success"
          disabled={!canStartJourney || running}
          onClick={onStart}
        >
          Start
        </Button>
        <Button
          variant="contained"
          color="error"
          disabled={!running && speed < 0.1}
          onClick={onStop}
        >
          Stopp
        </Button>
      </Stack>
      <Typography variant="caption" sx={{ opacity: 0.6 }}>
        {running
          ? "Reise aktiv – fart styres automatisk mot 20 kn."
          : "Velg start (S) og destinasjon (D) i kartet for å aktivere Start."}
      </Typography>
    </Paper>
  );
};

const Info = ({ label, value }: { label: string; value?: string }) => (
  <Box>
    <Typography variant="caption" sx={{ opacity: 0.6 }}>
      {label}
    </Typography>
    <Typography variant="body2" sx={{ fontWeight: 500 }}>
      {value ?? "-"}
    </Typography>
  </Box>
);

function formatEta(mins: number) {
  if (mins < 60) return mins.toFixed(0) + " min";
  const h = Math.floor(mins / 60);
  const m = Math.round(mins % 60);
  return `${h}t ${m}m`;
}

// Vindpølse: forankret i venstre kant (fast punkt) og roterer uten å overlappe tekst
const WindSock = ({
  direction,
  speed,
}: {
  direction: number;
  speed: number;
}) => {
  const intensity = Math.min(1, speed / 25);
  const length = 36 + 28 * intensity; // 36..64
  const color = speed > 20 ? "#ff5252" : speed > 12 ? "#ff9800" : "#4caf50";
  return (
    <Box
      sx={{
        position: "relative",
        width: "100%",
        height: 40,
      }}
      title={`Vind ${speed.toFixed(1)} kn @ ${direction.toFixed(0)}°`}
    >
      {/* Ankerpunkt / liten mast */}
      <Box
        sx={{
          position: "absolute",
          left: 4,
          top: "50%",
          width: 6,
          height: 6,
          marginTop: -3,
          borderRadius: "50%",
          background: "#666",
          boxShadow: "0 0 2px rgba(0,0,0,0.5)",
        }}
      />
      {/* Selve vimpelen */}
      <Box
        sx={{
          position: "absolute",
          left: 7,
          top: "50%",
          width: length,
          height: 12,
          marginTop: -6,
          background: color,
          borderRadius: "6px 10px 10px 6px",
          transform: `rotate(${direction}deg)`,
          transformOrigin: "0% 50%",
          transition: "transform 0.6s ease, background 0.4s, width 0.4s",
          boxShadow: "0 0 4px rgba(0,0,0,0.4)",
        }}
      />
    </Box>
  );
};
