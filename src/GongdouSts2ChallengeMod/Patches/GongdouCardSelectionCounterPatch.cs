using GongdouSts2ChallengeMod.Preparation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace GongdouSts2ChallengeMod.Patches;

[HarmonyPatch(typeof(NSimpleCardSelectScreen), "OnCardClicked")]
internal static class GongdouCardSelectionCounterPatch
{
    private static void Postfix(NSimpleCardSelectScreen __instance)
    {
        NativeChallengePreparationFlow.RefreshCardSelectionCounterFromNative(__instance);
    }
}
