using System.Reflection;
using System.Text.RegularExpressions;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Models;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using Sts2Cards = MegaCrit.Sts2.Core.Models.Cards;

namespace GongdouSts2ChallengeMod.Patches;

[HarmonyPatch]
internal static class GongdouCardDescriptionPatch
{
    private readonly record struct CardKeywordTipDef(string Key, string Term, Func<CardModel, IHoverTip>? Factory = null);

    private static readonly CardKeywordTipDef[] CardKeywordTips =
    [
        new("RetainedBlock", "保留格挡"),
        new("FixedDamage", "固定伤害"),
        new("DelayedDamage", "延迟伤害"),
        new("Orb", "充能球", _ => HoverTipFactory.Static(StaticHoverTip.Channeling)),
        new("Burn", "灼伤", _ => HoverTipFactory.FromCard<Sts2Cards.Burn>()),
        new("Wound", "伤口", _ => HoverTipFactory.FromCard<Sts2Cards.Wound>()),
        new("Dazed", "晕眩", _ => HoverTipFactory.FromCard<Sts2Cards.Dazed>()),
        new("Curse", "诅咒"),
        new("Status", "状态"),
        new("Void", "虚空"),
        new("Cunning", "奇巧", _ => HoverTipFactory.FromKeyword(CardKeyword.Sly)),
        new("Stance", "姿态"),
        new("Calm", "平静"),
        new("Wrath", "愤怒"),
        new("Normal", "普通"),
        new("Vulnerable", "易伤", _ => HoverTipFactory.FromPower<VulnerablePower>()),
        new("Weak", "虚弱", _ => HoverTipFactory.FromPower<WeakPower>()),
        new("Poison", "中毒", _ => HoverTipFactory.FromPower<PoisonPower>()),
        new("Poison", "毒", _ => HoverTipFactory.FromPower<PoisonPower>()),
        new("Block", "格挡", _ => HoverTipFactory.Static(StaticHoverTip.Block)),
        new("Armor", "护甲"),
        new("Strength", "力量", _ => HoverTipFactory.FromPower<StrengthPower>()),
        new("Energy", "能量", _ => HoverTipFactory.Static(StaticHoverTip.Energy)),
        new("Exhaust", "消耗", _ => HoverTipFactory.FromKeyword(CardKeyword.Exhaust)),
        new("Lightning", "闪电", _ => HoverTipFactory.FromOrb<LightningOrb>()),
        new("Frost", "冰霜", _ => HoverTipFactory.FromOrb<FrostOrb>()),
        new("Dark", "黑暗", _ => HoverTipFactory.FromOrb<DarkOrb>()),
        new("Focus", "集中", _ => HoverTipFactory.FromPower<FocusPower>()),
        new("Loop", "循环"),
        new("Charge", "充能"),
        new("Echo", "回声"),
        new("Overheat", "过热"),
        new("Mark", "标记"),
        new("Mantra", "真言"),
        new("Divinity", "神格")
    ];

    private static readonly Lazy<Dictionary<string, IReadOnlyList<CardKeywordTipDef>>> KeywordTipsByCardId =
        new(BuildKeywordTipsByCardId);

    private static readonly PropertyInfo? HoverTipIconProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Icon), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly Regex LeadingSlyLine = new(
        @"^\s*(?:\[gold\])?奇巧(?:\[/gold\])?[。.]?\s*(?:\r?\n)+",
        RegexOptions.Compiled);

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(CardModel)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(CardModel.GetDescriptionForPile));
    }

    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance is not GongdouD3BackstabCunning and not GongdouD3ShadowStep)
        {
            return;
        }

        __result = LeadingSlyLine.Replace(__result, "", 1);
    }

    private static Dictionary<string, IReadOnlyList<CardKeywordTipDef>> BuildKeywordTipsByCardId()
    {
        var result = new Dictionary<string, IReadOnlyList<CardKeywordTipDef>>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in PuzzleCatalog.AllConfigs)
        {
            foreach (var item in PuzzleCatalog.ResourcesForStage(stage.StageIndex).CardPool)
            {
                var tips = CardKeywordTips
                    .Where(tip => item.Description.Contains(tip.Term, StringComparison.Ordinal))
                    .DistinctBy(tip => tip.Key)
                    .ToArray();
                if (tips.Length > 0)
                {
                    result[NormalizeResourceId(item.Id)] = tips;
                }
            }
        }

        return result;
    }

    private static string NormalizeResourceId(string id)
    {
        return id.Replace("Gongdou", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static IEnumerable<IHoverTip> BuildKeywordHoverTips(CardModel card)
    {
        var id = NormalizeResourceId(card.Id.Entry);
        if (!KeywordTipsByCardId.Value.TryGetValue(id, out var tipDefs))
        {
            return [];
        }

        return tipDefs.Select(tip => BuildKeywordHoverTip(card, tip));
    }

    private static IHoverTip BuildKeywordHoverTip(CardModel card, CardKeywordTipDef tip)
    {
        var hoverTip = tip.Factory?.Invoke(card)
            ?? new HoverTip(
                new LocString("powers", $"GongdouCardKeyword.{tip.Key}.title"),
                new LocString("powers", $"GongdouCardKeyword.{tip.Key}.description"));

        return WithoutCardKeywordIcon(hoverTip);
    }

    private static IHoverTip WithoutCardKeywordIcon(IHoverTip tip)
    {
        if (tip is not HoverTip hoverTip || HoverTipIconProperty == null)
        {
            return tip;
        }

        try
        {
            object boxed = hoverTip;
            HoverTipIconProperty.SetValue(boxed, null);
            return (IHoverTip)boxed;
        }
        catch
        {
            return tip;
        }
    }

    [HarmonyPatch(typeof(CardModel), "get_HoverTips")]
    private static class CardHoverTipsPatch
    {
        private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
        {
            if (__instance is not GongdouChallengeCard)
            {
                return;
            }

            var extraTips = BuildKeywordHoverTips(__instance).ToList();
            if (extraTips.Count == 0)
            {
                __result = (__result ?? []).Select(WithoutCardKeywordIcon).ToList();
                return;
            }

            var tips = (__result ?? []).Select(WithoutCardKeywordIcon).ToList();
            tips.AddRange(extraTips);
            __result = IHoverTip.RemoveDupes(tips);
        }
    }
}
