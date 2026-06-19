using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using GongdouSts2ChallengeMod.Challenges;

namespace GongdouSts2ChallengeMod.Monsters;

public sealed class GongdouBrokenTrainingDummyMonster : MonsterModel
{
    public static int CurrentInitialHp { get; set; } = 170;

    public override LocString Title => new("monsters", "GongdouBrokenTrainingDummyMonster.name");
    protected override string VisualsPath => SceneHelper.GetScenePath("creature_visuals/axebot");
    public override int MinInitialHp => CurrentInitialHp;
    public override int MaxInitialHp => CurrentInitialHp;
    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    public override async Task AfterAddedToRoom()
    {
        await PowerCmd.Apply<GongdouThinDeckRulePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmd.Apply<GongdouCrackedArmorRulePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmd.Apply<GongdouMaintenanceRulePower>(Creature, 1m, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var turn1 = new MoveState("GONGDOU_ATTACK_10", _ => Attack(10), new SingleAttackIntent(10));
        var turn2 = new MoveState("GONGDOU_ATTACK_16", _ => Attack(16), new SingleAttackIntent(16));
        var turn3 = new MoveState("GONGDOU_ATTACK_22", _ => Attack(22), new SingleAttackIntent(22));
        var maintenance = new MoveState("GONGDOU_ATTACK_10_MAINTENANCE", _ => AttackAndMaintain(10, 24), new SingleAttackIntent(10));

        turn1.FollowUpState = turn2;
        turn2.FollowUpState = turn3;
        turn3.FollowUpState = maintenance;
        maintenance.FollowUpState = turn1;

        return new MonsterMoveStateMachine(
            [turn1, turn2, turn3, maintenance],
            turn1);
    }

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Creature.CombatState?.RoundNumber == 1 &&
            target == Creature &&
            cardSource?.Type == CardType.Attack &&
            dealer?.Side != Creature.Side)
        {
            return 40m;
        }

        return decimal.MaxValue;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        ChallengeSessionManager.NotifyCardPlayed(cardPlay.Card.Id.Entry);
        return Task.CompletedTask;
    }

    public override Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        ChallengeSessionManager.NotifyPotionUsed(potion.Id.Entry);
        return Task.CompletedTask;
    }

    private async Task Attack(int damage)
    {
        await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.35f)
            .WithAttackerFx(null, AttackSfx)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task AttackAndMaintain(int damage, int heal)
    {
        await Attack(damage);
        if (Creature.IsDead)
        {
            return;
        }

        SfxCmd.Play("event:/sfx/enemy/enemy_attacks/axebot/axebot_buff");
        await CreatureCmd.TriggerAnim(Creature, "sharpen", 0.3f);
        await CreatureCmd.Heal(Creature, heal);
        foreach (var power in Creature.Powers.Where(p => p.TypeForCurrentAmount == PowerType.Debuff).ToList())
        {
            await PowerCmd.Remove(power);
        }

        await PowerCmd.Apply<StrengthPower>(Creature, 4m, Creature, null);
        ChallengeSessionManager.FailActiveChallenge("maintenance_window_triggered");
    }

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        var idle = new AnimState("idle_loop", isLooping: true);
        var attack = new AnimState("attack") { NextState = idle };
        var uppercut = new AnimState("special") { NextState = idle };
        var sharpen = new AnimState("sharpen") { NextState = idle };
        var respawn = new AnimState("respawn") { NextState = idle };
        var hurt = new AnimState("hurt") { NextState = idle };
        var dead = new AnimState("die");

        var animator = new CreatureAnimator(idle, controller);
        animator.AddAnyState("Dead", dead);
        animator.AddAnyState("Hit", hurt);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("uppercut", uppercut);
        animator.AddAnyState("sharpen", sharpen);
        animator.AddAnyState("respawn", respawn);
        return animator;
    }
}
