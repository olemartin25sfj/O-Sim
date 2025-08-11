import { useEffect, useMemo, useState, useCallback } from "react";
import {
  MapContainer,
  TileLayer,
  Marker,
  Popup,
  useMap,
  Polyline,
} from "react-leaflet";
import { useMapEvent } from "react-leaflet";
import { Icon, LatLng, divIcon } from "leaflet";
import "leaflet/dist/leaflet.css";
import {
  Box,
  Paper,
  Fab,
  Tooltip,
  TextField,
  IconButton,
  Chip,
  Divider,
  CircularProgress,
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
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
  id: string;
  name: string;
  points: [number, number][];
  source?: "custom" | "generated" | "shipping-lane";
  modified?: boolean; // true once user drags a waypoint or renames
}

interface StartEndState {
  start?: [number, number];
  end?: [number, number];
}

export const VesselMap = ({ navigation }: VesselMapProps) => {
  const [routes, setRoutes] = useState<Route[]>([]);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [selectedRouteId, setSelectedRouteId] = useState<string | null>(null);
  const [editingRouteId, setEditingRouteId] = useState<string | null>(null);
  const [tempName, setTempName] = useState<string>("");
  const [selectMode, setSelectMode] = useState<"none" | "start" | "end">(
    "none"
  );
  const [startEnd, setStartEnd] = useState<StartEndState>({});
  const [activeGenerated, setActiveGenerated] = useState<
    [number, number][] | null
  >(null);
  // Catalog (fetched) routes state
  interface CatalogRouteMeta {
    id: string;
    name: string;
    category: string;
    minLat: number; // bounding box
    minLon: number;
    maxLat: number;
    maxLon: number;
    lengthNm: number;
    previewPoints: [number, number][];
  }
  const [catalog, setCatalog] = useState<CatalogRouteMeta[]>([]);
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [previewRouteId, setPreviewRouteId] = useState<string | null>(null);
  const previewRoute = useMemo(
    () => catalog.find((c) => c.id === previewRouteId) || null,
    [catalog, previewRouteId]
  );
  // Panel visibility
  const [showRoutesPanel, setShowRoutesPanel] = useState(true);
  const [showCatalogPanel, setShowCatalogPanel] = useState(true);
  const apiBase = (import.meta as any).env?.VITE_API_BASE || ""; // relative fallback

  const defaultPosition: [number, number] = [59.415065, 10.493529]; // Horten v/Asko
  const position: [number, number] = navigation
    ? [navigation.latitude, navigation.longitude]
    : defaultPosition;

  const generateId = () => Math.random().toString(36).slice(2, 11);

  const handleAddRoute = (
    routePoints: [number, number][],
    name?: string,
    source: Route["source"] = "custom"
  ) => {
    setRoutes((r) => [
      ...r,
      {
        id: generateId(),
        name: name || `Route ${r.length + 1}`,
        points: routePoints,
        source,
      },
    ]);
  };

  // Persist routes in localStorage
  useEffect(() => {
    try {
      const raw = localStorage.getItem("o-sim.routes");
      if (raw) {
        const parsed = JSON.parse(raw) as any[];
        if (Array.isArray(parsed)) {
          setRoutes(
            parsed.map((p, idx) => ({
              id: p.id || Math.random().toString(36).slice(2, 11),
              name: p.name || `Route ${idx + 1}`,
              points: p.points,
              source: p.source || "custom",
              modified: !!p.modified,
            }))
          );
        }
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

  const clearRoutes = () => {
    setRoutes([]);
    setSelectedRouteId(null);
  };

  // Great-circle generation between start and end (approximate) with segment length ~2nm
  const generateGreatCircle = useCallback(
    (a: [number, number], b: [number, number], segmentNm = 2) => {
      const toRad = (d: number) => (d * Math.PI) / 180;
      const toDeg = (r: number) => (r * 180) / Math.PI;
      const Rm = 6371000; // meters
      const distanceMeters = (() => {
        const dLat = toRad(b[0] - a[0]);
        const dLon = toRad(b[1] - a[1]);
        const lat1 = toRad(a[0]);
        const lat2 = toRad(b[0]);
        const h =
          Math.sin(dLat / 2) ** 2 +
          Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLon / 2) ** 2;
        return 2 * Rm * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
      })();
      if (distanceMeters === 0) return [a];
      const segmentMeters = segmentNm * 1852;
      const steps = Math.max(1, Math.round(distanceMeters / segmentMeters));
      const lat1 = toRad(a[0]);
      const lon1 = toRad(a[1]);
      const lat2 = toRad(b[0]);
      const lon2 = toRad(b[1]);
      const d = (() => {
        const dLat = lat2 - lat1;
        const dLon = lon2 - lon1;
        const h =
          Math.sin(dLat / 2) ** 2 +
          Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLon / 2) ** 2;
        return 2 * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
      })();
      const points: [number, number][] = [];
      for (let i = 0; i <= steps; i++) {
        const f = i / steps;
        const A = Math.sin((1 - f) * d) / Math.sin(d);
        const B = Math.sin(f * d) / Math.sin(d);
        const x =
          A * Math.cos(lat1) * Math.cos(lon1) +
          B * Math.cos(lat2) * Math.cos(lon2);
        const y =
          A * Math.cos(lat1) * Math.sin(lon1) +
          B * Math.cos(lat2) * Math.sin(lon2);
        const z = A * Math.sin(lat1) + B * Math.sin(lat2);
        const lat = Math.atan2(z, Math.sqrt(x * x + y * y));
        const lon = Math.atan2(y, x);
        points.push([toDeg(lat), ((toDeg(lon) + 540) % 360) - 180]);
      }
      return points;
    },
    []
  );

  // Persist start/end & generated route
  useEffect(() => {
    try {
      const raw = localStorage.getItem("o-sim.startEnd");
      if (raw) setStartEnd(JSON.parse(raw));
      const rawGen = localStorage.getItem("o-sim.generatedRoute");
      if (rawGen) setActiveGenerated(JSON.parse(rawGen));
    } catch {
      /*ignore*/
    }
  }, []);
  useEffect(() => {
    try {
      localStorage.setItem("o-sim.startEnd", JSON.stringify(startEnd));
    } catch {}
  }, [startEnd]);
  useEffect(() => {
    try {
      if (activeGenerated)
        localStorage.setItem(
          "o-sim.generatedRoute",
          JSON.stringify(activeGenerated)
        );
      else localStorage.removeItem("o-sim.generatedRoute");
    } catch {}
  }, [activeGenerated]);

  // Build generated route when both points exist
  useEffect(() => {
    if (startEnd.start && startEnd.end) {
      setActiveGenerated(generateGreatCircle(startEnd.start, startEnd.end, 2));
    }
  }, [startEnd, generateGreatCircle]);

  // Map click for selecting start/destination
  const ClickHandler = () => {
    useMapEvent("click", (e) => {
      if (selectMode === "none") return;
      const latlng: [number, number] = [e.latlng.lat, e.latlng.lng];
      setStartEnd((prev) =>
        selectMode === "start"
          ? { ...prev, start: latlng }
          : { ...prev, end: latlng }
      );
      setSelectMode("none");
    });
    return null;
  };

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

  // --- Map bounds watcher & catalog fetch ---
  const CatalogFetcher = () => {
    const map = useMap();
    useEffect(() => {
      const handler = () => {
        const b = map.getBounds();
        const minLat = b.getSouth();
        const maxLat = b.getNorth();
        const minLon = b.getWest();
        const maxLon = b.getEast();
        const bbox = `${minLat.toFixed(6)},${minLon.toFixed(
          6
        )},${maxLat.toFixed(6)},${maxLon.toFixed(6)}`;
        setCatalogLoading(true);
        setCatalogError(null);
        // Abort controller for race conditions
        const ac = new AbortController();
        fetch(`${apiBase}/api/routes?bbox=${bbox}`, { signal: ac.signal })
          .then(async (r) => {
            if (!r.ok) throw new Error(`HTTP ${r.status}`);
            const data = await r.json();
            // Normalize keys (backend uses PascalCase for JSON? Actually System.Text.Json default: property names as in record ctor parameters -> Id, Name etc.)
            const norm = (data as any[]).map((d) => ({
              id: d.id || d.Id,
              name: d.name || d.Name,
              category: d.category || d.Category,
              minLat: d.minLat ?? d.MinLat,
              minLon: d.minLon ?? d.MinLon,
              maxLat: d.maxLat ?? d.MaxLat,
              maxLon: d.maxLon ?? d.MaxLon,
              lengthNm: d.lengthNm ?? d.LengthNm,
              previewPoints: (d.previewPoints || d.PreviewPoints) as [
                number,
                number
              ][],
            }));
            setCatalog(norm);
          })
          .catch((e) => {
            if (e.name !== "AbortError") setCatalogError(e.message);
          })
          .finally(() => setCatalogLoading(false));
      };
      // Initial fetch
      handler();
      map.on("moveend", handler);
      return () => {
        map.off("moveend", handler);
      };
    }, [map]);
    return null;
  };

  // Fetch detail when preview selected (currently same data but future-proof)
  useEffect(() => {
    if (!previewRouteId) return;
    // If already imported, no need for detail
    const imported = routes.find((r) => r.id === previewRouteId);
    if (imported) return;
    const meta = catalog.find((c) => c.id === previewRouteId);
    if (!meta) return;
    // Optionally could fetch /api/routes/{id} if we had simplified preview vs full
  }, [previewRouteId, catalog, routes]);

  const importCatalogRoute = (id: string) => {
    const meta = catalog.find((c) => c.id === id);
    if (!meta) return;
    if (routes.some((r) => r.id === id)) return; // already
    setRoutes((rs) => [
      ...rs,
      {
        id: meta.id,
        name: meta.name,
        points: meta.previewPoints,
        source: "shipping-lane",
      },
    ]);
    setSelectedRouteId(id);
  };

  return (
    <Paper sx={{ height: "100%", p: 1 }}>
      <Box sx={{ height: "100%", width: "100%", position: "relative" }}>
        <MapContainer
          center={position}
          zoom={13}
          style={{ height: "100%", width: "100%" }}
        >
          <CatalogFetcher />
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
          {/* Draw stored routes */}
          {routes.map((route) => {
            const selected = route.id === selectedRouteId;
            const baseColor =
              route.source === "shipping-lane" ? "#8e24aa" : "#2196f3";
            const color = selected ? "#1976d2" : baseColor;
            return (
              <>
                <Polyline
                  key={`pl-${route.id}`}
                  positions={route.points}
                  color={color}
                  weight={selected ? 5 : 3}
                  opacity={selected ? 0.95 : 0.75}
                  eventHandlers={{
                    click: (e) => {
                      e.originalEvent.preventDefault();
                      setSelectedRouteId(route.id);
                    },
                  }}
                />
                {/* Start marker */}
                {route.points.length > 0 && (
                  <Marker
                    key={`start-${route.id}`}
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
                    key={`end-${route.id}`}
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
                {/* Draggable waypoints when selected */}
                {selected &&
                  route.points.map((pt, idx) => (
                    <Marker
                      key={`wp-${route.id}-${idx}`}
                      position={pt}
                      draggable
                      eventHandlers={{
                        dragend: (e) => {
                          const newLat = e.target.getLatLng().lat;
                          const newLng = e.target.getLatLng().lng;
                          setRoutes((rs) =>
                            rs.map((r) => {
                              if (r.id !== route.id) return r;
                              const renamed =
                                r.source === "shipping-lane" &&
                                !r.modified &&
                                !r.name.endsWith("(modified)")
                                  ? `${r.name} (modified)`
                                  : r.name;
                              return {
                                ...r,
                                name: renamed,
                                modified: true,
                                points: r.points.map((p, i) =>
                                  i === idx
                                    ? ([newLat, newLng] as [number, number])
                                    : p
                                ),
                              };
                            })
                          );
                        },
                        click: (e) => {
                          e.originalEvent.preventDefault();
                        },
                      }}
                      icon={divIcon({
                        className: "waypoint-marker",
                        html: `<div style="background:#fff;border:2px solid ${
                          idx === 0
                            ? "#1b5e20"
                            : idx === route.points.length - 1
                            ? "#b71c1c"
                            : route.source === "shipping-lane"
                            ? "#8e24aa"
                            : "#1976d2"
                        };width:14px;height:14px;border-radius:50%;box-shadow:0 0 2px rgba(0,0,0,0.6);"></div>`,
                      })}
                    />
                  ))}
              </>
            );
          })}
          {/* Generated start->end route */}
          {activeGenerated && (
            <Polyline
              positions={activeGenerated}
              color="#ff9800"
              weight={4}
              opacity={0.9}
            />
          )}
          {/* Preview (not yet imported) */}
          {previewRoute && !routes.some((r) => r.id === previewRoute.id) && (
            <Polyline
              positions={previewRoute.previewPoints}
              color="#8e24aa"
              weight={4}
              opacity={0.6}
              dashArray="8 8"
            />
          )}
          {startEnd.start && (
            <Marker
              position={startEnd.start}
              icon={
                new Icon({
                  iconUrl: "/marker-start.svg",
                  iconSize: [24, 24],
                  iconAnchor: [12, 12],
                })
              }
            />
          )}
          {startEnd.end && (
            <Marker
              position={startEnd.end}
              icon={
                new Icon({
                  iconUrl: "/marker-end.svg",
                  iconSize: [24, 24],
                  iconAnchor: [12, 12],
                })
              }
            />
          )}
          <ClickHandler />
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
          <Tooltip
            title={
              selectMode === "start"
                ? "Klikk i kartet for start"
                : "Velg startpunkt"
            }
          >
            <Fab
              size="small"
              color={selectMode === "start" ? "success" : "default"}
              onClick={() =>
                setSelectMode((m) => (m === "start" ? "none" : "start"))
              }
            >
              S
            </Fab>
          </Tooltip>
          <Tooltip
            title={
              selectMode === "end"
                ? "Klikk i kartet for destinasjon"
                : "Velg destinasjon"
            }
          >
            <Fab
              size="small"
              color={selectMode === "end" ? "error" : "default"}
              onClick={() =>
                setSelectMode((m) => (m === "end" ? "none" : "end"))
              }
            >
              D
            </Fab>
          </Tooltip>
          {activeGenerated && (
            <Tooltip title="Legg generert rute til listen">
              <Fab
                size="small"
                color="info"
                onClick={() =>
                  setRoutes((r) => [
                    ...r,
                    {
                      id: generateId(),
                      name: `Generated ${r.length + 1}`,
                      points: activeGenerated,
                      source: "generated",
                    },
                  ])
                }
              >
                +
              </Fab>
            </Tooltip>
          )}
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
      {/* Route list / summary & rename */}
      {routes.length > 0 && (
        <Box
          sx={{
            position: "absolute",
            top: 8,
            left: 8,
            zIndex: 1000,
            display: "flex",
            flexDirection: "column",
            gap: 0.5,
            maxWidth: 300,
          }}
        >
          {showRoutesPanel &&
            routes.map((r, i) => {
              const selected = r.id === selectedRouteId;
              const editing = r.id === editingRouteId;
              return (
                <Paper
                  key={r.id}
                  sx={{
                    p: 0.5,
                    bgcolor: selected
                      ? "rgba(25,118,210,0.85)"
                      : "rgba(0,0,0,0.55)",
                    color: "#fff",
                    display: "flex",
                    alignItems: "center",
                    gap: 0.5,
                  }}
                >
                  {!editing && (
                    <span
                      style={{
                        cursor: "pointer",
                        fontWeight: selected ? 600 : 400,
                      }}
                      onClick={() => setSelectedRouteId(r.id)}
                    >
                      {r.name}
                    </span>
                  )}
                  {editing && (
                    <TextField
                      size="small"
                      value={tempName}
                      autoFocus
                      onChange={(e) => setTempName(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") {
                          setRoutes((rs) =>
                            rs.map((x) =>
                              x.id === r.id
                                ? {
                                    ...x,
                                    name: tempName || x.name,
                                    modified: true,
                                  }
                                : x
                            )
                          );
                          setEditingRouteId(null);
                        }
                        if (e.key === "Escape") {
                          setEditingRouteId(null);
                        }
                      }}
                      onBlur={() => {
                        setEditingRouteId(null);
                        setTempName("");
                      }}
                      variant="outlined"
                      sx={{ bgcolor: "#fff", borderRadius: 0.5, flex: 1 }}
                    />
                  )}
                  <span style={{ fontSize: 11, opacity: 0.8 }}>
                    {routeSummaries[i]?.nm.toFixed(2)} nm
                  </span>
                  {selected && !editing && (
                    <IconButton
                      size="small"
                      sx={{ color: "#fff" }}
                      onClick={() => {
                        setEditingRouteId(r.id);
                        setTempName(r.name);
                      }}
                    >
                      <EditIcon fontSize="inherit" />
                    </IconButton>
                  )}
                  {selected && (
                    <IconButton
                      size="small"
                      sx={{ color: "#fff" }}
                      onClick={() =>
                        setRoutes((rs) => rs.filter((x) => x.id !== r.id))
                      }
                    >
                      <DeleteIcon fontSize="inherit" />
                    </IconButton>
                  )}
                </Paper>
              );
            })}
          {/* Toggle button */}
          <Fab
            size="small"
            sx={{
              alignSelf: "flex-start",
              mt: 0.5,
              bgcolor: showRoutesPanel ? "rgba(0,0,0,0.6)" : "rgba(0,0,0,0.4)",
              color: "#fff",
            }}
            onClick={() => setShowRoutesPanel((v) => !v)}
            title={showRoutesPanel ? "Skjul ruteliste" : "Vis ruteliste"}
          >
            {showRoutesPanel ? "−" : "+"}
          </Fab>
        </Box>
      )}
      {/* Catalog side panel (always visible) */}
      <Box
        sx={{
          position: "absolute",
          top: 8,
          right: 8,
          width: 300,
          maxHeight: "75%",
          display: "flex",
          flexDirection: "column",
          gap: 0.5,
          pointerEvents: "auto",
          zIndex: 1000,
        }}
      >
        {showCatalogPanel && (
          <Paper
            sx={{
              p: 1,
              bgcolor: "rgba(0,0,0,0.65)",
              color: "#fff",
              overflowY: "auto",
            }}
          >
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <strong style={{ flex: 1 }}>Tilgjengelige ruter</strong>
              {catalogLoading && <CircularProgress size={16} color="inherit" />}
            </Box>
            {catalogError && (
              <Box sx={{ mt: 1, fontSize: 12, color: "#ffb74d" }}>
                Feil: {catalogError}
              </Box>
            )}
            <Divider sx={{ my: 1, borderColor: "rgba(255,255,255,0.2)" }} />
            <Box sx={{ display: "flex", flexDirection: "column", gap: 0.75 }}>
              {catalog.map((c) => {
                const imported = routes.some((r) => r.id === c.id);
                const previewing = previewRouteId === c.id;
                return (
                  <Paper
                    key={c.id}
                    sx={{
                      p: 0.75,
                      bgcolor: previewing
                        ? "rgba(142,36,170,0.6)"
                        : "rgba(255,255,255,0.08)",
                      display: "flex",
                      flexDirection: "column",
                      gap: 0.5,
                    }}
                    elevation={0}
                  >
                    <Box
                      sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: 0.5,
                        cursor: "pointer",
                      }}
                      onClick={() =>
                        setPreviewRouteId((id) => (id === c.id ? null : c.id))
                      }
                    >
                      <span style={{ fontWeight: 500 }}>{c.name}</span>
                      <Chip
                        size="small"
                        label={c.category}
                        sx={{ ml: "auto", bgcolor: "#3949ab", color: "#fff" }}
                      />
                    </Box>
                    <Box
                      sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: 1,
                        fontSize: 12,
                        opacity: 0.85,
                      }}
                    >
                      <span>{c.lengthNm.toFixed(1)} nm</span>
                      <Box sx={{ ml: "auto", display: "flex", gap: 0.5 }}>
                        {!imported && (
                          <Fab
                            size="small"
                            color="primary"
                            sx={{ width: 32, height: 32 }}
                            onClick={() => importCatalogRoute(c.id)}
                          >
                            +
                          </Fab>
                        )}
                        {imported && (
                          <Chip
                            size="small"
                            label="Importert"
                            color="success"
                            sx={{ fontSize: 10 }}
                          />
                        )}
                      </Box>
                    </Box>
                  </Paper>
                );
              })}
              {catalog.length === 0 && !catalogLoading && !catalogError && (
                <span style={{ fontSize: 12, opacity: 0.7 }}>
                  Ingen ruter i utsnittet.
                </span>
              )}
            </Box>
          </Paper>
        )}
        <Fab
          size="small"
          sx={{
            alignSelf: "flex-end",
            bgcolor: showCatalogPanel ? "rgba(0,0,0,0.6)" : "rgba(0,0,0,0.4)",
            color: "#fff",
          }}
          onClick={() => setShowCatalogPanel((v) => !v)}
          title={showCatalogPanel ? "Skjul rute-katalog" : "Vis rute-katalog"}
        >
          {showCatalogPanel ? "−" : "+"}
        </Fab>
      </Box>
    </Paper>
  );
};
