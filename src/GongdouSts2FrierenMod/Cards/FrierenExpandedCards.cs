using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
using GongdouSts2FrierenMod.Powers;
using GongdouSts2FrierenMod.Relics;

namespace GongdouSts2FrierenMod.Cards;

public abstract class FrierenSimpleCard : FrierenCard
{
    protected static readonly Type[] SmallMagicPoolTypes =
    [
        typeof(UselessSmallMagic),
        typeof(CoinFindingMagic),
        typeof(RustRemovalMagic),
        typeof(CleanMagic),
        typeof(QuietSteps)
    ];

    protected static readonly Type[] NormalMagicPoolTypes =
    [
        typeof(FlickerFormula),
        typeof(ManaDetection),
        typeof(CleanMagic),
        typeof(RepairMagic),
        typeof(ExploreDungeon),
        typeof(QuietSteps),
        typeof(FlightMagic),
        typeof(IdentifyMimic),
        typeof(UselessSmallMagic),
        typeof(CoinFindingMagic),
        typeof(RustRemovalMagic)
    ];

    public static IReadOnlyList<Type> NormalMagicCardTypes => NormalMagicPoolTypes;
    public static IReadOnlyList<Type> SmallMagicCardTypes => SmallMagicPoolTypes;

    protected FrierenSimpleCard(int energy, CardType type, CardRarity rarity, TargetType targetType)
        : base(energy, type, rarity, targetType)
    {
    }

    public abstract string CardTitle { get; }
    public abstract string CardDescription { get; }
    public override string DesignTier => CardTitle switch
    {
        "破魔射击" or "贯穿防御" or "闪烁术式" or "魔力探知" or "清洁魔法" or "魔力丝线" or "小小收藏癖" => "C2",
        "近距施法" or "旧式杀魔法" or "鉴定魔法书" or "探索地城" or "缓慢咏唱" or "安静脚步" or "解除魔法" => "C3",
        "杀人魔法连射" or "穿刺解析" or "深度魔力抑制" or "菲伦的掩护" or "平静观察" or "飞行魔法" or "屏障反转" => "U1",
        "复写杀人魔法" or "对魔族术式" or "包围火线" or "闪烁齐射" or "杖击与施法" or "弱点解析" or "防御结界" or "魔法收藏" or "魔力隐蔽" or "战斗解析" or "高效咏唱" or "师徒节奏" => "U2",
        "镜像射击" or "魔力爆散" or "反屏障术式" or "隐蔽狙击" or "老练射线" or "鉴别宝箱怪" or "祛除诅咒" or "古旧魔导书" or "半世纪研究" or "咏唱保存" or "魔导书收藏家" or "人类魔法时代" or "菲伦的纪律" or "隐匿魔力储备" or "千年的从容" => "U3",
        "破界射线" or "最后一课" or "开出花田的魔法" or "古代屏障" or "漫长旅途" or "辛美尔的回忆" or "受限的魔力气息" or "普通魔法精通" => "R1",
        "葬送的杀人魔法" or "魔力压倒" or "复写：菲伦的一击" or "认真起来" or "魔法使的直觉" or "解析一切" or "赛丽艾的教诲" or "芙兰梅的教导" or "魔法百科全书" or "魔族杀手" => "R2",
        "全力杀人魔法" or "千年齐射" or "封印魔导书" or "静默蓄势" or "大魔法使芙莉莲" or "千年研究" or "巨大魔力" => "R3",
        "勇者一行的魔法" or "最后的杀人魔法" => "A",
        _ => base.DesignTier
    };

    protected abstract string ArtKey { get; }
    protected override string PortraitKey => ArtKey;
    public override bool IsSpell => Spell;
    public override bool IsNormalMagic => NormalMagic;

    protected virtual bool Spell => true;
    protected virtual bool NormalMagic => false;
    protected virtual bool Exhaust => false;
    protected virtual bool Retain => false;
    protected virtual bool Innate => false;
    protected virtual bool Unplayable => false;
    protected virtual decimal Damage => 0m;
    protected virtual decimal DamageUpgrade => 0m;
    protected virtual int HitCount => 1;
    protected virtual bool AllEnemies => false;
    protected virtual decimal Block => 0m;
    protected virtual decimal BlockUpgrade => 0m;
    protected virtual decimal Analysis => 0m;
    protected virtual decimal AnalysisUpgrade => 0m;
    protected virtual decimal Insight => 0m;
    protected virtual decimal InsightUpgrade => 0m;
    protected virtual decimal ConcealedMana => 0m;
    protected virtual decimal ConcealedManaUpgrade => 0m;
    protected virtual decimal Suppression => 0m;
    protected virtual decimal SuppressionUpgrade => 0m;
    protected virtual decimal Release => 0m;
    protected virtual decimal Draw => 0m;
    protected virtual decimal DrawUpgrade => 0m;
    protected virtual decimal EnergyGain => 0m;
    protected virtual decimal Vulnerable => 0m;
    protected virtual decimal Weak => 0m;
    protected virtual decimal WeakUpgrade => 0m;
    protected virtual decimal Heal => 0m;
    protected virtual int CostUpgrade => 0;
    protected virtual decimal AnalysisThreshold => 0m;
    protected virtual decimal AnalysisThresholdUpgrade => 0m;
    protected virtual decimal RetainedDamage => 0m;
    protected virtual decimal RetainedDamageUpgrade => 0m;
    protected virtual decimal RetainedBlock => 0m;
    protected virtual decimal RetainedBlockUpgrade => 0m;
    protected virtual decimal LifeLoss => 0m;
    protected virtual decimal LifeLossUpgrade => 0m;
    protected virtual bool GenerateTemporaryBasic => false;
    protected virtual bool GenerateUselessSmallMagic => false;
    protected virtual int GenerateSmallMagicCount => 0;
    protected virtual int GenerateNormalMagicCount => 0;
    protected virtual bool GeneratedSmallMagic => false;
    protected virtual decimal Memory => 0m;
    protected virtual decimal MemoryUpgrade => 0m;
    protected virtual decimal DelayedEnergy => 0m;
    protected virtual int RetainHandCards => 0;
    protected virtual bool RetainUpToCards => false;
    protected virtual bool NoAttackThisTurn => false;
    public override bool IsGeneratedSmallMagic => GeneratedSmallMagic;

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            if (Exhaust)
            {
                yield return CardKeyword.Exhaust;
            }

            if (Retain)
            {
                yield return CardKeyword.Retain;
            }

            if (Innate)
            {
                yield return CardKeyword.Innate;
            }

            if (Unplayable)
            {
                yield return CardKeyword.Unplayable;
            }
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            if (Damage > 0m)
            {
                yield return new DamageVar(Damage, ValueProp.Move);
            }

            if (Block > 0m)
            {
                yield return new BlockVar(Block, ValueProp.Move);
            }

            if (Analysis > 0m)
            {
                yield return new DynamicVar("Analysis", Analysis);
            }

            if (Insight > 0m)
            {
                yield return new DynamicVar("Insight", Insight);
            }

            if (ConcealedMana > 0m)
            {
                yield return new DynamicVar("ConcealedMana", ConcealedMana);
            }

            if (Suppression > 0m)
            {
                yield return new DynamicVar("Suppression", Suppression);
            }

            if (Release > 0m)
            {
                yield return new DynamicVar("Release", Release);
            }

            if (Draw > 0m)
            {
                yield return new CardsVar((int)Draw);
            }

            if (EnergyGain > 0m)
            {
                yield return new EnergyVar((int)EnergyGain);
            }

            if (Vulnerable > 0m)
            {
                yield return new PowerVar<VulnerablePower>(Vulnerable);
            }

            if (Weak > 0m)
            {
                yield return new PowerVar<WeakPower>(Weak);
            }

            if (Heal > 0m)
            {
                yield return new DynamicVar("Heal", Heal);
            }

            if (Memory > 0m)
            {
                yield return new DynamicVar("Memory", Memory);
            }

            if (DelayedEnergy > 0m)
            {
                yield return new DynamicVar("DelayedEnergy", DelayedEnergy);
            }

            if (AnalysisThreshold > 0m)
            {
                yield return new DynamicVar("AnalysisThreshold", AnalysisThreshold);
            }

            if (RetainedDamage > 0m)
            {
                yield return new DynamicVar("RetainedDamage", RetainedDamage);
            }

            if (RetainedBlock > 0m)
            {
                yield return new DynamicVar("RetainedBlock", RetainedBlock);
            }

            if (LifeLoss > 0m)
            {
                yield return new DynamicVar("LifeLoss", LifeLoss);
            }
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            foreach (var tip in FrierenKeywordTips.ForCard(CardDescription, Spell, NormalMagic))
            {
                yield return tip;
            }

            if (Vulnerable > 0m || DescriptionMentions("易伤"))
            {
                yield return HoverTipFactory.FromPower<VulnerablePower>();
            }

            if (Weak > 0m || DescriptionMentions("虚弱"))
            {
                yield return HoverTipFactory.FromPower<WeakPower>();
            }
        }
    }

    private bool DescriptionMentions(string term) => CardDescription.Contains(term, StringComparison.Ordinal);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Unplayable)
        {
            return;
        }

        await RecordNormalMagic(choiceContext);

        if (Memory > 0m)
        {
            await FrierenMemoryPower.GainMemory(choiceContext, Owner.Creature, DynamicVars["Memory"].BaseValue, this);
        }

        if (Block > 0m)
        {
            await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        }

        if (RetainHandCards > 0)
        {
            await RetainCardsInHand(choiceContext, RetainHandCards, RetainUpToCards);
        }

        if (EnergyGain > 0m)
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        }

        if (ConcealedMana > 0m)
        {
            await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
        }

        if (Suppression > 0m)
        {
            await PowerCmd.Apply<FrierenSuppressionPower>(Owner.Creature, DynamicVars["Suppression"].BaseValue, Owner.Creature, this);
        }

        if (Release > 0m)
        {
            await PowerCmd.Apply<FrierenReleasePower>(Owner.Creature, DynamicVars["Release"].BaseValue, Owner.Creature, this);
        }

        if (DelayedEnergy > 0m)
        {
            await PowerCmd.Apply<FrierenDelayedEnergyPower>(Owner.Creature, DynamicVars["DelayedEnergy"].BaseValue, Owner.Creature, this);
        }

        if (NoAttackThisTurn)
        {
            await PowerCmd.Apply<FrierenNoAttackThisTurnPower>(Owner.Creature, 1m, Owner.Creature, this);
        }

        if (Damage > 0m)
        {
            if (AllEnemies && CombatState != null)
            {
                foreach (var enemy in CombatState.HittableEnemies)
                {
                    await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                        .WithHitCount(HitCount)
                        .FromCard(this)
                        .Targeting(enemy)
                        .Execute(choiceContext);
                }
            }
            else
            {
                ArgumentNullException.ThrowIfNull(cardPlay.Target);
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .WithHitCount(HitCount)
                    .FromCard(this)
                    .Targeting(cardPlay.Target)
                    .Execute(choiceContext);
            }
        }

        await ApplyTargetPowers(cardPlay);

        if (Draw > 0m)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }

        if (GenerateTemporaryBasic)
        {
            await AddGeneratedCardToHand<TemporaryBasicKillingMagic>(freeThisTurn: true);
        }

        if (GenerateUselessSmallMagic)
        {
            await AddGeneratedCardToHand<UselessSmallMagic>(freeThisTurn: true);
        }

        if (GenerateSmallMagicCount > 0)
        {
            await AddRandomCardsToHand(SmallMagicPoolTypes, GenerateSmallMagicCount, freeThisTurn: true, exhaustOnNextPlay: true);
        }

        if (GenerateNormalMagicCount > 0)
        {
            await AddRandomCardsToHand(NormalMagicPoolTypes, GenerateNormalMagicCount, freeThisTurn: true, exhaustOnNextPlay: true);
        }

        if (Heal > 0m)
        {
            await CreatureCmd.Heal(Owner.Creature, DynamicVars["Heal"].BaseValue);
        }
    }

    protected virtual async Task ApplyTargetPowers(CardPlay cardPlay)
    {
        if (Analysis <= 0m && Insight <= 0m && Vulnerable <= 0m && Weak <= 0m)
        {
            return;
        }

        if (AllEnemies && CombatState != null)
        {
            foreach (var enemy in CombatState.HittableEnemies)
            {
                await ApplyPowersTo(enemy);
            }
        }
        else
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target);
            await ApplyPowersTo(cardPlay.Target);
        }
    }

    protected async Task ApplyPowersTo(Creature target)
    {
        if (Analysis > 0m)
        {
            await PowerCmd.Apply<FrierenAnalysisPower>(target, DynamicVars["Analysis"].BaseValue, Owner.Creature, this);
        }

        if (Insight > 0m)
        {
            await PowerCmd.Apply<FrierenInsightPower>(target, DynamicVars["Insight"].BaseValue, Owner.Creature, this);
        }

        if (Vulnerable > 0m)
        {
            await PowerCmd.Apply<VulnerablePower>(target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        }

        if (Weak > 0m)
        {
            await PowerCmd.Apply<WeakPower>(target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
        }
    }

    protected async Task AddGeneratedCardToHand<T>(bool freeThisTurn = false, bool exhaustOnNextPlay = false) where T : CardModel
    {
        var card = ICardScope.DebugOnlyGet(MegaCrit.Sts2.Core.Entities.Cards.CardScope.Combat).CreateCard<T>(Owner);
        if (freeThisTurn)
        {
            card.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        }

        if (exhaustOnNextPlay)
        {
            card.ExhaustOnNextPlay = true;
        }

        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }

    protected decimal GetTargetAnalysis(CardPlay cardPlay)
    {
        return cardPlay.Target?.GetPower<FrierenAnalysisPower>()?.Amount ?? 0m;
    }

    protected bool TargetHasAnalysis(CardPlay cardPlay, decimal minimum = 1m)
    {
        return GetTargetAnalysis(cardPlay) >= minimum;
    }

    protected bool AnyEnemyHasAnalysis()
    {
        return CombatState?.HittableEnemies.Any(enemy => enemy.GetPower<FrierenAnalysisPower>()?.Amount > 0m) == true;
    }

    protected FrierenCombatTrackerPower? CombatTracker => Owner.Creature.GetPower<FrierenCombatTrackerPower>();
    protected bool HasPlayedKillingMagicThisCombat => CombatTracker?.HasPlayedKillingMagic == true;
    protected bool WasPreviousCardSkill => CombatTracker?.LastPlayedCardType == CardType.Skill;

    protected async Task DealUnblockableSpellDamage(PlayerChoiceContext choiceContext, CardPlay cardPlay, decimal damage)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.Damage(choiceContext, cardPlay.Target, damage, ValueProp.Unblockable | ValueProp.Move, Owner.Creature, this);
    }

    protected async Task AddRandomCardsToHand(Type[] cardTypes, int count, bool freeThisTurn = false, bool exhaustOnNextPlay = false)
    {
        for (var i = 0; i < count; i++)
        {
            var selected = Owner.RunState.Rng.CombatCardSelection.NextItem(cardTypes)!;
            var card = CreateCombatCard(selected, freeThisTurn, exhaustOnNextPlay);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }

    protected CardModel CreateCombatCard(Type cardType, bool freeThisTurn = false, bool exhaustOnNextPlay = false)
    {
        var canonical = (CardModel)typeof(ModelDb)
            .GetMethod(nameof(ModelDb.Card))!
            .MakeGenericMethod(cardType)
            .Invoke(null, null)!;
        var card = ICardScope.DebugOnlyGet(MegaCrit.Sts2.Core.Entities.Cards.CardScope.Combat).CreateCard(canonical, Owner);
        if (freeThisTurn)
        {
            card.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        }

        if (exhaustOnNextPlay)
        {
            card.ExhaustOnNextPlay = true;
        }

        return card;
    }

    protected List<CardModel> CreateRandomCardChoices(Type[] cardTypes, int count, bool freeThisTurn = false, bool exhaustOnNextPlay = false)
    {
        var remaining = cardTypes.ToList();
        var cards = new List<CardModel>();
        while (cards.Count < count && remaining.Count > 0)
        {
            var selected = Owner.RunState.Rng.CombatCardSelection.NextItem(remaining)!;
            remaining.Remove(selected);
            cards.Add(CreateCombatCard(selected, freeThisTurn, exhaustOnNextPlay));
        }

        return cards;
    }

    protected static bool IsAutomatedChoiceContext(PlayerChoiceContext choiceContext)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var property in choiceContext.GetType().GetProperties(flags))
        {
            var name = property.Name;
            if (!name.Contains("Auto", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Automation", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Manual", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(choiceContext);
            }
            catch
            {
                continue;
            }

            if (value is bool boolValue)
            {
                if ((name.Contains("Auto", StringComparison.OrdinalIgnoreCase) && boolValue)
                    || (name.Contains("Manual", StringComparison.OrdinalIgnoreCase) && !boolValue))
                {
                    return true;
                }
            }
            else if (value is Enum enumValue
                     && !string.Equals(enumValue.ToString(), "None", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(enumValue.ToString(), "Manual", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    protected static LocString ChooseCardPrompt => new("card_selection", "FRIEREN_CHOOSE_CARD");
    protected static LocString AddToHandPrompt => new("card_selection", "FRIEREN_ADD_TO_HAND");
    protected static LocString MakeFreePrompt => new("card_selection", "FRIEREN_MAKE_FREE");
    protected static LocString RetainPrompt => new("card_selection", "FRIEREN_RETAIN_CARD");

    protected async Task<CardModel?> SelectOneCard(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<CardModel> cards,
        LocString? prompt = null)
    {
        if (cards.Count == 0)
        {
            return null;
        }

        if (cards.Count == 1)
        {
            return cards[0];
        }

        if (IsAutomatedChoiceContext(choiceContext))
        {
            return cards[0];
        }

        var selectedCards = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            cards,
            Owner,
            new CardSelectorPrefs(prompt ?? ChooseCardPrompt, 1)
            {
                RequireManualConfirmation = true
            })).ToList();
        return selectedCards.FirstOrDefault() ?? cards[0];
    }

    protected async Task RetainCardsInHand(PlayerChoiceContext choiceContext, int count, bool upTo)
    {
        var candidates = PileType.Hand.GetPile(Owner).Cards.Where(card => card != this).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var maxSelection = Math.Min(count, candidates.Count);
        var minSelection = upTo ? 0 : maxSelection;
        if (IsAutomatedChoiceContext(choiceContext))
        {
            foreach (var card in candidates.Take(maxSelection))
            {
                card.GiveSingleTurnRetain();
            }

            return;
        }

        var selectedCards = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            new CardSelectorPrefs(RetainPrompt, minSelection, maxSelection)
            {
                Cancelable = upTo,
                RequireManualConfirmation = true
            })).ToList();

        foreach (var card in selectedCards)
        {
            card.GiveSingleTurnRetain();
        }
    }

    protected async Task DrawUntilHandFull(PlayerChoiceContext choiceContext)
    {
        var missing = Math.Max(0, 10 - PileType.Hand.GetPile(Owner).Cards.Count);
        if (missing > 0)
        {
            await CardPileCmd.Draw(choiceContext, missing, Owner);
        }
    }

    protected async Task MoveTopDrawPileCardsToHandAndDiscard(PlayerChoiceContext choiceContext, int count)
    {
        var cards = PileType.Draw.GetPile(Owner).Cards.Take(count).ToList();
        if (cards.Count == 0)
        {
            return;
        }

        var selected = await SelectOneCard(choiceContext, cards, AddToHandPrompt) ?? cards[0];

        await CardPileCmd.Add(selected, PileType.Hand);
        foreach (var card in cards.Where(card => card != selected).ToList())
        {
            await CardPileCmd.Add(card, PileType.Discard);
        }
    }

    protected async Task ExhaustFirstStatusFromHand(PlayerChoiceContext choiceContext)
    {
        var candidates = PileType.Hand.GetPile(Owner).Cards.Where(c => c != this && c.Type == CardType.Status).ToList();
        var card = await SelectOneCard(choiceContext, candidates, CardSelectorPrefs.ExhaustSelectionPrompt);
        if (card != null)
        {
            await CardCmd.Exhaust(choiceContext, card);
            await SeinsPrayer.TryGrantReleaseFromPurifiedCard(choiceContext, Owner.Creature, card);
        }
    }

    protected async Task DiscardSelectedOtherHandCard(PlayerChoiceContext choiceContext)
    {
        var candidates = PileType.Hand.GetPile(Owner).Cards.Where(c => c != this).ToList();
        var selected = await SelectOneCard(choiceContext, candidates, CardSelectorPrefs.DiscardSelectionPrompt);
        if (selected != null)
        {
            await CardCmd.Discard(choiceContext, selected);
        }
    }

    protected async Task AddDazedToDiscard()
    {
        var card = ICardScope.DebugOnlyGet(MegaCrit.Sts2.Core.Entities.Cards.CardScope.Combat)
            .CreateCard<MegaCrit.Sts2.Core.Models.Cards.Dazed>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, creator: null);
    }

    protected async Task MakeSelectedHandCardFreeThisTurn(PlayerChoiceContext choiceContext)
    {
        var candidates = PileType.Hand.GetPile(Owner).Cards.Where(c => c != this).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var selected = await SelectOneCard(choiceContext, candidates, MakeFreePrompt) ?? candidates[0];
        selected.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
    }

    protected async Task RemoveTargetBlockAndArtifact(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (cardPlay.Target.Block > 0)
        {
            await CreatureCmd.LoseBlock(cardPlay.Target, cardPlay.Target.Block);
        }

        var artifact = cardPlay.Target.GetPower<ArtifactPower>();
        if (artifact != null)
        {
            await PowerCmd.Decrement(artifact);
        }
    }

    protected override void OnUpgrade()
    {
        if (Damage > 0m && DamageUpgrade != 0m)
        {
            DynamicVars.Damage.UpgradeValueBy(DamageUpgrade);
        }

        if (Block > 0m && BlockUpgrade != 0m)
        {
            DynamicVars.Block.UpgradeValueBy(BlockUpgrade);
        }

        if (Analysis > 0m && AnalysisUpgrade != 0m)
        {
            DynamicVars["Analysis"].UpgradeValueBy(AnalysisUpgrade);
        }

        if (Insight > 0m && InsightUpgrade != 0m)
        {
            DynamicVars["Insight"].UpgradeValueBy(InsightUpgrade);
        }

        if (ConcealedMana > 0m && ConcealedManaUpgrade != 0m)
        {
            DynamicVars["ConcealedMana"].UpgradeValueBy(ConcealedManaUpgrade);
        }

        if (Suppression > 0m && SuppressionUpgrade != 0m)
        {
            DynamicVars["Suppression"].UpgradeValueBy(SuppressionUpgrade);
        }

        if (Draw > 0m && DrawUpgrade != 0m)
        {
            DynamicVars.Cards.UpgradeValueBy(DrawUpgrade);
        }

        if (Memory > 0m && MemoryUpgrade != 0m)
        {
            DynamicVars["Memory"].UpgradeValueBy(MemoryUpgrade);
        }

        if (Weak > 0m && WeakUpgrade != 0m)
        {
            DynamicVars.Weak.UpgradeValueBy(WeakUpgrade);
        }

        if (CostUpgrade != 0)
        {
            EnergyCost.UpgradeBy(CostUpgrade);
        }

        if (AnalysisThreshold > 0m && AnalysisThresholdUpgrade != 0m)
        {
            DynamicVars["AnalysisThreshold"].UpgradeValueBy(AnalysisThresholdUpgrade);
        }

        if (RetainedDamage > 0m && RetainedDamageUpgrade != 0m)
        {
            DynamicVars["RetainedDamage"].UpgradeValueBy(RetainedDamageUpgrade);
        }

        if (RetainedBlock > 0m && RetainedBlockUpgrade != 0m)
        {
            DynamicVars["RetainedBlock"].UpgradeValueBy(RetainedBlockUpgrade);
        }

        if (LifeLoss > 0m && LifeLossUpgrade != 0m)
        {
            DynamicVars["LifeLoss"].UpgradeValueBy(LifeLossUpgrade);
        }
    }
}

public sealed class AntiMagicShot : FrierenSimpleCard
{
    public override string CardTitle => "破魔射击";
    public override string CardDescription => "造成{Damage}点伤害。目标有解析：获得{ConcealedMana}层隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/002_破魔射击";
    protected override decimal Damage => 8m;
    protected override decimal DamageUpgrade => 2m;
    protected override decimal ConcealedMana => 2m;
    public AntiMagicShot() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var hadAnalysis = TargetHasAnalysis(cardPlay);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (hadAnalysis)
        {
            await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
        }
    }
}

public sealed class PierceDefenseMagic : FrierenSimpleCard
{
    public override string CardTitle => "贯穿防御";
    public override string CardDescription => "造成{Damage}点伤害。目标有格挡或解析：无视格挡。";
    protected override string ArtKey => "奖励卡牌/003_贯穿防御";
    protected override decimal Damage => 6m;
    protected override decimal DamageUpgrade => 2m;
    public PierceDefenseMagic() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        if (TargetHasAnalysis(cardPlay) || (cardPlay.Target?.Block ?? 0m) > 0m)
        {
            await DealUnblockableSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
            return;
        }

        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
    }
}

public sealed class CloseRangeCast : FrierenSimpleCard
{
    public override string CardTitle => "近距施法";
    public override string CardDescription => "造成{Damage}点伤害。解放：伤害翻倍。看破：伤害翻倍。";
    protected override string ArtKey => "奖励卡牌/004_近距施法";
    protected override decimal Damage => 4m;
    protected override decimal DamageUpgrade => 1m;
    public CloseRangeCast() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var damage = DynamicVars.Damage.BaseValue;
        if ((Owner.Creature.GetPower<FrierenReleasePower>()?.Amount ?? 0) > 0
            || (cardPlay.Target?.GetPower<FrierenInsightPower>()?.Amount ?? 0m) > 0m)
        {
            damage *= 2m;
        }

        await DealSpellDamage(choiceContext, cardPlay, damage);
    }
}

public sealed class FlickerFormula : FrierenSimpleCard
{
    public override string CardTitle => "闪烁术式";
    public override string CardDescription => "造成{Damage}点伤害。施加{Analysis}层解析。消耗。";
    protected override string ArtKey => "奖励卡牌/007_闪烁术式";
    protected override bool NormalMagic => true;
    protected override bool Exhaust => true;
    protected override decimal Damage => 3m;
    protected override decimal DamageUpgrade => 2m;
    protected override decimal Analysis => 1m;
    public FlickerFormula() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
}

public sealed class ScatteredLightBullet : FrierenSimpleCard
{
    public override string CardTitle => "散射光弹";
    public override string CardDescription => "对所有敌人造成{Damage}点伤害，施加{Analysis}层解析。";
    protected override string ArtKey => "奖励卡牌/008_散射光弹";
    protected override bool AllEnemies => true;
    protected override decimal Damage => 10m;
    protected override decimal DamageUpgrade => 3m;
    protected override decimal Analysis => 1m;
    public ScatteredLightBullet() : base(2, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }
}

public sealed class OldKillingMagic : FrierenSimpleCard
{
    public override string CardTitle => "旧式杀魔法";
    public override string CardDescription => "造成{Damage}点伤害。目标{AnalysisThreshold}层解析：施加{Insight}层看破。";
    protected override string ArtKey => "奖励卡牌/009_旧式杀魔法";
    protected override decimal Damage => 9m;
    protected override decimal DamageUpgrade => 2m;
    protected override decimal Insight => 1m;
    protected override decimal AnalysisThreshold => 6m;
    protected override decimal AnalysisThresholdUpgrade => -1m;
    public OldKillingMagic() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (TargetHasAnalysis(cardPlay, DynamicVars["AnalysisThreshold"].BaseValue))
        {
            await ApplyPowersTo(cardPlay.Target!);
        }
    }
}

public sealed class RepairMagic : FrierenSimpleCard
{
    public override string CardTitle => "修补魔法";
    public override string CardDescription => "获得{Block}点格挡。3层回忆：抽{Cards}张牌。";
    protected override string ArtKey => "奖励卡牌/013_修补魔法";
    protected override bool NormalMagic => true;
    protected override decimal Block => 7m;
    protected override decimal BlockUpgrade => 2m;
    protected override decimal Draw => 1m;
    public RepairMagic() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        if ((Owner.Creature.GetPower<FrierenMemoryPower>()?.Amount ?? 0) >= 3m)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }
    }
}

public sealed class AppraiseGrimoire : FrierenSimpleCard
{
    public override string CardTitle => "鉴定魔法书";
    public override string CardDescription => "发现1张小魔法。本回合0费。消耗。";
    protected override string ArtKey => "奖励卡牌/014_鉴定魔法书";
    protected override bool Exhaust => true;
    public AppraiseGrimoire() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var choices = CreateRandomCardChoices(SmallMagicPoolTypes, 3, freeThisTurn: true, exhaustOnNextPlay: true);
        var selected = await SelectOneCard(choiceContext, choices);
        if (selected != null)
        {
            await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, Owner);
        }
    }
}

public sealed class ExploreDungeon : FrierenSimpleCard
{
    public override string CardTitle => "探索地城";
    public override string CardDescription => "查看抽牌堆顶{Cards}张牌。选择1张，弃掉其余牌。消耗。";
    protected override string ArtKey => "奖励卡牌/015_探索地城";
    protected override bool NormalMagic => true;
    protected override bool Exhaust => true;
    protected override decimal Draw => 3m;
    protected override decimal DrawUpgrade => 1m;
    public ExploreDungeon() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await MoveTopDrawPileCardsToHandAndDiscard(choiceContext, (int)DynamicVars.Cards.BaseValue);
    }
}

public sealed class SlowChant : FrierenSimpleCard
{
    public override string CardTitle => "缓慢咏唱";
    public override string CardDescription => "咏唱1：获得{Block}点格挡和{Energy}点能量。";
    protected override string ArtKey => "奖励卡牌/016_缓慢咏唱";
    protected override decimal Block => 12m;
    protected override decimal BlockUpgrade => 3m;
    protected override decimal EnergyGain => 1m;
    public SlowChant() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await PowerCmd.Apply<FrierenDelayedBlockEnergyPower>(Owner.Creature, DynamicVars.Block.BaseValue, Owner.Creature, this);
    }
}

public sealed class QuietSteps : FrierenSimpleCard
{
    public override string CardTitle => "安静脚步";
    public override string CardDescription => "获得{Block}点格挡。保留1张牌。本回合未打出攻击牌：回合结束时获得2层隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/017_安静脚步";
    protected override bool NormalMagic => true;
    protected override int RetainHandCards => 1;
    protected override decimal Block => 2m;
    protected override decimal BlockUpgrade => 2m;
    public QuietSteps() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.OnPlay(choiceContext, cardPlay);
        await PowerCmd.Apply<FrierenQuietStepsPower>(Owner.Creature, 2m, Owner.Creature, this);
    }
}

public sealed class DispelMagic : FrierenSimpleCard
{
    public override string CardTitle => "解除魔法";
    public override string CardDescription => "移除自身1个负面状态，或令敌人本回合失去{Weak}点力量。移除负面状态：施加1层看破。";
    protected override string ArtKey => "奖励卡牌/018_解除魔法";
    protected override decimal Weak => 3m;
    protected override decimal WeakUpgrade => 2m;
    public DispelMagic() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var debuff = Owner.Creature.Powers.FirstOrDefault(power => power.TypeForCurrentAmount == PowerType.Debuff);
        if (debuff != null)
        {
            await PowerCmd.Remove(debuff);
            if (cardPlay.Target != null)
            {
                await PowerCmd.Apply<FrierenInsightPower>(cardPlay.Target, 1m, Owner.Creature, this);
            }
            return;
        }

        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var amount = IsUpgraded ? 5m : DynamicVars.Weak.BaseValue;
        await PowerCmd.Apply<FrierenTemporaryStrengthDownPower>(cardPlay.Target, amount, Owner.Creature, this);
    }
}

public sealed class ManaThread : FrierenSimpleCard
{
    public override string CardTitle => "魔力丝线";
    public override string CardDescription => "抽{Cards}张牌。弃1张牌。获得{ConcealedMana}层隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/019_魔力丝线";
    protected override decimal Draw => 2m;
    protected override decimal ConcealedMana => 1m;
    protected override decimal ConcealedManaUpgrade => 1m;
    public ManaThread() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await DiscardSelectedOtherHandCard(choiceContext);
        await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
    }
}

public sealed class SmallCollector : FrierenSimpleCard
{
    public override string CardTitle => "小小收藏癖";
    public override string CardDescription => "获得{Block}点格挡。发现1张小魔法。未升级：放入弃牌堆。升级：加入手牌。";
    protected override string ArtKey => "奖励卡牌/020_小小收藏癖";
    protected override bool NormalMagic => true;
    protected override decimal Block => 7m;
    public SmallCollector() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        var choices = CreateRandomCardChoices(SmallMagicPoolTypes, 3);
        var selected = await SelectOneCard(choiceContext, choices);
        if (selected != null)
        {
            await CardPileCmd.AddGeneratedCardToCombat(selected, IsUpgraded ? PileType.Hand : PileType.Discard, Owner);
        }
    }
}

public sealed class KillingMagicBarrage : FrierenSimpleCard
{
    public override string CardTitle => "杀人魔法连射";
    public override string CardDescription => "造成{Damage}点伤害3次。";
    protected override string ArtKey => "奖励卡牌/021_杀人魔法连射";
    protected override decimal Damage => 6m;
    protected override decimal DamageUpgrade => 1m;
    protected override int HitCount => 3;
    public KillingMagicBarrage() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
}

public sealed class CopyKillingMagic : FrierenSimpleCard
{
    public override string CardTitle => "复写杀人魔法";
    public override string CardDescription => "造成{Damage}点伤害。本场打过杀人魔法牌：再打1次。";
    protected override string ArtKey => "奖励卡牌/022_复写杀人魔法";
    protected override decimal Damage => 7m;
    protected override decimal DamageUpgrade => 2m;
    public CopyKillingMagic() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var hits = HasPlayedKillingMagicThisCombat ? 2 : 1;
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, hits);
    }
}

public sealed class AntiDemonFormula : FrierenSimpleCard
{
    public override string CardTitle => "对魔族术式";
    public override string CardDescription => "造成{Damage}点伤害。目标8层解析：施加{Insight}层看破和{Vulnerable}层易伤。";
    protected override string ArtKey => "奖励卡牌/023_对魔族术式";
    protected override decimal Damage => 18m;
    protected override decimal DamageUpgrade => 4m;
    protected override decimal Insight => 1m;
    protected override decimal Vulnerable => 2m;
    public AntiDemonFormula() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var shouldVulnerable = TargetHasAnalysis(cardPlay, 8m);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (shouldVulnerable)
        {
            await ApplyPowersTo(cardPlay.Target!);
        }
    }
}

public sealed class PiercingAnalysis : FrierenSimpleCard
{
    public override string CardTitle => "穿刺解析";
    public override string CardDescription => "造成{Damage}点伤害。施加{Analysis}层解析。";
    protected override string ArtKey => "奖励卡牌/024_穿刺解析";
    protected override decimal Damage => 10m;
    protected override decimal DamageUpgrade => 2m;
    protected override decimal Analysis => 4m;
    public PiercingAnalysis() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
}

public sealed class MirrorShot : FrierenSimpleCard
{
    public override string CardTitle => "镜像射击";
    public override string CardDescription => "造成{Damage}点伤害。解放：获得{ConcealedMana}层隐匿魔力。抽1张牌。";
    protected override string ArtKey => "奖励卡牌/025_镜像射击";
    protected override decimal Damage => 8m;
    protected override decimal DamageUpgrade => 2m;
    protected override decimal ConcealedMana => 4m;
    public MirrorShot() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (cardPlay.PlayIndex > 0)
        {
            await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }
    }
}

public sealed class ManaBurst : FrierenSimpleCard
{
    public override string CardTitle => "魔力爆散";
    public override string CardDescription => "造成等同隐匿魔力层数的伤害，最多{Damage}点。失去所有隐匿魔力。失去至少10层：施加1层看破。";
    protected override string ArtKey => "奖励卡牌/026_魔力爆散";
    protected override decimal Damage => 12m;
    protected override decimal DamageUpgrade => 4m;
    public ManaBurst() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var mana = Owner.Creature.GetPower<FrierenConcealedManaPower>();
        var lostMana = mana?.Amount ?? 0m;
        var damage = Math.Min(lostMana, DynamicVars.Damage.BaseValue);
        if (mana != null)
        {
            await PowerCmd.Remove(mana);
        }

        await DealSpellDamage(choiceContext, cardPlay, damage);
        if (lostMana >= 10m)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target);
            await PowerCmd.Apply<FrierenInsightPower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
    }
}

public sealed class EncirclingFireline : FrierenSimpleCard
{
    public override string CardTitle => "包围火线";
    public override string CardDescription => "对所有敌人造成{Damage}点伤害，施加{Analysis}层解析。";
    protected override string ArtKey => "奖励卡牌/027_包围火线";
    protected override bool AllEnemies => true;
    protected override decimal Damage => 10m;
    protected override decimal DamageUpgrade => 3m;
    protected override decimal Analysis => 3m;
    public EncirclingFireline() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }
}

public sealed class FlickerVolley : FrierenSimpleCard
{
    public override string CardTitle => "闪烁齐射";
    public override string CardDescription => "造成{Damage}点伤害3次。每消耗1层解析，获得1层隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/028_闪烁齐射";
    protected override decimal Damage => 3m;
    protected override decimal DamageUpgrade => 1m;
    protected override int HitCount => 3;
    public FlickerVolley() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var before = GetTargetAnalysis(cardPlay);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, HitCount);
        var consumed = Math.Max(0m, before - GetTargetAnalysis(cardPlay));
        if (consumed > 0m)
        {
            await GainConcealedMana(consumed);
        }
    }
}

public sealed class StaffStrikeAndCast : FrierenSimpleCard
{
    public override string CardTitle => "杖击与施法";
    public override string CardDescription => "造成{Damage}点伤害。按目标解析层数获得格挡，最多{Block}点。施加{Analysis}层解析。";
    protected override string ArtKey => "奖励卡牌/029_杖击与施法";
    protected override decimal Damage => 5m;
    protected override decimal DamageUpgrade => 2m;
    protected override decimal Block => 10m;
    protected override decimal Analysis => 2m;
    public StaffStrikeAndCast() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var block = Math.Min(GetTargetAnalysis(cardPlay), DynamicVars.Block.BaseValue);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (block > 0)
        {
            await GainBlock(block, cardPlay);
        }

        await ApplyPowersTo(cardPlay.Target!);
    }
}

public sealed class ReverseBarrierFormula : FrierenSimpleCard
{
    public override string CardTitle => "反屏障术式";
    public override string CardDescription => "移除目标所有格挡。造成{Damage}点伤害。移除格挡：施加1层看破。";
    protected override string ArtKey => "奖励卡牌/030_反屏障术式";
    protected override decimal Damage => 9m;
    protected override decimal DamageUpgrade => 3m;
    public ReverseBarrierFormula() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var removedBlock = cardPlay.Target.Block > 0;
        if (cardPlay.Target.Block > 0)
        {
            await CreatureCmd.LoseBlock(cardPlay.Target, cardPlay.Target.Block);
        }

        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (removedBlock)
        {
            await PowerCmd.Apply<FrierenInsightPower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
    }
}

public sealed class HiddenSnipe : FrierenSimpleCard
{
    private bool _wasRetained;

    public override string CardTitle => "隐蔽狙击";
    public override string CardDescription => "保留。造成{Damage}点伤害。保留过：造成{RetainedDamage}点伤害并施加1层看破。";
    protected override string ArtKey => "奖励卡牌/031_隐蔽狙击";
    protected override bool Retain => true;
    protected override decimal Damage => 6m;
    protected override decimal DamageUpgrade => 2m;
    protected override decimal RetainedDamage => 14m;
    protected override decimal RetainedDamageUpgrade => 3m;
    public HiddenSnipe() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    public override Task BeforeCombatStart()
    {
        _wasRetained = false;
        return base.BeforeCombatStart();
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Creature.Side && Pile?.Type == PileType.Hand)
        {
            _wasRetained = true;
        }

        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var damage = _wasRetained ? DynamicVars["RetainedDamage"].BaseValue : DynamicVars.Damage.BaseValue;
        await DealSpellDamage(choiceContext, cardPlay, damage);
        if (_wasRetained && cardPlay.Target != null)
        {
            await PowerCmd.Apply<FrierenInsightPower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
        _wasRetained = false;
    }
}

public sealed class VeteranRay : FrierenSimpleCard
{
    public override string CardTitle => "老练射线";
    public override string CardDescription => "造成{Damage}点伤害。3层回忆：抽{Cards}张牌。";
    protected override string ArtKey => "奖励卡牌/032_老练射线";
    protected override decimal Damage => 14m;
    protected override decimal DamageUpgrade => 4m;
    protected override decimal Draw => 2m;
    public VeteranRay() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if ((Owner.Creature.GetPower<FrierenMemoryPower>()?.Amount ?? 0) >= 3)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }
    }
}

public sealed class WeaknessAnalysis : FrierenSimpleCard
{
    public override string CardTitle => "弱点解析";
    public override string CardDescription => "施加{Analysis}层解析。本回合下一张攻击牌费用-1。目标已有解析：施加1层看破。";
    protected override string ArtKey => "奖励卡牌/034_弱点解析";
    protected override decimal Analysis => 6m;
    protected override decimal AnalysisUpgrade => 2m;
    public WeaknessAnalysis() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var hadAnalysis = TargetHasAnalysis(cardPlay);
        await ApplyTargetPowers(cardPlay);
        if (hadAnalysis && cardPlay.Target != null)
        {
            await PowerCmd.Apply<FrierenInsightPower>(cardPlay.Target, 1m, Owner.Creature, this);
        }
        await PowerCmd.Apply<FrierenNextAttackDiscountPower>(Owner.Creature, 1m, Owner.Creature, this);
    }
}

public sealed class DefensiveBarrier : FrierenSimpleCard
{
    public override string CardTitle => "防御结界";
    public override string CardDescription => "获得{Block}点格挡。敌人有解析：获得{ConcealedMana}层隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/036_防御结界";
    protected override decimal Block => 16m;
    protected override decimal BlockUpgrade => 4m;
    protected override decimal ConcealedMana => 3m;
    public DefensiveBarrier() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        if (AnyEnemyHasAnalysis())
        {
            await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
        }
    }
}

public sealed class FlightMagic : FrierenSimpleCard
{
    public override string CardTitle => "飞行魔法";
    public override string CardDescription => "获得{Block}点格挡。抽{Cards}张牌。消耗。";
    protected override string ArtKey => "奖励卡牌/037_飞行魔法";
    protected override bool NormalMagic => true;
    protected override bool Exhaust => true;
    protected override decimal Block => 5m;
    protected override decimal BlockUpgrade => 2m;
    protected override decimal Draw => 1m;
    public FlightMagic() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
}

public sealed class MagicCollection : FrierenSimpleCard
{
    public override string CardTitle => "魔法收藏";
    public override string CardDescription => "将{Cards}张随机普通魔法加入手牌。本回合0费并消耗。";
    protected override string ArtKey => "奖励卡牌/038_魔法收藏";
    protected override int GenerateNormalMagicCount => (int)Draw;
    protected override decimal Draw => 2m;
    protected override decimal DrawUpgrade => 1m;
    public MagicCollection() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await AddRandomCardsToHand(NormalMagicPoolTypes, (int)DynamicVars.Cards.BaseValue, freeThisTurn: true, exhaustOnNextPlay: true);
    }
}

public sealed class IdentifyMimic : FrierenSimpleCard
{
    public override string CardTitle => "鉴别宝箱怪";
    public override string CardDescription => "抽{Cards}张牌。将1张宝箱怪咬痕放入弃牌堆。消耗。";
    protected override string ArtKey => "奖励卡牌/039_鉴别宝箱怪";
    protected override bool NormalMagic => true;
    protected override bool Exhaust => true;
    protected override decimal Draw => 2m;
    protected override decimal DrawUpgrade => 1m;
    public IdentifyMimic() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        var bite = ICardScope.DebugOnlyGet(MegaCrit.Sts2.Core.Entities.Cards.CardScope.Combat)
            .CreateCard<MimicBiteMark>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(bite, PileType.Discard, creator: null);
    }
}

public sealed class RemoveCurseMagic : FrierenSimpleCard
{
    public override string CardTitle => "祛除诅咒";
    public override string CardDescription => "消耗任一区域1张状态牌或诅咒牌。消耗成功：获得{ConcealedMana}层隐匿魔力。升级后可选择任意非攻击牌；只有消耗状态牌或诅咒牌时获得隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/040_祛除诅咒";
    protected override decimal ConcealedMana => 5m;
    protected override decimal ConcealedManaUpgrade => 1m;
    public RemoveCurseMagic() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var candidates = PileType.Hand.GetPile(Owner).Cards
            .Concat(PileType.Draw.GetPile(Owner).Cards)
            .Concat(PileType.Discard.GetPile(Owner).Cards)
            .Where(card => card != this)
            .Where(card => IsUpgraded ? card.Type != CardType.Attack : card.Type is CardType.Status or CardType.Curse)
            .ToList();

        var selected = await SelectOneCard(choiceContext, candidates, CardSelectorPrefs.ExhaustSelectionPrompt);
        if (selected == null)
        {
            return;
        }

        var grantsMana = selected.Type is CardType.Status or CardType.Curse;
        await CardCmd.Exhaust(choiceContext, selected);
        await SeinsPrayer.TryGrantReleaseFromPurifiedCard(choiceContext, Owner.Creature, selected);
        if (grantsMana)
        {
            await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
        }
    }
}

public sealed class FernCover : FrierenSimpleCard
{
    public override string CardTitle => "菲伦的掩护";
    public override string CardDescription => "获得{Block}点格挡。敌人有解析：抽{Cards}张牌。";
    protected override string ArtKey => "奖励卡牌/041_菲伦的掩护";
    protected override decimal Block => 10m;
    protected override decimal BlockUpgrade => 2m;
    protected override decimal Draw => 1m;
    public FernCover() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        if (CombatState?.HittableEnemies.Any(enemy => enemy.GetPower<FrierenAnalysisPower>()?.Amount > 0) == true)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }
    }
}

public sealed class CalmObservation : FrierenSimpleCard
{
    public override string CardTitle => "平静观察";
    public override string CardDescription => "保留。获得{Block}点格挡。施加{Analysis}层解析。";
    protected override string ArtKey => "奖励卡牌/042_平静观察";
    protected override bool Retain => true;
    protected override decimal Block => 5m;
    protected override decimal BlockUpgrade => 3m;
    protected override decimal Analysis => 3m;
    public CalmObservation() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }
}

public sealed class AncientGrimoire : FrierenSimpleCard
{
    public override string CardTitle => "古旧魔导书";
    public override string CardDescription => "从抽牌堆选择1张技能牌加入手牌。消耗。";
    protected override string ArtKey => "奖励卡牌/043_古旧魔导书";
    protected override bool Exhaust => true;
    protected override int CostUpgrade => -1;
    public AncientGrimoire() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var candidates = PileType.Draw.GetPile(Owner).Cards.Where(card => card.Type == CardType.Skill).ToList();
        var selected = await SelectOneCard(choiceContext, candidates, AddToHandPrompt);
        if (selected != null)
        {
            await CardPileCmd.Add(selected, PileType.Hand);
        }
    }
}

public sealed class HalfCenturyResearch : FrierenSimpleCard
{
    public override string CardTitle => "半世纪研究";
    public override string CardDescription => "本场战斗升级1张手牌。获得{Memory}层回忆。升级普通魔法：获得1层隐匿魔力。升级后：升级所有普通魔法。";
    protected override string ArtKey => "奖励卡牌/044_半世纪研究";
    protected override decimal Memory => 1m;
    public HalfCenturyResearch() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var upgradedNormalMagic = false;
        if (IsUpgraded)
        {
            var targets = PileType.Hand.GetPile(Owner).Cards
                .OfType<FrierenCard>()
                .Where(c => c != this && c.IsNormalMagic && c.IsUpgradable)
                .ToList();
            upgradedNormalMagic = targets.Count > 0;
            foreach (var normalMagic in targets)
            {
                CardCmd.Upgrade(normalMagic);
            }
        }
        else
        {
            var candidates = PileType.Hand.GetPile(Owner).Cards.Where(c => c != this && c.IsUpgradable).ToList();
            var card = await SelectOneCard(choiceContext, candidates, CardSelectorPrefs.UpgradeSelectionPrompt);
            if (card != null)
            {
                upgradedNormalMagic = card is FrierenCard { IsNormalMagic: true };
                CardCmd.Upgrade(card);
            }
        }

        await FrierenMemoryPower.GainMemory(choiceContext, Owner.Creature, DynamicVars["Memory"].BaseValue, this);
        if (upgradedNormalMagic)
        {
            await GainConcealedMana(1m);
        }
    }
}

public sealed class BarrierInversion : FrierenSimpleCard
{
    public override string CardTitle => "屏障反转";
    public override string CardDescription => "获得{Block}点格挡。格挡未破：下回合获得{ConcealedMana}层隐匿魔力，并对所有敌人施加2层解析。";
    protected override string ArtKey => "奖励卡牌/045_屏障反转";
    protected override decimal Block => 7m;
    protected override decimal BlockUpgrade => 3m;
    protected override decimal ConcealedMana => 4m;
    public BarrierInversion() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        await PowerCmd.Apply<FrierenBarrierInversionPower>(Owner.Creature, DynamicVars["ConcealedMana"].BaseValue, Owner.Creature, this);
    }
}

public sealed class ChantPreservation : FrierenSimpleCard
{
    public override string CardTitle => "咏唱保存";
    public override string CardDescription => "保留至多2张牌。未升级：消耗。";
    protected override string ArtKey => "奖励卡牌/046_咏唱保存";
    protected override bool Exhaust => !IsUpgraded;
    protected override int RetainHandCards => 2;
    protected override bool RetainUpToCards => true;
    public ChantPreservation() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
}

public sealed class ManaConcealment : FrierenSimpleCard
{
    public override string CardTitle => "魔力隐蔽";
    public override string CardDescription => "获得{Suppression}层抑制。未用能量转为隐匿魔力，每回合最多3层。";
    protected override string ArtKey => "奖励卡牌/047_魔力隐蔽";
    protected override bool Spell => false;
    protected override decimal Suppression => 1m;
    protected override decimal SuppressionUpgrade => 1m;
    public ManaConcealment() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenSuppressionPower>(Owner.Creature, DynamicVars["Suppression"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<FrierenManaConcealmentPower>(Owner.Creature, 3m, Owner.Creature, this);
    }
}

public sealed class GrimoireCollectorCard : FrierenSimpleCard
{
    public override string CardTitle => "魔导书收藏家";
    public override string CardDescription => "每回合前{Cards}次打出普通魔法，抽1张牌。回忆：获得3点格挡。";
    protected override string ArtKey => "奖励卡牌/048_魔导书收藏家";
    protected override bool Spell => false;
    protected override decimal Draw => 1m;
    protected override decimal DrawUpgrade => 1m;
    public GrimoireCollectorCard() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenGrimoireCollectorPower>(Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }
}

public sealed class CombatAnalysisCard : FrierenSimpleCard
{
    public override string CardTitle => "战斗解析";
    public override string CardDescription => "对已有解析的敌人施加解析时，额外施加{Analysis}层。敌人首次达到10层解析：施加1层看破。";
    protected override string ArtKey => "奖励卡牌/049_战斗解析";
    protected override bool Spell => false;
    protected override decimal Analysis => 1m;
    protected override decimal AnalysisUpgrade => 1m;
    public CombatAnalysisCard() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenCombatAnalysisPower>(Owner.Creature, DynamicVars["Analysis"].BaseValue, Owner.Creature, this);
    }
}

public sealed class EfficientChant : FrierenSimpleCard
{
    public override string CardTitle => "高效咏唱";
    public override string CardDescription => "每回合前{Energy}张以解析敌人为目标的法术费用-1。";
    protected override string ArtKey => "奖励卡牌/050_高效咏唱";
    protected override bool Spell => false;
    protected override decimal EnergyGain => 1m;
    public EfficientChant() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenEfficientChantPower>(Owner.Creature, DynamicVars.Energy.BaseValue, Owner.Creature, this);
    }
}

public sealed class HumanMagicEra : FrierenSimpleCard
{
    public override string CardTitle => "人类魔法时代";
    public override string CardDescription => "回合开始时，将1张0费临时基础杀人魔法加入手牌。消耗。";
    protected override string ArtKey => "奖励卡牌/051_人类魔法时代";
    protected override bool Spell => false;
    protected override bool Innate => IsUpgraded;
    public HumanMagicEra() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenHumanMagicEraPower>(Owner.Creature, 1m, Owner.Creature, this);
    }
}

public sealed class FernDiscipline : FrierenSimpleCard
{
    public override string CardTitle => "菲伦的纪律";
    public override string CardDescription => "每回合首次打出0费牌，获得{Block}点格挡。法术：获得1层隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/052_菲伦的纪律";
    protected override bool Spell => false;
    protected override decimal Block => 4m;
    protected override decimal BlockUpgrade => 2m;
    public FernDiscipline() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenDisciplinePower>(Owner.Creature, DynamicVars.Block.BaseValue, Owner.Creature, this);
    }
}

public sealed class ConcealedManaReserve : FrierenSimpleCard
{
    public override string CardTitle => "隐匿魔力储备";
    public override string CardDescription => "获得或消耗解放：获得{Energy}点能量。每回合最多2次。";
    protected override string ArtKey => "奖励卡牌/053_隐匿魔力储备";
    protected override bool Spell => false;
    protected override decimal EnergyGain => 1m;
    protected override int CostUpgrade => -1;
    public ConcealedManaReserve() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenConcealedManaReservePower>(Owner.Creature, DynamicVars.Energy.BaseValue, Owner.Creature, this);
    }
}

public sealed class MasterApprenticeRhythm : FrierenSimpleCard
{
    public override string CardTitle => "师徒节奏";
    public override string CardDescription => "触发回路时抽1张牌。低费用回路：额外抽1张牌。";
    protected override string ArtKey => "奖励卡牌/054_师徒节奏";
    protected override bool Spell => false;
    protected override decimal Draw => 1m;
    protected override decimal DrawUpgrade => 1m;
    public MasterApprenticeRhythm() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenMasterApprenticeRhythmPower>(Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }
}

public sealed class MillenniumComposure : FrierenSimpleCard
{
    public override string CardTitle => "千年的从容";
    public override string CardDescription => "获得2层抑制。上回合没有打出攻击牌：回合开始获得{ConcealedMana}层隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/055_千年的从容";
    protected override bool Spell => false;
    protected override decimal ConcealedMana => 2m;
    protected override decimal ConcealedManaUpgrade => 1m;
    public MillenniumComposure() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenSuppressionPower>(Owner.Creature, 2m, Owner.Creature, this);
        await PowerCmd.Apply<FrierenMillenniumComposurePower>(Owner.Creature, DynamicVars["ConcealedMana"].BaseValue, Owner.Creature, this);
    }
}

public sealed class SlayerKillingMagic : FrierenSimpleCard
{
    public override string CardTitle => "葬送的杀人魔法";
    public override string CardDescription => "造成{Damage}点伤害。看破或10层解析：额外打出1次。";
    protected override string ArtKey => "奖励卡牌/056_葬送的杀人魔法";
    protected override decimal Damage => 18m;
    protected override decimal DamageUpgrade => 4m;
    public SlayerKillingMagic() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var hits = TargetHasAnalysis(cardPlay, 10m) || (cardPlay.Target?.GetPower<FrierenInsightPower>()?.Amount ?? 0m) > 0m ? 2 : 1;
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, hits);
    }
}

public sealed class MillenniumVolley : FrierenSimpleCard
{
    public override string CardTitle => "千年齐射";
    public override string CardDescription => "造成{Damage}点伤害6次。每消耗1层解析，获得1层隐匿魔力。消耗看破：抽1张牌。";
    protected override string ArtKey => "奖励卡牌/058_千年齐射";
    protected override decimal Damage => 5m;
    protected override decimal DamageUpgrade => 1m;
    protected override int HitCount => 6;
    public MillenniumVolley() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var before = GetTargetAnalysis(cardPlay);
        var insightBefore = cardPlay.Target?.GetPower<FrierenInsightPower>()?.Amount ?? 0m;
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, HitCount);
        var consumed = Math.Max(0m, before - GetTargetAnalysis(cardPlay));
        if (consumed > 0m)
        {
            await GainConcealedMana(consumed);
        }

        var insightAfter = cardPlay.Target?.GetPower<FrierenInsightPower>()?.Amount ?? 0m;
        if (insightAfter < insightBefore)
        {
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }
    }
}

public sealed class ManaOverwhelm : FrierenSimpleCard
{
    public override string CardTitle => "魔力压倒";
    public override string CardDescription => "对所有敌人造成{Damage}点伤害。失去所有隐匿魔力，追加其一半层数的伤害。失去至少10层：获得1层解放。";
    protected override string ArtKey => "奖励卡牌/059_魔力压倒";
    protected override bool AllEnemies => true;
    protected override decimal Damage => 12m;
    protected override decimal DamageUpgrade => 4m;
    public ManaOverwhelm() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var mana = Owner.Creature.GetPower<FrierenConcealedManaPower>();
        var lostMana = mana?.Amount ?? 0m;
        var bonus = Math.Floor(lostMana / 2m);
        if (mana != null)
        {
            await PowerCmd.Remove(mana);
        }

        if (CombatState == null)
        {
            return;
        }

        foreach (var enemy in CombatState.HittableEnemies)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue + bonus)
                .FromCard(this)
                .Targeting(enemy)
                .Execute(choiceContext);
        }

        if (lostMana >= 10m)
        {
            await PowerCmd.Apply<FrierenReleasePower>(Owner.Creature, 1m, Owner.Creature, this);
        }
    }
}

public sealed class CopyFernStrike : FrierenSimpleCard
{
    public override string CardTitle => "复写：菲伦的一击";
    public override string CardDescription => "造成{Damage}点伤害。上一张是技能：再打1次。上一张是普通魔法：抽1张牌。";
    protected override string ArtKey => "奖励卡牌/060_复写：菲伦的一击";
    protected override decimal Damage => 9m;
    protected override decimal DamageUpgrade => 3m;
    public CopyFernStrike() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var hits = WasPreviousCardSkill ? 2 : 1;
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue, hits);
        if (CombatTracker?.LastPlayedWasNormalMagic == true)
        {
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }
    }
}

public sealed class BoundaryBreakingRay : FrierenSimpleCard
{
    public override string CardTitle => "破界射线";
    public override string CardDescription => "移除目标所有格挡和1层人工制品。造成{Damage}点伤害。施加{Insight}层看破。";
    protected override string ArtKey => "奖励卡牌/061_破界射线";
    protected override decimal Damage => 16m;
    protected override decimal DamageUpgrade => 4m;
    protected override decimal Insight => 1m;
    protected override decimal InsightUpgrade => 1m;
    public BoundaryBreakingRay() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await RemoveTargetBlockAndArtifact(cardPlay);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        await ApplyTargetPowers(cardPlay);
    }
}

public sealed class LastLesson : FrierenSimpleCard
{
    public override string CardTitle => "最后一课";
    public override string CardDescription => "造成{Damage}点伤害。击杀：获得1层回忆和{ConcealedMana}层隐匿魔力。消耗。";
    protected override string ArtKey => "奖励卡牌/062_最后一课";
    protected override bool Exhaust => true;
    protected override decimal Damage => 7m;
    protected override decimal DamageUpgrade => 3m;
    protected override decimal ConcealedMana => 5m;
    public LastLesson() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DealSpellDamage(choiceContext, cardPlay, DynamicVars.Damage.BaseValue);
        if (cardPlay.Target.IsDead)
        {
            await FrierenMemoryPower.GainMemory(choiceContext, Owner.Creature, 1m, this);
            await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
        }
    }
}

public sealed class GetSerious : FrierenSimpleCard
{
    public override string CardTitle => "认真起来";
    public override string CardDescription => "获得{Energy}点能量。抽{Cards}张牌。消耗。";
    protected override string ArtKey => "奖励卡牌/064_认真起来";
    protected override bool Exhaust => true;
    protected override decimal EnergyGain => 1m;
    protected override decimal Draw => 2m;
    protected override decimal DrawUpgrade => 1m;
    public GetSerious() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
}

public sealed class MageIntuition : FrierenSimpleCard
{
    public override string CardTitle => "魔法使的直觉";
    public override string CardDescription => "抽牌直到手牌满。消耗。";
    protected override string ArtKey => "奖励卡牌/065_魔法使的直觉";
    protected override bool Exhaust => true;
    protected override int CostUpgrade => -1;
    public MageIntuition() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await DrawUntilHandFull(choiceContext);
    }
}

public sealed class SealGrimoire : FrierenSimpleCard
{
    public override string CardTitle => "封印魔导书";
    public override string CardDescription => "选择1张手牌，本回合费用变为0。消耗。";
    protected override string ArtKey => "奖励卡牌/066_封印魔导书";
    protected override bool Exhaust => true;
    protected override int CostUpgrade => -1;
    public SealGrimoire() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await MakeSelectedHandCardFreeThisTurn(choiceContext);
    }
}

public sealed class SilentCharge : FrierenSimpleCard
{
    public override string CardTitle => "静默蓄势";
    public override string CardDescription => "保留。获得{ConcealedMana}层隐匿魔力。本回合不能打出攻击牌。消耗。";
    protected override string ArtKey => "奖励卡牌/067_静默蓄势";
    protected override bool Retain => true;
    protected override bool Exhaust => true;
    protected override bool NoAttackThisTurn => true;
    protected override decimal ConcealedMana => 6m;
    protected override decimal ConcealedManaUpgrade => 2m;
    public SilentCharge() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
}

public sealed class AncientBarrier : FrierenSimpleCard
{
    public override string CardTitle => "古代屏障";
    public override string CardDescription => "获得{Block}点格挡。解放：下回合保留最多{RetainedBlock}点格挡。消耗。";
    protected override string ArtKey => "奖励卡牌/068_古代屏障";
    protected override bool Exhaust => true;
    protected override decimal Block => 28m;
    protected override decimal BlockUpgrade => 7m;
    protected override decimal RetainedBlock => 12m;
    protected override decimal RetainedBlockUpgrade => 4m;
    public AncientBarrier() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        await GainBlock(DynamicVars.Block.BaseValue, cardPlay);
        if ((Owner.Creature.GetPower<FrierenReleasePower>()?.Amount ?? 0m) > 0m)
        {
            var retainedBlock = Math.Min(Owner.Creature.Block, DynamicVars["RetainedBlock"].BaseValue);
            if (retainedBlock > 0m)
            {
                await PowerCmd.Apply<FrierenDelayedBlockPower>(Owner.Creature, retainedBlock, Owner.Creature, this);
            }
        }
    }
}

public sealed class LongJourney : FrierenSimpleCard
{
    public override string CardTitle => "漫长旅途";
    public override string CardDescription => "获得1层回忆。下回合按回忆层数抽牌，最多{Cards}张牌。消耗。";
    protected override string ArtKey => "奖励卡牌/070_漫长旅途";
    protected override bool Exhaust => true;
    protected override decimal Draw => 5m;
    protected override decimal DrawUpgrade => 2m;
    public LongJourney() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await FrierenMemoryPower.GainMemory(choiceContext, Owner.Creature, 1m, this);
        await PowerCmd.Apply<FrierenLongJourneyPower>(Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }
}

public sealed class SerieTeachingCard : FrierenSimpleCard
{
    public override string CardTitle => "赛丽艾的教诲";
    public override string CardDescription => "获得回忆：获得1层隐匿魔力和{Block}点格挡。每3层回忆：对所有敌人施加1层看破。";
    protected override string ArtKey => "奖励卡牌/072_赛丽艾的教诲";
    protected override bool Spell => false;
    protected override decimal Block => 3m;
    protected override decimal BlockUpgrade => 2m;
    public SerieTeachingCard() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenSerieTeachingPower>(Owner.Creature, DynamicVars.Block.BaseValue, Owner.Creature, this);
    }
}

public sealed class HimmelMemoryCard : FrierenSimpleCard
{
    public override string CardTitle => "辛美尔的回忆";
    public override string CardDescription => "每场战斗首次生命值低于50%：获得1层解放。抽2张牌。";
    protected override string ArtKey => "奖励卡牌/073_辛美尔的回忆";
    protected override bool Spell => false;
    protected override bool Innate => IsUpgraded;
    public HimmelMemoryCard() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenHimmelMemoryPower>(Owner.Creature, 1m, Owner.Creature, this);
    }
}

public sealed class FlammeTeachingCard : FrierenSimpleCard
{
    public override string CardTitle => "芙兰梅的教导";
    public override string CardDescription => "打出能力牌：获得{ConcealedMana}层隐匿魔力。首次获得解放：对所有敌人施加1层看破。";
    protected override string ArtKey => "奖励卡牌/074_芙兰梅的教导";
    protected override bool Spell => false;
    protected override decimal ConcealedMana => 4m;
    protected override decimal ConcealedManaUpgrade => 2m;
    public FlammeTeachingCard() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenFlammeTeachingPower>(Owner.Creature, DynamicVars["ConcealedMana"].BaseValue, Owner.Creature, this);
    }
}

public sealed class MagicEncyclopediaCard : FrierenSimpleCard
{
    public override string CardTitle => "魔法百科全书";
    public override string CardDescription => "触发回路时抽1张牌。3张法术名称不同：获得1点能量。";
    protected override string ArtKey => "奖励卡牌/075_魔法百科全书";
    protected override bool Spell => false;
    protected override int CostUpgrade => -1;
    public MagicEncyclopediaCard() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenMagicEncyclopediaPower>(Owner.Creature, 1m, Owner.Creature, this);
    }
}

public sealed class LimitedManaAuraCard : FrierenSimpleCard
{
    public override string CardTitle => "受限的魔力气息";
    public override string CardDescription => "每回合首次获得隐匿魔力时，对所有敌人施加{Analysis}层解析。";
    protected override string ArtKey => "奖励卡牌/076_受限的魔力气息";
    protected override bool Spell => false;
    protected override decimal Analysis => 2m;
    protected override decimal AnalysisUpgrade => 1m;
    public LimitedManaAuraCard() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenLimitedManaAuraPower>(Owner.Creature, DynamicVars["Analysis"].BaseValue, Owner.Creature, this);
    }
}

public sealed class DemonKillerCard : FrierenSimpleCard
{
    public override string CardTitle => "魔族杀手";
    public override string CardDescription => "敌人每回合首次因解析或看破失去生命时，施加{Vulnerable}层易伤。目标有看破：额外失去3点生命。";
    protected override string ArtKey => "奖励卡牌/077_魔族杀手";
    protected override bool Spell => false;
    protected override decimal Vulnerable => 1m;
    protected override int CostUpgrade => -1;
    public DemonKillerCard() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenDemonKillerPower>(Owner.Creature, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
    }
}

public sealed class NormalMagicMasteryCard : FrierenSimpleCard
{
    public override string CardTitle => "普通魔法精通";
    public override string CardDescription => "每回合前{Cards}张普通魔法费用为0。回忆：额外获得1层回忆，每回合最多1次。";
    protected override string ArtKey => "奖励卡牌/078_普通魔法精通";
    protected override bool Spell => false;
    protected override decimal Draw => 1m;
    protected override decimal DrawUpgrade => 1m;
    public NormalMagicMasteryCard() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenNormalMagicMasteryPower>(Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }
}

public sealed class MillenniumResearchCard : FrierenSimpleCard
{
    public override string CardTitle => "千年研究";
    public override string CardDescription => "回合开始时，随机升级{Cards}张手牌并获得1层回忆。回忆奖励：抽1张牌。";
    protected override string ArtKey => "奖励卡牌/079_千年研究";
    protected override bool Spell => false;
    protected override decimal Draw => 1m;
    protected override decimal DrawUpgrade => 1m;
    public MillenniumResearchCard() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenMillenniumResearchPower>(Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }
}

public sealed class HugeManaCard : FrierenSimpleCard
{
    public override string CardTitle => "巨大魔力";
    public override string CardDescription => "获得{ConcealedMana}层隐匿魔力和2层抑制。回合开始时获得2层隐匿魔力。";
    protected override string ArtKey => "奖励卡牌/080_巨大魔力";
    protected override bool Spell => false;
    protected override decimal ConcealedMana => 6m;
    protected override decimal ConcealedManaUpgrade => 2m;
    public HugeManaCard() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GainConcealedMana(DynamicVars["ConcealedMana"].BaseValue);
        await PowerCmd.Apply<FrierenSuppressionPower>(Owner.Creature, 2m, Owner.Creature, this);
        await PowerCmd.Apply<FrierenHugeManaPower>(Owner.Creature, 2m, Owner.Creature, this);
    }
}

public sealed class HeroesPartyMagicCard : FrierenSimpleCard
{
    public override string CardTitle => "勇者一行的魔法";
    public override string CardDescription => "回合开始时，上回合打过攻击、技能、能力：获得{Release}层解放。首次达成：获得1层回忆。";
    protected override string ArtKey => "奖励卡牌/081_勇者一行的魔法";
    protected override bool Spell => false;
    protected override decimal Release => 1m;
    protected override int CostUpgrade => -1;
    public HeroesPartyMagicCard() : base(2, CardType.Power, CardRarity.Ancient, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrierenHeroesPartyMagicPower>(Owner.Creature, DynamicVars["Release"].BaseValue, Owner.Creature, this);
    }
}

public sealed class FinalKillingMagic : FrierenSimpleCard
{
    public override string CardTitle => "最后的杀人魔法";
    public override string CardDescription => "造成{Damage}点伤害。本战每获得1层解放，费用-1，最低0。看破：目标失去{LifeLoss}点生命。消耗。";
    protected override string ArtKey => "奖励卡牌/082_最后的杀人魔法";
    protected override bool Exhaust => true;
    protected override decimal Damage => 50m;
    protected override decimal DamageUpgrade => 10m;
    protected override decimal LifeLoss => 25m;
    protected override decimal LifeLossUpgrade => 5m;
    public FinalKillingMagic() : base(4, CardType.Attack, CardRarity.Ancient, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.OnPlay(choiceContext, cardPlay);
        if ((cardPlay.Target?.GetPower<FrierenInsightPower>()?.Amount ?? 0m) > 0m)
        {
            await CreatureCmd.Damage(choiceContext, cardPlay.Target!, DynamicVars["LifeLoss"].BaseValue, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);
        }
    }
}

public sealed class TemporaryBasicKillingMagic : FrierenSimpleCard
{
    public override string CardTitle => "临时基础杀人魔法";
    public override string CardDescription => "造成{Damage}点伤害。施加{Analysis}层解析。消耗。";
    protected override string ArtKey => "生成牌与状态/085_临时基础杀人魔法";
    protected override bool Exhaust => true;
    protected override decimal Damage => 7m;
    protected override decimal Analysis => 2m;
    public TemporaryBasicKillingMagic() : base(0, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }
}

public sealed class MimicBiteMark : FrierenSimpleCard
{
    private bool _relicDrawTriggered;

    public override string CardTitle => "宝箱怪咬痕";
    public override string CardDescription => "不能打出。抽到时失去2点生命。消耗。";
    protected override string ArtKey => "生成牌与状态/086_宝箱怪咬痕";
    protected override bool Spell => false;
    protected override bool Unplayable => true;
    protected override bool Exhaust => true;
    public MimicBiteMark() : base(-2, CardType.Status, CardRarity.Basic, TargetType.None) { }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this)
        {
            return;
        }

        await CreatureCmd.Damage(choiceContext, Owner.Creature, 2m, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);
        if (!_relicDrawTriggered && FrierenRelicOwnership.HasRelic(Owner, typeof(MimicGrimoire)))
        {
            _relicDrawTriggered = true;
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }

        if (Pile?.Type == PileType.Hand)
        {
            await CardCmd.Exhaust(choiceContext, this);
        }
    }
}

public sealed class UselessSmallMagic : FrierenSimpleCard
{
    public override string CardTitle => "无用小魔法";
    public override string CardDescription => "获得1层回忆。消耗。";
    protected override string ArtKey => "生成牌与状态/087_无用小魔法";
    protected override bool NormalMagic => true;
    protected override bool GeneratedSmallMagic => true;
    protected override bool Exhaust => true;
    public UselessSmallMagic() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self) { }
}

public sealed class CoinFindingMagic : FrierenSimpleCard
{
    public override string CardTitle => "找铜币的魔法";
    public override string CardDescription => "获得{Block}点格挡。获得{ConcealedMana}层隐匿魔力。消耗。";
    protected override string ArtKey => "生成牌与状态/088_找铜币的魔法";
    protected override bool NormalMagic => true;
    protected override bool GeneratedSmallMagic => true;
    protected override bool Exhaust => true;
    protected override decimal Block => 2m;
    protected override decimal ConcealedMana => 1m;
    public CoinFindingMagic() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self) { }
}

public sealed class RustRemovalMagic : FrierenSimpleCard
{
    public override string CardTitle => "除锈魔法";
    public override string CardDescription => "消耗手牌中1张状态牌或诅咒牌。消耗成功：抽1张牌。消耗。";
    protected override string ArtKey => "生成牌与状态/089_除锈魔法";
    protected override bool NormalMagic => true;
    protected override bool GeneratedSmallMagic => true;
    protected override bool Exhaust => true;
    public RustRemovalMagic() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RecordNormalMagic(choiceContext);
        var candidates = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card != this && card.Type is CardType.Status or CardType.Curse)
            .ToList();
        var selected = await SelectOneCard(choiceContext, candidates, CardSelectorPrefs.ExhaustSelectionPrompt);
        if (selected != null)
        {
            await CardCmd.Exhaust(choiceContext, selected);
            await SeinsPrayer.TryGrantReleaseFromPurifiedCard(choiceContext, Owner.Creature, selected);
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }
    }
}

public static class FrierenCardCatalog
{
    public static readonly Type[] StandardRewardCardTypes =
    [
        typeof(KillingMagic),
        typeof(AntiMagicShot),
        typeof(PierceDefenseMagic),
        typeof(CloseRangeCast),
        typeof(RepeatedShot),
        typeof(MagicNeedle),
        typeof(FlickerFormula),
        typeof(ScatteredLightBullet),
        typeof(OldKillingMagic),
        typeof(DefensiveMagic),
        typeof(ManaDetection),
        typeof(CleanMagic),
        typeof(RepairMagic),
        typeof(AppraiseGrimoire),
        typeof(ExploreDungeon),
        typeof(SlowChant),
        typeof(QuietSteps),
        typeof(DispelMagic),
        typeof(ManaThread),
        typeof(SmallCollector),
        typeof(KillingMagicBarrage),
        typeof(CopyKillingMagic),
        typeof(AntiDemonFormula),
        typeof(PiercingAnalysis),
        typeof(MirrorShot),
        typeof(ManaBurst),
        typeof(EncirclingFireline),
        typeof(FlickerVolley),
        typeof(StaffStrikeAndCast),
        typeof(ReverseBarrierFormula),
        typeof(HiddenSnipe),
        typeof(VeteranRay),
        typeof(DeepManaSuppression),
        typeof(WeaknessAnalysis),
        typeof(LongChant),
        typeof(DefensiveBarrier),
        typeof(FlightMagic),
        typeof(MagicCollection),
        typeof(IdentifyMimic),
        typeof(RemoveCurseMagic),
        typeof(FernCover),
        typeof(CalmObservation),
        typeof(AncientGrimoire),
        typeof(HalfCenturyResearch),
        typeof(BarrierInversion),
        typeof(ChantPreservation),
        typeof(ManaConcealment),
        typeof(GrimoireCollectorCard),
        typeof(CombatAnalysisCard),
        typeof(EfficientChant),
        typeof(HumanMagicEra),
        typeof(FernDiscipline),
        typeof(ConcealedManaReserve),
        typeof(MasterApprenticeRhythm),
        typeof(MillenniumComposure),
        typeof(SlayerKillingMagic),
        typeof(FullPowerKillingMagic),
        typeof(MillenniumVolley),
        typeof(ManaOverwhelm),
        typeof(CopyFernStrike),
        typeof(BoundaryBreakingRay),
        typeof(LastLesson),
        typeof(FlowerFieldMagic),
        typeof(GetSerious),
        typeof(MageIntuition),
        typeof(SealGrimoire),
        typeof(SilentCharge),
        typeof(AncientBarrier),
        typeof(AnalyzeEverything),
        typeof(LongJourney),
        typeof(GreatMageFrieren),
        typeof(SerieTeachingCard),
        typeof(HimmelMemoryCard),
        typeof(FlammeTeachingCard),
        typeof(MagicEncyclopediaCard),
        typeof(LimitedManaAuraCard),
        typeof(DemonKillerCard),
        typeof(NormalMagicMasteryCard),
        typeof(MillenniumResearchCard),
        typeof(HugeManaCard)
    ];

    public static readonly Type[] AncientCardTypes =
    [
        typeof(HeroesPartyMagicCard),
        typeof(FinalKillingMagic)
    ];

    public static readonly Type[] RewardCardTypes = StandardRewardCardTypes
        .Concat(AncientCardTypes)
        .ToArray();

    private static readonly HashSet<Type> StandardRewardTypeSet = new(StandardRewardCardTypes);
    private static readonly HashSet<Type> AncientTypeSet = new(AncientCardTypes);

    public static readonly Type[] AllCardTypes =
    [
        typeof(FrierenStrike),
        typeof(FrierenDefend),
        typeof(KillingMagic),
        typeof(AntiMagicShot),
        typeof(PierceDefenseMagic),
        typeof(CloseRangeCast),
        typeof(RepeatedShot),
        typeof(MagicNeedle),
        typeof(FlickerFormula),
        typeof(ScatteredLightBullet),
        typeof(OldKillingMagic),
        typeof(DefensiveMagic),
        typeof(ManaDetection),
        typeof(CleanMagic),
        typeof(RepairMagic),
        typeof(AppraiseGrimoire),
        typeof(ExploreDungeon),
        typeof(SlowChant),
        typeof(QuietSteps),
        typeof(DispelMagic),
        typeof(ManaThread),
        typeof(SmallCollector),
        typeof(KillingMagicBarrage),
        typeof(CopyKillingMagic),
        typeof(AntiDemonFormula),
        typeof(PiercingAnalysis),
        typeof(MirrorShot),
        typeof(ManaBurst),
        typeof(EncirclingFireline),
        typeof(FlickerVolley),
        typeof(StaffStrikeAndCast),
        typeof(ReverseBarrierFormula),
        typeof(HiddenSnipe),
        typeof(VeteranRay),
        typeof(DeepManaSuppression),
        typeof(WeaknessAnalysis),
        typeof(LongChant),
        typeof(DefensiveBarrier),
        typeof(FlightMagic),
        typeof(MagicCollection),
        typeof(IdentifyMimic),
        typeof(RemoveCurseMagic),
        typeof(FernCover),
        typeof(CalmObservation),
        typeof(AncientGrimoire),
        typeof(HalfCenturyResearch),
        typeof(BarrierInversion),
        typeof(ChantPreservation),
        typeof(ManaConcealment),
        typeof(GrimoireCollectorCard),
        typeof(CombatAnalysisCard),
        typeof(EfficientChant),
        typeof(HumanMagicEra),
        typeof(FernDiscipline),
        typeof(ConcealedManaReserve),
        typeof(MasterApprenticeRhythm),
        typeof(MillenniumComposure),
        typeof(SlayerKillingMagic),
        typeof(FullPowerKillingMagic),
        typeof(MillenniumVolley),
        typeof(ManaOverwhelm),
        typeof(CopyFernStrike),
        typeof(BoundaryBreakingRay),
        typeof(LastLesson),
        typeof(FlowerFieldMagic),
        typeof(GetSerious),
        typeof(MageIntuition),
        typeof(SealGrimoire),
        typeof(SilentCharge),
        typeof(AncientBarrier),
        typeof(AnalyzeEverything),
        typeof(LongJourney),
        typeof(GreatMageFrieren),
        typeof(SerieTeachingCard),
        typeof(HimmelMemoryCard),
        typeof(FlammeTeachingCard),
        typeof(MagicEncyclopediaCard),
        typeof(LimitedManaAuraCard),
        typeof(DemonKillerCard),
        typeof(NormalMagicMasteryCard),
        typeof(MillenniumResearchCard),
        typeof(HugeManaCard),
        typeof(HeroesPartyMagicCard),
        typeof(FinalKillingMagic),
        typeof(BasicKillingMagic),
        typeof(ManaSuppression),
        typeof(TemporaryBasicKillingMagic),
        typeof(MimicBiteMark),
        typeof(UselessSmallMagic),
        typeof(CoinFindingMagic),
        typeof(RustRemovalMagic)
    ];

    public static CardModel[] GetAllCards()
    {
        var cardMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Card))!;
        return AllCardTypes
            .Select(type => (CardModel)cardMethod.MakeGenericMethod(type).Invoke(null, null)!)
            .ToArray();
    }

    public static CardModel[] GetRewardCards()
    {
        var cardMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Card))!;
        return RewardCardTypes
            .Select(type => (CardModel)cardMethod.MakeGenericMethod(type).Invoke(null, null)!)
            .ToArray();
    }

    public static CardModel[] GetStandardRewardCards()
    {
        var cardMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Card))!;
        return StandardRewardCardTypes
            .Select(type => (CardModel)cardMethod.MakeGenericMethod(type).Invoke(null, null)!)
            .ToArray();
    }

    public static CardModel[] GetAncientRewardCards()
    {
        var cardMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Card))!;
        return AncientCardTypes
            .Select(type => (CardModel)cardMethod.MakeGenericMethod(type).Invoke(null, null)!)
            .ToArray();
    }

    public static CardModel[] GetWeightedRewardCards()
    {
        var cardMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Card))!;
        var cards = new List<CardModel>();
        foreach (var type in StandardRewardCardTypes)
        {
            var card = (CardModel)cardMethod.MakeGenericMethod(type).Invoke(null, null)!;
            var weight = card is FrierenCard frierenCard ? GetTierWeight(frierenCard.DesignTier) : 10;
            for (var i = 0; i < weight; i++)
            {
                cards.Add(card);
            }
        }

        return cards.ToArray();
    }

    public static bool IsStandardRewardCard(CardModel card) => StandardRewardTypeSet.Contains(card.GetType());

    public static bool IsAncientRewardCard(CardModel card) => AncientTypeSet.Contains(card.GetType());

    public static bool ShouldExposeToGlobalRandomCardPool(CardModel card)
    {
        if (card is not FrierenCard)
        {
            return true;
        }

        return card is FrierenStrike
            or FrierenDefend
            or BasicKillingMagic
            or ManaSuppression
            || IsStandardRewardCard(card);
    }

    private static int GetTierWeight(string designTier) => designTier switch
    {
        "C1" => 13,
        "C2" => 10,
        "C3" => 8,
        "U1" => 12,
        "U2" => 10,
        "U3" => 7,
        "R1" => 11,
        "R2" => 9,
        "R3" => 6,
        "A" => 1,
        _ => 10
    };

    public static Dictionary<string, string> BuildLocalization()
    {
        var entries = new Dictionary<string, string>();
        var cardMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Card))!;
        foreach (var type in AllCardTypes)
        {
            if (cardMethod.MakeGenericMethod(type).Invoke(null, null) is not FrierenSimpleCard card)
            {
                continue;
            }

            var key = ModelDb.GetId(type).Entry;
            entries[$"{key}.title"] = card.CardTitle;
            entries[$"{key}.description"] = BuildCardDescription(card);
        }

        return entries;
    }

    private static string BuildCardDescription(FrierenSimpleCard card)
    {
        var description = card.CardDescription.Trim();
        if (!card.IsNormalMagic || description.Contains("普通魔法", StringComparison.Ordinal))
        {
            return description;
        }

        return string.IsNullOrWhiteSpace(description)
            ? "普通魔法。"
            : $"普通魔法。\n{description}";
    }
}
