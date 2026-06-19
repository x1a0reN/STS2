using Godot;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Challenges;
using GongdouSts2ChallengeMod.Monsters;
using GongdouSts2ChallengeMod.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using System.Reflection;

namespace GongdouSts2ChallengeMod.Patches;

internal static class GongdouPowerIconPatch
{
    private static PowerModel ResolveFallbackKeywordIconSource(Type powerType)
    {
        var hash = 17;
        foreach (var ch in powerType.FullName ?? powerType.Name)
        {
            hash = unchecked(hash * 31 + ch);
        }

        return ((uint)hash % 11u) switch
        {
            0 => ModelDb.Power<StrengthPower>(),
            1 => ModelDb.Power<DexterityPower>(),
            2 => ModelDb.Power<BufferPower>(),
            3 => ModelDb.Power<HardenedShellPower>(),
            4 => ModelDb.Power<RegenPower>(),
            5 => ModelDb.Power<VulnerablePower>(),
            6 => ModelDb.Power<WeakPower>(),
            7 => ModelDb.Power<FrailPower>(),
            8 => ModelDb.Power<ArtifactPower>(),
            9 => ModelDb.Power<PoisonPower>(),
            _ => ModelDb.Power<FocusPower>()
        };
    }

    private static PowerModel ResolveKeywordIconSource(PowerModel power)
    {
        return power switch
        {
            GongdouPersistentArmorKeywordPower => ModelDb.Power<HardenedShellPower>(),

            GongdouD3BellLimitKeywordPower => ModelDb.Power<BufferPower>(),
            GongdouD3StickyHandKeywordPower => ModelDb.Power<FrailPower>(),
            GongdouD3FalseBladeKeywordPower => ModelDb.Power<WeakPower>(),
            GongdouD3ReadKeywordPower => ModelDb.Power<FocusPower>(),
            GongdouD3WrongDiscardArmorKeywordPower => ModelDb.Power<VulnerablePower>(),
            GongdouD3ChainBreachKeywordPower => ModelDb.Power<StrengthPower>(),
            GongdouD3NoDexterityArmorKeywordPower => ModelDb.Power<RegenPower>(),

            GongdouD4BurnKeywordPower => ModelDb.Power<PoisonPower>(),
            GongdouD4OrderKeywordPower => ModelDb.Power<FocusPower>(),
            GongdouD4CountdownKeywordPower => ModelDb.Power<WeakPower>(),
            GongdouD4OverheatKeywordPower => ModelDb.Power<VulnerablePower>(),
            GongdouD4FireBreathingPower => ModelDb.Power<StrengthPower>(),
            GongdouD4EvolvePower => ModelDb.Power<DexterityPower>(),

            GongdouD5OverloadLockKeywordPower => ModelDb.Power<FrailPower>(),
            GongdouD5VoidKeywordPower => ModelDb.Power<WeakPower>(),
            GongdouD5VoidCollapseKeywordPower => ModelDb.Power<PoisonPower>(),
            GongdouD5ArtifactKeywordPower => ModelDb.Power<ArtifactPower>(),
            GongdouD5PotionPhaseKeywordPower => ModelDb.Power<BufferPower>(),
            GongdouD5PotionPhaseTurn3KeywordPower => ModelDb.Power<StrengthPower>(),
            GongdouD5PotionPhaseTurn4KeywordPower => ModelDb.Power<DexterityPower>(),
            GongdouD5PotionPhaseTurn5KeywordPower => ModelDb.Power<RegenPower>(),

            GongdouD6StanceKeywordPower => ModelDb.Power<FocusPower>(),
            GongdouD6NormalKeywordPower => ModelDb.Power<ArtifactPower>(),
            GongdouD6WrathKeywordPower => ModelDb.Power<StrengthPower>(),
            GongdouD6CalmKeywordPower => ModelDb.Power<RegenPower>(),
            GongdouD6CalmBreachKeywordPower => ModelDb.Power<VulnerablePower>(),

            GongdouD7PoisonKeywordPower => ModelDb.Power<PoisonPower>(),
            GongdouD7PoisonFogKeywordPower => ModelDb.Power<FocusPower>(),
            GongdouD7CaltropsKeywordPower => ModelDb.Power<ThornsPower>(),
            GongdouD7AntidoteKeywordPower => ModelDb.Power<RegenPower>(),

            GongdouD8OrbSlotKeywordPower => ModelDb.Power<BufferPower>(),
            GongdouD8LightningKeywordPower => ModelDb.Power<StrengthPower>(),
            GongdouD8FrostKeywordPower => ModelDb.Power<RegenPower>(),
            GongdouD8DarkKeywordPower => ModelDb.Power<PoisonPower>(),
            GongdouD8LoopKeywordPower => ModelDb.Power<DexterityPower>(),
            GongdouD8FocusKeywordPower => ModelDb.Power<FocusPower>(),
            GongdouD8InsulationKeywordPower => ModelDb.Power<ArtifactPower>(),

            GongdouD9MirrorDrawKeywordPower => ModelDb.Power<BufferPower>(),
            GongdouD9MantraKeywordPower => ModelDb.Power<FocusPower>(),
            GongdouD9DivinityKeywordPower => ModelDb.Power<StrengthPower>(),
            GongdouD9ExhaustKeywordPower => ModelDb.Power<FrailPower>(),
            GongdouD9ArmorReflectKeywordPower => ModelDb.Power<VulnerablePower>(),
            GongdouD9EnemyArmorKeywordPower => ModelDb.Power<ArtifactPower>(),

            GongdouD10RiftDrawKeywordPower => ModelDb.Power<BufferPower>(),
            GongdouD10ChargeKeywordPower => ModelDb.Power<StrengthPower>(),
            GongdouD10EchoKeywordPower => ModelDb.Power<FocusPower>(),
            GongdouD10OverheatKeywordPower => ModelDb.Power<FrailPower>(),
            GongdouD10PhaseGateKeywordPower => ModelDb.Power<ArtifactPower>(),
            GongdouD10MarkKeywordPower => ModelDb.Power<VulnerablePower>(),
            GongdouD10DelayedDamageKeywordPower => ModelDb.Power<PoisonPower>(),
            GongdouD10EnemyArmorKeywordPower => ModelDb.Power<RegenPower>(),
            GongdouD10MarkDecayKeywordPower => ModelDb.Power<WeakPower>(),
            GongdouD10ArmorChargeKeywordPower => ModelDb.Power<HardenedShellPower>(),
            GongdouD10ResonatorKeywordPower => ModelDb.Power<BufferPower>(),
            GongdouD10PrismShardKeywordPower => ModelDb.Power<ArtifactPower>(),
            GongdouD10MarkResonanceKeywordPower => ModelDb.Power<DexterityPower>(),

            _ => ResolveFallbackKeywordIconSource(power.GetType())
        };
    }

    private static PowerModel? ResolveIconSource(PowerModel power)
    {
        return power switch
        {
            GongdouPoisonFogPower => ModelDb.Power<PoisonPower>(),
            GongdouNextTurnBlockPower => ModelDb.Power<HardenedShellPower>(),
            GongdouBurstPotionPower => ModelDb.Power<StrengthPower>(),
            GongdouStageRulePower => ModelDb.Power<FocusPower>(),
            GongdouThinDeckRulePower => ModelDb.Power<DexterityPower>(),
            GongdouCrackedArmorRulePower => ModelDb.Power<VulnerablePower>(),
            GongdouMaintenanceRulePower => ModelDb.Power<BufferPower>(),
            GongdouPersistentArmorRulePower => ModelDb.Power<HardenedShellPower>(),
            GongdouKeywordPower => ResolveKeywordIconSource(power),
            _ => null
        };
    }

    private static string? ResolveLocKey(PowerModel power)
    {
        return power switch
        {
            GongdouPoisonFogPower => nameof(GongdouPoisonFogPower),
            GongdouNextTurnBlockPower => nameof(GongdouNextTurnBlockPower),
            GongdouBurstPotionPower => nameof(GongdouBurstPotionPower),
            GongdouStageRulePower => nameof(GongdouStageRulePower),
            GongdouThinDeckRulePower => nameof(GongdouThinDeckRulePower),
            GongdouCrackedArmorRulePower => nameof(GongdouCrackedArmorRulePower),
            GongdouMaintenanceRulePower => nameof(GongdouMaintenanceRulePower),
            GongdouPersistentArmorRulePower => nameof(GongdouPersistentArmorRulePower),
            GongdouKeywordPower => power.GetType().Name,
            _ => null
        };
    }

    private static IEnumerable<IHoverTip> BuildHoverTips(PowerModel power, string locKey)
    {
        if (!power.IsVisible)
        {
            return [];
        }

        var title = new LocString("powers", $"{locKey}.title");
        var description = new LocString("powers", $"{locKey}.description");
        var amount = power switch
        {
            GongdouD3NoDexterityArmorKeywordPower => GongdouPuzzleRuntime.GetD3NoCunningArmorDisplayAmount(),
            GongdouD4BurnKeywordPower => GongdouPuzzleRuntime.GetD4BurnShuffleDisplayAmount(),
            GongdouD7AntidoteKeywordPower => GongdouPuzzleRuntime.GetD7AntidoteDisplayAmount(),
            _ => power.Amount
        };
        description.Add("Amount", amount);
        description.Add("DrawCount", amount);
        description.Add("PhaseTurn", amount);
        description.Add("Charge", amount);
        description.Add("Echo", amount);
        description.Add("Mark", amount);
        description.Add("DelayedDamage", amount);
        description.Add("BonusDamage", power is GongdouD10OverheatKeywordPower ? amount * 4m : amount);
        description.Add("Block", power is GongdouD7AntidoteKeywordPower
            ? GongdouPuzzleRuntime.GetD7AntidoteDisplayBlock()
            : amount * 2m);

        var tips = new List<IHoverTip>
        {
            new HoverTip(title, description, power.Icon)
            {
                IsInstanced = power.InstanceType != PowerInstanceType.None
            }
        };

        if (power is GongdouD4BurnKeywordPower)
        {
            tips.Add(new HoverTip(
                new LocString("powers", "GongdouCardKeyword.Burn.title"),
                new LocString("powers", "GongdouCardKeyword.Burn.description"),
                power.Icon));
        }

        return tips;
    }

    private static IEnumerable<IHoverTip> BuildD7PoisonHoverTips(PowerModel power)
    {
        if (!power.IsVisible)
        {
            return [];
        }

        return
        [
            new HoverTip(
                new LocString("powers", "GongdouD7PoisonKeywordPower.title"),
                new LocString("powers", "GongdouD7PoisonKeywordPower.description"),
                power.Icon)
            {
                IsInstanced = power.InstanceType != PowerInstanceType.None
            }
        ];
    }

    [HarmonyPatch(typeof(PowerModel), "get_Icon")]
    private static class IconPatch
    {
        private static bool Prefix(PowerModel __instance, ref Texture2D __result)
        {
            var iconSource = ResolveIconSource(__instance);
            if (iconSource == null)
            {
                return true;
            }

            __result = iconSource.Icon;
            return false;
        }
    }

    [HarmonyPatch(typeof(PowerModel), "get_BigIcon")]
    private static class BigIconPatch
    {
        private static bool Prefix(PowerModel __instance, ref Texture2D __result)
        {
            var iconSource = ResolveIconSource(__instance);
            if (iconSource == null)
            {
                return true;
            }

            __result = iconSource.BigIcon;
            return false;
        }
    }

    [HarmonyPatch(typeof(PowerModel), "get_HoverTips")]
    private static class HoverTipsPatch
    {
        private static bool Prefix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
        {
            if (__instance is PoisonPower && GongdouPuzzleRuntime.CurrentStage == 7)
            {
                __result = BuildD7PoisonHoverTips(__instance);
                return false;
            }

            var locKey = ResolveLocKey(__instance);
            if (locKey == null)
            {
                return true;
            }

            __result = BuildHoverTips(__instance, locKey);
            return false;
        }
    }

    [HarmonyPatch(typeof(RelicModel), "get_HoverTips")]
    private static class RelicHoverTipsPatch
    {
        private static bool Prefix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
        {
            if (__instance is not GongdouChallengeRelic)
            {
                return true;
            }

            var locKey = __instance.GetType().Name;
            var title = new LocString("relics", $"{locKey}.title");
            var description = new LocString("relics", $"{locKey}.description");
            __result = [new HoverTip(title, description, __instance.Icon)];
            return false;
        }
    }

    [HarmonyPatch]
    private static class InspectRelicOpenPatch
    {
        private static readonly FieldInfo? AllUnlockedRelicsField =
            typeof(NInspectRelicScreen).GetField("_allUnlockedRelics", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo? UpdateRelicDisplayMethod =
            typeof(NInspectRelicScreen).GetMethod("UpdateRelicDisplay", BindingFlags.Instance | BindingFlags.NonPublic);

        private static MethodBase? TargetMethod()
        {
            return AccessTools.Method(
                typeof(NInspectRelicScreen),
                "Open",
                [typeof(IReadOnlyList<RelicModel>), typeof(RelicModel)]);
        }

        private static void Postfix(NInspectRelicScreen __instance, IReadOnlyList<RelicModel> relics)
        {
            if (AllUnlockedRelicsField?.GetValue(__instance) is not HashSet<RelicModel> unlocked)
            {
                return;
            }

            var changed = false;
            foreach (var relic in relics)
            {
                if (relic is GongdouChallengeRelic)
                {
                    changed |= unlocked.Add(relic.CanonicalInstance);
                }
            }

            if (changed)
            {
                UpdateRelicDisplayMethod?.Invoke(__instance, null);
            }
        }
    }
}
