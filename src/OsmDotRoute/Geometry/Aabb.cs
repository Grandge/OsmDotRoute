namespace OsmDotRoute.Geometry;

/// <summary>
/// 緯度経度の軸並行矩形（Axis-Aligned Bounding Box）。
/// 制約管理（REQ-RST-014）・メッシュコード変換（REQ-RST-017）等で使用する内部値型。
/// </summary>
/// <remarks>
/// <para>
/// ステップ 7 時点では純粋なデータ保持のみ。
/// 交差判定・点包含判定はステップ 8（制約管理基盤）で本型に追加予定。
/// </para>
/// <para>
/// <see cref="GeoBounds"/> と構造的に同等だが、用途が異なる（<c>GeoBounds</c> は <see cref="OsmDotRoute.Routing.IRoadGraph"/>
/// の全体範囲、<c>Aabb</c> は制約交差判定・メッシュ矩形）。
/// 統合可能性はステップ 8 完了時に再評価する。
/// </para>
/// </remarks>
internal readonly record struct Aabb(GeoCoordinate SouthWest, GeoCoordinate NorthEast);
