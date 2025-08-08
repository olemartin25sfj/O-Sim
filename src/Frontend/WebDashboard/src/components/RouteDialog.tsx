import { useState } from "react";
import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Typography,
} from "@mui/material";
import {
  norwegianShippingLanes,
  internationalShippingLanes,
} from "../data/shippingLanes";

interface RouteDialogProps {
  open: boolean;
  onClose: () => void;
  onAddRoute: (route: [number, number][]) => void;
}

export const RouteDialog = ({
  open,
  onClose,
  onAddRoute,
}: RouteDialogProps) => {
  const [routeType, setRouteType] = useState<"predefined" | "custom">(
    "predefined"
  );
  const [selectedRoute, setSelectedRoute] = useState("");
  const [startLat, setStartLat] = useState("");
  const [startLon, setStartLon] = useState("");
  const [endLat, setEndLat] = useState("");
  const [endLon, setEndLon] = useState("");

  const handleSubmit = () => {
    if (routeType === "predefined") {
      const route = [
        ...norwegianShippingLanes,
        ...internationalShippingLanes,
      ].find((r) => r.name === selectedRoute);
      if (route) {
        onAddRoute(route.route);
        onClose();
      }
    } else {
      const sLat = parseFloat(startLat);
      const sLon = parseFloat(startLon);
      const eLat = parseFloat(endLat);
      const eLon = parseFloat(endLon);

      if (!isNaN(sLat) && !isNaN(sLon) && !isNaN(eLat) && !isNaN(eLon)) {
        onAddRoute([
          [sLat, sLon],
          [eLat, eLon],
        ]);
        onClose();
      }
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Add New Route</DialogTitle>
      <DialogContent>
        <FormControl fullWidth margin="normal">
          <InputLabel>Route Type</InputLabel>
          <Select
            value={routeType}
            onChange={(e) =>
              setRouteType(e.target.value as "predefined" | "custom")
            }
            label="Route Type"
          >
            <MenuItem value="predefined">Predefined Route</MenuItem>
            <MenuItem value="custom">Custom Route</MenuItem>
          </Select>
        </FormControl>

        {routeType === "predefined" ? (
          <FormControl fullWidth margin="normal">
            <InputLabel>Select Route</InputLabel>
            <Select
              value={selectedRoute}
              onChange={(e) => setSelectedRoute(e.target.value)}
              label="Select Route"
            >
              <MenuItem disabled value="">
                <em>Norwegian Routes</em>
              </MenuItem>
              {norwegianShippingLanes.map((route) => (
                <MenuItem key={route.name} value={route.name}>
                  {route.name}
                </MenuItem>
              ))}
              <MenuItem disabled value="">
                <em>International Routes</em>
              </MenuItem>
              {internationalShippingLanes.map((route) => (
                <MenuItem key={route.name} value={route.name}>
                  {route.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        ) : (
          <>
            <Typography variant="subtitle1" gutterBottom sx={{ mt: 2 }}>
              Custom Route Coordinates
            </Typography>
            <TextField
              label="Start Latitude"
              value={startLat}
              onChange={(e) => setStartLat(e.target.value)}
              type="number"
              fullWidth
              margin="normal"
            />
            <TextField
              label="Start Longitude"
              value={startLon}
              onChange={(e) => setStartLon(e.target.value)}
              type="number"
              fullWidth
              margin="normal"
            />
            <TextField
              label="End Latitude"
              value={endLat}
              onChange={(e) => setEndLat(e.target.value)}
              type="number"
              fullWidth
              margin="normal"
            />
            <TextField
              label="End Longitude"
              value={endLon}
              onChange={(e) => setEndLon(e.target.value)}
              type="number"
              fullWidth
              margin="normal"
            />
          </>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          onClick={handleSubmit}
          color="primary"
          disabled={
            routeType === "predefined"
              ? !selectedRoute
              : !startLat || !startLon || !endLat || !endLon
          }
        >
          Add Route
        </Button>
      </DialogActions>
    </Dialog>
  );
};
