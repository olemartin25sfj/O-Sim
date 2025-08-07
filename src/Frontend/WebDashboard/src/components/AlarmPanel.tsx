import React from "react";
import {
  Paper,
  Typography,
  List,
  ListItem,
  ListItemText,
  Alert,
} from "@mui/material";
import { AlarmData } from "../types/messages";

interface AlarmPanelProps {
  alarms: AlarmData[];
}

export const AlarmPanel: React.FC<AlarmPanelProps> = ({ alarms }) => {
  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>
        Active Alarms
      </Typography>
      <List>
        {alarms.length === 0 ? (
          <ListItem>
            <ListItemText primary="No active alarms" />
          </ListItem>
        ) : (
          alarms.map((alarm, index) => (
            <ListItem key={index}>
              <Alert
                severity={
                  alarm.severity === "critical"
                    ? "error"
                    : alarm.severity === "warning"
                    ? "warning"
                    : "info"
                }
                sx={{ width: "100%" }}
              >
                <Typography variant="subtitle2">{alarm.type}</Typography>
                {alarm.message}
              </Alert>
            </ListItem>
          ))
        )}
      </List>
    </Paper>
  );
};
