using Godot;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Ipc;
using GongdouSts2ChallengeMod.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace GongdouSts2ChallengeMod.Preparation;

public static class ChallengePreparationOverlay
{
    private const string LeaderboardMenuLayerName = "GongDouLeaderboardMenuLayer";
    private const string CoopModeMenuLayerName = "GongDouCoopModeMenuLayer";
    private const int NativeDuplicateVisualFlags = 12;

    public static async Task<GongdouCoopMenuSelection> ShowCoopModeMenuAsync()
    {
        var tree = Engine.GetMainLoop() as SceneTree
            ?? throw new InvalidOperationException("Godot SceneTree is not available.");

        var existing = tree.Root.GetNodeOrNull<CanvasLayer>(CoopModeMenuLayerName);
        existing?.QueueFree();

        var mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu?.SubmenuStack != null)
        {
            try
            {
                return await ShowNativeCoopModeSubmenuAsync(tree, mainMenu).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Native coop mode submenu failed, falling back to popup: {ex}");
            }
        }

        return await ShowCoopModePopupMenuAsync(tree).ConfigureAwait(false);
    }

    public static async Task<int?> ShowLeaderboardMenuAsync(IReadOnlyList<LeaderboardSummary> leaderboards)
    {
        var tree = Engine.GetMainLoop() as SceneTree
            ?? throw new InvalidOperationException("Godot SceneTree is not available.");

        var existing = tree.Root.GetNodeOrNull<CanvasLayer>(LeaderboardMenuLayerName);
        existing?.QueueFree();

        var mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu?.SubmenuStack != null)
        {
            try
            {
                return await ShowNativeLeaderboardSubmenuAsync(tree, mainMenu, leaderboards).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Native leaderboard submenu failed, falling back to popup: {ex}");
            }
        }

        return await ShowLeaderboardPopupMenuAsync(tree, leaderboards).ConfigureAwait(false);
    }

    private static Task<int?> ShowLeaderboardPopupMenuAsync(
        SceneTree tree,
        IReadOnlyList<LeaderboardSummary> leaderboards)
    {
        var tcs = new TaskCompletionSource<int?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = false;

        var layer = new CanvasLayer
        {
            Name = LeaderboardMenuLayerName,
            Layer = 500
        };

        var overlay = new Control
        {
            Name = "GongDouLeaderboardMenuOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(overlay);

        var scrim = new ColorRect
        {
            Color = new Color(0.015f, 0.018f, 0.025f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(scrim);
        overlay.AddChild(scrim);

        var center = new CenterContainer();
        FillParent(center);
        overlay.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(620, 420)
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var title = new Label
        {
            Text = "共斗挑战",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        root.AddChild(title);

        var subtitle = new Label
        {
            Text = "选择一个排行榜开始挑战。客户端必须保持启动。",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeFontSizeOverride("font_size", 17);
        root.AddChild(subtitle);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(list);

        var any = false;
        foreach (var item in leaderboards.Where(item => item.IsActive))
        {
            if (!int.TryParse(item.Id, out var leaderboardId) || leaderboardId <= 0)
            {
                continue;
            }

            any = true;
            var label = string.IsNullOrWhiteSpace(item.PresetName)
                ? item.Title
                : $"{item.Title}\n{item.PresetName}";
            var button = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(520, 58),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            button.AddThemeFontSizeOverride("font_size", 17);
            button.Pressed += () => Complete(leaderboardId);
            list.AddChild(button);
        }

        if (!any)
        {
            var empty = new Label
            {
                Text = "当前没有可用排行榜。",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            empty.AddThemeFontSizeOverride("font_size", 18);
            list.AddChild(empty);
        }

        var footer = new HBoxContainer();
        footer.Alignment = BoxContainer.AlignmentMode.End;
        footer.AddThemeConstantOverride("separation", 12);
        root.AddChild(footer);

        var cancel = new Button
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(120, 42)
        };
        cancel.Pressed += () => Complete(null);
        footer.AddChild(cancel);

        layer.AddChild(overlay);
        tree.Root.AddChild(layer);
        return tcs.Task;

        void Complete(int? leaderboardId)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            layer.QueueFree();
            tcs.TrySetResult(leaderboardId);
        }
    }

    private static Task<GongdouCoopMenuSelection> ShowCoopModePopupMenuAsync(SceneTree tree)
    {
        var tcs = new TaskCompletionSource<GongdouCoopMenuSelection>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = false;

        var layer = new CanvasLayer
        {
            Name = CoopModeMenuLayerName,
            Layer = 500
        };

        var overlay = new Control
        {
            Name = "GongDouCoopModeMenuOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(overlay);

        var scrim = new ColorRect
        {
            Color = new Color(0.015f, 0.018f, 0.025f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(scrim);
        overlay.AddChild(scrim);

        var center = new CenterContainer();
        FillParent(center);
        overlay.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(620, 360)
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var title = new Label
        {
            Text = "共斗",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        root.AddChild(title);

        AddPopupModeButton(root, "尖塔残局：十关连战", "选择排行榜并开始十关连战。", GongdouCoopMenuSelection.Puzzle);
        AddPopupModeButton(root, "芙莉莲人物挑战", "进入芙莉莲角色选择。", GongdouCoopMenuSelection.Frieren);

        var cancel = new Button
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(120, 42),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
        };
        cancel.Pressed += () => Complete(GongdouCoopMenuSelection.None);
        root.AddChild(cancel);

        layer.AddChild(overlay);
        tree.Root.AddChild(layer);
        return tcs.Task;

        void AddPopupModeButton(
            VBoxContainer parent,
            string titleText,
            string description,
            GongdouCoopMenuSelection selection)
        {
            var button = new Button
            {
                Text = $"{titleText}\n{description}",
                CustomMinimumSize = new Vector2(520, 64),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            button.AddThemeFontSizeOverride("font_size", 17);
            button.Pressed += () => Complete(selection);
            parent.AddChild(button);
        }

        void Complete(GongdouCoopMenuSelection selection)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            layer.QueueFree();
            tcs.TrySetResult(selection);
        }
    }

    private static async Task<GongdouCoopMenuSelection> ShowNativeCoopModeSubmenuAsync(
        SceneTree tree,
        NMainMenu mainMenu)
    {
        var submenu = NSingleplayerSubmenu.Create();
        if (submenu == null)
        {
            return await ShowCoopModePopupMenuAsync(tree).ConfigureAwait(false);
        }

        var tcs = new TaskCompletionSource<GongdouCoopMenuSelection>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = false;

        submenu.Name = "GongDouCoopModeSubmenu";
        submenu.Visible = false;
        mainMenu.SubmenuStack.AddChild(submenu);
        if (!submenu.IsNodeReady())
        {
            await submenu.ToSignal(submenu, Node.SignalName.Ready);
        }

        ConfigureNativeCoopModeSubmenu(submenu, Complete);
        submenu.Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(() =>
        {
            if (!submenu.Visible && !completed)
            {
                completed = true;
                submenu.QueueFree();
                tcs.TrySetResult(GongdouCoopMenuSelection.None);
            }
        }));

        mainMenu.SubmenuStack.Push(submenu);
        return await tcs.Task.ConfigureAwait(false);

        void Complete(GongdouCoopMenuSelection selection)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            try
            {
                if (mainMenu.SubmenuStack.Peek() == submenu)
                {
                    mainMenu.SubmenuStack.Pop();
                }
                else
                {
                    submenu.Visible = false;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to pop GongDou coop submenu: {ex.Message}");
            }

            submenu.QueueFree();
            tcs.TrySetResult(selection);
        }
    }

    private static void ConfigureNativeCoopModeSubmenu(
        NSingleplayerSubmenu submenu,
        Action<GongdouCoopMenuSelection> complete)
    {
        var entries = new[]
        {
            new NativeCoopModeEntry(
                "尖塔残局：十关连战",
                "选择排行榜并开始十关连战。",
                GongdouCoopMenuSelection.Puzzle),
            new NativeCoopModeEntry(
                "芙莉莲人物挑战",
                "进入芙莉莲角色选择。",
                GongdouCoopMenuSelection.Frieren)
        };

        var templates = new[]
            {
                submenu.GetNodeOrNull<NSubmenuButton>("StandardButton"),
                submenu.GetNodeOrNull<NSubmenuButton>("DailyButton"),
                submenu.GetNodeOrNull<NSubmenuButton>("CustomRunButton")
            }
            .Where(button => button != null)
            .Cast<NSubmenuButton>()
            .ToList();
        if (templates.Count == 0)
        {
            throw new InvalidOperationException("Native submenu button templates are not available.");
        }

        var parent = templates[0].GetParent();
        var created = new List<NSubmenuButton>(entries.Length);

        for (var i = 0; i < entries.Length; i++)
        {
            var slot = templates[Math.Min(i, templates.Count - 1)];
            if (slot.Duplicate(NativeDuplicateVisualFlags) is not NSubmenuButton button)
            {
                continue;
            }

            var entry = entries[i];
            button.Name = $"GongDouCoopModeButton{i + 1}";
            button.Visible = true;
            button.Modulate = Colors.White;
            button.MouseFilter = Control.MouseFilterEnum.Stop;
            button.FocusMode = Control.FocusModeEnum.All;

            parent.AddChild(button);
            parent.MoveChild(button, Math.Min(slot.GetIndex(), parent.GetChildCount() - 1));
            CopyNativeSubmenuSlotRect(button, slot, i >= templates.Count ? i - templates.Count + 1 : 0);
            ClearSubmenuButtonLocalization(button);
            SetSubmenuButtonText(button, entry.Title, entry.Description);
            button.Connect(
                NClickableControl.SignalName.Released,
                Callable.From(() => complete(entry.Selection)));
            button.Enable();
            created.Add(button);
        }

        foreach (var template in templates)
        {
            template.MouseFilter = Control.MouseFilterEnum.Ignore;
            template.FocusMode = Control.FocusModeEnum.None;
            template.Disable();
            template.Visible = false;
        }

        if (created.Count > 0)
        {
            SetSingleplayerSubmenuField(submenu, "_standardButton", created[0]);
        }
    }

    private static async Task<int?> ShowNativeLeaderboardSubmenuAsync(
        SceneTree tree,
        NMainMenu mainMenu,
        IReadOnlyList<LeaderboardSummary> leaderboards)
    {
        var submenu = NSingleplayerSubmenu.Create();
        if (submenu == null)
        {
            return await ShowLeaderboardPopupMenuAsync(tree, leaderboards).ConfigureAwait(false);
        }

        var tcs = new TaskCompletionSource<int?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = false;

        submenu.Name = "GongDouLeaderboardSubmenu";
        submenu.Visible = false;
        mainMenu.SubmenuStack.AddChild(submenu);
        if (!submenu.IsNodeReady())
        {
            await submenu.ToSignal(submenu, Node.SignalName.Ready);
        }

        ConfigureNativeLeaderboardSubmenu(submenu, leaderboards, Complete);
        submenu.Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(() =>
        {
            if (!submenu.Visible && !completed)
            {
                completed = true;
                submenu.QueueFree();
                tcs.TrySetResult(null);
            }
        }));

        mainMenu.SubmenuStack.Push(submenu);
        return await tcs.Task.ConfigureAwait(false);

        void Complete(int? leaderboardId)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            try
            {
                if (mainMenu.SubmenuStack.Peek() == submenu)
                {
                    mainMenu.SubmenuStack.Pop();
                }
                else
                {
                    submenu.Visible = false;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to pop GongDou submenu: {ex.Message}");
            }

            submenu.QueueFree();
            tcs.TrySetResult(leaderboardId);
        }
    }

    private static void ConfigureNativeLeaderboardSubmenu(
        NSingleplayerSubmenu submenu,
        IReadOnlyList<LeaderboardSummary> leaderboards,
        Action<int?> complete)
    {
        var templates = new[]
            {
                submenu.GetNodeOrNull<NSubmenuButton>("StandardButton"),
                submenu.GetNodeOrNull<NSubmenuButton>("DailyButton"),
                submenu.GetNodeOrNull<NSubmenuButton>("CustomRunButton")
            }
            .Where(button => button != null)
            .Cast<NSubmenuButton>()
            .ToList();
        if (templates.Count == 0)
        {
            throw new InvalidOperationException("Native submenu button templates are not available.");
        }

        var parent = templates[0].GetParent();
        var activeLeaderboards = leaderboards
            .Where(item => item.IsActive && int.TryParse(item.Id, out var id) && id > 0)
            .ToList();
        var count = Math.Max(1, activeLeaderboards.Count);
        var created = new List<NSubmenuButton>(count);

        for (var i = 0; i < count; i++)
        {
            var slot = templates[Math.Min(i, templates.Count - 1)];
            if (slot.Duplicate(NativeDuplicateVisualFlags) is not NSubmenuButton button)
            {
                continue;
            }

            button.Name = $"GongDouLeaderboardButton{i + 1}";
            button.Visible = true;
            button.Modulate = Colors.White;
            button.MouseFilter = Control.MouseFilterEnum.Stop;
            button.FocusMode = Control.FocusModeEnum.All;

            parent.AddChild(button);
            parent.MoveChild(button, Math.Min(slot.GetIndex(), parent.GetChildCount() - 1));
            CopyNativeSubmenuSlotRect(button, slot, i >= templates.Count ? i - templates.Count + 1 : 0);
            ClearSubmenuButtonLocalization(button);
            LogNativeSubmenuLayout("created", button);

            if (i < activeLeaderboards.Count)
            {
                var item = activeLeaderboards[i];
                var title = string.IsNullOrWhiteSpace(item.Title) ? "共斗挑战" : item.Title;
                var description = string.IsNullOrWhiteSpace(item.PresetName)
                    ? "选择后开始挑战。客户端必须保持启动。"
                    : $"{item.PresetName}\n客户端必须保持启动。";
                SetSubmenuButtonText(button, title, description);
                if (int.TryParse(item.Id, out var leaderboardId) && leaderboardId > 0)
                {
                    button.Connect(
                        NClickableControl.SignalName.Released,
                        Callable.From(() => complete(leaderboardId)));
                }

                button.Enable();
            }
            else
            {
                SetSubmenuButtonText(button, "暂无可用排行榜", "请先在共斗客户端登录并刷新挑战。");
                button.Disable();
            }

            created.Add(button);
        }

        foreach (var template in templates)
        {
            template.MouseFilter = Control.MouseFilterEnum.Ignore;
            template.FocusMode = Control.FocusModeEnum.None;
            template.Disable();
            template.Visible = false;
            LogNativeSubmenuLayout("template-hidden", template);
        }

        if (created.Count > 0)
        {
            SetSingleplayerSubmenuField(submenu, "_standardButton", created[0]);
        }
    }

    private static void CopyNativeSubmenuSlotRect(Control target, Control source, int extraRows)
    {
        var rowOffset = extraRows * 150f;
        target.LayoutMode = source.LayoutMode;
        target.AnchorLeft = source.AnchorLeft;
        target.AnchorTop = source.AnchorTop;
        target.AnchorRight = source.AnchorRight;
        target.AnchorBottom = source.AnchorBottom;
        target.OffsetLeft = source.OffsetLeft;
        target.OffsetTop = source.OffsetTop + rowOffset;
        target.OffsetRight = source.OffsetRight;
        target.OffsetBottom = source.OffsetBottom + rowOffset;
        target.GrowHorizontal = source.GrowHorizontal;
        target.GrowVertical = source.GrowVertical;
        target.PivotOffset = source.PivotOffset;
        target.Scale = source.Scale;
        target.Rotation = source.Rotation;
        target.CustomMinimumSize = source.CustomMinimumSize;
        target.SizeFlagsHorizontal = source.SizeFlagsHorizontal;
        target.SizeFlagsVertical = source.SizeFlagsVertical;
        target.ZIndex = source.ZIndex + 1;
    }

    private static void LogNativeSubmenuLayout(string label, Control control)
    {
        GD.Print(
            $"[GongDou STS2] Leaderboard submenu {label} {control.Name}: " +
            $"layout={control.LayoutMode}, anchors=({control.AnchorLeft},{control.AnchorTop},{control.AnchorRight},{control.AnchorBottom}), " +
            $"offsets=({control.OffsetLeft},{control.OffsetTop},{control.OffsetRight},{control.OffsetBottom}), " +
            $"pos={control.Position}, size={control.Size}, global={control.GlobalPosition}.");
    }

    private static void SetSubmenuButtonText(NSubmenuButton button, string title, string description)
    {
        SetNodeText(button.GetNodeOrNull<Node>("%Title"), title, preferAutoSize: true);
        SetNodeText(button.GetNodeOrNull<Node>("%Description"), description, preferAutoSize: false);
    }

    private static void SetNodeText(Node? node, string text, bool preferAutoSize)
    {
        if (node == null)
        {
            return;
        }

        try
        {
            if (preferAutoSize && node.HasMethod("SetTextAutoSize"))
            {
                node.Call("SetTextAutoSize", text);
                return;
            }

            if (node is Label label)
            {
                label.Text = text;
            }
            else if (node is RichTextLabel richTextLabel)
            {
                richTextLabel.Text = text;
            }
            else
            {
                node.Set("text", text);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to set native submenu text: {ex.Message}");
        }
    }

    private static void ClearSubmenuButtonLocalization(NSubmenuButton button)
    {
        try
        {
            var field = typeof(NSubmenuButton).GetField(
                "_locKeyPrefix",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(button, null);
        }
        catch
        {
            // Cosmetic only; explicit text assignment above is the source of truth.
        }
    }

    private static void SetSingleplayerSubmenuField(
        NSingleplayerSubmenu submenu,
        string fieldName,
        object value)
    {
        try
        {
            var field = typeof(NSingleplayerSubmenu).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(submenu, value);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to set native submenu field {fieldName}: {ex.Message}");
        }
    }

    public static async Task<Func<Task>> ShowTransitionOverlay(string titleText, string subtitleText)
    {
        var transition = NGame.Instance?.Transition;
        if (transition == null)
        {
            return ShowFallbackTransitionOverlay(titleText, subtitleText);
        }

        var closed = false;
        try
        {
            await transition.RoomFadeOut().ConfigureAwait(false);
            return async () =>
            {
                if (closed)
                {
                    return;
                }

                closed = true;
                await transition.RoomFadeIn(showTransition: true).ConfigureAwait(false);
            };
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Native transition failed, falling back to challenge overlay: {ex.Message}");
            return ShowFallbackTransitionOverlay(titleText, subtitleText);
        }
    }

    private static Func<Task> ShowFallbackTransitionOverlay(string titleText, string subtitleText)
    {
        var tree = Engine.GetMainLoop() as SceneTree
            ?? throw new InvalidOperationException("Godot SceneTree is not available.");

        var layer = new CanvasLayer
        {
            Name = "GongDouChallengeTransitionLayer",
            Layer = 510
        };

        var overlay = new Control
        {
            Name = "GongDouChallengeTransitionOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(overlay);

        var scrim = new ColorRect
        {
            Color = new Color(0.015f, 0.018f, 0.025f, 0.98f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(scrim);
        overlay.AddChild(scrim);

        var center = new CenterContainer();
        FillParent(center);
        overlay.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(520, 180)
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.Alignment = BoxContainer.AlignmentMode.Center;
        box.AddThemeConstantOverride("separation", 16);
        margin.AddChild(box);

        var title = new Label
        {
            Text = titleText,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        box.AddChild(title);

        var subtitle = new Label
        {
            Text = subtitleText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(subtitle);

        layer.AddChild(overlay);
        tree.Root.AddChild(layer);
        var closed = false;
        return () =>
        {
            if (closed)
            {
                return Task.CompletedTask;
            }

            closed = true;
            layer.QueueFree();
            return Task.CompletedTask;
        };
    }

    public static Task<ChallengeSelection?> ShowAsync(Sts2PuzzleConfig config, ResourcePool resources)
    {
        var tree = Engine.GetMainLoop() as SceneTree
            ?? throw new InvalidOperationException("Godot SceneTree is not available.");

        var tcs = new TaskCompletionSource<ChallengeSelection?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var selectedCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedPotions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedRelics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cardButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        var potionButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        var relicButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        var cardItemsByKey = new Dictionary<string, ResourceItem>(StringComparer.OrdinalIgnoreCase);
        Label cardCountLabel = null!;
        Label countLabel = null!;
        Label errorLabel = null!;
        Button? cancelButton = null;
        Button? startButton = null;
        var completed = false;

        var layer = new CanvasLayer
        {
            Name = "GongDouChallengePreparationLayer",
            Layer = 500
        };

        var overlay = new Control
        {
            Name = "GongDouChallengePreparationOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(overlay);

        var scrim = new ColorRect
        {
            Color = new Color(0.015f, 0.018f, 0.025f, 0.94f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        FillParent(scrim);
        overlay.AddChild(scrim);

        var margin = new MarginContainer();
        FillParent(margin);
        margin.AddThemeConstantOverride("margin_left", 42);
        margin.AddThemeConstantOverride("margin_top", 34);
        margin.AddThemeConstantOverride("margin_right", 42);
        margin.AddThemeConstantOverride("margin_bottom", 34);
        overlay.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var title = new Label
        {
            Text = $"{config.PuzzleSetName} {config.StageIndex:00}/{config.StageCount:00}：{config.PuzzleName}",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        root.AddChild(title);

        var subtitle = new Label
        {
            Text = $"通关条件：{config.WinConditionText}角色固定：铁甲战士。请在游戏内选择本局资源。",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 18);
        root.AddChild(subtitle);

        var info = new Label
        {
            Text = config.EnemyInfoText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        info.AddThemeFontSizeOverride("font_size", 16);
        root.AddChild(info);

        var body = new HBoxContainer();
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 18);
        root.AddChild(body);

        var cardSection = CreateSection($"卡牌池（{BuildCardCountText()}）");
        body.AddChild(cardSection.Container);
        cardSection.Container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        cardCountLabel = new Label
        {
            Text = BuildCardCountText(),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        cardCountLabel.AddThemeFontSizeOverride("font_size", 18);
        cardCountLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.86f, 1.0f));
        cardSection.Content.AddChild(cardCountLabel);

        var cardGrid = new GridContainer { Columns = 2 };
        cardGrid.AddThemeConstantOverride("h_separation", 10);
        cardGrid.AddThemeConstantOverride("v_separation", 10);
        cardSection.Content.AddChild(cardGrid);

        var cardIndex = 0;
        foreach (var item in resources.ExpandedCardPool())
        {
            cardIndex++;
            var cardKey = $"{item.Id}#{cardIndex}";
            cardItemsByKey[cardKey] = item;
            var button = CreateResourceButton(item, ResolveCardResourceText(item));
            button.Pressed += () =>
            {
                ToggleSelection(selectedCards, cardKey, config.MaxCards, button, cardButtons, "卡牌已达到选择上限。");
                UpdateFooter();
            };
            cardButtons[cardKey] = button;
            cardGrid.AddChild(button);
        }

        var side = new VBoxContainer();
        side.CustomMinimumSize = new Vector2(330, 0);
        side.AddThemeConstantOverride("separation", 14);
        body.AddChild(side);

        var potionSection = CreateSection($"药水池（最多选 {config.MaxPotions} 瓶）");
        side.AddChild(potionSection.Container);
        foreach (var item in resources.PotionPool)
        {
            var button = CreateResourceButton(item);
            button.Pressed += () =>
            {
                ToggleSelection(selectedPotions, item.Id, config.MaxPotions, button, potionButtons, "药水已达到选择上限。");
                UpdateFooter();
            };
            potionButtons[item.Id] = button;
            potionSection.Content.AddChild(button);
        }

        var relicSection = CreateSection($"遗物池（最多选 {config.MaxRelics} 个）");
        side.AddChild(relicSection.Container);
        if (resources.RelicPool.Count == 0)
        {
            var noRelics = new Label { Text = "无。", HorizontalAlignment = HorizontalAlignment.Center };
            noRelics.AddThemeFontSizeOverride("font_size", 18);
            relicSection.Content.AddChild(noRelics);
        }
        else
        {
            foreach (var item in resources.RelicPool)
            {
                var button = CreateResourceButton(item);
                button.Pressed += () =>
                {
                    ToggleSelection(selectedRelics, item.Id, config.MaxRelics, button, relicButtons, "遗物已达到选择上限。");
                    UpdateFooter();
                };
                relicButtons[item.Id] = button;
                relicSection.Content.AddChild(button);
            }
        }

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        root.AddChild(footer);

        countLabel = new Label();
        countLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        countLabel.AddThemeFontSizeOverride("font_size", 16);
        footer.AddChild(countLabel);

        errorLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        errorLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.35f));
        errorLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(errorLabel);

        cancelButton = new Button { Text = "取消挑战", CustomMinimumSize = new Vector2(130, 42) };
        cancelButton.Pressed += () =>
        {
            Complete(null);
        };
        footer.AddChild(cancelButton);

        startButton = new Button { Text = "开始挑战", CustomMinimumSize = new Vector2(150, 42) };
        startButton.Pressed += () =>
        {
            var selection = new ChallengeSelection();
            selection.CardIds.AddRange(selectedCards.Select(key => cardItemsByKey[key].Id));
            selection.PotionIds.AddRange(selectedPotions);
            selection.RelicIds.AddRange(selectedRelics);
            Complete(selection);
        };
        footer.AddChild(startButton);

        layer.AddChild(overlay);
        tree.Root.AddChild(layer);
        UpdateFooter();
        return tcs.Task;

        void Complete(ChallengeSelection? selection)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            if (cancelButton != null)
            {
                cancelButton.Disabled = true;
            }

            if (startButton != null)
            {
                startButton.Disabled = true;
            }

            layer.QueueFree();
            tcs.TrySetResult(selection);
        }

        void ToggleSelection(
            HashSet<string> target,
            string id,
            int max,
            Button button,
            Dictionary<string, Button> buttons,
            string limitMessage)
        {
            if (target.Contains(id))
            {
                target.Remove(id);
                errorLabel.Text = "";
            }
            else if (target.Count >= max)
            {
                errorLabel.Text = limitMessage;
            }
            else
            {
                target.Add(id);
                errorLabel.Text = "";
            }

            foreach (var (resourceId, resourceButton) in buttons)
            {
                resourceButton.ButtonPressed = target.Contains(resourceId);
            }
        }

        void UpdateFooter()
        {
            var cardCountText = BuildCardCountText();
            cardSection.TitleLabel.Text = $"卡牌池（{cardCountText}）";
            cardCountLabel.Text = cardCountText;
            countLabel.Text = $"已选择：卡牌 {selectedCards.Count}/{config.MaxCards}（需要 {FormatRequiredCardCount()}），药水 {selectedPotions.Count}/{config.MaxPotions}，遗物 {selectedRelics.Count}/{config.MaxRelics}";
            if (startButton != null)
            {
                startButton.Disabled = selectedCards.Count < config.MinCards || selectedCards.Count > config.MaxCards;
            }
        }

        string BuildCardCountText()
        {
            return $"当前已选 {selectedCards.Count}/{config.MaxCards} 张，{FormatRequiredCardCount()}";
        }

        string FormatRequiredCardCount()
        {
            return config.MinCards == config.MaxCards
                ? $"必须选择 {config.MinCards} 张"
                : $"需要选择 {config.MinCards}-{config.MaxCards} 张";
        }
    }

    private static (PanelContainer Container, VBoxContainer Content, Label TitleLabel) CreateSection(string title)
    {
        var panel = new PanelContainer();
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", 20);
        root.AddChild(label);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(scroll);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);

        return (panel, content, label);
    }

    private static Button CreateResourceButton(ResourceItem item, string? text = null)
    {
        var button = new Button
        {
            ToggleMode = true,
            Text = text ?? $"{item.Name}\n{item.Description}",
            CustomMinimumSize = new Vector2(280, 74),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        button.AddThemeFontSizeOverride("font_size", 14);
        return button;
    }

    private static string ResolveCardResourceText(ResourceItem item)
    {
        try
        {
            var card = ChallengeCardFactory.CreateById(item.Id);
            string description;
            try
            {
                description = card.GetDescriptionForPile(PileType.None);
            }
            catch
            {
                var descriptionLoc = card.Description;
                card.DynamicVars.AddTo(descriptionLoc);
                description = descriptionLoc.GetFormattedText();
            }

            return $"{card.Title}\n{description}";
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to resolve card text for {item.Id}: {ex.Message}");
            return $"{item.Name}\n{item.Description}";
        }
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

    private sealed record NativeCoopModeEntry(
        string Title,
        string Description,
        GongdouCoopMenuSelection Selection);
}

public enum GongdouCoopMenuSelection
{
    None = 0,
    Puzzle = 1,
    Frieren = 2
}
