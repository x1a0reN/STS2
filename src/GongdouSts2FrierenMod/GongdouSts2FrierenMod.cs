using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using GongdouSts2FrierenMod.Assets;
using GongdouSts2FrierenMod.Cards;
using GongdouSts2FrierenMod.Characters;
using GongdouSts2FrierenMod.Diagnostics;
using GongdouSts2FrierenMod.Potions;
using GongdouSts2FrierenMod.Powers;
using GongdouSts2FrierenMod.Relics;

namespace GongdouSts2FrierenMod;

[ModInitializer("Initialize")]
public static class GongdouSts2FrierenMod
{
    public const string Version = "0.1.36";

    private static bool _localizationRegistered;

    public static void Initialize()
    {
        try
        {
            ModDiagnosticReporter.InstallGlobalHandlers("sts2.frieren", Version);
            EnsureLocalizationRegistered();
            new Harmony("com.gongdou.sts2.frieren").PatchAll();
            FrierenAssetPaths.PreloadCustomTextures();
            GD.Print($"[GongDou STS2 Frieren] v{Version} initialized.");
        }
        catch (Exception ex)
        {
            ModDiagnosticReporter.ReportException("sts2.frieren.initialize", Version, ex);
            GD.PrintErr($"[GongDou STS2 Frieren] Initialize failed: {ex}");
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

        RegisterLocalization();
        _localizationRegistered = true;
    }

    private static void RegisterLocalization()
    {
        MergeLoc("characters", new Dictionary<string, string>
        {
            ["FRIEREN_CHARACTER.title"] = "芙莉莲",
            ["FRIEREN_CHARACTER.titleObject"] = "芙莉莲",
            ["FRIEREN_CHARACTER.description"] = "跨越千年的精灵魔法使。解析敌人、隐匿魔力，并在关键回合释放法术。",
            ["FRIEREN_CHARACTER.pronounSubject"] = "她",
            ["FRIEREN_CHARACTER.pronounObject"] = "她",
            ["FRIEREN_CHARACTER.pronounPossessive"] = "她的",
            ["FRIEREN_CHARACTER.possessiveAdjective"] = "她的",
            ["FRIEREN_CHARACTER.cardsModifierTitle"] = "芙莉莲牌",
            ["FRIEREN_CHARACTER.cardsModifierDescription"] = "奖励中包含芙莉莲的法术牌。",
            ["FRIEREN_CHARACTER.unlockText"] = "测试角色：当前框架默认可用。",
            ["FRIEREN_CHARACTER.eventDeathPrevention"] = "现在倒下还太早。"
        });

        MergeLoc("card_selection", new Dictionary<string, string>
        {
            ["FRIEREN_CHOOSE_CARD"] = "选择{Amount}张牌。",
            ["FRIEREN_ADD_TO_HAND"] = "选择{Amount}张牌加入手牌。",
            ["FRIEREN_MAKE_FREE"] = "选择{Amount}张牌。本回合费用变为0。",
            ["FRIEREN_RETAIN_CARD"] = "选择最多{Amount}张牌保留。"
        });

        MergeLoc("cards", new Dictionary<string, string>
        {
            ["FRIEREN_STRIKE.title"] = "打击",
            ["FRIEREN_STRIKE.description"] = "造成{Damage}点伤害。",
            ["FRIEREN_DEFEND.title"] = "防御",
            ["FRIEREN_DEFEND.description"] = "获得{Block}点格挡。",
            ["BASIC_KILLING_MAGIC.title"] = "基础杀人魔法",
            ["BASIC_KILLING_MAGIC.description"] = "造成{Damage}点伤害。施加{Analysis}层解析。",
            ["MANA_SUPPRESSION.title"] = "魔力抑制",
            ["MANA_SUPPRESSION.description"] = "获得{Block}点格挡。获得{ConcealedMana}层隐匿魔力。",
            ["KILLING_MAGIC.title"] = "杀人魔法",
            ["KILLING_MAGIC.description"] = "造成{Damage}点伤害。施加{Analysis}层解析。",
            ["DEFENSIVE_MAGIC.title"] = "防御魔法",
            ["DEFENSIVE_MAGIC.description"] = "获得{Block}点格挡。获得{ConcealedMana}层隐匿魔力。",
            ["MANA_DETECTION.title"] = "魔力探知",
            ["MANA_DETECTION.description"] = "普通魔法。\n施加{Analysis}层解析。抽{Cards}张牌。",
            ["CLEAN_MAGIC.title"] = "清洁魔法",
            ["CLEAN_MAGIC.description"] = "普通魔法。\n获得{Block}点格挡。消耗。",
            ["MAGIC_NEEDLE.title"] = "魔力针",
            ["MAGIC_NEEDLE.description"] = "造成{Damage}点伤害。抽{Cards}张牌。消耗解析：获得1层隐匿魔力。",
            ["REPEATED_SHOT.title"] = "重复射击",
            ["REPEATED_SHOT.description"] = "造成{Damage}点伤害2次。第一段施加{Analysis}层解析。",
            ["DEEP_MANA_SUPPRESSION.title"] = "深度魔力抑制",
            ["DEEP_MANA_SUPPRESSION.description"] = "获得{Block}点格挡。获得{ConcealedMana}层隐匿魔力。",
            ["LONG_CHANT.title"] = "长咏唱",
            ["LONG_CHANT.description"] = "获得{Block}点格挡。咏唱1：获得{Release}层解放。抽1张牌。",
            ["FULL_POWER_KILLING_MAGIC.title"] = "全力杀人魔法",
            ["FULL_POWER_KILLING_MAGIC.description"] = "造成{Damage}点伤害。解放：追加造成{ExtraDamage}点伤害。",
            ["ANALYZE_EVERYTHING.title"] = "解析一切",
            ["ANALYZE_EVERYTHING.description"] = "对所有敌人施加{Analysis}层解析和{Insight}层看破。抽{Cards}张牌。消耗。",
            ["GREAT_MAGE_FRIEREN.title"] = "大魔法使芙莉莲",
            ["GREAT_MAGE_FRIEREN.description"] = "回合开始时，获得{ConcealedMana}层隐匿魔力，并对随机敌人施加{Analysis}层解析。每场战斗首次触发回路：对解析层数最多的敌人施加1层看破。",
            ["FLOWER_FIELD_MAGIC.title"] = "开出花田的魔法",
            ["FLOWER_FIELD_MAGIC.description"] = "普通魔法。\n获得{Block}点格挡。3层回忆：战斗结束时回复2点生命。消耗。"
        });
        MergeLoc("cards", FrierenCardCatalog.BuildLocalization());

        MergeLoc("powers", new Dictionary<string, string>
        {
            ["FRIEREN_ANALYSIS_POWER.title"] = "解析",
            ["FRIEREN_ANALYSIS_POWER.description"] = "法术造成未格挡伤害时，消耗1层，使敌人失去2点生命。",
            ["FRIEREN_INSIGHT_POWER.title"] = "看破",
            ["FRIEREN_INSIGHT_POWER.description"] = "下一张消耗解析的法术，使每层解析改为失去3点生命。",
            ["FRIEREN_CONCEALED_MANA_POWER.title"] = "隐匿魔力",
            ["FRIEREN_CONCEALED_MANA_POWER.description"] = "达到10层时，失去10层，获得1层解放。上限19层。",
            ["FRIEREN_RELEASE_POWER.title"] = "解放",
            ["FRIEREN_RELEASE_POWER.description"] = "下一张法术额外打出1次。消耗1层解放。上限2层。",
            ["FRIEREN_SPELL_KEYWORD_POWER.title"] = "法术",
            ["FRIEREN_SPELL_KEYWORD_POWER.description"] = "芙莉莲专属牌标签。打击、防御不算。",
            ["FRIEREN_NORMAL_MAGIC_KEYWORD_POWER.title"] = "普通魔法",
            ["FRIEREN_NORMAL_MAGIC_KEYWORD_POWER.description"] = "小型功能魔法标签，用于回忆和普通魔法流。",
            ["FRIEREN_CHANT_KEYWORD_POWER.title"] = "咏唱",
            ["FRIEREN_CHANT_KEYWORD_POWER.description"] = "延迟指定回合后结算。费用和目标在打出时确定。",
            ["FRIEREN_CIRCUIT_KEYWORD_POWER.title"] = "回路",
            ["FRIEREN_CIRCUIT_KEYWORD_POWER.description"] = "每手动打出3张法术，触发1次回路。",
            ["FRIEREN_MEMORY_POWER.title"] = "回忆",
            ["FRIEREN_MEMORY_POWER.description"] = "每场战斗首次手动打出不同名普通魔法时，获得1层回忆。每3层触发一次旅途奖励。",
            ["FRIEREN_SUPPRESSION_POWER.title"] = "抑制",
            ["FRIEREN_SUPPRESSION_POWER.description"] = "回合结束时，未打出攻击牌：获得{Amount}层隐匿魔力。",
            ["FRIEREN_QUIET_STEPS_POWER.title"] = "安静脚步",
            ["FRIEREN_QUIET_STEPS_POWER.description"] = "回合结束时，未打出攻击牌：获得{Amount}层隐匿魔力。",
            ["FRIEREN_DELAYED_RELEASE_POWER.title"] = "长咏唱",
            ["FRIEREN_DELAYED_RELEASE_POWER.description"] = "下回合开始时，获得{Amount}层解放。",
            ["FRIEREN_GREAT_MAGE_POWER.title"] = "大魔法使芙莉莲",
            ["FRIEREN_GREAT_MAGE_POWER.description"] = "回合开始时，获得{Amount}层隐匿魔力，并对随机敌人施加解析。每场战斗首次触发回路：对解析层数最多的敌人施加1层看破。",
            ["FRIEREN_DELAYED_ENERGY_POWER.title"] = "缓慢咏唱",
            ["FRIEREN_DELAYED_ENERGY_POWER.description"] = "下回合开始时，获得{Amount}点能量。",
            ["FRIEREN_DELAYED_BLOCK_ENERGY_POWER.title"] = "缓慢咏唱",
            ["FRIEREN_DELAYED_BLOCK_ENERGY_POWER.description"] = "下回合开始时，获得{Amount}点格挡和1点能量。",
            ["FRIEREN_DELAYED_BLOCK_POWER.title"] = "保留格挡",
            ["FRIEREN_DELAYED_BLOCK_POWER.description"] = "下回合开始时，获得{Amount}点格挡。",
            ["FRIEREN_NO_ATTACK_THIS_TURN_POWER.title"] = "静默",
            ["FRIEREN_NO_ATTACK_THIS_TURN_POWER.description"] = "本回合不能打出攻击牌。",
            ["FRIEREN_NEXT_ATTACK_DISCOUNT_POWER.title"] = "弱点解析",
            ["FRIEREN_NEXT_ATTACK_DISCOUNT_POWER.description"] = "本回合下一张攻击牌费用-1。",
            ["FRIEREN_TEMPORARY_STRENGTH_DOWN_POWER.title"] = "解除魔法",
            ["FRIEREN_TEMPORARY_STRENGTH_DOWN_POWER.description"] = "本回合暂时失去力量。",
            ["FRIEREN_DELAYED_CONCEALED_MANA_POWER.title"] = "延迟隐匿魔力",
            ["FRIEREN_DELAYED_CONCEALED_MANA_POWER.description"] = "下回合开始时，获得{Amount}层隐匿魔力。",
            ["FRIEREN_BARRIER_INVERSION_POWER.title"] = "屏障反转",
            ["FRIEREN_BARRIER_INVERSION_POWER.description"] = "格挡未破：下回合获得{Amount}层隐匿魔力，并对所有敌人施加2层解析。",
            ["FRIEREN_DELAYED_BARRIER_INVERSION_REWARD_POWER.title"] = "屏障反转",
            ["FRIEREN_DELAYED_BARRIER_INVERSION_REWARD_POWER.description"] = "下回合开始时，获得{Amount}层隐匿魔力，并对所有敌人施加2层解析。",
            ["FRIEREN_LONG_JOURNEY_POWER.title"] = "漫长旅途",
            ["FRIEREN_LONG_JOURNEY_POWER.description"] = "下回合按回忆层数抽牌，最多{Amount}张牌。",
            ["FRIEREN_FLOWER_FIELD_HEAL_POWER.title"] = "开出花田的魔法",
            ["FRIEREN_FLOWER_FIELD_HEAL_POWER.description"] = "战斗结束时，回复{Amount}点生命。",
            ["FRIEREN_MANA_CONCEALMENT_POWER.title"] = "魔力隐蔽",
            ["FRIEREN_MANA_CONCEALMENT_POWER.description"] = "未用能量转为隐匿魔力，每回合最多{Amount}层。",
            ["FRIEREN_GRIMOIRE_COLLECTOR_POWER.title"] = "魔导书收藏家",
            ["FRIEREN_GRIMOIRE_COLLECTOR_POWER.description"] = "每回合前{Amount}次打出普通魔法，抽1张牌。回忆：获得3点格挡。",
            ["FRIEREN_COMBAT_ANALYSIS_POWER.title"] = "战斗解析",
            ["FRIEREN_COMBAT_ANALYSIS_POWER.description"] = "对已有解析的敌人施加解析时，额外施加{Amount}层解析。",
            ["FRIEREN_EFFICIENT_CHANT_POWER.title"] = "高效咏唱",
            ["FRIEREN_EFFICIENT_CHANT_POWER.description"] = "每回合前{Amount}张以解析敌人为目标的法术费用-1。",
            ["FRIEREN_HUMAN_MAGIC_ERA_POWER.title"] = "人类魔法时代",
            ["FRIEREN_HUMAN_MAGIC_ERA_POWER.description"] = "回合开始时，将1张0费临时基础杀人魔法加入手牌。消耗。",
            ["FRIEREN_DISCIPLINE_POWER.title"] = "菲伦的纪律",
            ["FRIEREN_DISCIPLINE_POWER.description"] = "每回合首次打出0费牌：获得{Amount}点格挡。",
            ["FRIEREN_CONCEALED_MANA_RESERVE_POWER.title"] = "隐匿魔力储备",
            ["FRIEREN_CONCEALED_MANA_RESERVE_POWER.description"] = "获得或消耗解放：获得{Amount}点能量。每回合最多2次。",
            ["FRIEREN_MASTER_APPRENTICE_RHYTHM_POWER.title"] = "师徒节奏",
            ["FRIEREN_MASTER_APPRENTICE_RHYTHM_POWER.description"] = "触发回路时，抽1张牌。低费用回路：额外抽1张牌。",
            ["FRIEREN_MILLENNIUM_COMPOSURE_POWER.title"] = "千年的从容",
            ["FRIEREN_MILLENNIUM_COMPOSURE_POWER.description"] = "上回合未打出攻击牌：回合开始时获得{Amount}层隐匿魔力。",
            ["FRIEREN_SERIE_TEACHING_POWER.title"] = "赛丽艾的教诲",
            ["FRIEREN_SERIE_TEACHING_POWER.description"] = "获得回忆：获得1层隐匿魔力和{Amount}点格挡。",
            ["FRIEREN_HIMMEL_MEMORY_POWER.title"] = "辛美尔的回忆",
            ["FRIEREN_HIMMEL_MEMORY_POWER.description"] = "每场战斗首次生命值低于50%：获得1层解放。抽2张牌。",
            ["FRIEREN_FLAMME_TEACHING_POWER.title"] = "芙兰梅的教导",
            ["FRIEREN_FLAMME_TEACHING_POWER.description"] = "打出能力牌：获得{Amount}层隐匿魔力。",
            ["FRIEREN_MAGIC_ENCYCLOPEDIA_POWER.title"] = "魔法百科全书",
            ["FRIEREN_MAGIC_ENCYCLOPEDIA_POWER.description"] = "触发回路时抽1张牌。3张法术名称不同：获得1点能量。",
            ["FRIEREN_LIMITED_MANA_AURA_POWER.title"] = "受限的魔力气息",
            ["FRIEREN_LIMITED_MANA_AURA_POWER.description"] = "每回合首次获得隐匿魔力时，对所有敌人施加{Amount}层解析。",
            ["FRIEREN_DEMON_KILLER_POWER.title"] = "魔族杀手",
            ["FRIEREN_DEMON_KILLER_POWER.description"] = "敌人每回合首次因解析或看破失去生命时，施加{Amount}层易伤。",
            ["FRIEREN_NORMAL_MAGIC_MASTERY_POWER.title"] = "普通魔法精通",
            ["FRIEREN_NORMAL_MAGIC_MASTERY_POWER.description"] = "每回合前{Amount}张普通魔法费用为0。回忆：额外获得1层回忆，每回合最多1次。",
            ["FRIEREN_MILLENNIUM_RESEARCH_POWER.title"] = "千年研究",
            ["FRIEREN_MILLENNIUM_RESEARCH_POWER.description"] = "回合开始时，随机升级{Amount}张手牌，并获得1层回忆。",
            ["FRIEREN_HUGE_MANA_POWER.title"] = "巨大魔力",
            ["FRIEREN_HUGE_MANA_POWER.description"] = "回合开始时，获得{Amount}层隐匿魔力。",
            ["FRIEREN_HEROES_PARTY_MAGIC_POWER.title"] = "勇者一行的魔法",
            ["FRIEREN_HEROES_PARTY_MAGIC_POWER.description"] = "回合开始时，上回合打过攻击、技能、能力：获得{Amount}层解放。",
            ["FRIEREN_FLOWER_FIELD_POTION_POWER.title"] = "花田药水",
            ["FRIEREN_FLOWER_FIELD_POTION_POWER.description"] = "本战每打出不同名普通魔法，获得{Amount}点格挡。"
        });

        MergeLoc("relics", new Dictionary<string, string>
        {
            ["BLUE_MOON_GRASS_BOOKMARK.title"] = "蓝月草书签",
            ["BLUE_MOON_GRASS_BOOKMARK.description"] = "战斗开始时，获得3层隐匿魔力。每场战斗首次获得解放：下回合开始时抽1张牌。",
            ["BLUE_MOON_GRASS_BOOKMARK.flavor"] = "一枚来自漫长旅途的书签。",
            ["FADED_GRIMOIRE.title"] = "褪色的魔导书",
            ["FADED_GRIMOIRE.description"] = "替换蓝月草书签。战斗开始时，获得4层隐匿魔力。获得解放：下回合开始时抽1张牌。",
            ["FADED_GRIMOIRE.flavor"] = "字迹已经褪色，但魔力还在。",
            ["SMALL_SUITCASE.title"] = "小型手提箱",
            ["SMALL_SUITCASE.description"] = "每场战斗首次打出普通魔法：获得4点格挡。触发回忆奖励：获得2点格挡。",
            ["SMALL_SUITCASE.flavor"] = "看起来很小，装得却不少。",
            ["TATTERED_MAGIC_PAGE.title"] = "破旧魔法书页",
            ["TATTERED_MAGIC_PAGE.description"] = "每场战斗首次对无解析敌人施加解析：额外施加2层解析。",
            ["TATTERED_MAGIC_PAGE.flavor"] = "缺页并不影响关键段落。",
            ["FERN_HAIR_ORNAMENT.title"] = "菲伦的发饰",
            ["FERN_HAIR_ORNAMENT.description"] = "每回合首次消耗解放：下回合开始时抽1张牌。",
            ["FERN_HAIR_ORNAMENT.flavor"] = "严肃少女的小小装饰。",
            ["SEINS_PRAYER.title"] = "赞因的祷言",
            ["SEINS_PRAYER.description"] = "休息时额外回复6点生命。每场战斗首次移除自身负面状态、状态牌或诅咒牌：获得1层解放。",
            ["SEINS_PRAYER.flavor"] = "不靠谱的僧侣，靠谱的祷言。",
            ["MIMIC_GRIMOIRE.title"] = "宝箱怪图鉴",
            ["MIMIC_GRIMOIRE.description"] = "宝箱房间额外提供1个奖励选项。战斗开始时，将1张宝箱怪咬痕放入弃牌堆。每场战斗首次抽到宝箱怪咬痕：抽1张牌。",
            ["MIMIC_GRIMOIRE.flavor"] = "别问为什么每一页都在尖叫。",
            ["FLAMMES_NOTES.title"] = "芙兰梅的手记",
            ["FLAMMES_NOTES.description"] = "获得回忆：获得2层隐匿魔力。每场战斗首次达到6层回忆：获得1层解放。",
            ["FLAMMES_NOTES.flavor"] = "来自师父的温柔和恶趣味。",
            ["SERIES_CERTIFICATE.title"] = "赛丽艾的证书",
            ["SERIES_CERTIFICATE.description"] = "每场战斗首次打出稀有牌或古代牌：获得1层解放。",
            ["SERIES_CERTIFICATE.flavor"] = "上面写着极其挑剔的认可。",
            ["HIMMELS_RING.title"] = "欣梅尔的戒指",
            ["HIMMELS_RING.description"] = "首领战开始时，获得1层解放。每场战斗首次生命值低于25%：获得12点格挡，并对所有敌人施加1层看破。",
            ["HIMMELS_RING.flavor"] = "勇者留下的东西，总会在某个时候派上用场。"
        });

        MergeLoc("potions", new Dictionary<string, string>
        {
            ["MANA_RELEASE_POTION.title"] = "魔力解放药水",
            ["MANA_RELEASE_POTION.description"] = "获得1层解放。",
            ["ANALYSIS_POTION.title"] = "解析药水",
            ["ANALYSIS_POTION.description"] = "对所有敌人施加8层解析。",
            ["NORMAL_MAGIC_BOTTLE_POTION.title"] = "普通魔法瓶",
            ["NORMAL_MAGIC_BOTTLE_POTION.description"] = "将3张随机小魔法加入手牌。本回合0费并消耗。",
            ["CONCEALED_MANA_POTION.title"] = "隐匿魔力药水",
            ["CONCEALED_MANA_POTION.description"] = "获得9层隐匿魔力。",
            ["FLOWER_FIELD_POTION.title"] = "花田药水",
            ["FLOWER_FIELD_POTION.description"] = "获得15点格挡。本战每打出不同名普通魔法，额外获得3点格挡。"
        });
    }

    private static void MergeLoc(string table, Dictionary<string, string> entries)
    {
        try
        {
            LocManager.Instance.GetTable(table).MergeWith(entries);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2 Frieren] Failed to merge localization table {table}: {ex.Message}");
        }
    }
}

internal static class FrierenModelRegistry
{
    private static readonly Type[] CoreModelTypes =
    [
        typeof(FrierenCharacter),
        typeof(FrierenCardPool),
        typeof(FrierenPotionPool),
        typeof(FrierenRelicPool),
        typeof(FrierenAnalysisPower),
        typeof(FrierenInsightPower),
        typeof(FrierenConcealedManaPower),
        typeof(FrierenReleasePower),
        typeof(FrierenSpellKeywordPower),
        typeof(FrierenNormalMagicKeywordPower),
        typeof(FrierenChantKeywordPower),
        typeof(FrierenCircuitKeywordPower),
        typeof(FrierenMemoryPower),
        typeof(FrierenSuppressionPower),
        typeof(FrierenQuietStepsPower),
        typeof(FrierenDelayedReleasePower),
        typeof(FrierenGreatMagePower),
        typeof(FrierenDelayedEnergyPower),
        typeof(FrierenDelayedBlockEnergyPower),
        typeof(FrierenDelayedBlockPower),
        typeof(FrierenNoAttackThisTurnPower),
        typeof(FrierenCombatTrackerPower),
        typeof(FrierenNextAttackDiscountPower),
        typeof(FrierenTemporaryStrengthDownPower),
        typeof(FrierenDelayedConcealedManaPower),
        typeof(FrierenBarrierInversionPower),
        typeof(FrierenDelayedBarrierInversionRewardPower),
        typeof(FrierenLongJourneyPower),
        typeof(FrierenFlowerFieldHealPower),
        typeof(FrierenManaConcealmentPower),
        typeof(FrierenGrimoireCollectorPower),
        typeof(FrierenCombatAnalysisPower),
        typeof(FrierenEfficientChantPower),
        typeof(FrierenHumanMagicEraPower),
        typeof(FrierenDisciplinePower),
        typeof(FrierenConcealedManaReservePower),
        typeof(FrierenMasterApprenticeRhythmPower),
        typeof(FrierenMillenniumComposurePower),
        typeof(FrierenSerieTeachingPower),
        typeof(FrierenHimmelMemoryPower),
        typeof(FrierenFlammeTeachingPower),
        typeof(FrierenMagicEncyclopediaPower),
        typeof(FrierenLimitedManaAuraPower),
        typeof(FrierenDemonKillerPower),
        typeof(FrierenNormalMagicMasteryPower),
        typeof(FrierenMillenniumResearchPower),
        typeof(FrierenHugeManaPower),
        typeof(FrierenHeroesPartyMagicPower),
        typeof(FrierenFlowerFieldPotionPower),
        typeof(BlueMoonGrassBookmark),
        typeof(FadedGrimoire),
        typeof(ManaReleasePotion),
        typeof(AnalysisPotion)
    ];

    public static readonly Type[] ModelTypes = CoreModelTypes
        .Concat(FrierenCardCatalog.AllCardTypes)
        .Concat(FrierenRelicCatalog.AllRelicTypes)
        .Concat(FrierenPotionCatalog.AllPotionTypes)
        .Distinct()
        .ToArray();
}
