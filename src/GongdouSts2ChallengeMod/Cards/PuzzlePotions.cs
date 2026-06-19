using GongdouSts2ChallengeMod.Challenges;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace GongdouSts2ChallengeMod.Cards;

public abstract class GongdouFixedPotion : PotionModel
{
    private readonly decimal _damage;
    private readonly decimal _block;
    private readonly decimal _vulnerable;
    private readonly decimal _weak;
    private readonly decimal _poison;
    private readonly decimal _energy;
    private readonly bool _targetsEnemy;

    protected GongdouFixedPotion(
        decimal damage = 0m,
        decimal block = 0m,
        decimal vulnerable = 0m,
        decimal weak = 0m,
        decimal poison = 0m,
        decimal energy = 0m,
        bool targetsEnemy = true)
    {
        _damage = damage;
        _block = block;
        _vulnerable = vulnerable;
        _weak = weak;
        _poison = poison;
        _energy = energy;
        _targetsEnemy = targetsEnemy;
    }

    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => _targetsEnemy ? TargetType.AnyEnemy : TargetType.Self;

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

            return vars;
        }
    }

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (await GongdouPuzzleRuntime.TryUsePotionAsync(this, choiceContext, target))
        {
            return;
        }

        if (_block > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature, _block, ValueProp.Unpowered, null);
        }

        if (_energy > 0)
        {
            await PlayerCmd.GainEnergy(_energy, Owner);
        }

        if (_targetsEnemy)
        {
            PotionModel.AssertValidForTargetedPotion(target);
            if (_damage > 0)
            {
                await CreatureCmd.Damage(choiceContext, target, _damage, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, Owner.Creature);
            }

            if (_vulnerable > 0)
            {
                await PowerCmd.Apply<VulnerablePower>(target, _vulnerable, Owner.Creature, null);
            }

            if (_weak > 0)
            {
                await PowerCmd.Apply<WeakPower>(target, _weak, Owner.Creature, null);
            }

            if (_poison > 0)
            {
                await PowerCmd.Apply<PoisonPower>(target, _poison, Owner.Creature, null);
            }
        }
    }

}

public sealed class GongdouD2FirePotion : GongdouFixedPotion { public GongdouD2FirePotion() : base(damage: 20m) { } }
public sealed class GongdouD3CunningPotion : GongdouFixedPotion { public GongdouD3CunningPotion() : base(block: 6m, targetsEnemy: false) { } }
public sealed class GongdouD4FirePotion : GongdouFixedPotion { public GongdouD4FirePotion() : base(damage: 20m) { } }
public sealed class GongdouD4ClarityPotion : GongdouFixedPotion { public GongdouD4ClarityPotion() : base(damage: 13m) { } }
public sealed class GongdouD4GhostPotion : GongdouFixedPotion { public GongdouD4GhostPotion() : base(block: 18m, targetsEnemy: false) { } }
public sealed class GongdouD5BarrierPotion : GongdouFixedPotion { public GongdouD5BarrierPotion() : base(vulnerable: 2m) { } }
public sealed class GongdouD5EnergyPotion : GongdouFixedPotion { public GongdouD5EnergyPotion() : base(block: 20m, energy: 3m, targetsEnemy: false) { } }
public sealed class GongdouD6RagePotion : GongdouFixedPotion { public GongdouD6RagePotion() : base(energy: 1m, targetsEnemy: false) { } }
public sealed class GongdouD6CalmPotion : GongdouFixedPotion { public GongdouD6CalmPotion() : base(block: 6m, targetsEnemy: false) { } }
public sealed class GongdouD6FirePotion : GongdouFixedPotion { public GongdouD6FirePotion() : base(damage: 18m) { } }
public sealed class GongdouD7PoisonPotion : GongdouFixedPotion { public GongdouD7PoisonPotion() : base(poison: 10m) { } }
public sealed class GongdouD7CatalystPotion : GongdouFixedPotion { public GongdouD7CatalystPotion() : base() { } }
public sealed class GongdouD8FocusPotion : GongdouFixedPotion { public GongdouD8FocusPotion() : base(targetsEnemy: false) { } }
public sealed class GongdouD8DarkPotion : GongdouFixedPotion { public GongdouD8DarkPotion() : base(targetsEnemy: false) { } }
public sealed class GongdouD8EvokePotion : GongdouFixedPotion { public GongdouD8EvokePotion() : base(targetsEnemy: false) { } }
public sealed class GongdouD9DivinityPotion : GongdouFixedPotion { public GongdouD9DivinityPotion() : base(targetsEnemy: false) { } }
public sealed class GongdouD9MantraPotion : GongdouFixedPotion { public GongdouD9MantraPotion() : base(targetsEnemy: false) { } }
public sealed class GongdouD9MirrorBreakPotion : GongdouFixedPotion { public GongdouD9MirrorBreakPotion() : base(targetsEnemy: false) { } }
public sealed class GongdouD10TimePotion : GongdouFixedPotion { public GongdouD10TimePotion() : base(targetsEnemy: false) { } }
public sealed class GongdouD10EchoPotion : GongdouFixedPotion { public GongdouD10EchoPotion() : base(targetsEnemy: false) { } }
public sealed class GongdouD10ShatterArmorPotion : GongdouFixedPotion { public GongdouD10ShatterArmorPotion() : base() { } }
