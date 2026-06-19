using Godot;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Models;
using GongdouSts2ChallengeMod.Relics;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace GongdouSts2ChallengeMod.Preparation;

public sealed class NativeItemSelectionOverlay : Control, IOverlayScreen
{
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<ResourceItem> _items;
    private readonly int _maxSelect;
    private readonly ItemKind _kind;
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Control> _tiles = new(StringComparer.OrdinalIgnoreCase);

    private Label _countLabel = null!;
    private bool _completed;

    private NativeItemSelectionOverlay(
        Sts2PuzzleConfig config,
        IReadOnlyList<ResourceItem> items,
        int maxSelect,
        ItemKind kind)
    {
        _items = items.ToList();
        _maxSelect = maxSelect;
        _kind = kind;
        Name = $"GongDou{kind}SelectionOverlay";
        MouseFilter = MouseFilterEnum.Stop;
        BuildUi(config);
    }

    public NetScreenType ScreenType => NetScreenType.None;
    public bool UseSharedBackstop => true;
    public Control? DefaultFocusedControl { get; private set; }
    public Control? FocusedControlFromTopBar => DefaultFocusedControl;

    public static Task<IReadOnlyList<string>?> ShowPotionsAsync(
        Sts2PuzzleConfig config,
        IReadOnlyList<ResourceItem> items,
        int maxSelect)
    {
        return maxSelect == 1 && items.Count > 0
            ? ShowNativeSinglePotionAsync(config, items)
            : ShowOverlayAsync(config, items, maxSelect, ItemKind.Potion);
    }

    public static Task<IReadOnlyList<string>?> ShowRelicsAsync(
        Sts2PuzzleConfig config,
        IReadOnlyList<ResourceItem> items,
        int maxSelect)
    {
        return maxSelect == 1 && items.Count > 0
            ? ShowNativeSingleRelicAsync(config, items)
            : ShowOverlayAsync(config, items, maxSelect, ItemKind.Relic);
    }

    private static async Task<IReadOnlyList<string>?> ShowNativeSinglePotionAsync(
        Sts2PuzzleConfig config,
        IReadOnlyList<ResourceItem> items)
    {
        try
        {
            var screen = new NativeSinglePotionSelectionScreen(items);
            var overlayStack = NOverlayStack.Instance
                ?? throw new InvalidOperationException("STS2 overlay stack is not available.");
            overlayStack.Push(screen);
            return await screen.PotionsSelected().ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Native potion selection failed, falling back to lightweight selector: {ex.Message}");
            return await ShowOverlayAsync(config, items, 1, ItemKind.Potion).ConfigureAwait(false);
        }
    }

    private static Task<IReadOnlyList<string>?> ShowOverlayAsync(
        Sts2PuzzleConfig config,
        IReadOnlyList<ResourceItem> items,
        int maxSelect,
        ItemKind kind)
    {
        var overlay = new NativeItemSelectionOverlay(config, items, maxSelect, kind);
        var overlayStack = NOverlayStack.Instance
            ?? throw new InvalidOperationException("STS2 overlay stack is not available.");
        overlayStack.Push(overlay);
        return overlay._completion.Task;
    }

    private static async Task<IReadOnlyList<string>?> ShowNativeSingleRelicAsync(
        Sts2PuzzleConfig config,
        IReadOnlyList<ResourceItem> items)
    {
        try
        {
            var relics = items
                .Select(item => new
                {
                    item.Id,
                    Relic = ChallengeRelicFactory.CreateById(item.Id)
                })
                .ToList();
            var screen = NChooseARelicSelection.ShowScreen(relics.Select(item => item.Relic).ToList());
            if (screen == null)
            {
                return await ShowOverlayAsync(config, items, 1, ItemKind.Relic).ConfigureAwait(false);
            }

            if (screen.IsNodeReady())
            {
                HideNativeRelicSkip(screen);
            }
            else
            {
                screen.Connect(Node.SignalName.Ready, Callable.From(() => HideNativeRelicSkip(screen)));
            }

            var selectedRelic = (await screen.RelicsSelected().ConfigureAwait(false)).FirstOrDefault();
            if (selectedRelic == null)
            {
                return null;
            }

            var selected = relics.FirstOrDefault(item =>
                ReferenceEquals(item.Relic, selectedRelic) ||
                Equals(item.Relic.Id, selectedRelic.Id));
            return string.IsNullOrWhiteSpace(selected?.Id) ? null : [selected.Id];
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Native relic selection failed, falling back to lightweight selector: {ex.Message}");
            return await ShowOverlayAsync(config, items, 1, ItemKind.Relic).ConfigureAwait(false);
        }
    }

    private static void HideNativeRelicSkip(NChooseARelicSelection screen)
    {
        var skip = screen.GetNodeOrNull<Control>("SkipButton");
        if (skip == null)
        {
            return;
        }

        skip.Hide();
        skip.MouseFilter = MouseFilterEnum.Ignore;
    }

    public void AfterOverlayOpened()
    {
    }

    public void AfterOverlayClosed()
    {
        if (!_completed)
        {
            _completion.TrySetResult(null);
        }

        QueueFree();
    }

    public void AfterOverlayShown()
    {
    }

    public void AfterOverlayHidden()
    {
    }

    private void BuildUi(Sts2PuzzleConfig config)
    {
        FillParent(this);

        var backdrop = new ColorRect
        {
            Color = new Color(0.015f, 0.018f, 0.025f, 0.92f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        FillParent(backdrop);
        AddChild(backdrop);

        var margin = new MarginContainer();
        FillParent(margin);
        margin.AddThemeConstantOverride("margin_left", 96);
        margin.AddThemeConstantOverride("margin_top", 150);
        margin.AddThemeConstantOverride("margin_right", 96);
        margin.AddThemeConstantOverride("margin_bottom", 96);
        AddChild(margin);

        var root = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 46);
        margin.AddChild(root);

        _countLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _countLabel.AddThemeFontSizeOverride("font_size", 32);
        _countLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.96f, 1f));
        root.AddChild(_countLabel);

        var grid = new GridContainer
        {
            Columns = 6,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        grid.AddThemeConstantOverride("h_separation", 64);
        grid.AddThemeConstantOverride("v_separation", 48);
        root.AddChild(grid);

        foreach (var item in _items)
        {
            var tile = CreateTile(item);
            _tiles[item.Id] = tile;
            grid.AddChild(tile);
            DefaultFocusedControl ??= tile;
        }

        if (_items.Count == 0)
        {
            var empty = new Label
            {
                Text = "没有可选项",
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            empty.AddThemeFontSizeOverride("font_size", 22);
            grid.AddChild(empty);
        }

        UpdateCount();
    }

    private Control CreateTile(ResourceItem item)
    {
        var nativeNode = CreateNativeNode(item);
        nativeNode.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        nativeNode.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        if (nativeNode is NClickableControl clickable)
        {
            clickable.MouseEntered += () => clickable.TryGrabFocus();
            clickable.Connect(NClickableControl.SignalName.Released, Callable.From(() => Toggle(item.Id)));
        }
        else
        {
            nativeNode.GuiInput += input =>
            {
                if (input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
                {
                    Toggle(item.Id);
                }
            };
        }

        return nativeNode;
    }

    private Control CreateNativeNode(ResourceItem item)
    {
        if (_kind == ItemKind.Potion)
        {
            var potion = ChallengePotionFactory.CreateById(item.Id);
            var holder = NPotionHolder.Create(isUsable: false);
            holder.CustomMinimumSize = new Vector2(120, 120);
            holder.Connect(Node.SignalName.Ready, Callable.From(() =>
            {
                if (holder.Potion != null)
                {
                    return;
                }

                var potionNode = NPotion.Create(potion)
                    ?? throw new InvalidOperationException($"Failed to create native potion node for {item.Id}.");
                potionNode.Position = new Vector2(-30f, -30f);
                holder.AddPotion(potionNode);
            }));
            return holder;
        }

        var relic = ChallengeRelicFactory.CreateById(item.Id);
        var relicHolder = NRelicBasicHolder.Create(relic)
            ?? throw new InvalidOperationException($"Failed to create native relic holder for {item.Id}.");
        relicHolder.Scale = Vector2.One * 2f;
        relicHolder.CustomMinimumSize = new Vector2(120, 120);
        return relicHolder;
    }

    private void Toggle(string id)
    {
        if (_maxSelect <= 0)
        {
            Complete(Array.Empty<string>());
            return;
        }

        if (_maxSelect == 1)
        {
            _selected.Clear();
            _selected.Add(id);
            UpdateItemVisuals();
            UpdateCount();
            Complete(_selected.ToList());
            return;
        }

        if (_selected.Contains(id))
        {
            _selected.Remove(id);
        }
        else if (_selected.Count < _maxSelect)
        {
            _selected.Add(id);
        }

        UpdateItemVisuals();
        UpdateCount();
        if (_selected.Count == _maxSelect)
        {
            Complete(_selected.ToList());
        }
    }

    private void UpdateItemVisuals()
    {
        foreach (var (itemId, tile) in _tiles)
        {
            tile.Modulate = _selected.Contains(itemId)
                ? Colors.White
                : new Color(0.82f, 0.82f, 0.86f, 0.96f);
        }
    }

    private void Complete(IReadOnlyList<string>? selected)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _completion.TrySetResult(selected);
        NOverlayStack.Instance?.Remove(this);
    }

    private void UpdateCount()
    {
        _countLabel.Text = $"选择{KindName}：{_selected.Count}/{_maxSelect}";
    }

    private string KindName => _kind == ItemKind.Potion ? "药水" : "遗物";

    private sealed class NativeSinglePotionSelectionScreen : Control, IOverlayScreen
    {
        private const float PotionXSpacing = 200f;
        private readonly TaskCompletionSource<IReadOnlyList<string>?> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly IReadOnlyList<(string Id, PotionModel Potion)> _potions;
        private readonly List<NPotionHolder> _holders = new();
        private bool _completed;
        private Tween? _cardTween;
        private Tween? _fadeTween;

        public NativeSinglePotionSelectionScreen(IReadOnlyList<ResourceItem> items)
        {
            _potions = items
                .Select(item => (Id: item.Id, Potion: ChallengePotionFactory.CreateById(item.Id)))
                .ToList();
            Name = "GongDouNativePotionSelectionScreen";
            MouseFilter = MouseFilterEnum.Stop;
            FillParent(this);
            BuildUi();
        }

        public NetScreenType ScreenType => NetScreenType.Rewards;
        public bool UseSharedBackstop => true;
        public Control? DefaultFocusedControl { get; private set; }
        public Control? FocusedControlFromTopBar => DefaultFocusedControl;

        public override void _Ready()
        {
            ConnectFocusNeighbors();
        }

        public override void _ExitTree()
        {
            if (!_completed)
            {
                _completion.TrySetResult(null);
            }
        }

        public async Task<IReadOnlyList<string>?> PotionsSelected()
        {
            var result = await _completion.Task.ConfigureAwait(false);
            NOverlayStack.Instance?.Remove(this);
            return result;
        }

        public void AfterOverlayOpened()
        {
            Modulate = Colors.Transparent;
            ArrangePotionHolders(animate: true);
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.2);
        }

        public void AfterOverlayClosed()
        {
            _cardTween?.Kill();
            _fadeTween?.Kill();
            if (!_completed)
            {
                _completion.TrySetResult(null);
            }

            QueueFree();
        }

        public void AfterOverlayShown()
        {
            Visible = true;
        }

        public void AfterOverlayHidden()
        {
            Visible = false;
        }

        private void BuildUi()
        {
            AddChild(CreateRelicStyleBanner());

            var row = new Control
            {
                Name = "PotionRow",
                MouseFilter = MouseFilterEnum.Ignore
            };
            FillParent(row);
            AddChild(row);

            for (var i = 0; i < _potions.Count; i++)
            {
                var item = _potions[i];
                var holder = CreatePotionHolder(item.Id, item.Potion);
                row.AddChild(holder);
                _holders.Add(holder);
                DefaultFocusedControl ??= holder;
            }
        }

        private Control CreateRelicStyleBanner()
        {
            var banner = TryInstantiateNativeScene<NCommonBanner>(
                "common_ui/common_banner",
                "screens/common_banner",
                "ui/common_banner");
            if (banner != null)
            {
                banner.Connect(Node.SignalName.Ready, Callable.From(() =>
                {
                    banner.label.SetTextAutoSize("选择一瓶药水");
                    banner.AnimateIn();
                }));
                return banner;
            }

            return CreateFallbackBanner();
        }

        private static T? TryInstantiateNativeScene<T>(params string[] scenePaths)
            where T : Control
        {
            foreach (var scenePath in scenePaths)
            {
                try
                {
                    var control = SceneHelper.Instantiate<T>(scenePath);
                    if (control != null)
                    {
                        return control;
                    }
                }
                catch
                {
                    // STS2 scene paths move between builds; try the next known native candidate.
                }
            }

            GD.PrintErr($"[GongDou STS2] Failed to instantiate native {typeof(T).Name} for item selection.");
            return null;
        }

        private static Control CreateFallbackBanner()
        {
            var panel = new PanelContainer
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            panel.AnchorLeft = 0.5f;
            panel.AnchorRight = 0.5f;
            panel.AnchorTop = 0f;
            panel.AnchorBottom = 0f;
            panel.OffsetLeft = -300f;
            panel.OffsetRight = 300f;
            panel.OffsetTop = 205f;
            panel.OffsetBottom = 290f;

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.56f, 0.40f, 0.22f, 0.94f),
                BorderColor = new Color(0.28f, 0.20f, 0.12f, 0.95f),
                BorderWidthLeft = 3,
                BorderWidthTop = 3,
                BorderWidthRight = 3,
                BorderWidthBottom = 3,
                CornerRadiusTopLeft = 12,
                CornerRadiusTopRight = 12,
                CornerRadiusBottomRight = 12,
                CornerRadiusBottomLeft = 12
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var label = new Label
            {
                Text = "选择一瓶药水",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 32);
            label.AddThemeColorOverride("font_color", new Color(0.94f, 0.90f, 0.82f));
            panel.AddChild(label);
            return panel;
        }

        private NPotionHolder CreatePotionHolder(string id, PotionModel potion)
        {
            var holder = NPotionHolder.Create(isUsable: false);
            holder.Name = $"Potion_{id}";
            holder.Scale = Vector2.One * 2f;
            holder.CustomMinimumSize = new Vector2(120, 120);
            holder.Modulate = Colors.Transparent;
            holder.Connect(Node.SignalName.Ready, Callable.From(() =>
            {
                if (holder.Potion != null)
                {
                    return;
                }

                var potionNode = NPotion.Create(potion)
                    ?? throw new InvalidOperationException($"Failed to create native potion node for {id}.");
                potionNode.Position = new Vector2(-30f, -30f);
                holder.AddPotion(potionNode);
            }));
            holder.MouseEntered += () => holder.TryGrabFocus();
            holder.Connect(NClickableControl.SignalName.Released, Callable.From(() => SelectPotion(id)));
            return holder;
        }

        private void ArrangePotionHolders(bool animate)
        {
            if (_holders.Count == 0)
            {
                return;
            }

            var viewportSize = GetViewportRect().Size;
            var center = new Vector2(viewportSize.X * 0.5f, viewportSize.Y * 0.56f);
            var startX = -(_holders.Count - 1) * PotionXSpacing * 0.5f;
            _cardTween?.Kill();
            _cardTween = animate ? CreateTween().SetParallel() : null;
            for (var i = 0; i < _holders.Count; i++)
            {
                var holder = _holders[i];
                var target = center + Vector2.Right * (startX + PotionXSpacing * i) - new Vector2(60f, 60f);
                if (animate && _cardTween != null)
                {
                    holder.Position = target + Vector2.Down * 48f;
                    _cardTween.TweenProperty(holder, "position", target, 0.5)
                        .SetEase(Tween.EaseType.Out)
                        .SetTrans(Tween.TransitionType.Expo);
                    _cardTween.TweenProperty(holder, "modulate", Colors.White, 0.35)
                        .SetEase(Tween.EaseType.Out)
                        .SetTrans(Tween.TransitionType.Cubic);
                }
                else
                {
                    holder.Position = target;
                    holder.Modulate = Colors.White;
                }
            }
        }

        private void ConnectFocusNeighbors()
        {
            if (_holders.Count == 0 || !IsInsideTree())
            {
                return;
            }

            if (_holders.Any(holder => !holder.IsInsideTree()))
            {
                return;
            }

            for (var i = 0; i < _holders.Count; i++)
            {
                var holder = _holders[i];
                var left = _holders[(i + _holders.Count - 1) % _holders.Count];
                var right = _holders[(i + 1) % _holders.Count];
                holder.FocusNeighborLeft = left.GetPath();
                holder.FocusNeighborRight = right.GetPath();
                holder.FocusNeighborTop = holder.GetPath();
                holder.FocusNeighborBottom = holder.GetPath();
            }
        }

        private void SelectPotion(string id)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _completion.TrySetResult([id]);
        }
    }

    private static StyleBoxFlat CreateTileStyle(bool selected)
    {
        var style = new StyleBoxFlat
        {
            BgColor = selected ? new Color(0.16f, 0.21f, 0.36f, 0.88f) : new Color(0.02f, 0.025f, 0.035f, 0.72f),
            BorderColor = selected ? new Color(0.45f, 0.58f, 1.0f, 1.0f) : new Color(0.22f, 0.24f, 0.30f, 0.88f),
            BorderWidthLeft = selected ? 3 : 1,
            BorderWidthTop = selected ? 3 : 1,
            BorderWidthRight = selected ? 3 : 1,
            BorderWidthBottom = selected ? 3 : 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusBottomLeft = 12
        };
        return style;
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

    private enum ItemKind
    {
        Potion,
        Relic
    }
}
