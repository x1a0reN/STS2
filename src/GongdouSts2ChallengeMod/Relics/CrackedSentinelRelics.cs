using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace GongdouSts2ChallengeMod.Relics;

public abstract class GongdouChallengeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
}

public sealed class GongdouTacticalMemoClip : GongdouChallengeRelic
{
    protected override string IconBaseName => "bag_of_preparation";

    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<WellLaidPlansPower>(Owner.Creature, 1m, Owner.Creature, null);
    }
}

public sealed class GongdouCrackedBladeCharm : GongdouChallengeRelic
{
    protected override string IconBaseName => "akabeko";

    private bool _used;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return !_used && cardSource?.Type == CardType.Attack && dealer == Owner.Creature ? 8m : 0m;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (!_used && cardPlay.Card.Type == CardType.Attack)
        {
            _used = true;
            Flash();
        }

        return Task.CompletedTask;
    }
}

public sealed class GongdouPoisonSac : GongdouChallengeRelic
{
    protected override string IconBaseName => "twisted_funnel";

    private bool _used;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (_used || cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        _used = true;
        Flash();

        var targets = cardPlay.Target != null && cardPlay.Target.Side != Owner.Creature.Side
            ? [cardPlay.Target]
            : cardPlay.Card.CombatState?.HittableEnemies.ToArray() ?? [];

        foreach (var target in targets)
        {
            await PowerCmd.Apply<PoisonPower>(target, 3m, Owner.Creature, null);
        }
    }
}

public sealed class GongdouBloodSigil : GongdouChallengeRelic
{
    protected override string IconBaseName => "red_skull";

    private bool _pending;

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Owner.Creature && delta < 0)
        {
            _pending = true;
            Flash();
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return _pending && cardSource?.Type == CardType.Attack && dealer == Owner.Creature ? 4m : 0m;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (_pending && cardPlay.Card.Type == CardType.Attack)
        {
            _pending = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Creature.Side)
        {
            _pending = false;
        }

        return Task.CompletedTask;
    }
}

public sealed class GongdouOldWhetstone : GongdouChallengeRelic
{
    protected override string IconBaseName => "whetstone";

    private bool _usedThisTurn;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            _usedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_usedThisTurn || cardSource?.Type != CardType.Attack || dealer != Owner.Creature)
        {
            return 0m;
        }

        if (!cardSource.EnergyCost.CostsX && cardSource.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1)
        {
            return 3m;
        }

        return 0m;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (!_usedThisTurn && cardPlay.Card.Type == CardType.Attack && !cardPlay.Card.EnergyCost.CostsX &&
            cardPlay.Card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1)
        {
            _usedThisTurn = true;
            Flash();
        }

        return Task.CompletedTask;
    }
}

public sealed class GongdouSlowWarmCore : GongdouChallengeRelic
{
    protected override string IconBaseName => "happy_flower";

    private bool _used;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.Creature.CombatState;
        if (_used || player != Owner || combatState == null || combatState.RoundNumber < 3)
        {
            return;
        }

        _used = true;
        Flash();
        await PowerCmd.Apply<StrengthPower>(Owner.Creature, 1m, Owner.Creature, null);
        await PlayerCmd.GainEnergy(1m, Owner);
    }
}
