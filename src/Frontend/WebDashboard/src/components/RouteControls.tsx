import React, { useEffect, useState } from "react";
import {
  Box,
  Paper,
  Typography,
  Slider,
  Button,
  Chip,
  Stack,
  IconButton,
  Tooltip,
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import StopIcon from "@mui/icons-material/Stop";
import FlashOnIcon from "@mui/icons-material/FlashOn";

interface RouteControlsProps {
  onSetSpeed: (speed: number) => void;
  currentSpeed?: number;
  maxSpeed?: number;
  onStartRoute?: () => void;
  onStopRoute?: () => void;
  routeActive?: boolean;
  selectedRouteName?: string | null;
}

export const RouteControls: React.FC<RouteControlsProps> = ({
  onSetSpeed,
  currentSpeed,
  maxSpeed = 20,
  onStartRoute,
  onStopRoute,
  routeActive = false,
  selectedRouteName,
}) => {
  const [speed, setSpeed] = useState<number>(currentSpeed ?? 0);
  const [internalActive, setInternalActive] = useState(routeActive);
  const active = routeActive ?? internalActive;

  useEffect(() => {
    if (currentSpeed !== undefined) setSpeed(currentSpeed);
  }, [currentSpeed]);

  const applySpeed = () => {
    const s = Math.max(0, Math.min(maxSpeed, speed));
    onSetSpeed(s);
  };

  const handleStart = () => {
    setInternalActive(true);
    onStartRoute?.();
  };
  const handleStop = () => {
    setInternalActive(false);
    onStopRoute?.();
  };

  const presets = [0, 5, 10, 15, 20].filter((p) => p <= maxSpeed);

  return (
    <Paper sx={{ p: 2, position: "relative", overflow: "hidden" }}>
      <Typography variant="h6" gutterBottom>
        Route & Throttle
      </Typography>
      <Box sx={{ display: "flex", gap: 2, flexWrap: "wrap" }}>
        {/* Start Button */}
        <Tooltip title={active ? "Rute kjører" : "Start rute"}>
          <IconButton
            onClick={handleStart}
            disabled={active}
            aria-pressed={active}
            size="large"
            sx={{
              width: 72,
              height: 72,
              borderRadius: "50%",
              bgcolor: "#1faa00",
              color: "#fff",
              boxShadow: active
                ? "0 0 10px 2px rgba(0,255,120,0.9), 0 0 24px 8px rgba(0,255,120,0.35)"
                : "0 2px 6px rgba(0,0,0,0.5)",
              animation: active
                ? "greenPulse 1.8s ease-in-out infinite"
                : "none",
              transition: "box-shadow .3s, transform .15s",
              "&:hover": {
                boxShadow: "0 0 12px 4px rgba(0,255,140,0.9)",
                transform: "translateY(-2px)",
              },
              "&.Mui-disabled": { opacity: 0.7, color: "#e0ffe0" },
            }}
          >
            <PlayArrowIcon sx={{ fontSize: 40 }} />
          </IconButton>
        </Tooltip>
        {/* Stop Button */}
        <Tooltip title={active ? "Stopp / nødstopp" : "Ingen rute aktiv"}>
          <IconButton
            onClick={handleStop}
            disabled={!active}
            aria-pressed={!active}
            size="large"
            sx={{
              width: 72,
              height: 72,
              borderRadius: "50%",
              bgcolor: "#d50000",
              color: "#fff",
              boxShadow: !active
                ? "0 2px 6px rgba(0,0,0,0.5)"
                : "0 0 8px 2px rgba(255,60,60,0.9), 0 0 18px 6px rgba(255,60,60,0.4)",
              animation: active ? "redAlert 1.2s ease-in-out infinite" : "none",
              transition: "box-shadow .3s, transform .15s",
              "&:hover": {
                boxShadow: "0 0 14px 5px rgba(255,80,80,0.95)",
                transform: "translateY(-2px)",
              },
              "&.Mui-disabled": { opacity: 0.5 },
            }}
          >
            <StopIcon sx={{ fontSize: 40 }} />
          </IconButton>
        </Tooltip>
        {/* Status */}
        <Box sx={{ flex: 1, minWidth: 160 }}>
          <Typography variant="subtitle2" sx={{ opacity: 0.8 }}>
            Status
          </Typography>
          <Typography
            variant="body2"
            sx={{ fontWeight: 500, color: active ? "#4caf50" : "#ccc" }}
          >
            {active
              ? `Kjører${selectedRouteName ? ": " + selectedRouteName : ""}`
              : "Idle"}
          </Typography>
        </Box>
      </Box>

      {/* Speed Control */}
      <Box sx={{ mt: 3 }}>
        <Typography variant="subtitle2" gutterBottom>
          Fart (knop)
        </Typography>
        <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
          <Slider
            min={0}
            max={maxSpeed}
            step={0.5}
            value={speed}
            onChange={(_, v) => setSpeed(v as number)}
            sx={{ flex: 1 }}
            valueLabelDisplay="auto"
            aria-label="Speed"
          />
          <Box
            sx={{
              width: 64,
              textAlign: "center",
              fontSize: 18,
              fontVariantNumeric: "tabular-nums",
            }}
          >
            {speed.toFixed(1)}
          </Box>
          <Button variant="contained" size="small" onClick={applySpeed}>
            Sett
          </Button>
        </Box>
        <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: "wrap" }}>
          {presets.map((p) => (
            <Chip
              key={p}
              label={p}
              clickable
              size="small"
              onClick={() => {
                setSpeed(p);
                onSetSpeed(p);
              }}
              sx={{ bgcolor: "#263238", color: "#fff" }}
            />
          ))}
          <Chip
            label="+1"
            size="small"
            onClick={() =>
              setSpeed((s) => Math.min(maxSpeed, +(s + 1).toFixed(1)))
            }
            sx={{ bgcolor: "#2e3b40", color: "#fff" }}
          />
          <Chip
            label="-1"
            size="small"
            onClick={() => setSpeed((s) => Math.max(0, +(s - 1).toFixed(1)))}
            sx={{ bgcolor: "#2e3b40", color: "#fff" }}
          />
          <Chip
            icon={<FlashOnIcon />}
            label="Apply"
            size="small"
            onClick={applySpeed}
            sx={{ bgcolor: "#1565c0", color: "#fff" }}
          />
        </Stack>
      </Box>
      <style>{`
        @keyframes greenPulse { 0%,100% { box-shadow: 0 0 8px 2px rgba(0,255,120,0.6),0 0 20px 8px rgba(0,255,120,0.25);} 50% { box-shadow:0 0 14px 4px rgba(0,255,160,0.95),0 0 30px 12px rgba(0,255,160,0.35);} }
        @keyframes redAlert { 0%,100% { box-shadow:0 0 6px 1px rgba(255,60,60,0.6),0 0 14px 6px rgba(255,60,60,0.25);} 50% { box-shadow:0 0 14px 4px rgba(255,80,80,0.95),0 0 26px 10px rgba(255,80,80,0.4);} }
      `}</style>
    </Paper>
  );
};
