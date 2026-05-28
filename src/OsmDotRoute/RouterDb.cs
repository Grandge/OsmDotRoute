using OsmDotRoute.Native;
using OsmDotRoute.Routing;

namespace OsmDotRoute;

/// <summary>
/// 経路計算用のグラフデータ（REQ-MAP-001 / REQ-MAP-009）。
/// Phase 3 ステップ 3C.1 以降は <see cref="LoadFromOdrg(string)"/> から <c>.odrg</c> ファイルを直接ロードする（Itinero 非依存）。
/// 公開 API に内部実装型を露出させない（REQ-API-003）。
/// </summary>
public sealed class RouterDb
{
    private readonly IRoadGraph _graph;
    private readonly IRoadSnapper _snapper;

    internal RouterDb(IRoadGraph graph, IRoadSnapper snapper)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(snapper);
        _graph = graph;
        _snapper = snapper;
    }

    /// <summary>
    /// <c>.odrg</c> ファイル（OsmDotRoute Native Graph、Phase 2 グラフ形式仕様書 v0.2）を読み込み、
    /// <see cref="RouterDb"/> を生成する（Phase 3 ステップ 3C.1、REQ-MAP-009）。
    /// 内部で <see cref="NativeRoadGraph"/>（MMF + Span ゼロコピー読込）と
    /// <see cref="NativeRoadSnapper"/>（R-tree クエリ）を構築する。
    /// </summary>
    /// <param name="odrgPath"><c>.odrg</c> ファイルパス</param>
    /// <returns>ロードされた <see cref="RouterDb"/></returns>
    /// <exception cref="ArgumentException"><paramref name="odrgPath"/> が <c>null</c> または空白</exception>
    /// <exception cref="FileNotFoundException">ファイルが存在しない</exception>
    /// <exception cref="OsmDotRoute.Internal.Odrg.OdrgFormatException">ファイル形式が不正</exception>
    public static RouterDb LoadFromOdrg(string odrgPath)
    {
        if (string.IsNullOrWhiteSpace(odrgPath))
        {
            throw new ArgumentException("ファイルパスを指定してください。", nameof(odrgPath));
        }
        if (!File.Exists(odrgPath))
        {
            throw new FileNotFoundException(".odrg ファイルが見つかりません。", odrgPath);
        }

        var graph = new NativeRoadGraph(odrgPath);
        var snapper = new NativeRoadSnapper(graph);
        return new RouterDb(graph, snapper);
    }

    /// <summary>
    /// メモリ上の <c>.odrg</c> バイト列（fetch 済みデータなど）を読み込み、<see cref="RouterDb"/> を生成する
    /// （Phase 3 ステップ 3J.2、ブラウザ WASM など <see cref="System.IO.MemoryMappedFiles.MemoryMappedFile"/> が
    /// 使えない環境向け）。内部で <see cref="NativeRoadGraph"/>（ピン留めバッファ + Span ゼロコピー読込）と
    /// <see cref="NativeRoadSnapper"/> を構築する。ファイル版 <see cref="LoadFromOdrg(string)"/> と同一の結果を返す。
    /// </summary>
    /// <param name="odrgData"><c>.odrg</c> 全体のバイト列（非空）</param>
    /// <returns>ロードされた <see cref="RouterDb"/></returns>
    /// <exception cref="ArgumentException"><paramref name="odrgData"/> が空</exception>
    /// <exception cref="OsmDotRoute.Internal.Odrg.OdrgFormatException">ファイル形式が不正</exception>
    public static RouterDb LoadFromOdrg(ReadOnlyMemory<byte> odrgData)
    {
        if (odrgData.IsEmpty)
        {
            throw new ArgumentException(".odrg データが空です。", nameof(odrgData));
        }

        var graph = new NativeRoadGraph(odrgData);
        var snapper = new NativeRoadSnapper(graph);
        return new RouterDb(graph, snapper);
    }

    /// <summary>
    /// 経路計算エンジン向けの内部グラフアクセサ。
    /// </summary>
    internal IRoadGraph Graph => _graph;

    /// <summary>
    /// 道路スナップ機能の内部アクセサ。
    /// </summary>
    internal IRoadSnapper Snapper => _snapper;

    /// <summary>
    /// 読み込み済みグラフから頂点数・辺数・経緯度範囲の統計を取得する（REQ-MAP-002）。
    /// 都道府県単位（数百万エッジ）でも int 範囲内 (~2.1B) に収まる前提。
    /// Phase 4+ で全国対応する場合は long に拡張。
    /// </summary>
    /// <returns>頂点数・辺数・経緯度範囲を含む統計</returns>
    public RouterDbStatistics GetStatistics()
    {
        var bounds = _graph.GetBounds();
        return new RouterDbStatistics(
            checked((int)_graph.VertexCount),
            checked((int)_graph.EdgeCount),
            bounds.SouthWest,
            bounds.NorthEast);
    }
}
