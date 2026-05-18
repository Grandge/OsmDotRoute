namespace OsmDotRoute;

/// <summary>
/// JIS X0410 地域メッシュの階層。<see cref="MeshCode.Level"/> が桁数から自動判定する。
/// </summary>
public enum MeshLevel
{
    /// <summary>第3次メッシュ（約 1km 四方、8 桁、例 53394547）</summary>
    Mesh3rd,

    /// <summary>1/2 細分メッシュ（約 500m 四方、9 桁、例 533945471）</summary>
    HalfMesh,

    /// <summary>1/4 細分メッシュ（約 250m 四方、10 桁、例 5339454713）</summary>
    QuarterMesh,

    /// <summary>1/10 細分メッシュ（約 100m 四方、11 桁、例 53394547135）</summary>
    TenthMesh,
}
