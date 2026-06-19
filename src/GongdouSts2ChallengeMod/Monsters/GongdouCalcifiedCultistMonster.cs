using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using GongdouSts2ChallengeMod.Challenges;
using GongdouSts2ChallengeMod.Models;
using Sts2Monsters = MegaCrit.Sts2.Core.Models.Monsters;

namespace GongdouSts2ChallengeMod.Monsters;

public sealed class GongdouCalcifiedCultistMonster : MonsterModel
{
    private sealed record VisualProfile(
        string Name,
        Func<MonsterModel> CreateModel,
        string AttackTrigger = "Attack",
        string BuffTrigger = "Cast",
        float AttackDelay = 0.2f,
        float BuffDelay = 0.5f);

    private static readonly MethodInfo? VisualsPathGetter = typeof(MonsterModel)
        .GetProperty("VisualsPath", BindingFlags.Instance | BindingFlags.NonPublic)
        ?.GetGetMethod(nonPublic: true);

    private static readonly MethodInfo? AttackSfxGetter = typeof(MonsterModel)
        .GetProperty("AttackSfx", BindingFlags.Instance | BindingFlags.NonPublic)
        ?.GetGetMethod(nonPublic: true);

    private static readonly VisualProfile DefaultVisualProfile =
        Profile<Sts2Monsters.CalcifiedCultist>();

    private static readonly VisualProfile[] EarlyVisuals =
    [
        Profile<Sts2Monsters.CalcifiedCultist>(),
        Profile<Sts2Monsters.DampCultist>(),
        Profile<Sts2Monsters.Chomper>(),
        Profile<Sts2Monsters.Mawler>()
    ];

    private static readonly VisualProfile[] MidVisuals =
    [
        Profile<Sts2Monsters.Axebot>(buffTrigger: "sharpen", attackDelay: 0.35f, buffDelay: 0.3f),
        Profile<Sts2Monsters.FlailKnight>(attackTrigger: "FlailAttack"),
        Profile<Sts2Monsters.TheForgotten>(),
        Profile<Sts2Monsters.TheLost>(),
        Profile<Sts2Monsters.Byrdonis>(buffTrigger: "Angry")
    ];

    private static readonly VisualProfile[] HardVisuals =
    [
        Profile<Sts2Monsters.LagavulinMatriarch>(attackTrigger: "AttackHeavy"),
        Profile<Sts2Monsters.CeremonialBeast>(),
        Profile<Sts2Monsters.SoulFysh>(),
        Profile<Sts2Monsters.MechaKnight>(buffTrigger: "windUp"),
        Profile<Sts2Monsters.Vantom>(buffTrigger: "BUFF")
    ];

    private static readonly VisualProfile[] BossVisuals =
    [
        Profile<Sts2Monsters.WaterfallGiant>(),
        Profile<Sts2Monsters.KnowledgeDemon>(attackTrigger: "HeavyAttackTrigger", buffTrigger: "MindRotTrigger"),
        Profile<Sts2Monsters.LagavulinMatriarch>(attackTrigger: "AttackHeavy"),
        Profile<Sts2Monsters.CeremonialBeast>(),
        Profile<Sts2Monsters.TheAdversaryMkThree>()
    ];

    private static VisualProfile? _currentVisualProfile;
    private static MonsterModel? _currentVisualModel;

    public static int CurrentInitialHp { get; set; } = 55;
    public static string CurrentPuzzleId { get; set; } = "difficulty1_final_puzzle";
    public static Sts2PuzzleConfig CurrentConfig { get; set; } = PuzzleCatalog.DifficultyOneConfig;

    private float _attackSfxStrength;

    public override LocString Title => ResolveTitle();
    protected override string VisualsPath => ResolveVisualsPath(ActiveVisualModel);
    public override int MinInitialHp => CurrentInitialHp;
    public override int MaxInitialHp => CurrentInitialHp;
    public override DamageSfxType TakeDamageSfxType => ActiveVisualModel.TakeDamageSfxType;
    public override Vector2 ExtraDeathVfxPadding => ActiveVisualModel.ExtraDeathVfxPadding;
    public override string DeathSfx => ActiveVisualModel.DeathSfx;
    public override bool HasDeathSfx => ActiveVisualModel.HasDeathSfx;
    protected override string AttackSfx => ResolveAttackSfx(ActiveVisualModel);

    public override async Task AfterAddedToRoom()
    {
        if (ShouldUsePersistentArmor)
        {
            await PowerCmd.Apply<GongdouPersistentArmorRulePower>(Creature, 1m, Creature, null, silent: true);
        }

        await PowerCmd.Apply<GongdouStageRulePower>(Creature, CurrentConfig.StageIndex, Creature, null, silent: true);
        await GongdouPuzzleRuntime.OnCombatStart(Creature);
    }

    private float AttackSfxStrength
    {
        get => _attackSfxStrength;
        set
        {
            AssertMutable();
            _attackSfxStrength = value;
        }
    }

    public static void RandomizeVisualForStage(int stageIndex)
    {
        var pool = GetVisualPool(stageIndex);
        var profile = pool[Random.Shared.Next(pool.Length)];
        _currentVisualProfile = profile;
        _currentVisualModel = profile.CreateModel();
        GD.Print($"[GongDou STS2] Stage {stageIndex} visual model: {profile.Name}.");
    }

    public override void SetupSkins(MegaSprite spine, MegaSkeleton skeleton)
    {
        try
        {
            ActiveVisualModel.SetupSkins(spine, skeleton);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to apply visual skin {ActiveVisualProfile.Name}: {ex.Message}");
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var actions = CurrentConfig.EnemyActions.Count > 0
            ? CurrentConfig.EnemyActions
            : PuzzleCatalog.DifficultyOneConfig.EnemyActions;

        var states = actions.Select(action =>
        {
            return action.Damage > 0
                ? new MoveState($"GONGDOU_STAGE_{CurrentConfig.StageIndex}_TURN_{action.Turn}", _ => ExecuteAction(action), new SingleAttackIntent(action.Damage))
                : new MoveState($"GONGDOU_STAGE_{CurrentConfig.StageIndex}_TURN_{action.Turn}", _ => ExecuteAction(action), new BuffIntent());
        }).ToList();

        for (var index = 0; index < states.Count; index++)
        {
            states[index].FollowUpState = index + 1 < states.Count ? states[index + 1] : states[index];
        }

        return new MonsterMoveStateMachine(states, states[0]);
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

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return GongdouPuzzleRuntime.ModifyDamageCap(Creature, target, props, dealer, cardSource);
    }

    private async Task Attack(int damage)
    {
        var profile = ActiveVisualProfile;
        await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(profile.AttackTrigger, profile.AttackDelay)
            .BeforeDamage(PlayAttackSfx)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task ExecuteAction(EnemyActionConfig action)
    {
        if (action.Damage > 0)
        {
            await Attack(GongdouPuzzleRuntime.ModifyEnemyAttackDamage(action.Damage));
            await GongdouPuzzleRuntime.AfterEnemyAttack(null, Creature);
        }
        else
        {
            SfxCmd.Play("event:/sfx/enemy/enemy_attacks/cultists/cultists_buff_calcified");
            var profile = ActiveVisualProfile;
            await CreatureCmd.TriggerAnim(Creature, profile.BuffTrigger, profile.BuffDelay);
            await Cmd.CustomScaledWait(0.15f, 0.3f);
        }

        if (action.ArmorGain > 0 && Creature.IsAlive)
        {
            await CreatureCmd.GainBlock(Creature, action.ArmorGain, ValueProp.Unpowered, null);
        }

        if (action.FailIfAlive && Creature.IsAlive)
        {
            ChallengeSessionManager.FailActiveChallenge(string.IsNullOrWhiteSpace(action.FailureReason)
                ? $"stage_{CurrentConfig.StageIndex}_deadline"
                : action.FailureReason);
        }
    }

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        try
        {
            return ActiveVisualModel.GenerateAnimator(controller);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to use visual animator {ActiveVisualProfile.Name}: {ex.Message}");
            return GenerateFallbackAnimator(controller);
        }
    }

    private static CreatureAnimator GenerateFallbackAnimator(MegaSprite controller)
    {
        var idle = new AnimState("idle_loop", isLooping: true);
        var cast = new AnimState("buff") { NextState = idle };
        var attack = new AnimState("attack") { NextState = idle };
        var hurt = new AnimState("hurt") { NextState = idle };
        var dead = new AnimState("die");

        var animator = new CreatureAnimator(idle, controller);
        animator.AddAnyState("Cast", cast);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("Dead", dead);
        animator.AddAnyState("Hit", hurt);
        return animator;
    }

    private static VisualProfile ActiveVisualProfile => _currentVisualProfile ?? DefaultVisualProfile;

    private static MonsterModel ActiveVisualModel => _currentVisualModel ??= ActiveVisualProfile.CreateModel();

    private static VisualProfile Profile<T>(
        string attackTrigger = "Attack",
        string buffTrigger = "Cast",
        float attackDelay = 0.2f,
        float buffDelay = 0.5f)
        where T : MonsterModel
    {
        return new VisualProfile(
            typeof(T).Name,
            () => ModelDb.GetById<T>(ModelDb.GetId<T>()),
            attackTrigger,
            buffTrigger,
            attackDelay,
            buffDelay);
    }

    private static VisualProfile[] GetVisualPool(int stageIndex)
    {
        return stageIndex switch
        {
            <= 3 => EarlyVisuals,
            <= 6 => MidVisuals,
            <= 8 => HardVisuals,
            _ => BossVisuals
        };
    }

    private static string ResolveVisualsPath(MonsterModel model)
    {
        try
        {
            return VisualsPathGetter?.Invoke(model, null) as string
                ?? SceneHelper.GetScenePath("creature_visuals/calcified_cultist");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to resolve visual path {ActiveVisualProfile.Name}: {ex.Message}");
            return SceneHelper.GetScenePath("creature_visuals/calcified_cultist");
        }
    }

    private static string ResolveAttackSfx(MonsterModel model)
    {
        try
        {
            return AttackSfxGetter?.Invoke(model, null) as string
                ?? "event:/sfx/enemy/enemy_attacks/cultists/cultists_attack";
        }
        catch
        {
            return "event:/sfx/enemy/enemy_attacks/cultists/cultists_attack";
        }
    }

    private Task PlayAttackSfx()
    {
        SfxCmd.Play(AttackSfx, "enemy_strength", AttackSfxStrength);
        AttackSfxStrength += 0.2f;
        return Task.CompletedTask;
    }

    private static bool ShouldUsePersistentArmor =>
        CurrentConfig.StageIndex != 4 &&
        (CurrentConfig.EnemyActions.Any(action => action.ArmorGain > 0) ||
         CurrentConfig.StageIndex >= 2);

    private static LocString ResolveTitle()
    {
        return CurrentConfig.StageIndex switch
        {
            1 => new LocString("monsters", "GongdouCalcifiedCultistMonster.name"),
            2 => new LocString("monsters", "GongdouArmorThresholdSentinel.name"),
            3 => new LocString("monsters", "GongdouCunningBellEnemy.name"),
            4 => new LocString("monsters", "GongdouBurnCountdownEnemy.name"),
            5 => new LocString("monsters", "GongdouVoidLockEnemy.name"),
            6 => new LocString("monsters", "GongdouStanceBreachEnemy.name"),
            7 => new LocString("monsters", "GongdouPoisonAntidoteEnemy.name"),
            8 => new LocString("monsters", "GongdouOrbOverloadEnemy.name"),
            9 => new LocString("monsters", "GongdouDivinityMirrorEnemy.name"),
            10 => new LocString("monsters", "GongdouTimeRiftEnemy.name"),
            _ => new LocString("monsters", "GongdouChallengeEnemy.name")
        };
    }
}
