using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Godot;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Challenges;
using GongdouSts2ChallengeMod.Models;
using GongdouSts2ChallengeMod.Monsters;
using GongdouSts2ChallengeMod.Relics;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace GongdouSts2ChallengeMod.Sts2;

public sealed class RunBootstrapper
{
    private bool _ownsChallengeRun;

    public async Task<PreparedChallengeRun> CreateChallengeRunAsync(Sts2PuzzleConfig config)
    {
        if (RunManager.Instance.IsInProgress)
        {
            if (RunManager.Instance.ShouldSave && !_ownsChallengeRun)
            {
                throw new InvalidOperationException("A saved STS2 run is already in progress. Exit the current run before starting a GongDou challenge.");
            }

            RunManager.Instance.CleanUp(graceful: false);
            _ownsChallengeRun = false;
        }

        var game = NGame.Instance
            ?? throw new InvalidOperationException("STS2 game node is not available.");

        RunState runState;
        try
        {
            runState = await StartChallengeRunShellAsync(
                game,
                ModelDb.Character<Ironclad>(),
                shouldSave: true,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                CreateSeed(),
                GameMode.Standard,
                config.Player.AscensionLevel);
        }
        catch
        {
            if (RunManager.Instance.IsInProgress && (_ownsChallengeRun || !RunManager.Instance.ShouldSave))
            {
                RunManager.Instance.CleanUp(graceful: false);
            }

            _ownsChallengeRun = false;
            throw;
        }

        _ownsChallengeRun = true;
        var player = runState.Players.FirstOrDefault()
            ?? throw new InvalidOperationException("Challenge run has no player.");
        ResetChallengePlayer(runState, player, config);
        CloseMapAndClearOverlays();
        ChallengePreparationRoomController.SwitchToBlankRoom("after-run-created");
        return new PreparedChallengeRun(runState, player);
    }

    public async Task<PreparedChallengeRun> ResumeSavedChallengeRunAsync(
        SerializableRun save,
        Sts2PuzzleConfig config,
        ChallengeSelection selection)
    {
        if (RunManager.Instance.IsInProgress)
        {
            if (RunManager.Instance.ShouldSave && !_ownsChallengeRun)
            {
                throw new InvalidOperationException("A saved STS2 run is already in progress. Exit the current run before resuming a GongDou challenge.");
            }

            RunManager.Instance.CleanUp(graceful: false);
            _ownsChallengeRun = false;
        }

        var game = NGame.Instance
            ?? throw new InvalidOperationException("STS2 game node is not available.");
        var runState = RunState.FromSerializable(save);
        var player = runState.Players.FirstOrDefault()
            ?? throw new InvalidOperationException("Saved challenge run has no player.");

        try
        {
            GongdouCalcifiedCultistMonster.CurrentInitialHp = config.Enemy.GetInitialHp(selection.CardIds.Count);
            GongdouCalcifiedCultistMonster.CurrentPuzzleId = config.PuzzleId;
            GongdouCalcifiedCultistMonster.CurrentConfig = config;
            GongdouCalcifiedCultistMonster.RandomizeVisualForStage(config.StageIndex);
            GongdouPuzzleRuntime.Configure(config, selection);

            NAudioManager.Instance?.StopMusic();
            await SetUpSavedSinglePlayerCompat(runState, save);
            _ownsChallengeRun = true;
            game.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
            await game.LoadRun(runState, save.PreFinishedRoom);
            if (save.PreFinishedRoom == null)
            {
                GD.Print("[GongDou STS2] Saved GongDou challenge has no pre-finished room; entering challenge combat room directly.");
                CloseMapAndClearOverlays();
                ChallengePreparationRoomController.MarkInactive();
                await EnterChallengeCombatRoomAsync();
            }

            RestartRunMusicForChallengeCombat();
            ChallengeSessionManager.EnableCompletedCombatHold();
            await PowerCmd.Apply<GongdouChallengeResultHoldPower>(player.Creature, 1m, player.Creature, null, silent: true);
            GD.Print("[GongDou STS2] Saved GongDou challenge run resumed.");
            return new PreparedChallengeRun(runState, player);
        }
        catch
        {
            ChallengeSessionManager.DisableCompletedCombatHold();
            if (RunManager.Instance.IsInProgress && (_ownsChallengeRun || !RunManager.Instance.ShouldSave))
            {
                RunManager.Instance.CleanUp(graceful: false);
            }

            _ownsChallengeRun = false;
            throw;
        }
    }

    public async Task<PreparedChallengeRun> ResumeSavedPreparationRunAsync(
        SerializableRun save,
        Sts2PuzzleConfig config)
    {
        if (RunManager.Instance.IsInProgress)
        {
            if (RunManager.Instance.ShouldSave && !_ownsChallengeRun)
            {
                throw new InvalidOperationException("A saved STS2 run is already in progress. Exit the current run before resuming a GongDou challenge.");
            }

            RunManager.Instance.CleanUp(graceful: false);
            _ownsChallengeRun = false;
        }

        var game = NGame.Instance
            ?? throw new InvalidOperationException("STS2 game node is not available.");
        var runState = RunState.FromSerializable(save);
        var player = runState.Players.FirstOrDefault()
            ?? throw new InvalidOperationException("Saved challenge preparation run has no player.");

        try
        {
            NAudioManager.Instance?.StopMusic();
            await SetUpSavedSinglePlayerCompat(runState, save);
            _ownsChallengeRun = true;
            game.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
            await PreloadManager.LoadRunAssets(runState.Players.Select(p => p.Character));
            await PreloadManager.LoadActAssets(runState.Act);
            RunManager.Instance.Launch();
            game.RootSceneContainer.SetCurrentScene(NRun.Create(runState));
            await game.AwaitProcessFrame();
            await RunManager.Instance.GenerateMap();
            NMapScreen.Instance?.SetTravelEnabled(enabled: false);
            NMapScreen.Instance?.Close(animateOut: false);
            NOverlayStack.Instance?.Clear();
            ResetChallengePlayer(runState, player, config);
            ChallengePreparationRoomController.SwitchToBlankRoom("resume-preparation");
            GD.Print("[GongDou STS2] Saved GongDou challenge preparation resumed.");
            return new PreparedChallengeRun(runState, player);
        }
        catch
        {
            if (RunManager.Instance.IsInProgress && (_ownsChallengeRun || !RunManager.Instance.ShouldSave))
            {
                RunManager.Instance.CleanUp(graceful: false);
            }

            _ownsChallengeRun = false;
            throw;
        }
    }

    private static async Task<RunState> StartChallengeRunShellAsync(
        NGame game,
        CharacterModel character,
        bool shouldSave,
        IReadOnlyList<ActModel> acts,
        IReadOnlyList<ModifierModel> modifiers,
        string seed,
        GameMode gameMode,
        int ascensionLevel = 0)
    {
        var unlockState = SaveManager.Instance.GenerateUnlockStateFromProgress();
        var runState = RunState.CreateForNewRun(
            [Player.CreateForNewRun(character, unlockState, 1uL)],
            acts.Select(act => act.ToMutable()).ToList(),
            modifiers,
            gameMode,
            ascensionLevel,
            seed);

        SetUpNewSinglePlayerCompat(runState, shouldSave);
        using (new NetLoadingHandle(RunManager.Instance.NetService))
        {
            await PreloadManager.LoadRunAssets(runState.Players.Select(player => player.Character));
            await PreloadManager.LoadActAssets(runState.Acts[0]);
            await RunManager.Instance.FinalizeStartingRelics();
            RunManager.Instance.Launch();
            game.RootSceneContainer.SetCurrentScene(NRun.Create(runState));

            // Keep a real run UI alive for native screens, but never enter Neow/first-room rewards.
            await game.AwaitProcessFrame();
            await RunManager.Instance.GenerateMap();
            NMapScreen.Instance?.SetTravelEnabled(enabled: false);
            NMapScreen.Instance?.Close(animateOut: false);
            NOverlayStack.Instance?.Clear();
            GD.Print("[GongDou STS2] Challenge run shell created without entering the first room.");
        }

        return runState;
    }

    private static void SetUpNewSinglePlayerCompat(RunState runState, bool shouldSave)
    {
        var result = InvokeRunManagerSetupCompat(
            "new singleplayer setup",
            ["SetUpNewSinglePlayer", "SetUpNewSingleplayer"],
            method => TryBuildSetUpNewSinglePlayerArgs(method, runState, shouldSave));
        if (result is Task task)
        {
            task.GetAwaiter().GetResult();
        }
    }

    private static async Task SetUpSavedSinglePlayerCompat(RunState runState, SerializableRun save)
    {
        var result = InvokeRunManagerSetupCompat(
            "saved singleplayer setup",
            ["SetUpSavedSinglePlayer", "SetUpSavedSingleplayer"],
            method => TryBuildSetUpSavedSinglePlayerArgs(method, runState, save));
        if (result is Task task)
        {
            await task;
        }
    }

    private static object? InvokeRunManagerSetupCompat(
        string operation,
        IReadOnlyCollection<string> methodNames,
        Func<MethodInfo, object?[]?> tryBuildArgs)
    {
        var manager = RunManager.Instance;
        var overloads = manager.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => methodNames.Contains(method.Name, StringComparer.Ordinal))
            .OrderBy(method => method.GetParameters().Length)
            .ToList();

        GD.Print($"[GongDou STS2] Runtime RunManager {operation} overloads: {(overloads.Count == 0 ? "<none>" : string.Join(" | ", overloads.Select(DescribeMethod)))}");
        foreach (var overload in overloads)
        {
            var args = tryBuildArgs(overload);
            if (args == null)
            {
                continue;
            }

            try
            {
                var result = overload.Invoke(manager, args);
                GD.Print($"[GongDou STS2] RunManager {operation} bound to runtime overload: {DescribeMethod(overload)}");
                return result;
            }
            catch (ArgumentException)
            {
                continue;
            }
            catch (TargetParameterCountException)
            {
                continue;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        var signatures = overloads.Count == 0
            ? "<none>"
            : string.Join(" | ", overloads.Select(DescribeMethod));
        throw new MissingMethodException($"Could not bind RunManager {operation} runtime overloads: {signatures}");
    }

    private static object?[]? TryBuildSetUpNewSinglePlayerArgs(MethodInfo method, RunState runState, bool shouldSave)
    {
        var parameters = method.GetParameters();
        if (parameters.Any(parameter => parameter.ParameterType.IsByRef || parameter.IsOut))
        {
            return null;
        }

        var args = new object?[parameters.Length];
        var runStateAssigned = false;
        var saveFlagAssigned = false;
        var boolParameterCount = parameters.Count(parameter => parameter.ParameterType == typeof(bool));

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterType = parameter.ParameterType;
            if (!runStateAssigned && parameterType.IsInstanceOfType(runState))
            {
                args[i] = runState;
                runStateAssigned = true;
                continue;
            }

            if (parameterType == typeof(bool))
            {
                if (!saveFlagAssigned && IsSaveParameter(parameter, boolParameterCount))
                {
                    args[i] = shouldSave;
                    saveFlagAssigned = true;
                    continue;
                }

                args[i] = false;
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                var defaultValue = parameter.DefaultValue;
                args[i] = defaultValue == DBNull.Value || defaultValue == Type.Missing ? null : defaultValue;
                continue;
            }

            var nullableType = Nullable.GetUnderlyingType(parameterType);
            if (nullableType != null)
            {
                args[i] = null;
                continue;
            }

            if (!parameterType.IsValueType)
            {
                args[i] = null;
                continue;
            }

            if (parameterType == typeof(DateTimeOffset))
            {
                args[i] = DateTimeOffset.UtcNow;
                continue;
            }

            if (parameterType == typeof(DateTime))
            {
                args[i] = DateTime.UtcNow;
                continue;
            }

            if (parameterType == typeof(CancellationToken))
            {
                args[i] = CancellationToken.None;
                continue;
            }

            if (parameterType.IsEnum)
            {
                args[i] = Activator.CreateInstance(parameterType);
                continue;
            }

            if (TryCreateZeroValue(parameterType, out var zeroValue))
            {
                args[i] = zeroValue;
                continue;
            }

            return null;
        }

        return runStateAssigned ? args : null;
    }

    private static object?[]? TryBuildSetUpSavedSinglePlayerArgs(MethodInfo method, RunState runState, SerializableRun save)
    {
        var parameters = method.GetParameters();
        if (parameters.Any(parameter => parameter.ParameterType.IsByRef || parameter.IsOut))
        {
            return null;
        }

        var args = new object?[parameters.Length];
        var runStateAssigned = false;
        var saveAssigned = false;

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterType = parameter.ParameterType;
            if (!runStateAssigned && parameterType.IsInstanceOfType(runState))
            {
                args[i] = runState;
                runStateAssigned = true;
                continue;
            }

            if (!saveAssigned && parameterType.IsInstanceOfType(save))
            {
                args[i] = save;
                saveAssigned = true;
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                var defaultValue = parameter.DefaultValue;
                args[i] = defaultValue == DBNull.Value || defaultValue == Type.Missing ? null : defaultValue;
                continue;
            }

            var nullableType = Nullable.GetUnderlyingType(parameterType);
            if (nullableType != null)
            {
                args[i] = null;
                continue;
            }

            if (!parameterType.IsValueType)
            {
                args[i] = null;
                continue;
            }

            if (parameterType == typeof(CancellationToken))
            {
                args[i] = CancellationToken.None;
                continue;
            }

            if (parameterType.IsEnum)
            {
                args[i] = Activator.CreateInstance(parameterType);
                continue;
            }

            if (TryCreateZeroValue(parameterType, out var zeroValue))
            {
                args[i] = zeroValue;
                continue;
            }

            return null;
        }

        return runStateAssigned && saveAssigned ? args : null;
    }

    private static bool IsSaveParameter(ParameterInfo parameter, int boolParameterCount)
    {
        if (boolParameterCount == 1)
        {
            return true;
        }

        var name = parameter.Name ?? string.Empty;
        return name.Contains("save", StringComparison.OrdinalIgnoreCase)
            || name.Contains("persist", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateZeroValue(Type type, out object? value)
    {
        if (type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal))
        {
            value = Activator.CreateInstance(type);
            return true;
        }

        value = null;
        return false;
    }

    private static string DescribeMethod(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}")
            .ToArray();
        return $"{method.Name}({string.Join(", ", parameters)})";
    }

    public void CancelChallengeRun()
    {
        ChallengePreparationRoomController.MarkInactive();
        if (RunManager.Instance.IsInProgress && (_ownsChallengeRun || !RunManager.Instance.ShouldSave))
        {
            RunManager.Instance.CleanUp(graceful: false);
        }

        _ownsChallengeRun = false;
    }

    public void PrepareForMainMenuSelection()
    {
        if (!RunManager.Instance.IsInProgress)
        {
            return;
        }

        if (RunManager.Instance.ShouldSave)
        {
            throw new InvalidOperationException("A saved STS2 run is already in progress. Exit the current run before starting a GongDou challenge.");
        }

        ChallengePreparationRoomController.MarkInactive();
        RunManager.Instance.CleanUp(graceful: false);
    }

    public async Task<ChallengeStartResult> ApplySelectionAndEnterCalcifiedCultistAsync(
        Sts2PuzzleConfig config,
        ChallengeSelection selection,
        PreparedChallengeRun preparedRun)
    {
        if (!RunManager.Instance.IsInProgress)
        {
            throw new InvalidOperationException("Challenge run is not in progress.");
        }

        var runState = preparedRun.RunState;
        var player = preparedRun.Player;
        ResetChallengePlayer(runState, player, config);

        var deckSize = selection.CardIds.Count;
        var enemyHp = config.Enemy.GetInitialHp(deckSize);
        GongdouCalcifiedCultistMonster.CurrentInitialHp = enemyHp;
        GongdouCalcifiedCultistMonster.CurrentPuzzleId = config.PuzzleId;
        GongdouCalcifiedCultistMonster.CurrentConfig = config;
        GongdouCalcifiedCultistMonster.RandomizeVisualForStage(config.StageIndex);
        GongdouPuzzleRuntime.Configure(config, selection);

        await ConfigureChallengeLoadoutAsync(runState, player, selection);

        CloseMapAndClearOverlays();
        ChallengePreparationRoomController.MarkInactive();

        try
        {
            await EnterChallengeCombatRoomAsync();
            RestartRunMusicForChallengeCombat();
            ChallengeSessionManager.EnableCompletedCombatHold();
            await PowerCmd.Apply<GongdouChallengeResultHoldPower>(player.Creature, 1m, player.Creature, null, silent: true);
        }
        catch
        {
            ChallengeSessionManager.DisableCompletedCombatHold();
            if (RunManager.Instance.IsInProgress && !RunManager.Instance.ShouldSave)
            {
                RunManager.Instance.CleanUp(graceful: false);
            }

            throw;
        }

        return new ChallengeStartResult
        {
            DeckSize = selection.CardIds.Count,
            EnemyInitialHp = enemyHp,
            PlayerInitialHp = player.Creature.CurrentHp,
            PlayerMaxHp = player.Creature.MaxHp,
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task EnterChallengeCombatRoomAsync()
    {
        var encounter = ModelDb.GetById<GongdouCalcifiedCultistEncounter>(
            ModelDb.GetId<GongdouCalcifiedCultistEncounter>()).ToMutable();
        encounter.DebugRandomizeRng();
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            encounter,
            showTransition: false);
    }

    private static void RestartRunMusicForChallengeCombat()
    {
        try
        {
            NAudioManager.Instance?.StopMusic();
            var runMusic = NRunMusicController.Instance;
            runMusic?.UpdateMusic();
            runMusic?.UpdateTrack();
            runMusic?.UpdateAmbience();
            GD.Print("[GongDou STS2] Challenge combat music restarted for current room.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to restart challenge combat music: {ex.Message}");
        }
    }

    private static void ResetChallengePlayer(
        RunState runState,
        Player player,
        Sts2PuzzleConfig config)
    {
        foreach (var card in player.Deck.Cards.ToList())
        {
            player.Deck.RemoveInternal(card, silent: true);
            runState.RemoveCard(card);
        }

        foreach (var relic in player.Relics.ToList())
        {
            player.RemoveRelicInternal(relic, silent: true);
        }

        foreach (var potion in player.PotionSlots.Where(p => p != null).Cast<PotionModel>().ToList())
        {
            player.DiscardPotionInternal(potion, silent: true);
        }

        player.Creature.SetMaxHpInternal(config.Player.MaxHp);
        player.Creature.SetCurrentHpInternal(config.Player.StartingHp);
        player.MaxEnergy = config.Player.MaxEnergy;
    }

    private static async Task ConfigureChallengeLoadoutAsync(
        RunState runState,
        Player player,
        ChallengeSelection selection)
    {
        foreach (var cardId in selection.CardIds)
        {
            var card = ChallengeCardFactory.CreateById(cardId);
            runState.AddCard(card, player);
            card.AfterCreated();
            player.Deck.AddInternal(card, silent: true);
        }

        var slot = 0;
        foreach (var potionId in selection.PotionIds)
        {
            var potion = ChallengePotionFactory.CreateById(potionId);
            var result = player.AddPotionInternal(potion, slot, silent: false);
            if (!result.success)
            {
                throw new InvalidOperationException($"Failed to add challenge potion {potionId}: {result.failureReason}");
            }

            slot++;
        }

        foreach (var relicId in selection.RelicIds)
        {
            var relic = ChallengeRelicFactory.CreateById(relicId);
            await RelicCmd.Obtain(relic, player);
        }
    }

    private static string CreateSeed()
    {
        return $"GONGDOU-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private static void CloseMapAndClearOverlays()
    {
        NOverlayStack.Instance?.Clear();
        NMapScreen.Instance?.Close(animateOut: false);
    }

}

internal static class ChallengePreparationRoomController
{
    public const string RoomName = "GongDouBlankPreparationRoom";
    private static bool _active;
    private static BlankChallengePreparationRoom? _activeRoom;

    public static bool IsBlankRoomActive =>
        _active &&
        NRun.Instance != null &&
        _activeRoom != null &&
        GodotObject.IsInstanceValid(_activeRoom);

    public static void SwitchToBlankRoom(string reason = "unspecified")
    {
        _active = false;
        _activeRoom = null;
        var run = NRun.Instance;
        if (run == null)
        {
            GD.PrintErr("[GongDou STS2] Cannot switch to blank preparation room: NRun.Instance is null.");
            return;
        }

        var room = new BlankChallengePreparationRoom();
        try
        {
            run.SetCurrentRoom(room);
            _active = true;
            _activeRoom = room;
            GD.Print($"[GongDou STS2] Blank preparation room active: {reason}.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to switch to blank preparation room: {ex}");
            if (GodotObject.IsInstanceValid(room))
            {
                room.QueueFree();
            }
        }
    }

    public static void MarkInactive()
    {
        _active = false;
        _activeRoom = null;
    }
}

internal sealed partial class BlankChallengePreparationRoom : Control
{
    private const string BackdropName = "Backdrop";

    public BlankChallengePreparationRoom()
    {
        Name = ChallengePreparationRoomController.RoomName;
        MouseFilter = MouseFilterEnum.Ignore;
        FillParent(this);

        var backdrop = new ColorRect
        {
            Name = BackdropName,
            Color = new Color(0.015f, 0.018f, 0.025f, 1.0f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        FillParent(backdrop);
        AddChild(backdrop);
    }

    public override void _Ready()
    {
        FillParent(this);
        if (GetNodeOrNull<ColorRect>(BackdropName) is { } backdrop)
        {
            FillParent(backdrop);
        }
    }

    private static void FillParent(Control control)
    {
        control.AnchorLeft = 0;
        control.AnchorTop = 0;
        control.AnchorRight = 1;
        control.AnchorBottom = 1;
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
    }
}

public sealed record PreparedChallengeRun(RunState RunState, Player Player);

public sealed record ChallengeStartResult
{
    public int DeckSize { get; init; }
    public int EnemyInitialHp { get; init; }
    public int PlayerInitialHp { get; init; }
    public int PlayerMaxHp { get; init; }
    public DateTimeOffset StartedAt { get; init; }
}
