using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace GongdouSts2ChallengeMod.Cards;

public static class ChallengeCardFactory
{
    private static readonly Dictionary<string, Func<CardModel>> CardFactories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Comet"] = () =>
        {
            var card = Create<Comet>();
            card.SetToFreeThisCombat();
            return card;
        },
        ["Bury"] = Create<Bury>,
        ["BeamCell"] = Create<BeamCell>,
        ["BallLightning"] = Create<GongdouBallLightning>,
        ["D2BallLightning"] = Create<GongdouD2BallLightning>,
        ["D5BallLightning"] = Create<GongdouD5BallLightning>,
        ["Dismantle"] = Create<Dismantle>,
        ["Bludgeon"] = Create<Bludgeon>,
        ["TwinStrike"] = Create<TwinStrike>,
        ["Backstab"] = Create<Backstab>,
        ["DramaticEntrance"] = Create<DramaticEntrance>,
        ["Inflame"] = Create<Inflame>,
        ["Bloodletting"] = Create<Bloodletting>,
        ["Adrenaline"] = Create<Adrenaline>,
        ["FlashOfSteel"] = Create<FlashOfSteel>,
        ["PommelStrike"] = Create<PommelStrike>,
        ["BurningPact"] = Create<BurningPact>,
        ["TrueGrit"] = Create<TrueGrit>,
        ["SecondWind"] = Create<SecondWind>,
        ["FeelNoPain"] = Create<FeelNoPain>,
        ["DeadlyPoison"] = Create<DeadlyPoison>,
        ["NoxiousFumes"] = Create<NoxiousFumes>,
        ["BouncingFlask"] = Create<BouncingFlask>,
        ["BubbleBubble"] = Create<BubbleBubble>,
        ["Strike"] = Create<StrikeIronclad>,
        ["StrikeIronclad"] = Create<StrikeIronclad>,
        ["Defend"] = Create<DefendIronclad>,
        ["DefendIronclad"] = Create<DefendIronclad>,
        ["PerfectedStrike"] = Create<PerfectedStrike>,
        ["IronWave"] = Create<IronWave>,
        ["ShrugItOff"] = Create<ShrugItOff>,
        ["BodySlam"] = Create<BodySlam>,
        ["Uppercut"] = Create<Uppercut>,
        ["Finesse"] = Create<Finesse>,
        ["Prepared"] = Create<Prepared>,
        ["Neutralize"] = Create<Neutralize>,
        ["Survivor"] = Create<Survivor>,
        ["QuickSlash"] = Create<GongdouQuickSlash>,
        ["D2QuickSlash"] = Create<GongdouD2QuickSlash>,
        ["D5QuickSlash"] = Create<GongdouD5QuickSlash>,
        ["DaggerThrow"] = Create<GongdouDaggerThrow>,
        ["Clothesline"] = Create<GongdouClothesline>,
        ["D2Clothesline"] = Create<GongdouD2Clothesline>,
        ["D5Clothesline"] = Create<GongdouD5Clothesline>,
        ["ScratchBeam"] = Create<BeamCell>,
        ["ThunderStrike"] = Create<Thunderclap>,
        ["Thunderclap"] = Create<Thunderclap>,
        ["Bash"] = Create<Bash>,
        ["Hemokinesis"] = Create<Hemokinesis>,
        ["DoubleEnergy"] = Create<DoubleEnergy>,
        ["Skewer"] = Create<Skewer>,
        ["PoisonedStab"] = Create<PoisonedStab>,
        ["Slice"] = Create<Slice>,
        ["Catalyst"] = Create<GongdouCatalyst>,
        ["PreciseStab"] = Create<GongdouPreciseStab>,
        ["ArmorBreakSlash"] = Create<GongdouArmorBreakSlash>,
        ["HeavySwing"] = Create<GongdouHeavySwing>,
        ["DoubleHit"] = Create<GongdouDoubleHit>,
        ["OpportunityStrike"] = Create<GongdouOpportunityStrike>,
        ["PoisonDagger"] = Create<GongdouPoisonDagger>,
        ["PoisonFog"] = Create<GongdouPoisonFog>,
        ["CorrosiveSalve"] = Create<GongdouCorrosiveSalve>,
        ["BattleCry"] = Create<GongdouBattleCry>,
        ["PreparedStance"] = Create<GongdouPreparedStance>,
        ["BloodRush"] = Create<GongdouBloodRush>,
        ["Bluff"] = Create<GongdouBluff>
        ,
        ["D1HeavyHammer"] = Create<GongdouD1HeavyHammer>,
        ["FlyingSword"] = Create<SwordBoomerang>,
        ["D3Prepared"] = Create<GongdouD3Prepared>,
        ["D3Survivor"] = Create<GongdouD3Survivor>,
        ["D3Finisher"] = Create<GongdouD3Finisher>,
        ["D3BackstabCunning"] = Create<GongdouD3BackstabCunning>,
        ["D3Feint"] = Create<GongdouD3Feint>,
        ["D3ShadowStep"] = Create<GongdouD3ShadowStep>,
        ["D3VoidBlade"] = Create<GongdouD3VoidBlade>,
        ["D4Carnage"] = Create<GongdouD4Carnage>,
        ["D4BurningPact"] = Create<GongdouD4BurningPact>,
        ["D4TrueGrit"] = Create<GongdouD4TrueGrit>,
        ["D4Survivor"] = Create<GongdouD4Survivor>,
        ["D4FireBreathing"] = Create<GongdouD4FireBreathing>,
        ["D4Evolve"] = Create<GongdouD4Evolve>,
        ["D4WildStrike"] = Create<GongdouD4WildStrike>,
        ["D4RecklessCharge"] = Create<GongdouD4RecklessCharge>,
        ["D4Cleave"] = Create<GongdouD4Cleave>,
        ["D5VoidRend"] = Create<GongdouD5VoidRend>,
        ["D5ColdSnap"] = Create<GongdouD5ColdSnap>,
        ["D5Recycle"] = Create<GongdouD5Recycle>,
        ["D5Feint"] = Create<GongdouD5Feint>,
        ["D5Void"] = Create<GongdouD5Void>,
        ["D6Eruption"] = Create<GongdouD6Eruption>,
        ["D6Vigilance"] = Create<GongdouD6Vigilance>,
        ["D6EmptyFist"] = Create<GongdouD6EmptyFist>,
        ["D6EmptyBody"] = Create<GongdouD6EmptyBody>,
        ["D6FollowUp"] = Create<GongdouD6FollowUp>,
        ["D6WheelKick"] = Create<GongdouD6WheelKick>,
        ["D6Offering"] = Create<GongdouD6Offering>,
        ["D6CutThroughFate"] = Create<GongdouD6CutThroughFate>,
        ["D6BowlingBash"] = Create<GongdouD6BowlingBash>,
        ["D6Protect"] = Create<GongdouD6Protect>,
        ["D6Halt"] = Create<GongdouD6Halt>,
        ["D7DeadlyPoison"] = Create<GongdouD7DeadlyPoison>,
        ["D7BouncingFlask"] = Create<GongdouD7BouncingFlask>,
        ["D7PoisonedStab"] = Create<GongdouD7PoisonedStab>,
        ["D7Catalyst"] = Create<GongdouD7Catalyst>,
        ["D7NoxiousFumes"] = Create<GongdouD7NoxiousFumes>,
        ["D7Bane"] = Create<GongdouD7Bane>,
        ["D7Predator"] = Create<GongdouD7Predator>,
        ["D7LegSweep"] = Create<GongdouD7LegSweep>,
        ["D7CloakAndDagger"] = Create<GongdouD7CloakAndDagger>,
        ["D7Backflip"] = Create<GongdouD7Backflip>,
        ["D7Caltrops"] = Create<GongdouD7Caltrops>,
        ["D8Zap"] = Create<GongdouD8Zap>,
        ["D8Dualcast"] = Create<GongdouD8Dualcast>,
        ["D8Darkness"] = Create<GongdouD8Darkness>,
        ["D8Recursion"] = Create<GongdouD8Recursion>,
        ["D8Loop"] = Create<GongdouD8Loop>,
        ["D8Chill"] = Create<GongdouD8Chill>,
        ["D8ColdSnap"] = Create<GongdouD8ColdSnap>,
        ["D8BallLightningOrb"] = Create<GongdouD8BallLightningOrb>,
        ["D8Melter"] = Create<GongdouD8Melter>,
        ["D8Streamline"] = Create<GongdouD8Streamline>,
        ["D8Leap"] = Create<Leap>,
        ["D8Coolheaded"] = Create<GongdouD8Coolheaded>,
        ["D9Devotion"] = Create<GongdouD9Devotion>,
        ["D9Prostrate"] = Create<GongdouD9Prostrate>,
        ["D9Prayer"] = Create<GongdouD9Prayer>,
        ["D9Worship"] = Create<GongdouD9Worship>,
        ["D9Brilliance"] = Create<GongdouD9Brilliance>,
        ["D9Ragnarok"] = Create<GongdouD9Ragnarok>,
        ["D9Judgment"] = Create<GongdouD9Judgment>,
        ["D9CarveReality"] = Create<GongdouD9CarveReality>,
        ["D9Smite"] = Create<GongdouD9Smite>,
        ["D9Offering"] = Create<GongdouD9Offering>,
        ["D9Sanctity"] = Create<GongdouD9Sanctity>,
        ["D9Wallop"] = Create<GongdouD9Wallop>,
        ["D9EmptyBody"] = Create<GongdouD9EmptyBody>,
        ["D10TimeSeal"] = Create<GongdouD10TimeSeal>,
        ["D10ChargeStance"] = Create<GongdouD10ChargeStance>,
        ["D10RiftMark"] = Create<GongdouD10RiftMark>,
        ["D10EchoStrike"] = Create<GongdouD10EchoStrike>,
        ["D10EchoForm"] = Create<GongdouD10EchoForm>,
        ["D10DelayedBlast"] = Create<GongdouD10DelayedBlast>,
        ["D10OverloadRay"] = Create<GongdouD10OverloadRay>,
        ["D10VentHeat"] = Create<GongdouD10VentHeat>,
        ["D10PhaseBarrier"] = Create<GongdouD10PhaseBarrier>,
        ["D10FocusCalibrate"] = Create<GongdouD10FocusCalibrate>,
        ["D10FinalCommand"] = Create<GongdouD10FinalCommand>,
        ["D10MirrorPreview"] = Create<GongdouD10MirrorPreview>,
        ["D10BurningShot"] = Create<GongdouD10BurningShot>,
        ["D10CoolingLoop"] = Create<GongdouD10CoolingLoop>,
        ["D10IdleProgram"] = Create<GongdouD10IdleProgram>,
        ["D10SpikeMark"] = Create<GongdouD10SpikeMark>
    };

    public static CardModel CreateById(string id)
    {
        if (CardFactories.TryGetValue(Normalize(id), out var factory))
        {
            return factory();
        }

        throw new KeyNotFoundException($"Unsupported STS2 challenge card id: {id}");
    }

    private static CardModel Create<T>() where T : CardModel
    {
        return ModelDb.Card<T>().ToMutable();
    }

    private static string Normalize(string id)
    {
        return id.Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Trim();
    }
}
