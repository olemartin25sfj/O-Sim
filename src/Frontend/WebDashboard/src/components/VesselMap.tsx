import { useEffect } from "react";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import { DivIcon } from "leaflet";
import "leaflet/dist/leaflet.css";
import { NavigationData } from "../types/messages";
import "./VesselMap.css";

interface VesselMapProps {
  navigation: NavigationData | null;
}

// Component to update map center when navigation changes
const MapUpdater: React.FC<{ navigation: NavigationData }> = ({
  navigation,
}) => {
  const map = useMap();

  useEffect(() => {
    map.setView([navigation.latitude, navigation.longitude]);
  }, [map, navigation.latitude, navigation.longitude]);

  return null;
};

const createShipIcon = (heading: number) =>
  new DivIcon({
    className: "vessel-icon",
    html: `<div style="transform: rotate(${heading}deg);">
           <img src="/ship-icon.svg" width="32" height="32" style="transform: translate(-16px, -16px);" />
         </div>`,
    iconSize: [32, 32],
  });

export const VesselMap: React.FC<VesselMapProps> = ({ navigation }) => {
  if (!navigation) return null;

  return (
    <MapContainer
      center={[navigation.latitude, navigation.longitude]}
      zoom={13}
      style={{ height: "100%", width: "100%" }}
    >
      <MapUpdater navigation={navigation} />
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      <Marker
        position={[navigation.latitude, navigation.longitude]}
        icon={createShipIcon(navigation.heading)}
      >
        <Popup>
          <div>
            <h3>Vessel Position</h3>
            <p>Lat: {navigation.latitude.toFixed(5)}</p>
            <p>Lon: {navigation.longitude.toFixed(5)}</p>
            <p>Heading: {navigation.heading.toFixed(1)}°</p>
            <p>Speed: {navigation.speed.toFixed(1)} knots</p>
          </div>
        </Popup>
      </Marker>
    </MapContainer>
  );
};
