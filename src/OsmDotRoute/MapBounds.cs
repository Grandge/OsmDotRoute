namespace OsmDotRoute;

/// <summary>
/// 緯度経度の矩形範囲（南西端と北東端で表現）。
/// 動的制約 GML 入力時のマップ範囲フィルタ（REQ-RST-040）等に使用する公開値型。
/// </summary>
/// <param name="SouthWest">南西端（最小緯度・最小経度）</param>
/// <param name="NorthEast">北東端（最大緯度・最大経度）</param>
public readonly record struct MapBounds(GeoCoordinate SouthWest, GeoCoordinate NorthEast)
{
    /// <summary>南端緯度</summary>
    public double MinLatitude => SouthWest.Latitude;

    /// <summary>北端緯度</summary>
    public double MaxLatitude => NorthEast.Latitude;

    /// <summary>西端経度</summary>
    public double MinLongitude => SouthWest.Longitude;

    /// <summary>東端経度</summary>
    public double MaxLongitude => NorthEast.Longitude;

    /// <summary>
    /// 指定座標が本範囲内（境界線上を含む）にあるかを判定する。
    /// </summary>
    /// <param name="coordinate">判定対象の緯度経度</param>
    /// <returns>範囲内（境界含む）なら <c>true</c></returns>
    public bool Contains(GeoCoordinate coordinate)
    {
        return coordinate.Latitude >= SouthWest.Latitude
            && coordinate.Latitude <= NorthEast.Latitude
            && coordinate.Longitude >= SouthWest.Longitude
            && coordinate.Longitude <= NorthEast.Longitude;
    }
}
