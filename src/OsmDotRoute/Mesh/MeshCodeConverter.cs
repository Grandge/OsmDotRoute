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
}
