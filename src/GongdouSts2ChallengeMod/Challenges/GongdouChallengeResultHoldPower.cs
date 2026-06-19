using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace GongdouSts2ChallengeMod.Challenges;

public sealed class GongdouChallengeResultHoldPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override bool ShouldStopCombatFromEnding()
    {
        return ChallengeSessionManager.ShouldHoldCompletedCombat;
    }
}
