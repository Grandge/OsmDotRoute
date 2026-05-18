namespace OsmDotRoute;

/// <summary>
/// 経路計算結果（REQ-FMT-001〜003）。
/// 総距離・総所要時間・経路形状を保持する。
/// </summary>
public sealed class Route
{
    /// <summary>
    /// 経路を作成する。
    /// </summary>
    /// <param name="totalDistanceM">総距離（メートル）</param>
    /// <param name="totalDurationSec">総所要時間（秒）</param>
    /// <param name="shape">経路形状（緯度経度頂点列、起点から終点まで）</param>
    public Route(double totalDistanceM, double totalDurationSec, IReadOnlyList<GeoCoordinate> shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        TotalDistanceM = totalDistanceM;
        TotalDurationSec = totalDurationSec;
        Shape = shape;
    }

    /// <summary>総距離（メートル）</summary>
    public double TotalDistanceM { get; }

    /// <summary>総所要時間（秒）</summary>
    public double TotalDurationSec { get; }

    /// <summary>経路形状（起点から終点までの緯度経度頂点列、シェイプ込み）</summary>
    public IReadOnlyList<GeoCoordinate> Shape { get; }
}
