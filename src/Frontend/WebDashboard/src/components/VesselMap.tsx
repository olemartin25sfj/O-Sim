import { useEffect, useMemo, useState, useCallback, useRef } from "react";
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
import { Box, Paper, Fab, Tooltip, TextField, IconButton } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { NavigationData } from "../types/messages";
import { RouteDialog } from "./RouteDialog";

// Ship icon
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

function MapCenterUpdater({
  position,
  follow,
  lastUserPanRef,
}: {
  position: LatLng;
  follow: boolean;
  lastUserPanRef: React.MutableRefObject<number>;
}) {
  const map = useMap();
  useEffect(() => {
    const onDragStart = () => {
      lastUserPanRef.current = Date.now();
    };
    map.on("dragstart", onDragStart);
    return () => {
      map.off("dragstart", onDragStart);
    };
  }, [map]);

  useEffect(() => {
    if (!follow) return;
    if (Date.now() - lastUserPanRef.current < 3000) return; // user panned recently
    map.setView(position, map.getZoom(), { animate: false });
  }, [map, position, follow, lastUserPanRef]);
  return null;
}

interface VesselMapProps {
  navigation: NavigationData | null;
  onSelectStart?: (lat: number, lon: number) => void;
  onSelectEnd?: (lat: number, lon: number) => void;
  selectedStart?: [number, number] | null;
  selectedEnd?: [number, number] | null;
  onActiveRouteChange?: (points: [number, number][] | null) => void;
  // Active journey (planned) start and end
  journeyStart?: [number, number] | null;
  journeyEnd?: [number, number] | null;
  journeyPlannedRoute?: [number, number][] | null; // full rute (waypoints) for aktiv reise
  // Track of vessel positions during active journey
  journeyTrack?: [number, number][];
  isJourneyRunning?: boolean;
  // Arrival info
  hasArrived?: boolean;
  arrivalPoint?: [number, number] | null;
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

export const VesselMap = ({
  navigation,
  onSelectStart,
  onSelectEnd,
  selectedStart,
  selectedEnd,
  journeyStart,
  journeyEnd,
  journeyTrack,
  isJourneyRunning,
  hasArrived,
  arrivalPoint,
  onActiveRouteChange,
  journeyPlannedRoute,
}: VesselMapProps) => {
  const [routes, setRoutes] = useState<Route[]>([]);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [selectedRouteId, setSelectedRouteId] = useState<string | null>(null);
  const [editingRouteId, setEditingRouteId] = useState<string | null>(null);
  const [tempName, setTempName] = useState<string>("");
  const [selectMode, setSelectMode] = useState<"none" | "start" | "end">(
    "none"
  );
  const [startEnd, setStartEnd] = useState<StartEndState>({}); // retained for local persistence fallback
  const [activeGenerated, setActiveGenerated] = useState<
    [number, number][] | null
  >(null);
  // Enkel interaktiv rute (brukerens manuelle veipunkter: start + mid + end)
  const [editableRoutePoints, setEditableRoutePoints] = useState<
    [number, number][] | null
  >(null);
  // Indeks til midlertidig veipunkt som dras inn (null når inaktiv)
  const [addingIdx, setAddingIdx] = useState<number | null>(null);
  const [smoothedRoute, setSmoothedRoute] = useState<[number, number][] | null>(
    null
  );
  // Removed predefined catalog routes
  // Panel visibility
  const [showRoutesPanel, setShowRoutesPanel] = useState(true);
  // Removed catalog panel state
  const [followVessel, setFollowVessel] = useState(true);
  const lastUserPanRef = useRef<number>(0);
  // Removed apiBase (catalog feature removed)

  const defaultPosition: [number, number] = [59.415065, 10.493529]; // Horten v/Asko
  const position: [number, number] = navigation
    ? [navigation.latitude, navigation.longitude]
    : defaultPosition;

  // Smoothed heading interpolation
  const [displayHeading, setDisplayHeading] = useState<number>(
    navigation?.headingDegrees ?? 0
  );
  useEffect(() => {
    if (!navigation) return;
    const target = navigation.headingDegrees;
    setDisplayHeading((prev) => {
      let diff = ((target - prev + 540) % 360) - 180; // shortest path
      // limit step to avoid large jumps (e.g., wrap-around)
      const step = diff * 0.35; // smoothing factor
      const next = prev + step;
      // close enough -> snap
      if (Math.abs(diff) < 0.5) return target;
      return (next + 360) % 360;
    });
  }, [navigation?.headingDegrees, navigation]);

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

  // Eksponer aktiv (valgt) rute til parent for journey-start
  useEffect(() => {
    if (onActiveRouteChange) {
      if (editableRoutePoints) {
        onActiveRouteChange(editableRoutePoints);
        return;
      }
      const r = routes.find((r) => r.id === selectedRouteId);
      onActiveRouteChange(r ? r.points : null);
    }
  }, [selectedRouteId, routes, onActiveRouteChange, editableRoutePoints]);

  // Sett opp enkel rute når bruker har valgt start og slutt (fra props) og ingen eksisterende redigering
  useEffect(() => {
    if (!editableRoutePoints && selectedStart && selectedEnd) {
      setEditableRoutePoints([selectedStart, selectedEnd]);
      // Slå av evt generert great-circle for å ikke forvirre
      setActiveGenerated(null);
    }
    // Hvis endepunkt fjernes, reset
    if (editableRoutePoints && (!selectedStart || !selectedEnd)) {
      setEditableRoutePoints(null);
    }
  }, [selectedStart, selectedEnd]);

  // Catmull-Rom smoothing (planar approx). Returnerer tett samplet kurve.
  function smoothRoute(points: [number, number][], spacingMeters = 120) {
    if (points.length < 3) return points;
    const res: [number, number][] = [];
    const dup = (i: number) =>
      points[Math.min(points.length - 1, Math.max(0, i))];
    const toRad = (d: number) => (d * Math.PI) / 180;
    const distMeters = (a: [number, number], b: [number, number]) => {
      const R = 6371000;
      const dLat = toRad(b[0] - a[0]);
      const dLon = toRad(b[1] - a[1]);
      const lat1 = toRad(a[0]);
      const lat2 = toRad(b[0]);
      const h =
        Math.sin(dLat / 2) ** 2 +
        Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLon / 2) ** 2;
      return 2 * R * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
    };
    const catmull = (
      p0: [number, number],
      p1: [number, number],
      p2: [number, number],
      p3: [number, number],
      t: number
    ): [number, number] => {
      const t2 = t * t;
      const t3 = t2 * t;
      const x =
        0.5 *
        (2 * p1[1] +
          (-p0[1] + p2[1]) * t +
          (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 +
          (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3);
      const y =
        0.5 *
        (2 * p1[0] +
          (-p0[0] + p2[0]) * t +
          (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 +
          (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3);
      return [y, x];
    };
    for (let i = 0; i < points.length - 1; i++) {
      const p0 = dup(i - 1);
      const p1 = dup(i);
      const p2 = dup(i + 1);
      const p3 = dup(i + 2);
      const segLen = distMeters(p1, p2);
      const steps = Math.max(2, Math.round(segLen / spacingMeters));
      for (let s = 0; s < steps; s++) {
        const t = s / steps;
        const c = catmull(p0, p1, p2, p3, t);
        if (res.length === 0 || distMeters(res[res.length - 1], c) > 5) {
          res.push(c);
        }
      }
    }
    // ensure last point
    const last = points[points.length - 1];
    if (
      !res.length ||
      res[res.length - 1][0] !== last[0] ||
      res[res.length - 1][1] !== last[1]
    )
      res.push(last);
    return res;
  }

  // Re-smooth når bruker endrer veipunkter
  useEffect(() => {
    if (editableRoutePoints && editableRoutePoints.length >= 2) {
      setSmoothedRoute(smoothRoute(editableRoutePoints));
    } else {
      setSmoothedRoute(null);
    }
  }, [editableRoutePoints]);

  // Hjelp: legg inn nytt veipunkt nærmest segment
  // insertWaypointAt ble erstattet av beginInsertDrag

  // Start innsetting + dragging: returnerer indeks for ny node
  const beginInsertDrag = (lat: number, lon: number) => {
    if (!editableRoutePoints || editableRoutePoints.length < 2) return;
    // Bruk samme algoritme men hold på indeksen vi satte inn
    let bestIdx = 0;
    let bestDist = Infinity;
    for (let i = 0; i < editableRoutePoints.length - 1; i++) {
      const d = distancePointToSegment(
        lat,
        lon,
        editableRoutePoints[i],
        editableRoutePoints[i + 1]
      );
      if (d < bestDist) {
        bestDist = d;
        bestIdx = i;
      }
    }
    setEditableRoutePoints((pts) => {
      if (!pts) return pts;
      const newPts = [...pts];
      newPts.splice(bestIdx + 1, 0, [lat, lon]);
      // Sett addingIdx etter at vi faktisk har lagt inn
      setAddingIdx(bestIdx + 1);
      return newPts;
    });
  };

  // Avstand punkt->segment (grovt, grader skalert med cos mid-lat for lon)
  function distancePointToSegment(
    lat: number,
    lon: number,
    a: [number, number],
    b: [number, number]
  ) {
    const toRad = (d: number) => (d * Math.PI) / 180;
    const latScale = 111320; // meter per grad lat
    const midLat = toRad((a[0] + b[0]) / 2);
    const lonScale = 111320 * Math.cos(midLat);
    const ax = (b[1] - a[1]) * lonScale;
    const ay = (b[0] - a[0]) * latScale;
    const px = (lon - a[1]) * lonScale;
    const py = (lat - a[0]) * latScale;
    const segLen2 = ax * ax + ay * ay || 1;
    let t = (px * ax + py * ay) / segLen2;
    t = Math.max(0, Math.min(1, t));
    const cx = ax * t;
    const cy = ay * t;
    const dx = px - cx;
    const dy = py - cy;
    return Math.sqrt(dx * dx + dy * dy);
  }

  // Oppdater waypoint ved dragging
  const updateWaypoint = (index: number, lat: number, lon: number) => {
    setEditableRoutePoints((pts) => {
      if (!pts) return pts;
      const next = [...pts];
      next[index] = [lat, lon];
      return next;
    });
  };

  const removeWaypoint = (index: number) => {
    setEditableRoutePoints((pts) => {
      if (!pts) return pts;
      if (index === 0 || index === pts.length - 1) return pts; // ikke slett endepunkt
      const next = pts.filter((_, i) => i !== index);
      return next;
    });
  };

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

  // Build generated route when both points exist
  useEffect(() => {
    if (startEnd.start && startEnd.end) {
      setActiveGenerated(generateGreatCircle(startEnd.start, startEnd.end, 2));
    }
  }, [startEnd, generateGreatCircle]);

  // Removed preview route detail effect

  const clearRoutes = () => {
    setRoutes([]);
    setSelectedRouteId(null);
  };

  // (Removed duplicate persistence & build effect block)

  // Map click for selecting start/destination
  const ClickHandler = () => {
    useMapEvent("click", (e) => {
      if (selectMode === "none") return;
      const latlng: [number, number] = [e.latlng.lat, e.latlng.lng];
      if (selectMode === "start") {
        onSelectStart?.(latlng[0], latlng[1]);
        setStartEnd((prev) => ({ ...prev, start: latlng }));
      } else if (selectMode === "end") {
        onSelectEnd?.(latlng[0], latlng[1]);
        setStartEnd((prev) => ({ ...prev, end: latlng }));
      }
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

  // (Removed catalog fetcher)

  // (Removed preview detail effect)

  // (Removed importCatalogRoute)

  return (
    <Paper sx={{ height: "100%", p: 1 }}>
      <Box sx={{ height: "100%", width: "100%", position: "relative" }}>
        <MapContainer
          center={position}
          zoom={13}
          style={{ height: "100%", width: "100%" }}
        >
          {/* CatalogFetcher removed */}
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
          {/* Interaktiv redigerbar rute (vises når start+slutt valgt) */}
          {editableRoutePoints && editableRoutePoints.length >= 2 && (
            <Polyline
              positions={editableRoutePoints}
              color="#00bcd4"
              weight={5}
              dashArray="2 10"
              opacity={0.85}
              eventHandlers={{
                mousedown: (e) => {
                  // Start drag-innsetting (ikke hvis allerede legger til)
                  if (addingIdx !== null) return;
                  beginInsertDrag(e.latlng.lat, e.latlng.lng);
                  // Forsøk å deaktivere kart-pan midlertidig
                  try {
                    // @ts-ignore intern leaflet ref
                    e.target._map.dragging.disable();
                  } catch {}
                },
              }}
            />
          )}
          {smoothedRoute && smoothedRoute.length > 1 && (
            <Polyline
              positions={smoothedRoute}
              color="#4caf50"
              weight={4}
              opacity={0.9}
            />
          )}
          {editableRoutePoints &&
            editableRoutePoints.map((pt, idx) => (
              <Marker
                key={`editwp-${idx}`}
                position={pt}
                draggable={idx !== 0 && idx !== editableRoutePoints.length - 1}
                eventHandlers={{
                  dragend: (e) => {
                    const ll = e.target.getLatLng();
                    updateWaypoint(idx, ll.lat, ll.lng);
                  },
                  contextmenu: () => removeWaypoint(idx),
                  click: (e) => {
                    e.originalEvent.preventDefault();
                    e.originalEvent.stopPropagation();
                  },
                }}
                icon={divIcon({
                  className: "edit-waypoint",
                  html: `<div style="background:${
                    idx === 0
                      ? "#1b5e20"
                      : idx === editableRoutePoints.length - 1
                      ? "#b71c1c"
                      : "#00bcd4"
                  };width:14px;height:14px;border-radius:50%;border:2px solid #fff;box-shadow:0 0 3px rgba(0,0,0,0.6);"></div>`,
                })}
              />
            ))}
          {/* Handler for mus-bevegelser når vi legger inn nytt veipunkt via drag */}
          {addingIdx !== null && (
            <DragInsertHandler
              addingIdx={addingIdx}
              stopDragging={() => setAddingIdx(null)}
              updateWaypoint={updateWaypoint}
            />
          )}
          {/* Preview removed */}
          {(selectedStart || startEnd.start) && (
            <Marker
              position={selectedStart || (startEnd.start as [number, number])}
              icon={
                new Icon({
                  iconUrl: "/marker-start.svg",
                  iconSize: [24, 24],
                  iconAnchor: [12, 12],
                })
              }
            />
          )}
          {(selectedEnd || startEnd.end) && (
            <Marker
              position={selectedEnd || (startEnd.end as [number, number])}
              icon={
                new Icon({
                  iconUrl: "/marker-end.svg",
                  iconSize: [24, 24],
                  iconAnchor: [12, 12],
                })
              }
            />
          )}
          {/* Planned active journey (when running) */}
          {journeyPlannedRoute && journeyPlannedRoute.length > 1 ? (
            <Polyline
              positions={journeyPlannedRoute}
              color={isJourneyRunning ? "#00e676" : "#757575"}
              dashArray={isJourneyRunning ? "6 10" : "2 6"}
              weight={isJourneyRunning ? 5 : 3}
              opacity={0.9}
            />
          ) : (
            journeyStart &&
            journeyEnd && (
              <Polyline
                positions={[journeyStart, journeyEnd]}
                color={isJourneyRunning ? "#00e676" : "#757575"}
                dashArray={isJourneyRunning ? "4 8" : "2 6"}
                weight={isJourneyRunning ? 5 : 3}
                opacity={0.85}
              />
            )
          )}
          {/* Traveled track */}
          {journeyTrack && journeyTrack.length > 1 && (
            <Polyline
              positions={journeyTrack}
              color="#ffeb3b"
              weight={3}
              opacity={0.9}
            />
          )}
          <ClickHandler />
          {/* Current vessel position */}
          {navigation && (
            <Marker position={position} icon={createShipIcon(displayHeading)}>
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
          {/* Arrival marker */}
          {hasArrived && arrivalPoint && (
            <Marker
              position={arrivalPoint}
              icon={divIcon({
                className: "arrival-marker-wrapper",
                html: `<div class="arrival-marker" title="Ankomst">
                        <div class="pulse"></div>
                        <img src="/marker-end.svg" width="28" height="28" />
                      </div>`,
              })}
            />
          )}
          <MapCenterUpdater
            position={new LatLng(position[0], position[1])}
            follow={followVessel}
            lastUserPanRef={lastUserPanRef}
          />
        </MapContainer>
        <Box
          sx={{
            position: "absolute",
            top: 8,
            left: "50%",
            transform: "translateX(-50%)",
            zIndex: 1200,
          }}
        >
          <Fab
            size="small"
            color={followVessel ? "primary" : "default"}
            onClick={() => setFollowVessel((f) => !f)}
            title={followVessel ? "Deaktiver auto-følge" : "Aktiver auto-følge"}
          >
            {followVessel ? "Følger" : "Fri"}
          </Fab>
        </Box>
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
              onClick={() => {
                setSelectMode((m) => (m === "start" ? "none" : "start"));
                // Deaktiver auto-følge når bruker går inn i seleksjonsmodus
                setFollowVessel(false);
              }}
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
              onClick={() => {
                setSelectMode((m) => (m === "end" ? "none" : "end"));
                setFollowVessel(false);
              }}
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
      {/* Catalog panel removed */}
    </Paper>
  );
};

// Hjelpekomponent: overvåker musbevegelse for nytt veipunkt
function DragInsertHandler({
  addingIdx,
  stopDragging,
  updateWaypoint,
}: {
  addingIdx: number;
  stopDragging: () => void;
  updateWaypoint: (idx: number, lat: number, lon: number) => void;
}) {
  useMapEvent("mousemove", (e) => {
    updateWaypoint(addingIdx, e.latlng.lat, e.latlng.lng);
  });
  useMapEvent("mouseup", (e) => {
    updateWaypoint(addingIdx, e.latlng.lat, e.latlng.lng);
    // Re‑aktiver dragging på kartet
    try {
      // @ts-ignore
      e.target.dragging.enable();
    } catch {}
    stopDragging();
  });
  useMapEvent("mouseout", () => {
    // Avslutt hvis musepekeren forlater kartet
    stopDragging();
  });
  return null;
}
