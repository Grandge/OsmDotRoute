namespace OsmDotRoute;

/// <summary>
/// 車両プロファイル。JSON 外部ファイルで定義され、リビルドなしにパラメータ調整可能（REQ-PRF-007）。
/// 同梱プロファイル <see cref="Car"/> / <see cref="Pedestrian"/> は埋込リソースから遅延ロードされる（REQ-PRF-008）。
/// </summary>
public sealed class VehicleProfile
{
    // Step 5a で内部実装（JsonProfileDefinition / ProfileEvaluator 注入）に置き換える。
    private VehicleProfile(string name)
    {
        Name = name;
    }

    /// <summary>プロファイル名（例: "car", "pedestrian"）</summary>
    public string Name { get; }

    /// <summary>同梱の自動車プロファイル。Profiles/car.json から遅延ロード（Step 5a で実装）。</summary>
    public static VehicleProfile Car { get; } = new VehicleProfile("car");

    /// <summary>同梱の歩行者プロファイル。Profiles/pedestrian.json から遅延ロード（Step 5a で実装）。</summary>
    public static VehicleProfile Pedestrian { get; } = new VehicleProfile("pedestrian");

    /// <summary>
    /// JSON ファイルからユーザー定義プロファイルを読み込む（REQ-PRF-009）。
    /// </summary>
    /// <param name="filePath">JSON ファイルパス</param>
    public static VehicleProfile LoadFromJsonFile(string filePath)
        => throw new NotImplementedException("Step 5a で実装予定");

    /// <summary>
    /// JSON 文字列からユーザー定義プロファイルを読み込む（REQ-PRF-009）。
    /// </summary>
    /// <param name="json">JSON 文字列</param>
    public static VehicleProfile LoadFromJsonString(string json)
        => throw new NotImplementedException("Step 5a で実装予定");

    /// <summary>
    /// JSON Stream からユーザー定義プロファイルを読み込む（REQ-PRF-009）。
    /// </summary>
    /// <param name="stream">JSON Stream</param>
    public static VehicleProfile LoadFromJsonStream(Stream stream)
        => throw new NotImplementedException("Step 5a で実装予定");
}
