using MegaCrit.Sts2.Core.Entities.Encounters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace GongdouSts2ChallengeMod.Monsters;

public sealed class GongdouCalcifiedCultistEncounter : EncounterModel
{
    public override RoomType RoomType => RoomType.Monster;
    public override bool ShouldGiveRewards => false;
    public override bool IsDebugEncounter => true;
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.GetById<GongdouCalcifiedCultistMonster>(ModelDb.GetId<GongdouCalcifiedCultistMonster>())];
    public override IEnumerable<EncounterTag> Tags => [];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var monster = ModelDb.GetById<GongdouCalcifiedCultistMonster>(
            ModelDb.GetId<GongdouCalcifiedCultistMonster>()).ToMutable();
        return [(monster, null)];
    }
}
