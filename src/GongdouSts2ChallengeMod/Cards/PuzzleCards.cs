using GongdouSts2ChallengeMod.Challenges;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace GongdouSts2ChallengeMod.Cards;

public abstract class GongdouFixedPuzzleCard : GongdouChallengeCard
{
    private readonly string _portrait;
    private readonly decimal _damage;
    private readonly int _hits;
    private readonly decimal _block;
    private readonly decimal _vulnerable;
    private readonly decimal _weak;
    private readonly decimal _poison;
    private readonly decimal _strength;
    private readonly bool _exhaust;
    private readonly bool _sly;

    protected GongdouFixedPuzzleCard(
        int energy,
        CardType type,
        TargetType targetType,
        string portrait,
        decimal damage = 0m,
        int hits = 1,
        decimal block = 0m,
        decimal vulnerable = 0m,
        decimal weak = 0m,
        decimal poison = 0m,
        decimal strength = 0m,
        bool exhaust = false,
        bool sly = false)
        : base(energy, type, CardRarity.Common, targetType)
    {
        _portrait = portrait;
        _damage = damage;
        _hits = Math.Max(1, hits);
        _block = block;
        _vulnerable = vulnerable;
        _weak = weak;
        _poison = poison;
        _strength = strength;
        _exhaust = exhaust;
        _sly = sly;
    }

    protected override string PortraitCardAtlasId => _portrait;

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            if (_sly)
            {
                yield return CardKeyword.Sly;
            }

            if (_exhaust)
            {
                yield return CardKeyword.Exhaust;
            }
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var vars = new List<DynamicVar>();
            if (_damage > 0)
            {
                vars.Add(new DamageVar(_damage, ValueProp.Move));
            }

            if (_block > 0)
            {
                vars.Add(new BlockVar(_block, ValueProp.Move));
            }

            if (_vulnerable > 0)
            {
                vars.Add(new PowerVar<VulnerablePower>(_vulnerable));
            }

            if (_weak > 0)
            {
                vars.Add(new PowerVar<WeakPower>(_weak));
            }

            if (_poison > 0)
            {
                vars.Add(new PowerVar<PoisonPower>(_poison));
            }

            if (_strength > 0)
            {
                vars.Add(new PowerVar<StrengthPower>(_strength));
            }

            return vars;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var tips = new List<IHoverTip>();
            if (_vulnerable > 0)
            {
                tips.Add(HoverTipFactory.FromPower<VulnerablePower>());
            }

            if (_weak > 0)
            {
                tips.Add(HoverTipFactory.FromPower<WeakPower>());
            }

            if (_poison > 0)
            {
                tips.Add(HoverTipFactory.FromPower<PoisonPower>());
            }

            if (_strength > 0)
            {
                tips.Add(HoverTipFactory.FromPower<StrengthPower>());
            }

            return tips;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (await GongdouPuzzleRuntime.TryPlayCardAsync(this, choiceContext, cardPlay))
        {
            return;
        }

        if (_block > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature, _block, ValueProp.Unpowered, null);
        }

        if (_strength > 0)
        {
            await PowerCmd.Apply<StrengthPower>(Owner.Creature, _strength, Owner.Creature, this);
        }

        if (_damage > 0 || _vulnerable > 0 || _weak > 0 || _poison > 0)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target);
            if (_damage > 0)
            {
                await DamageCmd.Attack(_damage)
                    .WithHitCount(_hits)
                    .FromCard(this)
                    .Targeting(cardPlay.Target)
                    .Execute(choiceContext);
            }

            if (_vulnerable > 0)
            {
                await PowerCmd.Apply<VulnerablePower>(cardPlay.Target, _vulnerable, Owner.Creature, this);
            }

            if (_weak > 0)
            {
                await PowerCmd.Apply<WeakPower>(cardPlay.Target, _weak, Owner.Creature, this);
            }

            if (_poison > 0)
            {
                await PowerCmd.Apply<PoisonPower>(cardPlay.Target, _poison, Owner.Creature, this);
            }
        }
    }
}

public abstract class GongdouAttackCard(int energy, string portrait, decimal damage, int hits = 1, decimal vulnerable = 0m, decimal weak = 0m, decimal poison = 0m, bool exhaust = false, bool sly = false)
    : GongdouFixedPuzzleCard(energy, CardType.Attack, TargetType.AnyEnemy, portrait, damage: damage, hits: hits, vulnerable: vulnerable, weak: weak, poison: poison, exhaust: exhaust, sly: sly);

public abstract class GongdouSkillCard(int energy, string portrait, decimal block, decimal strength = 0m, bool exhaust = false)
    : GongdouFixedPuzzleCard(energy, CardType.Skill, TargetType.Self, portrait, block: block, strength: strength, exhaust: exhaust);

public abstract class GongdouTargetedSkillCard(int energy, string portrait, decimal block = 0m, bool exhaust = false)
    : GongdouFixedPuzzleCard(energy, CardType.Skill, TargetType.AnyEnemy, portrait, block: block, exhaust: exhaust);

public abstract class GongdouAttackBlockCard(int energy, string portrait, decimal damage, decimal block, decimal vulnerable = 0m, decimal weak = 0m, decimal poison = 0m, bool exhaust = false, bool sly = false)
    : GongdouFixedPuzzleCard(energy, CardType.Attack, TargetType.AnyEnemy, portrait, damage: damage, block: block, vulnerable: vulnerable, weak: weak, poison: poison, exhaust: exhaust, sly: sly);

public sealed class GongdouStrikeIronclad : GongdouAttackCard { public GongdouStrikeIronclad() : base(1, "strike_ironclad", 6m) { } }
public sealed class GongdouDefendIronclad : GongdouSkillCard { public GongdouDefendIronclad() : base(1, "defend_ironclad", 5m) { } }
public sealed class GongdouBash : GongdouAttackCard { public GongdouBash() : base(2, "bash", 8m, vulnerable: 2m) { } }
public sealed class GongdouNeutralize : GongdouAttackCard { public GongdouNeutralize() : base(0, "neutralize", 3m, weak: 1m) { } }
public sealed class GongdouUppercut : GongdouAttackCard { public GongdouUppercut() : base(2, "uppercut", 13m, vulnerable: 1m, weak: 1m) { } }
public sealed class GongdouClothesline : GongdouAttackCard { public GongdouClothesline() : base(2, "uppercut", 12m, weak: 2m) { } }
public sealed class GongdouD2Clothesline : GongdouAttackCard { public GongdouD2Clothesline() : base(2, "uppercut", 12m, weak: 2m) { } }
public sealed class GongdouD5Clothesline : GongdouAttackCard { public GongdouD5Clothesline() : base(2, "uppercut", 12m, weak: 2m) { } }
public sealed class GongdouIronWave : GongdouAttackBlockCard { public GongdouIronWave() : base(1, "iron_wave", 5m, 5m) { } }
public sealed class GongdouSurvivor : GongdouSkillCard { public GongdouSurvivor() : base(1, "survivor", 8m) { } }
public sealed class GongdouQuickSlash : GongdouAttackCard { public GongdouQuickSlash() : base(1, "slice", 7m) { } }
public sealed class GongdouDaggerThrow : GongdouAttackCard { public GongdouDaggerThrow() : base(1, "dagger_throw", 9m) { } }
public sealed class GongdouBallLightning : GongdouAttackCard { public GongdouBallLightning() : base(1, "ball_lightning", 5m) { } }
public sealed class GongdouD2QuickSlash : GongdouAttackCard { public GongdouD2QuickSlash() : base(1, "slice", 7m) { } }
public sealed class GongdouD2BallLightning : GongdouAttackCard { public GongdouD2BallLightning() : base(1, "ball_lightning", 5m) { } }
public sealed class GongdouD5QuickSlash : GongdouAttackCard { public GongdouD5QuickSlash() : base(1, "slice", 7m) { } }
public sealed class GongdouD5BallLightning : GongdouAttackCard { public GongdouD5BallLightning() : base(1, "ball_lightning", 5m) { } }

public sealed class GongdouD1HeavyHammer : GongdouAttackCard { public GongdouD1HeavyHammer() : base(3, "bludgeon", 23m) { } }
public sealed class GongdouFlyingSword : GongdouAttackCard { public GongdouFlyingSword() : base(1, "dagger_spray", 3m, hits: 3) { } }

public sealed class GongdouD3Prepared : GongdouSkillCard
{
    public GongdouD3Prepared() : base(0, "prepared", 0m) { }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("DiscardTargetLine", GongdouPuzzleRuntime.GetD3DiscardTargetLine(this));
    }
}

public sealed class GongdouD3Survivor : GongdouSkillCard
{
    public GongdouD3Survivor() : base(1, "survivor", 5m) { }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("DiscardTargetLine", GongdouPuzzleRuntime.GetD3DiscardTargetLine(this));
    }
}
public sealed class GongdouD3Finisher : GongdouAttackCard { public GongdouD3Finisher() : base(1, "finisher", 8m) { } }
public sealed class GongdouD3BackstabCunning : GongdouAttackCard { public GongdouD3BackstabCunning() : base(1, "backstab", 6m, sly: true) { } }
public sealed class GongdouD3Feint : GongdouAttackCard { public GongdouD3Feint() : base(1, "slice", 7m) { } }
public sealed class GongdouD3ShadowStep : GongdouAttackBlockCard { public GongdouD3ShadowStep() : base(1, "cloak_and_dagger", 5m, 3m, sly: true) { } }
public sealed class GongdouD3VoidBlade : GongdouAttackCard { public GongdouD3VoidBlade() : base(0, "slice", 4m) { } }

public sealed class GongdouD4Carnage : GongdouAttackCard { public GongdouD4Carnage() : base(2, "bludgeon", 18m) { } }
public sealed class GongdouD4BurningPact : GongdouAttackCard { public GongdouD4BurningPact() : base(1, "burning_pact", 9m) { } }
public sealed class GongdouD4TrueGrit : GongdouSkillCard { public GongdouD4TrueGrit() : base(1, "true_grit", 7m) { } }
public sealed class GongdouD4Survivor : GongdouSkillCard { public GongdouD4Survivor() : base(1, "survivor", 7m) { } }
public sealed class GongdouD4FireBreathing : GongdouFixedPuzzleCard { public GongdouD4FireBreathing() : base(1, CardType.Power, TargetType.Self, "inflame") { } }
public sealed class GongdouD4Evolve : GongdouFixedPuzzleCard { public GongdouD4Evolve() : base(1, CardType.Power, TargetType.Self, "offering") { } }
public sealed class GongdouD4WildStrike : GongdouAttackCard { public GongdouD4WildStrike() : base(1, "twin_strike", 12m) { } }
public sealed class GongdouD4RecklessCharge : GongdouAttackCard { public GongdouD4RecklessCharge() : base(0, "hemokinesis", 7m) { } }
public sealed class GongdouD4Cleave : GongdouAttackCard { public GongdouD4Cleave() : base(1, "twin_strike", 8m) { } }

public sealed class GongdouD5VoidRend : GongdouAttackCard { public GongdouD5VoidRend() : base(1, "reaper", 8m) { } }
public sealed class GongdouD5ColdSnap : GongdouAttackBlockCard { public GongdouD5ColdSnap() : base(1, "cold_snap", 6m, 4m) { } }
public sealed class GongdouD5Recycle : GongdouSkillCard { public GongdouD5Recycle() : base(1, "charge_battery", 5m) { } }
public sealed class GongdouD5Feint : GongdouAttackCard { public GongdouD5Feint() : base(0, "slice", 4m) { } }

public sealed class GongdouD5Void : GongdouChallengeCard
{
    public override CardPoolModel Pool => ModelDb.CardPool<StatusCardPool>();
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<StatusCardPool>();
    public override string PortraitPath => ImageHelper.GetImagePath("atlases/card_atlas.sprites/status/void.tres");
    public override string BetaPortraitPath => PortraitPath;
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    protected override bool IsPlayable => false;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    public GongdouD5Void()
        : base(-1, CardType.Status, CardRarity.Status, TargetType.None)
    {
    }
}

public sealed class GongdouD6Eruption : GongdouAttackCard { public GongdouD6Eruption() : base(2, "bash", 9m) { } }
public sealed class GongdouD6Vigilance : GongdouSkillCard { public GongdouD6Vigilance() : base(2, "defend_ironclad", 8m) { } }
public sealed class GongdouD6EmptyFist : GongdouAttackCard { public GongdouD6EmptyFist() : base(1, "slice", 9m) { } }
public sealed class GongdouD6EmptyBody : GongdouSkillCard { public GongdouD6EmptyBody() : base(1, "defend_ironclad", 8m) { } }
public sealed class GongdouD6FollowUp : GongdouAttackCard { public GongdouD6FollowUp() : base(0, "slice", 8m) { } }
public sealed class GongdouD6WheelKick : GongdouAttackCard { public GongdouD6WheelKick() : base(2, "uppercut", 15m) { } }
public sealed class GongdouD6Offering : GongdouAttackCard { public GongdouD6Offering() : base(0, "offering", 5m) { } }
public sealed class GongdouD6CutThroughFate : GongdouAttackCard { public GongdouD6CutThroughFate() : base(1, "slice", 6m) { } }
public sealed class GongdouD6BowlingBash : GongdouAttackCard { public GongdouD6BowlingBash() : base(1, "body_slam", 8m) { } }
public sealed class GongdouD6Protect : GongdouSkillCard { public GongdouD6Protect() : base(1, "defend_ironclad", 11m) { } }
public sealed class GongdouD6Halt : GongdouSkillCard { public GongdouD6Halt() : base(0, "defend_ironclad", 4m) { } }

public sealed class GongdouD7DeadlyPoison : GongdouFixedPuzzleCard { public GongdouD7DeadlyPoison() : base(1, CardType.Skill, TargetType.AnyEnemy, "deadly_poison", poison: 5m) { } }
public sealed class GongdouD7BouncingFlask : GongdouFixedPuzzleCard { public GongdouD7BouncingFlask() : base(2, CardType.Skill, TargetType.AnyEnemy, "bouncing_flask", poison: 8m) { } }
public sealed class GongdouD7PoisonedStab : GongdouAttackCard { public GongdouD7PoisonedStab() : base(1, "poisoned_stab", 6m, poison: 3m) { } }
public sealed class GongdouD7Catalyst : GongdouFixedPuzzleCard { public GongdouD7Catalyst() : base(1, CardType.Skill, TargetType.AnyEnemy, "deadly_poison", exhaust: true) { } }
public sealed class GongdouD7NoxiousFumes : GongdouSkillCard { public GongdouD7NoxiousFumes() : base(1, "noxious_fumes", 0m, exhaust: true) { } }
public sealed class GongdouD7Bane : GongdouAttackCard { public GongdouD7Bane() : base(1, "poisoned_stab", 7m) { } }
public sealed class GongdouD7Predator : GongdouAttackCard { public GongdouD7Predator() : base(2, "predator", 15m) { } }
public sealed class GongdouD7LegSweep : GongdouFixedPuzzleCard { public GongdouD7LegSweep() : base(2, CardType.Skill, TargetType.AnyEnemy, "leg_sweep", block: 11m, weak: 2m) { } }
public sealed class GongdouD7CloakAndDagger : GongdouAttackBlockCard { public GongdouD7CloakAndDagger() : base(1, "cloak_and_dagger", 4m, 6m) { } }
public sealed class GongdouD7Backflip : GongdouSkillCard { public GongdouD7Backflip() : base(1, "backflip", 7m) { } }
public sealed class GongdouD7Caltrops : GongdouSkillCard { public GongdouD7Caltrops() : base(1, "noxious_fumes", 0m, exhaust: true) { } }

public sealed class GongdouD8Zap : GongdouSkillCard { public GongdouD8Zap() : base(1, "zap", 0m) { } }
public sealed class GongdouD8Dualcast : GongdouSkillCard { public GongdouD8Dualcast() : base(1, "dualcast", 0m) { } }
public sealed class GongdouD8Darkness : GongdouSkillCard { public GongdouD8Darkness() : base(1, "darkness", 0m) { } }
public sealed class GongdouD8Recursion : GongdouSkillCard { public GongdouD8Recursion() : base(1, "dualcast", 0m) { } }
public sealed class GongdouD8Loop : GongdouSkillCard { public GongdouD8Loop() : base(1, "loop", 0m, exhaust: true) { } }
public sealed class GongdouD8Chill : GongdouSkillCard { public GongdouD8Chill() : base(0, "chill", 0m) { } }
public sealed class GongdouD8ColdSnap : GongdouAttackCard { public GongdouD8ColdSnap() : base(1, "cold_snap", 6m) { } }
public sealed class GongdouD8BallLightningOrb : GongdouAttackCard { public GongdouD8BallLightningOrb() : base(1, "ball_lightning", 7m) { } }
public sealed class GongdouD8Melter : GongdouAttackCard { public GongdouD8Melter() : base(1, "beam_cell", 10m) { } }
public sealed class GongdouD8Streamline : GongdouAttackCard { public GongdouD8Streamline() : base(2, "ball_lightning", 15m) { } }
public sealed class GongdouD8Leap : GongdouSkillCard { public GongdouD8Leap() : base(1, "leap", 9m) { } }
public sealed class GongdouD8Coolheaded : GongdouSkillCard { public GongdouD8Coolheaded() : base(1, "coolheaded", 5m) { } }

public sealed class GongdouD9Devotion : GongdouSkillCard { public GongdouD9Devotion() : base(1, "venerate", 0m, exhaust: true) { } }
public sealed class GongdouD9Prostrate : GongdouSkillCard { public GongdouD9Prostrate() : base(0, "guards", 4m) { } }
public sealed class GongdouD9Prayer : GongdouSkillCard { public GongdouD9Prayer() : base(1, "guiding_star", 5m) { } }
public sealed class GongdouD9Worship : GongdouSkillCard { public GongdouD9Worship() : base(2, "venerate", 0m) { } }
public sealed class GongdouD9Brilliance : GongdouAttackCard { public GongdouD9Brilliance() : base(1, "radiate", 12m) { } }
public sealed class GongdouD9Ragnarok : GongdouAttackCard { public GongdouD9Ragnarok() : base(2, "seven_stars", 5m, hits: 4) { } }
public sealed class GongdouD9Judgment : GongdouAttackCard { public GongdouD9Judgment() : base(1, "monarchs_gaze", 30m) { } }
public sealed class GongdouD9CarveReality : GongdouAttackCard { public GongdouD9CarveReality() : base(1, "refine_blade", 6m) { } }
public sealed class GongdouD9Smite : GongdouAttackCard { public GongdouD9Smite() : base(1, "slice", 12m) { } }
public sealed class GongdouD9Offering : GongdouAttackCard { public GongdouD9Offering() : base(0, "offering", 5m) { } }
public sealed class GongdouD9Sanctity : GongdouSkillCard { public GongdouD9Sanctity() : base(1, "neutron_aegis", 8m) { } }
public sealed class GongdouD9Wallop : GongdouSkillCard { public GongdouD9Wallop() : base(2, "hammer_time", 13m) { } }
public sealed class GongdouD9EmptyBody : GongdouSkillCard { public GongdouD9EmptyBody() : base(1, "defend_ironclad", 7m) { } }

public sealed class GongdouD10TimeSeal : GongdouSkillCard { public GongdouD10TimeSeal() : base(1, "coolheaded", 0m) { } }
public sealed class GongdouD10ChargeStance : GongdouSkillCard { public GongdouD10ChargeStance() : base(0, "charge_battery", 3m) { } }
public sealed class GongdouD10RiftMark : GongdouAttackCard { public GongdouD10RiftMark() : base(1, "beam_cell", 0m, vulnerable: 1m) { } }
public sealed class GongdouD10EchoStrike : GongdouAttackCard { public GongdouD10EchoStrike() : base(1, "rebound", 11m) { } }
public sealed class GongdouD10EchoForm : GongdouSkillCard { public GongdouD10EchoForm() : base(1, "echo_form", 0m) { } }
public sealed class GongdouD10DelayedBlast : GongdouAttackCard { public GongdouD10DelayedBlast() : base(2, "meteor_strike", 18m) { } }
public sealed class GongdouD10OverloadRay : GongdouAttackCard { public GongdouD10OverloadRay() : base(2, "hyperbeam", 12m) { } }
public sealed class GongdouD10VentHeat : GongdouSkillCard { public GongdouD10VentHeat() : base(0, "coolheaded", 6m) { } }
public sealed class GongdouD10PhaseBarrier : GongdouSkillCard { public GongdouD10PhaseBarrier() : base(1, "charge_battery", 8m) { } }
public sealed class GongdouD10FocusCalibrate : GongdouAttackBlockCard { public GongdouD10FocusCalibrate() : base(1, "beam_cell", 3m, 2m, vulnerable: 1m) { } }
public sealed class GongdouD10FinalCommand : GongdouAttackCard { public GongdouD10FinalCommand() : base(2, "judgment", 32m) { } }
public sealed class GongdouD10MirrorPreview : GongdouSkillCard { public GongdouD10MirrorPreview() : base(1, "echo_form", 0m) { } }
public sealed class GongdouD10BurningShot : GongdouAttackCard { public GongdouD10BurningShot() : base(1, "beam_cell", 6m) { } }
public sealed class GongdouD10CoolingLoop : GongdouSkillCard { public GongdouD10CoolingLoop() : base(1, "coolheaded", 5m) { } }
public sealed class GongdouD10IdleProgram : GongdouSkillCard { public GongdouD10IdleProgram() : base(1, "defend_blue", 2m) { } }
public sealed class GongdouD10SpikeMark : GongdouAttackCard { public GongdouD10SpikeMark() : base(1, "beam_cell", 5m, vulnerable: 1m) { } }
