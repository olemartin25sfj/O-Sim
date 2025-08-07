export interface NavigationData {
  timestamp: string;
  latitude: number;
  longitude: number;
  heading: number;
  speed: number;
  courseOverGround: number;
}

export interface EnvironmentData {
  timestamp: string;
  windSpeedKnots: number;
  windDirection: number;
  currentSpeed: number;
  currentDirection: number;
  waveHeight: number;
  waveDirection: number;
  wavePeriod: number;
}

export interface AlarmData {
  timestamp: string;
  type: string;
  message: string;
  severity: "info" | "warning" | "critical";
}

export interface WebSocketMessage<T> {
  topic: string;
  data: T;
}
