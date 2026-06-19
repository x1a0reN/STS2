using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace GongdouSts2ChallengeMod.Cards;

public abstract class GongdouChallengeCard : CardModel
{
    private static readonly Dictionary<string, (string Pool, string AtlasId)> PortraitAtlasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["strike_ironclad"] = ("ironclad", "strike_ironclad"),
        ["defend_ironclad"] = ("ironclad", "defend_ironclad"),
        ["bash"] = ("ironclad", "bash"),
        ["perfected_strike"] = ("ironclad", "perfected_strike"),
        ["body_slam"] = ("ironclad", "body_slam"),
        ["iron_wave"] = ("ironclad", "iron_wave"),
        ["sword_boomerang"] = ("ironclad", "sword_boomerang"),
        ["uppercut"] = ("ironclad", "uppercut"),
        ["bludgeon"] = ("ironclad", "bludgeon"),
        ["twin_strike"] = ("ironclad", "twin_strike"),
        ["hemokinesis"] = ("ironclad", "hemokinesis"),
        ["inflame"] = ("ironclad", "inflame"),
        ["burning_pact"] = ("ironclad", "burning_pact"),
        ["true_grit"] = ("ironclad", "true_grit"),
        ["offering"] = ("ironclad", "offering"),

        ["neutralize"] = ("silent", "neutralize"),
        ["survivor"] = ("silent", "survivor"),
        ["dagger_throw"] = ("silent", "dagger_throw"),
        ["dagger_spray"] = ("silent", "dagger_spray"),
        ["prepared"] = ("silent", "prepared"),
        ["finisher"] = ("silent", "finisher"),
        ["backstab"] = ("silent", "backstab"),
        ["slice"] = ("silent", "slice"),
        ["skewer"] = ("silent", "skewer"),
        ["deadly_poison"] = ("silent", "deadly_poison"),
        ["poisoned_stab"] = ("silent", "poisoned_stab"),
        ["noxious_fumes"] = ("silent", "noxious_fumes"),
        ["bouncing_flask"] = ("silent", "bouncing_flask"),
        ["leg_sweep"] = ("silent", "leg_sweep"),
        ["cloak_and_dagger"] = ("silent", "cloak_and_dagger"),
        ["backflip"] = ("silent", "backflip"),
        ["caltrops"] = ("silent", "noxious_fumes"),
        ["predator"] = ("silent", "predator"),
        ["bane"] = ("silent", "poisoned_stab"),
        ["piercing_wail"] = ("silent", "piercing_wail"),

        ["ball_lightning"] = ("defect", "ball_lightning"),
        ["cold_snap"] = ("defect", "cold_snap"),
        ["coolheaded"] = ("defect", "coolheaded"),
        ["zap"] = ("defect", "zap"),
        ["dualcast"] = ("defect", "dualcast"),
        ["darkness"] = ("defect", "darkness"),
        ["loop"] = ("defect", "loop"),
        ["chill"] = ("defect", "chill"),
        ["leap"] = ("defect", "leap"),
        ["charge_battery"] = ("defect", "charge_battery"),
        ["beam_cell"] = ("defect", "beam_cell"),
        ["rebound"] = ("defect", "beam_cell"),
        ["echo_form"] = ("defect", "echo_form"),
        ["meteor_strike"] = ("defect", "meteor_strike"),
        ["hyperbeam"] = ("defect", "hyperbeam"),

        ["smite"] = ("colorless", "smite"),

        ["venerate"] = ("regent", "venerate"),
        ["guards"] = ("regent", "guards"),
        ["guiding_star"] = ("regent", "guiding_star"),
        ["radiate"] = ("regent", "radiate"),
        ["seven_stars"] = ("regent", "seven_stars"),
        ["monarchs_gaze"] = ("regent", "monarchs_gaze"),
        ["refine_blade"] = ("regent", "refine_blade"),
        ["neutron_aegis"] = ("regent", "neutron_aegis"),
        ["hammer_time"] = ("regent", "hammer_time"),

        // STS2 currently ships localization or old design references for these names
        // without matching atlas entries. Keep the nearest shipped card art instead
        // of falling back to the ugly NOPE placeholder.
        ["quick_slash"] = ("silent", "slice"),
        ["clothesline"] = ("ironclad", "uppercut"),
        ["carnage"] = ("ironclad", "bludgeon"),
        ["reaper"] = ("ironclad", "hemokinesis"),
        ["recycle"] = ("defect", "charge_battery"),
        ["catalyst"] = ("silent", "deadly_poison"),
        ["eruption"] = ("ironclad", "bash"),
        ["vigilance"] = ("ironclad", "defend_ironclad"),
        ["empty_fist"] = ("colorless", "smite"),
        ["empty_body"] = ("ironclad", "defend_ironclad"),
        ["follow_up"] = ("silent", "slice"),
        ["wheel_kick"] = ("ironclad", "uppercut"),
        ["cut_through_fate"] = ("silent", "slice"),
        ["bowling_bash"] = ("ironclad", "body_slam"),
        ["protect"] = ("ironclad", "defend_ironclad"),
        ["halt"] = ("ironclad", "defend_ironclad"),
        ["devotion"] = ("regent", "venerate"),
        ["prostrate"] = ("regent", "guards"),
        ["pray"] = ("regent", "guiding_star"),
        ["worship"] = ("regent", "venerate"),
        ["brilliance"] = ("regent", "radiate"),
        ["ragnarok"] = ("regent", "seven_stars"),
        ["judgment"] = ("regent", "monarchs_gaze"),
        ["carve_reality"] = ("regent", "refine_blade"),
        ["sanctity"] = ("regent", "neutron_aegis"),
        ["wallop"] = ("regent", "hammer_time"),
        ["recursion"] = ("defect", "dualcast"),
        ["melter"] = ("defect", "beam_cell"),
        ["streamline"] = ("defect", "ball_lightning"),
        ["defend_blue"] = ("defect", "charge_battery")
    };

    protected GongdouChallengeCard(int energy, CardType type, CardRarity rarity, TargetType targetType)
        : base(energy, type, rarity, targetType)
    {
    }

    public override CardPoolModel Pool => ModelDb.CardPool<IroncladCardPool>();
    public override CardPoolModel VisualCardPool => ResolvePortraitPool().Pool switch
    {
        "silent" => ModelDb.CardPool<SilentCardPool>(),
        "defect" => ModelDb.CardPool<DefectCardPool>(),
        "colorless" => ModelDb.CardPool<ColorlessCardPool>(),
        "regent" => ModelDb.CardPool<RegentCardPool>(),
        _ => ModelDb.CardPool<IroncladCardPool>()
    };
    protected virtual string PortraitCardAtlasId => "strike_ironclad";

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"atlases/card_atlas.sprites/{ResolvePortraitPool().Pool}/{ResolvePortraitPool().AtlasId}.tres");

    public override string BetaPortraitPath => PortraitPath;

    private (string Pool, string AtlasId) ResolvePortraitPool()
    {
        return PortraitAtlasMap.TryGetValue(PortraitCardAtlasId, out var portrait)
            ? portrait
            : ("ironclad", PortraitCardAtlasId);
    }
}

public sealed class GongdouSkewer7 : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "skewer";
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move)];

    public GongdouSkewer7()
        : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(ResolveEnergyXValue())
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }
}

public sealed class GongdouCatalyst : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "deadly_poison";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public GongdouCatalyst()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var poison = cardPlay.Target.GetPower<PoisonPower>();
        if (poison == null || poison.Amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<PoisonPower>(cardPlay.Target, poison.Amount * 2m, Owner.Creature, this);
    }
}

public sealed class GongdouPreciseStab : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "poisoned_stab";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move)];

    public GongdouPreciseStab()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var damage = cardPlay.Target.GetPower<VulnerablePower>() != null ? 17m : DynamicVars.Damage.BaseValue;
        await DamageCmd.Attack(damage).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
    }
}

public sealed class GongdouHeavySwing : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "bludgeon";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(24m, ValueProp.Move)];

    public GongdouHeavySwing()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var attacksPlayed = CombatManager.Instance.History.CardPlaysFinished
            .Count(e => e.HappenedThisTurn(CombatState) && e.CardPlay.Card.Owner == Owner && e.CardPlay.Card.Type == CardType.Attack);
        var damage = Math.Max(0m, DynamicVars.Damage.BaseValue - attacksPlayed * 6m);
        await DamageCmd.Attack(damage).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
    }
}

public sealed class GongdouArmorBreakSlash : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "bash";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(18m, ValueProp.Move), new PowerVar<VulnerablePower>(1m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VulnerablePower>()];

    public GongdouArmorBreakSlash()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
    }
}

public sealed class GongdouDoubleHit : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "twin_strike";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    public GongdouDoubleHit()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
    }
}

public sealed class GongdouOpportunityStrike : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "slice";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    public GongdouOpportunityStrike()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var cardsPlayedThisTurn = CombatManager.Instance.History.CardPlaysFinished
            .Count(e => e.HappenedThisTurn(CombatState) && e.CardPlay.Card.Owner == Owner);
        var damage = cardsPlayedThisTurn == 0 ? 12m : 6m;
        await DamageCmd.Attack(damage).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
    }
}

public sealed class GongdouPoisonDagger : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "poisoned_stab";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move), new PowerVar<PoisonPower>(4m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

    public GongdouPoisonDagger()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
        await PowerCmd.Apply<PoisonPower>(cardPlay.Target, DynamicVars.Poison.BaseValue, Owner.Creature, this);
    }
}

public sealed class GongdouPoisonFog : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "noxious_fumes";

    public GongdouPoisonFog()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<GongdouPoisonFogPower>(Owner.Creature, 3m, Owner.Creature, this);
    }
}

public sealed class GongdouCorrosiveSalve : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "deadly_poison";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(10m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

    public GongdouCorrosiveSalve()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<PoisonPower>(cardPlay.Target, DynamicVars.Poison.BaseValue, Owner.Creature, this);
    }
}

public sealed class GongdouBattleCry : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "inflame";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(1m), new DynamicVar("Cards", 1m)];

    public GongdouBattleCry()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, this);
        await CardPileCmd.Draw(choiceContext, DynamicVars["Cards"].BaseValue, Owner);
        var card = (await CardSelectCmd.FromHandForDiscard(choiceContext, Owner, new CardSelectorPrefs(SelectionScreenPrompt, 0, 1), null, this)).FirstOrDefault();
        if (card != null)
        {
            await CardCmd.Discard(choiceContext, card);
        }
    }
}

public sealed class GongdouPreparedStance : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "prepared";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public GongdouPreparedStance()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 0, 1),
            c => !c.ShouldRetainThisTurn,
            this)).FirstOrDefault();
        card?.GiveSingleTurnRetain();
    }
}

public sealed class GongdouPoisonFogPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Enemy)
        {
            return;
        }

        foreach (var enemy in combatState.HittableEnemies)
        {
            await PowerCmd.Apply<PoisonPower>(enemy, Amount, Owner, null);
        }
    }
}

public sealed class GongdouBloodRush : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "hemokinesis";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(20m, ValueProp.Move), new HpLossVar(4m)];

    public GongdouBloodRush()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.HpLoss.BaseValue, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
    }
}

public sealed class GongdouBallLightning7NoOrb : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "ball_lightning";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move)];

    public GongdouBallLightning7NoOrb()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
    }
}

public sealed class GongdouQuickSlash8NoDraw : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "slice";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move)];

    public GongdouQuickSlash8NoDraw()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
    }
}

public sealed class GongdouDaggerThrow9NoDiscard : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "dagger_throw";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9m, ValueProp.Move)];

    public GongdouDaggerThrow9NoDiscard()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
    }
}

public sealed class GongdouClothesline12Weak2 : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "uppercut";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move), new PowerVar<WeakPower>(2m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<WeakPower>()];

    public GongdouClothesline12Weak2()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
        await PowerCmd.Apply<WeakPower>(cardPlay.Target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
    }
}

public sealed class GongdouBluff : GongdouChallengeCard
{
    protected override string PortraitCardAtlasId => "piercing_wail";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<VulnerablePower>(2m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VulnerablePower>()];

    public GongdouBluff()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<VulnerablePower>(cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<GongdouNextTurnBlockPower>(cardPlay.Target, 8m, Owner.Creature, this);
    }
}

public sealed class GongdouNextTurnBlockPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
            RemoveInternal();
        }
    }
}

public sealed class GongdouBurstPotionPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        var targets = cardPlay.Target != null && cardPlay.Target.Side != Owner.Side
            ? [cardPlay.Target]
            : cardPlay.Card.CombatState?.HittableEnemies.ToArray() ?? [];

        foreach (var target in targets)
        {
            await DamageCmd.Attack(Amount).FromCard(cardPlay.Card).Targeting(target).Execute(context);
        }

        RemoveInternal();
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side)
        {
            RemoveInternal();
        }

        return Task.CompletedTask;
    }
}
