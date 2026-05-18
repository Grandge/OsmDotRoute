namespace OsmDotRoute;

/// <summary>
/// JIS X0410 地域メッシュの階層。<see cref="MeshCode.Level"/> が桁数から自動判定する。
/// Phase 1 では 8〜10 桁の 3 階層に対応（親プロジェクト「災害廃棄物処理シミュレーション」と同範囲）。
/// 11 桁（100m メッシュ）は仕様未確定のため Phase 2 以降で対応予定。
/// </summary>
public enum MeshLevel
{
    /// <summary>第3次メッシュ（約 1km 四方、8 桁、例 53394611）</summary>
    Mesh3rd,

    /// <summary>1/2 細分メッシュ（約 500m 四方、9 桁、例 533946111）</summary>
    HalfMesh,

    /// <summary>1/4 細分メッシュ（約 250m 四方、10 桁、例 5339461111）</summary>
    QuarterMesh,
}
