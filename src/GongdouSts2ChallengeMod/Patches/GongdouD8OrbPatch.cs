using GongdouSts2ChallengeMod.Challenges;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Orbs;

namespace GongdouSts2ChallengeMod.Patches;

internal static class GongdouD8OrbPatch
{
    [HarmonyPatch(typeof(FrostOrb), nameof(FrostOrb.Passive))]
    private static class FrostPassivePatch
    {
        private static bool Prefix(
            FrostOrb __instance,
            PlayerChoiceContext choiceContext,
            Creature? target,
            ref Task __result)
        {
            if (!GongdouPuzzleRuntime.IsD8Active)
            {
                return true;
            }

            __result = GongdouPuzzleRuntime.D8FrostPassive(choiceContext, __instance, target);
            return false;
        }
    }

    [HarmonyPatch(typeof(FrostOrb), nameof(FrostOrb.Evoke))]
    private static class FrostEvokePatch
    {
        private static bool Prefix(
            FrostOrb __instance,
            PlayerChoiceContext playerChoiceContext,
            ref Task<IEnumerable<Creature>> __result)
        {
            if (!GongdouPuzzleRuntime.IsD8Active)
            {
                return true;
            }

            __result = GongdouPuzzleRuntime.D8FrostEvoke(playerChoiceContext, __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(DarkOrb), nameof(DarkOrb.Passive))]
    private static class DarkPassivePatch
    {
        private static bool Prefix(
            DarkOrb __instance,
            PlayerChoiceContext choiceContext,
            Creature? target,
            ref Task __result)
        {
            if (!GongdouPuzzleRuntime.IsD8Active)
            {
                return true;
            }

            __result = GongdouPuzzleRuntime.D8DarkPassive(choiceContext, __instance, target);
            return false;
        }
    }
}
