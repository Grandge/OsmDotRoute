using System.Text.Json;
using OsmDotRoute.Profiles;

namespace OsmDotRoute;

/// <summary>
/// 車両プロファイル。JSON 外部ファイルで定義され、リビルドなしにパラメータ調整可能（REQ-PRF-007）。
/// 同梱プロファイル <see cref="Car"/> / <see cref="Pedestrian"/> は埋込リソースから遅延ロードされる（REQ-PRF-008）。
/// ユーザー独自プロファイルは <see cref="LoadFromJsonFile"/> / <see cref="LoadFromJsonString"/> /
/// <see cref="LoadFromJsonStream"/> で読み込む（REQ-PRF-009）。
/// </summary>
public sealed class VehicleProfile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly Lazy<VehicleProfile> CarLazy = new(() => LoadEmbedded("car.json"));
    private static readonly Lazy<VehicleProfile> PedestrianLazy = new(() => LoadEmbedded("pedestrian.json"));
    private static readonly Lazy<VehicleProfile> BicycleLazy = new(() => LoadEmbedded("bicycle.json"));

    private readonly JsonProfileDefinition _definition;
    private readonly ProfileEvaluator _evaluator;

    private VehicleProfile(JsonProfileDefinition definition, ProfileEvaluator evaluator)
    {
        _definition = definition;
        _evaluator = evaluator;
        Name = definition.Name!;
    }

    /// <summary>プロファイル名（例: "car", "pedestrian"）</summary>
    public string Name { get; }

    /// <summary>同梱の自動車プロファイル。Profiles/car.json から遅延ロード（埋込リソース）。</summary>
    public static VehicleProfile Car => CarLazy.Value;

    /// <summary>同梱の歩行者プロファイル。Profiles/pedestrian.json から遅延ロード（埋込リソース）。</summary>
    public static VehicleProfile Pedestrian => PedestrianLazy.Value;

    /// <summary>
    /// 同梱の自転車プロファイル（REQ-PRF-003、Phase 3 ステップ 3D.1）。
    /// Profiles/bicycle.json から遅延ロード（埋込リソース）。
    /// 平均 15 km/h、cycleway/path 優先、motorway/trunk 通行不可。
    /// </summary>
    public static VehicleProfile Bicycle => BicycleLazy.Value;

    /// <summary>内部評価器（Dijkstra・難所判定で使用）</summary>
    internal ProfileEvaluator Evaluator => _evaluator;

    /// <summary>
    /// JSON ファイルからユーザー定義プロファイルを読み込む（REQ-PRF-009）。
    /// </summary>
    /// <param name="filePath">JSON ファイルパス</param>
    /// <returns>ロードされた <see cref="VehicleProfile"/> インスタンス</returns>
    /// <exception cref="ArgumentException">パスが null または空</exception>
    /// <exception cref="FileNotFoundException">ファイルが存在しない</exception>
    /// <exception cref="InvalidProfileException">JSON 形式不正または検証失敗</exception>
    public static VehicleProfile LoadFromJsonFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("ファイルパスを指定してください。", nameof(filePath));
        }
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("プロファイル JSON ファイルが見つかりません。", filePath);
        }

        using var stream = File.OpenRead(filePath);
        return LoadFromJsonStream(stream);
    }

    /// <summary>
    /// JSON 文字列からユーザー定義プロファイルを読み込む（REQ-PRF-009）。
    /// </summary>
    /// <param name="json">JSON 文字列</param>
    /// <returns>ロードされた <see cref="VehicleProfile"/> インスタンス</returns>
    /// <exception cref="ArgumentException">JSON が null または空</exception>
    /// <exception cref="InvalidProfileException">JSON 形式不正または検証失敗</exception>
    public static VehicleProfile LoadFromJsonString(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON 文字列を指定してください。", nameof(json));
        }

        JsonProfileDefinition? def;
        try
        {
            def = JsonSerializer.Deserialize<JsonProfileDefinition>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidProfileException("プロファイル JSON のパースに失敗しました。", ex);
        }

        if (def is null)
        {
            throw new InvalidProfileException("プロファイル JSON が null です。");
        }

        var evaluator = new ProfileEvaluator(def);
        return new VehicleProfile(def, evaluator);
    }

    /// <summary>
    /// JSON Stream からユーザー定義プロファイルを読み込む（REQ-PRF-009）。
    /// </summary>
    /// <param name="stream">JSON Stream</param>
    /// <returns>ロードされた <see cref="VehicleProfile"/> インスタンス</returns>
    /// <exception cref="ArgumentNullException">Stream が null</exception>
    /// <exception cref="InvalidProfileException">JSON 形式不正または検証失敗</exception>
    public static VehicleProfile LoadFromJsonStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        JsonProfileDefinition? def;
        try
        {
            def = JsonSerializer.Deserialize<JsonProfileDefinition>(stream, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidProfileException("プロファイル JSON のパースに失敗しました。", ex);
        }

        if (def is null)
        {
            throw new InvalidProfileException("プロファイル JSON が null です。");
        }

        var evaluator = new ProfileEvaluator(def);
        return new VehicleProfile(def, evaluator);
    }

    private static VehicleProfile LoadEmbedded(string resourceFileName)
    {
        var assembly = typeof(VehicleProfile).Assembly;
        var resourceName = $"OsmDotRoute.Profiles.{resourceFileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"埋込リソースが見つかりません: {resourceName}");
        return LoadFromJsonStream(stream);
    }
}
