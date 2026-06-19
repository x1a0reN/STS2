using System.Collections.Concurrent;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Challenges;
using GongdouSts2ChallengeMod.Diagnostics;
using GongdouSts2ChallengeMod.Relics;

namespace GongdouSts2ChallengeMod;

[ModInitializer("Initialize")]
public static class GongdouSts2ChallengeMod
{
    public const string Version = "0.1.192";
    public const string ChallengeType = "slay_the_spire_2_puzzle";
    public const string ExecutableName = "SlayTheSpire2.exe";

    private static readonly ConcurrentQueue<Action> MainThreadQueue = new();
    private static ChallengeSessionManager? _sessionManager;
    private static bool _localizationRegistered;
    private static int _mainThreadId;
    private static readonly HashSet<string> VanillaResourceLocalizationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Strike",
        "StrikeIronclad",
        "Defend",
        "DefendIronclad",
        "Bash",
        "PerfectedStrike",
        "BodySlam",
        "IronWave",
        "ShrugItOff",
        "Uppercut",
        "Neutralize",
        "Survivor",
        "FlyingSword",
        "D7LegSweep",
        "D8Leap",
        "FirePotion",
        "VulnerablePotion",
        "WeakPotion"
    };

    private static readonly string[] CardKeywordTermsToColor =
    [
        "保留格挡",
        "固定伤害",
        "延迟伤害",
        "相位门槛",
        "充能球",
        "灼伤",
        "伤口",
        "晕眩",
        "诅咒",
        "状态",
        "虚空",
        "奇巧",
        "姿态",
        "平静",
        "愤怒",
        "普通",
        "易伤",
        "虚弱",
        "中毒",
        "毒",
        "格挡",
        "保留格挡",
        "力量",
        "能量",
        "消耗",
        "闪电",
        "冰霜",
        "黑暗",
        "集中",
        "循环",
        "充能",
        "回声",
        "过热",
        "标记",
        "真言",
        "神格"
    ];

    private static readonly Dictionary<string, string> CustomResourceModelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BallLightning"] = "GongdouBallLightning",
        ["QuickSlash"] = "GongdouQuickSlash",
        ["DaggerThrow"] = "GongdouDaggerThrow",
        ["Clothesline"] = "GongdouClothesline",
        ["Catalyst"] = "GongdouCatalyst",
        ["PreciseStab"] = "GongdouPreciseStab",
        ["ArmorBreakSlash"] = "GongdouArmorBreakSlash",
        ["HeavySwing"] = "GongdouHeavySwing",
        ["DoubleHit"] = "GongdouDoubleHit",
        ["OpportunityStrike"] = "GongdouOpportunityStrike",
        ["PoisonDagger"] = "GongdouPoisonDagger",
        ["PoisonFog"] = "GongdouPoisonFog",
        ["CorrosiveSalve"] = "GongdouCorrosiveSalve",
        ["BattleCry"] = "GongdouBattleCry",
        ["PreparedStance"] = "GongdouPreparedStance",
        ["BloodRush"] = "GongdouBloodRush",
        ["Bluff"] = "GongdouBluff",
        ["BurstPotion"] = "GongdouBurstPotion",
        ["ExplosiveAmpoule"] = "GongdouBurstPotion",
        ["TacticalMemoClip"] = "GongdouTacticalMemoClip",
        ["CrackedBladeCharm"] = "GongdouCrackedBladeCharm",
        ["PoisonSac"] = "GongdouPoisonSac",
        ["BloodSigil"] = "GongdouBloodSigil",
        ["OldWhetstone"] = "GongdouOldWhetstone",
        ["SlowWarmCore"] = "GongdouSlowWarmCore"
    };

    public static void Initialize()
    {
        try
        {
            ModDiagnosticReporter.InstallGlobalHandlers("sts2.challenge", Version);
            _mainThreadId = System.Environment.CurrentManagedThreadId;
            EnsureLocalizationRegistered();
            new Harmony("com.gongdou.sts2.challenge").PatchAll();

            var tree = (SceneTree)Engine.GetMainLoop();
            tree.Connect(SceneTree.SignalName.ProcessFrame, Callable.From(ProcessFrame));

            _sessionManager = new ChallengeSessionManager();
            _sessionManager.Start();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => _sessionManager?.RequestShutdown();
            GD.Print($"[GongDou STS2] v{Version} initialized.");
        }
        catch (Exception ex)
        {
            ModDiagnosticReporter.ReportException("sts2.challenge.initialize", Version, ex);
            GD.PrintErr($"[GongDou STS2] Initialize failed: {ex}");
        }
    }

    public static void EnsureLocalizationRegistered()
    {
        if (_localizationRegistered)
        {
            return;
        }

        try
        {
            if (LocManager.Instance == null)
            {
                return;
            }

            _ = LocManager.Instance.GetTable("cards");
        }
        catch
        {
            return;
        }

        RegisterDifficultyOneLocalization();
        _localizationRegistered = true;
        GD.Print("[GongDou STS2] Runtime localization registered.");
    }

    private static void RegisterDifficultyOneLocalization()
    {
        MergeLoc("card_selection", new Dictionary<string, string>
        {
            ["GONGDOU_SELECT_CARDS"] = "选择卡牌：0/x"
        });

        MergeLoc("static_hover_tips", new Dictionary<string, string>
        {
            ["GONGDOU_CHALLENGE_INFO.title"] = "关卡信息",
            ["GONGDOU_CHALLENGE_INFO.description"] = "查看当前残局的怪物、行动、药水、遗物和状态信息。"
        });

        MergeLoc("cards", new Dictionary<string, string>
        {
            ["GONGDOU_SELECT_CARDS"] = "选择卡牌：0/x",
            ["GongdouBallLightning7NoOrb.title"] = "球状闪电（改）",
            ["GongdouBallLightning7NoOrb.description"] = "造成7点伤害。",
            ["GongdouQuickSlash8NoDraw.title"] = "切割（改）",
            ["GongdouQuickSlash8NoDraw.description"] = "造成8点伤害。",
            ["GongdouDaggerThrow9NoDiscard.title"] = "投掷匕首（改）",
            ["GongdouDaggerThrow9NoDiscard.description"] = "造成9点伤害。",
            ["GongdouClothesline12Weak2.title"] = "上勾拳（改）",
            ["GongdouClothesline12Weak2.description"] = "造成12点伤害。\n给予敌人2层[gold]虚弱[/gold]。",
            ["GongdouD5Void.title"] = "虚空",
            ["GongdouD5Void.description"] = "不可打出，抽到时本回合失去1点能量。\n回合结束进入弃牌堆。",
            ["GONGDOU_D5_VOID.title"] = "虚空",
            ["GONGDOU_D5_VOID.description"] = "不可打出，抽到时本回合失去1点能量。\n回合结束进入弃牌堆。",
            ["GongdouPersistentArmorRulePower.title"] = "保留格挡",
            ["GongdouPersistentArmorRulePower.description"] = "[gold]格挡[/gold]不会在回合开始时被移除。"
        });

        MergeLoc("potions", new Dictionary<string, string>
        {
            ["GongdouVulnerablePotion2.title"] = "易伤药水",
            ["GongdouVulnerablePotion2.description"] = "给予敌人 3 层易伤。",
            ["GongdouWeakPotion2.title"] = "虚弱药水",
            ["GongdouWeakPotion2.description"] = "给予敌人 2 层虚弱。"
        });

        MergeLoc("monsters", new Dictionary<string, string>
        {
            ["GongdouCalcifiedCultistMonster.name"] = "石化狂信徒",
            ["GongdouArmorThresholdSentinel.name"] = "护甲门槛哨卫"
        });

        RegisterPuzzleCatalogLocalization();
    }

    private static void RegisterPuzzleCatalogLocalization()
    {
        var cardEntries = new Dictionary<string, string>();
        var potionEntries = new Dictionary<string, string>();
        var relicEntries = new Dictionary<string, string>();

        foreach (var stage in Models.PuzzleCatalog.AllConfigs)
        {
            var resources = Models.PuzzleCatalog.ResourcesForStage(stage.StageIndex);
            foreach (var item in resources.CardPool)
            {
                AddResourceLoc(cardEntries, item, colorizeCardKeywords: true);
            }

            foreach (var item in resources.PotionPool)
            {
                AddResourceLoc(potionEntries, item);
            }

            foreach (var item in resources.RelicPool)
            {
                AddResourceLoc(relicEntries, item, includeFlavor: true);
            }
        }

        MergeLoc("cards", cardEntries);
        MergeLoc("potions", potionEntries);
        MergeLoc("relics", relicEntries);
        MergeLoc("powers", new Dictionary<string, string>
        {
            ["GongdouStageRulePower.title"] = "尖塔残局规则",
            ["GongdouStageRulePower.description"] = "当前战斗使用共斗尖塔残局第 {Amount} 关规则。请按准备界面和题面限制完成挑战。",
            ["GongdouPersistentArmorRulePower.title"] = "保留格挡",
            ["GongdouPersistentArmorRulePower.description"] = "格挡不会在回合开始时被移除。",
            ["GongdouPersistentArmorKeywordPower.title"] = "保留格挡",
            ["GongdouPersistentArmorKeywordPower.description"] = "格挡不会在回合开始时被移除。",
            ["GongdouD3DexterityKeywordPower.title"] = "奇巧",
            ["GongdouD3DexterityKeywordPower.description"] = "带有奇巧的牌在你的回合结束前从手牌被弃掉时，会按原版奇巧流程免费打出。",
            ["GongdouD3BellLimitKeywordPower.title"] = "铃鸣",
            ["GongdouD3BellLimitKeywordPower.description"] = "每回合最多主动打出2张牌。因奇巧免费打出的牌不计入此限制。",
            ["GongdouD3StickyHandKeywordPower.title"] = "缠手",
            ["GongdouD3StickyHandKeywordPower.description"] = "每回合只能通过预备、生存者或奇巧药水丢弃1张牌。",
            ["GongdouD3FalseBladeKeywordPower.title"] = "伪刃",
            ["GongdouD3FalseBladeKeywordPower.description"] = "只有奇巧牌会因丢弃免费打出。丢弃非奇巧牌会触发误弃。",
            ["GongdouD3ReadKeywordPower.title"] = "识破",
            ["GongdouD3ReadKeywordPower.description"] = "本回合若你直接打出过奇巧牌，你的回合结束时，该敌人获得10点保留格挡。",
            ["GongdouD3WrongDiscardArmorKeywordPower.title"] = "误弃",
            ["GongdouD3WrongDiscardArmorKeywordPower.description"] = "本回合若你丢弃过非奇巧牌，你的回合结束时，该敌人获得8点保留格挡。",
            ["GongdouD3ChainBreachKeywordPower.title"] = "连环破绽",
            ["GongdouD3ChainBreachKeywordPower.description"] = "连续3个玩家回合触发奇巧后，下个回合开始时敌人受到40点固定伤害，该伤害不受格挡影响。",
            ["GongdouD3NoDexterityArmorKeywordPower.title"] = "无巧可乘",
            ["GongdouD3NoDexterityArmorKeywordPower.description"] = "本回合若没有牌因奇巧免费打出，你的回合结束时，该敌人获得{Amount}点保留格挡。",
            ["GongdouD4BurnKeywordPower.title"] = "灼伤洗牌",
            ["GongdouD4BurnKeywordPower.description"] = "本回合结束时，敌人将{Amount}张[gold]灼伤[/gold]加入你的弃牌堆。",
            ["GongdouD4CountdownKeywordPower.title"] = "状态解锁",
            ["GongdouD4CountdownKeywordPower.description"] = "状态牌或诅咒牌进入你的牌堆后，顺劈斩（改）稳定解锁强化。",
            ["GongdouD4OverheatKeywordPower.title"] = "过热裂解旧规则",
            ["GongdouD4OverheatKeywordPower.description"] = "本规则不再在第4关应用。",
            ["GongdouD4FireBreathingPower.title"] = "火焰吐息",
            ["GongdouD4FireBreathingPower.description"] = "每当抽到[gold]状态[/gold]牌或[gold]诅咒[/gold]牌时，对所有敌人造成6点伤害。",
            ["GongdouD4EvolvePower.title"] = "进化",
            ["GongdouD4EvolvePower.description"] = "每当抽到[gold]状态[/gold]牌时，抽1张牌。",
            ["GongdouCardKeyword.Status.title"] = "状态",
            ["GongdouCardKeyword.Status.description"] = "状态牌通常不能主动打出，会污染抽牌与手牌，并可能在抽到或回合结束时触发效果。",
            ["GongdouCardKeyword.Curse.title"] = "诅咒",
            ["GongdouCardKeyword.Curse.description"] = "诅咒牌通常不能主动打出，会污染牌组，并带来负面效果。",
            ["GongdouCardKeyword.Wound.title"] = "伤口",
            ["GongdouCardKeyword.Wound.description"] = "状态牌。不能打出，没有额外效果。",
            ["GongdouCardKeyword.Dazed.title"] = "晕眩",
            ["GongdouCardKeyword.Dazed.description"] = "状态牌。不能打出，回合结束时消耗。",
            ["GongdouCardKeyword.Burn.title"] = "灼伤",
            ["GongdouCardKeyword.Burn.description"] = "状态牌。回合结束时若在手牌中，失去2点生命。",
            ["GongdouCardKeyword.Void.title"] = "虚空",
            ["GongdouCardKeyword.Void.description"] = "状态牌。不可打出，抽到时本回合失去1点能量。回合结束进入弃牌堆。",
            ["GongdouCardKeyword.Cunning.title"] = "奇巧",
            ["GongdouCardKeyword.Cunning.description"] = "如果这张牌在你的回合结束前从手牌中被丢弃，则免费将其打出。",
            ["GongdouCardKeyword.Stance.title"] = "姿态",
            ["GongdouCardKeyword.Stance.description"] = "姿态分为普通、平静、愤怒。",
            ["GongdouCardKeyword.Calm.title"] = "平静",
            ["GongdouCardKeyword.Calm.description"] = "离开平静时获得2点能量。",
            ["GongdouCardKeyword.Wrath.title"] = "愤怒",
            ["GongdouCardKeyword.Wrath.description"] = "玩家攻击伤害翻倍，敌人攻击伤害也翻倍。",
            ["GongdouCardKeyword.Normal.title"] = "普通",
            ["GongdouCardKeyword.Normal.description"] = "没有额外姿态修正。",
            ["GongdouCardKeyword.Vulnerable.title"] = "易伤",
            ["GongdouCardKeyword.Vulnerable.description"] = "受到攻击伤害增加。",
            ["GongdouCardKeyword.Weak.title"] = "虚弱",
            ["GongdouCardKeyword.Weak.description"] = "造成的攻击伤害降低。",
            ["GongdouCardKeyword.Poison.title"] = "毒",
            ["GongdouCardKeyword.Poison.description"] = "敌人回合开始前，敌人失去等同于当前毒层的生命。毒伤害不受格挡阻挡。之后毒层减少1。",
            ["GongdouCardKeyword.Block.title"] = "格挡",
            ["GongdouCardKeyword.Block.description"] = "抵消即将受到的攻击伤害。",
            ["GongdouCardKeyword.Armor.title"] = "保留格挡",
            ["GongdouCardKeyword.Armor.description"] = "格挡不会在回合开始时被移除。",
            ["GongdouCardKeyword.Strength.title"] = "力量",
            ["GongdouCardKeyword.Strength.description"] = "提高攻击造成的伤害。",
            ["GongdouCardKeyword.Energy.title"] = "能量",
            ["GongdouCardKeyword.Energy.description"] = "用于支付卡牌费用。",
            ["GongdouCardKeyword.Exhaust.title"] = "消耗",
            ["GongdouCardKeyword.Exhaust.description"] = "本场战斗中移除这张牌。",
            ["GongdouCardKeyword.RetainedBlock.title"] = "保留格挡",
            ["GongdouCardKeyword.RetainedBlock.description"] = "格挡不会在回合开始时被移除。",
            ["GongdouCardKeyword.FixedDamage.title"] = "固定伤害",
            ["GongdouCardKeyword.FixedDamage.description"] = "不受普通伤害增减修正影响的伤害。",
            ["GongdouCardKeyword.DelayedDamage.title"] = "延迟伤害",
            ["GongdouCardKeyword.DelayedDamage.description"] = "延迟伤害会在下回合开始时结算。",
            ["GongdouCardKeyword.Orb.title"] = "充能球",
            ["GongdouCardKeyword.Orb.description"] = "进入充能球槽后，会在被动或释放时触发对应效果。",
            ["GongdouCardKeyword.Lightning.title"] = "闪电",
            ["GongdouCardKeyword.Lightning.description"] = "一种充能球，会造成伤害。",
            ["GongdouCardKeyword.Frost.title"] = "冰霜",
            ["GongdouCardKeyword.Frost.description"] = "一种充能球，会提供格挡。",
            ["GongdouCardKeyword.Dark.title"] = "黑暗",
            ["GongdouCardKeyword.Dark.description"] = "一种充能球，会储存伤害并在释放时造成累计伤害。",
            ["GongdouCardKeyword.Focus.title"] = "集中",
            ["GongdouCardKeyword.Focus.description"] = "提高充能球的被动和释放效果。",
            ["GongdouCardKeyword.Loop.title"] = "循环",
            ["GongdouCardKeyword.Loop.description"] = "循环（改）生效后，回合结束时最左侧充能球额外触发1次被动效果。",
            ["GongdouCardKeyword.Charge.title"] = "充能",
            ["GongdouCardKeyword.Charge.description"] = "部分牌会根据充能提高伤害或格挡。充能不会在回合结束时失去。",
            ["GongdouCardKeyword.Echo.title"] = "回声",
            ["GongdouCardKeyword.Echo.description"] = "下一次攻击造成的伤害翻倍。",
            ["GongdouCardKeyword.Overheat.title"] = "过热",
            ["GongdouCardKeyword.Overheat.description"] = "每层过热使敌人攻击额外造成4点伤害。过热不会在回合结束时失去。",
            ["GongdouCardKeyword.Mark.title"] = "标记",
            ["GongdouCardKeyword.Mark.description"] = "部分牌会根据敌人的标记提高伤害或斩杀门槛。标记不会在回合结束时失去。",
            ["GongdouCardKeyword.Mantra.title"] = "真言",
            ["GongdouCardKeyword.Mantra.description"] = "达到10点时进入神格。",
            ["GongdouCardKeyword.Divinity.title"] = "神格",
            ["GongdouCardKeyword.Divinity.description"] = "本回合你的攻击伤害变为3倍。",
            ["GongdouD5OverloadLockKeywordPower.title"] = "过载锁",
            ["GongdouD5OverloadLockKeywordPower.description"] = "每回合最多主动打出 2 张牌。",
            ["GongdouD5VoidKeywordPower.title"] = "虚空",
            ["GongdouD5VoidKeywordPower.description"] = "不可打出，抽到时本回合失去1点能量。回合结束进入弃牌堆。",
            ["GongdouD5VoidCollapseKeywordPower.title"] = "虚空坍缩",
            ["GongdouD5VoidCollapseKeywordPower.description"] = "如果本场战斗累计抽到过至少 2 张虚空，第 5 回合开始时敌人失去 36 点生命，不受格挡影响。",
            ["GongdouD5ArtifactKeywordPower.title"] = "人工制品",
            ["GongdouD5ArtifactKeywordPower.description"] = "敌人拥有 3 次人工制品抵消，会抵消易伤或虚弱等负面效果。",
            ["GongdouD5PotionPhaseKeywordPower.title"] = "药水相位",
            ["GongdouD5PotionPhaseKeywordPower.description"] = "早于5回合之前的致命伤害会让该敌人降至1点生命。",
            ["GongdouD5PotionPhaseTurn3KeywordPower.title"] = "药水相位",
            ["GongdouD5PotionPhaseTurn3KeywordPower.description"] = "早于3回合之前的致命伤害会让该敌人降至1点生命。",
            ["GongdouD5PotionPhaseTurn4KeywordPower.title"] = "药水相位",
            ["GongdouD5PotionPhaseTurn4KeywordPower.description"] = "早于4回合之前的致命伤害会让该敌人降至1点生命。",
            ["GongdouD5PotionPhaseTurn5KeywordPower.title"] = "药水相位",
            ["GongdouD5PotionPhaseTurn5KeywordPower.description"] = "早于5回合之前的致命伤害会让该敌人降至1点生命。",
            ["GongdouD6StanceKeywordPower.title"] = "姿态",
            ["GongdouD6StanceKeywordPower.description"] = "姿态分为普通、平静、愤怒。初始为普通。",
            ["GongdouD6NormalKeywordPower.title"] = "普通",
            ["GongdouD6NormalKeywordPower.description"] = "当前处于普通姿态，没有额外伤害修正。",
            ["GongdouD6WrathKeywordPower.title"] = "愤怒",
            ["GongdouD6WrathKeywordPower.description"] = "玩家攻击伤害翻倍。敌人攻击伤害也翻倍。",
            ["GongdouD6CalmKeywordPower.title"] = "平静",
            ["GongdouD6CalmKeywordPower.description"] = "离开平静时获得2点能量。",
            ["GongdouD6CalmBreachKeywordPower.title"] = "平静破绽",
            ["GongdouD6CalmBreachKeywordPower.description"] = "若你本场战斗累计离开平静至少 2 次，第 4 回合开始时敌人失去 28 点生命，不受格挡影响。",
            ["GongdouD7PoisonKeywordPower.title"] = "毒",
            ["GongdouD7PoisonKeywordPower.description"] = "敌人回合开始前，敌人失去等同于当前毒层的生命，之后毒层减少1，毒伤害不受格挡阻挡。",
            ["GongdouD7PoisonFogKeywordPower.title"] = "毒雾",
            ["GongdouD7PoisonFogKeywordPower.description"] = "本场战斗中，每回合开始时给予敌人3层毒。",
            ["GongdouD7CaltropsKeywordPower.title"] = "铁蒺藜",
            ["GongdouD7CaltropsKeywordPower.description"] = "本场战斗中，每当敌人攻击时，造成6点反击伤害。",
            ["GongdouD7AntidoteKeywordPower.title"] = "解毒腺",
            ["GongdouD7AntidoteKeywordPower.description"] = "本回合敌人受到毒伤害后会移除{Amount}层毒。回合结束时，获得{Block}点保留格挡。",
            ["GongdouD8OrbSlotKeywordPower.title"] = "充能球槽",
            ["GongdouD8OrbSlotKeywordPower.description"] = "战斗开始时生成1个闪电和1个冰霜充能球。最多容纳3个充能球。充能球满时再生成充能球，会先释放最左侧充能球。",
            ["GongdouD8LightningKeywordPower.title"] = "闪电",
            ["GongdouD8LightningKeywordPower.description"] = "回合结束被动造成3+集中伤害。释放时造成8+集中伤害。",
            ["GongdouD8FrostKeywordPower.title"] = "冰霜",
            ["GongdouD8FrostKeywordPower.description"] = "回合结束被动获得2+集中格挡。释放时获得7+集中格挡。",
            ["GongdouD8DarkKeywordPower.title"] = "黑暗",
            ["GongdouD8DarkKeywordPower.description"] = "回合结束被动使自身储值增加6+集中。释放时造成等同储值的伤害。",
            ["GongdouD8LoopKeywordPower.title"] = "循环",
            ["GongdouD8LoopKeywordPower.description"] = "循环（改）生效后，回合结束时最左侧充能球额外触发1次被动效果。",
            ["GongdouD8FocusKeywordPower.title"] = "集中",
            ["GongdouD8FocusKeywordPower.description"] = "影响闪电、冰霜、黑暗的数值。",
            ["GongdouD8InsulationKeywordPower.title"] = "绝缘破裂",
            ["GongdouD8InsulationKeywordPower.description"] = "每当你通过冰霜充能球获得格挡，敌人失去等量生命，该生命流失不受格挡阻挡。",
            ["GongdouD9MirrorDrawKeywordPower.title"] = "镜像抽取",
            ["GongdouD9MirrorDrawKeywordPower.description"] = "每回合从镜像牌组中随机抽4张。回合开始时获得1点真言。回合结束后镜像重置，下回合仍从完整镜像牌组抽取。",
            ["GongdouD9MantraKeywordPower.title"] = "真言",
            ["GongdouD9MantraKeywordPower.description"] = "当前真言：{Amount}/10。达到10时进入神格。",
            ["GongdouD9DivinityKeywordPower.title"] = "神格",
            ["GongdouD9DivinityKeywordPower.description"] = "本回合你的攻击伤害变为3倍。",
            ["GongdouD9ExhaustKeywordPower.title"] = "消耗",
            ["GongdouD9ExhaustKeywordPower.description"] = "本题为镜像牌组，消耗只影响当前回合手牌，不会使该牌从后续回合的镜像抽取中永久移除。",
            ["GongdouD9ArmorReflectKeywordPower.title"] = "护甲反射",
            ["GongdouD9ArmorReflectKeywordPower.description"] = "玩家在非神格下攻击时，若该次攻击的基础结算伤害至少为12，则该敌人获得10格挡。",
            ["GongdouD9EnemyArmorKeywordPower.title"] = "保留格挡",
            ["GongdouD9EnemyArmorKeywordPower.description"] = "敌人的格挡不会在回合开始时被移除。攻击牌先扣格挡，再扣生命。",
            ["GongdouD10RiftDrawKeywordPower.title"] = "裂隙抽取",
            ["GongdouD10RiftDrawKeywordPower.description"] = "本回合从裂隙牌组中抽取{DrawCount}张牌。回合开始时获得1点充能。偶数回合开始时给予敌人1层标记。回合结束时，裂隙牌组重置。",
            ["GongdouD10ChargeKeywordPower.title"] = "充能",
            ["GongdouD10ChargeKeywordPower.description"] = "你的部分牌会根据充能提高数值。当前拥有{Charge}点充能。",
            ["GongdouD10EchoKeywordPower.title"] = "回声",
            ["GongdouD10EchoKeywordPower.description"] = "你的下{Echo}次攻击造成的伤害翻倍。",
            ["GongdouD10OverheatKeywordPower.title"] = "过热",
            ["GongdouD10OverheatKeywordPower.description"] = "敌人的攻击额外造成{BonusDamage}点伤害。",
            ["GongdouD10PhaseGateKeywordPower.title"] = "相位锁定",
            ["GongdouD10PhaseGateKeywordPower.description"] = "第{PhaseTurn}回合前，敌人不会被击杀。致命伤害会使其生命值变为1点。",
            ["GongdouD10MarkKeywordPower.title"] = "标记",
            ["GongdouD10MarkKeywordPower.description"] = "你的部分牌会根据标记提高效果。当前有{Mark}层标记。",
            ["GongdouD10DelayedDamageKeywordPower.title"] = "延迟爆破",
            ["GongdouD10DelayedDamageKeywordPower.description"] = "下回合开始时，受到{DelayedDamage}点伤害。",
            ["GongdouD10EnemyArmorKeywordPower.title"] = "保留格挡",
            ["GongdouD10EnemyArmorKeywordPower.description"] = "格挡不会在回合开始时被移除。",
            ["GongdouD10MarkDecayKeywordPower.title"] = "标记衰减",
            ["GongdouD10MarkDecayKeywordPower.description"] = "本回合结束时，敌人失去最多3层标记。",
            ["GongdouD10ArmorChargeKeywordPower.title"] = "蓄甲",
            ["GongdouD10ArmorChargeKeywordPower.description"] = "本回合结束时，敌人获得32点保留格挡。",
            ["GongdouD10ResonatorKeywordPower.title"] = "谐振器",
            ["GongdouD10ResonatorKeywordPower.description"] = "下一次延迟爆破的延迟伤害增加12点。",
            ["GongdouD10PrismShardKeywordPower.title"] = "棱镜碎片",
            ["GongdouD10PrismShardKeywordPower.description"] = "给予敌人标记时，多给予1层。",
            ["GongdouD10MarkResonanceKeywordPower.title"] = "标记共鸣",
            ["GongdouD10MarkResonanceKeywordPower.description"] = "你的攻击额外造成4点伤害。"
        });
        MergeLoc("monsters", new Dictionary<string, string>
        {
            ["GongdouCunningBellEnemy.name"] = "伪刃铃卫",
            ["GongdouBurnCountdownEnemy.name"] = "灼伤洗牌守卫",
            ["GongdouVoidLockEnemy.name"] = "虚空锁哨卫",
            ["GongdouStanceBreachEnemy.name"] = "怒火越界者",
            ["GongdouPoisonAntidoteEnemy.name"] = "解毒腺异兽",
            ["GongdouOrbOverloadEnemy.name"] = "绝缘破裂体",
            ["GongdouDivinityMirrorEnemy.name"] = "破镜神格像",
            ["GongdouTimeRiftEnemy.name"] = "时间裂隙核心",
            ["GongdouChallengeEnemy.name"] = "尖塔残局守门人"
        });
    }

    private static void AddResourceLoc(
        Dictionary<string, string> entries,
        Models.ResourceItem item,
        bool includeFlavor = false,
        bool colorizeCardKeywords = false)
    {
        if (VanillaResourceLocalizationIds.Contains(NormalizeResourceId(item.Id)))
        {
            return;
        }

        var modelKey = ToGongdouModelKey(item.Id);
        var description = colorizeCardKeywords ? ColorizeCardKeywordTerms(item.Description) : item.Description;
        entries[$"{modelKey}.title"] = item.Name;
        entries[$"{modelKey}.description"] = description;
        if (includeFlavor)
        {
            entries[$"{modelKey}.flavor"] = description;
        }

        var runtimeKey = ToRuntimeGongdouKey(item.Id);
        entries[$"{runtimeKey}.title"] = item.Name;
        entries[$"{runtimeKey}.description"] = description;
        if (includeFlavor)
        {
            entries[$"{runtimeKey}.flavor"] = description;
        }
    }

    private static string ColorizeCardKeywordTerms(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        foreach (var term in CardKeywordTermsToColor.OrderByDescending(static term => term.Length))
        {
            description = ReplaceOutsideGoldTags(description, term);
        }

        return description;
    }

    private static string ReplaceOutsideGoldTags(string description, string term)
    {
        var result = new System.Text.StringBuilder(description.Length + 16);
        var index = 0;
        while (index < description.Length)
        {
            var termIndex = description.IndexOf(term, index, StringComparison.Ordinal);
            if (termIndex < 0)
            {
                result.Append(description, index, description.Length - index);
                break;
            }

            var goldStart = description.IndexOf("[gold]", index, StringComparison.Ordinal);
            if (goldStart >= 0 && goldStart <= termIndex)
            {
                var goldEnd = description.IndexOf("[/gold]", goldStart, StringComparison.Ordinal);
                if (goldEnd < 0)
                {
                    result.Append(description, index, description.Length - index);
                    break;
                }

                var goldBlockEnd = goldEnd + "[/gold]".Length;
                result.Append(description, index, goldBlockEnd - index);
                index = goldBlockEnd;
                continue;
            }

            result.Append(description, index, termIndex - index);
            result.Append("[gold]").Append(term).Append("[/gold]");
            index = termIndex + term.Length;
        }

        return result.ToString();
    }

    private static string ToGongdouModelKey(string id)
    {
        var normalized = NormalizeResourceId(id);
        if (CustomResourceModelKeys.TryGetValue(normalized, out var customKey))
        {
            return customKey;
        }

        return normalized.StartsWith("D", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "FlyingSword", StringComparison.OrdinalIgnoreCase)
            ? $"Gongdou{normalized}"
            : normalized;
    }

    private static string ToRuntimeGongdouKey(string id)
    {
        var normalized = NormalizeResourceId(id);
        return $"GONGDOU_{ToUpperSnakeCase(normalized)}";
    }

    private static string NormalizeResourceId(string id)
    {
        return id.Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Trim();
    }

    private static string ToUpperSnakeCase(string value)
    {
        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0)
            {
                var previous = value[i - 1];
                var next = i + 1 < value.Length ? value[i + 1] : '\0';
                if (char.IsUpper(current) &&
                    (char.IsLower(previous) || (char.IsDigit(previous) && char.IsLetter(next))))
                {
                    chars.Add('_');
                }
            }

            chars.Add(char.ToUpperInvariant(current));
        }

        return new string(chars.ToArray());
    }

    private static void RegisterLocalization()
    {
        MergeLoc("card_selection", new Dictionary<string, string>
        {
            ["GONGDOU_SELECT_CARDS"] = "选择卡牌：0/x"
        });

        MergeLoc("cards", new Dictionary<string, string>
        {
            ["GongdouSkewer7.title"] = "串刺",
            ["GongdouSkewer7.description"] = "造成 7 点伤害 X 次。",
            ["GongdouCatalyst.title"] = "致命毒药（改）",
            ["GongdouCatalyst.description"] = "使敌人的中毒层数变为 3 倍。消耗。",
            ["GongdouPreciseStab.title"] = "精准刺击",
            ["GongdouPreciseStab.description"] = "造成12点伤害。如果敌人有易伤，改为17点伤害。",
            ["GongdouArmorBreakSlash.title"] = "破甲斩",
            ["GongdouArmorBreakSlash.description"] = "造成 18 点伤害。施加 1 层易伤。",
            ["GongdouHeavySwing.title"] = "沉重挥击",
            ["GongdouHeavySwing.description"] = "造成24点伤害。本回合每打出过1张攻击牌，伤害-6。",
            ["GongdouDoubleHit.title"] = "双连击",
            ["GongdouDoubleHit.description"] = "造成 6 点伤害 2 次。",
            ["GongdouOpportunityStrike.title"] = "机会打击",
            ["GongdouOpportunityStrike.description"] = "造成6点伤害。如果这是本回合打出的第1张牌，额外造成6点伤害。",
            ["GongdouPoisonDagger.title"] = "剧毒匕首",
            ["GongdouPoisonDagger.description"] = "造成 5 点伤害。施加 4 层中毒。",
            ["GongdouPoisonFog.title"] = "毒雾",
            ["GongdouPoisonFog.description"] = "敌人每回合开始时获得 3 层中毒。",
            ["GongdouCorrosiveSalve.title"] = "腐蚀药膏",
            ["GongdouCorrosiveSalve.description"] = "施加 10 层中毒。消耗。",
            ["GongdouBattleCry.title"] = "战吼",
            ["GongdouBattleCry.description"] = "获得 1 点力量。抽 1 张牌，然后弃 1 张牌。",
            ["GongdouBattleCry.selectionScreenPrompt"] = "弃掉 1 张牌。",
            ["GongdouPreparedStance.title"] = "预备姿态",
            ["GongdouPreparedStance.description"] = "选择最多 1 张手牌保留到下回合。消耗。",
            ["GongdouPreparedStance.selectionScreenPrompt"] = "选择最多 1 张手牌保留到下回合。",
            ["GongdouBloodRush.title"] = "燃血冲锋",
            ["GongdouBloodRush.description"] = "失去 4 生命。造成 20 点伤害。",
            ["GongdouBluff.title"] = "虚张声势",
            ["GongdouBluff.description"] = "施加 2 层易伤。下回合敌人获得 8 格挡。"
        });

        MergeLoc("potions", new Dictionary<string, string>
        {
            ["GongdouPoisonPotion12.title"] = "毒药水",
            ["GongdouPoisonPotion12.description"] = "施加 12 层中毒。",
            ["GongdouBurstPotion.title"] = "爆炸安瓿",
            ["GongdouBurstPotion.description"] = "本回合下一张攻击牌额外造成 10 点伤害。"
        });

        MergeLoc("relics", new Dictionary<string, string>
        {
            ["GongdouTacticalMemoClip.title"] = "战术备忘夹",
            ["GongdouTacticalMemoClip.description"] = "每回合结束时可保留 1 张手牌。",
            ["GongdouTacticalMemoClip.flavor"] = "给跨回合规划留一点余地。",
            ["GongdouCrackedBladeCharm.title"] = "裂刃护符",
            ["GongdouCrackedBladeCharm.description"] = "每场战斗第一张攻击牌额外造成 8 点伤害。",
            ["GongdouCrackedBladeCharm.flavor"] = "只锋利一次也够吓人。",
            ["GongdouPoisonSac.title"] = "毒囊",
            ["GongdouPoisonSac.description"] = "每场战斗第一张攻击牌额外施加 3 层中毒。",
            ["GongdouPoisonSac.flavor"] = "剂量很足，但没到离谱。",
            ["GongdouBloodSigil.title"] = "燃血徽记",
            ["GongdouBloodSigil.description"] = "每次失去生命后，本回合下一张攻击牌额外造成 4 点伤害。",
            ["GongdouBloodSigil.flavor"] = "疼痛换来一点火力。",
            ["GongdouOldWhetstone.title"] = "旧磨刀石",
            ["GongdouOldWhetstone.description"] = "每回合第一张 1 费攻击牌额外造成 3 点伤害。",
            ["GongdouOldWhetstone.flavor"] = "老东西，能用。",
            ["GongdouSlowWarmCore.title"] = "慢热核心",
            ["GongdouSlowWarmCore.description"] = "第 3 回合开始时获得 1 点力量和 1 点能量。",
            ["GongdouSlowWarmCore.flavor"] = "启动有点慢。"
        });

        MergeLoc("powers", new Dictionary<string, string>
        {
            ["GongdouPoisonFogPower.title"] = "毒雾",
            ["GongdouPoisonFogPower.description"] = "敌人每回合开始时获得 {Amount} 层中毒。",
            ["GongdouNextTurnBlockPower.title"] = "虚张声势",
            ["GongdouNextTurnBlockPower.description"] = "下回合开始时获得 {Amount} 点格挡。",
            ["GongdouBurstPotionPower.title"] = "爆炸安瓿",
            ["GongdouBurstPotionPower.description"] = "本回合下一张攻击牌额外造成 {Amount} 点伤害。"
        });

        MergeLoc("monsters", new Dictionary<string, string>
        {
            ["GongdouBrokenTrainingDummyMonster.name"] = "巨斧机器人"
        });
    }

    private static void MergeLoc(string table, Dictionary<string, string> entries)
    {
        try
        {
            if (string.Equals(table, "cards", StringComparison.OrdinalIgnoreCase))
            {
                entries = entries.ToDictionary(
                    pair => pair.Key,
                    pair => ShouldCleanCardLoc(pair.Key) ? CleanCardLocText(pair.Value) : pair.Value);
            }

            entries = AddCanonicalLocAliases(entries);
            LocManager.Instance.GetTable(table).MergeWith(entries);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to merge localization table {table}: {ex.Message}");
        }
    }

    private static Dictionary<string, string> AddCanonicalLocAliases(Dictionary<string, string> entries)
    {
        var result = new Dictionary<string, string>(entries, StringComparer.Ordinal);
        foreach (var pair in entries)
        {
            var dot = pair.Key.IndexOf('.', StringComparison.Ordinal);
            if (dot <= 0)
            {
                continue;
            }

            var modelKey = pair.Key[..dot];
            if (!modelKey.StartsWith("Gongdou", StringComparison.Ordinal))
            {
                continue;
            }

            var alias = ToCanonicalGongdouLocKey(modelKey) + pair.Key[dot..];
            result.TryAdd(alias, pair.Value);
        }

        return result;
    }

    private static string ToCanonicalGongdouLocKey(string modelKey)
    {
        var raw = modelKey["Gongdou".Length..];
        if (raw.Length == 0)
        {
            return "GONGDOU";
        }

        var builder = new StringBuilder("GONGDOU_");
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (i > 0 && char.IsUpper(c))
            {
                var prev = raw[i - 1];
                var nextIsLower = i + 1 < raw.Length && char.IsLower(raw[i + 1]);
                if (char.IsLower(prev) || char.IsDigit(prev) || (char.IsUpper(prev) && nextIsLower))
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    private static bool ShouldCleanCardLoc(string key)
    {
        return key.EndsWith(".description", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith(".selectionScreenPrompt", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanCardLocText(string text)
    {
        return string.Join(
            "\n",
            text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => NormalizeChallengeCardLocLine(RemoveLeadingCardCost(line)))
                .Where(line => line.Length > 0));
    }

    private static string NormalizeChallengeCardLocLine(string line)
    {
        var normalized = line
            .Replace("；", "。", StringComparison.Ordinal)
            .Replace(";", "。", StringComparison.Ordinal)
            .Trim();

        var suffixes = new[]
        {
            "。不抽牌、不弃牌。",
            "。不抽牌，不弃牌。",
            "。不抽牌。",
            "。不弃牌。",
            "。不保留。",
            "。不消耗。",
            "。消耗。"
        };

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var suffix in suffixes)
            {
                if (normalized.EndsWith(suffix, StringComparison.Ordinal))
                {
                    normalized = normalized[..^suffix.Length] + "。";
                    changed = true;
                }
            }
        }

        return normalized;
    }

    private static string RemoveLeadingCardCost(string line)
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

    private static void RegisterReadableLocalization()
    {
        MergeLoc("card_selection", new Dictionary<string, string>
        {
            ["GONGDOU_SELECT_CARDS"] = "选择卡牌：0/x"
        });

        MergeLoc("cards", new Dictionary<string, string>
        {
            ["GongdouSkewer7.title"] = "串刺",
            ["GongdouSkewer7.description"] = "造成 7 点伤害 X 次。",
            ["GongdouCatalyst.title"] = "致命毒药（改）",
            ["GongdouCatalyst.description"] = "使敌人的中毒层数变为 3 倍。消耗。",
            ["GongdouPreciseStab.title"] = "精准刺击",
            ["GongdouPreciseStab.description"] = "造成12点伤害。如果敌人有易伤，改为17点伤害。",
            ["GongdouArmorBreakSlash.title"] = "破甲斩",
            ["GongdouArmorBreakSlash.description"] = "造成 18 点伤害。施加 1 层易伤。",
            ["GongdouHeavySwing.title"] = "沉重挥击",
            ["GongdouHeavySwing.description"] = "造成24点伤害。本回合每打出过1张攻击牌，伤害-6。",
            ["GongdouDoubleHit.title"] = "双连击",
            ["GongdouDoubleHit.description"] = "造成 6 点伤害 2 次。",
            ["GongdouOpportunityStrike.title"] = "机会打击",
            ["GongdouOpportunityStrike.description"] = "造成6点伤害。如果这是本回合打出的第1张牌，额外造成6点伤害。",
            ["GongdouPoisonDagger.title"] = "剧毒匕首",
            ["GongdouPoisonDagger.description"] = "造成 5 点伤害。施加 4 层中毒。",
            ["GongdouPoisonFog.title"] = "毒雾",
            ["GongdouPoisonFog.description"] = "敌人每回合开始时获得 3 层中毒。",
            ["GongdouCorrosiveSalve.title"] = "腐蚀药膏",
            ["GongdouCorrosiveSalve.description"] = "施加 10 层中毒。消耗。",
            ["GongdouBattleCry.title"] = "战吼",
            ["GongdouBattleCry.description"] = "获得 1 点力量。抽 1 张牌，然后弃 1 张牌。",
            ["GongdouBattleCry.selectionScreenPrompt"] = "弃掉 1 张牌。",
            ["GongdouPreparedStance.title"] = "预备姿态",
            ["GongdouPreparedStance.description"] = "选择最多 1 张手牌保留到下回合。消耗。",
            ["GongdouPreparedStance.selectionScreenPrompt"] = "选择最多 1 张手牌保留到下回合。",
            ["GongdouBloodRush.title"] = "燃血冲锋",
            ["GongdouBloodRush.description"] = "失去 4 生命。造成 20 点伤害。",
            ["GongdouBluff.title"] = "虚张声势",
            ["GongdouBluff.description"] = "施加 2 层易伤。下回合敌人获得 8 格挡。"
        });

        MergeLoc("potions", new Dictionary<string, string>
        {
            ["GongdouPoisonPotion12.title"] = "毒药水",
            ["GongdouPoisonPotion12.description"] = "施加 12 层中毒。",
            ["GongdouBurstPotion.title"] = "爆炸安瓿",
            ["GongdouBurstPotion.description"] = "本回合下一张攻击牌额外造成 10 点伤害。"
        });

        MergeLoc("relics", new Dictionary<string, string>
        {
            ["GongdouTacticalMemoClip.title"] = "战术备忘夹",
            ["GongdouTacticalMemoClip.description"] = "每回合结束时可保留 1 张手牌。",
            ["GongdouTacticalMemoClip.flavor"] = "给跨回合规划留一点余地。",
            ["GongdouCrackedBladeCharm.title"] = "裂刃护符",
            ["GongdouCrackedBladeCharm.description"] = "每场战斗第一张攻击牌额外造成 8 点伤害。",
            ["GongdouCrackedBladeCharm.flavor"] = "只锋利一次也够吓人。",
            ["GongdouPoisonSac.title"] = "毒囊",
            ["GongdouPoisonSac.description"] = "每场战斗第一张攻击牌额外施加 3 层中毒。",
            ["GongdouPoisonSac.flavor"] = "剂量很足，但没到离谱。",
            ["GongdouBloodSigil.title"] = "燃血徽记",
            ["GongdouBloodSigil.description"] = "每次失去生命后，本回合下一张攻击牌额外造成 4 点伤害。",
            ["GongdouBloodSigil.flavor"] = "疼痛换来一点火力。",
            ["GongdouOldWhetstone.title"] = "旧磨刀石",
            ["GongdouOldWhetstone.description"] = "每回合第一张 1 费攻击牌额外造成 3 点伤害。",
            ["GongdouOldWhetstone.flavor"] = "老东西，能用。",
            ["GongdouSlowWarmCore.title"] = "慢热核心",
            ["GongdouSlowWarmCore.description"] = "第 3 回合开始时获得 1 点力量和 1 点能量。",
            ["GongdouSlowWarmCore.flavor"] = "启动有点慢。"
        });

        MergeLoc("powers", new Dictionary<string, string>
        {
            ["GongdouPoisonFogPower.title"] = "毒雾",
            ["GongdouPoisonFogPower.description"] = "敌人每回合开始时获得 {Amount} 层中毒。",
            ["GongdouNextTurnBlockPower.title"] = "虚张声势",
            ["GongdouNextTurnBlockPower.description"] = "下回合开始时获得 {Amount} 点格挡。",
            ["GongdouBurstPotionPower.title"] = "爆炸安瓿",
            ["GongdouBurstPotionPower.description"] = "本回合下一张攻击牌额外造成 {Amount} 点伤害。"
        });

        MergeLoc("monsters", new Dictionary<string, string>
        {
            ["GongdouBrokenTrainingDummyMonster.name"] = "巨斧机器人"
        });
    }

    private static void RegisterRuntimeLocalizationAliases()
    {
        MergeLoc("cards", new Dictionary<string, string>
        {
            ["GONGDOU_SELECT_CARDS"] = "选择卡牌：0/x",
            ["GongdouD5Void.title"] = "虚空",
            ["GongdouD5Void.description"] = "不可打出，抽到时本回合失去1点能量。\n回合结束进入弃牌堆。",
            ["GONGDOU_D5_VOID.title"] = "虚空",
            ["GONGDOU_D5_VOID.description"] = "不可打出，抽到时本回合失去1点能量。\n回合结束进入弃牌堆。",
            ["GONGDOU_SKEWER7.title"] = "串刺",
            ["GONGDOU_SKEWER7.description"] = "造成 7 点伤害 X 次。",
            ["GONGDOU_CATALYST.title"] = "致命毒药（改）",
            ["GONGDOU_CATALYST.description"] = "使敌人的中毒层数变为 3 倍。消耗。",
            ["GONGDOU_PRECISE_STAB.title"] = "精准刺击",
            ["GONGDOU_PRECISE_STAB.description"] = "造成12点伤害。如果敌人有易伤，改为17点伤害。",
            ["GONGDOU_ARMOR_BREAK_SLASH.title"] = "破甲斩",
            ["GONGDOU_ARMOR_BREAK_SLASH.description"] = "造成 18 点伤害。施加 1 层易伤。",
            ["GONGDOU_HEAVY_SWING.title"] = "沉重挥击",
            ["GONGDOU_HEAVY_SWING.description"] = "造成24点伤害。本回合每打出过1张攻击牌，伤害-6。",
            ["GONGDOU_DOUBLE_HIT.title"] = "双连击",
            ["GONGDOU_DOUBLE_HIT.description"] = "造成 6 点伤害 2 次。",
            ["GONGDOU_OPPORTUNITY_STRIKE.title"] = "机会打击",
            ["GONGDOU_OPPORTUNITY_STRIKE.description"] = "造成6点伤害。如果这是本回合打出的第1张牌，额外造成6点伤害。",
            ["GONGDOU_POISON_DAGGER.title"] = "剧毒匕首",
            ["GONGDOU_POISON_DAGGER.description"] = "造成 5 点伤害。施加 4 层中毒。",
            ["GONGDOU_POISON_FOG.title"] = "毒雾",
            ["GONGDOU_POISON_FOG.description"] = "敌人每回合开始时获得 3 层中毒。",
            ["GONGDOU_CORROSIVE_SALVE.title"] = "腐蚀药膏",
            ["GONGDOU_CORROSIVE_SALVE.description"] = "施加 10 层中毒。消耗。",
            ["GONGDOU_BATTLE_CRY.title"] = "战吼",
            ["GONGDOU_BATTLE_CRY.description"] = "获得 1 点力量。抽 1 张牌，然后弃 1 张牌。",
            ["GONGDOU_BATTLE_CRY.selectionScreenPrompt"] = "弃掉 1 张牌。",
            ["GONGDOU_PREPARED_STANCE.title"] = "预备姿态",
            ["GONGDOU_PREPARED_STANCE.description"] = "选择最多 1 张手牌保留到下回合。消耗。",
            ["GONGDOU_PREPARED_STANCE.selectionScreenPrompt"] = "选择最多 1 张手牌保留到下回合。",
            ["GONGDOU_BLOOD_RUSH.title"] = "燃血冲锋",
            ["GONGDOU_BLOOD_RUSH.description"] = "失去 4 生命。造成 20 点伤害。",
            ["GONGDOU_BLUFF.title"] = "虚张声势",
            ["GONGDOU_BLUFF.description"] = "施加 2 层易伤。下回合敌人获得 8 格挡。"
        });

        MergeLoc("potions", new Dictionary<string, string>
        {
            ["GONGDOU_POISON_POTION12.title"] = "毒药水",
            ["GONGDOU_POISON_POTION12.description"] = "施加 12 层中毒。",
            ["GONGDOU_BURST_POTION.title"] = "爆炸安瓿",
            ["GONGDOU_BURST_POTION.description"] = "本回合下一张攻击牌额外造成 10 点伤害。"
        });

        MergeLoc("relics", new Dictionary<string, string>
        {
            ["GONGDOU_TACTICAL_MEMO_CLIP.title"] = "战术备忘夹",
            ["GONGDOU_TACTICAL_MEMO_CLIP.description"] = "每回合结束时可保留 1 张手牌。",
            ["GONGDOU_TACTICAL_MEMO_CLIP.flavor"] = "给跨回合规划留一点余地。",
            ["GONGDOU_CRACKED_BLADE_CHARM.title"] = "裂刃护符",
            ["GONGDOU_CRACKED_BLADE_CHARM.description"] = "每场战斗第一张攻击牌额外造成 8 点伤害。",
            ["GONGDOU_CRACKED_BLADE_CHARM.flavor"] = "只锋利一次也够吓人。",
            ["GONGDOU_POISON_SAC.title"] = "毒囊",
            ["GONGDOU_POISON_SAC.description"] = "每场战斗第一张攻击牌额外施加 3 层中毒。",
            ["GONGDOU_POISON_SAC.flavor"] = "剂量很足，但没到离谱。",
            ["GONGDOU_BLOOD_SIGIL.title"] = "燃血徽记",
            ["GONGDOU_BLOOD_SIGIL.description"] = "每次失去生命后，本回合下一张攻击牌额外造成 4 点伤害。",
            ["GONGDOU_BLOOD_SIGIL.flavor"] = "疼痛换来一点火力。",
            ["GONGDOU_OLD_WHETSTONE.title"] = "旧磨刀石",
            ["GONGDOU_OLD_WHETSTONE.description"] = "每回合第一张 1 费攻击牌额外造成 3 点伤害。",
            ["GONGDOU_OLD_WHETSTONE.flavor"] = "老东西，能用。",
            ["GONGDOU_SLOW_WARM_CORE.title"] = "慢热核心",
            ["GONGDOU_SLOW_WARM_CORE.description"] = "第 3 回合开始时获得 1 点力量和 1 点能量。",
            ["GONGDOU_SLOW_WARM_CORE.flavor"] = "启动有点慢。"
        });

        MergeLoc("powers", new Dictionary<string, string>
        {
            ["GONGDOU_POISON_FOG_POWER.title"] = "毒雾",
            ["GONGDOU_POISON_FOG_POWER.description"] = "敌人每回合开始时获得 {Amount} 层中毒。",
            ["GONGDOU_NEXT_TURN_BLOCK_POWER.title"] = "虚张声势",
            ["GONGDOU_NEXT_TURN_BLOCK_POWER.description"] = "下回合开始时获得 {Amount} 点格挡。",
            ["GONGDOU_BURST_POTION_POWER.title"] = "爆炸安瓿",
            ["GONGDOU_BURST_POTION_POWER.description"] = "本回合下一张攻击牌额外造成 {Amount} 点伤害。"
        });
    }

    private static void RegisterAxebotWindowLocalization()
    {
        MergeLoc("cards", new Dictionary<string, string>
        {
            ["GONGDOU_SELECT_CARDS"] = "选择卡牌：0/x",
            ["GongdouSkewer7.title"] = "串刺",
            ["GongdouSkewer7.description"] = "造成 8 点伤害 X 次。",
            ["GONGDOU_SKEWER7.title"] = "串刺",
            ["GONGDOU_SKEWER7.description"] = "造成 8 点伤害 X 次。"
        });

        MergeLoc("potions", new Dictionary<string, string>
        {
            ["GongdouPoisonPotion12.title"] = "毒药水",
            ["GongdouPoisonPotion12.description"] = "施加 12 层中毒。",
            ["GONGDOU_POISON_POTION12.title"] = "毒药水",
            ["GONGDOU_POISON_POTION12.description"] = "施加 12 层中毒。",
            ["GongdouBurstPotion.title"] = "爆炸安瓿",
            ["GongdouBurstPotion.description"] = "本回合下一张攻击牌额外造成 10 点伤害。",
            ["GONGDOU_BURST_POTION.title"] = "爆炸安瓿",
            ["GONGDOU_BURST_POTION.description"] = "本回合下一张攻击牌额外造成 10 点伤害。"
        });

        MergeLoc("powers", new Dictionary<string, string>
        {
            ["GongdouBurstPotionPower.title"] = "爆炸安瓿",
            ["GongdouBurstPotionPower.description"] = "本回合下一张攻击牌额外造成 {Amount} 点伤害。",
            ["GONGDOU_BURST_POTION_POWER.title"] = "爆炸安瓿",
            ["GONGDOU_BURST_POTION_POWER.description"] = "本回合下一张攻击牌额外造成 {Amount} 点伤害。",
            ["GongdouThinDeckRulePower.title"] = "稀疏检测",
            ["GongdouThinDeckRulePower.description"] = "起始牌组少于 16 张时，每少 1 张，巨斧机器人获得 80 点最大生命。该生命修正已在开局结算。",
            ["GONGDOU_THIN_DECK_RULE_POWER.title"] = "稀疏检测",
            ["GONGDOU_THIN_DECK_RULE_POWER.description"] = "起始牌组少于 16 张时，每少 1 张，巨斧机器人获得 80 点最大生命。该生命修正已在开局结算。",
            ["GongdouCrackedArmorRulePower.title"] = "裂甲窗口",
            ["GongdouCrackedArmorRulePower.description"] = "第1回合，巨斧机器人受到的单段攻击伤害最高只结算40点。多段攻击逐段结算。毒和药水固定伤害不受限制。第2回合开始消失。",
            ["GONGDOU_CRACKED_ARMOR_RULE_POWER.title"] = "裂甲窗口",
            ["GONGDOU_CRACKED_ARMOR_RULE_POWER.description"] = "第1回合，巨斧机器人受到的单段攻击伤害最高只结算40点。多段攻击逐段结算。毒和药水固定伤害不受限制。第2回合开始消失。",
            ["GongdouMaintenanceRulePower.title"] = "维护窗口",
            ["GongdouMaintenanceRulePower.description"] = "第 4 回合敌方行动后，如果巨斧机器人仍然存活，将触发维护窗口并判定挑战失败。行动循环：10 攻击 -> 16 攻击 -> 22 攻击 -> 10 攻击并维护。",
            ["GONGDOU_MAINTENANCE_RULE_POWER.title"] = "维护窗口",
            ["GONGDOU_MAINTENANCE_RULE_POWER.description"] = "第 4 回合敌方行动后，如果巨斧机器人仍然存活，将触发维护窗口并判定挑战失败。行动循环：10 攻击 -> 16 攻击 -> 22 攻击 -> 10 攻击并维护。"
        });

        MergeLoc("monsters", new Dictionary<string, string>
        {
            ["GongdouBrokenTrainingDummyMonster.name"] = "巨斧机器人"
        });
    }

    private static void RegisterAxebotRecalibrationLocalization()
    {
        MergeLoc("card_selection", new Dictionary<string, string>
        {
            ["GONGDOU_SELECT_CARDS"] = "选择卡牌：0/x"
        });

        MergeLoc("potions", new Dictionary<string, string>
        {
            ["GongdouPoisonPotion12.title"] = "毒药水",
            ["GongdouPoisonPotion12.description"] = "施加 12 层中毒。",
            ["GONGDOU_POISON_POTION12.title"] = "毒药水",
            ["GONGDOU_POISON_POTION12.description"] = "施加 12 层中毒。",
            ["GongdouBurstPotion.title"] = "爆炸安瓿",
            ["GongdouBurstPotion.description"] = "本回合下一张攻击牌额外造成 10 点伤害。",
            ["GONGDOU_BURST_POTION.title"] = "爆炸安瓿",
            ["GONGDOU_BURST_POTION.description"] = "本回合下一张攻击牌额外造成 10 点伤害。"
        });

        MergeLoc("powers", new Dictionary<string, string>
        {
            ["GongdouBurstPotionPower.title"] = "爆炸安瓿",
            ["GongdouBurstPotionPower.description"] = "本回合下一张攻击牌额外造成 {Amount} 点伤害。",
            ["GONGDOU_BURST_POTION_POWER.title"] = "爆炸安瓿",
            ["GONGDOU_BURST_POTION_POWER.description"] = "本回合下一张攻击牌额外造成 {Amount} 点伤害。",
            ["GongdouThinDeckRulePower.title"] = "稀疏检测",
            ["GongdouThinDeckRulePower.description"] = "起始牌组少于 16 张时，每少 1 张，巨斧机器人获得 80 点最大生命。该生命修正已在开局结算。",
            ["GONGDOU_THIN_DECK_RULE_POWER.title"] = "稀疏检测",
            ["GONGDOU_THIN_DECK_RULE_POWER.description"] = "起始牌组少于 16 张时，每少 1 张，巨斧机器人获得 80 点最大生命。该生命修正已在开局结算。",
            ["GongdouCrackedArmorRulePower.title"] = "裂甲窗口",
            ["GongdouCrackedArmorRulePower.description"] = "第1回合，巨斧机器人受到的单段攻击伤害最高只结算40点。多段攻击逐段结算。毒和药水固定伤害不受限制。第2回合开始消失。",
            ["GONGDOU_CRACKED_ARMOR_RULE_POWER.title"] = "裂甲窗口",
            ["GONGDOU_CRACKED_ARMOR_RULE_POWER.description"] = "第1回合，巨斧机器人受到的单段攻击伤害最高只结算40点。多段攻击逐段结算。毒和药水固定伤害不受限制。第2回合开始消失。",
            ["GongdouMaintenanceRulePower.title"] = "维护窗口",
            ["GongdouMaintenanceRulePower.description"] = "第 4 回合敌方行动后，如果巨斧机器人仍然存活，将触发维护窗口并判定挑战失败。行动循环：10 攻击 -> 16 攻击 -> 22 攻击 -> 10 攻击并维护。",
            ["GONGDOU_MAINTENANCE_RULE_POWER.title"] = "维护窗口",
            ["GONGDOU_MAINTENANCE_RULE_POWER.description"] = "第 4 回合敌方行动后，如果巨斧机器人仍然存活，将触发维护窗口并判定挑战失败。行动循环：10 攻击 -> 16 攻击 -> 22 攻击 -> 10 攻击并维护。"
        });

        MergeLoc("monsters", new Dictionary<string, string>
        {
            ["GongdouBrokenTrainingDummyMonster.name"] = "巨斧机器人"
        });
    }

    public static bool IsOnMainThread =>
        _mainThreadId != 0 && System.Environment.CurrentManagedThreadId == _mainThreadId;

    public static Task RunOnMainThread(Action action)
    {
        if (IsOnMainThread)
        {
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task<T> RunOnMainThread<T>(Func<T> func)
    {
        if (IsOnMainThread)
        {
            try
            {
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task RunOnMainThread(Func<Task> func)
    {
        if (IsOnMainThread)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                func().ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        tcs.SetException(task.Exception.InnerExceptions);
                    }
                    else if (task.IsCanceled)
                    {
                        tcs.SetCanceled();
                    }
                    else
                    {
                        tcs.SetResult();
                    }
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task<T> RunOnMainThread<T>(Func<Task<T>> func)
    {
        if (IsOnMainThread)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                func().ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        tcs.SetException(task.Exception.InnerExceptions);
                    }
                    else if (task.IsCanceled)
                    {
                        tcs.SetCanceled();
                    }
                    else
                    {
                        tcs.SetResult(task.Result);
                    }
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private static void ProcessFrame()
    {
        _mainThreadId = System.Environment.CurrentManagedThreadId;
        EnsureLocalizationRegistered();

        var processed = 0;
        while (processed < 20 && MainThreadQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Main thread action failed: {ex}");
            }

            processed++;
        }

        _sessionManager?.Tick();
    }
}
