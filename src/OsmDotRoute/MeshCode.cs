using System.Globalization;
using OsmDotRoute.Mesh;

namespace OsmDotRoute;

/// <summary>
/// JIS X0410 地域メッシュコード。第3次（1km、8 桁）〜 1/4 細分（250m、10 桁）に対応。
/// 桁数で <see cref="MeshLevel"/> が自動判定される。
/// </summary>
/// <remarks>
/// Phase 1 では 8〜10 桁の 3 階層のみ対応（親プロジェクト「災害廃棄物処理シミュレーション」と同範囲）。
/// 11 桁（1/10 細分 = 100m）は仕様未確定のため Phase 2 以降で対応予定（REQ-RST-016 参照）。
/// </remarks>
/// <param name="Value">メッシュコード数値（8〜10 桁）</param>
public readonly record struct MeshCode(long Value)
{
    /// <summary>
    /// メッシュ階層を桁数から判定する（REQ-RST-017）。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">桁数が 8〜10 桁の範囲外（REQ-RST-018）</exception>
    public MeshLevel Level => Value switch
    {
        >= 10_000_000L and < 100_000_000L => MeshLevel.Mesh3rd,
        >= 100_000_000L and < 1_000_000_000L => MeshLevel.HalfMesh,
        >= 1_000_000_000L and < 10_000_000_000L => MeshLevel.QuarterMesh,
        _ => throw new ArgumentOutOfRangeException(
            nameof(Value), Value,
            "メッシュコードは 8〜10 桁の数値である必要があります（REQ-RST-018、Phase 1 対応範囲）"),
    };

    /// <summary>メッシュコードを数値文字列で表現する。</summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// メッシュコードを表す緯度経度矩形範囲を返す（REQ-RST-017）。
    /// </summary>
    /// <returns>南西端・北東端で表現される矩形範囲</returns>
    /// <exception cref="ArgumentOutOfRangeException">桁数が 8〜10 桁の範囲外（REQ-RST-018）</exception>
    /// <exception cref="ArgumentException">桁ごとの数値範囲が不正（第2次 0〜7、細分 1〜4 等）</exception>
    public MapBounds ToBounds()
    {
        var aabb = MeshCodeConverter.ToBoundingBox(this);
        return new MapBounds(aabb.SouthWest, aabb.NorthEast);
    }

    /// <summary>
    /// 指定範囲 <paramref name="bounds"/> と交差する全メッシュコードを <paramref name="level"/> 階層で列挙する。
    /// 範囲外にはみ出すメッシュも、矩形が <paramref name="bounds"/> と交差する限り含まれる（境界線上を含む）。
    /// </summary>
    /// <param name="bounds">列挙対象の緯度経度範囲</param>
    /// <param name="level">列挙するメッシュ階層</param>
    /// <returns>南西から北東に向かう走査順のメッシュコード列</returns>
    public static IEnumerable<MeshCode> EnumerateInBounds(MapBounds bounds, MeshLevel level)
    {
        return MeshCodeConverter.EnumerateInBounds(bounds, level);
    }
}
