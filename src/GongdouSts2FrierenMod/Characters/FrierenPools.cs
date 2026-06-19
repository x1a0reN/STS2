using Godot;
using MegaCrit.Sts2.Core.Models;
using GongdouSts2FrierenMod.Cards;
using GongdouSts2FrierenMod.Potions;
using GongdouSts2FrierenMod.Relics;

namespace GongdouSts2FrierenMod.Characters;

public sealed class FrierenCardPool : CardPoolModel
{
    public override string Title => "frieren";
    public override string EnergyColorName => FrierenCharacter.EnergyColor;
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color DeckEntryCardColor => new("8FD7FFFF");
    public override Color EnergyOutlineColor => new("335568FF");
    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() => FrierenCardCatalog.GetWeightedRewardCards();
}

public sealed class FrierenRelicPool : RelicPoolModel
{
    public override string EnergyColorName => FrierenCharacter.EnergyColor;
    public override Color LabOutlineColor => new("8FD7FF80");

    protected override IEnumerable<RelicModel> GenerateAllRelics() => FrierenRelicCatalog.GetStandardRelics();
}

public sealed class FrierenPotionPool : PotionPoolModel
{
    public override string EnergyColorName => FrierenCharacter.EnergyColor;
    public override Color LabOutlineColor => new("8FD7FF80");

    protected override IEnumerable<PotionModel> GenerateAllPotions() => FrierenPotionCatalog.GetAllPotions();
}
