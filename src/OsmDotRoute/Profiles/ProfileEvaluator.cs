using System.Globalization;

namespace OsmDotRoute.Profiles;

/// <summary>
/// プロファイル JSON の評価器（REQ-PRF-001〜002, REQ-PRF-007〜014）。
/// OSM タグ集合からエッジ評価 (canPass, speed, oneway) を導出する。
/// 難所タイプから難所評価 (speedFactor, canPass) を導出する。
/// </summary>
internal sealed class ProfileEvaluator
{
    private readonly JsonProfileDefinition _def;
    private readonly JsonDifficultyRule _difficultyDefault;
    private readonly double _fallbackSpeedKmh;
    private readonly bool _fallbackAccessAllow;
    private readonly double _minKmh;
    private readonly double _maxKmh;
    private readonly string _maxspeedTagKey;
    private readonly bool _maxspeedDefaultMph;
    private readonly double _speedMultiplier;

    /// <summary>
    /// プロファイル定義から評価器を構築する。検証は <see cref="ValidateAndCompile"/> で行う。
    /// </summary>
    /// <exception cref="InvalidProfileException">プロファイル定義が不正</exception>
    public ProfileEvaluator(JsonProfileDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        ValidateAndCompile(def);

        _def = def;
        _difficultyDefault = def.DifficultyDefault!;
        _fallbackSpeedKmh = def.Fallback!.SpeedKmh;
        _fallbackAccessAllow = NormalizeAccessLiteral(def.Fallback.Access);
        _minKmh = def.SpeedBounds!.MinKmh;
        _maxKmh = def.SpeedBounds.MaxKmh;
        _maxspeedTagKey = def.MaxspeedTagKey ?? "maxspeed";
        _maxspeedDefaultMph = string.Equals(def.MaxspeedUnitDefault, "mph", StringComparison.OrdinalIgnoreCase);
        _speedMultiplier = def.SpeedMultiplier ?? 1.0;
    }

    /// <summary>
    /// プロファイル JSON の <c>name</c> フィールド。<c>NativeRoadGraph</c> が
    /// <c>.odrg</c> の BAKED_PROFILE スロット解決に使う（Phase 3 ステップ 3A.3b）。
    /// </summary>
    public string Name => _def.Name
        ?? throw new InvalidOperationException(
            "ProfileEvaluator: JSON プロファイルに name フィールドがありません。");

    /// <summary>
    /// OSM タグ集合からエッジ評価を導出する。
    /// 評価順: highway 別ルール（access/speed 基本値） → アクセスタグ上書き → maxspeed タグ → 速度クランプ → oneway。
    /// </summary>
    public EdgeEvaluation Evaluate(IReadOnlyDictionary<string, string> osmTags)
    {
        ArgumentNullException.ThrowIfNull(osmTags);

        // 1. highway 値を取得し、対応ルール参照。無ければ fallback。
        // 「highway 別ルールが access:"no" を明示している場合」は hard-deny とし、
        // アクセスタグでの上書きを許可しない（Itinero car.lua / pedestrian.lua のセマンティクスに合わせる）。
        // 例: car プロファイルにとって highway=footway は motor_vehicle=yes 等の上書きを許さない。
        bool accessAllow;
        bool isHardDeny;
        double speedKmh;

        if (osmTags.TryGetValue("highway", out var highwayValue)
            && _def.Highway!.TryGetValue(highwayValue, out var highwayRule))
        {
            var hwyAccessRaw = highwayRule.Access;
            if (string.Equals(hwyAccessRaw, "no", StringComparison.OrdinalIgnoreCase))
            {
                accessAllow = false;
                isHardDeny = true;
            }
            else
            {
                accessAllow = true; // null または "yes"
                isHardDeny = false;
            }
            speedKmh = highwayRule.SpeedKmh ?? _fallbackSpeedKmh;
        }
        else
        {
            accessAllow = _fallbackAccessAllow;
            isHardDeny = false; // 未知 highway は access タグでの上書き可
            speedKmh = _fallbackSpeedKmh;
        }

        // 2. アクセスタグで上書き（hard-deny 時は適用しない）
        if (!isHardDeny && _def.AccessTagKeys is { } accessKeys)
        {
            foreach (var key in accessKeys)
            {
                if (osmTags.TryGetValue(key, out var accessValue)
                    && _def.AccessValueMap!.TryGetValue(accessValue, out var decision))
                {
                    accessAllow = string.Equals(decision, "allow", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        // 3. 通行不可なら早期 return
        if (!accessAllow)
        {
            return new EdgeEvaluation(false, 0f, OnewayDirection.Bidirectional);
        }

        // 4. maxspeed タグでさらに上書き
        if (osmTags.TryGetValue(_maxspeedTagKey, out var maxspeedRaw)
            && TryParseMaxspeed(maxspeedRaw, _maxspeedDefaultMph, out var parsedKmh))
        {
            speedKmh = parsedKmh;
        }

        // 5. speedMultiplier を適用（Itinero Fastest 相当: 0.75）
        speedKmh *= _speedMultiplier;

        // 6. 速度クランプ
        if (speedKmh < _minKmh) speedKmh = _minKmh;
        if (speedKmh > _maxKmh) speedKmh = _maxKmh;

        // 7. oneway
        var oneway = _def.IgnoreOneway
            ? OnewayDirection.Bidirectional
            : ParseOneway(osmTags);

        return new EdgeEvaluation(true, (float)speedKmh, oneway);
    }

    /// <summary>
    /// 難所タイプ評価。プロファイルに該当タイプ定義があればそれを、無ければ <c>difficultyDefault</c> を返す（REQ-PRF-014）。
    /// </summary>
    public DifficultyEvaluation EvaluateDifficulty(string difficultyType)
    {
        if (string.IsNullOrWhiteSpace(difficultyType))
        {
            return new DifficultyEvaluation((float)_difficultyDefault.SpeedFactor, _difficultyDefault.CanPass);
        }

        if (_def.Difficulty is { } table
            && table.TryGetValue(difficultyType, out var rule))
        {
            return new DifficultyEvaluation((float)rule.SpeedFactor, rule.CanPass);
        }

        return new DifficultyEvaluation((float)_difficultyDefault.SpeedFactor, _difficultyDefault.CanPass);
    }

    private static OnewayDirection ParseOneway(IReadOnlyDictionary<string, string> osmTags)
    {
        if (!osmTags.TryGetValue("oneway", out var v))
        {
            return OnewayDirection.Bidirectional;
        }

        return v switch
        {
            "yes" or "true" or "1" => OnewayDirection.Forward,
            "-1" or "reverse" => OnewayDirection.Backward,
            _ => OnewayDirection.Bidirectional,
        };
    }

    private static bool TryParseMaxspeed(string raw, bool defaultMph, out double speedKmh)
    {
        speedKmh = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // 例: "50", "50 mph", "30 kmh", "walk", "signals"
        var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false; // "walk" / "signals" 等は対応せず、highway デフォルトを使う
        }

        bool isMph;
        if (parts.Length == 2)
        {
            isMph = parts[1].Equals("mph", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            isMph = defaultMph;
        }

        speedKmh = isMph ? value * 1.609344 : value;
        return true;
    }

    private static bool NormalizeAccessLiteral(string? access)
    {
        // null / "yes" → 許可、"no" → 拒否
        if (string.IsNullOrWhiteSpace(access)) return true;
        return access.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateAndCompile(JsonProfileDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Name))
        {
            throw new InvalidProfileException("プロファイル定義に 'name' が必要です。");
        }

        if (def.Highway is null || def.Highway.Count == 0)
        {
            throw new InvalidProfileException($"プロファイル '{def.Name}' に 'highway' ルールが必要です。");
        }

        if (def.AccessValueMap is null)
        {
            throw new InvalidProfileException($"プロファイル '{def.Name}' に 'accessValueMap' が必要です。");
        }

        if (def.Fallback is null)
        {
            throw new InvalidProfileException($"プロファイル '{def.Name}' に 'fallback' が必要です。");
        }

        if (def.SpeedBounds is null)
        {
            throw new InvalidProfileException($"プロファイル '{def.Name}' に 'speedBounds' が必要です。");
        }

        if (def.SpeedBounds.MinKmh < 0 || def.SpeedBounds.MaxKmh <= def.SpeedBounds.MinKmh)
        {
            throw new InvalidProfileException(
                $"プロファイル '{def.Name}' の 'speedBounds' が不正です (min={def.SpeedBounds.MinKmh}, max={def.SpeedBounds.MaxKmh})。");
        }

        if (def.DifficultyDefault is null)
        {
            throw new InvalidProfileException($"プロファイル '{def.Name}' に 'difficultyDefault' が必要です。");
        }

        if (def.DifficultyDefault.SpeedFactor < 0 || def.DifficultyDefault.SpeedFactor > 1)
        {
            throw new InvalidProfileException(
                $"プロファイル '{def.Name}' の 'difficultyDefault.speedFactor' は 0.0〜1.0 の範囲が必要です。");
        }

        if (def.Difficulty is { } diff)
        {
            foreach (var (key, rule) in diff)
            {
                if (rule.SpeedFactor < 0 || rule.SpeedFactor > 1)
                {
                    throw new InvalidProfileException(
                        $"プロファイル '{def.Name}' の 'difficulty[{key}].speedFactor' は 0.0〜1.0 の範囲が必要です（実値: {rule.SpeedFactor}）。");
                }
            }
        }
    }
}
