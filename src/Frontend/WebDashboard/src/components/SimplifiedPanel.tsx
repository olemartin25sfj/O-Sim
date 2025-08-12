import React from "react";
import { Paper, Box, Typography, Button, Stack } from "@mui/material";
import { NavigationData, DestinationStatus } from "../types/messages";

interface SimplifiedPanelProps {
  navigation: NavigationData | null;
  destination: DestinationStatus | null;
  onStart: () => void; // start journey with fixed speed
  onStop: () => void; // manual stop
  running: boolean;
  canStartJourney: boolean; // has destination selected
}

export const SimplifiedPanel: React.FC<SimplifiedPanelProps> = ({
  navigation,
  destination,
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
      <Box sx={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
        <Info label="Fart" value={`${speed.toFixed(1)} kn`} />
        <Info label="Kurs" value={`${heading.toFixed(1)}°`} />
        <Info label="Posisjon" value={`${lat}, ${lon}`} />
        {destination?.hasDestination && !destination?.hasArrived && (
          <Info
            label="Distanse"
            value={destination.distanceNm?.toFixed(2) + " nm"}
          />
        )}
        {destination?.etaMinutes && destination?.etaMinutes > 0 && (
          <Info label="ETA" value={formatEta(destination.etaMinutes)} />
        )}
        {destination?.hasArrived && <Info label="Status" value="Ankommet" />}
      </Box>

      <Stack direction="row" spacing={2}>
        <Button
          variant="contained"
          color="success"
          disabled={!canStartJourney || running}
          onClick={onStart}
        >
          Start (20 kn)
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
