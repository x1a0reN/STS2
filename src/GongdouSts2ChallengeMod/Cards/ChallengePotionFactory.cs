using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;

namespace GongdouSts2ChallengeMod.Cards;

public static class ChallengePotionFactory
{
    private static readonly Dictionary<string, Func<PotionModel>> PotionFactories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FirePotion"] = Create<FirePotion>,
        ["EnergyPotion"] = Create<EnergyPotion>,
        ["GhostInAJar"] = Create<GhostInAJar>,
        ["SwiftPotion"] = Create<SwiftPotion>,
        ["StrengthPotion"] = Create<StrengthPotion>,
        ["PoisonPotion"] = Create<PoisonPotion>,
        ["VulnerablePotion"] = Create<VulnerablePotion>,
        ["WeakPotion"] = Create<WeakPotion>,
        ["GamblersBrew"] = Create<GamblersBrew>,
        ["BurstPotion"] = Create<GongdouBurstPotion>,
        ["ExplosiveAmpoule"] = Create<GongdouBurstPotion>,
        ["D2FirePotion"] = Create<GongdouD2FirePotion>,
        ["D3CunningPotion"] = Create<GongdouD3CunningPotion>,
        ["D4FirePotion"] = Create<GongdouD4FirePotion>,
        ["D4ClarityPotion"] = Create<GongdouD4ClarityPotion>,
        ["D4GhostPotion"] = Create<GongdouD4GhostPotion>,
        ["D5BarrierPotion"] = Create<GongdouD5BarrierPotion>,
        ["D5EnergyPotion"] = Create<GongdouD5EnergyPotion>,
        ["D6RagePotion"] = Create<GongdouD6RagePotion>,
        ["D6CalmPotion"] = Create<GongdouD6CalmPotion>,
        ["D6FirePotion"] = Create<GongdouD6FirePotion>,
        ["D7PoisonPotion"] = Create<GongdouD7PoisonPotion>,
        ["D7CatalystPotion"] = Create<GongdouD7CatalystPotion>,
        ["D8FocusPotion"] = Create<GongdouD8FocusPotion>,
        ["D8DarkPotion"] = Create<GongdouD8DarkPotion>,
        ["D8EvokePotion"] = Create<GongdouD8EvokePotion>,
        ["D9DivinityPotion"] = Create<GongdouD9DivinityPotion>,
        ["D9MantraPotion"] = Create<GongdouD9MantraPotion>,
        ["D9MirrorBreakPotion"] = Create<GongdouD9MirrorBreakPotion>,
        ["D10TimePotion"] = Create<GongdouD10TimePotion>,
        ["D10EchoPotion"] = Create<GongdouD10EchoPotion>,
        ["D10ShatterArmorPotion"] = Create<GongdouD10ShatterArmorPotion>
    };

    public static PotionModel CreateById(string id)
    {
        if (PotionFactories.TryGetValue(Normalize(id), out var factory))
        {
            return factory();
        }

        throw new KeyNotFoundException($"Unsupported STS2 challenge potion id: {id}");
    }

    private static PotionModel Create<T>() where T : PotionModel
    {
        return ModelDb.Potion<T>().ToMutable();
    }

    private static string Normalize(string id)
    {
        return id.Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Trim();
    }
}
