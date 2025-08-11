import { useEffect, useMemo, useState } from "react";
import {
  MapContainer,
  TileLayer,
  Marker,
  Popup,
  useMap,
  Polyline,
} from "react-leaflet";
import { Icon, LatLng, divIcon } from "leaflet";
import "leaflet/dist/leaflet.css";
import { Box, Paper, Fab, Tooltip } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { NavigationData } from "../types/messages";
import { RouteDialog } from "./RouteDialog";

// Ship icon (CSS rotation for crisper rendering and avoiding image re-create)
const createShipIcon = (heading: number) =>
  divIcon({
    className: "ship-icon-wrapper",
    html: `<div class="ship-icon" style="transform: rotate(${heading}deg)">
            <img src="/ship-icon.svg" width="32" height="32" />
          </div>`,
    iconSize: [32, 32],
    iconAnchor: [16, 16],
    popupAnchor: [0, -16],
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

  const defaultPosition: [number, number] = [59.415065, 10.493529]; // Horten v/Asko
  const position: [number, number] = navigation
    ? [navigation.latitude, navigation.longitude]
    : defaultPosition;

  const handleAddRoute = (routePoints: [number, number][]) => {
    setRoutes((r) => [...r, { points: routePoints }]);
  };

  // Persist routes in localStorage
  useEffect(() => {
    try {
      const raw = localStorage.getItem("o-sim.routes");
      if (raw) {
        const parsed = JSON.parse(raw) as Route[];
        if (Array.isArray(parsed)) setRoutes(parsed);
      }
    } catch {
      /* ignore */
    }
  }, []);

  useEffect(() => {
    try {
      localStorage.setItem("o-sim.routes", JSON.stringify(routes));
    } catch {
      /* ignore */
    }
  }, [routes]);

  const clearRoutes = () => setRoutes([]);

  // Calculate lengths (nautical miles) lazily
  const routeSummaries = useMemo(() => {
    const R = 6371000; // meters
    const toRad = (d: number) => (d * Math.PI) / 180;
    const dist = (a: [number, number], b: [number, number]) => {
      const dLat = toRad(b[0] - a[0]);
      const dLon = toRad(b[1] - a[1]);
      const lat1 = toRad(a[0]);
      const lat2 = toRad(b[0]);
      const h =
        Math.sin(dLat / 2) * Math.sin(dLat / 2) +
        Math.cos(lat1) *
          Math.cos(lat2) *
          Math.sin(dLon / 2) *
          Math.sin(dLon / 2);
      const c = 2 * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
      return R * c; // meters
    };
    return routes.map((r) => {
      let meters = 0;
      for (let i = 1; i < r.points.length; i++)
        meters += dist(r.points[i - 1], r.points[i]);
      const nm = meters / 1852;
      return { meters, nm };
    });
  }, [routes]);

  return (
    <Paper sx={{ height: "100%", p: 1 }}>
      <Box sx={{ height: "100%", width: "100%", position: "relative" }}>
        <MapContainer
          center={position}
          zoom={13}
          style={{ height: "100%", width: "100%" }}
        >
          {/* Base (OSM) first */}
          <TileLayer
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          />
          {/* Maritime overlay */}
          <TileLayer
            url="https://tiles.openseamap.org/seamark/{z}/{x}/{y}.png"
            attribution='&copy; <a href="http://www.openseamap.org">OpenSeaMap</a> contributors'
            opacity={0.9}
          />
          {/* Draw routes */}
          {routes.map((route, index) => (
            <>
              <Polyline
                key={`pl-${index}`}
                positions={route.points}
                color="#2196f3"
                weight={3}
                opacity={0.75}
              />
              {/* Start marker */}
              {route.points.length > 0 && (
                <Marker
                  key={`start-${index}`}
                  position={route.points[0]}
                  icon={
                    new Icon({
                      iconUrl: "/marker-start.svg",
                      iconSize: [20, 20],
                      iconAnchor: [10, 10],
                    })
                  }
                />
              )}
              {/* End marker */}
              {route.points.length > 1 && (
                <Marker
                  key={`end-${index}`}
                  position={route.points[route.points.length - 1]}
                  icon={
                    new Icon({
                      iconUrl: "/marker-end.svg",
                      iconSize: [20, 20],
                      iconAnchor: [10, 10],
                    })
                  }
                />
              )}
            </>
          ))}
          {/* Current vessel position */}
          {navigation && (
            <Marker
              position={position}
              icon={createShipIcon(navigation.headingDegrees)}
            >
              <Popup>
                <div>
                  <strong>Vessel Position</strong>
                  <br />
                  Latitude: {navigation.latitude.toFixed(6)}
                  <br />
                  Longitude: {navigation.longitude.toFixed(6)}
                  <br />
                  Heading: {navigation.headingDegrees.toFixed(1)}°<br />
                  Speed: {navigation.speedKnots.toFixed(1)} knots
                </div>
              </Popup>
            </Marker>
          )}
          <MapCenterUpdater position={new LatLng(position[0], position[1])} />
        </MapContainer>
        {/* Add route button */}
        <Box
          sx={{
            position: "absolute",
            bottom: 16,
            right: 16,
            display: "flex",
            flexDirection: "column",
            gap: 1,
          }}
        >
          <Tooltip title="Legg til rute">
            <Fab
              color="primary"
              size="medium"
              aria-label="add route"
              onClick={() => setDialogOpen(true)}
            >
              <AddIcon />
            </Fab>
          </Tooltip>
          {routes.length > 0 && (
            <Tooltip title="Fjern alle ruter">
              <Fab
                color="secondary"
                size="small"
                aria-label="clear routes"
                onClick={clearRoutes}
              >
                <DeleteIcon />
              </Fab>
            </Tooltip>
          )}
        </Box>
      </Box>
      <RouteDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onAddRoute={handleAddRoute}
      />
      {/* Route summary overlay */}
      {routes.length > 0 && (
        <Box
          sx={{
            position: "absolute",
            top: 8,
            left: 8,
            bgcolor: "rgba(0,0,0,0.55)",
            color: "#fff",
            p: 1,
            borderRadius: 1,
            fontSize: 12,
          }}
        >
          {routes.map((_, i) => (
            <div key={i}>
              Route {i + 1}: {routeSummaries[i].nm.toFixed(2)} nm
            </div>
          ))}
        </Box>
      )}
    </Paper>
  );
};
