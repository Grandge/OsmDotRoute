using System.Globalization;

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
}
