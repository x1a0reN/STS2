using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using GongdouSts2FrierenMod.Assets;
using GongdouSts2FrierenMod.Cards;
using GongdouSts2FrierenMod.Powers;

namespace GongdouSts2FrierenMod.Potions;

public abstract class FrierenPotion : PotionModel
{
    public abstract string FrierenImageKey { get; }
    public string FrierenImagePath => FrierenAssetPaths.PotionIcon(FrierenImageKey);
    public string FrierenImageRelativePath => FrierenAssetPaths.PotionRelativePath(FrierenImageKey);
    public override IEnumerable<IHoverTip> ExtraHoverTips => FrierenKeywordTips.FromText(DynamicDescription.GetFormattedText());
}

public sealed class ManaReleasePotion : FrierenPotion
{
    public override string FrierenImageKey => "100_魔力解放药水";
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        await PowerCmd.Apply<FrierenReleasePower>(target, 1m, Owner.Creature, null);
    }
}

public sealed class AnalysisPotion : FrierenPotion
{
    public override string FrierenImageKey => "101_解析药水";
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AllEnemies;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        foreach (var enemy in combatState.HittableEnemies)
        {
            await PowerCmd.Apply<FrierenAnalysisPower>(enemy, 8m, Owner.Creature, null);
        }
    }
}

public sealed class NormalMagicBottlePotion : FrierenPotion
{
    private static readonly Type[] SmallMagicPoolTypes =
    [
        typeof(UselessSmallMagic),
        typeof(CoinFindingMagic),
        typeof(RustRemovalMagic),
        typeof(CleanMagic),
        typeof(QuietSteps)
    ];

    public override string FrierenImageKey => "102_普通魔法瓶";
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        var player = target.Player ?? Owner;
        for (var i = 0; i < 3; i++)
        {
            var selected = player.RunState.Rng.CombatCardSelection.NextItem(SmallMagicPoolTypes)!;
            await AddGeneratedToHand(selected, player);
        }
    }

    private async Task AddGeneratedToHand(Type cardType, Player player)
    {
        var canonical = (CardModel)typeof(ModelDb)
            .GetMethod(nameof(ModelDb.Card))!
            .MakeGenericMethod(cardType)
            .Invoke(null, null)!;
        var card = ICardScope.DebugOnlyGet(MegaCrit.Sts2.Core.Entities.Cards.CardScope.Combat).CreateCard(canonical, player);
        card.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        card.ExhaustOnNextPlay = true;
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }
}

public sealed class ConcealedManaPotion : FrierenPotion
{
    public override string FrierenImageKey => "103_隐匿魔力药水";
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        await PowerCmd.Apply<FrierenConcealedManaPower>(target, 9m, Owner.Creature, null);
    }
}

public sealed class FlowerFieldPotion : FrierenPotion
{
    public override string FrierenImageKey => "104_花田药水";
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        await CreatureCmd.GainBlock(target, 15m, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, null);
        await PowerCmd.Apply<FrierenFlowerFieldPotionPower>(target, 3m, Owner.Creature, null);
    }
}

public static class FrierenPotionCatalog
{
    public static readonly Type[] AllPotionTypes =
    [
        typeof(ManaReleasePotion),
        typeof(AnalysisPotion),
        typeof(NormalMagicBottlePotion),
        typeof(ConcealedManaPotion),
        typeof(FlowerFieldPotion)
    ];

    public static PotionModel[] GetAllPotions()
    {
        var potionMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Potion))!;
        return AllPotionTypes
            .Select(type => (PotionModel)potionMethod.MakeGenericMethod(type).Invoke(null, null)!)
            .ToArray();
    }
}
