import * as React from "react";
import { Paper, Typography, Grid } from "@mui/material";
import { EnvironmentData } from "../types/messages";

interface EnvironmentPanelProps {
  environment: EnvironmentData | null;
}

export const EnvironmentPanel: React.FC<EnvironmentPanelProps> = ({
  environment,
}) => {
  if (!environment) return null;

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>
        Environmental Conditions
      </Typography>
      <Grid container spacing={2}>
        <Grid item xs={6}>
          <Typography variant="subtitle2">Wind</Typography>
          <Typography>
            {environment.windSpeedKnots.toFixed(1)} kts @{" "}
            {environment.windDirection.toFixed(0)}°
          </Typography>
        </Grid>
        <Grid item xs={6}>
          <Typography variant="subtitle2">Current</Typography>
          <Typography>
            {environment.currentSpeed.toFixed(1)} kts @{" "}
            {environment.currentDirection.toFixed(0)}°
          </Typography>
        </Grid>
        <Grid item xs={12}>
          <Typography variant="subtitle2">Waves</Typography>
          <Typography>
            Height: {environment.waveHeight.toFixed(1)}m | Direction:{" "}
            {environment.waveDirection.toFixed(0)}° | Period:{" "}
            {environment.wavePeriod.toFixed(1)}s
          </Typography>
        </Grid>
      </Grid>
    </Paper>
  );
};
