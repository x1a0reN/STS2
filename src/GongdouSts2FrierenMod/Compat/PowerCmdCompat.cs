using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GongdouSts2FrierenMod.Compat;

internal static class PowerCmdCompat
{
    private static ThrowingPlayerChoiceContext ChoiceContext => new();

    public static Task<IReadOnlyList<T>> Apply<T>(
        IEnumerable<Creature> targets,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
    {
        return PowerCmd.Apply<T>(ChoiceContext, targets, amount, applier, cardSource, silent);
    }

    public static Task<T?> Apply<T>(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
    {
        return PowerCmd.Apply<T>(ChoiceContext, target, amount, applier, cardSource, silent);
    }

    public static Task<IReadOnlyList<T>> Apply<T>(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
    {
        return PowerCmd.Apply<T>(choiceContext, targets, amount, applier, cardSource, silent);
    }

    public static Task<T?> Apply<T>(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
    {
        return PowerCmd.Apply<T>(choiceContext, target, amount, applier, cardSource, silent);
    }

    public static Task Apply(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
    {
        return PowerCmd.Apply(choiceContext, power, target, amount, applier, cardSource, silent);
    }

    public static Task<int> ModifyAmount(
        PowerModel power,
        decimal offset,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
    {
        return PowerCmd.ModifyAmount(ChoiceContext, power, offset, applier, cardSource, silent);
    }

    public static Task<int> ModifyAmount(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal offset,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
    {
        return PowerCmd.ModifyAmount(choiceContext, power, offset, applier, cardSource, silent);
    }

    public static Task Decrement(PowerModel power) => PowerCmd.Decrement(power);

    public static Task TickDownDuration(PowerModel power) => PowerCmd.TickDownDuration(power);

    public static Task Remove<T>(Creature creature)
        where T : PowerModel
    {
        return PowerCmd.Remove<T>(creature);
    }

    public static Task Remove(PowerModel? power) => PowerCmd.Remove(power);
}
