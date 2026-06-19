using GongdouSts2ChallengeMod.Challenges;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;

namespace GongdouSts2ChallengeMod.Patches;

[HarmonyPatch(typeof(NPauseMenu), "OnSaveAndQuitButtonPressed")]
internal static class GongdouPauseMenuSaveAndQuitPatch
{
    private static bool Prefix()
    {
        return !ChallengeSessionManager.TryHandlePauseSaveAndQuit();
    }
}
