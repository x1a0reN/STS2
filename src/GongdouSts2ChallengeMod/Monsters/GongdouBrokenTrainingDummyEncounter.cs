using MegaCrit.Sts2.Core.Entities.Encounters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace GongdouSts2ChallengeMod.Monsters;

public sealed class GongdouBrokenTrainingDummyEncounter : EncounterModel
{
    public override RoomType RoomType => RoomType.Monster;
    public override bool ShouldGiveRewards => false;
    public override bool IsDebugEncounter => true;
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.GetById<GongdouBrokenTrainingDummyMonster>(ModelDb.GetId<GongdouBrokenTrainingDummyMonster>())];
    public override IEnumerable<EncounterTag> Tags => [];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var monster = ModelDb.GetById<GongdouBrokenTrainingDummyMonster>(
            ModelDb.GetId<GongdouBrokenTrainingDummyMonster>()).ToMutable();
        return [(monster, null)];
    }
}
