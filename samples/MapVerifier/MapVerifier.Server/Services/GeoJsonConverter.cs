using MapVerifier.Server.Contracts;
using OsmRoute = OsmDotRoute.Route;

namespace MapVerifier.Server.Services;

/// <summary>
/// <see cref="OsmRoute"/> を GeoJSON LineString に変換する。REQ-FMT-004 廃止により本ライブラリ機能を使わず、
/// MapVerifier 内部ユーティリティとして実装する。
/// </summary>
public static class GeoJsonConverter
{
    public static GeoJsonLineString ToLineString(OsmRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var shape = route.Shape.Span;
        var coords = new double[shape.Length][];
        for (var i = 0; i < shape.Length; i++)
        {
            var c = shape[i];
            coords[i] = new[] { c.Longitude, c.Latitude };
        }
        return new GeoJsonLineString("LineString", coords);
    }
}
