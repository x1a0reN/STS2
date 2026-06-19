using Godot;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Challenges;
using GongdouSts2ChallengeMod.Models;
using GongdouSts2ChallengeMod.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

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

        var state = TryGetCombatState();
        var enemy = state?.Enemies.FirstOrDefault(e => e.IsPrimaryEnemy) ?? state?.Enemies.FirstOrDefault();
        var player = state?.PlayerCreatures.FirstOrDefault();
        var resources = ChallengeSessionManager.ActiveResources ?? PuzzleCatalog.ResolveResources(_config);
        var selection = ChallengeSessionManager.ActiveSelection;

        var content = CreateAnchoredControl<Control>("NativeChallengeInfoContent", 0f, 0f, 1f, 1f);
        AddChild(content);

        content.AddChild(BuildTitle());
        content.AddChild(BuildMonsterSummary(state, enemy));

        content.AddChild(BuildStageInfo());
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
        box.AddChild(CreateSectionLabel("目标"));
        box.AddChild(CreateLabel(name, 26, StsText));
        box.AddChild(CreateWrappedLabel(FormatEnemyHp(enemy), 20));

        if (state != null)
        {
            box.AddChild(CreateWrappedLabel($"当前：第{state.RoundNumber}回合。", 19));
        }

        box.AddChild(CreateSectionLabel("条件"));
        box.AddChild(CreateWrappedLabel("胜利：击败敌人。", 19));
        box.AddChild(CreateWrappedLabel("失败：玩家死亡。", 18));

        if (_config.EnemyActions.Any(action => action.FailIfAlive))
        {
            box.AddChild(CreateWrappedLabel("终结：行动后敌人仍存活，挑战失败。", 18));
        }

        return box;
    }

    private Control BuildStageInfo()
    {
        var box = CreateAnchoredControl<VBoxContainer>("NativeStageInfo", 0.38f, 0.30f, 0.62f, 0.72f);
        box.AddThemeConstantOverride("separation", 8);
        box.AddChild(CreateCenteredSectionLabel("关卡信息"));

        foreach (var line in BuildStageInfoLines())
        {
            box.AddChild(CreateWrappedLabel(line, 18));
        }

        return box;
    }

    private IEnumerable<string> BuildStageInfoLines()
    {
        yield return $"角色：铁甲战士。生命{_config.Player.StartingHp}，能量{_config.Player.MaxEnergy}，每回合抽{_config.Player.DrawPerTurn}张。";
        yield return $"选牌：{FormatCardLimit()}。药水：{FormatItemLimit(_config.MaxPotions, "瓶")}。遗物：{FormatItemLimit(_config.MaxRelics, "个")}。";

        foreach (var line in BuildKeyMechanicLines())
        {
            yield return line;
        }
    }

    private IEnumerable<string> BuildKeyMechanicLines()
    {
        if (_config.EnemyActions.Any(action => action.ArmorGain > 0))
        {
            yield return "保留格挡：敌人格挡不会在回合结束时失去。";
        }

        if (_config.EnemyActions.Any(action => action.FailIfAlive))
        {
            yield return "终结：指定行动结算后，敌人仍存活则失败。";
        }

        foreach (var line in BuildStageSpecificMechanicLines().Take(3))
        {
            yield return line;
        }

        foreach (var constraint in _config.SelectionConstraints.Take(2))
        {
            if (!string.IsNullOrWhiteSpace(constraint.Description))
            {
                yield return $"限制：{NormalizeSentence(constraint.Description)}";
            }
        }
    }

    private IEnumerable<string> BuildStageSpecificMechanicLines()
    {
        switch (_config.StageIndex)
        {
            case 3:
                yield return "弃牌：触发奇巧牌的免费打出。";
                break;
            case 4:
                yield return "灼伤：第1/2/3回合加入弃牌堆。";
                yield return "状态：状态牌或诅咒牌进入牌堆后，顺劈斩（改）获得强化。";
                break;
            case 5:
                yield return "人工制品：先抵消负面状态。虚空可被指定牌消耗。";
                break;
            case 6:
                yield return "愤怒：攻击伤害翻倍。";
                yield return "平静：离开时获得能量。";
                break;
            case 7:
                yield return "中毒：回合开始失去生命。敌人会解毒并获得保留格挡。";
                break;
            case 8:
                yield return "开局：生成闪电与冰霜充能球。";
                yield return "充能球：回合结束触发被动效果，释放最左侧充能球。";
                break;
            case 9:
                yield return "镜像：每回合抽4张。";
                yield return "真言：达到10点时进入神格。";
                break;
            case 10:
                yield return "裂隙牌组：每回合抽4张。";
                yield return "裂隙：充能、标记、回声与过热决定斩杀窗口。";
                break;
        }
    }

    private Control BuildActionSummary(CombatState? state, Creature? enemy, Creature? player)
    {
        var box = CreateAnchoredControl<VBoxContainer>("NativeActionSummary", 0.67f, 0.30f, 0.93f, 0.72f);
        box.AddThemeConstantOverride("separation", 7);
        box.AddChild(CreateSectionLabel("敌方行动"));

        var actions = _config.EnemyActions.OrderBy(action => action.Turn).Take(5).ToList();
        if (actions.Count == 0)
        {
            box.AddChild(CreateWrappedLabel("使用原生行动表。", 19));
            return box;
        }

        if (state != null)
        {
            var current = ResolveActionForRound(actions, state.RoundNumber);
            box.AddChild(CreateWrappedLabel($"当前行动：{FormatAction(current)}", 18));
        }

        foreach (var action in actions)
        {
            box.AddChild(CreateActionRow(action, enemy, player));
        }

        box.AddChild(CreateWrappedLabel(FormatActionLoopEnd(actions), 16));
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
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(CreateIntentIcon(action, enemy, player));

        var text = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        text.AddThemeConstantOverride("separation", 2);
        text.AddChild(CreateLabel($"第{action.Turn}回合", 18, StsText));
        text.AddChild(CreateWrappedLabel(FormatAction(action), 15));
        row.AddChild(text);
        return row;
    }

    private Control CreateIntentIcon(EnemyActionConfig action, Creature? enemy, Creature? player)
    {
        var frame = new Control
        {
            CustomMinimumSize = new Vector2(56, 56),
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
            node.Scale = Vector2.One * 0.62f;
            node.Position = new Vector2(2f, 4f);
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

    private Control CreateNativeCloseButton()
    {
        var button = new Button
        {
            Name = "NativeChallengeInfoBackButton",
            Text = "返回战斗",
            CustomMinimumSize = new Vector2(260f, 62f),
            Size = new Vector2(260f, 62f),
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.All
        };

        button.AnchorLeft = 0.5f;
        button.AnchorRight = 0.5f;
        button.AnchorTop = 1f;
        button.AnchorBottom = 1f;
        button.OffsetLeft = -130f;
        button.OffsetRight = 130f;
        button.OffsetTop = -100f;
        button.OffsetBottom = -38f;
        button.Pressed += Close;
        return button;
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

    private void Close()
    {
        NOverlayStack.Instance?.Remove(this);
    }

    private void OnGuiInput(InputEvent input)
    {
        if (IsCloseInput(input))
        {
            Close();
            AcceptEvent();
        }
    }

    private static bool IsCloseInput(InputEvent input)
    {
        return input.IsActionPressed("ui_cancel")
            || input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            || input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right };
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

    private string FormatCardLimit()
    {
        return _config.MinCards == _config.MaxCards
            ? $"选择{_config.MaxCards}张"
            : $"选择{_config.MinCards}-{_config.MaxCards}张";
    }

    private static string FormatItemLimit(int count, string unit)
    {
        return count <= 0 ? "无" : $"选择{count}{unit}";
    }

    private static string FormatActionLoopEnd(IReadOnlyList<EnemyActionConfig> actions)
    {
        var last = actions[^1];
        return last.FailIfAlive
            ? "终结行动结算后，敌人仍存活则失败。"
            : "行动表结束后，重复最后一个行动。";
    }

    private static string FormatAction(EnemyActionConfig action)
    {
        var parts = new List<string>();
        if (action.Damage > 0)
        {
            parts.Add($"攻击{action.Damage}点。");
        }
        else
        {
            parts.Add(string.Equals(action.Type, "ritual", StringComparison.OrdinalIgnoreCase)
                ? "蓄力。"
                : $"{action.Type}。");
        }

        if (action.ArmorGain > 0)
        {
            parts.Add($"回合结束获得{action.ArmorGain}点保留格挡。");
        }

        if (action.FailIfAlive)
        {
            parts.Add("若敌人仍存活，挑战失败。");
        }

        return string.Concat(parts);
    }

    private static string NormalizeSentence(string text)
    {
        var trimmed = text.Trim().Replace(" ", "").TrimEnd('。', '；', ';', '.');
        return $"{trimmed}。";
    }

    private static Label CreateSectionLabel(string text)
    {
        return CreateLabel(text, 23, StsGold);
    }

    private static Label CreateCenteredSectionLabel(string text)
    {
        var label = CreateSectionLabel(text);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private static Label CreateWrappedLabel(string text, int fontSize)
    {
        var label = CreateLabel(text, fontSize, StsTextMuted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    private static Label CreateLabel(string text, int fontSize, Color color)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
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
