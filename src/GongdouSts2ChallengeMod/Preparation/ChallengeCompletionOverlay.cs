using Godot;
using GongdouSts2ChallengeMod.Challenges;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace GongdouSts2ChallengeMod.Preparation;

public static class ChallengeCompletionOverlay
{
    private const string LayerName = "GongDouChallengeCompletionLayer";
    private const string NativeNoticeDismissBridgeName = "GongDouNativeNoticeDismissBridge";
    private const string NativeNoticeDismissSuppressorName = "GongDouNativeNoticeDismissSuppressor";

    public static void ShowChallengeCompleted(
        bool success,
        bool stageCleared,
        long timeMs,
        int turns,
        string? failureReason,
        int stageIndex = 1,
        int stageCount = 10)
    {
        var nextHint = success && stageCleared && stageIndex < stageCount
            ? $"客户端会弹出结算浮层，可直接继续第 {stageIndex + 1} 关。"
            : "这是本组最后一关或当前没有下一关。";

        Show(
            success ? "挑战已完成" : "挑战已失败",
            success
                ? $"成绩已提交。用时 {FormatMs(timeMs)}，回合 {turns}。{nextHint}当前战斗画面会保留；也可以点击下方按钮返回主菜单。"
                : $"失败结果已提交。原因：{failureReason ?? "unknown"}",
            "返回主菜单",
            ChallengeSessionManager.ReturnHeldChallengeToMainMenu);
    }

    public static void ShowNotice(string title, string message)
    {
        if (TryShowNativeNotice(title, message))
        {
            return;
        }

        Show(title, message, "关闭", Close, closeOnBackdrop: true);
    }

    private static bool TryShowNativeNotice(string title, string message)
    {
        try
        {
            ClearTransientMenuLayersBestEffort();

            var modalContainer = NModalContainer.Instance;
            if (modalContainer == null)
            {
                return false;
            }

            var popup = NErrorPopup.Create(title, message, showReportBugButton: false);
            if (popup == null)
            {
                return false;
            }

            // The native popup only queues itself when OK is pressed. Clear any
            // stale modal state before and after it so the backstop cannot get stuck.
            modalContainer.Clear();

            Node? dismissBridge = null;
            popup.TreeExiting += () =>
            {
                dismissBridge?.QueueFree();
                ClearModalContainerForPopupBestEffort(popup);
            };

            modalContainer.Add(popup);
            dismissBridge = InstallNativeNoticeDismissBridge(popup);
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Native notice popup failed: {ex}");
            ClearNativeNoticeBestEffort();
            return false;
        }
    }

    private static Node? InstallNativeNoticeDismissBridge(NErrorPopup popup)
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            return null;
        }

        tree.Root.GetNodeOrNull<Node>(NativeNoticeDismissBridgeName)?.QueueFree();

        var bridge = new NativeNoticeDismissBridge(popup)
        {
            Name = NativeNoticeDismissBridgeName
        };
        tree.Root.AddChild(bridge);
        return bridge;
    }

    private static void ClearTransientMenuLayersBestEffort()
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                return;
            }

            tree.Root.GetNodeOrNull<CanvasLayer>("GongDouCoopModeMenuLayer")?.QueueFree();
            tree.Root.GetNodeOrNull<CanvasLayer>("GongDouLeaderboardMenuLayer")?.QueueFree();
        }
        catch
        {
            // Stale menu overlays are cosmetic; notice display must keep going.
        }
    }

    private static void ClearNativeNoticeBestEffort()
    {
        try
        {
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                tree.Root.GetNodeOrNull<Node>(NativeNoticeDismissBridgeName)?.QueueFree();
                tree.Root.GetNodeOrNull<Node>(NativeNoticeDismissSuppressorName)?.QueueFree();
            }

            NModalContainer.Instance?.Clear();
        }
        catch
        {
            // Notice cleanup must never break the challenge flow.
        }
    }

    private static void ClearModalContainerForPopupBestEffort(NErrorPopup popup)
    {
        try
        {
            var modalContainer = NModalContainer.Instance;
            if (modalContainer == null)
            {
                return;
            }

            modalContainer.Clear();
        }
        catch
        {
            // Native popup close cleanup must not rethrow from TreeExiting.
        }
    }

    private sealed class NativeNoticeDismissBridge : CanvasLayer
    {
        private readonly NErrorPopup _popup;
        private readonly DateTimeOffset _armedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(250);
        private NClickableControl? _nativeOkButton;
        private bool _buttonHooked;
        private bool _dismissed;

        public NativeNoticeDismissBridge(NErrorPopup popup)
        {
            _popup = popup;
            Layer = 4096;
            ProcessMode = ProcessModeEnum.Always;
            SetProcess(true);
            SetProcessInput(true);
            SetProcessUnhandledInput(true);
        }

        public override void _Ready()
        {
            // Main-menu native modals can be paused behind the menu stack and leave
            // their visible OK button inert. The catcher draws nothing; it only turns
            // any post-arm click/Escape into a deterministic modal dismiss.
            var catcher = new Control
            {
                Name = "GongDouNativeNoticeDismissCatcher",
                MouseFilter = Control.MouseFilterEnum.Stop,
                FocusMode = Control.FocusModeEnum.All
            };
            FillParent(catcher);
            catcher.GuiInput += input =>
            {
                if (DateTimeOffset.UtcNow < _armedAtUtc || !IsCloseInput(input))
                {
                    return;
                }

                Dismiss();
                catcher.GetViewport()?.SetInputAsHandled();
            };
            AddChild(catcher);

            // popup and make the visible "关闭" button feel dead on some menu screens.
        }

        public override void _Process(double delta)
        {
            if (!GodotObject.IsInstanceValid(_popup))
            {
                Dismiss();
                return;
            }

            TryHookNativeOkButton();
        }

        public override void _Input(InputEvent @event)
        {
            if (DateTimeOffset.UtcNow < _armedAtUtc)
            {
                return;
            }

            if (IsKeyboardDismissInput(@event) || IsPointerDismissInput(@event))
            {
                Dismiss();
                GetViewport()?.SetInputAsHandled();
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (DateTimeOffset.UtcNow < _armedAtUtc)
            {
                return;
            }

            if (IsKeyboardDismissInput(@event) || IsPointerDismissInput(@event))
            {
                Dismiss();
                GetViewport()?.SetInputAsHandled();
            }
        }

        private static bool IsKeyboardDismissInput(InputEvent @event)
        {
            return @event is InputEventKey { Pressed: true } key &&
                   key.Keycode is Key.Escape or Key.Enter or Key.KpEnter or Key.Space;
        }

        private void TryHookNativeOkButton()
        {
            if (_buttonHooked || !GodotObject.IsInstanceValid(_popup))
            {
                return;
            }

            try
            {
                var yesButton = _popup
                    .GetNodeOrNull<NVerticalPopup>("VerticalPopup")
                    ?.GetNodeOrNull<NClickableControl>("YesButton")
                    ?? _popup.FindChild("YesButton", recursive: true, owned: false) as NClickableControl;
                if (yesButton == null)
                {
                    return;
                }

                _nativeOkButton = yesButton;
                // Main-menu popups can appear while the tree is paused, which prevents
                // NClickableControl from becoming focused on hover. Its raw mouse signal
                // still arrives, so bind that as a safety close path for the native OK button.
                yesButton.Connect(
                    NClickableControl.SignalName.MouseReleased,
                    Callable.From<InputEvent>(input =>
                    {
                        if (DateTimeOffset.UtcNow >= _armedAtUtc && IsLeftMouseRelease(input))
                        {
                            Dismiss();
                            GetViewport()?.SetInputAsHandled();
                        }
                    }));
                yesButton.Connect(
                    NClickableControl.SignalName.Released,
                    Callable.From(() => Dismiss()));
                _buttonHooked = true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Native notice OK hook failed: {ex.Message}");
                _buttonHooked = true;
            }
        }

        private bool IsPointerDismissInput(InputEvent input)
        {
            if (!IsLeftMouseRelease(input))
            {
                return false;
            }

            // This is a notice-only popup. On some main-menu states the native
            // button can be visually present but not clickable, so any fresh
            // click after the popup appears should dismiss it.
            return true;
        }

        private bool IsPointerInsideNativeOkButton(InputEvent input)
        {
            if (input is not InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } mouse)
            {
                return false;
            }

            var button = _nativeOkButton;
            if (button == null || !GodotObject.IsInstanceValid(button) || !button.IsVisibleInTree())
            {
                return false;
            }

            var rect = button.GetGlobalRect();
            rect.Position -= new Vector2(24, 24);
            rect.Size += new Vector2(48, 48);
            return rect.HasPoint(mouse.GlobalPosition);
        }

        private void Dismiss()
        {
            if (_dismissed)
            {
                return;
            }

            _dismissed = true;
            InstallPostDismissInputSuppressor();
            try
            {
                // NErrorPopup only queues itself on OK; NModalContainer.OpenModal/backstop must be cleared explicitly.
                NModalContainer.Instance?.Clear();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Native notice dismiss failed: {ex.Message}");
            }

            try
            {
                if (GodotObject.IsInstanceValid(_popup))
                {
                    _popup.QueueFree();
                }
            }
            catch
            {
                // Popup may already be exiting through the native OK button.
            }

            QueueFree();
        }

        private static void InstallPostDismissInputSuppressor()
        {
            try
            {
                if (Engine.GetMainLoop() is not SceneTree tree)
                {
                    return;
                }

                tree.Root.GetNodeOrNull<Node>(NativeNoticeDismissSuppressorName)?.QueueFree();
                tree.Root.AddChild(new PostDismissInputSuppressor
                {
                    Name = NativeNoticeDismissSuppressorName
                });
            }
            catch
            {
                // This is only a click-through guard; dismissal itself already happened.
            }
        }
    }

    private sealed class PostDismissInputSuppressor : CanvasLayer
    {
        private readonly DateTimeOffset _expiresAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(350);

        public PostDismissInputSuppressor()
        {
            Layer = 4097;
            ProcessMode = ProcessModeEnum.Always;
            SetProcess(true);
            SetProcessInput(true);
            SetProcessUnhandledInput(true);
        }

        public override void _Ready()
        {
            var blocker = new Control
            {
                Name = "GongDouNativeNoticeDismissInputBlocker",
                MouseFilter = Control.MouseFilterEnum.Stop,
                FocusMode = Control.FocusModeEnum.All
            };
            FillParent(blocker);
            blocker.GuiInput += input =>
            {
                if (IsCloseInput(input))
                {
                    blocker.GetViewport()?.SetInputAsHandled();
                }
            };
            AddChild(blocker);
        }

        public override void _Process(double delta)
        {
            if (DateTimeOffset.UtcNow >= _expiresAtUtc)
            {
                QueueFree();
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (IsCloseInput(@event))
            {
                GetViewport()?.SetInputAsHandled();
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (IsCloseInput(@event))
            {
                GetViewport()?.SetInputAsHandled();
            }
        }
    }

    public static void Close()
    {
        ClearNativeNoticeBestEffort();

        var tree = Engine.GetMainLoop() as SceneTree;
        var existing = tree?.Root.GetNodeOrNull<CanvasLayer>(LayerName);
        if (existing == null)
        {
            return;
        }

        existing.Visible = false;
        existing.QueueFree();
    }

    private static void Show(string titleText, string bodyText, string buttonText, Action onButtonPressed, bool closeOnBackdrop = false)
    {
        var tree = Engine.GetMainLoop() as SceneTree
            ?? throw new InvalidOperationException("Godot SceneTree is not available.");

        closeOnBackdrop |= onButtonPressed.Method == ((Action)Close).Method && onButtonPressed.Target == null;

        Close();

        var layer = closeOnBackdrop
            ? new DismissibleNoticeLayer(Close)
            : new CanvasLayer();
        layer.Name = LayerName;
        layer.Layer = 130;

        var overlay = new Control
        {
            Name = "GongDouChallengeCompletionOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(overlay);
        if (closeOnBackdrop)
        {
            overlay.GuiInput += input =>
            {
                if (IsCloseInput(input))
                {
                    Close();
                    overlay.GetViewport()?.SetInputAsHandled();
                }
            };
        }

        var scrim = new ColorRect
        {
            Color = new Color(0.015f, 0.018f, 0.025f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(scrim);
        if (closeOnBackdrop)
        {
            scrim.GuiInput += input =>
            {
                if (IsLeftMouseRelease(input) || input.IsActionPressed("ui_cancel"))
                {
                    Close();
                    scrim.GetViewport()?.SetInputAsHandled();
                }
            };
        }
        overlay.AddChild(scrim);

        var center = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        FillParent(center);
        if (closeOnBackdrop)
        {
            center.GuiInput += input =>
            {
                if (IsLeftMouseRelease(input) || input.IsActionPressed("ui_cancel"))
                {
                    Close();
                    center.GetViewport()?.SetInputAsHandled();
                }
            };
        }
        overlay.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(560, 230),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        AttachNoticeDismissInput(panel, closeOnBackdrop);
        center.AddChild(panel);

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        AttachNoticeDismissInput(margin, closeOnBackdrop);
        margin.AddThemeConstantOverride("margin_left", 30);
        margin.AddThemeConstantOverride("margin_top", 26);
        margin.AddThemeConstantOverride("margin_right", 30);
        margin.AddThemeConstantOverride("margin_bottom", 26);
        panel.AddChild(margin);

        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        AttachNoticeDismissInput(box, closeOnBackdrop);
        box.AddThemeConstantOverride("separation", 18);
        margin.AddChild(box);

        var title = new Label
        {
            Text = titleText,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        box.AddChild(title);

        var body = new Label
        {
            Text = bodyText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        body.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(body);

        var button = new Button
        {
            Text = buttonText,
            CustomMinimumSize = new Vector2(170, 46),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All
        };
        var actionInvoked = false;
        void InvokeActionOnce()
        {
            if (actionInvoked)
            {
                return;
            }

            actionInvoked = true;
            try
            {
                onButtonPressed();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Completion overlay action failed: {ex}");
            }
        }

        button.Pressed += InvokeActionOnce;
        button.GuiInput += input =>
        {
            if (IsLeftMouseRelease(input) || input.IsActionPressed("ui_accept"))
            {
                InvokeActionOnce();
            }
        };
        box.AddChild(button);

        layer.AddChild(overlay);
        tree.Root.AddChild(layer);
    }

    private sealed class DismissibleNoticeLayer : CanvasLayer
    {
        private readonly Action _dismiss;
        private readonly DateTimeOffset _armedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(250);
        private bool _dismissed;

        public DismissibleNoticeLayer(Action dismiss)
        {
            _dismiss = dismiss;
            ProcessMode = ProcessModeEnum.Always;
            SetProcessInput(true);
            SetProcessUnhandledInput(true);
        }

        public override void _Input(InputEvent @event)
        {
            TryDismiss(@event);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            TryDismiss(@event);
        }

        private void TryDismiss(InputEvent @event)
        {
            if (_dismissed || DateTimeOffset.UtcNow < _armedAtUtc || !IsCloseInput(@event))
            {
                return;
            }

            _dismissed = true;
            try
            {
                _dismiss();
                GetViewport()?.SetInputAsHandled();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Notice fallback dismiss failed: {ex}");
            }
        }
    }

    private static void AttachNoticeDismissInput(Control control, bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        control.GuiInput += input =>
        {
            if (!IsCloseInput(input))
            {
                return;
            }

            Close();
            control.GetViewport()?.SetInputAsHandled();
        };
    }

    private static bool IsLeftMouseRelease(InputEvent input)
    {
        return input is InputEventMouseButton
        {
            Pressed: false,
            ButtonIndex: MouseButton.Left
        };
    }

    private static bool IsCloseInput(InputEvent input)
    {
        return IsLeftMouseRelease(input) ||
               input.IsActionPressed("ui_accept") ||
               input.IsActionPressed("ui_cancel");
    }

    private static string FormatMs(long timeMs)
    {
        var span = TimeSpan.FromMilliseconds(Math.Max(0, timeMs));
        return $"{(int)span.TotalMinutes:0}:{span.Seconds:00}.{span.Milliseconds:000}";
    }

    private static void FillParent(Control control)
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
}
