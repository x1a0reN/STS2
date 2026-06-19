using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;
using GongdouSts2ChallengeMod.Challenges;

namespace GongdouSts2ChallengeMod.Monsters;

public abstract class GongdouAxebotRulePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;
}

public abstract class GongdouKeywordPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => true;
}

public sealed class GongdouThinDeckRulePower : GongdouAxebotRulePower;

public sealed class GongdouCrackedArmorRulePower : GongdouAxebotRulePower;

public sealed class GongdouMaintenanceRulePower : GongdouAxebotRulePower;

public sealed class GongdouPersistentArmorRulePower : GongdouAxebotRulePower
{
    public override bool ShouldClearBlock(Creature creature)
    {
        return creature != Owner;
    }
}

public sealed class GongdouStageRulePower : GongdouAxebotRulePower
{
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return GongdouPuzzleRuntime.ShouldPlayCard(card, Owner, autoPlayType);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (combatState is CombatState state)
        {
            await GongdouPuzzleRuntime.AfterSideTurnStart(side, state);
        }
    }

    public override async Task AfterSideTurnStartLate(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (combatState is CombatState state)
        {
            await GongdouPuzzleRuntime.AfterSideTurnStartLate(side, state);
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        return GongdouPuzzleRuntime.AfterCardPlayed(context, cardPlay, Owner);
    }

    public override Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        return GongdouPuzzleRuntime.AfterCardDrawn(context, card, Owner);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return GongdouPuzzleRuntime.ModifyDamageMultiplicative(Owner, target, amount, props, dealer, cardSource);
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return GongdouPuzzleRuntime.ModifyDamageAdditive(Owner, target, amount, props, dealer, cardSource);
    }

    public override decimal ModifyOrbValue(OrbModel orb, decimal value)
    {
        return GongdouPuzzleRuntime.ModifyD8OrbValue(orb.Owner, value);
    }

    public override int ModifyOrbPassiveTriggerCounts(OrbModel orb, int triggerCount)
    {
        return GongdouPuzzleRuntime.ModifyD8OrbPassiveTriggerCounts(orb, triggerCount);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        await GongdouPuzzleRuntime.AfterTurnEnd(choiceContext, side, Owner);
    }
}

public sealed class GongdouPersistentArmorKeywordPower : GongdouKeywordPower;
public sealed class GongdouD3DexterityKeywordPower : GongdouKeywordPower;
public sealed class GongdouD3BellLimitKeywordPower : GongdouKeywordPower;
public sealed class GongdouD3StickyHandKeywordPower : GongdouKeywordPower;
public sealed class GongdouD3FalseBladeKeywordPower : GongdouKeywordPower;
public sealed class GongdouD3ReadKeywordPower : GongdouKeywordPower;
public sealed class GongdouD3WrongDiscardArmorKeywordPower : GongdouKeywordPower;
public sealed class GongdouD3ChainBreachKeywordPower : GongdouKeywordPower;
public sealed class GongdouD3NoDexterityArmorKeywordPower : GongdouKeywordPower;
public sealed class GongdouD4BurnKeywordPower : GongdouKeywordPower;
public sealed class GongdouD4OrderKeywordPower : GongdouKeywordPower;
public sealed class GongdouD4CountdownKeywordPower : GongdouKeywordPower;
public sealed class GongdouD4OverheatKeywordPower : GongdouKeywordPower;
public sealed class GongdouD4FireBreathingPower : GongdouKeywordPower;
public sealed class GongdouD4EvolvePower : GongdouKeywordPower;
public sealed class GongdouD5OverloadLockKeywordPower : GongdouKeywordPower;
public sealed class GongdouD5VoidKeywordPower : GongdouKeywordPower;
public sealed class GongdouD5VoidCollapseKeywordPower : GongdouKeywordPower;
public sealed class GongdouD5ArtifactKeywordPower : GongdouKeywordPower;
public sealed class GongdouD5PotionPhaseKeywordPower : GongdouKeywordPower;
public sealed class GongdouD5PotionPhaseTurn3KeywordPower : GongdouKeywordPower;
public sealed class GongdouD5PotionPhaseTurn4KeywordPower : GongdouKeywordPower;
public sealed class GongdouD5PotionPhaseTurn5KeywordPower : GongdouKeywordPower;
public sealed class GongdouD6StanceKeywordPower : GongdouKeywordPower
{
    public override LocString Title => new("powers", StanceLocKey + ".title");
    public override LocString Description => new("powers", StanceLocKey + ".description");
    public override int DisplayAmount => 0;

    private string StanceLocKey => Amount switch
    {
        2 => nameof(GongdouD6CalmKeywordPower),
        3 => nameof(GongdouD6WrathKeywordPower),
        _ => nameof(GongdouD6NormalKeywordPower)
    };
}
public sealed class GongdouD6NormalKeywordPower : GongdouKeywordPower;
public sealed class GongdouD6WrathKeywordPower : GongdouKeywordPower;
public sealed class GongdouD6CalmKeywordPower : GongdouKeywordPower;
public sealed class GongdouD6CalmBreachKeywordPower : GongdouKeywordPower;
public sealed class GongdouD7PoisonKeywordPower : GongdouKeywordPower;
public sealed class GongdouD7PoisonFogKeywordPower : GongdouKeywordPower;
public sealed class GongdouD7CaltropsKeywordPower : GongdouKeywordPower;
public sealed class GongdouD7AntidoteKeywordPower : GongdouKeywordPower;
public sealed class GongdouD8OrbSlotKeywordPower : GongdouKeywordPower;
public sealed class GongdouD8LightningKeywordPower : GongdouKeywordPower;
public sealed class GongdouD8FrostKeywordPower : GongdouKeywordPower;
public sealed class GongdouD8DarkKeywordPower : GongdouKeywordPower;
public sealed class GongdouD8LoopKeywordPower : GongdouKeywordPower;
public sealed class GongdouD8FocusKeywordPower : GongdouKeywordPower;
public sealed class GongdouD8InsulationKeywordPower : GongdouKeywordPower;
public sealed class GongdouD9MirrorDrawKeywordPower : GongdouKeywordPower;
public sealed class GongdouD9MantraKeywordPower : GongdouKeywordPower;
public sealed class GongdouD9DivinityKeywordPower : GongdouKeywordPower;
public sealed class GongdouD9ExhaustKeywordPower : GongdouKeywordPower;
public sealed class GongdouD9ArmorReflectKeywordPower : GongdouKeywordPower;
public sealed class GongdouD9EnemyArmorKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10RiftDrawKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10ChargeKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10EchoKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10OverheatKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10PhaseGateKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10MarkKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10DelayedDamageKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10EnemyArmorKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10MarkDecayKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10ArmorChargeKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10ResonatorKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10PrismShardKeywordPower : GongdouKeywordPower;
public sealed class GongdouD10MarkResonanceKeywordPower : GongdouKeywordPower;
