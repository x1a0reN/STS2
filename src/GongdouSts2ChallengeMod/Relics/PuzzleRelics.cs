using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace GongdouSts2ChallengeMod.Relics;

public abstract class GongdouFixedRelic : GongdouChallengeRelic
{
    private readonly string _icon;
    private readonly decimal _startBlock;
    private readonly decimal _startStrength;

    protected GongdouFixedRelic(string icon, decimal startBlock = 0m, decimal startStrength = 0m)
    {
        _icon = icon;
        _startBlock = startBlock;
        _startStrength = startStrength;
    }

    protected override string IconBaseName => _icon;

    public override async Task BeforeCombatStart()
    {
        if (_startBlock > 0)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner.Creature, _startBlock, ValueProp.Unpowered, null);
        }

        if (_startStrength > 0)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(Owner.Creature, _startStrength, Owner.Creature, null);
        }
    }
}

public sealed class GongdouD3SharpDice : GongdouFixedRelic { public GongdouD3SharpDice() : base("akabeko") { } }
public sealed class GongdouD3ReturnHolster : GongdouFixedRelic { public GongdouD3ReturnHolster() : base("happy_flower") { } }
public sealed class GongdouD3HollowCharm : GongdouFixedRelic { public GongdouD3HollowCharm() : base("anchor") { } }

public sealed class GongdouD5Shuriken : GongdouFixedRelic { public GongdouD5Shuriken() : base("shuriken") { } }
public sealed class GongdouD5Anchor : GongdouFixedRelic
{
    private bool _granted;

    public GongdouD5Anchor() : base("anchor") { }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (_granted || side != Owner.Creature.Side || combatState is not CombatState state || state.RoundNumber > 1)
        {
            return;
        }

        _granted = true;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, 10m, ValueProp.Unpowered, null);
    }
}
public sealed class GongdouD5VoidLens : GongdouFixedRelic { public GongdouD5VoidLens() : base("runic_cube") { } }

public sealed class GongdouD6VioletLotus : GongdouFixedRelic { public GongdouD6VioletLotus() : base("happy_flower") { } }
public sealed class GongdouD6RageCharm : GongdouFixedRelic { public GongdouD6RageCharm() : base("anchor") { } }
public sealed class GongdouD6StanceSeal : GongdouFixedRelic { public GongdouD6StanceSeal() : base("akabeko") { } }

public sealed class GongdouD7SnakeSkull : GongdouFixedRelic { public GongdouD7SnakeSkull() : base("snecko_skull") { } }
public sealed class GongdouD7PoisonFunnel : GongdouFixedRelic { public GongdouD7PoisonFunnel() : base("twisted_funnel") { } }
public sealed class GongdouD7RingOfNeedles : GongdouFixedRelic { public GongdouD7RingOfNeedles() : base("akabeko") { } }

public sealed class GongdouD8DataDisk : GongdouFixedRelic { public GongdouD8DataDisk() : base("data_disk") { } }
public sealed class GongdouD8GoldCable : GongdouFixedRelic { public GongdouD8GoldCable() : base("gold_plated_cables") { } }
public sealed class GongdouD8DarkCore : GongdouFixedRelic { public GongdouD8DarkCore() : base("darkstone_periapt") { } }

public sealed class GongdouD9Damaru : GongdouFixedRelic { public GongdouD9Damaru() : base("happy_flower") { } }
public sealed class GongdouD9Scripture : GongdouFixedRelic { public GongdouD9Scripture() : base("runic_cube") { } }
public sealed class GongdouD9SundialShard : GongdouFixedRelic { public GongdouD9SundialShard() : base("data_disk") { } }

public sealed class GongdouD10WatchCore : GongdouFixedRelic { public GongdouD10WatchCore() : base("happy_flower") { } }
public sealed class GongdouD10Resonator : GongdouFixedRelic { public GongdouD10Resonator() : base("akabeko") { } }
public sealed class GongdouD10PrismShard : GongdouFixedRelic { public GongdouD10PrismShard() : base("data_disk") { } }
