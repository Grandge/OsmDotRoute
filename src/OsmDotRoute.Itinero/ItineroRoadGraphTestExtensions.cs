using OsmDotRoute.Routing;

namespace OsmDotRoute.Itinero;

/// <summary>
/// Itinero 系テスト専用の internal extension。本番ホットパスからは呼ばない。
/// <para>
/// <see cref="IRoadGraph"/> 抽象から OSM タグ生データ取得 API を削除する Phase 3 §2.5 設計のため、
/// テストの「車道判定」用途（<c>tags["highway"]</c> による <c>motorway</c>/<c>trunk</c>/... 分類）のみ
/// Itinero 実装から直接タグを取り出すヘルパとして提供する。
/// Native 系（<c>NativeRoadGraph</c>）では呼出時に <see cref="InvalidOperationException"/> を投げる。
/// </para>
/// </summary>
internal static class ItineroRoadGraphTestExtensions
{
    /// <summary>
    /// 旧 <c>IRoadGraph.GetEdgeOsmTags(ushort)</c> 相当のタグ取得（テスト用）。
    /// グラフが <see cref="ItineroRoadGraph"/> でない場合は <see cref="InvalidOperationException"/>。
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetEdgeOsmTagsForTest(
        this IRoadGraph graph,
        ushort edgeProfileIndex)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph is not ItineroRoadGraph itinero)
        {
            throw new InvalidOperationException(
                $"GetEdgeOsmTagsForTest は ItineroRoadGraph 専用のテストヘルパです（実型: {graph.GetType().Name}）。");
        }
        return itinero.GetTagsByProfileIndex(edgeProfileIndex);
    }
}
