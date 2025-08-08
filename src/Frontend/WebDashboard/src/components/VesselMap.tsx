import { useEffect, useState } from "react";
import {
  MapContainer,
  TileLayer,
  Marker,
  Popup,
  useMap,
  Polyline,
} from "react-leaflet";
import { Icon, LatLng } from "leaflet";
import "leaflet/dist/leaflet.css";
import { Box, Paper, Fab } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { NavigationData } from "../types/messages";
import { RouteDialog } from "./RouteDialog";

// Custom ship icon
const createShipIcon = (heading: number) =>
  new Icon({
    iconUrl: "/ship-icon.svg",
    iconSize: [32, 32],
    iconAnchor: [16, 16],
    popupAnchor: [0, -16],
    className: "ship-icon",
    style: { transform: `rotate(${heading}deg)` },
  });

// Custom component to update map center when vessel position changes
function MapCenterUpdater({ position }: { position: LatLng }) {
  const map = useMap();

  useEffect(() => {
    map.setView(position, map.getZoom());
  }, [map, position]);

  return null;
}

interface VesselMapProps {
  navigation: NavigationData | null;
}

interface Route {
  points: [number, number][];
}

export const VesselMap = ({ navigation }: VesselMapProps) => {
  const [routes, setRoutes] = useState<Route[]>([]);
  const [dialogOpen, setDialogOpen] = useState(false);

  const defaultPosition: [number, number] = [60.391262, 5.322054]; // Bergen
  const position: [number, number] = navigation
    ? [navigation.latitude, navigation.longitude]
    : defaultPosition;

  const handleAddRoute = (routePoints: [number, number][]) => {
    setRoutes([
      ...routes,
      {
        points: routePoints,
      },
    ]);
  };

  return (
    <Paper sx={{ height: "100%", p: 1 }}>
      <Box sx={{ height: "100%", width: "100%", position: "relative" }}>
        <MapContainer
          center={position}
          zoom={13}
          style={{ height: "100%", width: "100%" }}
        >
          {/* OpenSeaMap maritime layer */}
          <TileLayer
            url="https://tiles.openseamap.org/seamark/{z}/{x}/{y}.png"
            attribution='&copy; <a href="http://www.openseamap.org">OpenSeaMap</a> contributors'
          />
          {/* OpenStreetMap base layer */}
          <TileLayer
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          />
          {/* Draw routes */}
          {routes.map((route, index) => (
            <Polyline
              key={index}
              positions={route.points}
              color="#2196f3"
              weight={3}
              opacity={0.7}
            />
          ))}
          {/* Current vessel position */}
          {navigation && (
            <Marker
              position={position}
              icon={createShipIcon(navigation.heading)}
            >
              <Popup>
                <div>
                  <strong>Vessel Position</strong>
                  <br />
                  Latitude: {navigation.latitude.toFixed(6)}
                  <br />
                  Longitude: {navigation.longitude.toFixed(6)}
                  <br />
                  Heading: {navigation.heading.toFixed(1)}°<br />
                  Speed: {navigation.speed.toFixed(1)} knots
                </div>
              </Popup>
            </Marker>
          )}
          <MapCenterUpdater position={new LatLng(position[0], position[1])} />
        </MapContainer>
        {/* Add route button */}
        <Fab
          color="primary"
          aria-label="add route"
          onClick={() => setDialogOpen(true)}
          sx={{ position: "absolute", bottom: 16, right: 16 }}
        >
          <AddIcon />
        </Fab>
      </Box>
      <RouteDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onAddRoute={handleAddRoute}
      />
    </Paper>
  );
};
