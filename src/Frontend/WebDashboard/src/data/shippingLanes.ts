// Shipping lanes along the Norwegian coast
export const norwegianShippingLanes = [
  {
    name: "Bergen - Stavanger",
    route: [
      // Bergen
      [60.391262, 5.322054] as [number, number],
      // Via Korsfjorden
      [60.147064, 5.180649] as [number, number],
      // Via Selbjørnsfjorden
      [59.923543, 5.086021] as [number, number],
      // Via Sletta
      [59.418288, 5.243435] as [number, number],
      // Stavanger
      [58.9747, 5.73] as [number, number],
    ],
  },
  {
    name: "Bergen - Oslo",
    route: [
      // Bergen
      [60.391262, 5.322054] as [number, number],
      // Via Korsfjorden
      [60.147064, 5.180649] as [number, number],
      // South of Sotra
      [60.123456, 4.987654] as [number, number],
      // Via Skagerrak
      [58.123456, 8.987654] as [number, number],
      // Via Oslofjorden
      [59.123456, 10.56789] as [number, number],
      // Oslo
      [59.904667, 10.748197] as [number, number],
    ],
  },
  {
    name: "Bergen - Ålesund",
    route: [
      // Bergen
      [60.391262, 5.322054] as [number, number],
      // Via Hjeltefjorden
      [60.524722, 4.893889] as [number, number],
      // Via Sognefjorden entrance
      [61.083333, 4.866667] as [number, number],
      // Via Stadthavet
      [62.15, 5.116667] as [number, number],
      // Ålesund
      [62.472228, 6.149722] as [number, number],
    ],
  },
];

// International routes from/to Norway
export const internationalShippingLanes = [
  {
    name: "Bergen - Aberdeen",
    route: [
      // Bergen
      [60.391262, 5.322054] as [number, number],
      // Via North Sea
      [60.0, 3.0] as [number, number],
      [59.5, 1.0] as [number, number],
      // Aberdeen
      [57.144164, -2.114048] as [number, number],
    ],
  },
  {
    name: "Bergen - Hamburg",
    route: [
      // Bergen
      [60.391262, 5.322054] as [number, number],
      // Via North Sea
      [58.0, 5.0] as [number, number],
      [57.0, 7.0] as [number, number],
      // Via Elbe
      [54.0, 8.0] as [number, number],
      // Hamburg
      [53.551086, 9.993682] as [number, number],
    ],
  },
];

interface ShippingRoute {
  name: string;
  route: [number, number][];
}

// Helper function to get route by name
export const getRouteByName = (name: string): ShippingRoute | undefined => {
  const allRoutes = [...norwegianShippingLanes, ...internationalShippingLanes];
  return allRoutes.find((route) => route.name === name);
};

// Helper function to get nearest route point
export const findNearestRoutePoint = (
  position: [number, number]
): {
  point: [number, number] | null;
  distance: number;
  routeName: string;
} => {
  const allRoutes = [...norwegianShippingLanes, ...internationalShippingLanes];
  let nearestPoint: [number, number] | null = null;
  let nearestDistance = Infinity;
  let routeName = "";

  allRoutes.forEach((route) => {
    route.route.forEach((point) => {
      const distance = calculateDistance(position, point);
      if (distance < nearestDistance) {
        nearestDistance = distance;
        nearestPoint = point;
        routeName = route.name;
      }
    });
  });

  return { point: nearestPoint, distance: nearestDistance, routeName };
};

// Calculate distance between two points in kilometers using the Haversine formula
function calculateDistance(
  point1: [number, number],
  point2: [number, number]
): number {
  const [lat1, lon1] = point1;
  const [lat2, lon2] = point2;

  const R = 6371; // Earth's radius in kilometers
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);

  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(toRad(lat1)) *
      Math.cos(toRad(lat2)) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);

  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
}

function toRad(degrees: number): number {
  return degrees * (Math.PI / 180);
}
