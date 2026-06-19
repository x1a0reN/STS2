using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace GongdouSts2ChallengeMod.Cards;

public sealed class GongdouPoisonPotion12 : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(12m)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        target ??= ResolveEnemyTarget();
        PotionModel.AssertValidForTargetedPotion(target);
        await PowerCmd.Apply<PoisonPower>(target, DynamicVars.Poison.BaseValue, Owner.Creature, null);
    }

    private Creature ResolveEnemyTarget()
    {
        var combatState = Owner.Creature.CombatState;
        return combatState?.HittableEnemies.FirstOrDefault() ??
            combatState?.Enemies.FirstOrDefault(creature => creature.IsAlive) ??
            throw new InvalidOperationException($"No enemy target is available for challenge potion {Id.Entry}.");
    }
}

public sealed class GongdouVulnerablePotion2 : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<VulnerablePower>(2m)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        target ??= ResolveEnemyTarget();
        PotionModel.AssertValidForTargetedPotion(target);
        await PowerCmd.Apply<VulnerablePower>(target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, null);
    }

    private Creature ResolveEnemyTarget()
    {
        var combatState = Owner.Creature.CombatState;
        return combatState?.HittableEnemies.FirstOrDefault() ??
            combatState?.Enemies.FirstOrDefault(creature => creature.IsAlive) ??
            throw new InvalidOperationException($"No enemy target is available for challenge potion {Id.Entry}.");
    }
}

public sealed class GongdouWeakPotion2 : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(2m)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        target ??= ResolveEnemyTarget();
        PotionModel.AssertValidForTargetedPotion(target);
        await PowerCmd.Apply<WeakPower>(target, DynamicVars.Weak.BaseValue, Owner.Creature, null);
    }

    private Creature ResolveEnemyTarget()
    {
        var combatState = Owner.Creature.CombatState;
        return combatState?.HittableEnemies.FirstOrDefault() ??
            combatState?.Enemies.FirstOrDefault(creature => creature.IsAlive) ??
            throw new InvalidOperationException($"No enemy target is available for challenge potion {Id.Entry}.");
    }
}

public sealed class GongdouBurstPotion : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("BonusDamage", 10m)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await PowerCmd.Apply<GongdouBurstPotionPower>(Owner.Creature, 10m, Owner.Creature, null);
    }
}
