using System.Reflection;
using Godot;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Models;
using GongdouSts2ChallengeMod.Relics;
using GongdouSts2ChallengeMod.Sts2;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace GongdouSts2ChallengeMod.Preparation;

public static class NativeChallengePreparationFlow
{
    private const string CardSelectionBackdropName = "GongDouCardSelectionOpaqueBackdrop";
    private const string PreparationBackdropName = "GongDouPreparationOpaqueBackdrop";
    private static readonly FieldInfo? SelectedCardsField = typeof(NSimpleCardSelectScreen).GetField(
        "_selectedCards",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly Dictionary<Type, MemberInfo?> SelectedFlagMemberCache = new();
    private static readonly object SelectedFlagMemberCacheLock = new();
    private static ActiveCardSelectionCounterState? _activeCardSelectionCounter;

    public static async Task<ChallengeSelection?> ShowAsync(
        Sts2PuzzleConfig config,
        ResourcePool resources,
        RunState runState,
        Player player,
        Func<Task>? closePreparationCover = null)
    {
        GongdouSts2ChallengeMod.EnsureLocalizationRegistered();

        await GongdouSts2ChallengeMod.RunOnMainThread(() =>
        {
            GongdouSts2ChallengeMod.EnsureLocalizationRegistered();
            if (NOverlayStack.Instance == null)
            {
                throw new InvalidOperationException("STS2 overlay stack is not available. Challenge preparation requires an active run UI.");
            }

            NOverlayStack.Instance.Clear();
            NMapScreen.Instance?.Close(animateOut: false);
            ChallengePreparationRoomController.SwitchToBlankRoom("preparation-flow-opened");
        }).ConfigureAwait(false);

        var closePreparationBackdrop = ChallengePreparationRoomController.IsBlankRoomActive
            ? (Action)(() => { })
            : await GongdouSts2ChallengeMod.RunOnMainThread(ShowPreparationBlankBackdrop).ConfigureAwait(false);
        try
        {
            var selection = new ChallengeSelection();
            while (true)
            {
                selection.CardIds.Clear();
                var selectedCards = await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => SelectCardsAsync(config, resources, runState, player, closePreparationCover)).ConfigureAwait(false);
                if (selectedCards == null)
                {
                    return null;
                }

                selection.CardIds.AddRange(selectedCards);
                var constraintErrors = ValidateCardConstraints(config, selection.CardIds);
                if (constraintErrors.Count == 0)
                {
                    break;
                }

                await GongdouSts2ChallengeMod.RunOnMainThread(() =>
                    ChallengeCompletionOverlay.ShowNotice(
                        "选牌限制未满足",
                        string.Join("\n", constraintErrors.Take(6)))).ConfigureAwait(false);
            }

            IReadOnlyList<string>? selectedPotions;
            if (config.MaxPotions <= 0 || resources.PotionPool.Count == 0)
            {
                selectedPotions = Array.Empty<string>();
            }
            else
            {
                await GongdouSts2ChallengeMod.RunOnMainThread(() =>
                    ChallengePreparationRoomController.SwitchToBlankRoom("before-potion-selection")).ConfigureAwait(false);
                selectedPotions = await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => NativeItemSelectionOverlay.ShowPotionsAsync(
                        config,
                        resources.PotionPool,
                        config.MaxPotions)).ConfigureAwait(false);
            }
            if (selectedPotions == null)
            {
                return null;
            }

            selection.PotionIds.AddRange(selectedPotions);

            IReadOnlyList<string>? selectedRelics;
            if (config.MaxRelics <= 0 || resources.RelicPool.Count == 0)
            {
                selectedRelics = Array.Empty<string>();
            }
            else
            {
                await GongdouSts2ChallengeMod.RunOnMainThread(() =>
                    ChallengePreparationRoomController.SwitchToBlankRoom("before-relic-selection")).ConfigureAwait(false);
                selectedRelics = await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => NativeItemSelectionOverlay.ShowRelicsAsync(
                        config,
                        resources.RelicPool,
                        config.MaxRelics)).ConfigureAwait(false);
            }
            if (selectedRelics == null)
            {
                return null;
            }

            selection.RelicIds.AddRange(selectedRelics);
            return selection;
        }
        finally
        {
            await GongdouSts2ChallengeMod.RunOnMainThread(closePreparationBackdrop).ConfigureAwait(false);
        }
    }

    private static List<string> ValidateCardConstraints(Sts2PuzzleConfig config, IReadOnlyList<string> cardIds)
    {
        var errors = new List<string>();
        foreach (var constraint in config.SelectionConstraints)
        {
            var error = constraint.Validate(cardIds);
            if (!string.IsNullOrWhiteSpace(error))
            {
                errors.Add(error);
            }
        }

        return errors;
    }

    private static async Task<List<string>?> SelectCardsAsync(
        Sts2PuzzleConfig config,
        ResourcePool resources,
        RunState runState,
        Player player,
        Func<Task>? closePreparationCover)
    {
        GongdouSts2ChallengeMod.EnsureLocalizationRegistered();

        var candidates = new List<(string Id, CardModel Card)>();
        try
        {
            foreach (var item in resources.ExpandedCardPool())
            {
                var card = ChallengeCardFactory.CreateById(item.Id);
                runState.AddCard(card, player);
                card.AfterCreated();
                candidates.Add((item.Id, card));
            }

            var prefs = new CardSelectorPrefs(
                new LocString("cards", "GONGDOU_SELECT_CARDS"),
                config.MinCards,
                config.MaxCards)
            {
                RequireManualConfirmation = true,
                Cancelable = true,
                PretendCardsCanBePlayed = true
            };

            ChallengePreparationRoomController.SwitchToBlankRoom("before-card-selection");
            var selectionTask = CardSelectCmd.FromSimpleGrid(
                    new BlockingPlayerChoiceContext(),
                    candidates.Select(c => c.Card).ToList(),
                    player,
                    prefs);
            if (closePreparationCover != null)
            {
                await closePreparationCover().ConfigureAwait(false);
            }

            Action closeBackdrop = () => { };
            Action closeCounter = () => { };
            await GongdouSts2ChallengeMod.RunOnMainThread(() =>
            {
                ChallengePreparationRoomController.SwitchToBlankRoom("after-preparation-cover-close");
                closeBackdrop = ShowCardSelectionBackdrop();
                closeCounter = ShowCardSelectionCounter(config, resources, candidates.Select(c => c.Card).ToList());
            }).ConfigureAwait(false);
            try
            {
                var selected = (await selectionTask).ToList();

                return candidates
                    .Where(c => selected.Any(card => ReferenceEquals(card, c.Card)))
                    .Select(c => c.Id)
                    .ToList();
            }
            finally
            {
                await GongdouSts2ChallengeMod.RunOnMainThread(() =>
                {
                    closeCounter();
                    closeBackdrop();
                }).ConfigureAwait(false);
            }
        }
        finally
        {
            await GongdouSts2ChallengeMod.RunOnMainThread(() =>
            {
                foreach (var (_, card) in candidates)
                {
                    runState.RemoveCard(card);
                }
            }).ConfigureAwait(false);
        }
    }

    private static Action ShowCardSelectionCounter(
        Sts2PuzzleConfig config,
        ResourcePool resources,
        IReadOnlyList<CardModel> cards)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            return () => { };
        }

        var constraintText = ChallengeSelectionRequirementText.BuildCardConstraintHintText(config, resources);
        var state = new ActiveCardSelectionCounterState(config, constraintText, cards);
        _activeCardSelectionCounter = state;
        var timer = new Godot.Timer
        {
            WaitTime = 0.12,
            OneShot = false,
            Autostart = true
        };
        tree.Root.AddChild(timer);

        void UpdateLabel()
        {
            RefreshActiveCardSelectionCounter(null);
        }

        timer.Timeout += UpdateLabel;
        UpdateLabel();

        var closed = false;
        return () =>
        {
            if (closed)
            {
                return;
            }

            closed = true;
            timer.Timeout -= UpdateLabel;
            timer.QueueFree();
            if (ReferenceEquals(_activeCardSelectionCounter, state))
            {
                _activeCardSelectionCounter = null;
            }
        };
    }

    internal static void RefreshCardSelectionCounterFromNative(NSimpleCardSelectScreen screen)
    {
        RefreshActiveCardSelectionCounter(screen);
    }

    private static void RefreshActiveCardSelectionCounter(NSimpleCardSelectScreen? screen)
    {
        var state = _activeCardSelectionCounter;
        if (state == null)
        {
            return;
        }

        int? nativeSelectedCount = null;
        if (screen != null && GodotObject.IsInstanceValid(screen))
        {
            state.NativeScreen = screen;
            nativeSelectedCount = ReadNativeSelectedCardCount(screen);
        }

        int selectedCount;
        if (nativeSelectedCount.HasValue)
        {
            selectedCount = nativeSelectedCount.Value;
        }
        else
        {
            var cachedNativeScreen = state.NativeScreen;
            var ticksUntilNativeSearch = state.TicksUntilNativeSearch;
            selectedCount = CountSelectedCards(state.Cards, ref cachedNativeScreen, ref ticksUntilNativeSearch);
            state.NativeScreen = cachedNativeScreen;
            state.TicksUntilNativeSearch = ticksUntilNativeSearch;
        }

        state.LastSelectedCount = selectedCount;

        var nativeLabel = screen != null && GodotObject.IsInstanceValid(screen)
            ? TryGetNativeCardSelectionBottomLabel(screen)
            : null;
        if (nativeLabel == null)
        {
            var cachedNativeLabel = state.NativeLabel;
            var ticksUntilLabelSearch = state.TicksUntilLabelSearch;
            nativeLabel = ResolveNativeCardSelectionBottomLabel(
                state.NativeScreen,
                ref cachedNativeLabel,
                ref ticksUntilLabelSearch);
            state.NativeLabel = cachedNativeLabel;
            state.TicksUntilLabelSearch = ticksUntilLabelSearch;
        }
        else
        {
            state.NativeLabel = nativeLabel;
        }

        nativeLabel?.SetTextAutoSize(BuildNativeCardSelectionBottomText(
            state.Config,
            state.LastSelectedCount,
            state.ConstraintText));
    }

    private static string BuildNativeCardSelectionBottomText(
        Sts2PuzzleConfig config,
        int selectedCount,
        string constraintText)
    {
        var countLine = $"选择卡牌：{selectedCount}/{config.MaxCards}";
        return string.IsNullOrWhiteSpace(constraintText)
            ? countLine
            : countLine + "\n" + constraintText;
    }

    private static MegaRichTextLabel? ResolveNativeCardSelectionBottomLabel(
        NSimpleCardSelectScreen? cachedNativeScreen,
        ref MegaRichTextLabel? cachedNativeLabel,
        ref int ticksUntilSearch)
    {
        if (cachedNativeLabel != null && GodotObject.IsInstanceValid(cachedNativeLabel))
        {
            return cachedNativeLabel;
        }

        cachedNativeLabel = TryGetNativeCardSelectionBottomLabel(cachedNativeScreen);
        if (cachedNativeLabel != null)
        {
            return cachedNativeLabel;
        }

        if (ticksUntilSearch > 0)
        {
            ticksUntilSearch--;
            return null;
        }

        ticksUntilSearch = 5;
        if (NOverlayStack.Instance is not Node overlayStack)
        {
            return null;
        }

        foreach (var node in EnumerateNodes(overlayStack))
        {
            if (node is not NSimpleCardSelectScreen screen)
            {
                continue;
            }

            cachedNativeLabel = TryGetNativeCardSelectionBottomLabel(screen);
            if (cachedNativeLabel != null)
            {
                return cachedNativeLabel;
            }
        }

        return null;
    }

    private static MegaRichTextLabel? TryGetNativeCardSelectionBottomLabel(NSimpleCardSelectScreen? screen)
    {
        return screen != null && GodotObject.IsInstanceValid(screen)
            ? screen.GetNodeOrNull<MegaRichTextLabel>("%BottomLabel")
            : null;
    }

    private static Action ShowCardSelectionBackdrop()
    {
        if (NOverlayStack.Instance is not Control stack)
        {
            return () => { };
        }

        var backdrop = stack.GetNodeOrNull<ColorRect>(CardSelectionBackdropName);
        if (backdrop == null)
        {
            backdrop = new ColorRect
            {
                Name = CardSelectionBackdropName,
                Color = new Color(0.015f, 0.018f, 0.025f, 0.985f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            FillParent(backdrop);
            stack.AddChild(backdrop);
        }

        backdrop.Visible = true;
        stack.MoveChild(backdrop, 0);

        var closed = false;
        return () =>
        {
            if (closed)
            {
                return;
            }

            closed = true;
            if (GodotObject.IsInstanceValid(backdrop))
            {
                backdrop.QueueFree();
            }
        };
    }

    private static Action ShowPreparationBlankBackdrop()
    {
        var globalUi = NRun.Instance?.GlobalUi;
        if (globalUi == null)
        {
            return () => { };
        }

        var backdrop = globalUi.GetNodeOrNull<ColorRect>(PreparationBackdropName);
        if (backdrop == null)
        {
            backdrop = new ColorRect
            {
                Name = PreparationBackdropName,
                Color = new Color(0.015f, 0.018f, 0.025f, 1.0f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            FillParent(backdrop);
            globalUi.AddChild(backdrop);
        }

        backdrop.Visible = true;
        globalUi.MoveChild(backdrop, 0);

        var closed = false;
        return () =>
        {
            if (closed)
            {
                return;
            }

            closed = true;
            if (GodotObject.IsInstanceValid(backdrop))
            {
                backdrop.QueueFree();
            }
        };
    }

    private static string FormatRequiredCardCount(Sts2PuzzleConfig config)
    {
        return config.MinCards == config.MaxCards
            ? $"必须选择 {config.MinCards} 张"
            : $"需要选择 {config.MinCards}-{config.MaxCards} 张";
    }

    private static int CountSelectedCards(
        IEnumerable<CardModel> cards,
        ref NSimpleCardSelectScreen? cachedNativeScreen,
        ref int ticksUntilNativeSearch)
    {
        var nativeSelectedCount = TryCountNativeSelectedCards(ref cachedNativeScreen, ref ticksUntilNativeSearch);
        if (nativeSelectedCount.HasValue)
        {
            return nativeSelectedCount.Value;
        }

        var count = 0;
        foreach (var card in cards)
        {
            if (ReadSelectedFlag(card))
            {
                count++;
            }
        }

        return count;
    }

    private static int? TryCountNativeSelectedCards(
        ref NSimpleCardSelectScreen? cachedNativeScreen,
        ref int ticksUntilNativeSearch)
    {
        if (cachedNativeScreen != null && GodotObject.IsInstanceValid(cachedNativeScreen))
        {
            var cachedCount = ReadNativeSelectedCardCount(cachedNativeScreen);
            if (cachedCount.HasValue)
            {
                return cachedCount.Value;
            }

            cachedNativeScreen = null;
        }

        if (ticksUntilNativeSearch > 0)
        {
            ticksUntilNativeSearch--;
            return null;
        }

        ticksUntilNativeSearch = 5;
        if (NOverlayStack.Instance is not Node overlayStack)
        {
            return null;
        }

        foreach (var node in EnumerateNodes(overlayStack))
        {
            if (node is not NSimpleCardSelectScreen screen)
            {
                continue;
            }

            var count = ReadNativeSelectedCardCount(screen);
            if (count.HasValue)
            {
                cachedNativeScreen = screen;
                return count.Value;
            }
        }

        return null;
    }

    private static int? ReadNativeSelectedCardCount(NSimpleCardSelectScreen screen)
    {
        var selectedCards = SelectedCardsField?.GetValue(screen);
        return selectedCards switch
        {
            ICollection<CardModel> typed => typed.Count,
            System.Collections.ICollection collection => collection.Count,
            IEnumerable<CardModel> enumerable => enumerable.Count(),
            _ => null
        };
    }

    private static IEnumerable<Node> EnumerateNodes(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!GodotObject.IsInstanceValid(node))
            {
                continue;
            }

            yield return node;

            foreach (var child in node.GetChildren().OfType<Node>())
            {
                stack.Push(child);
            }
        }
    }

    private static bool ReadSelectedFlag(CardModel card)
    {
        var member = GetSelectedFlagMember(card.GetType());
        return member switch
        {
            PropertyInfo property when property.GetValue(card) is bool selected => selected,
            FieldInfo field when field.GetValue(card) is bool selected => selected,
            _ => false
        };
    }

    private static MemberInfo? GetSelectedFlagMember(Type cardType)
    {
        lock (SelectedFlagMemberCacheLock)
        {
            if (SelectedFlagMemberCache.TryGetValue(cardType, out var cached))
            {
                return cached;
            }

            var member = FindSelectedFlagMember(cardType);
            SelectedFlagMemberCache[cardType] = member;
            return member;
        }
    }

    private static MemberInfo? FindSelectedFlagMember(Type cardType)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (var type = cardType; type != null; type = type.BaseType)
        {
            var property = type.GetProperty("Selected", Flags) ?? type.GetProperty("IsSelected", Flags);
            if (property?.PropertyType == typeof(bool))
            {
                return property;
            }

            var field = type.GetField("_selected", Flags)
                ?? type.GetField("_isSelected", Flags)
                ?? type.GetField("selected", Flags)
                ?? type.GetField("isSelected", Flags);
            if (field?.FieldType == typeof(bool))
            {
                return field;
            }
        }

        return null;
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

    private sealed class ActiveCardSelectionCounterState(
        Sts2PuzzleConfig config,
        string constraintText,
        IReadOnlyList<CardModel> cards)
    {
        public Sts2PuzzleConfig Config { get; } = config;
        public string ConstraintText { get; } = constraintText;
        public IReadOnlyList<CardModel> Cards { get; } = cards;
        public NSimpleCardSelectScreen? NativeScreen { get; set; }
        public MegaRichTextLabel? NativeLabel { get; set; }
        public int TicksUntilNativeSearch { get; set; }
        public int TicksUntilLabelSearch { get; set; }
        public int LastSelectedCount { get; set; }
    }
}

internal static class ChallengeSelectionRequirementText
{
    public static string BuildCardSelectionText(Sts2PuzzleConfig config, ResourcePool resources)
    {
        var lines = new List<string>
        {
            $"本关资源限制：卡牌{FormatCardCount(config)}；药水必须选择 {config.MaxPotions} 瓶；遗物必须选择 {config.MaxRelics} 个。",
            resources.PotionPool.Count > 0
                ? $"可选药水：{FormatPool(resources.PotionPool)}。"
                : "可选药水：无，本关不能带药水。",
            resources.RelicPool.Count > 0
                ? $"可选遗物：{FormatPool(resources.RelicPool)}。"
                : "可选遗物：无，本关不能带遗物。"
        };

        if (config.SelectionConstraints.Count == 0)
        {
            lines.Add("选牌额外限制：无，只要选满指定张数即可。");
        }
        else
        {
            lines.Add("选牌额外限制：");
            lines.AddRange(config.SelectionConstraints.Select((constraint, index) =>
                $"{index + 1}. {FormatConstraint(constraint, resources)}"));
        }

        return string.Join("\n", lines);
    }

    public static string BuildCardConstraintHintText(Sts2PuzzleConfig config, ResourcePool resources)
    {
        if (config.SelectionConstraints.Count == 0)
        {
            return "";
        }

        return string.Join("\n", config.SelectionConstraints.Select((constraint, index) =>
            $"{index + 1}. {FormatConstraint(constraint, resources)}"));
    }

    public static string BuildItemSelectionText(
        Sts2PuzzleConfig config,
        IReadOnlyList<ResourceItem> items,
        string kindName,
        int requiredCount)
    {
        var candidates = items.Count > 0 ? FormatPool(items) : "无";
        return
            $"本关总限制：卡牌{FormatCardCount(config)}；药水必须选择 {config.MaxPotions} 瓶；遗物必须选择 {config.MaxRelics} 个。\n" +
            $"当前步骤：必须从下方候选中选择 {requiredCount} 个{kindName}，不多不少。\n" +
            $"候选{kindName}：{candidates}。";
    }

    private static string FormatCardCount(Sts2PuzzleConfig config)
    {
        return config.MinCards == config.MaxCards
            ? $"必须选择 {config.MinCards} 张"
            : $"需要选择 {config.MinCards}-{config.MaxCards} 张";
    }

    private static string FormatConstraint(SelectionConstraintConfig constraint, ResourcePool resources)
    {
        var names = constraint.CardIds.Length == 0
            ? "未指定卡牌"
            : string.Join("、", constraint.CardIds.Select(id => ResolveResourceName(id, resources.CardPool)));

        string requirement;
        if (constraint.RequireEach)
        {
            requirement = $"每一种都至少 {Math.Max(1, constraint.MinCount)} 张";
        }
        else if (constraint.ExactCount >= 0)
        {
            requirement = $"合计必须 {constraint.ExactCount} 张";
        }
        else if (constraint.MinCount >= 0 && constraint.MaxCount >= 0)
        {
            requirement = $"合计必须 {constraint.MinCount}-{constraint.MaxCount} 张";
        }
        else if (constraint.MinCount >= 0)
        {
            requirement = $"合计至少 {constraint.MinCount} 张";
        }
        else if (constraint.MaxCount >= 0)
        {
            requirement = $"合计最多 {constraint.MaxCount} 张";
        }
        else
        {
            requirement = "必须满足该组合条件";
        }

        return string.IsNullOrWhiteSpace(constraint.Description)
            ? $"{requirement}：{names}"
            : $"{constraint.Description}（{requirement}：{names}）";
    }

    private static string FormatPool(IReadOnlyList<ResourceItem> items)
    {
        return string.Join("、", items.Select(item =>
            item.MaxCopies > 1 ? $"{item.Name} x{item.MaxCopies}" : item.Name));
    }

    private static string ResolveResourceName(string id, IReadOnlyList<ResourceItem> items)
    {
        var item = items.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
        return item == null
            ? id
            : item.MaxCopies > 1 ? $"{item.Name} x{item.MaxCopies}" : item.Name;
    }
}
