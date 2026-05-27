namespace OsmDotRoute;

/// <summary>
/// 経路計算結果（REQ-FMT-001〜003）。
/// 総距離・総所要時間・経路形状を保持する。
/// </summary>
/// <remarks>
/// Phase 3 ステップ 3C.1（Phase 2 §5.5-8 確定）で <see cref="Shape"/> 型を
/// <c>IReadOnlyList&lt;GeoCoordinate&gt;</c> から <see cref="ReadOnlyMemory{T}"/> に
/// 破壊変更した。反復には <c>route.Shape.Span</c>、要素アクセスには <c>route.Shape.Span[i]</c>、
/// 要素数取得には <c>route.Shape.Length</c> を用いる。
/// </remarks>
public sealed class Route
{
    /// <summary>
    /// 経路を作成する。
    /// </summary>
    /// <param name="totalDistanceM">総距離（メートル）</param>
    /// <param name="totalDurationSec">総所要時間（秒）</param>
    /// <param name="shape">経路形状（緯度経度頂点列、起点から終点まで）</param>
    public Route(double totalDistanceM, double totalDurationSec, ReadOnlyMemory<GeoCoordinate> shape)
    {
        TotalDistanceM = totalDistanceM;
        TotalDurationSec = totalDurationSec;
        Shape = shape;
    }

    /// <summary>総距離（メートル）</summary>
    public double TotalDistanceM { get; }

    /// <summary>総所要時間（秒）</summary>
    public double TotalDurationSec { get; }

    /// <summary>
    /// 経路形状（起点から終点までの緯度経度頂点列、シェイプ込み）。
    /// 反復には <c>Shape.Span</c>、要素数取得には <c>Shape.Length</c> を用いる（Phase 2 §5.5-8、Phase 3 3C.1 破壊変更）。
    /// </summary>
    public ReadOnlyMemory<GeoCoordinate> Shape { get; }
}
