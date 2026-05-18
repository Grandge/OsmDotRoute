using OsmDotRoute.Profiles;

namespace OsmDotRoute.Routing;

/// <summary>
/// エッジ重み計算器。プロファイル評価結果と動的制約からエッジの所要時間（秒）と方向別通行可否を算出する。
/// </summary>
/// <remarks>
/// <para>重み = 距離 (m) / (速度 (m/s) × 結合 speedFactor)。プロファイルで通行不可・速度 0・進入不可制約交差時は無限大。</para>
/// <para>
/// 方向解釈: エニュメレータの <c>From → To</c> 方向を「順方向」と呼ぶ。
/// <c>DataInverted=false</c> なら順方向 = OSM デジタイズ方向、
/// <c>DataInverted=true</c> なら順方向 = OSM デジタイズ方向の逆。
/// この変換を <see cref="CanTraverseInEnumeratorDirection"/> に閉じ込めている。
/// </para>
/// <para>
/// 制約評価（REQ-RST-013〜015, REQ-RST-030〜032）はエッジ全体（端点 + 中間シェイプ）を単位とし、
/// 同一エッジを部分通過する場合（ソース／ターゲットスナップ）でも同じ結合 speedFactor を適用する。
/// </para>
/// </remarks>
internal sealed class EdgeWeightCalculator
{
    private readonly IRoadGraph _graph;
    private readonly ProfileEvaluator _evaluator;
    private readonly RestrictedAreaService? _restrictions;

    public EdgeWeightCalculator(IRoadGraph graph, ProfileEvaluator evaluator, RestrictedAreaService? restrictions = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(evaluator);
        _graph = graph;
        _evaluator = evaluator;
        _restrictions = restrictions;
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

    /// <summary>
    /// 制約評価込みでエッジ全体の所要時間 (秒) を返す。通行不可・進入不可制約交差時は <see cref="double.PositiveInfinity"/>。
    /// </summary>
    public double EvaluateEdgeDurationSec(IRoadGraphEdgeEnumerator en)
    {
        var eval = Evaluate(en.EdgeProfileIndex);
        if (!CanTraverseInEnumeratorDirection(eval, en.DataInverted)) return double.PositiveInfinity;

        var baseDuration = DurationSec(en.DistanceM, eval.SpeedKmh);
        if (double.IsPositiveInfinity(baseDuration)) return baseDuration;

        var factor = EvaluateConstraintFactor(en.From, en.To, en.Shape);
        if (double.IsPositiveInfinity(factor)) return double.PositiveInfinity;
        return baseDuration / factor;
    }

    /// <summary>
    /// 指定エッジで部分距離 (m) を通過する所要時間 (秒) を制約込みで返す。
    /// ソース／ターゲットのスナップ部分通過用。制約評価はエッジ全体に対して 1 回行う（部分通過でも同じ係数）。
    /// </summary>
    /// <returns>部分通過所要時間。通行不可・進入不可なら <see cref="double.PositiveInfinity"/>。</returns>
    public double EvaluateEdgePartialDurationSec(RoadEdge edge, double partialDistanceM, EdgeEvaluation eval)
    {
        var baseDuration = DurationSec(partialDistanceM, eval.SpeedKmh);
        if (double.IsPositiveInfinity(baseDuration)) return baseDuration;

        var factor = EvaluateConstraintFactor(edge.From, edge.To, edge.Shape);
        if (double.IsPositiveInfinity(factor)) return double.PositiveInfinity;
        return baseDuration / factor;
    }

    /// <summary>
    /// 制約サービスが未設定／登録 0 件の場合は 1.0、制約交差時は <see cref="double.PositiveInfinity"/>。
    /// それ以外は結合 speedFactor を返す。
    /// </summary>
    private double EvaluateConstraintFactor(uint from, uint to, IReadOnlyList<GeoCoordinate> middleShape)
    {
        if (_restrictions is null) return 1.0;
        var shape = BuildFullShape(from, to, middleShape);
        return _restrictions.EvaluateConstraints(shape, _evaluator);
    }

    private IReadOnlyList<GeoCoordinate> BuildFullShape(uint from, uint to, IReadOnlyList<GeoCoordinate> middle)
    {
        var list = new List<GeoCoordinate>(middle.Count + 2)
        {
            _graph.GetVertex(from),
        };
        for (var i = 0; i < middle.Count; i++) list.Add(middle[i]);
        list.Add(_graph.GetVertex(to));
        return list;
    }
}
