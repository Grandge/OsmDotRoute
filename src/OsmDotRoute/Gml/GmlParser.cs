using System.Globalization;
using System.Xml;

namespace OsmDotRoute.Gml;

/// <summary>
/// 国土数値情報 KSJ アプリケーションスキーマ準拠 GML 3.2 から
/// 制約エリア用のポリゴン形状を抽出するストリーミングパーサー（REQ-RST-020〜028）。
/// </summary>
/// <remarks>
/// <para>
/// Phase 1 動作確認は A31「浸水想定区域」(`&lt;ksj:ExpectedFloodArea&gt;`) で行うが、
/// パーサーは**フィーチャ要素名にハードコード依存しない**（任意の KSJ プロダクトを受け入れる、REQ-RST-020）。
/// 解析対象はフィーチャの形状（外周＋Hole）のみで、`&lt;ksj:waterDepth&gt;` 等のハザード属性は読み飛ばす（REQ-RST-026）。
/// </para>
/// <para>
/// 構造: <c>&lt;ksj:Dataset&gt;</c> 直下に <c>&lt;gml:Curve&gt;</c>（リング座標）→
/// <c>&lt;gml:Surface&gt;</c>（外周 + Hole、xlink で Curve 参照）→
/// 任意フィーチャ要素（xlink で Surface 参照）の順に並ぶ。
/// 1 パスで Curve / Surface 辞書とフィーチャ未解決リストを構築し、終端で未解決を解決する。
/// </para>
/// <para>
/// <c>&lt;gml:MultiSurface&gt;</c> は Phase 1 非対応、検出時に <see cref="NotSupportedException"/> を投げる（REQ-RST-023）。
/// </para>
/// </remarks>
internal static class GmlParser
{
    private const string GmlNs = "http://www.opengis.net/gml/3.2";
    private const string XlinkNs = "http://www.w3.org/1999/xlink";

    /// <summary>GML 文字列をパースし、抽出されたポリゴン列を返す。</summary>
    /// <exception cref="InvalidGmlException">GML が不正、xlink 参照解決失敗</exception>
    /// <exception cref="NotSupportedException"><c>&lt;gml:MultiSurface&gt;</c> 検出（REQ-RST-023）</exception>
    public static IReadOnlyList<GeoPolygon> ParseString(string gml)
    {
        ArgumentNullException.ThrowIfNull(gml);
        using var stringReader = new StringReader(gml);
        using var xmlReader = CreateReader(stringReader);
        return Parse(xmlReader);
    }

    /// <summary>GML Stream をパースし、抽出されたポリゴン列を返す。</summary>
    /// <exception cref="InvalidGmlException">GML が不正、xlink 参照解決失敗</exception>
    /// <exception cref="NotSupportedException"><c>&lt;gml:MultiSurface&gt;</c> 検出（REQ-RST-023）</exception>
    public static IReadOnlyList<GeoPolygon> ParseStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var xmlReader = CreateReader(stream);
        return Parse(xmlReader);
    }

    private static XmlReader CreateReader(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            // 外部実体参照を解決しない（セキュリティ・XXE 対策）
            XmlResolver = null,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        return XmlReader.Create(stream, settings);
    }

    private static XmlReader CreateReader(TextReader reader)
    {
        var settings = new XmlReaderSettings
        {
            XmlResolver = null,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreWhitespace = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        return XmlReader.Create(reader, settings);
    }

    /// <summary>
    /// 1 パスで Curve / Surface 辞書と未解決フィーチャを構築し、終了時に解決して返す。
    /// </summary>
    private static List<GeoPolygon> Parse(XmlReader reader)
    {
        var curves = new Dictionary<string, IReadOnlyList<GeoCoordinate>>(StringComparer.Ordinal);
        var surfaces = new Dictionary<string, SurfaceRef>(StringComparer.Ordinal);
        var pendingFeatures = new List<string>();   // Surface ID を後で解決する

        try
        {
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                // ルート要素 (Depth=0、`<ksj:Dataset>` 等) はスキップ。直下の子 (Depth=1) のみ識別対象とする
                if (reader.Depth != 1) continue;

                if (reader.NamespaceURI == GmlNs)
                {
                    switch (reader.LocalName)
                    {
                        case "Curve":
                            ReadCurve(reader, curves);
                            break;
                        case "Surface":
                            ReadSurface(reader, surfaces);
                            break;
                        case "MultiSurface":
                            throw new NotSupportedException(
                                "Phase 1 では <gml:MultiSurface> 非対応です（REQ-RST-023、Phase 2 以降で対応予定）。");
                        // gml:boundedBy などのメタ要素は素通り（次の Read で兄弟へ）
                    }
                }
                else
                {
                    // gml 名前空間外 = フィーチャ候補。
                    // 配下の xlink:href から Surface 参照 ID を見つける（要素名非依存、KSJ では <ksj:bounds> 等）。
                    var surfaceId = FindSurfaceReferenceInFeature(reader.ReadSubtree());
                    if (surfaceId is not null)
                    {
                        pendingFeatures.Add(surfaceId);
                    }
                }
            }
        }
        catch (XmlException ex)
        {
            throw new InvalidGmlException("GML の XML パースに失敗しました。", ex);
        }

        // フィーチャ参照の Surface ID を解決して GeoPolygon を構築
        var polygons = new List<GeoPolygon>(pendingFeatures.Count);
        foreach (var surfaceId in pendingFeatures)
        {
            if (!surfaces.TryGetValue(surfaceId, out var surfaceRef))
            {
                throw new InvalidGmlException(
                    $"フィーチャが参照する <gml:Surface gml:id=\"{surfaceId}\"> が見つかりません。");
            }

            if (!curves.TryGetValue(surfaceRef.ExteriorCurveId, out var outer))
            {
                throw new InvalidGmlException(
                    $"<gml:Surface gml:id=\"{surfaceId}\"> の外周が参照する <gml:Curve gml:id=\"{surfaceRef.ExteriorCurveId}\"> が見つかりません。");
            }

            var holes = new List<IReadOnlyList<GeoCoordinate>>(surfaceRef.InteriorCurveIds.Count);
            foreach (var holeCurveId in surfaceRef.InteriorCurveIds)
            {
                if (!curves.TryGetValue(holeCurveId, out var hole))
                {
                    throw new InvalidGmlException(
                        $"<gml:Surface gml:id=\"{surfaceId}\"> の Hole が参照する <gml:Curve gml:id=\"{holeCurveId}\"> が見つかりません。");
                }
                holes.Add(hole);
            }

            polygons.Add(new GeoPolygon(outer, holes));
        }
        return polygons;
    }

    private static void ReadCurve(XmlReader reader, Dictionary<string, IReadOnlyList<GeoCoordinate>> curves)
    {
        var id = reader.GetAttribute("id", GmlNs);
        if (string.IsNullOrEmpty(id))
        {
            throw new InvalidGmlException("<gml:Curve> に gml:id 属性が必要です。");
        }

        IReadOnlyList<GeoCoordinate>? coords = null;
        using var sub = reader.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType == XmlNodeType.Element
                && sub.NamespaceURI == GmlNs
                && sub.LocalName == "posList")
            {
                var raw = sub.ReadElementContentAsString();
                coords = ParsePosList(raw);
            }
        }

        if (coords is null || coords.Count < 3)
        {
            throw new InvalidGmlException(
                $"<gml:Curve gml:id=\"{id}\"> の <gml:posList> が空または頂点数が 3 未満です。");
        }
        curves[id] = coords;
    }

    private static void ReadSurface(XmlReader reader, Dictionary<string, SurfaceRef> surfaces)
    {
        var id = reader.GetAttribute("id", GmlNs);
        if (string.IsNullOrEmpty(id))
        {
            throw new InvalidGmlException("<gml:Surface> に gml:id 属性が必要です。");
        }

        string? exteriorCurveId = null;
        var interiorCurveIds = new List<string>();

        using var sub = reader.ReadSubtree();
        var depth = 0;  // exterior / interior 配下にいるかを示す: 0=outside, 1=exterior, 2=interior
        while (sub.Read())
        {
            if (sub.NodeType == XmlNodeType.Element && sub.NamespaceURI == GmlNs)
            {
                switch (sub.LocalName)
                {
                    case "exterior":
                        depth = 1;
                        break;
                    case "interior":
                        depth = 2;
                        break;
                    case "curveMember":
                        var href = sub.GetAttribute("href", XlinkNs);
                        if (!string.IsNullOrEmpty(href) && href.StartsWith("#", StringComparison.Ordinal))
                        {
                            var refId = href.Substring(1);
                            if (depth == 1 && exteriorCurveId is null) exteriorCurveId = refId;
                            else if (depth == 2) interiorCurveIds.Add(refId);
                        }
                        break;
                }
            }
            else if (sub.NodeType == XmlNodeType.EndElement && sub.NamespaceURI == GmlNs
                && (sub.LocalName == "exterior" || sub.LocalName == "interior"))
            {
                depth = 0;
            }
        }

        if (exteriorCurveId is null)
        {
            throw new InvalidGmlException(
                $"<gml:Surface gml:id=\"{id}\"> に <gml:exterior><gml:Ring><gml:curveMember xlink:href=\"#...\"/> が必要です。");
        }
        surfaces[id] = new SurfaceRef(exteriorCurveId, interiorCurveIds);
    }

    /// <summary>
    /// フィーチャ要素配下を走査し、xlink:href が "#..." 形式の最初の子要素から参照 ID を取り出す。
    /// 要素名（KSJ では `&lt;ksj:bounds&gt;` 慣習）に依存しない、汎用性のため。
    /// </summary>
    /// <returns>Surface 候補 ID。見つからなければ <c>null</c>。Surface 辞書との照合は呼び出し側で行う</returns>
    private static string? FindSurfaceReferenceInFeature(XmlReader subtree)
    {
        using (subtree)
        {
            while (subtree.Read())
            {
                if (subtree.NodeType != XmlNodeType.Element || !subtree.HasAttributes) continue;
                var href = subtree.GetAttribute("href", XlinkNs);
                if (!string.IsNullOrEmpty(href) && href.StartsWith("#", StringComparison.Ordinal))
                {
                    return href.Substring(1);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// `&lt;gml:posList&gt;` の内容を「緯度 経度」順で解析する（REQ-RST-028）。
    /// </summary>
    private static IReadOnlyList<GeoCoordinate> ParsePosList(string raw)
    {
        var tokens = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length % 2 != 0)
        {
            throw new InvalidGmlException(
                $"<gml:posList> のトークン数が奇数です（緯度経度ペアの組成不正、token={tokens.Length}）。");
        }

        var coords = new List<GeoCoordinate>(tokens.Length / 2);
        for (var i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            {
                throw new InvalidGmlException(
                    $"<gml:posList> の数値解析に失敗しました（token[{i}]=\"{tokens[i]}\", token[{i + 1}]=\"{tokens[i + 1]}\"）。");
            }
            coords.Add(new GeoCoordinate(lat, lon));
        }
        return coords;
    }

    private readonly record struct SurfaceRef(string ExteriorCurveId, IReadOnlyList<string> InteriorCurveIds);
}
