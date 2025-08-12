// Synkronisert med OSim.Shared.Messages records
export interface NavigationData {
  timestampUtc: string; // ISO-8601
  latitude: number;
  longitude: number;
  headingDegrees: number;
  speedKnots: number;
  courseOverGroundDegrees: number;
}

export interface EnvironmentData {
  timestampUtc: string;
  mode: "Static" | "Dynamic";
  windSpeedKnots: number;
  windDirectionDegrees: number;
  currentSpeedKnots: number;
  currentDirectionDegrees: number;
  waveHeightMeters: number;
  waveDirectionDegrees: number;
  wavePeriodSeconds: number;
}

export interface AlarmData {
  timestampUtc: string;
  alarmType: string;
  message: string;
  severity: "Info" | "Warning" | "Critical";
}

export interface WebSocketMessage<T> {
  topic: string;
  data: T;
}

// Pollet destinasjonsstatus fra /api/simulator/destination
export interface DestinationStatus {
  hasDestination: boolean;
  targetLatitude?: number;
  targetLongitude?: number;
  distanceNm?: number;
  etaMinutes?: number;
  hasArrived?: boolean;
}
