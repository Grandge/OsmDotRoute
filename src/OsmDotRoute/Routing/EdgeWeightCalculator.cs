using OsmDotRoute.Profiles;

namespace OsmDotRoute.Routing;

/// <summary>
/// エッジ重み計算器。プロファイル評価結果からエッジの所要時間（秒）と方向別通行可否を算出する。
/// </summary>
/// <remarks>
/// <para>重み = 距離 (m) / 速度 (m/s)。プロファイル評価で通行不可（<c>CanPass=false</c>）または速度 0 のエッジは無限大扱い。</para>
/// <para>
/// 方向解釈: エニュメレータの <c>From → To</c> 方向を「順方向」と呼ぶ。
/// <c>DataInverted=false</c> なら順方向 = OSM デジタイズ方向、
/// <c>DataInverted=true</c> なら順方向 = OSM デジタイズ方向の逆。
/// この変換を <see cref="CanTraverseInEnumeratorDirection"/> に閉じ込めている。
/// </para>
/// </remarks>
internal sealed class EdgeWeightCalculator
{
    private readonly IRoadGraph _graph;
    private readonly ProfileEvaluator _evaluator;

    public EdgeWeightCalculator(IRoadGraph graph, ProfileEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(evaluator);
        _graph = graph;
        _evaluator = evaluator;
    }

    /// <summary>エッジプロファイル index から評価結果（通行可否・速度・方向制限）を取得する。</summary>
    public EdgeEvaluation Evaluate(ushort edgeProfileIndex)
    {
        var tags = _graph.GetEdgeOsmTags(edgeProfileIndex);
        return _evaluator.Evaluate(tags);
    }

    /// <summary>距離 (m) と速度 (km/h) から所要時間 (秒) を算出する。速度が 0 以下なら <see cref="double.PositiveInfinity"/>。</summary>
    public static double DurationSec(double distanceM, float speedKmh)
    {
        if (speedKmh <= 0f) return double.PositiveInfinity;
        var speedMps = speedKmh * 1000.0 / 3600.0;
        return distanceM / speedMps;
    }

    /// <summary>
    /// 評価結果と <see cref="IRoadGraphEdgeEnumerator.DataInverted"/> から、
    /// エニュメレータの <c>From → To</c> 方向に通行可能かを判定する。
    /// </summary>
    public static bool CanTraverseInEnumeratorDirection(EdgeEvaluation eval, bool dataInverted)
    {
        if (!eval.CanPass) return false;
        return eval.Oneway switch
        {
            // 両方向通行可
            OnewayDirection.Bidirectional => true,
            // OSM 順方向のみ通行可。enum 方向 = OSM 順 (DataInverted=false) のとき通行可
            OnewayDirection.Forward => !dataInverted,
            // OSM 逆方向のみ通行可。enum 方向 = OSM 逆 (DataInverted=true) のとき通行可
            OnewayDirection.Backward => dataInverted,
            _ => false,
        };
    }
}
