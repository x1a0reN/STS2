using Godot;
using GongdouSts2ChallengeMod.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;

namespace GongdouSts2ChallengeMod.Patches;

internal static class GongdouPotionIconPatch
{
    private static readonly Dictionary<string, string> SpriteBasesByModelKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GongdouPoisonPotion12"] = "poison_potion",
        ["GongdouD7PoisonPotion"] = "poison_potion",
        ["GongdouD7CatalystPotion"] = "poison_potion",
        ["GongdouVulnerablePotion2"] = "vulnerable_potion",
        ["GongdouD5BarrierPotion"] = "vulnerable_potion",
        ["GongdouD10ShatterArmorPotion"] = "vulnerable_potion",
        ["GongdouWeakPotion2"] = "weak_potion",
        ["GongdouBurstPotion"] = "explosive_ampoule",
        ["GongdouD2FirePotion"] = "fire_potion",
        ["GongdouD4FirePotion"] = "fire_potion",
        ["GongdouD6FirePotion"] = "fire_potion",
        ["GongdouD3CunningPotion"] = "cunning_potion",
        ["GongdouD4ClarityPotion"] = "clarity",
        ["GongdouD4GhostPotion"] = "ghost_in_a_jar",
        ["GongdouD5EnergyPotion"] = "energy_potion",
        ["GongdouD6RagePotion"] = "energy_potion",
        ["GongdouD10TimePotion"] = "energy_potion",
        ["GongdouD10EchoPotion"] = "energy_potion",
        ["GongdouD6CalmPotion"] = "block_potion",
        ["GongdouD8FocusPotion"] = "focus_potion",
        ["GongdouD8DarkPotion"] = "essence_of_darkness",
        ["GongdouD8EvokePotion"] = "skill_potion",
        ["GongdouD9DivinityPotion"] = "radiant_tincture",
        ["GongdouD9MantraPotion"] = "radiant_tincture",
        ["GongdouD9MirrorBreakPotion"] = "shackling_potion"
    };

    private static readonly string[] FallbackSpriteBases =
    [
        "fire_potion",
        "energy_potion",
        "vulnerable_potion",
        "weak_potion",
        "poison_potion"
    ];

    private static IEnumerable<IHoverTip> BuildHoverTips(PotionModel potion)
    {
        var locKey = potion.GetType().Name;
        var title = new LocString("potions", $"{locKey}.title");
        var description = new LocString("potions", $"{locKey}.description");

        try
        {
            potion.DynamicVars.AddTo(description);
        }
        catch
        {
            // 原版药水 hover 会反查 PotionPool；自定义药水不应因为动态变量失败拖崩界面。
        }

        return [new HoverTip(title, description, potion.Image)];
    }

    private static PotionModel? ResolveIconSource(PotionModel potion)
    {
        try
        {
            return potion.GetType().Name switch
            {
                nameof(GongdouPoisonPotion12) => ModelDb.Potion<PoisonPotion>(),
                nameof(GongdouD7PoisonPotion) => ModelDb.Potion<PoisonPotion>(),
                nameof(GongdouD7CatalystPotion) => ModelDb.Potion<PoisonPotion>(),
                nameof(GongdouVulnerablePotion2) => ModelDb.Potion<VulnerablePotion>(),
                nameof(GongdouD5BarrierPotion) => ModelDb.Potion<VulnerablePotion>(),
                nameof(GongdouD10ShatterArmorPotion) => ModelDb.Potion<VulnerablePotion>(),
                nameof(GongdouWeakPotion2) => ModelDb.Potion<WeakPotion>(),
                nameof(GongdouBurstPotion) => ModelDb.Potion<ExplosiveAmpoule>(),
                nameof(GongdouD2FirePotion) => ModelDb.Potion<FirePotion>(),
                nameof(GongdouD4FirePotion) => ModelDb.Potion<FirePotion>(),
                nameof(GongdouD6FirePotion) => ModelDb.Potion<FirePotion>(),
                nameof(GongdouD3CunningPotion) => ModelDb.Potion<CunningPotion>(),
                nameof(GongdouD4ClarityPotion) => ModelDb.Potion<Clarity>(),
                nameof(GongdouD4GhostPotion) => ModelDb.Potion<GhostInAJar>(),
                nameof(GongdouD5EnergyPotion) => ModelDb.Potion<EnergyPotion>(),
                nameof(GongdouD6RagePotion) => ModelDb.Potion<EnergyPotion>(),
                nameof(GongdouD10TimePotion) => ModelDb.Potion<EnergyPotion>(),
                nameof(GongdouD10EchoPotion) => ModelDb.Potion<EnergyPotion>(),
                nameof(GongdouD6CalmPotion) => ModelDb.Potion<BlockPotion>(),
                nameof(GongdouD8FocusPotion) => ModelDb.Potion<FocusPotion>(),
                nameof(GongdouD8DarkPotion) => ModelDb.Potion<EssenceOfDarkness>(),
                nameof(GongdouD8EvokePotion) => ModelDb.Potion<SkillPotion>(),
                nameof(GongdouD9DivinityPotion) => ModelDb.Potion<RadiantTincture>(),
                nameof(GongdouD9MantraPotion) => ModelDb.Potion<RadiantTincture>(),
                nameof(GongdouD9MirrorBreakPotion) => ModelDb.Potion<ShacklingPotion>(),
                _ when IsGongdouPotion(potion) => ModelDb.Potion<FirePotion>(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool IsGongdouPotion(PotionModel potion)
    {
        return potion.GetType().Namespace?.StartsWith("GongdouSts2ChallengeMod.", StringComparison.Ordinal) == true ||
               NormalizeModelKey(potion.Id.Entry).StartsWith("Gongdou", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetSpriteBase(PotionModel potion, out string spriteBase)
    {
        if (SpriteBasesByModelKey.TryGetValue(potion.GetType().Name, out var mappedSpriteBase) ||
            SpriteBasesByModelKey.TryGetValue(NormalizeModelKey(potion.Id.Entry), out mappedSpriteBase))
        {
            spriteBase = mappedSpriteBase;
            return true;
        }

        if (IsGongdouPotion(potion))
        {
            spriteBase = "fire_potion";
            return true;
        }

        spriteBase = string.Empty;
        return false;
    }

    private static string NormalizeModelKey(string id)
    {
        var normalized = id.Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Trim();

        return normalized.StartsWith("D", StringComparison.OrdinalIgnoreCase)
            ? $"Gongdou{normalized}"
            : normalized;
    }

    private static string PotionSpritePath(string spriteBase)
    {
        return ImageHelper.GetImagePath($"atlases/potion_atlas.sprites/{spriteBase}.tres");
    }

    private static string PotionOutlinePath(string spriteBase)
    {
        return ImageHelper.GetImagePath($"atlases/potion_outline_atlas.sprites/{spriteBase}.tres");
    }

    private static string ResolveExistingPotionSpritePath(string spriteBase)
    {
        var path = PotionSpritePath(spriteBase);
        if (ResourceLoader.Exists(path))
        {
            return path;
        }

        foreach (var fallback in FallbackSpriteBases)
        {
            var fallbackPath = PotionSpritePath(fallback);
            if (ResourceLoader.Exists(fallbackPath))
            {
                return fallbackPath;
            }
        }

        return path;
    }

    private static string? ResolveExistingPotionOutlinePath(string spriteBase)
    {
        var path = PotionOutlinePath(spriteBase);
        if (ResourceLoader.Exists(path))
        {
            return path;
        }

        foreach (var fallback in FallbackSpriteBases)
        {
            var fallbackPath = PotionOutlinePath(fallback);
            if (ResourceLoader.Exists(fallbackPath))
            {
                return fallbackPath;
            }
        }

        return null;
    }

    [HarmonyPatch(typeof(PotionModel), "get_ImagePath")]
    private static class ImagePathPatch
    {
        private static bool Prefix(PotionModel __instance, ref string __result)
        {
            var iconSource = ResolveIconSource(__instance);
            if (iconSource != null)
            {
                __result = iconSource.ImagePath;
                return false;
            }

            if (!TryGetSpriteBase(__instance, out var spriteBase))
            {
                return true;
            }

            __result = ResolveExistingPotionSpritePath(spriteBase);
            return false;
        }
    }

    [HarmonyPatch(typeof(PotionModel), "get_HoverTips")]
    private static class HoverTipsPatch
    {
        private static bool Prefix(PotionModel __instance, ref IEnumerable<IHoverTip> __result)
        {
            if (!IsGongdouPotion(__instance))
            {
                return true;
            }

            __result = BuildHoverTips(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(PotionModel), "get_Image")]
    private static class ImagePatch
    {
        private static bool Prefix(PotionModel __instance, ref Texture2D __result)
        {
            var iconSource = ResolveIconSource(__instance);
            if (iconSource != null)
            {
                __result = iconSource.Image;
                return false;
            }

            if (!TryGetSpriteBase(__instance, out var spriteBase))
            {
                return true;
            }

            __result = ResourceLoader.Load<Texture2D>(
                ResolveExistingPotionSpritePath(spriteBase),
                null,
                ResourceLoader.CacheMode.Reuse);
            return false;
        }
    }

    [HarmonyPatch(typeof(PotionModel), "get_OutlinePath")]
    private static class OutlinePathPatch
    {
        private static bool Prefix(PotionModel __instance, ref string? __result)
        {
            var iconSource = ResolveIconSource(__instance);
            if (iconSource != null)
            {
                __result = iconSource.OutlinePath;
                return false;
            }

            if (!TryGetSpriteBase(__instance, out var spriteBase))
            {
                return true;
            }

            __result = ResolveExistingPotionOutlinePath(spriteBase);
            return false;
        }
    }

    [HarmonyPatch(typeof(PotionModel), "get_Outline")]
    private static class OutlinePatch
    {
        private static bool Prefix(PotionModel __instance, ref Texture2D? __result)
        {
            var iconSource = ResolveIconSource(__instance);
            if (iconSource != null)
            {
                __result = iconSource.Outline;
                return false;
            }

            if (!TryGetSpriteBase(__instance, out var spriteBase))
            {
                return true;
            }

            var path = ResolveExistingPotionOutlinePath(spriteBase);
            __result = path != null
                ? ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse)
                : null;
            return false;
        }
    }
}
