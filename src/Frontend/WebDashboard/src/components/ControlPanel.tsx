import React, { useState } from "react";
import { Paper, Typography, TextField, Button, Grid } from "@mui/material";
import { SetPositionDialog } from "./SetPositionDialog";

interface ControlPanelProps {
  onSetCourse: (course: number) => void;
  onSetSpeed: (speed: number) => void;
  onSetPosition: (latitude: number, longitude: number) => void;
  currentCourse?: number;
  currentSpeed?: number;
}

export const ControlPanel: React.FC<ControlPanelProps> = ({
  onSetCourse,
  onSetSpeed,
  onSetPosition,
  currentCourse,
  currentSpeed,
}) => {
  const [course, setCourse] = useState(currentCourse?.toString() || "");
  const [speed, setSpeed] = useState(currentSpeed?.toString() || "");
  const [positionDialogOpen, setPositionDialogOpen] = useState(false);

  const handleCourseSubmit = () => {
    const courseNum = parseFloat(course);
    if (!isNaN(courseNum) && courseNum >= 0 && courseNum <= 360) {
      onSetCourse(courseNum);
    }
  };

  const handleSpeedSubmit = () => {
    const speedNum = parseFloat(speed);
    if (!isNaN(speedNum) && speedNum >= 0) {
      onSetSpeed(speedNum);
    }
  };

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>
        Control Panel
      </Typography>
      <Grid container spacing={2}>
        <Grid item xs={12}>
          <TextField
            fullWidth
            label="Course (0-360°)"
            value={course}
            onChange={(e) => setCourse(e.target.value)}
            type="number"
            InputProps={{
              inputProps: { min: 0, max: 360 },
            }}
          />
          {currentCourse !== undefined && (
            <Typography variant="caption">
              Current Course: {currentCourse.toFixed(1)}°
            </Typography>
          )}
        </Grid>
        <Grid item xs={12}>
          <Button
            fullWidth
            variant="contained"
            color="primary"
            onClick={handleCourseSubmit}
          >
            Set Course
          </Button>
        </Grid>
        <Grid item xs={12}>
          <TextField
            fullWidth
            label="Speed (knots)"
            value={speed}
            onChange={(e) => setSpeed(e.target.value)}
            type="number"
            InputProps={{
              inputProps: { min: 0 },
            }}
          />
          {currentSpeed !== undefined && (
            <Typography variant="caption">
              Current Speed: {currentSpeed.toFixed(1)} knots
            </Typography>
          )}
        </Grid>
        <Grid item xs={12}>
          <Button
            fullWidth
            variant="contained"
            color="primary"
            onClick={handleSpeedSubmit}
          >
            Set Speed
          </Button>
        </Grid>
        <Grid item xs={12}>
          <Button
            fullWidth
            variant="contained"
            color="secondary"
            onClick={() => setPositionDialogOpen(true)}
          >
            Set Position
          </Button>
        </Grid>
      </Grid>
      <SetPositionDialog
        open={positionDialogOpen}
        onClose={() => setPositionDialogOpen(false)}
        onSetPosition={onSetPosition}
      />
    </Paper>
  );
};
