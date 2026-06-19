using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using GongdouSts2FrierenMod.Assets;
using GongdouSts2FrierenMod.Characters;
using GongdouSts2FrierenMod.Powers;

namespace GongdouSts2FrierenMod.Cards;

public abstract class FrierenCard : CardModel
{
    protected FrierenCard(int energy, CardType type, CardRarity rarity, TargetType targetType)
        : base(energy, type, rarity, targetType)
    {
    }

    public virtual bool IsSpell => false;
    public virtual bool IsNormalMagic => false;
    public virtual bool IsGeneratedSmallMagic => false;
    public virtual string DesignTier => "";
    public override CardPoolModel Pool => ModelDb.CardPool<FrierenCardPool>();
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<FrierenCardPool>();
    protected virtual string PortraitKey => GetType().Name;
    protected virtual string PortraitCardAtlasId => "strike_ironclad";
    public string FrierenPortraitKey => PortraitKey;
    public string FrierenPortraitRelativePath => FrierenAssetPaths.CardRelativePath(PortraitKey);
    public bool HasFrierenCustomPortrait => FrierenAssetPaths.IsCustomArtKey(PortraitKey);
    public override string PortraitPath => FrierenAssetPaths.CardPortrait(PortraitKey, PortraitCardAtlasId);
    public override string BetaPortraitPath => PortraitPath;

    public override async Task BeforeCombatStart()
    {
        if (Owner?.Creature == null || Owner.Creature.GetPower<FrierenCombatTrackerPower>() != null)
        {
            return;
        }

        await PowerCmd.Apply<FrierenCombatTrackerPower>(Owner.Creature, 1m, Owner.Creature, null, silent: true);
    }

    protected async Task GainBlock(decimal amount, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, cardPlay);
    }

    protected async Task GainConcealedMana(decimal amount)
    {
        await PowerCmd.Apply<FrierenConcealedManaPower>(Owner.Creature, amount, Owner.Creature, this);
    }

    protected async Task ApplyAnalysis(CardPlay cardPlay, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<FrierenAnalysisPower>(cardPlay.Target, amount, Owner.Creature, this);
    }

    protected async Task DealSpellDamage(PlayerChoiceContext choiceContext, CardPlay cardPlay, decimal damage, int hitCount = 1)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected async Task<bool> RecordNormalMagic(PlayerChoiceContext choiceContext)
    {
        if (IsNormalMagic)
        {
            return await FrierenMemoryPower.RecordNormalMagic(choiceContext, Owner.Creature, this);
        }

        return false;
    }

    protected IEnumerable<IHoverTip> WithFrierenTagHoverTips(params IHoverTip[] tips)
    {
        return FrierenKeywordTips.ForCard("", IsSpell, IsNormalMagic).Concat(tips);
    }
}

public sealed class FrierenStrike : FrierenCard
{
    protected override string PortraitKey => "起始牌/打击";
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    public FrierenStrike()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}

public sealed class FrierenDefend : FrierenCard
{
    protected override string PortraitKey => "起始牌/防御";
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];

    public FrierenDefend()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

public sealed class BasicKillingMagic : FrierenCard
{
    public override bool IsSpell => true;
    protected override string PortraitKey => "起始牌/083_基础杀人魔法";
    protected override string PortraitCardAtlasId => "strike_ironclad";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move), new DynamicVar("Analysis", 2m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenAnalysisPower>());

    public BasicKillingMagic()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await ApplyAnalysis(cardPlay, DynamicVars["Analysis"].BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Analysis"].UpgradeValueBy(1m);
    }
}

public sealed class ManaSuppression : FrierenCard
{
    public override bool IsSpell => true;
    protected override string PortraitKey => "起始牌/084_魔力抑制";
    protected override string PortraitCardAtlasId => "defend_ironclad";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6m, ValueProp.Move), new DynamicVar("ConcealedMana", 2m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenConcealedManaPower>());

    public ManaSuppression()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["ConcealedMana"].UpgradeValueBy(1m);
    }
}

public sealed class KillingMagic : FrierenCard
{
    public override string DesignTier => "C1";
    public override bool IsSpell => true;
    protected override string PortraitKey => "奖励卡牌/001_杀人魔法";
    protected override string PortraitCardAtlasId => "strike_ironclad";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9m, ValueProp.Move), new DynamicVar("Analysis", 2m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenAnalysisPower>());

    public KillingMagic()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await ApplyAnalysis(cardPlay, DynamicVars["Analysis"].BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["Analysis"].UpgradeValueBy(1m);
    }
}

public sealed class DefensiveMagic : FrierenCard
{
    public override bool IsSpell => true;
    public override string DesignTier => "C1";
    protected override string PortraitKey => "奖励卡牌/010_防御魔法";
    protected override string PortraitCardAtlasId => "shrug_it_off";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(7m, ValueProp.Move), new DynamicVar("ConcealedMana", 1m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenConcealedManaPower>());

    public DefensiveMagic()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["ConcealedMana"].UpgradeValueBy(1m);
    }
}

public sealed class ManaDetection : FrierenCard
{
    public override bool IsSpell => true;
    public override bool IsNormalMagic => true;
    public override string DesignTier => "C2";
    protected override string PortraitKey => "奖励卡牌/011_魔力探知";
    protected override string PortraitCardAtlasId => "spot_weakness";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Analysis", 4m), new CardsVar(1)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenAnalysisPower>(), HoverTipFactory.FromPower<FrierenMemoryPower>());

    public ManaDetection()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await ApplyAnalysis(cardPlay, DynamicVars["Analysis"].BaseValue);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Analysis"].UpgradeValueBy(1m);
    }
}

public sealed class CleanMagic : FrierenCard
{
    public override bool IsSpell => true;
    public override bool IsNormalMagic => true;
    public override string DesignTier => "C2";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override string PortraitKey => "奖励卡牌/012_清洁魔法";
    protected override string PortraitCardAtlasId => "defend_ironclad";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(3m, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenMemoryPower>());

    public CleanMagic()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}

public sealed class MagicNeedle : FrierenCard
{
    public override bool IsSpell => true;
    public override string DesignTier => "C1";
    protected override string PortraitKey => "奖励卡牌/006_魔力针";
    protected override string PortraitCardAtlasId => "pommel_strike";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move), new CardsVar(1)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenAnalysisPower>(), HoverTipFactory.FromPower<FrierenConcealedManaPower>());

    public MagicNeedle()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var before = cardPlay.Target.GetPower<FrierenAnalysisPower>()?.Amount ?? 0m;
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        var after = cardPlay.Target.GetPower<FrierenAnalysisPower>()?.Amount ?? 0m;
        if (after < before)
        {
            await GainConcealedMana(1m);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}

public sealed class RepeatedShot : FrierenCard
{
    public override bool IsSpell => true;
    public override string DesignTier => "C1";
    protected override string PortraitKey => "奖励卡牌/005_重复射击";
    protected override string PortraitCardAtlasId => "twin_strike";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move), new DynamicVar("Analysis", 2m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenAnalysisPower>());

    public RepeatedShot()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await ApplyAnalysis(cardPlay, DynamicVars["Analysis"].BaseValue);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars["Analysis"].UpgradeValueBy(1m);
    }
}

public sealed class DeepManaSuppression : FrierenCard
{
    public override bool IsSpell => true;
    public override string DesignTier => "U1";
    protected override string PortraitKey => "奖励卡牌/033_深度魔力抑制";
    protected override string PortraitCardAtlasId => "impervious";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move), new DynamicVar("ConcealedMana", 3m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenConcealedManaPower>());

    public DeepManaSuppression()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}

public sealed class LongChant : FrierenCard
{
    public override bool IsSpell => true;
    public override string DesignTier => "U1";
    protected override string PortraitKey => "奖励卡牌/035_长咏唱";
    protected override string PortraitCardAtlasId => "entrench";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move), new DynamicVar("Release", 1m), new CardsVar(1)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenReleasePower>());

    public LongChant()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        await PowerCmd.Apply<FrierenDelayedReleasePower>(Owner.Creature, DynamicVars["Release"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}

public sealed class FullPowerKillingMagic : FrierenCard
{
    public override bool IsSpell => true;
    public override string DesignTier => "R3";
    protected override string PortraitKey => "奖励卡牌/057_全力杀人魔法";
    protected override string PortraitCardAtlasId => "bludgeon";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(28m, ValueProp.Move), new ExtraDamageVar(12m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenReleasePower>());

    public FullPowerKillingMagic()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (cardPlay.PlayIndex > 0)
        {
            await DealSpellDamage(choiceContext, cardPlay, DynamicVars.ExtraDamage.BaseValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
        DynamicVars.ExtraDamage.UpgradeValueBy(4m);
    }
}

public sealed class AnalyzeEverything : FrierenCard
{
    public override bool IsSpell => true;
    public override string DesignTier => "R2";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override string PortraitKey => "奖励卡牌/069_解析一切";
    protected override string PortraitCardAtlasId => "shockwave";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Analysis", 8m), new DynamicVar("Insight", 1m), new CardsVar(1)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenAnalysisPower>(), HoverTipFactory.FromPower<FrierenInsightPower>());

    public AnalyzeEverything()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState == null)
        {
            return;
        }

        foreach (var enemy in CombatState.HittableEnemies)
        {
            await PowerCmd.Apply<FrierenAnalysisPower>(enemy, DynamicVars["Analysis"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<FrierenInsightPower>(enemy, DynamicVars["Insight"].BaseValue, Owner.Creature, this);
        }
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Analysis"].UpgradeValueBy(2m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

public sealed class GreatMageFrieren : FrierenCard
{
    public override string DesignTier => "R3";
    protected override string PortraitKey => "奖励卡牌/071_大魔法使芙莉莲";
    protected override string PortraitCardAtlasId => "demon_form";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("ConcealedMana", 3m), new DynamicVar("Analysis", 2m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenGreatMagePower>());

    public GreatMageFrieren()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenGreatMagePower>(Owner.Creature, DynamicVars["ConcealedMana"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ConcealedMana"].UpgradeValueBy(1m);
        DynamicVars["Analysis"].UpgradeValueBy(1m);
    }
}

public sealed class FlowerFieldMagic : FrierenCard
{
    public override bool IsSpell => true;
    public override bool IsNormalMagic => true;
    public override string DesignTier => "R1";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override string PortraitKey => "奖励卡牌/063_开出花田的魔法";
    protected override string PortraitCardAtlasId => "impervious";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(14m, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithFrierenTagHoverTips(HoverTipFactory.FromPower<FrierenMemoryPower>());

    public FlowerFieldMagic()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        if ((Owner.Creature.GetPower<FrierenMemoryPower>()?.Amount ?? 0m) >= 3m)
        {
            await PowerCmd.Apply<FrierenFlowerFieldHealPower>(Owner.Creature, 2m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
