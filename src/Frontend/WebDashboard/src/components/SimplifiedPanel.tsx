import React from "react";
import { Paper, Box, Typography, Slider, Button, Stack } from "@mui/material";
import { NavigationData, DestinationStatus } from "../types/messages";

interface SimplifiedPanelProps {
  navigation: NavigationData | null;
  destination: DestinationStatus | null;
  onSetSpeed: (speed: number) => void;
  onStartJourney: () => void;
  cruiseSpeed: number;
  onCruiseSpeedChange: (v: number) => void;
  canStartJourney: boolean;
}

export const SimplifiedPanel: React.FC<SimplifiedPanelProps> = ({
  navigation,
  destination,
  onSetSpeed,
  onStartJourney,
  cruiseSpeed,
  onCruiseSpeedChange,
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

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Sett fart
        </Typography>
        <Stack direction="row" spacing={2} alignItems="center">
          <Slider
            value={speed}
            min={0}
            max={25}
            step={0.5}
            onChange={(_, v) => onSetSpeed(v as number)}
            valueLabelDisplay="auto"
            sx={{ flex: 1 }}
          />
          <Button
            variant="contained"
            size="small"
            onClick={() => onSetSpeed(speed)}
          >
            Oppdatér
          </Button>
        </Stack>
      </Box>

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Journey cruise-fart
        </Typography>
        <Stack direction="row" spacing={2} alignItems="center">
          <Slider
            value={cruiseSpeed}
            min={0}
            max={25}
            step={0.5}
            onChange={(_, v) => onCruiseSpeedChange(v as number)}
            valueLabelDisplay="auto"
            sx={{ flex: 1 }}
          />
          <Button
            variant="contained"
            size="small"
            disabled={!canStartJourney}
            onClick={onStartJourney}
          >
            Start
          </Button>
        </Stack>
      </Box>
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
