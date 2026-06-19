using Godot;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Challenges;
using GongdouSts2ChallengeMod.Models;
using GongdouSts2ChallengeMod.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.addons.mega_text;

namespace GongdouSts2ChallengeMod.Patches;

internal sealed class GongdouChallengeInfoScreen : Control, IOverlayScreen
{
    private readonly Sts2PuzzleConfig _config;
    private Control? _closeButton;

    public GongdouChallengeInfoScreen(Sts2PuzzleConfig config)
    {
        _config = config;
        Name = "GongDouChallengeInfoScreen";
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        GuiInput += OnGuiInput;
        try
        {
            BuildUi();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to build challenge info screen: {ex}");
            BuildFallbackCloseOnlyUi();
        }
    }

    public NetScreenType ScreenType => NetScreenType.None;
    public bool UseSharedBackstop => true;
    public Control? DefaultFocusedControl => _closeButton;
    public Control? FocusedControlFromTopBar => DefaultFocusedControl;

    public void AfterOverlayOpened()
    {
    }

    public void AfterOverlayClosed()
    {
        QueueFree();
    }

    public void AfterOverlayShown()
    {
        Visible = true;
        GrabFocus();
    }

    public void AfterOverlayHidden()
    {
        Visible = false;
    }

    private void BuildUi()
    {
        FillParent(this);
        AddChild(CreateNativeInfoBanner());

        var state = TryGetCombatState();
        var enemy = state?.Enemies.FirstOrDefault(e => e.IsPrimaryEnemy) ?? state?.Enemies.FirstOrDefault();
        var player = state?.PlayerCreatures.FirstOrDefault();
        var resources = ChallengeSessionManager.ActiveResources ?? PuzzleCatalog.ResolveResources(_config);
        var selection = ChallengeSessionManager.ActiveSelection;

        var content = CreateAnchoredControl<Control>("NativeChallengeInfoContent", 0f, 0f, 1f, 1f);
        AddChild(content);

        content.AddChild(BuildTitle());
        content.AddChild(BuildMonsterSummary(state, enemy));

        var monsterFrame = CreateAnchoredControl<Control>("NativeMonsterVisual", 0.38f, 0.29f, 0.62f, 0.70f);
        content.AddChild(monsterFrame);
        AddMonsterVisual(monsterFrame, enemy);

        content.AddChild(BuildActionSummary(state, enemy, player));
        content.AddChild(BuildResourceSummary(resources, selection, enemy, player));

        _closeButton = CreateNativeCloseButton();
        AddChild(_closeButton);
    }

    private Control BuildTitle()
    {
        var box = CreateAnchoredControl<VBoxContainer>("NativeInfoTitle", 0.18f, 0.18f, 0.82f, 0.25f);
        box.Alignment = BoxContainer.AlignmentMode.Center;

        var title = CreateLabel($"第{_config.StageIndex}/{_config.StageCount}关：{_config.PuzzleName}", 32, StsGold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        return box;
    }

    private Control BuildMonsterSummary(CombatState? state, Creature? enemy)
    {
        var box = CreateAnchoredControl<VBoxContainer>("NativeMonsterSummary", 0.08f, 0.30f, 0.33f, 0.72f);
        box.AddThemeConstantOverride("separation", 10);

        var name = string.IsNullOrWhiteSpace(enemy?.Name) ? _config.Enemy.Name : enemy.Name;
        box.AddChild(CreateSectionLabel("怪物"));
        box.AddChild(CreateLabel(name, 26, StsText));
        box.AddChild(CreateWrappedLabel(FormatEnemyHp(enemy), 20));

        if (state != null)
        {
            box.AddChild(CreateWrappedLabel($"当前第{state.RoundNumber}回合", 19));
        }

        box.AddChild(CreateSectionLabel("胜利条件"));
        box.AddChild(CreateWrappedLabel(_config.WinConditionText, 19));

        if (!string.IsNullOrWhiteSpace(_config.EnemyInfoText))
        {
            box.AddChild(CreateSectionLabel("规则"));
            box.AddChild(CreateWrappedLabel(_config.EnemyInfoText, 18));
        }

        return box;
    }

    private Control BuildActionSummary(CombatState? state, Creature? enemy, Creature? player)
    {
        var box = CreateAnchoredControl<VBoxContainer>("NativeActionSummary", 0.67f, 0.30f, 0.93f, 0.72f);
        box.AddThemeConstantOverride("separation", 10);
        box.AddChild(CreateSectionLabel("行动循环"));

        var actions = _config.EnemyActions.OrderBy(action => action.Turn).Take(4).ToList();
        if (actions.Count == 0)
        {
            box.AddChild(CreateWrappedLabel("按当前怪物原生行动执行。", 19));
            return box;
        }

        if (state != null)
        {
            var current = ResolveActionForRound(actions, state.RoundNumber);
            box.AddChild(CreateWrappedLabel($"当前：{FormatAction(current)}", 19));
        }

        foreach (var action in actions)
        {
            box.AddChild(CreateActionRow(action, enemy, player));
        }

        box.AddChild(CreateWrappedLabel($"之后重复第{actions[^1].Turn}回合行动。", 18));
        return box;
    }

    private Control BuildResourceSummary(
        ResourcePool resources,
        ChallengeSelection? selection,
        Creature? enemy,
        Creature? player)
    {
        var box = CreateAnchoredControl<VBoxContainer>("NativeResourceSummary", 0.18f, 0.74f, 0.82f, 0.88f);
        box.Alignment = BoxContainer.AlignmentMode.Center;
        box.AddThemeConstantOverride("separation", 8);
        box.AddChild(CreateCenteredSectionLabel("资源与状态"));

        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        row.AddThemeConstantOverride("separation", 18);
        box.AddChild(row);

        AddSelectedResourceIcons(row, resources, selection);
        AddPowerIcons(row, enemy);
        AddPowerIcons(row, player);

        if (row.GetChildCount() == 0)
        {
            var empty = CreateLabel("本关无药水、遗物或可见状态。", 19, StsTextMuted);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(empty);
        }

        return box;
    }

    private Control CreateActionRow(EnemyActionConfig action, Creature? enemy, Creature? player)
    {
        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(CreateIntentIcon(action, enemy, player));

        var text = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        text.AddThemeConstantOverride("separation", 2);
        text.AddChild(CreateLabel($"第{action.Turn}回合", 20, StsText));
        text.AddChild(CreateWrappedLabel(FormatAction(action), 17));
        row.AddChild(text);
        return row;
    }

    private Control CreateIntentIcon(EnemyActionConfig action, Creature? enemy, Creature? player)
    {
        var frame = new Control
        {
            CustomMinimumSize = new Vector2(72, 72),
            MouseFilter = MouseFilterEnum.Ignore
        };

        if (enemy == null)
        {
            frame.AddChild(CreateTextIcon(action.Damage > 0 ? "攻" : "技"));
            return frame;
        }

        try
        {
            var node = NIntent.Create(0f);
            node.Scale = Vector2.One * 0.78f;
            node.Position = new Vector2(4f, 6f);
            var targets = player == null ? Array.Empty<Creature>() : [player];
            node.Connect(Node.SignalName.Ready, Callable.From(() =>
                node.UpdateIntent(CreateIntent(action), targets, enemy)));
            frame.AddChild(node);
            return frame;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to create native intent icon: {ex.Message}");
            frame.AddChild(CreateTextIcon(action.Damage > 0 ? "攻" : "技"));
            return frame;
        }
    }

    private static AbstractIntent CreateIntent(EnemyActionConfig action)
    {
        return action.Damage > 0
            ? new SingleAttackIntent(action.Damage)
            : new BuffIntent();
    }

    private void AddSelectedResourceIcons(HBoxContainer row, ResourcePool resources, ChallengeSelection? selection)
    {
        var potionIds = (selection?.PotionIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
        var relicIds = (selection?.RelicIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        if (potionIds.Count == 0 && _config.MaxPotions > 0)
        {
            potionIds = resources.PotionPool.Select(item => item.Id).Take(_config.MaxPotions).ToList();
        }

        if (relicIds.Count == 0 && _config.MaxRelics > 0)
        {
            relicIds = resources.RelicPool.Select(item => item.Id).Take(_config.MaxRelics).ToList();
        }

        foreach (var id in potionIds.Take(3))
        {
            row.AddChild(CreatePotionIcon(id));
        }

        foreach (var id in relicIds.Take(4))
        {
            row.AddChild(CreateRelicIcon(id));
        }
    }

    private static Control CreatePotionIcon(string id)
    {
        try
        {
            var potion = ChallengePotionFactory.CreateById(id);
            var holder = NPotionHolder.Create(isUsable: false);
            holder.CustomMinimumSize = new Vector2(88, 88);
            holder.Scale = Vector2.One * 0.85f;
            holder.Connect(Node.SignalName.Ready, Callable.From(() =>
            {
                if (holder.Potion != null)
                {
                    return;
                }

                var potionNode = NPotion.Create(potion);
                if (potionNode == null)
                {
                    return;
                }

                potionNode.Position = new Vector2(-30f, -30f);
                holder.AddPotion(potionNode);
            }));
            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to create potion icon for challenge info ({id}): {ex.Message}");
            return CreateTextIcon("药");
        }
    }

    private static Control CreateRelicIcon(string id)
    {
        try
        {
            var relic = ChallengeRelicFactory.CreateById(id);
            var holder = NRelicBasicHolder.Create(relic);
            if (holder == null)
            {
                return CreateTextIcon("遗");
            }

            holder.CustomMinimumSize = new Vector2(88, 88);
            holder.Scale = Vector2.One * 1.25f;
            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to create relic icon for challenge info ({id}): {ex.Message}");
            return CreateTextIcon("遗");
        }
    }

    private static void AddPowerIcons(HBoxContainer row, Creature? creature)
    {
        if (creature == null)
        {
            return;
        }

        foreach (var power in creature.Powers.Where(power => power.IsVisible).Take(5))
        {
            try
            {
                var icon = NPower.Create(power);
                icon.Scale = Vector2.One * 0.9f;
                row.AddChild(icon);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to create native power icon for challenge info: {ex.Message}");
            }
        }
    }

    private void AddMonsterVisual(Control frame, Creature? enemy)
    {
        if (enemy == null)
        {
            frame.AddChild(CreateCenteredFallback("暂无怪物模型"));
            return;
        }

        Callable.From(() =>
        {
            try
            {
                if (!GodotObject.IsInstanceValid(frame))
                {
                    return;
                }

                var visuals = enemy.CreateVisuals();
                if (visuals == null)
                {
                    frame.AddChild(CreateCenteredFallback("暂无怪物模型"));
                    return;
                }

                visuals.Position = new Vector2(frame.Size.X * 0.5f, frame.Size.Y * 0.86f);
                visuals.Scale = Vector2.One * 0.9f;
                frame.AddChild(visuals);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to create native challenge info monster visual: {ex.Message}");
                if (GodotObject.IsInstanceValid(frame))
                {
                    frame.AddChild(CreateCenteredFallback("怪物模型加载失败"));
                }
            }
        }).CallDeferred();
    }

    private Control CreateNativeInfoBanner()
    {
        var banner = TryInstantiateNativeScene<NCommonBanner>(
            "res://scenes/ui/common_banner.tscn",
            "common_ui/common_banner",
            "screens/common_banner",
            "ui/common_banner");
        if (banner != null)
        {
            banner.Connect(Node.SignalName.Ready, Callable.From(() =>
            {
                banner.label.SetTextAutoSize("关卡信息");
                banner.AnimateIn();
            }));
            return banner;
        }

        return CreateInvisibleFallback("NativeChallengeInfoBannerFallback");
    }

    private Control CreateNativeCloseButton()
    {
        var button = TryInstantiateNativeScene<NChoiceSelectionSkipButton>(
            "res://scenes/ui/choice_selection_skip_button.tscn",
            "screens/card_selection/choice_selection_skip_button",
            "card_selection/choice_selection_skip_button",
            "ui/choice_selection_skip_button");
        if (button != null)
        {
            button.Name = "NativeChallengeInfoBackButton";
            button.Connect(Node.SignalName.Ready, Callable.From(() =>
            {
                SetNativeSkipButtonText(button, "返回战斗");
                button.AnimateIn();
            }));
            button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => Close()));
            return button;
        }

        return CreateInvisibleFallback("NativeChallengeInfoBackButtonFallback");
    }

    private void BuildFallbackCloseOnlyUi()
    {
        FillParent(this);
        _closeButton = CreateNativeCloseButton();
        AddChild(_closeButton);
    }

    private static CombatState? TryGetCombatState()
    {
        try
        {
            return CombatManager.Instance?.DebugOnlyGetState();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Challenge info opened without active combat state: {ex.Message}");
            return null;
        }
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

        GD.PrintErr($"[GongDou STS2] Failed to instantiate native {typeof(T).Name} for challenge info.");
        return null;
    }

    private static void SetNativeSkipButtonText(Control button, string text)
    {
        if (button.GetNodeOrNull<MegaLabel>("Label") is { } megaLabel)
        {
            megaLabel.SetTextAutoSize(text);
            return;
        }

        if (button.GetNodeOrNull<Label>("Label") is { } label)
        {
            label.Text = text;
        }
    }

    private void Close()
    {
        NOverlayStack.Instance?.Remove(this);
    }

    private void OnGuiInput(InputEvent input)
    {
        if (input.IsActionPressed("ui_cancel")
            || input.IsActionPressed("ui_pause")
            || input.IsActionPressed("ui_back"))
        {
            Close();
            AcceptEvent();
        }
    }

    private static EnemyActionConfig ResolveActionForRound(IReadOnlyList<EnemyActionConfig> actions, int round)
    {
        foreach (var action in actions)
        {
            if (action.Turn >= round)
            {
                return action;
            }
        }

        return actions[^1];
    }

    private string FormatEnemyHp(Creature? enemy)
    {
        return enemy != null
            ? $"生命{enemy.CurrentHp}/{enemy.MaxHp}，格挡{enemy.Block}"
            : $"初始生命{_config.Enemy.BaseHp}";
    }

    private static string FormatAction(EnemyActionConfig action)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(action.Description))
        {
            parts.Add(action.Description.Trim());
        }
        else if (action.Damage > 0)
        {
            parts.Add($"攻击{action.Damage}点");
        }
        else
        {
            parts.Add(string.Equals(action.Type, "ritual", StringComparison.OrdinalIgnoreCase) ? "蓄力" : action.Type);
        }

        if (action.ArmorGain > 0)
        {
            parts.Add($"获得{action.ArmorGain}点保留格挡");
        }

        if (action.FailIfAlive)
        {
            parts.Add("行动后仍存活则挑战失败");
        }

        return string.Join("；", parts);
    }

    private static MegaLabel CreateSectionLabel(string text)
    {
        return CreateLabel(text, 23, StsGold);
    }

    private static MegaLabel CreateCenteredSectionLabel(string text)
    {
        var label = CreateSectionLabel(text);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private static MegaLabel CreateWrappedLabel(string text, int fontSize)
    {
        var label = CreateLabel(text, fontSize, StsTextMuted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    private static MegaLabel CreateLabel(string text, int fontSize, Color color)
    {
        var label = new MegaLabel
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            MinFontSize = Math.Max(8, fontSize - 4),
            MaxFontSize = fontSize
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Control CreateCenteredFallback(string text)
    {
        var label = CreateWrappedLabel(text, 19);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        FillParent(label);
        return label;
    }

    private static Control CreateTextIcon(string text)
    {
        var label = CreateLabel(text, 24, StsText);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        CenterChild(label, new Vector2(64, 64));
        return label;
    }

    private static Control CreateInvisibleFallback(string name)
    {
        return new Control
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };
    }

    private static T CreateAnchoredControl<T>(string name, float left, float top, float right, float bottom)
        where T : Control, new()
    {
        var control = new T
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Ignore
        };
        control.AnchorLeft = left;
        control.AnchorTop = top;
        control.AnchorRight = right;
        control.AnchorBottom = bottom;
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
        return control;
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

    private static void CenterChild(Control control, Vector2 size)
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

    private static readonly Color StsGold = new(0.98f, 0.90f, 0.42f);
    private static readonly Color StsText = new(0.95f, 0.94f, 0.88f);
    private static readonly Color StsTextMuted = new(0.86f, 0.88f, 0.86f);
}
