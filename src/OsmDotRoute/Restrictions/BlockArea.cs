namespace OsmDotRoute;

/// <summary>
/// 進入不可エリア（REQ-RST-001〜003）。
/// ポリゴンまたはメッシュコードのいずれかで領域を定義する。
/// </summary>
public sealed class BlockArea : RestrictedArea
{
    /// <summary>ポリゴンで定義される進入不可エリアを作成する（REQ-RST-001）。</summary>
    public BlockArea(RestrictedAreaId id, GeoPolygon polygon, string? tag = null)
        : base(id, tag)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        Polygon = polygon;
    }

    /// <summary>メッシュコードで定義される進入不可エリアを作成する（REQ-RST-002）。</summary>
    public BlockArea(RestrictedAreaId id, MeshCode meshCode, string? tag = null)
        : base(id, tag)
    {
        MeshCode = meshCode;
    }

    /// <summary>ポリゴン領域。メッシュ指定で作成された場合は <c>null</c>。</summary>
    public GeoPolygon? Polygon { get; }

    /// <summary>メッシュコード。ポリゴン指定で作成された場合は <c>null</c>。</summary>
    public MeshCode? MeshCode { get; }
}
