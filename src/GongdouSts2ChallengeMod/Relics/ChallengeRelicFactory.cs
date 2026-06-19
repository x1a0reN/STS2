using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace GongdouSts2ChallengeMod.Relics;

public static class ChallengeRelicFactory
{
    private static readonly Dictionary<string, Func<RelicModel>> RelicFactories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TacticalMemoClip"] = Create<GongdouTacticalMemoClip>,
        ["CrackedBladeCharm"] = Create<GongdouCrackedBladeCharm>,
        ["PoisonSac"] = Create<GongdouPoisonSac>,
        ["BloodSigil"] = Create<GongdouBloodSigil>,
        ["OldWhetstone"] = Create<GongdouOldWhetstone>,
        ["SlowWarmCore"] = Create<GongdouSlowWarmCore>,
        ["BagOfPreparation"] = Create<BagOfPreparation>,
        ["Akabeko"] = Create<Akabeko>,
        ["BagOfMarbles"] = Create<BagOfMarbles>,
        ["Whetstone"] = Create<Whetstone>,
        ["HappyFlower"] = Create<HappyFlower>,
        ["RedSkull"] = Create<RedSkull>,
        ["D3SharpDice"] = Create<GongdouD3SharpDice>,
        ["D3ReturnHolster"] = Create<GongdouD3ReturnHolster>,
        ["D3HollowCharm"] = Create<GongdouD3HollowCharm>,
        ["D5Shuriken"] = Create<GongdouD5Shuriken>,
        ["D5Anchor"] = Create<GongdouD5Anchor>,
        ["D5VoidLens"] = Create<GongdouD5VoidLens>,
        ["D6VioletLotus"] = Create<GongdouD6VioletLotus>,
        ["D6RageCharm"] = Create<GongdouD6RageCharm>,
        ["D6StanceSeal"] = Create<GongdouD6StanceSeal>,
        ["D7SnakeSkull"] = Create<GongdouD7SnakeSkull>,
        ["D7PoisonFunnel"] = Create<GongdouD7PoisonFunnel>,
        ["D7RingOfNeedles"] = Create<GongdouD7RingOfNeedles>,
        ["D8DataDisk"] = Create<GongdouD8DataDisk>,
        ["D8GoldCable"] = Create<GongdouD8GoldCable>,
        ["D8DarkCore"] = Create<GongdouD8DarkCore>,
        ["D9Damaru"] = Create<GongdouD9Damaru>,
        ["D9Scripture"] = Create<GongdouD9Scripture>,
        ["D9SundialShard"] = Create<GongdouD9SundialShard>,
        ["D10WatchCore"] = Create<GongdouD10WatchCore>,
        ["D10Resonator"] = Create<GongdouD10Resonator>,
        ["D10PrismShard"] = Create<GongdouD10PrismShard>
    };

    public static RelicModel CreateById(string id)
    {
        if (RelicFactories.TryGetValue(Normalize(id), out var factory))
        {
            return factory();
        }

        throw new KeyNotFoundException($"Unsupported STS2 challenge relic id: {id}");
    }

    private static RelicModel Create<T>() where T : RelicModel
    {
        return ModelDb.Relic<T>().ToMutable();
    }

    private static string Normalize(string id)
    {
        return id.Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Trim();
    }
}
