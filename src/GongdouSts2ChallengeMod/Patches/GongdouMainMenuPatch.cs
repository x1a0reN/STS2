using Godot;
using GongdouSts2ChallengeMod.Challenges;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.addons.mega_text;

namespace GongdouSts2ChallengeMod.Patches;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class GongdouMainMenuPatch
{
    private const string ButtonName = "GongDouChallengeButton";
    private const int DuplicateVisualFlags = 12;

    private static void Postfix(NMainMenu __instance)
    {
        try
        {
            var buttonRoot = __instance.GetNodeOrNull<Control>("MainMenuTextButtons")
                ?? __instance.GetNodeOrNull<Control>("%MainMenuTextButtons");
            if (buttonRoot == null || buttonRoot.GetNodeOrNull<Node>(ButtonName) != null)
            {
                return;
            }

            var template = buttonRoot.GetNodeOrNull<NMainMenuTextButton>("MultiplayerButton")
                ?? buttonRoot.GetChildren()
                    .OfType<NMainMenuTextButton>()
                    .FirstOrDefault(child => child.Name != ButtonName);
            if (template == null)
            {
                GD.PrintErr("[GongDou STS2] Main menu GongDou entry skipped: no native text button template found.");
                return;
            }

            var button = CreateMenuButton(template);
            if (button == null)
            {
                return;
            }
            button.Name = ButtonName;
            SetMenuButtonText(button, "\u5171\u6597");
            button.FocusMode = template.FocusMode;
            button.MouseFilter = template.MouseFilter;
            button.Visible = true;
            button.Enable();
            button.Connect(
                NClickableControl.SignalName.Released,
                Callable.From(() => ChallengeSessionManager.StartFromMainMenuButton()));

            buttonRoot.AddChild(button);
            ConnectFocusReticle(__instance, button);

            // Keep it near the normal game-entry buttons instead of hiding it below Quit.
            var targetIndex = Math.Min(3, buttonRoot.GetChildCount() - 1);
            buttonRoot.MoveChild(button, targetIndex);
            GD.Print("[GongDou STS2] Main menu GongDou entry added.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to add main menu GongDou entry: {ex}");
        }
    }

    private static NMainMenuTextButton? CreateMenuButton(NMainMenuTextButton template)
    {
        try
        {
            if (template.Duplicate(DuplicateVisualFlags) is NMainMenuTextButton duplicated)
            {
                ClearCopiedLocalization(duplicated);
                return duplicated;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to duplicate menu button template: {ex}");
        }

        GD.PrintErr("[GongDou STS2] Main menu GongDou entry skipped: native text button duplication failed.");
        return null;
    }

    private static void SetMenuButtonText(NMainMenuTextButton button, string text)
    {
        ClearCopiedLocalization(button);
        if (button.label != null)
        {
            button.label.Text = text;
        }

        foreach (var label in DescendantsOfType<MegaLabel>(button))
        {
            label.Text = text;
        }

        foreach (var label in DescendantsOfType<Label>(button))
        {
            label.Text = text;
        }
    }

    private static IEnumerable<T> DescendantsOfType<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var descendant in DescendantsOfType<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void ClearCopiedLocalization(NMainMenuTextButton button)
    {
        try
        {
            var field = typeof(NMainMenuTextButton).GetField(
                "_locString",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(button, null);
        }
        catch
        {
            // Cosmetic only; if reflection fails the explicitly assigned label text still applies now.
        }
    }

    private static void ConnectFocusReticle(NMainMenu mainMenu, NMainMenuTextButton button)
    {
        try
        {
            button.Connect(
                NClickableControl.SignalName.Unfocused,
                Callable.From<NClickableControl>(_ => mainMenu.Call("MainMenuButtonUnfocused", button)));
            button.Connect(
                NClickableControl.SignalName.Focused,
                Callable.From<NClickableControl>(_ =>
                    Callable.From(() => mainMenu.Call("MainMenuButtonFocused", button)).CallDeferred()));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to connect native menu focus reticle: {ex.Message}");
        }
    }
}
