import { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Box,
} from "@mui/material";

interface Port {
  name: string;
  latitude: number;
  longitude: number;
}

const norwegianPorts: Port[] = [
  { name: "Horten", latitude: 59.4167, longitude: 10.4833 },
  { name: "Oslo", latitude: 59.9139, longitude: 10.7522 },
  { name: "Bergen", latitude: 60.3913, longitude: 5.3221 },
  { name: "Stavanger", latitude: 58.97, longitude: 5.7331 },
  { name: "Trondheim", latitude: 63.4305, longitude: 10.3951 },
  { name: "Tromsø", latitude: 69.6492, longitude: 18.9553 },
];

interface SetPositionDialogProps {
  open: boolean;
  onClose: () => void;
  onSetPosition: (latitude: number, longitude: number) => void;
}

export const SetPositionDialog = ({
  open,
  onClose,
  onSetPosition,
}: SetPositionDialogProps) => {
  const [selectedPort, setSelectedPort] = useState<string>("");
  const [customMode, setCustomMode] = useState(false);
  const [latitude, setLatitude] = useState("");
  const [longitude, setLongitude] = useState("");

  const handlePortSelect = (portName: string) => {
    if (portName === "custom") {
      setCustomMode(true);
      setSelectedPort("");
    } else {
      setCustomMode(false);
      setSelectedPort(portName);
      const port = norwegianPorts.find((p) => p.name === portName);
      if (port) {
        setLatitude(port.latitude.toString());
        setLongitude(port.longitude.toString());
      }
    }
  };

  const handleSubmit = () => {
    const lat = parseFloat(latitude);
    const lon = parseFloat(longitude);
    if (!isNaN(lat) && !isNaN(lon)) {
      onSetPosition(lat, lon);
      onClose();
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Set Vessel Starting Position</DialogTitle>
      <DialogContent>
        <Box sx={{ my: 2 }}>
          <FormControl fullWidth>
            <InputLabel>Select Port</InputLabel>
            <Select
              value={customMode ? "custom" : selectedPort}
              onChange={(e) => handlePortSelect(e.target.value)}
              label="Select Port"
            >
              {norwegianPorts.map((port) => (
                <MenuItem key={port.name} value={port.name}>
                  {port.name}
                </MenuItem>
              ))}
              <MenuItem value="custom">Custom Position</MenuItem>
            </Select>
          </FormControl>
        </Box>

        {customMode && (
          <Box sx={{ mt: 2 }}>
            <TextField
              label="Latitude"
              value={latitude}
              onChange={(e) => setLatitude(e.target.value)}
              type="number"
              fullWidth
              margin="normal"
            />
            <TextField
              label="Longitude"
              value={longitude}
              onChange={(e) => setLongitude(e.target.value)}
              type="number"
              fullWidth
              margin="normal"
            />
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          onClick={handleSubmit}
          color="primary"
          disabled={!latitude || !longitude}
        >
          Set Position
        </Button>
      </DialogActions>
    </Dialog>
  );
};
