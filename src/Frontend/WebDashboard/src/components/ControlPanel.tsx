import React, { useState } from "react";
import { Paper, Typography, TextField, Button, Grid } from "@mui/material";
import { SetPositionDialog } from "./SetPositionDialog";

interface ControlPanelProps {
  onSetCourse: (course: number) => void;
  onSetPosition: (latitude: number, longitude: number) => void;
  currentCourse?: number;
}

export const ControlPanel: React.FC<ControlPanelProps> = ({
  onSetCourse,
  onSetPosition,
  currentCourse,
}) => {
  const [course, setCourse] = useState(currentCourse?.toString() || "");
  const [positionDialogOpen, setPositionDialogOpen] = useState(false);

  const handleCourseSubmit = () => {
    const courseNum = parseFloat(course);
    if (!isNaN(courseNum) && courseNum >= 0 && courseNum <= 360) {
      onSetCourse(courseNum);
    }
  };

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>
        Navigasjon
      </Typography>
      <Grid container spacing={2}>
        <Grid item xs={12}>
          <TextField
            fullWidth
            label="Kurs (0-360°)"
            value={course}
            onChange={(e) => setCourse(e.target.value)}
            type="number"
            InputProps={{
              inputProps: { min: 0, max: 360 },
            }}
          />
          {currentCourse !== undefined && (
            <Typography variant="caption">
              Nåværende kurs: {currentCourse.toFixed(1)}°
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
            Sett kurs
          </Button>
        </Grid>
        <Grid item xs={12}>
          <Button
            fullWidth
            variant="contained"
            color="secondary"
            onClick={() => setPositionDialogOpen(true)}
          >
            Sett posisjon
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
