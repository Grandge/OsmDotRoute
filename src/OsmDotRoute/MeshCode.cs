using System.Globalization;

namespace OsmDotRoute;

/// <summary>
/// JIS X0410 地域メッシュコード。第3次（1km、8桁）〜1/10 細分（100m、11桁）に対応。
/// 桁数で <see cref="MeshLevel"/> が自動判定される。
/// </summary>
/// <param name="Value">メッシュコード数値（8〜11 桁）</param>
public readonly record struct MeshCode(long Value)
{
    /// <summary>
    /// メッシュ階層を桁数から判定する。
    /// 桁数が 8〜11 桁の範囲外の場合は <see cref="ArgumentOutOfRangeException"/> をスローする。
    /// </summary>
    public MeshLevel Level => Value switch
    {
        >= 10_000_000L and < 100_000_000L => MeshLevel.Mesh3rd,
        >= 100_000_000L and < 1_000_000_000L => MeshLevel.HalfMesh,
        >= 1_000_000_000L and < 10_000_000_000L => MeshLevel.QuarterMesh,
        >= 10_000_000_000L and < 100_000_000_000L => MeshLevel.TenthMesh,
        _ => throw new ArgumentOutOfRangeException(
            nameof(Value), Value,
            "メッシュコードは 8〜11 桁の数値である必要があります（REQ-RST-018）"),
    };

    /// <summary>メッシュコードを数値文字列で表現する。</summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
