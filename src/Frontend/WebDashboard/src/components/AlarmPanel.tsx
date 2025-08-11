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
          alarms
            .slice()
            .reverse() // vis nyeste først
            .map((alarm, index) => {
              const severityMap: Record<string, "error" | "warning" | "info"> =
                {
                  Critical: "error",
                  Warning: "warning",
                  Info: "info",
                };
              const muiSeverity = severityMap[alarm.severity] ?? "info";
              const ts = new Date(alarm.timestampUtc).toLocaleTimeString();
              return (
                <ListItem key={index} disableGutters>
                  <Alert severity={muiSeverity} sx={{ width: "100%" }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                      {alarm.alarmType}{" "}
                      <Typography component="span" variant="caption">
                        ({ts} UTC)
                      </Typography>
                    </Typography>
                    {alarm.message}
                  </Alert>
                </ListItem>
              );
            })
        )}
      </List>
    </Paper>
  );
};
