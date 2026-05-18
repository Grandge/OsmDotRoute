using OsmDotRoute.Routing;
using ItineroEdgeEnumerator = global::Itinero.Data.Network.RoutingNetwork.EdgeEnumerator;

namespace OsmDotRoute.Itinero;

/// <summary>
/// Itinero <see cref="ItineroEdgeEnumerator"/> をラップして <see cref="IRoadGraphEdgeEnumerator"/> を提供する。
/// </summary>
internal sealed class ItineroEdgeEnumeratorAdapter : IRoadGraphEdgeEnumerator
{
    private readonly ItineroEdgeEnumerator _inner;

    public ItineroEdgeEnumeratorAdapter(ItineroEdgeEnumerator inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc/>
    public bool MoveNext() => _inner.MoveNext();

    /// <inheritdoc/>
    public uint EdgeId => _inner.Id;

    /// <inheritdoc/>
    public uint From => _inner.From;

    /// <inheritdoc/>
    public uint To => _inner.To;

    /// <inheritdoc/>
    public ushort EdgeProfileIndex => _inner.Data.Profile;

    /// <inheritdoc/>
    public float DistanceM => _inner.Data.Distance;

    /// <inheritdoc/>
    public bool DataInverted => _inner.DataInverted;

    /// <inheritdoc/>
    public IReadOnlyList<GeoCoordinate> Shape
    {
        get
        {
            var shape = _inner.Shape;
            if (shape == null)
            {
                return Array.Empty<GeoCoordinate>();
            }

            var list = new List<GeoCoordinate>(shape.Count);
            for (int i = 0; i < shape.Count; i++)
            {
                var c = shape[i];
                list.Add(new GeoCoordinate(c.Latitude, c.Longitude));
            }

            return list;
        }
    }
}
