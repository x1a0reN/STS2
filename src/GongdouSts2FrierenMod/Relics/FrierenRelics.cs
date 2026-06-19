using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using GongdouSts2FrierenMod.Assets;
using GongdouSts2FrierenMod.Cards;
using GongdouSts2FrierenMod.Powers;

namespace GongdouSts2FrierenMod.Relics;

public abstract class FrierenRelic : RelicModel
{
    public virtual string FrierenIconKey => IconBaseName;
    public string FrierenIconPath => FrierenAssetPaths.RelicIcon(FrierenIconKey);
    public string FrierenIconRelativePath => FrierenAssetPaths.RelicRelativePath(FrierenIconKey);
    protected override IEnumerable<IHoverTip> ExtraHoverTips => FrierenKeywordTips.FromText(DynamicDescription.GetFormattedText());
}

internal static class FrierenRelicOwnership
{
    public static bool HasRelic(Player? player, Type relicType)
    {
        if (player == null)
        {
            return false;
        }

        return EnumerateRelics(player).Any(relic => relicType.IsInstanceOfType(relic));
    }

    public static T? FindRelic<T>(Player? player) where T : RelicModel
    {
        if (player == null)
        {
            return null;
        }

        return EnumerateRelics(player).OfType<T>().FirstOrDefault();
    }

    private static IEnumerable<RelicModel> EnumerateRelics(Player player)
    {
        var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        return EnumerateRelicsFrom(player, visited, 0);
    }

    private static IEnumerable<RelicModel> EnumerateRelicsFrom(object? source, HashSet<object> visited, int depth)
    {
        if (source == null || depth > 3 || !visited.Add(source))
        {
            yield break;
        }

        if (source is RelicModel relic)
        {
            yield return relic;
            yield break;
        }

        if (source is System.Collections.IEnumerable enumerable && source is not string)
        {
            foreach (var item in enumerable)
            {
                foreach (var nestedRelic in EnumerateRelicsFrom(item, visited, depth + 1))
                {
                    yield return nestedRelic;
                }
            }
        }

        var flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;
        foreach (var property in source.GetType().GetProperties(flags))
        {
            if (property.GetIndexParameters().Length > 0 || !property.Name.Contains("Relic", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(source);
            }
            catch
            {
                continue;
            }

            foreach (var nestedRelic in EnumerateRelicsFrom(value, visited, depth + 1))
            {
                yield return nestedRelic;
            }
        }

        foreach (var field in source.GetType().GetFields(flags))
        {
            if (!field.Name.Contains("Relic", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            object? value;
            try
            {
                value = field.GetValue(source);
            }
            catch
            {
                continue;
            }

            foreach (var nestedRelic in EnumerateRelicsFrom(value, visited, depth + 1))
            {
                yield return nestedRelic;
            }
        }
    }

    public static async Task<bool> TryObtainRelicAsync(Player player, RelicModel relic)
    {
        var flags = System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;

        foreach (var method in typeof(RelicCmd).GetMethods(flags)
                     .Where(method => !method.ContainsGenericParameters)
                     .Where(method => method.Name.Contains("Obtain", StringComparison.OrdinalIgnoreCase)
                                      || method.Name.Contains("Grant", StringComparison.OrdinalIgnoreCase)))
        {
            if (!TryBuildRelicCommandArgs(method.GetParameters(), player, relic, out var args))
            {
                continue;
            }

            var result = method.Invoke(null, args);
            if (result is Task task)
            {
                await task;
            }

            return true;
        }

        return false;
    }

    public static bool TryCreateRelicReward(Player player, RelicModel relic, out Reward? reward)
    {
        reward = null;
        var rewardType = typeof(Reward).Assembly.GetType("MegaCrit.Sts2.Core.Rewards.RelicReward");
        if (rewardType == null || !typeof(Reward).IsAssignableFrom(rewardType))
        {
            return false;
        }

        var flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;
        foreach (var constructor in rewardType.GetConstructors(flags))
        {
            var parameters = constructor.GetParameters();
            if (!parameters.Any(ParameterCanReceiveRelic) || !TryBuildRelicCommandArgs(parameters, player, relic, out var args))
            {
                continue;
            }

            reward = constructor.Invoke(args) as Reward;
            if (reward != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ParameterCanReceiveRelic(System.Reflection.ParameterInfo parameter)
    {
        var parameterType = parameter.ParameterType;
        return parameterType.IsAssignableFrom(typeof(RelicModel))
            || typeof(RelicModel).IsAssignableFrom(parameterType)
            || parameterType.IsArray && parameterType.GetElementType()?.IsAssignableFrom(typeof(RelicModel)) == true
            || parameterType.IsAssignableFrom(typeof(List<RelicModel>))
            || parameterType.IsAssignableFrom(typeof(IEnumerable<RelicModel>));
    }

    private static bool TryBuildRelicCommandArgs(
        System.Reflection.ParameterInfo[] parameters,
        Player player,
        RelicModel relic,
        out object?[] args)
    {
        args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsInstanceOfType(player))
            {
                args[i] = player;
            }
            else if (parameterType.IsInstanceOfType(relic))
            {
                args[i] = relic;
            }
            else if (parameterType.IsArray && parameterType.GetElementType()?.IsAssignableFrom(typeof(RelicModel)) == true)
            {
                var array = Array.CreateInstance(parameterType.GetElementType()!, 1);
                array.SetValue(relic, 0);
                args[i] = array;
            }
            else if (parameterType.IsAssignableFrom(typeof(List<RelicModel>)))
            {
                args[i] = new List<RelicModel> { relic };
            }
            else if (parameterType.IsAssignableFrom(typeof(IEnumerable<RelicModel>)))
            {
                args[i] = new[] { relic };
            }
            else if (parameterType == typeof(bool))
            {
                args[i] = false;
            }
            else if (parameterType == typeof(int))
            {
                args[i] = 0;
            }
            else if (parameterType == typeof(decimal))
            {
                args[i] = 0m;
            }
            else if (parameterType.IsEnum)
            {
                args[i] = Enum.GetValues(parameterType).GetValue(0);
            }
            else if (parameters[i].HasDefaultValue)
            {
                args[i] = parameters[i].DefaultValue;
            }
            else if (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null)
            {
                args[i] = null;
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class BlueMoonGrassBookmark : FrierenRelic
{
    public override string FrierenIconKey => "090_蓝月草书签";
    protected override string IconBaseName => "bag_of_preparation";
    public override RelicRarity Rarity => RelicRarity.Starter;

    private bool _firstReleaseDrawn;
    private int _pendingDraws;
    private bool _suppressedByFadedGrimoire;
    private bool _bossReplacementGranted;
    private bool _bossReplacementRewardOffered;

    public override async Task BeforeCombatStart()
    {
        _firstReleaseDrawn = false;
        _pendingDraws = 0;
        _bossReplacementGranted = false;
        _bossReplacementRewardOffered = false;
        _suppressedByFadedGrimoire = FrierenRelicOwnership.HasRelic(Owner, typeof(FadedGrimoire));
        if (_suppressedByFadedGrimoire)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<FrierenConcealedManaPower>(Owner.Creature, 3m, Owner.Creature, null);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (_suppressedByFadedGrimoire || player != Owner || _pendingDraws <= 0)
        {
            return;
        }

        var draws = _pendingDraws;
        _pendingDraws = 0;
        await CardPileCmd.Draw(choiceContext, draws, Owner);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (_suppressedByFadedGrimoire || _firstReleaseDrawn || amount <= 0 || power is not FrierenReleasePower || power.Owner != Owner.Creature)
        {
            return;
        }

        _firstReleaseDrawn = true;
        Flash();
        _pendingDraws++;
        await Task.CompletedTask;
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (_bossReplacementGranted
            || _bossReplacementRewardOffered
            || room.RoomType != RoomType.Boss
            || FrierenRelicOwnership.HasRelic(Owner, typeof(FadedGrimoire)))
        {
            return;
        }

        var fadedGrimoire = ModelDb.Relic<FadedGrimoire>();
        if (await FrierenRelicOwnership.TryObtainRelicAsync(Owner, fadedGrimoire))
        {
            _bossReplacementGranted = true;
            Flash();
        }
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (_bossReplacementRewardOffered
            || player != Owner
            || room?.RoomType != RoomType.Boss
            || FrierenRelicOwnership.HasRelic(Owner, typeof(FadedGrimoire)))
        {
            return false;
        }

        var fadedGrimoire = ModelDb.Relic<FadedGrimoire>();
        if (!FrierenRelicOwnership.TryCreateRelicReward(player, fadedGrimoire, out var reward) || reward == null)
        {
            return false;
        }

        rewards.Add(reward);
        _bossReplacementRewardOffered = true;
        return true;
    }
}

public sealed class FadedGrimoire : FrierenRelic
{
    public override string FrierenIconKey => "091_褪色的魔导书";
    protected override string IconBaseName => "necronomicon";
    public override RelicRarity Rarity => RelicRarity.Ancient;
    private int _pendingDraws;

    public override async Task BeforeCombatStart()
    {
        _pendingDraws = 0;
        Flash();
        await PowerCmd.Apply<FrierenConcealedManaPower>(Owner.Creature, 4m, Owner.Creature, null);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player != Owner || _pendingDraws <= 0)
        {
            return;
        }

        var draws = _pendingDraws;
        _pendingDraws = 0;
        await CardPileCmd.Draw(choiceContext, draws, Owner);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0 || power is not FrierenReleasePower || power.Owner != Owner.Creature)
        {
            return;
        }

        Flash();
        _pendingDraws++;
        await Task.CompletedTask;
    }
}

public sealed class SmallSuitcase : FrierenRelic
{
    public override string FrierenIconKey => "092_小型手提箱";
    protected override string IconBaseName => "toolbox";
    public override RelicRarity Rarity => RelicRarity.Common;
    private bool _used;

    public override Task BeforeCombatStart()
    {
        _used = false;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (_used || cardPlay.Card is not FrierenCard { IsNormalMagic: true })
        {
            return;
        }

        _used = true;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, 4m, ValueProp.Move, cardPlay);
    }
}

public sealed class TatteredMagicPage : FrierenRelic
{
    public override string FrierenIconKey => "093_破旧魔法书页";
    protected override string IconBaseName => "paper_phrog";
    public override RelicRarity Rarity => RelicRarity.Common;
    private bool _usedThisCombat;

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }

    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource)
    {
        if (!_usedThisCombat && power is FrierenAnalysisPower && target?.GetPower<FrierenAnalysisPower>() == null)
        {
            _usedThisCombat = true;
            Flash();
            return 2m;
        }

        return 0m;
    }
}

public sealed class FernHairOrnament : FrierenRelic
{
    public override string FrierenIconKey => "094_菲伦的发饰";
    protected override string IconBaseName => "magic_flower";
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    private bool _usedThisTurn;
    private int _pendingDraws;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player == Owner)
        {
            _usedThisTurn = false;
            if (_pendingDraws > 0)
            {
                var draws = _pendingDraws;
                _pendingDraws = 0;
                await CardPileCmd.Draw(choiceContext, draws, Owner);
            }
        }
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (_usedThisTurn || amount >= 0 || power is not FrierenReleasePower || power.Owner != Owner.Creature)
        {
            return;
        }

        _usedThisTurn = true;
        Flash();
        _pendingDraws++;
        await Task.CompletedTask;
    }
}

public sealed class SeinsPrayer : FrierenRelic
{
    public override string FrierenIconKey => "095_赞因的祷言";
    protected override string IconBaseName => "regal_pillow";
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    private bool _releaseGranted;

    public override Task BeforeCombatStart()
    {
        _releaseGranted = false;
        return Task.CompletedTask;
    }

    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount)
    {
        return creature == Owner.Creature ? amount + 6m : amount;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (_releaseGranted || amount >= 0 || power.Owner != Owner.Creature || power.Type != PowerType.Debuff)
        {
            return;
        }

        _releaseGranted = true;
        Flash();
        await PowerCmd.Apply<FrierenReleasePower>(Owner.Creature, 1m, Owner.Creature, null);
    }

    public static async Task TryGrantReleaseFromPurifiedCard(PlayerChoiceContext choiceContext, Creature owner, CardModel card)
    {
        if (card.Type is not (CardType.Status or CardType.Curse))
        {
            return;
        }

        var relic = FrierenRelicOwnership.FindRelic<SeinsPrayer>(owner.Player);
        if (relic == null || relic._releaseGranted)
        {
            return;
        }

        relic._releaseGranted = true;
        relic.Flash();
        await PowerCmd.Apply<FrierenReleasePower>(owner, 1m, owner, card);
    }
}

public sealed class MimicGrimoire : FrierenRelic
{
    public override string FrierenIconKey => "096_宝箱怪图鉴";
    protected override string IconBaseName => "nloths_gift";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task BeforeCombatStart()
    {
        var card = ICardScope.DebugOnlyGet(MegaCrit.Sts2.Core.Entities.Cards.CardScope.Combat)
            .CreateCard<MimicBiteMark>(Owner);
        Flash();
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, creator: null);
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner || room?.RoomType != RoomType.Treasure)
        {
            return false;
        }

        Flash();
        var options = CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool]);
        rewards.Add(new CardReward(options, 1, player));
        return true;
    }
}

public sealed class FlammesNotes : FrierenRelic
{
    public override string FrierenIconKey => "097_芙兰梅的手记";
    protected override string IconBaseName => "captains_wheel";
    public override RelicRarity Rarity => RelicRarity.Rare;
    private bool _releaseGranted;

    public override Task BeforeCombatStart()
    {
        _releaseGranted = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power is FrierenMemoryPower && power.Owner == Owner.Creature && amount > 0)
        {
            Flash();
            await PowerCmd.Apply<FrierenConcealedManaPower>(Owner.Creature, 2m, Owner.Creature, null);
            if (!_releaseGranted && power.Amount >= 6m)
            {
                _releaseGranted = true;
                await PowerCmd.Apply<FrierenReleasePower>(Owner.Creature, 1m, Owner.Creature, null);
            }
        }
    }
}

public sealed class SeriesCertificate : FrierenRelic
{
    public override string FrierenIconKey => "098_赛丽艾的证书";
    protected override string IconBaseName => "certificate";
    public override RelicRarity Rarity => RelicRarity.Rare;
    private bool _used;

    public override Task BeforeCombatStart()
    {
        _used = false;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (_used || (cardPlay.Card.Rarity != CardRarity.Rare && cardPlay.Card.Rarity != CardRarity.Ancient))
        {
            return;
        }

        _used = true;
        Flash();
        await PowerCmd.Apply<FrierenReleasePower>(Owner.Creature, 1m, Owner.Creature, null);
    }
}

public sealed class HimmelsRing : FrierenRelic
{
    public override string FrierenIconKey => "099_欣梅尔的戒指";
    protected override string IconBaseName => "snake_ring";
    public override RelicRarity Rarity => RelicRarity.Event;
    private bool _used;

    public override async Task BeforeCombatStart()
    {
        _used = false;
        if (Owner.Creature.CombatState?.Encounter?.RoomType == RoomType.Boss)
        {
            Flash();
            await PowerCmd.Apply<FrierenReleasePower>(Owner.Creature, 1m, Owner.Creature, null);
        }
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (_used || creature != Owner.Creature || Owner.Creature.GetHpPercentRemaining() > 0.25)
        {
            return;
        }

        _used = true;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, 12m, ValueProp.Move, null);
        if (Owner.Creature.CombatState != null)
        {
            foreach (var enemy in Owner.Creature.CombatState.HittableEnemies)
            {
                await PowerCmd.Apply<FrierenInsightPower>(enemy, 1m, Owner.Creature, null);
            }
        }
    }
}

public static class FrierenRelicCatalog
{
    public static readonly Type[] StandardRelicTypes =
    [
        typeof(BlueMoonGrassBookmark),
        typeof(SmallSuitcase),
        typeof(TatteredMagicPage),
        typeof(FernHairOrnament),
        typeof(SeinsPrayer),
        typeof(MimicGrimoire),
        typeof(FlammesNotes),
        typeof(SeriesCertificate),
        typeof(HimmelsRing)
    ];

    public static readonly Type[] BossReplacementRelicTypes =
    [
        typeof(FadedGrimoire)
    ];

    public static readonly Type[] AllRelicTypes = StandardRelicTypes
        .Concat(BossReplacementRelicTypes)
        .ToArray();

    public static RelicModel[] GetAllRelics()
    {
        var relicMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Relic))!;
        return AllRelicTypes
            .Select(type => (RelicModel)relicMethod.MakeGenericMethod(type).Invoke(null, null)!)
            .ToArray();
    }

    public static RelicModel[] GetStandardRelics()
    {
        var relicMethod = typeof(ModelDb).GetMethod(nameof(ModelDb.Relic))!;
        return StandardRelicTypes
            .Select(type => (RelicModel)relicMethod.MakeGenericMethod(type).Invoke(null, null)!)
            .ToArray();
    }
}
