using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using GongdouSts2FrierenMod.Cards;
using GongdouSts2FrierenMod.Relics;

namespace GongdouSts2FrierenMod.Powers;

public static class FrierenKeywordTips
{
    public static IEnumerable<IHoverTip> ForCard(string text, bool isSpell, bool isNormalMagic)
    {
        if (isSpell)
        {
            yield return HoverTipFactory.FromPower<FrierenSpellKeywordPower>();
        }

        if (isNormalMagic)
        {
            yield return HoverTipFactory.FromPower<FrierenNormalMagicKeywordPower>();
        }

        foreach (var tip in FromText(text, includeSpell: !isSpell, includeNormalMagic: !isNormalMagic))
        {
            yield return tip;
        }
    }

    public static IEnumerable<IHoverTip> FromText(
        string? text,
        bool includeSpell = true,
        bool includeNormalMagic = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        if (includeSpell && text.Contains("法术", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenSpellKeywordPower>();
        }

        if (includeNormalMagic && text.Contains("普通魔法", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenNormalMagicKeywordPower>();
        }

        if (text.Contains("咏唱", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenChantKeywordPower>();
        }

        if (text.Contains("回路", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenCircuitKeywordPower>();
        }

        if (text.Contains("解析", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenAnalysisPower>();
        }

        if (text.Contains("看破", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenInsightPower>();
        }

        if (text.Contains("隐匿魔力", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenConcealedManaPower>();
        }

        if (text.Contains("解放", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenReleasePower>();
        }

        if (text.Contains("回忆", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenMemoryPower>();
        }

        if (text.Contains("抑制", StringComparison.Ordinal))
        {
            yield return HoverTipFactory.FromPower<FrierenSuppressionPower>();
        }
    }
}

public abstract class FrierenPower : PowerModel
{
    public override LocString Description
    {
        get
        {
            var description = base.Description;
            description.Add("Amount", Amount);
            return description;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips => FrierenKeywordTips.FromText(Description.GetFormattedText());
}

public sealed class FrierenSpellKeywordPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class FrierenNormalMagicKeywordPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class FrierenChantKeywordPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class FrierenCircuitKeywordPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class FrierenAnalysisPower : FrierenPower
{
    private readonly HashSet<CardModel> _insightConsumedByCards = [];

    public bool InsightTriggeredThisTurn { get; private set; }

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        _insightConsumedByCards.Clear();
        InsightTriggeredThisTurn = false;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Owner != target || dealer == null || result.UnblockedDamage <= 0 || cardSource is not FrierenCard { IsSpell: true })
        {
            return;
        }

        var lifeLoss = 2m;
        var usedInsight = false;
        var insight = target.GetPower<FrierenInsightPower>();
        var cardAlreadyUsedInsight = _insightConsumedByCards.Contains(cardSource);
        if (cardAlreadyUsedInsight || insight?.Amount > 0)
        {
            lifeLoss = 3m;
            usedInsight = true;
            InsightTriggeredThisTurn = true;
            if (!cardAlreadyUsedInsight)
            {
                _insightConsumedByCards.Add(cardSource);
                await PowerCmd.Decrement(insight!);
            }
        }

        SetAmount(Amount - 1);
        await CreatureCmd.Damage(choiceContext, target, lifeLoss, ValueProp.Unblockable | ValueProp.Unpowered, dealer, null);
        foreach (var demonKiller in dealer.Powers.OfType<FrierenDemonKillerPower>().ToList())
        {
            await demonKiller.TriggerFromAnalysisLoss(choiceContext, target, usedInsight, cardSource);
        }

        if (Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    public void ClearInsightConsumptionFor(CardModel card)
    {
        _insightConsumedByCards.Remove(card);
    }
}

public sealed class FrierenInsightPower : FrierenPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class FrierenConcealedManaPower : FrierenPower
{
    private bool _resolving;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await ResolveThreshold(applier, cardSource);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this)
        {
            await ResolveThreshold(applier, cardSource);
        }
    }

    private async Task ResolveThreshold(Creature? applier, CardModel? cardSource)
    {
        if (_resolving)
        {
            return;
        }

        _resolving = true;
        try
        {
            if (Amount > 19m)
            {
                SetAmount(19);
            }

            while (Amount >= 10m && (Owner.GetPower<FrierenReleasePower>()?.Amount ?? 0m) < 2m)
            {
                SetAmount((int)(Amount - 10m));
                await PowerCmd.Apply<FrierenReleasePower>(Owner, 1m, applier ?? Owner, cardSource);
            }

            if (Amount > 19m)
            {
                SetAmount(19);
            }

            if (Amount <= 0)
            {
                await PowerCmd.Remove(this);
            }
        }
        finally
        {
            _resolving = false;
        }
    }
}

public sealed class FrierenReleasePower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ClampToCap();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this)
        {
            ClampToCap();
        }

        return Task.CompletedTask;
    }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        return Amount > 0 && card is FrierenCard { IsSpell: true } ? playCount + 1 : playCount;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Amount <= 0 || cardPlay.PlayIndex != 0 || cardPlay.Card is not FrierenCard { IsSpell: true })
        {
            return;
        }

        if (Amount == 1)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            SetAmount(Amount - 1);
        }
    }

    private void ClampToCap()
    {
        if (Amount > 2m)
        {
            SetAmount(2);
        }
    }
}

public sealed class FrierenMemoryPower : FrierenPower
{
    private readonly HashSet<string> _rememberedNormalMagics = [];
    private bool _generatedSmallMagicRememberedThisTurn;
    private int _resolvedMilestones;
    private int _pendingMilestoneDraws;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _generatedSmallMagicRememberedThisTurn = false;
            if (_pendingMilestoneDraws > 0)
            {
                var draws = _pendingMilestoneDraws;
                _pendingMilestoneDraws = 0;
                await CardPileCmd.Draw(choiceContext, draws, player);
            }
        }
    }

    public static async Task<bool> RecordNormalMagic(PlayerChoiceContext choiceContext, Creature owner, FrierenCard card)
    {
        var existing = owner.GetPower<FrierenMemoryPower>();
        if (existing is { } memory && card.IsGeneratedSmallMagic && memory._generatedSmallMagicRememberedThisTurn)
        {
            return false;
        }

        if (existing == null)
        {
            await GainMemory(choiceContext, owner, 1m, card);
            memory = owner.GetPower<FrierenMemoryPower>();
            if (memory != null && card.IsGeneratedSmallMagic)
            {
                memory._generatedSmallMagicRememberedThisTurn = true;
            }
            else
            {
                memory?._rememberedNormalMagics.Add(card.Id.Entry);
            }

            await FrierenNormalMagicMasteryPower.TryGrantExtraMemory(choiceContext, owner, card);
            return true;
        }

        if (card.IsGeneratedSmallMagic)
        {
            existing._generatedSmallMagicRememberedThisTurn = true;
            await GainMemory(choiceContext, owner, 1m, card);
            await FrierenNormalMagicMasteryPower.TryGrantExtraMemory(choiceContext, owner, card);
            return true;
        }

        if (existing._rememberedNormalMagics.Add(card.Id.Entry))
        {
            await GainMemory(choiceContext, owner, 1m, card);
            await FrierenNormalMagicMasteryPower.TryGrantExtraMemory(choiceContext, owner, card);
            return true;
        }

        return false;
    }

    public static async Task GainMemory(PlayerChoiceContext? choiceContext, Creature owner, decimal amount, CardModel? source)
    {
        await PowerCmd.Apply<FrierenMemoryPower>(owner, amount, owner, source);
        var memory = owner.GetPower<FrierenMemoryPower>();
        if (memory != null)
        {
            await memory.ResolveMilestones(choiceContext, source);
        }
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this && amount > 0)
        {
            await ResolveMilestones(null, cardSource);
        }
    }

    private async Task ResolveMilestones(PlayerChoiceContext? choiceContext, CardModel? source)
    {
        var targetMilestones = (int)Math.Floor(Amount / 3m);
        while (_resolvedMilestones < targetMilestones)
        {
            _resolvedMilestones++;
            var rewardIndex = (_resolvedMilestones - 1) % 3;
            switch (rewardIndex)
            {
                case 0:
                    if (choiceContext != null && Owner.Player != null)
                    {
                        await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
                    }
                    else
                    {
                        _pendingMilestoneDraws++;
                    }
                    break;
                case 1:
                    await CreatureCmd.GainBlock(Owner, 7m, ValueProp.Unpowered, null);
                    break;
                default:
                    await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, 3m, Owner, source);
                    break;
            }

            if (FrierenRelicOwnership.HasRelic(Owner.Player, typeof(SmallSuitcase)))
            {
                await CreatureCmd.GainBlock(Owner, 2m, ValueProp.Unpowered, null);
            }

            if (Owner.GetPower<FrierenMillenniumResearchPower>() != null && choiceContext != null && Owner.Player != null)
            {
                await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
            }
        }
    }
}

public sealed class FrierenSuppressionPower : FrierenPower
{
    private bool _playedAttackThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ClampToCap();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this)
        {
            ClampToCap();
        }

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _playedAttackThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Type == CardType.Attack)
        {
            _playedAttackThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && !_playedAttackThisTurn)
        {
            await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, Amount, Owner, null);
        }
    }

    private void ClampToCap()
    {
        if (Amount > 5m)
        {
            SetAmount(5);
        }
    }
}

public sealed class FrierenQuietStepsPower : FrierenPower
{
    private bool _playedAttackThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Type == CardType.Attack)
        {
            _playedAttackThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
        {
            return;
        }

        if (!_playedAttackThisTurn)
        {
            await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, Amount, Owner, null);
        }

        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenDelayedReleasePower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PowerCmd.Apply<FrierenReleasePower>(Owner, Amount, Owner, null);
        await CardPileCmd.Draw(choiceContext, 1m, player);
        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenGreatMagePower : FrierenPower
{
    private bool _appliedFirstCircuitInsight;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCombatStart()
    {
        _appliedFirstCircuitInsight = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, Amount, Owner, null);
        var combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        var enemy = Owner.Player?.RunState.Rng.CombatCardSelection.NextItem(combatState.HittableEnemies);
        if (enemy != null)
        {
            var analysis = Math.Max(1m, Amount - 1m);
            await PowerCmd.Apply<FrierenAnalysisPower>(enemy, analysis, Owner, null);
        }
    }

    public async Task OnCircuitTriggered()
    {
        if (_appliedFirstCircuitInsight)
        {
            return;
        }

        _appliedFirstCircuitInsight = true;
        var enemy = Owner.CombatState?.HittableEnemies
            .OrderByDescending(enemy => enemy.GetPower<FrierenAnalysisPower>()?.Amount ?? 0m)
            .FirstOrDefault();
        if (enemy != null)
        {
            await PowerCmd.Apply<FrierenInsightPower>(enemy, 1m, Owner, null);
        }
    }
}

public sealed class FrierenDelayedEnergyPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PlayerCmd.GainEnergy(Amount, player);
        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenDelayedBlockEnergyPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        await PlayerCmd.GainEnergy(1m, player);
        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenDelayedBlockPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenNoAttackThisTurnPower : FrierenPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card.Owner?.Creature != Owner)
        {
            return true;
        }

        return card.Type != CardType.Attack;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }
}

public sealed class FrierenCombatTrackerPower : FrierenPower
{
    private readonly List<CardModel> _currentCircuitCards = [];
    private decimal _currentCircuitCost;

    public CardType LastPlayedCardType { get; private set; } = CardType.None;
    public bool LastPlayedWasNormalMagic { get; private set; }
    public bool HasPlayedKillingMagic { get; private set; }
    public decimal ReleaseGainedThisCombat { get; private set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
    public override bool ShouldPlayVfx => false;

    public override Task BeforeCombatStart()
    {
        LastPlayedCardType = CardType.None;
        LastPlayedWasNormalMagic = false;
        HasPlayedKillingMagic = false;
        ReleaseGainedThisCombat = 0m;
        _currentCircuitCards.Clear();
        _currentCircuitCost = 0m;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _currentCircuitCards.Clear();
            _currentCircuitCost = 0m;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature != Owner)
        {
            return;
        }

        if (IsKillingMagic(cardPlay.Card))
        {
            HasPlayedKillingMagic = true;
        }

        if (cardPlay.Card is FrierenCard { IsSpell: true } && cardPlay.PlayIndex == 0)
        {
            _currentCircuitCards.Add(cardPlay.Card);
            _currentCircuitCost += cardPlay.Resources.EnergySpent;
            if (_currentCircuitCards.Count >= 3)
            {
                var cost = _currentCircuitCost;
                var distinctNames = _currentCircuitCards.Select(card => card.Id.Entry).Distinct().Count() == _currentCircuitCards.Count;
                _currentCircuitCards.Clear();
                _currentCircuitCost = 0m;
                await TriggerCircuit(context, cost, distinctNames);
            }
        }

        foreach (var enemy in Owner.CombatState?.HittableEnemies ?? Enumerable.Empty<Creature>())
        {
            enemy.GetPower<FrierenAnalysisPower>()?.ClearInsightConsumptionFor(cardPlay.Card);
        }

        LastPlayedCardType = cardPlay.Card.Type;
        LastPlayedWasNormalMagic = cardPlay.Card is FrierenCard { IsNormalMagic: true };
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount > 0 && power is FrierenReleasePower && power.Owner == Owner)
        {
            ReleaseGainedThisCombat += amount;
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner?.Creature != Owner || card is not FinalKillingMagic || ReleaseGainedThisCombat <= 0m)
        {
            return false;
        }

        modifiedCost = Math.Max(0m, originalCost - ReleaseGainedThisCombat);
        return modifiedCost != originalCost;
    }

    private static bool IsKillingMagic(CardModel card)
    {
        return card is BasicKillingMagic
            or TemporaryBasicKillingMagic
            or KillingMagic
            or OldKillingMagic
            or KillingMagicBarrage
            or CopyKillingMagic
            or SlayerKillingMagic
            or FullPowerKillingMagic
            or FinalKillingMagic;
    }

    private async Task TriggerCircuit(PlayerChoiceContext context, decimal cost, bool distinctNames)
    {
        foreach (var rhythm in Owner.Powers.OfType<FrierenMasterApprenticeRhythmPower>().ToList())
        {
            await rhythm.OnCircuitTriggered(context, cost);
        }

        foreach (var encyclopedia in Owner.Powers.OfType<FrierenMagicEncyclopediaPower>().ToList())
        {
            await encyclopedia.OnCircuitTriggered(context, distinctNames);
        }

        foreach (var greatMage in Owner.Powers.OfType<FrierenGreatMagePower>().ToList())
        {
            await greatMage.OnCircuitTriggered();
        }
    }
}

public sealed class FrierenNextAttackDiscountPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner?.Creature != Owner || card.Type != CardType.Attack || card.Pile?.Type is not (PileType.Hand or PileType.Play))
        {
            return false;
        }

        modifiedCost = Math.Max(0m, originalCost - 1m);
        return modifiedCost != originalCost;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Type == CardType.Attack && cardPlay.Card.Pile?.Type is PileType.Hand or PileType.Play)
        {
            await PowerCmd.Decrement(this);
        }
    }
}

public sealed class FrierenTemporaryStrengthDownPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<DispelMagic>();
    protected override bool IsPositive => false;
}

public sealed class FrierenDelayedConcealedManaPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, Amount, Owner, null);
        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenBarrierInversionPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
        {
            return;
        }

        var shouldGainMana = Owner.Block > 0;
        await PowerCmd.Remove(this);
        if (shouldGainMana)
        {
            await PowerCmd.Apply<FrierenDelayedBarrierInversionRewardPower>(Owner, Amount, Owner, null);
        }
    }
}

public sealed class FrierenDelayedBarrierInversionRewardPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, Amount, Owner, null);
        if (Owner.CombatState != null)
        {
            foreach (var enemy in Owner.CombatState.HittableEnemies)
            {
                await PowerCmd.Apply<FrierenAnalysisPower>(enemy, 2m, Owner, null);
            }
        }

        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenLongJourneyPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        var memory = Owner.GetPower<FrierenMemoryPower>()?.Amount ?? 0m;
        var drawCount = Math.Min(Amount, memory);
        if (drawCount > 0m)
        {
            await CardPileCmd.Draw(choiceContext, drawCount, player);
        }

        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenFlowerFieldHealPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        await CreatureCmd.Heal(Owner, Amount);
        await PowerCmd.Remove(this);
    }
}

public sealed class FrierenManaConcealmentPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        var player = Owner.Player;
        var playerCombatState = player?.PlayerCombatState;
        if (side != Owner.Side || playerCombatState == null || playerCombatState.Energy <= 0)
        {
            return;
        }

        var unusedEnergy = Math.Min(Amount, playerCombatState.Energy);
        if (unusedEnergy > 0m)
        {
            await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, unusedEnergy, Owner, null);
        }
    }
}

public sealed class FrierenGrimoireCollectorPower : FrierenPower
{
    private int _normalMagicPlayedThisTurn;
    private decimal _memoryAmountBeforeCard;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _normalMagicPlayedThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card is FrierenCard { IsNormalMagic: true })
        {
            _memoryAmountBeforeCard = Owner.GetPower<FrierenMemoryPower>()?.Amount ?? 0m;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature != Owner || cardPlay.Card is not FrierenCard { IsNormalMagic: true })
        {
            return;
        }

        _normalMagicPlayedThisTurn++;
        if (_normalMagicPlayedThisTurn <= Amount)
        {
            await CardPileCmd.Draw(context, 1m, Owner.Player!);
            var memoryAmountAfterCard = Owner.GetPower<FrierenMemoryPower>()?.Amount ?? 0m;
            if (memoryAmountAfterCard > _memoryAmountBeforeCard)
            {
                await CreatureCmd.GainBlock(Owner, 3m, ValueProp.Unpowered, cardPlay);
            }
        }
    }
}

public sealed class FrierenCombatAnalysisPower : FrierenPower
{
    private readonly HashSet<Creature> _reachedTenThisCombat = [];
    private bool _applyingBonus;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCombatStart()
    {
        _reachedTenThisCombat.Clear();
        _applyingBonus = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0 || power is not FrierenAnalysisPower || applier != Owner)
        {
            return;
        }

        var before = power.Amount - amount;
        if (!_applyingBonus && before > 0m)
        {
            _applyingBonus = true;
            await PowerCmd.Apply<FrierenAnalysisPower>(power.Owner, Amount, Owner, cardSource);
            _applyingBonus = false;
        }

        if (before < 10m && power.Amount >= 10m && _reachedTenThisCombat.Add(power.Owner))
        {
            await PowerCmd.Apply<FrierenInsightPower>(power.Owner, 1m, Owner, cardSource);
        }
    }
}

public sealed class FrierenEfficientChantPower : FrierenPower
{
    private int _discountsUsedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _discountsUsedThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner?.Creature != Owner || card is not FrierenCard { IsSpell: true } || _discountsUsedThisTurn >= Amount || !HasAnalyzedEnemy())
        {
            return false;
        }

        modifiedCost = Math.Max(0, originalCost - 1);
        return true;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card is FrierenCard { IsSpell: true } && HasAnalyzedEnemy() && _discountsUsedThisTurn < Amount)
        {
            _discountsUsedThisTurn++;
        }

        return Task.CompletedTask;
    }

    private bool HasAnalyzedEnemy()
    {
        return Owner.CombatState?.HittableEnemies.Any(enemy => enemy.GetPower<FrierenAnalysisPower>()?.Amount > 0) == true;
    }
}

public sealed class FrierenHumanMagicEraPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        for (var i = 0; i < Amount; i++)
        {
            var card = combatState.CreateCard<TemporaryBasicKillingMagic>(player);
            card.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
            card.ExhaustOnNextPlay = true;
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner.Player);
        }
    }
}

public sealed class FrierenDisciplinePower : FrierenPower
{
    private bool _usedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _usedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (_usedThisTurn || cardPlay.Card.Owner?.Creature != Owner || cardPlay.Resources.EnergySpent > 0)
        {
            return;
        }

        _usedThisTurn = true;
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, cardPlay);
        if (cardPlay.Card is FrierenCard { IsSpell: true })
        {
            await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, 1m, Owner, cardPlay.Card);
        }
    }
}

public sealed class FrierenConcealedManaReservePower : FrierenPower
{
    private int _triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _triggeredThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount != 0m && power is FrierenReleasePower && power.Owner == Owner && _triggeredThisTurn < 2)
        {
            _triggeredThisTurn++;
            await PlayerCmd.GainEnergy(Amount, Owner.Player!);
        }
    }
}

public sealed class FrierenMasterApprenticeRhythmPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnCircuitTriggered(PlayerChoiceContext context, decimal cost)
    {
        await CardPileCmd.Draw(context, 1m, Owner.Player!);
        var threshold = Amount > 1m ? 1m : 0m;
        if (cost <= threshold)
        {
            await CardPileCmd.Draw(context, 1m, Owner.Player!);
        }
    }
}

public sealed class FrierenMillenniumComposurePower : FrierenPower
{
    private bool _playedAttackLastTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (!_playedAttackLastTurn)
        {
            await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, Amount, Owner, null);
        }

        _playedAttackLastTurn = false;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Type == CardType.Attack)
        {
            _playedAttackLastTurn = true;
        }

        return Task.CompletedTask;
    }
}

public sealed class FrierenSerieTeachingPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0 || power is not FrierenMemoryPower || power.Owner != Owner)
        {
            return;
        }

        await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, 1m, Owner, cardSource);
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, null);
        if (Math.Floor(power.Amount / 3m) > Math.Floor((power.Amount - amount) / 3m)
            && Owner.CombatState != null)
        {
            foreach (var enemy in Owner.CombatState.HittableEnemies)
            {
                await PowerCmd.Apply<FrierenInsightPower>(enemy, 1m, Owner, cardSource);
            }
        }
    }
}

public sealed class FrierenHimmelMemoryPower : FrierenPower
{
    private bool _used;
    private bool _pendingDraw;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCombatStart()
    {
        _used = false;
        _pendingDraw = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !_pendingDraw)
        {
            return;
        }

        _pendingDraw = false;
        await CardPileCmd.Draw(choiceContext, 2m, player);
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (_used || creature != Owner || Owner.GetHpPercentRemaining() >= 0.5)
        {
            return;
        }

        _used = true;
        await PowerCmd.Apply<FrierenReleasePower>(Owner, 1m, Owner, null);
        _pendingDraw = true;
    }
}

public sealed class FrierenFlammeTeachingPower : FrierenPower
{
    private bool _insightApplied;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCombatStart()
    {
        _insightApplied = false;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Type == CardType.Power)
        {
            await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, Amount, Owner, cardPlay.Card);
        }
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (_insightApplied || amount <= 0m || power is not FrierenReleasePower || power.Owner != Owner || Owner.CombatState == null)
        {
            return;
        }

        _insightApplied = true;
        foreach (var enemy in Owner.CombatState.HittableEnemies)
        {
            await PowerCmd.Apply<FrierenInsightPower>(enemy, 1m, Owner, cardSource);
        }
    }
}

public sealed class FrierenMagicEncyclopediaPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnCircuitTriggered(PlayerChoiceContext context, bool distinctNames)
    {
        await CardPileCmd.Draw(context, 1m, Owner.Player!);
        if (distinctNames)
        {
            await PlayerCmd.GainEnergy(1m, Owner.Player!);
        }
    }
}

public sealed class FrierenLimitedManaAuraPower : FrierenPower
{
    private bool _usedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _usedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (_usedThisTurn || amount <= 0 || power is not FrierenConcealedManaPower || power.Owner != Owner)
        {
            return;
        }

        _usedThisTurn = true;
        if (Owner.CombatState == null)
        {
            return;
        }

        foreach (var enemy in Owner.CombatState.HittableEnemies)
        {
            await PowerCmd.Apply<FrierenAnalysisPower>(enemy, Amount, Owner, cardSource);
        }
    }
}

public sealed class FrierenDemonKillerPower : FrierenPower
{
    private readonly HashSet<Creature> _triggeredThisTurn = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _triggeredThisTurn.Clear();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        return Task.CompletedTask;
    }

    public async Task TriggerFromAnalysisLoss(PlayerChoiceContext choiceContext, Creature creature, bool usedInsight, CardModel? source)
    {
        if (creature.Side == Owner.Side || !_triggeredThisTurn.Add(creature))
        {
            return;
        }

        await PowerCmd.Apply<VulnerablePower>(creature, Amount, Owner, source);
        if (usedInsight || creature.GetPower<FrierenInsightPower>()?.Amount > 0m)
        {
            await CreatureCmd.Damage(choiceContext, creature, 3m, ValueProp.Unblockable | ValueProp.Unpowered, Owner, source);
        }
    }
}

public sealed class FrierenNormalMagicMasteryPower : FrierenPower
{
    private int _freeNormalMagicsThisTurn;
    private int _extraMemoriesThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _freeNormalMagicsThisTurn = 0;
            _extraMemoriesThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner?.Creature != Owner || card is not FrierenCard { IsNormalMagic: true } || _freeNormalMagicsThisTurn >= Amount)
        {
            return false;
        }

        modifiedCost = 0;
        return true;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card is FrierenCard { IsNormalMagic: true } && _freeNormalMagicsThisTurn < Amount)
        {
            _freeNormalMagicsThisTurn++;
        }

        return Task.CompletedTask;
    }

    public static async Task TryGrantExtraMemory(PlayerChoiceContext choiceContext, Creature owner, CardModel source)
    {
        var power = owner.GetPower<FrierenNormalMagicMasteryPower>();
        if (power == null || power._extraMemoriesThisTurn >= 1)
        {
            return;
        }

        power._extraMemoriesThisTurn++;
        await FrierenMemoryPower.GainMemory(choiceContext, owner, 1m, source);
    }
}

public sealed class FrierenMillenniumResearchPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        var candidates = PileType.Hand.GetPile(player).Cards.Where(card => card.IsUpgradable).ToList();
        var upgradeCount = Math.Min((int)Amount, candidates.Count);
        for (var i = 0; i < upgradeCount; i++)
        {
            var card = player.RunState.Rng.CombatCardSelection.NextItem(candidates)!;
            candidates.Remove(card);
            CardCmd.Upgrade(card);
        }

        await FrierenMemoryPower.GainMemory(choiceContext, Owner, 1m, null);
    }
}

public sealed class FrierenHugeManaPower : FrierenPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            await PowerCmd.Apply<FrierenConcealedManaPower>(Owner, Amount, Owner, null);
        }
    }
}

public sealed class FrierenHeroesPartyMagicPower : FrierenPower
{
    private bool _playedAttack;
    private bool _playedSkill;
    private bool _playedPower;
    private bool _qualifiedLastTurn;
    private bool _memoryGranted;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCombatStart()
    {
        _memoryGranted = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (_qualifiedLastTurn)
        {
            await PowerCmd.Apply<FrierenReleasePower>(Owner, Amount, Owner, null);
            if (!_memoryGranted)
            {
                _memoryGranted = true;
                await FrierenMemoryPower.GainMemory(choiceContext, Owner, 1m, null);
            }
        }

        _qualifiedLastTurn = false;
        _playedAttack = false;
        _playedSkill = false;
        _playedPower = false;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        _playedAttack |= cardPlay.Card.Type == CardType.Attack;
        _playedSkill |= cardPlay.Card.Type == CardType.Skill;
        _playedPower |= cardPlay.Card.Type == CardType.Power;
        _qualifiedLastTurn = _playedAttack && _playedSkill && _playedPower;
        return Task.CompletedTask;
    }
}

public sealed class FrierenFlowerFieldPotionPower : FrierenPower
{
    private readonly HashSet<string> _normalMagics = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCombatStart()
    {
        _normalMagics.Clear();
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card is FrierenCard { IsNormalMagic: true } && _normalMagics.Add(cardPlay.Card.Id.Entry))
        {
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, cardPlay);
        }
    }
}
