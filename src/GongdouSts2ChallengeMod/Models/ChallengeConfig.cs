using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GongdouSts2ChallengeMod.Models;

public sealed record LeaderboardRuleConfig
{
    public int LeaderboardId { get; init; }
    public string? Title { get; init; }
    public string? PresetName { get; init; }
    public string ChallengeType { get; init; } = "";
    public JsonElement ChallengeConfig { get; init; }
    public JsonElement Resources { get; init; }
    public JsonElement? EditorSchema { get; init; }
    public bool RequiresLoadoutSelection { get; init; }
}

public sealed record LaunchContext
{
    public string? LaunchSessionId { get; init; }
    public string LeaderboardId { get; init; } = "";
    public string? ServerId { get; init; }
    public int? PresetId { get; init; }
    public JsonNode? Raw { get; init; }
}

public sealed record PreparedLoadoutSelection
{
    public string? SessionId { get; init; }
    public int LeaderboardId { get; init; }
    public Dictionary<string, string[]> Selected { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int ClientTotalWeight { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
}

public sealed class Sts2PuzzleConfig
{
    public string PuzzleSetId { get; init; } = "sts2_puzzle_series_01";
    public string PuzzleSetName { get; init; } = "尖塔残局";
    public int StageIndex { get; init; } = 1;
    public int StageCount { get; init; } = 10;
    public int RulesVersion { get; init; } = 6;
    public string PuzzleId { get; init; } = "difficulty1_final_puzzle";
    public string PuzzleName { get; init; } = "石化狂信徒的第一课";
    public string PuzzleDoc { get; init; } = "docs/difficulty1_final_puzzle.md";
    public string WinConditionText { get; init; } = "击败敌人；不限回合，除非一方死亡。";
    public string EnemyInfoText { get; init; } = "敌人：石化狂信徒。敌方行动在行动表末尾后重复最后一个行动；不限回合，除非一方死亡。";
    public string LoadoutSource { get; init; } = "in_game";
    public PlayerConfig Player { get; init; } = new();
    public EnemyConfig Enemy { get; init; } = new();
    public int MinCards { get; init; } = 8;
    public int MaxCards { get; init; } = 8;
    public int MaxPotions { get; init; } = 0;
    public int MaxRelics { get; init; } = 0;
    public string SelectionRulesText { get; init; } = "";
    public string KeyRulesText { get; init; } = "";
    public List<EnemyActionConfig> EnemyActions { get; init; } = [];
    public List<SelectionConstraintConfig> SelectionConstraints { get; init; } = [];

    public static Sts2PuzzleConfig FromRuleConfig(LeaderboardRuleConfig ruleConfig, int? stageOverride = null)
    {
        var backend = ReadBackendConfig(ruleConfig, stageOverride);
        var defaults = PuzzleDefaults.Resolve(backend);
        if (backend == null)
        {
            return defaults;
        }

        // Gameplay rules are owned by this MOD build. Keep backend JSON useful
        // for context/display without letting stale admin data alter combat math.
        return new Sts2PuzzleConfig
        {
            PuzzleSetId = string.IsNullOrWhiteSpace(backend.PuzzleSetId) ? defaults.PuzzleSetId : backend.PuzzleSetId,
            PuzzleSetName = string.IsNullOrWhiteSpace(backend.PuzzleSetName) ? defaults.PuzzleSetName : backend.PuzzleSetName,
            StageIndex = defaults.StageIndex,
            StageCount = defaults.StageCount,
            RulesVersion = defaults.RulesVersion,
            PuzzleId = defaults.PuzzleId,
            PuzzleName = defaults.PuzzleName,
            PuzzleDoc = defaults.PuzzleDoc,
            WinConditionText = defaults.WinConditionText,
            EnemyInfoText = defaults.EnemyInfoText,
            LoadoutSource = defaults.LoadoutSource,
            Player = defaults.Player,
            Enemy = defaults.Enemy,
            MinCards = defaults.MinCards,
            MaxCards = defaults.MaxCards,
            MaxPotions = defaults.MaxPotions,
            MaxRelics = defaults.MaxRelics,
            SelectionRulesText = defaults.SelectionRulesText,
            KeyRulesText = defaults.KeyRulesText,
            EnemyActions = defaults.EnemyActions,
            SelectionConstraints = defaults.SelectionConstraints
        };
    }

    internal static Sts2PuzzleConfig? ReadBackendConfig(LeaderboardRuleConfig ruleConfig, int? stageOverride = null)
    {
        Sts2PuzzleConfig? backend = null;
        if (ruleConfig.ChallengeConfig.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            backend = JsonSerializer.Deserialize<Sts2PuzzleConfig>(
                ruleConfig.ChallengeConfig.GetRawText(),
                JsonOptions.CamelCaseInsensitive);
        }

        if (stageOverride is > 0)
        {
            return new Sts2PuzzleConfig
            {
                PuzzleSetId = string.IsNullOrWhiteSpace(backend?.PuzzleSetId) ? "sts2_puzzle_series_01" : backend.PuzzleSetId,
                PuzzleSetName = string.IsNullOrWhiteSpace(backend?.PuzzleSetName) ? "尖塔残局" : backend.PuzzleSetName,
                StageIndex = stageOverride.Value,
                StageCount = backend is { StageCount: > 0 } ? backend.StageCount : 10,
                PuzzleId = backend?.PuzzleId ?? "",
                PuzzleName = backend?.PuzzleName ?? "",
                PuzzleDoc = backend?.PuzzleDoc ?? "",
                RulesVersion = backend is { RulesVersion: > 0 } ? backend.RulesVersion : 6
            };
        }

        return backend;
    }
}

public sealed record EnemyActionConfig
{
    public int Turn { get; init; }
    public int Damage { get; init; }
    public int ArmorGain { get; init; }
    public bool FailIfAlive { get; init; }
    public string FailureReason { get; init; } = "";
    public string Type { get; init; } = "attack";
    public string Description { get; init; } = "";
}

public sealed record SelectionConstraintConfig
{
    public string Description { get; init; } = "";
    public string[] CardIds { get; init; } = [];
    public int MinCount { get; init; } = -1;
    public int MaxCount { get; init; } = -1;
    public int ExactCount { get; init; } = -1;
    public bool RequireEach { get; init; }

    public string? Validate(IReadOnlyList<string> cardIds)
    {
        if (RequireEach)
        {
            var missing = CardIds
                .Where(ruleId => cardIds.Count(id => string.Equals(ruleId, id, StringComparison.OrdinalIgnoreCase)) < Math.Max(1, MinCount))
                .ToArray();
            if (missing.Length > 0)
            {
                return $"{Description}：缺少 {string.Join(", ", missing)}。";
            }
        }

        var count = cardIds.Count(id => CardIds.Any(ruleId => string.Equals(ruleId, id, StringComparison.OrdinalIgnoreCase)));
        if (ExactCount >= 0 && count != ExactCount)
        {
            return $"{Description}：当前 {count}，要求 {ExactCount}。";
        }

        if (MinCount >= 0 && count < MinCount)
        {
            return $"{Description}：当前 {count}，至少 {MinCount}。";
        }

        if (MaxCount >= 0 && count > MaxCount)
        {
            return $"{Description}：当前 {count}，最多 {MaxCount}。";
        }

        return null;
    }
}

public sealed class PlayerConfig
{
    public string Character { get; init; } = "Ironclad";
    public int AscensionLevel { get; init; }
    public int StartingHp { get; init; } = 8;
    public int MaxHp { get; init; } = 8;
    public int MaxEnergy { get; init; } = 3;
    public int DrawPerTurn { get; init; } = 5;
}

public sealed class EnemyConfig
{
    public string Id { get; init; } = "CalcifiedCultist";
    public string Name { get; init; } = "石化狂信徒";
    public int BaseHp { get; init; } = 55;
    public ThinDeckPenalty ThinDeckPenalty { get; init; } = new() { MinimumCards = 0, HpPerMissingCard = 0 };

    public int GetInitialHp(int deckSize)
    {
        var missing = Math.Max(0, ThinDeckPenalty.MinimumCards - deckSize);
        return BaseHp + missing * ThinDeckPenalty.HpPerMissingCard;
    }
}

public sealed class ThinDeckPenalty
{
    public int MinimumCards { get; init; }
    public int HpPerMissingCard { get; init; }
}

public sealed class ResourcePool
{
    public List<ResourceItem> CardPool { get; init; } = [];
    public List<ResourceItem> PotionPool { get; init; } = [];
    public List<ResourceItem> RelicPool { get; init; } = [];

    public static ResourcePool FromRuleConfig(LeaderboardRuleConfig ruleConfig, int? stageOverride = null)
    {
        // STS2 puzzle rules are versioned in the MOD. Backend resources are
        // descriptive only, so stale admin JSON cannot silently change gameplay.
        if (ruleConfig.ChallengeConfig.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null && stageOverride is not > 0)
        {
            return DifficultyOneDefaults.Resources;
        }

        var backend = Sts2PuzzleConfig.ReadBackendConfig(ruleConfig, stageOverride);
        return PuzzleDefaults.ResolveResources(backend);
    }

    public IEnumerable<ResourceItem> ExpandedCardPool()
    {
        foreach (var item in CardPool)
        {
            var copies = Math.Max(1, item.MaxCopies);
            for (var index = 0; index < copies; index++)
            {
                yield return item with { CopyIndex = index + 1 };
            }
        }
    }
}

public sealed record ResourceItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Weight { get; init; } = 1;
    public int MaxCopies { get; init; } = 1;
    public int CopyIndex { get; init; } = 1;
}

public sealed class ChallengeSelection
{
    public List<string> CardIds { get; } = [];
    public List<string> PotionIds { get; } = [];
    public List<string> RelicIds { get; } = [];

    public static ChallengeSelection FromPrepared(PreparedLoadoutSelection? prepared, ResourcePool resources, Sts2PuzzleConfig config)
    {
        var selection = new ChallengeSelection();
        if (prepared != null)
        {
            AddSelected(selection.CardIds, prepared.Selected, "cardPool", "cards", "card_pool");
            AddSelected(selection.PotionIds, prepared.Selected, "potionPool", "potions", "potion_pool");
            AddSelected(selection.RelicIds, prepared.Selected, "relicPool", "relics", "relic_pool");
        }

        if (selection.CardIds.Count == 0)
        {
            selection.CardIds.AddRange(resources.ExpandedCardPool()
                .Take(config.MaxCards)
                .Select(i => i.Id));
        }

        if (selection.CardIds.Count > config.MaxCards)
        {
            selection.CardIds.RemoveRange(config.MaxCards, selection.CardIds.Count - config.MaxCards);
        }

        if (selection.PotionIds.Count > config.MaxPotions)
        {
            selection.PotionIds.RemoveRange(config.MaxPotions, selection.PotionIds.Count - config.MaxPotions);
        }

        if (selection.RelicIds.Count > config.MaxRelics)
        {
            selection.RelicIds.RemoveRange(config.MaxRelics, selection.RelicIds.Count - config.MaxRelics);
        }

        return selection;
    }

    private static void AddSelected(List<string> target, Dictionary<string, string[]> selected, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (selected.TryGetValue(key, out var values))
            {
                target.AddRange(values.Where(v => !string.IsNullOrWhiteSpace(v)));
            }
        }
    }
}

public static class DifficultyOneDefaults
{
    public static Sts2PuzzleConfig Config { get; } = new();

    public static ResourcePool Resources { get; } = new()
    {
        CardPool =
        [
            Item("StrikeIronclad", "打击", "造成6点伤害。", 5),
            Item("PerfectedStrike", "完美打击", "造成6点伤害。\n你每有一张名字中含有“打击”的牌，伤害+2。", 2),
            Item("Bash", "痛击", "造成8点伤害。\n给予2层易伤。"),
            Item("IronWave", "铁斩波", "获得5点格挡。\n造成5点伤害。"),
            Item("ShrugItOff", "耸肩无视", "获得8点格挡。\n抽1张牌。"),
            Item("DefendIronclad", "防御", "获得5点格挡。"),
            Item("BodySlam", "全身撞击", "造成你当前格挡值的伤害。"),
            Item("Uppercut", "上勾拳", "造成13点伤害。\n给予1层虚弱。\n给予1层易伤。")
        ],
        PotionPool = [],
        RelicPool = []
    };

    private static ResourceItem Item(string id, string name, string description, int maxCopies = 1) =>
        new() { Id = id, Name = name, Description = CleanDescription(description), MaxCopies = maxCopies };

    private static string CleanDescription(string description)
    {
        return string.Join(
            "\n",
            description
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(RemoveLeadingCost));
    }

    private static string RemoveLeadingCost(string line)
    {
        var trimmed = line.TrimStart().Replace(" ", "", StringComparison.Ordinal).Replace("\t", "", StringComparison.Ordinal);
        var costPrefixes = new[] { "0费，", "1费，", "2费，", "3费，", "X费，", "x费，", "0费、", "1费、", "2费、", "3费、", "X费、", "x费、" };
        foreach (var prefix in costPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return trimmed[prefix.Length..];
            }
        }

        return trimmed;
    }
}

public static class DifficultyTwoDefaults
{
    public static Sts2PuzzleConfig Config { get; } = new()
    {
        StageIndex = 2,
        RulesVersion = 1,
        PuzzleId = "difficulty2_armor_threshold",
        PuzzleName = "药水池与护甲门槛",
        PuzzleDoc = "docs/difficulty2_final_puzzle.md",
        WinConditionText = "击败敌人；不限回合，除非一方死亡。",
        EnemyInfoText = "敌人：护甲门槛哨卫。敌方行动在行动表末尾后重复最后一个行动；不限回合，除非一方死亡。敌人格挡先吸收伤害且不会自动清空。",
        Player = new PlayerConfig
        {
            Character = "Ironclad",
            StartingHp = 14,
            MaxHp = 14,
            MaxEnergy = 3,
            DrawPerTurn = 5
        },
        Enemy = new EnemyConfig
        {
            Id = "ArmorThresholdSentinel",
            Name = "护甲门槛哨卫",
            BaseHp = 99,
            ThinDeckPenalty = new ThinDeckPenalty { MinimumCards = 0, HpPerMissingCard = 0 }
        },
        MinCards = 6,
        MaxCards = 6,
        MaxPotions = 1,
        MaxRelics = 0
    };

    public static ResourcePool Resources { get; } = new()
    {
        CardPool =
        [
            Item("StrikeIronclad", "打击", "造成6点伤害。", 3),
            Item("DefendIronclad", "防御", "获得5点格挡。", 2),
            Item("Bash", "痛击", "造成8点伤害。\n给予2层易伤。"),
            Item("Neutralize", "中和", "造成3点伤害。\n给予1层虚弱。"),
            Item("D2_BallLightning", "球状闪电（改）", "造成5点伤害。若敌人有格挡，改为造成11点伤害。", 2),
            Item("Survivor", "生存者", "获得8点格挡。"),
            Item("D2_QuickSlash", "切割（改）", "造成7点伤害。若敌人有易伤，改为造成11点伤害。", 2),
            Item("DaggerThrow", "投掷匕首（改）", "造成9点伤害。"),
            Item("D2_Clothesline", "上勾拳（改）", "造成12点伤害。\n给予敌人2层虚弱。")
        ],
        PotionPool =
        [
            Item("FirePotion", "火焰药水", "造成[blue]{Damage}[/blue]点伤害。"),
            Item("VulnerablePotion", "易伤药水", "给予[blue]{VulnerablePower}[/blue]层易伤。"),
            Item("WeakPotion", "虚弱药水", "给予[blue]{WeakPower}[/blue]层虚弱。")
        ],
        RelicPool = []
    };

    private static ResourceItem Item(string id, string name, string description, int maxCopies = 1) =>
        new() { Id = id, Name = name, Description = CleanDescription(description), MaxCopies = maxCopies };

    private static string CleanDescription(string description)
    {
        return string.Join(
            "\n",
            description
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(RemoveLeadingCost));
    }

    private static string RemoveLeadingCost(string line)
    {
        var trimmed = line.TrimStart().Replace(" ", "", StringComparison.Ordinal).Replace("\t", "", StringComparison.Ordinal);
        var costPrefixes = new[] { "0费，", "1费，", "2费，", "3费，", "X费，", "x费，", "0费、", "1费、", "2费、", "3费、", "X费、", "x费、" };
        foreach (var prefix in costPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return trimmed[prefix.Length..];
            }
        }

        return trimmed;
    }
}

public static class PuzzleDefaults
{
    public static Sts2PuzzleConfig Resolve(Sts2PuzzleConfig? backend)
    {
        return PuzzleCatalog.Resolve(backend);
    }

    public static ResourcePool ResolveResources(Sts2PuzzleConfig? backend)
    {
        return PuzzleCatalog.ResolveResources(backend);
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions CamelCaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = false
    };
}
