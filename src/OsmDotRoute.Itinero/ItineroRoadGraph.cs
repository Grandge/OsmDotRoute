using OsmDotRoute.Geometry;
using OsmDotRoute.Routing;
using ItineroDb = global::Itinero.RouterDb;

namespace OsmDotRoute.Itinero;

/// <summary>
/// Itinero <see cref="ItineroDb"/> をラップする <see cref="IRoadGraph"/> 実装（Phase 1）。
/// Phase 2 で独自バイナリグラフ実装に置き換わるため、このクラスは Phase 2 で破棄予定。
/// </summary>
internal sealed class ItineroRoadGraph : IRoadGraph
{
    private readonly ItineroDb _routerDb;

    /// <summary>Itinero RouterDb をラップして道路グラフを作成する。</summary>
    public ItineroRoadGraph(ItineroDb routerDb)
    {
        ArgumentNullException.ThrowIfNull(routerDb);
        _routerDb = routerDb;
    }

    /// <inheritdoc/>
    public uint VertexCount => _routerDb.Network.VertexCount;

    /// <inheritdoc/>
    public long EdgeCount => _routerDb.Network.EdgeCount;

    /// <inheritdoc/>
    public GeoCoordinate GetVertex(uint vertexId)
    {
        var c = _routerDb.Network.GetVertex(vertexId);
        return new GeoCoordinate(c.Latitude, c.Longitude);
    }

    /// <inheritdoc/>
    public IRoadGraphEdgeEnumerator GetEdgeEnumerator(uint vertexId)
        => new ItineroEdgeEnumeratorAdapter(_routerDb.Network.GetEdgeEnumerator(vertexId));

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> GetEdgeOsmTags(ushort edgeProfileIndex)
    {
        var attrs = _routerDb.EdgeProfiles.Get(edgeProfileIndex);
        if (attrs == null)
        {
            return EmptyTagDictionary;
        }

        var dict = new Dictionary<string, string>(attrs.Count, StringComparer.Ordinal);
        foreach (var attr in attrs)
        {
            dict[attr.Key] = attr.Value;
        }

        return dict;
    }

    /// <inheritdoc/>
    public GeoBounds GetBounds()
    {
        var network = _routerDb.Network;
        if (network.VertexCount == 0)
        {
            return new GeoBounds(default, default);
        }

        float minLat = float.MaxValue;
        float maxLat = float.MinValue;
        float minLon = float.MaxValue;
        float maxLon = float.MinValue;

        for (uint i = 0; i < network.VertexCount; i++)
        {
            var c = network.GetVertex(i);
            if (c.Latitude < minLat) minLat = c.Latitude;
            if (c.Latitude > maxLat) maxLat = c.Latitude;
            if (c.Longitude < minLon) minLon = c.Longitude;
            if (c.Longitude > maxLon) maxLon = c.Longitude;
        }

        return new GeoBounds(
            new GeoCoordinate(minLat, minLon),
            new GeoCoordinate(maxLat, maxLon));
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyTagDictionary
        = new Dictionary<string, string>(0, StringComparer.Ordinal);
}
