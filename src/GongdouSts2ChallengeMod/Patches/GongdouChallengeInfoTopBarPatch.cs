using Godot;
using HarmonyLib;
using GongdouSts2ChallengeMod.Challenges;
using GongdouSts2ChallengeMod.Monsters;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace GongdouSts2ChallengeMod.Patches;

[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
internal static class GongdouChallengeInfoTopBarPatch
{
    private static void Postfix(NTopBar __instance)
    {
        GongdouChallengeInfoTopBar.EnsureButton(__instance);
    }
}

internal static class GongdouChallengeInfoTopBar
{
    private const string ButtonName = "GongDouChallengeInfoButton";
    private const float NativeButtonGap = 8f;
    private static readonly StringName ShaderV = new("v");

    public static void EnsureButton(NTopBar topBar)
    {
        if (ResolveConfig() == null)
        {
            topBar.GetNodeOrNull<Button>(ButtonName)?.QueueFree();
            return;
        }

        var button = topBar.GetNodeOrNull<Button>(ButtonName);
        if (button == null)
        {
            button = CreateButton(topBar);
            topBar.AddChild(button);
        }

        button.Visible = true;
        Callable.From(() => PositionButton(topBar, button)).CallDeferred();
    }

    private static Button CreateButton(NTopBar topBar)
    {
        var anchor = ResolveAnchor(topBar);
        var buttonSize = ResolveNativeButtonSize(anchor);
        var button = new Button
        {
            Name = ButtonName,
            Text = "",
            TooltipText = "",
            CustomMinimumSize = buttonSize,
            Size = buttonSize,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All,
            Flat = true
        };

        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());

        var visual = CreateNativeTopBarIconVisual(topBar);
        button.AddChild(visual);

        var icon = ResolveIconControl(visual);
        if (icon != null)
        {
            Callable.From(() => icon.PivotOffset = icon.Size * 0.5f).CallDeferred();
        }

        AttachNativeTopBarBehavior(button, icon, icon?.Material as ShaderMaterial);
        return button;
    }

    private static void PositionButton(NTopBar topBar, Button button)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(topBar) || !GodotObject.IsInstanceValid(button))
            {
                return;
            }

            Control? anchor = ResolveAnchor(topBar);
            if (anchor == null || !GodotObject.IsInstanceValid(anchor))
            {
                return;
            }

            var buttonSize = ResolveNativeButtonSize(anchor);
            button.CustomMinimumSize = buttonSize;
            button.Size = buttonSize;
            button.GlobalPosition = anchor.GlobalPosition - new Vector2(buttonSize.X + NativeButtonGap, 0f);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to position challenge info button: {ex.Message}");
        }
    }

    private static Control? ResolveAnchor(NTopBar topBar)
    {
        if (topBar.Map != null)
        {
            return topBar.Map;
        }

        if (topBar.Deck != null)
        {
            return topBar.Deck;
        }

        return topBar.Pause;
    }

    private static Vector2 ResolveNativeButtonSize(Control? anchor)
    {
        if (anchor != null && GodotObject.IsInstanceValid(anchor))
        {
            if (anchor.Size.X > 0f && anchor.Size.Y > 0f)
            {
                return anchor.Size;
            }

            if (anchor.CustomMinimumSize.X > 0f && anchor.CustomMinimumSize.Y > 0f)
            {
                return anchor.CustomMinimumSize;
            }
        }

        return new Vector2(72f, 72f);
    }

    private static Control CreateNativeTopBarIconVisual(NTopBar topBar)
    {
        var nativeVisual = TryCloneNativeTopBarVisual(topBar);
        if (nativeVisual != null)
        {
            return nativeVisual;
        }

        var holder = new Control
        {
            Name = "NativeInfoIconHolder",
            CustomMinimumSize = ResolveNativeButtonSize(ResolveAnchor(topBar)),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        FillButtonChild(holder);

        var texture = TryLoadNativeInfoIconTexture();
        if (texture != null)
        {
            var icon = new TextureRect
            {
                Name = "NativeInfoIcon",
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(0.96f, 0.94f, 0.86f, 1f)
            };
            CenterButtonChild(icon, new Vector2(54f, 54f));
            holder.AddChild(icon);
            return holder;
        }

        var fallback = new Label
        {
            Text = "i",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        fallback.AddThemeFontSizeOverride("font_size", 34);
        fallback.AddThemeColorOverride("font_color", new Color(0.96f, 0.94f, 0.86f, 1f));
        FillButtonChild(fallback);
        holder.AddChild(fallback);
        return holder;
    }

    private static Control? TryCloneNativeTopBarVisual(NTopBar topBar)
    {
        var anchor = ResolveAnchor(topBar);
        var template = anchor?.GetNodeOrNull<Control>("Control");
        if (template == null || !GodotObject.IsInstanceValid(template))
        {
            return null;
        }

        try
        {
            if (template.Duplicate() is not Control clone)
            {
                return null;
            }

            clone.Name = "Control";
            clone.MouseFilter = Control.MouseFilterEnum.Ignore;
            FillButtonChild(clone);
            SetMouseFilterRecursive(clone, Control.MouseFilterEnum.Ignore);

            var icon = clone.GetNodeOrNull<Control>("Icon");
            if (icon?.Material != null)
            {
                icon.Material = (Material)icon.Material.Duplicate();
            }

            return clone;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to clone native top bar visual: {ex.Message}");
            return null;
        }
    }

    private static void AttachNativeTopBarBehavior(Button button, Control? icon, ShaderMaterial? hsv)
    {
        Tween? activeTween = null;
        var pointerInside = false;
        var keyboardFocused = false;
        var isFocused = false;
        var lastOpenAtUtc = DateTimeOffset.MinValue;

        void InvokeOpen()
        {
            var now = DateTimeOffset.UtcNow;
            if (now - lastOpenAtUtc < TimeSpan.FromMilliseconds(250))
            {
                return;
            }

            lastOpenAtUtc = now;
            ClearHoverState();
            ShowInfoScreen();
        }

        void KillTween()
        {
            if (activeTween != null && GodotObject.IsInstanceValid(activeTween))
            {
                activeTween.Kill();
            }

            activeTween = null;
        }

        void AnimateHover()
        {
            if (icon == null || !GodotObject.IsInstanceValid(icon))
            {
                return;
            }

            KillTween();
            hsv?.SetShaderParameter(ShaderV, 1.1f);
            activeTween = button.CreateTween();
            activeTween.TweenProperty(icon, "rotation", -(float)Math.PI / 15f, 0.5f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            activeTween.Parallel().TweenProperty(icon, "scale", Vector2.One * 1.1f, 0.18f)
                .SetTrans(Tween.TransitionType.Expo)
                .SetEase(Tween.EaseType.Out);
        }

        void AnimateUnhover()
        {
            if (icon == null || !GodotObject.IsInstanceValid(icon))
            {
                return;
            }

            KillTween();
            activeTween = button.CreateTween();
            activeTween.TweenProperty(icon, "rotation", 0f, 0.75f)
                .SetTrans(Tween.TransitionType.Spring)
                .SetEase(Tween.EaseType.Out);
            activeTween.Parallel().TweenProperty(icon, "scale", Vector2.One, 0.45f)
                .SetTrans(Tween.TransitionType.Expo)
                .SetEase(Tween.EaseType.Out);
            activeTween.Finished += () => hsv?.SetShaderParameter(ShaderV, 0.9f);
        }

        void ClearHoverState()
        {
            pointerInside = false;
            keyboardFocused = false;
            isFocused = false;
            NHoverTipSet.Remove(button);
            KillTween();
            if (icon != null && GodotObject.IsInstanceValid(icon))
            {
                icon.Rotation = 0f;
                icon.Scale = Vector2.One;
            }

            hsv?.SetShaderParameter(ShaderV, 0.9f);
            if (button.HasFocus())
            {
                button.ReleaseFocus();
            }
        }

        void ShowNativeTip()
        {
            NHoverTipSet.Remove(button);
            var tip = NHoverTipSet.CreateAndShow(button, CreateChallengeInfoHoverTip());
            tip.GlobalPosition = button.GlobalPosition + new Vector2(button.Size.X - tip.Size.X, button.Size.Y + 20f);
        }

        void RefreshFocus()
        {
            var shouldFocus = pointerInside || keyboardFocused;
            if (shouldFocus == isFocused)
            {
                return;
            }

            isFocused = shouldFocus;
            if (isFocused)
            {
                PlayUiSfx("event:/sfx/ui/clicks/ui_hover");
                AnimateHover();
                ShowNativeTip();
            }
            else
            {
                NHoverTipSet.Remove(button);
                AnimateUnhover();
            }
        }

        button.MouseEntered += () =>
        {
            pointerInside = true;
            RefreshFocus();
        };
        button.MouseExited += () =>
        {
            pointerInside = false;
            RefreshFocus();
        };
        button.FocusEntered += () =>
        {
            keyboardFocused = true;
            RefreshFocus();
        };
        button.FocusExited += () =>
        {
            keyboardFocused = false;
            RefreshFocus();
        };
        button.ButtonDown += () =>
        {
            PlayUiSfx("event:/sfx/ui/clicks/ui_click");
            if (icon == null || !GodotObject.IsInstanceValid(icon))
            {
                return;
            }

            KillTween();
            hsv?.SetShaderParameter(ShaderV, 0.4f);
            activeTween = button.CreateTween();
            activeTween.TweenProperty(icon, "rotation", icon.Rotation + (float)Math.PI * 2f / 15f, 0.2f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        };
        button.ButtonUp += () =>
        {
            if (isFocused)
            {
                AnimateHover();
            }
        };
        button.Pressed += InvokeOpen;
        button.GuiInput += input =>
        {
            if (IsLeftMouseRelease(input) || input.IsActionPressed("ui_accept"))
            {
                InvokeOpen();
                button.AcceptEvent();
            }
        };
        button.TreeExiting += () =>
        {
            NHoverTipSet.Remove(button);
            KillTween();
        };
    }

    private static HoverTip CreateChallengeInfoHoverTip()
    {
        return new HoverTip(
            new LocString("static_hover_tips", "GONGDOU_CHALLENGE_INFO.title"),
            new LocString("static_hover_tips", "GONGDOU_CHALLENGE_INFO.description"));
    }

    private static bool IsLeftMouseRelease(InputEvent input)
    {
        return input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false };
    }

    private static void PlayUiSfx(string eventPath)
    {
        try
        {
            SfxCmd.Play(eventPath);
        }
        catch
        {
            // Audio can be unavailable during very early UI bootstrap.
        }
    }

    private static Control? ResolveIconControl(Control visual)
    {
        return visual.GetNodeOrNull<Control>("Icon")
            ?? visual.GetNodeOrNull<Control>("Control/Icon")
            ?? FindNamedControl(visual, "Icon")
            ?? FindNamedControl(visual, "NativeInfoIcon");
    }

    private static Control? FindNamedControl(Node root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Control control && string.Equals(control.Name.ToString(), name, StringComparison.Ordinal))
            {
                return control;
            }

            var nested = FindNamedControl(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void SetMouseFilterRecursive(Node node, Control.MouseFilterEnum mouseFilter)
    {
        if (node is Control control)
        {
            control.MouseFilter = mouseFilter;
        }

        foreach (var child in node.GetChildren())
        {
            SetMouseFilterRecursive(child, mouseFilter);
        }
    }

    private static Texture2D? TryLoadNativeInfoIconTexture()
    {
        string[] paths =
        [
            "res://images/ui/emote/question.png",
            "res://images/packed/statistics_screen/stats_questionmark.png"
        ];

        foreach (var path in paths)
        {
            try
            {
                Texture2D? texture = null;
                if (PreloadManager.Cache.ContainsKey(path))
                {
                    texture = PreloadManager.Cache.GetTexture2D(path);
                }

                texture ??= ResourceLoader.Load<Texture2D>(path);
                if (texture != null && GodotObject.IsInstanceValid(texture))
                {
                    return texture;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to load native challenge info icon {path}: {ex.Message}");
            }
        }

        return null;
    }

    private static void CenterButtonChild(Control control, Vector2 size)
    {
        control.AnchorLeft = 0.5f;
        control.AnchorTop = 0.5f;
        control.AnchorRight = 0.5f;
        control.AnchorBottom = 0.5f;
        control.OffsetLeft = -size.X * 0.5f;
        control.OffsetTop = -size.Y * 0.5f;
        control.OffsetRight = size.X * 0.5f;
        control.OffsetBottom = size.Y * 0.5f;
    }

    private static void FillButtonChild(Control control)
    {
        control.AnchorLeft = 0;
        control.AnchorTop = 0;
        control.AnchorRight = 1;
        control.AnchorBottom = 1;
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
    }

    private static void ShowInfoScreen()
    {
        try
        {
            var config = ResolveConfig();
            if (config == null)
            {
                return;
            }

            var overlayStack = NOverlayStack.Instance;
            if (overlayStack == null)
            {
                GD.PrintErr("[GongDou STS2] Cannot show challenge info: NOverlayStack is null.");
                return;
            }

            overlayStack.Push(new GongdouChallengeInfoScreen(config));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to show challenge info screen: {ex}");
        }
    }

    private static Models.Sts2PuzzleConfig? ResolveConfig()
    {
        if (!ChallengeSessionManager.IsChallengeStartingOrActive)
        {
            return null;
        }

        return ChallengeSessionManager.ActiveConfig ?? GongdouCalcifiedCultistMonster.CurrentConfig;
    }
}
