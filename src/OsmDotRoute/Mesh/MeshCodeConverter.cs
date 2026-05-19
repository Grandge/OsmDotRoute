using System.Globalization;
using OsmDotRoute.Geometry;

namespace OsmDotRoute.Mesh;

/// <summary>
/// JIS X0410 地域メッシュコード → 緯度経度矩形変換ユーティリティ（REQ-RST-017）。
/// </summary>
/// <remarks>
/// Phase 1 対応階層: 8 桁（第3次、1km） / 9 桁（1/2 細分、500m） / 10 桁（1/4 細分、250m）。
/// 細分メッシュの分割番号は 1〜4（南西=1、南東=2、北西=3、北東=4）。
/// 参考: 親プロジェクト `Documents/標準地域メッシュ計算方法.md`、JIS X0410。
/// </remarks>
internal static class MeshCodeConverter
{
    // 第3次メッシュのステップ幅（度）
    private const double Lat3StepDeg = 30.0 / 3600.0;   // 緯度 30 秒 = 1/120 度
    private const double Lon3StepDeg = 45.0 / 3600.0;   // 経度 45 秒 = 1/80 度
    private const double Lat2StepDeg = 5.0 / 60.0;      // 第2次メッシュ 緯度 5 分
    private const double Lon2StepDeg = 7.5 / 60.0;      // 第2次メッシュ 経度 7.5 分

    /// <summary>
    /// メッシュコードを緯度経度矩形 <see cref="Aabb"/> に変換する。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">桁数が 8〜10 の範囲外（<see cref="MeshCode.Level"/> から伝播）</exception>
    /// <exception cref="ArgumentException">桁ごとの数値範囲が不正（第2次 0〜7、細分 1〜4 等）</exception>
    public static Aabb ToBoundingBox(MeshCode meshCode)
    {
        // Level プロパティで桁数 8〜10 を検証
        var level = meshCode.Level;
        var code = meshCode.Value.ToString(CultureInfo.InvariantCulture);

        // 8 桁: 第3次メッシュ基準点を算出
        var p1 = int.Parse(code.AsSpan(0, 2), CultureInfo.InvariantCulture);
        var u1 = int.Parse(code.AsSpan(2, 2), CultureInfo.InvariantCulture);
        var p2 = code[4] - '0';
        var u2 = code[5] - '0';
        var p3 = code[6] - '0';
        var u3 = code[7] - '0';

        if (p2 is < 0 or > 7 || u2 is < 0 or > 7)
        {
            throw new ArgumentException(
                $"第2次メッシュ番号は 0〜7 の範囲が必要です（実値: p2={p2}, u2={u2}, code={code}）",
                nameof(meshCode));
        }

        var swLat = p1 / 1.5 + p2 * Lat2StepDeg + p3 * Lat3StepDeg;
        var swLon = (u1 + 100) + u2 * Lon2StepDeg + u3 * Lon3StepDeg;
        var latStep = Lat3StepDeg;
        var lonStep = Lon3StepDeg;

        // 9 桁: 1/2 細分（2×2 の南西=1, 南東=2, 北西=3, 北東=4）
        if (level >= MeshLevel.HalfMesh)
        {
            var sub1 = code[8] - '0';
            if (sub1 is < 1 or > 4)
            {
                throw new ArgumentException(
                    $"1/2 細分メッシュ番号は 1〜4 の範囲が必要です（実値: {sub1}, code={code}）",
                    nameof(meshCode));
            }
            var halfLat = (sub1 - 1) / 2;
            var halfLon = (sub1 - 1) % 2;
            swLat += halfLat * (Lat3StepDeg / 2);
            swLon += halfLon * (Lon3StepDeg / 2);
            latStep = Lat3StepDeg / 2;
            lonStep = Lon3StepDeg / 2;
        }

        // 10 桁: 1/4 細分（1/2 を 2×2 にさらに分割）
        if (level == MeshLevel.QuarterMesh)
        {
            var sub2 = code[9] - '0';
            if (sub2 is < 1 or > 4)
            {
                throw new ArgumentException(
                    $"1/4 細分メッシュ番号は 1〜4 の範囲が必要です（実値: {sub2}, code={code}）",
                    nameof(meshCode));
            }
            var qLat = (sub2 - 1) / 2;
            var qLon = (sub2 - 1) % 2;
            swLat += qLat * (Lat3StepDeg / 4);
            swLon += qLon * (Lon3StepDeg / 4);
            latStep = Lat3StepDeg / 4;
            lonStep = Lon3StepDeg / 4;
        }

        return new Aabb(
            new GeoCoordinate(swLat, swLon),
            new GeoCoordinate(swLat + latStep, swLon + lonStep));
    }

    /// <summary>
    /// 指定範囲 <paramref name="bounds"/> と交差する全メッシュコードを <paramref name="level"/> 階層で列挙する。
    /// 第3次（1km）→ 1/2 細分 → 1/4 細分の順に階層が細かい。出力順は緯度（南→北）×経度（西→東）の格子走査。
    /// </summary>
    public static IEnumerable<MeshCode> EnumerateInBounds(MapBounds bounds, MeshLevel level)
    {
        // ステップ幅と桁構成は階層毎に固定
        var (latStep, lonStep) = level switch
        {
            MeshLevel.Mesh3rd => (Lat3StepDeg, Lon3StepDeg),
            MeshLevel.HalfMesh => (Lat3StepDeg / 2, Lon3StepDeg / 2),
            MeshLevel.QuarterMesh => (Lat3StepDeg / 4, Lon3StepDeg / 4),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "未対応の MeshLevel"),
        };

        // 範囲の南西を含む基準セルのインデックスと、北東を超えない最後のセルのインデックスを整数で求める。
        // 入力 bounds の各座標はメッシュ境界に対し ±数 ULP の浮動小数誤差を持ちうるので（例: ToBounds() の結果）、
        // 境界付近では整数側にスナップする（eps = 1e-7 度 ≒ 1cm 相当、メッシュ最小幅 250m から十分小さい）。
        const double snapEps = 1e-7;

        var iLatStart = (long)Math.Floor(bounds.MinLatitude / latStep + snapEps);
        var iLatEnd = (long)Math.Ceiling(bounds.MaxLatitude / latStep - snapEps);
        var iLonStart = (long)Math.Floor(bounds.MinLongitude / lonStep + snapEps);
        var iLonEnd = (long)Math.Ceiling(bounds.MaxLongitude / lonStep - snapEps);

        for (var iLat = iLatStart; iLat < iLatEnd; iLat++)
        {
            // 各セルの中心座標で MeshCode を導出（境界ぴったりを避ける）
            var centerLat = (iLat + 0.5) * latStep;
            for (var iLon = iLonStart; iLon < iLonEnd; iLon++)
            {
                var centerLon = (iLon + 0.5) * lonStep;
                yield return ToMeshCode(centerLat, centerLon, level);
            }
        }
    }

    /// <summary>
    /// 緯度経度から指定階層のメッシュコードを算出する。<paramref name="lat"/> / <paramref name="lon"/> は
    /// 当該メッシュ矩形の内部を指す座標であること（境界ピッタリの場合は南西側のメッシュを返す）。
    /// </summary>
    public static MeshCode ToMeshCode(double lat, double lon, MeshLevel level)
    {
        // 第1次メッシュ
        var p1 = (int)Math.Floor(lat * 1.5);
        var u1 = (int)Math.Floor(lon - 100);

        // 第2次メッシュ番号（0〜7）
        var lat2Origin = p1 / 1.5;
        var lon2Origin = u1 + 100;
        var p2 = (int)Math.Floor((lat - lat2Origin) / Lat2StepDeg);
        var u2 = (int)Math.Floor((lon - lon2Origin) / Lon2StepDeg);

        // 第3次メッシュ番号（0〜9）
        var lat3Origin = lat2Origin + p2 * Lat2StepDeg;
        var lon3Origin = lon2Origin + u2 * Lon2StepDeg;
        var p3 = (int)Math.Floor((lat - lat3Origin) / Lat3StepDeg);
        var u3 = (int)Math.Floor((lon - lon3Origin) / Lon3StepDeg);

        long code = (long)p1 * 1_000_000 + (long)u1 * 10_000 + (long)p2 * 1_000 + (long)u2 * 100 + (long)p3 * 10 + u3;

        if (level == MeshLevel.Mesh3rd)
        {
            return new MeshCode(code);
        }

        // 1/2 細分: SW=1, SE=2, NW=3, NE=4
        var lat4Origin = lat3Origin + p3 * Lat3StepDeg;
        var lon4Origin = lon3Origin + u3 * Lon3StepDeg;
        var halfLat = (lat - lat4Origin) >= (Lat3StepDeg / 2) ? 1 : 0;
        var halfLon = (lon - lon4Origin) >= (Lon3StepDeg / 2) ? 1 : 0;
        var sub1 = halfLat * 2 + halfLon + 1;
        code = code * 10 + sub1;

        if (level == MeshLevel.HalfMesh)
        {
            return new MeshCode(code);
        }

        // 1/4 細分
        var halfSwLat = lat4Origin + halfLat * (Lat3StepDeg / 2);
        var halfSwLon = lon4Origin + halfLon * (Lon3StepDeg / 2);
        var qLat = (lat - halfSwLat) >= (Lat3StepDeg / 4) ? 1 : 0;
        var qLon = (lon - halfSwLon) >= (Lon3StepDeg / 4) ? 1 : 0;
        var sub2 = qLat * 2 + qLon + 1;
        code = code * 10 + sub2;

        return new MeshCode(code);
    }
}
