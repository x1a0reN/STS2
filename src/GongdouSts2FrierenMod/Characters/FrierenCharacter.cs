using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using GongdouSts2FrierenMod.Assets;
using GongdouSts2FrierenMod.Cards;
using GongdouSts2FrierenMod.Relics;

namespace GongdouSts2FrierenMod.Characters;

public sealed class FrierenCharacter : CharacterModel
{
    public const string EnergyColor = "ironclad";

    public override CharacterGender Gender => CharacterGender.Feminine;
    protected override CharacterModel? UnlocksAfterRunAs => null;
    public override Color NameColor => new("8FD7FFFF");
    public override int StartingHp => 72;
    public override int StartingGold => 99;
    public override int MaxEnergy => 3;
    public override CardPoolModel CardPool => ModelDb.CardPool<FrierenCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<FrierenRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<FrierenPotionPool>();

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<FrierenStrike>(),
        ModelDb.Card<FrierenStrike>(),
        ModelDb.Card<FrierenStrike>(),
        ModelDb.Card<FrierenStrike>(),
        ModelDb.Card<FrierenDefend>(),
        ModelDb.Card<FrierenDefend>(),
        ModelDb.Card<FrierenDefend>(),
        ModelDb.Card<FrierenDefend>(),
        ModelDb.Card<BasicKillingMagic>(),
        ModelDb.Card<ManaSuppression>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<BlueMoonGrassBookmark>()];
    public override float AttackAnimDelay => 0.15f;
    public override float CastAnimDelay => 0.25f;
    public override Color EnergyLabelOutlineColor => new("335568FF");
    public override Color DialogueColor => new("234452");
    public override VfxColor SpeechBubbleColor => VfxColor.Cyan;
    public override Color MapDrawingColor => new("8FD7FFFF");
    public override Color RemoteTargetingLineColor => new("8FD7FFFF");
    public override Color RemoteTargetingLineOutline => new("335568FF");
    public override string CharacterSelectSfx => "event:/sfx/characters/ironclad/ironclad_select";
    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

    protected override string IconPath => FrierenAssetPaths.CharacterIconScene;
    protected override string CharacterSelectIconPath => FrierenAssetPaths.CharacterSelectIcon;
    protected override string CharacterSelectLockedIconPath => FrierenAssetPaths.CharacterSelectLockedIcon;
    protected override string MapMarkerPath => FrierenAssetPaths.MapMarker;

    public override List<string> GetArchitectAttackVfx() =>
    [
        "vfx/vfx_attack_blunt",
        "vfx/vfx_heavy_blunt",
        "vfx/vfx_attack_slash",
        "vfx/vfx_bloody_impact",
        "vfx/vfx_rock_shatter"
    ];
}
