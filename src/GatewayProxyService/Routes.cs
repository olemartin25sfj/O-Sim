using System;
using System.Linq;

namespace GatewayProxyService;

internal record RouteRaw(string id, string name, string category, double[][] points);

internal record RouteMeta(
    string Id,
    string Name,
    string Category,
    double MinLat,
    double MinLon,
    double MaxLat,
    double MaxLon,
    double LengthNm,
    double[][] PreviewPoints
);

internal record RouteDetail(
    string Id,
    string Name,
    string Category,
    double MinLat,
    double MinLon,
    double MaxLat,
    double MaxLon,
    double LengthNm,
    double[][] Points
);

internal static class RouteComputed
{
    public static (double minLat, double minLon, double maxLat, double maxLon) Bounds(double[][] pts)
    {
        double minLat = pts.Min(p => p[0]);
        double maxLat = pts.Max(p => p[0]);
        double minLon = pts.Min(p => p[1]);
        double maxLon = pts.Max(p => p[1]);
        return (minLat, minLon, maxLat, maxLon);
    }

    public static double LengthNm(double[][] pts)
    {
        double totalKm = 0;
        for (int i = 1; i < pts.Length; i++)
        {
            totalKm += HaversineKm(pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1]);
        }
        return totalKm * 0.539957; // km -> NM
    }

    public static (RouteMeta meta, RouteDetail detail) From(RouteRaw raw)
    {
        var pts = raw.points;
        var (minLat, minLon, maxLat, maxLon) = Bounds(pts);
        var lengthNm = LengthNm(pts);
        var preview = pts; // small dataset, no simplification yet
        var meta = new RouteMeta(raw.id, raw.name, raw.category, minLat, minLon, maxLat, maxLon, Math.Round(lengthNm, 1), preview);
        var detail = new RouteDetail(raw.id, raw.name, raw.category, minLat, minLon, maxLat, maxLon, Math.Round(lengthNm, 1), pts);
        return (meta, detail);
    }

    static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // km
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    static double ToRad(double deg) => deg * Math.PI / 180d;
}
