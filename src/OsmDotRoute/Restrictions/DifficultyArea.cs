namespace OsmDotRoute;

/// <summary>
/// 難所エリア（REQ-RST-004〜007）。
/// 「客観的事実（道路状況種別）」のみを保持し、速度低下係数・通行可否は <see cref="VehicleProfile"/> 側で規定される。
/// 組込みタイプは <see cref="DifficultyTypes"/> を参照。ユーザー定義タイプも使用可（REQ-PRF-013）。
/// </summary>
public sealed class DifficultyArea : RestrictedArea
{
    /// <summary>ポリゴンで定義される難所エリアを作成する（REQ-RST-004）。</summary>
    /// <param name="id">エリア ID</param>
    /// <param name="polygon">ポリゴン領域</param>
    /// <param name="difficultyType">難所タイプ文字列（組込み 8 種または任意ユーザー定義キー）</param>
    /// <param name="tag">タグ文字列。null 可</param>
    public DifficultyArea(RestrictedAreaId id, GeoPolygon polygon, string difficultyType, string? tag = null)
        : base(id, tag)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        ValidateDifficultyType(difficultyType);
        Polygon = polygon;
        DifficultyType = difficultyType;
    }

    /// <summary>メッシュコードで定義される難所エリアを作成する（REQ-RST-005）。</summary>
    /// <param name="id">エリア ID</param>
    /// <param name="meshCode">メッシュコード</param>
    /// <param name="difficultyType">難所タイプ文字列</param>
    /// <param name="tag">タグ文字列。null 可</param>
    public DifficultyArea(RestrictedAreaId id, MeshCode meshCode, string difficultyType, string? tag = null)
        : base(id, tag)
    {
        ValidateDifficultyType(difficultyType);
        MeshCode = meshCode;
        DifficultyType = difficultyType;
    }

    /// <summary>ポリゴン領域。メッシュ指定で作成された場合は <c>null</c>。</summary>
    public GeoPolygon? Polygon { get; }

    /// <summary>メッシュコード。ポリゴン指定で作成された場合は <c>null</c>。</summary>
    public MeshCode? MeshCode { get; }

    /// <summary>
    /// 難所タイプ文字列。プロファイルが反応（速度係数・通行可否）を決定する（REQ-PRF-011）。
    /// </summary>
    public string DifficultyType { get; }

    private static void ValidateDifficultyType(string difficultyType)
    {
        if (string.IsNullOrWhiteSpace(difficultyType))
        {
            throw new ArgumentException(
                "難所タイプ文字列は空文字または null にできません（REQ-RST-007）。",
                nameof(difficultyType));
        }
    }
}
