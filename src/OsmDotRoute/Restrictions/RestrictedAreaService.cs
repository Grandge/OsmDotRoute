namespace OsmDotRoute;

/// <summary>
/// 動的制約（進入不可エリア・難所エリア）の登録・削除・一覧取得を提供するサービス（REQ-API-004）。
/// 制約変更は次回の <see cref="Router.Calculate"/> 呼び出しから即時反映される（REQ-RST-012）。
/// </summary>
public sealed class RestrictedAreaService
{
    /// <summary>新規空のサービスを作成する。</summary>
    public RestrictedAreaService()
    {
    }

    // --- ポリゴン指定 ---

    /// <summary>ポリゴンによる進入不可エリアを登録する（REQ-RST-001）。</summary>
    public RestrictedAreaId AddBlockArea(GeoPolygon polygon, string? tag = null)
        => throw new NotImplementedException("Step 8 で実装予定");

    /// <summary>ポリゴンによる難所エリアを登録する（REQ-RST-004）。</summary>
    public RestrictedAreaId AddDifficultyArea(GeoPolygon polygon, string difficultyType, string? tag = null)
        => throw new NotImplementedException("Step 8 で実装予定");

    // --- メッシュコード指定 ---

    /// <summary>メッシュコードによる進入不可エリアを登録する（REQ-RST-002）。</summary>
    public RestrictedAreaId AddBlockArea(MeshCode meshCode, string? tag = null)
        => throw new NotImplementedException("Step 8 で実装予定");

    /// <summary>複数メッシュコードを一括で進入不可エリアとして登録する（REQ-RST-003）。</summary>
    public RestrictedAreaId AddBlockArea(IEnumerable<MeshCode> meshCodes, string? tag = null)
        => throw new NotImplementedException("Step 8 で実装予定");

    /// <summary>メッシュコードによる難所エリアを登録する（REQ-RST-005）。</summary>
    public RestrictedAreaId AddDifficultyArea(MeshCode meshCode, string difficultyType, string? tag = null)
        => throw new NotImplementedException("Step 8 で実装予定");

    /// <summary>複数メッシュコードを一括で難所エリアとして登録する（REQ-RST-006）。</summary>
    public RestrictedAreaId AddDifficultyArea(IEnumerable<MeshCode> meshCodes, string difficultyType, string? tag = null)
        => throw new NotImplementedException("Step 8 で実装予定");

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

    /// <summary>指定 ID の制約を削除する（REQ-RST-008）。</summary>
    public void Remove(RestrictedAreaId id)
        => throw new NotImplementedException("Step 8 で実装予定");

    /// <summary>指定タグの制約を一括削除する（REQ-RST-010）。</summary>
    public void RemoveByTag(string tag)
        => throw new NotImplementedException("Step 8 で実装予定");

    /// <summary>全制約を一括クリアする（REQ-RST-009）。</summary>
    public void ClearAll()
        => throw new NotImplementedException("Step 8 で実装予定");

    // --- 一覧 ---

    /// <summary>登録済み制約の一覧を読み取り専用ビューで取得する（REQ-RST-011）。</summary>
    public IReadOnlyList<RestrictedArea> ListAll()
        => throw new NotImplementedException("Step 8 で実装予定");
}
