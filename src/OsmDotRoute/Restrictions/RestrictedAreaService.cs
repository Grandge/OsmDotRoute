using OsmDotRoute.Geometry;
using OsmDotRoute.Mesh;

namespace OsmDotRoute;

/// <summary>
/// 動的制約（進入不可エリア・難所エリア）の登録・削除・一覧取得を提供するサービス（REQ-API-004）。
/// 制約変更は次回の <see cref="Router.Calculate"/> 呼び出しから即時反映される（REQ-RST-012）。
/// </summary>
public sealed class RestrictedAreaService
{
    private readonly Dictionary<RestrictedAreaId, AreaEntry> _entries = new();
    private readonly SpatialIndex<ShapeRef> _index = new();

    /// <summary>新規空のサービスを作成する。</summary>
    public RestrictedAreaService()
    {
    }

    // --- ポリゴン指定 ---

    /// <summary>ポリゴンによる進入不可エリアを登録する（REQ-RST-001）。</summary>
    public RestrictedAreaId AddBlockArea(GeoPolygon polygon, string? tag = null)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        var id = RestrictedAreaId.New();
        var area = new BlockArea(id, polygon, tag);
        var shapes = new[] { Shape.FromPolygon(polygon) };
        Register(id, area, shapes);
        return id;
    }

    /// <summary>ポリゴンによる難所エリアを登録する（REQ-RST-004）。</summary>
    public RestrictedAreaId AddDifficultyArea(GeoPolygon polygon, string difficultyType, string? tag = null)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        var id = RestrictedAreaId.New();
        var area = new DifficultyArea(id, polygon, difficultyType, tag);
        var shapes = new[] { Shape.FromPolygon(polygon) };
        Register(id, area, shapes);
        return id;
    }

    // --- メッシュコード指定 ---

    /// <summary>メッシュコードによる進入不可エリアを登録する（REQ-RST-002）。</summary>
    public RestrictedAreaId AddBlockArea(MeshCode meshCode, string? tag = null)
    {
        var id = RestrictedAreaId.New();
        var area = new BlockArea(id, meshCode, tag);
        var shapes = new[] { Shape.FromMesh(meshCode) };
        Register(id, area, shapes);
        return id;
    }

    /// <summary>複数メッシュコードを一括で進入不可エリアとして登録する（REQ-RST-003）。</summary>
    public RestrictedAreaId AddBlockArea(IEnumerable<MeshCode> meshCodes, string? tag = null)
    {
        ArgumentNullException.ThrowIfNull(meshCodes);
        var id = RestrictedAreaId.New();
        var area = new BlockArea(id, meshCodes, tag);
        var shapes = area.MeshCodes!.Select(Shape.FromMesh).ToArray();
        Register(id, area, shapes);
        return id;
    }

    /// <summary>メッシュコードによる難所エリアを登録する（REQ-RST-005）。</summary>
    public RestrictedAreaId AddDifficultyArea(MeshCode meshCode, string difficultyType, string? tag = null)
    {
        var id = RestrictedAreaId.New();
        var area = new DifficultyArea(id, meshCode, difficultyType, tag);
        var shapes = new[] { Shape.FromMesh(meshCode) };
        Register(id, area, shapes);
        return id;
    }

    /// <summary>複数メッシュコードを一括で難所エリアとして登録する（REQ-RST-006）。</summary>
    public RestrictedAreaId AddDifficultyArea(IEnumerable<MeshCode> meshCodes, string difficultyType, string? tag = null)
    {
        ArgumentNullException.ThrowIfNull(meshCodes);
        var id = RestrictedAreaId.New();
        var area = new DifficultyArea(id, meshCodes, difficultyType, tag);
        var shapes = area.MeshCodes!.Select(Shape.FromMesh).ToArray();
        Register(id, area, shapes);
        return id;
    }

    // --- GeoJSON 入力 ---

    /// <summary>GeoJSON 文字列から複数制約を一括登録する（REQ-RST-023〜025）。</summary>
    public RestrictedAreaId[] AddFromGeoJson(string geoJson, string? defaultTag = null)
        => throw new NotImplementedException("Step 10 で実装予定");

    /// <summary>GeoJSON ファイルから複数制約を一括登録する（REQ-RST-024）。</summary>
    public RestrictedAreaId[] AddFromGeoJsonFile(string filePath, string? defaultTag = null)
        => throw new NotImplementedException("Step 10 で実装予定");

    /// <summary>GeoJSON Stream から複数制約を一括登録する（REQ-RST-025）。</summary>
    public RestrictedAreaId[] AddFromGeoJsonStream(Stream stream, string? defaultTag = null)
        => throw new NotImplementedException("Step 10 で実装予定");

    // --- 削除 ---

    /// <summary>指定 ID の制約を削除する（REQ-RST-008）。存在しない ID は何もしない。</summary>
    public void Remove(RestrictedAreaId id)
    {
        if (_entries.Remove(id))
        {
            _index.RemoveAll(s => s.Id.Equals(id));
        }
    }

    /// <summary>指定タグの制約を一括削除する（REQ-RST-010）。<c>null</c> 渡しは例外。</summary>
    public void RemoveByTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var targets = _entries
            .Where(kv => string.Equals(kv.Value.Area.Tag, tag, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToArray();
        foreach (var id in targets)
        {
            _entries.Remove(id);
        }
        if (targets.Length > 0)
        {
            var idSet = new HashSet<RestrictedAreaId>(targets);
            _index.RemoveAll(s => idSet.Contains(s.Id));
        }
    }

    /// <summary>全制約を一括クリアする（REQ-RST-009）。</summary>
    public void ClearAll()
    {
        _entries.Clear();
        _index.Clear();
    }

    // --- 一覧 ---

    /// <summary>登録済み制約の一覧を読み取り専用ビューで取得する（REQ-RST-011）。</summary>
    public IReadOnlyList<RestrictedArea> ListAll()
    {
        return _entries.Values.Select(e => e.Area).ToArray();
    }

    // --- internal クエリ API（Step 9 の EdgeWeightCalculator から使用） ---

    /// <summary>線分と AABB が交差する候補制約（重複除去済み）を返す。</summary>
    internal IEnumerable<RestrictedArea> QueryCandidates(GeoCoordinate p1, GeoCoordinate p2)
    {
        var seen = new HashSet<RestrictedAreaId>();
        foreach (var s in _index.Query(p1, p2))
        {
            if (seen.Add(s.Id))
            {
                yield return s.Area;
            }
        }
    }

    /// <summary>クエリ AABB と交差する候補制約（重複除去済み）を返す。</summary>
    internal IEnumerable<RestrictedArea> QueryCandidates(Aabb queryBounds)
    {
        var seen = new HashSet<RestrictedAreaId>();
        foreach (var s in _index.Query(queryBounds))
        {
            if (seen.Add(s.Id))
            {
                yield return s.Area;
            }
        }
    }

    /// <summary>指定 ID の制約が登録済みかを返す（テスト・診断用）。</summary>
    internal bool Contains(RestrictedAreaId id) => _entries.ContainsKey(id);

    private void Register(RestrictedAreaId id, RestrictedArea area, Shape[] shapes)
    {
        var entry = new AreaEntry(area, shapes);
        _entries[id] = entry;
        foreach (var s in shapes)
        {
            _index.Add(s.Bounds, new ShapeRef(id, area, s));
        }
    }

    private sealed record AreaEntry(RestrictedArea Area, Shape[] Shapes);

    /// <summary>
    /// 1 つの制約 ID 内に複数存在しうる空間形状の単位（ポリゴン本体 1 つ、またはメッシュ AABB 1 つ）。
    /// </summary>
    private readonly record struct Shape(Aabb Bounds, GeoPolygon? Polygon)
    {
        public bool IsMesh => Polygon is null;

        public static Shape FromPolygon(GeoPolygon polygon)
            => new(PolygonIntersection.ComputeBoundingBox(polygon), polygon);

        public static Shape FromMesh(MeshCode meshCode)
            => new(MeshCodeConverter.ToBoundingBox(meshCode), null);
    }

    private readonly record struct ShapeRef(RestrictedAreaId Id, RestrictedArea Area, Shape Shape);
}
