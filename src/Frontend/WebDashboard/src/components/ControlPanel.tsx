import React, { useState } from "react";
import { Paper, Typography, TextField, Button, Grid } from "@mui/material";

interface ControlPanelProps {
  onSetCourse: (course: number) => void;
  onSetSpeed: (speed: number) => void;
}

export const ControlPanel: React.FC<ControlPanelProps> = ({
  onSetCourse,
  onSetSpeed,
}) => {
  const [course, setCourse] = useState("");
  const [speed, setSpeed] = useState("");

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
        Vessel Control
      </Typography>
      <Grid container spacing={2}>
        <Grid item xs={6}>
          <TextField
            label="Course (0-360°)"
            type="number"
            value={course}
            onChange={(e) => setCourse(e.target.value)}
            fullWidth
            inputProps={{ min: 0, max: 360 }}
          />
          <Button
            variant="contained"
            onClick={handleCourseSubmit}
            sx={{ mt: 1 }}
            fullWidth
          >
            Set Course
          </Button>
        </Grid>
        <Grid item xs={6}>
          <TextField
            label="Speed (knots)"
            type="number"
            value={speed}
            onChange={(e) => setSpeed(e.target.value)}
            fullWidth
            inputProps={{ min: 0 }}
          />
          <Button
            variant="contained"
            onClick={handleSpeedSubmit}
            sx={{ mt: 1 }}
            fullWidth
          >
            Set Speed
          </Button>
        </Grid>
      </Grid>
    </Paper>
  );
};
