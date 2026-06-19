using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Ipc;
using GongdouSts2ChallengeMod.Models;
using GongdouSts2ChallengeMod.Preparation;
using GongdouSts2ChallengeMod.Recording;
using GongdouSts2ChallengeMod.Relics;
using GongdouSts2ChallengeMod.Sts2;

namespace GongdouSts2ChallengeMod.Challenges;

public sealed class ChallengeSessionManager
{
    private const string DebugSkipLayerName = "GongDouDebugSkipLayer";
    private const string DebugSkipButtonName = "GongDouDebugSkipButton";
    private const string FrierenCharacterSelectGateKey = "Gongdou.Sts2.Frieren.CharacterSelectEnabled";
    private const string PersistedSessionFileName = "gongdou_sts2_challenge_session.json";
    private const string CurrentRunSaveFileName = "current_run.save";
    private const string GongdouRunSaveFileName = "gongdou_sts2_challenge_current_run.save";
    private const string NativeRunSaveSnapshotFileName = "gongdou_sts2_native_current_run.snapshot";
    private const string NativeRunSaveSnapshotMetaFileName = "gongdou_sts2_native_current_run_snapshot.json";

    private static ChallengeSessionManager? _current;
    private readonly CancellationTokenSource _cts = new();
    private readonly GongdouIpcClient _ipc = new();
    private readonly RunBootstrapper _bootstrapper = new();
    private GodotFrameRecorder? _recorder;
    private RecordingSnapshot? _lastRecordingSnapshot;
    private ChallengeRuntime? _active;
    private Task? _backgroundTask;
    private volatile bool _isStartingChallenge;
    private volatile bool _completionQueued;
    private volatile bool _suspendQueued;
    private volatile bool _autotestStartQueued;
    private volatile bool _autotestResumeQueued;
    private volatile bool _autotestSaveQuitQueued;
    private int _autotestPreparationSaveQuitTicks;
    private long _seriesElapsedBeforeCurrentStageMs;
    private int _seriesTurnsBeforeCurrentStage;
    private int? _lastResultLeaderboardId;
    private int? _lastResultStageIndex;
    private int _lastResultStageCount = 10;
    private long _lastResultSeriesElapsedBeforeStageMs;
    private int _lastResultSeriesTurnsBeforeStage;
    private bool _lastResultStageCleared;
    private static volatile bool _shouldHoldCompletedCombat;

    private enum ChallengeRuntimePhase
    {
        Preparation,
        Combat
    }

    public static bool ShouldHoldCompletedCombat => _shouldHoldCompletedCombat;

    public static bool IsChallengeStartingOrActive
    {
        get
        {
            var current = _current;
            return current != null && (current._isStartingChallenge || current._active != null);
        }
    }

    public static Sts2PuzzleConfig? ActiveConfig => _current?._active?.Config;
    public static ResourcePool? ActiveResources => _current?._active?.Resources;
    public static ChallengeSelection? ActiveSelection => _current?._active?.Selection;

    public static bool TryHandlePauseSaveAndQuit()
    {
        var current = _current;
        if (current == null || !current.HasActiveChallengeRuntime())
        {
            return false;
        }

        current.QueuePauseSaveAndQuit();
        return true;
    }

    public ChallengeSessionManager()
    {
        _current = this;
    }

    private bool HasActiveChallengeRuntime()
    {
        var active = _active;
        return active != null &&
               !_completionQueued &&
               (!_isStartingChallenge || active.Phase == ChallengeRuntimePhase.Preparation) &&
               RunManager.Instance.IsInProgress;
    }

    public void Start()
    {
        _backgroundTask = Task.Run(() => BackgroundLoopAsync(_cts.Token));
    }

    public void RequestShutdown()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // Process teardown is best-effort.
        }

        DisableCompletedCombatHold();
        var active = _active;
        if (active != null)
        {
            StopStageTimer(active);
            active.IsSuspended = true;
            PersistActiveChallengeSession(active);
            StoreCurrentNativeRunAsChallengeSaveAndRestoreNative("shutdown");
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));
            var stopTask = active != null
                ? StopRecordingAsync(active.LeaderboardId, active.LaunchSessionId, timeoutCts.Token)
                : StopRecorderOnlyAsync();
            stopTask.Wait(TimeSpan.FromSeconds(2));
            _ipc.DisposeAsync().AsTask().Wait(TimeSpan.FromMilliseconds(500));
            _backgroundTask?.Wait(TimeSpan.FromMilliseconds(250));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Shutdown cleanup failed: {ex.Message}");
        }
    }

    public static void EnableCompletedCombatHold()
    {
        _shouldHoldCompletedCombat = true;
    }

    public static void DisableCompletedCombatHold()
    {
        _shouldHoldCompletedCombat = false;
    }

    public static void ReturnHeldChallengeToMainMenu()
    {
        var current = _current;
        if (current == null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await current.ReturnHeldChallengeToMainMenuAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Return to main menu failed: {ex}");
            }
        });
    }

    public static void FailActiveChallenge(string failureReason)
    {
        var current = _current;
        if (current?._active == null || current._completionQueued)
        {
            return;
        }

        current.QueueCompletion(success: false, failureReason);
    }

    public static void DebugSkipActiveStage()
    {
        var current = _current;
        if (current == null || !IsDebugToolsEnabled())
        {
            return;
        }

        current.DebugSkipActiveStageInternal();
    }

    public static void StartFromMainMenuButton()
    {
        var current = _current;
        if (current == null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await current.StartFromMainMenuButtonAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Main menu GongDou entry failed: {ex}");
                try
                {
                    await ShowNoticeAsync("共斗挑战", $"启动共斗失败：{ex.Message}").ConfigureAwait(false);
                }
                catch
                {
                    // The original exception is already logged; do not let notice UI failures cascade.
                }
            }
        });
    }

    public void Tick()
    {
        var active = _active;
        if (active == null || _completionQueued)
        {
            RemoveDebugSkipButton();
            return;
        }

        if (ShouldSuspendActiveChallengeAtMainMenu())
        {
            RemoveDebugSkipButton();
            QueueSuspendActiveChallenge();
            return;
        }

        UpdateDebugSkipButton(active);

        if (ShouldRunAutotestPreparationSaveQuit(active))
        {
            _autotestSaveQuitQueued = true;
            GD.Print("[GongDou STS2] Autotest preparation save-and-quit requested.");
            QueuePauseSaveAndQuit();
            return;
        }

        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (ShouldRunAutotestSaveQuit(active, combatState))
        {
            _autotestSaveQuitQueued = true;
            GD.Print("[GongDou STS2] Autotest save-and-quit requested.");
            QueuePauseSaveAndQuit();
            return;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        var player = runState?.Players.FirstOrDefault();
        if (player == null)
        {
            return;
        }

        active.LatestRound = combatState?.RoundNumber ?? active.LatestRound;
        active.LatestPlayerHp = player.Creature.CurrentHp;
        if (combatState?.Enemies.Count > 0)
        {
            active.HasSeenEnemy = true;
        }

        if (player.Creature.IsDead)
        {
            QueueCompletion(success: false, "player_dead");
            return;
        }

        if (combatState != null && active.HasSeenEnemy && combatState.Enemies.All(e => !e.IsAlive))
        {
            QueueCompletion(success: true, null);
        }
    }

    private bool ShouldRunAutotestSaveQuit(ChallengeRuntime active, CombatState? combatState)
    {
        return !_autotestSaveQuitQueued &&
               string.Equals(GetAutotestMode(), "resume-save-quit", StringComparison.OrdinalIgnoreCase) &&
               active is { IsSuspended: false, Phase: ChallengeRuntimePhase.Combat } &&
               combatState != null &&
               RunManager.Instance.IsInProgress;
    }

    private bool ShouldRunAutotestPreparationSaveQuit(ChallengeRuntime active)
    {
        if (_autotestSaveQuitQueued ||
            !string.Equals(GetAutotestMode(), "prep-save-quit", StringComparison.OrdinalIgnoreCase) ||
            active is not { IsSuspended: false, Phase: ChallengeRuntimePhase.Preparation } ||
            !RunManager.Instance.IsInProgress)
        {
            return false;
        }

        _autotestPreparationSaveQuitTicks++;
        return _autotestPreparationSaveQuitTicks >= 30;
    }

    public static void NotifyCardPlayed(string cardId)
    {
        var active = _current?._active;
        if (active == null)
        {
            return;
        }

        active.CardsPlayed++;
        active.EventCount++;
        active.PlayedCards.Add(cardId);
        _current?.PersistActiveChallengeSession(active);
    }

    public static void NotifyPotionUsed(string potionId)
    {
        var active = _current?._active;
        if (active == null)
        {
            return;
        }

        active.PotionsUsed++;
        active.EventCount++;
        active.UsedPotions.Add(potionId);
        _current?.PersistActiveChallengeSession(active);
    }

    private async Task BackgroundLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (ShouldRunAutotestResume())
                {
                    _autotestResumeQueued = true;
                    GD.Print($"[GongDou STS2] Autotest resume requested: {GetAutotestMode()}.");
                    await TryResumePersistedChallengeFromMainMenuSafelyAsync(
                            showNotice: false,
                            "autotest-resume")
                        .ConfigureAwait(false);
                }

                if (ShouldRunAutotestStartPreparation())
                {
                    _autotestStartQueued = true;
                    GD.Print($"[GongDou STS2] Autotest local preparation challenge requested: {GetAutotestMode()}.");
                    await StartLeaderboardChallengeAsync(
                            leaderboardId: 900001,
                            launchSessionId: $"autotest-{GetAutotestPreparationPresetName()}",
                            ackLaunchContext: false,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }

                if (!_ipc.IsConnected)
                {
                    var connected = await _ipc.ConnectAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                    if (!connected)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                }

                if (_active == null && !_isStartingChallenge)
                {
                    await TryConsumeLaunchContextAsync(cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Background loop error: {ex}");
                await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static string GetAutotestMode()
    {
        return (System.Environment.GetEnvironmentVariable("GONGDOU_STS2_AUTOTEST") ?? "").Trim();
    }

    private static bool IsAutotestPreparationMode(string mode)
    {
        return string.Equals(mode, "prep-save-quit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mode, "prep-hold", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAutotestPreparationPresetName()
    {
        return string.Equals(GetAutotestMode(), "prep-hold", StringComparison.OrdinalIgnoreCase)
            ? "preparation-hold"
            : "preparation-save-quit";
    }

    private bool ShouldRunAutotestResume()
    {
        var mode = GetAutotestMode();
        return !_autotestResumeQueued &&
               (string.Equals(mode, "resume-persisted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "resume-save-quit", StringComparison.OrdinalIgnoreCase)) &&
               _active == null &&
               !_isStartingChallenge &&
               IsAtMainMenuWithoutRun();
    }

    private bool ShouldRunAutotestStartPreparation()
    {
        return !_autotestStartQueued &&
               IsAutotestPreparationMode(GetAutotestMode()) &&
               _active == null &&
               !_isStartingChallenge &&
               IsAtMainMenuWithoutRun();
    }

    private async Task StartFromMainMenuButtonAsync()
    {
        if (_isStartingChallenge)
        {
            await ShowNoticeAsync("共斗挑战", "挑战已经在启动或进行中。").ConfigureAwait(false);
            return;
        }

        if (_active != null)
        {
            if (await TryResumeActiveChallengeFromMainMenuAsync().ConfigureAwait(false))
            {
                return;
            }

            if (_active != null)
            {
                await ShowNoticeAsync("共斗挑战", "挑战已经在启动或进行中。").ConfigureAwait(false);
                return;
            }
        }

        if (await TryResumePersistedChallengeFromMainMenuSafelyAsync(showNotice: true, "main-menu-button").ConfigureAwait(false))
        {
            return;
        }

        var selectedMode = await GongdouSts2ChallengeMod.RunOnMainThread(
            ChallengePreparationOverlay.ShowCoopModeMenuAsync).ConfigureAwait(false);
        if (selectedMode == GongdouCoopMenuSelection.Frieren)
        {
            await OpenFrierenCharacterSelectAsync().ConfigureAwait(false);
            return;
        }

        if (selectedMode != GongdouCoopMenuSelection.Puzzle)
        {
            return;
        }

        await StartPuzzleLeaderboardFromMainMenuAsync().ConfigureAwait(false);
    }

    private async Task StartPuzzleLeaderboardFromMainMenuAsync()
    {
        if (!_ipc.IsConnected)
        {
            var connected = await _ipc.ConnectAsync(TimeSpan.FromSeconds(2), _cts.Token).ConfigureAwait(false);
            if (!connected)
            {
                await ShowNoticeAsync("共斗挑战", "没有连接到共斗客户端。请先启动客户端，再从这里选择排行榜。").ConfigureAwait(false);
                return;
            }
        }

        List<LeaderboardSummary>? leaderboards;
        try
        {
            leaderboards = await _ipc.ListLeaderboardsAsync(_cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to list leaderboards: {ex}");
            await ShowNoticeAsync("共斗挑战", "读取排行榜失败，请确认客户端已登录并保持运行。").ConfigureAwait(false);
            return;
        }

        var activeLeaderboards = (leaderboards ?? [])
            .Where(item => item.IsActive && int.TryParse(item.Id, out _))
            .ToList();
        if (activeLeaderboards.Count == 0)
        {
            await ShowNoticeAsync("共斗挑战", "当前没有可用排行榜。").ConfigureAwait(false);
            return;
        }

        var selectedLeaderboardId = await GongdouSts2ChallengeMod.RunOnMainThread(
            () => ChallengePreparationOverlay.ShowLeaderboardMenuAsync(activeLeaderboards)).ConfigureAwait(false);
        if (selectedLeaderboardId is not > 0)
        {
            return;
        }

        await StartLeaderboardChallengeAsync(selectedLeaderboardId.Value, launchSessionId: null, ackLaunchContext: false, _cts.Token)
            .ConfigureAwait(false);
    }

    private static Task OpenFrierenCharacterSelectAsync()
    {
        return GongdouSts2ChallengeMod.RunOnMainThread(() =>
        {
            var mainMenu = NGame.Instance?.MainMenu
                ?? throw new InvalidOperationException("Main menu is not available.");
            if (mainMenu.SubmenuStack == null)
            {
                throw new InvalidOperationException("Main menu submenu stack is not available.");
            }

            AppDomain.CurrentDomain.SetData(FrierenCharacterSelectGateKey, true);

            var characterSelect = mainMenu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
            characterSelect.InitializeSingleplayer();
            mainMenu.SubmenuStack.Push(characterSelect);
        });
    }

    private async Task ReturnHeldChallengeToMainMenuAsync()
    {
        await ResetHeldChallengeSceneAsync(returnToMainMenu: true, "return-to-menu").ConfigureAwait(false);
    }

    private async Task PrepareHeldChallengeForImmediateRelaunchAsync()
    {
        await ResetHeldChallengeSceneAsync(returnToMainMenu: true, "immediate-relaunch").ConfigureAwait(false);
    }

    private async Task ResetHeldChallengeSceneAsync(bool returnToMainMenu, string reason)
    {
        DisableCompletedCombatHold();
        _active = null;
        _completionQueued = false;

        await GongdouSts2ChallengeMod.RunOnMainThread(async () =>
        {
            try
            {
                RemoveDebugSkipButton();
                ChallengeCompletionOverlay.Close();
                if (CombatManager.Instance.IsPaused)
                {
                    CombatManager.Instance.Unpause();
                }

                var game = NGame.Instance;
                if (returnToMainMenu && game != null)
                {
                    await game.ReturnToMainMenu();
                }

                _bootstrapper.CancelChallengeRun();

                if (game != null)
                {
                    await game.AwaitProcessFrame();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] ResetHeldChallengeSceneAsync({reason}) main-thread failure: {ex}");
                try
                {
                    _bootstrapper.CancelChallengeRun();
                }
                catch (Exception cleanupEx)
                {
                    GD.PrintErr($"[GongDou STS2] ResetHeldChallengeSceneAsync({reason}) cleanup fallback failed: {cleanupEx}");
                }
            }
        }).ConfigureAwait(false);
    }

    private static Task ShowNoticeAsync(string title, string message)
    {
        return GongdouSts2ChallengeMod.RunOnMainThread(() =>
            ChallengeCompletionOverlay.ShowNotice(title, message));
    }

    private bool ShouldSuspendActiveChallengeAtMainMenu()
    {
        var active = _active;
        return active != null &&
               !active.IsSuspended &&
               !_completionQueued &&
               !_isStartingChallenge &&
               !_suspendQueued &&
               IsAtMainMenuWithoutRun();
    }

    private static bool IsAtMainMenuWithoutRun()
    {
        try
        {
            return NGame.Instance?.MainMenu != null && !RunManager.Instance.IsInProgress;
        }
        catch
        {
            return false;
        }
    }

    private void QueueSuspendActiveChallenge()
    {
        if (_suspendQueued)
        {
            return;
        }

        _suspendQueued = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await SuspendActiveChallengeAsync("main-menu-save-exit").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to suspend challenge session: {ex}");
            }
            finally
            {
                _suspendQueued = false;
            }
        });
    }

    private void QueuePauseSaveAndQuit()
    {
        if (_suspendQueued)
        {
            return;
        }

        _suspendQueued = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveAndQuitActiveChallengeFromPauseMenuAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to save and quit challenge session: {ex}");
                try
                {
                    await ShowNoticeAsync("共斗挑战", "保存并退出共斗挑战失败，请稍后重试。").ConfigureAwait(false);
                }
                catch (Exception noticeEx)
                {
                    GD.PrintErr($"[GongDou STS2] Failed to show save-and-quit failure notice: {noticeEx}");
                }
            }
            finally
            {
                _suspendQueued = false;
            }
        });
    }

    private async Task SaveAndQuitActiveChallengeFromPauseMenuAsync()
    {
        await SuspendActiveChallengeAsync("pause-save-quit").ConfigureAwait(false);

        await GongdouSts2ChallengeMod.RunOnMainThread(async () =>
        {
            try
            {
                NRunMusicController.Instance?.StopMusic();
                NDebugAudioManager.Instance?.StopAll();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to stop audio during challenge save-and-quit: {ex.Message}");
            }

            var game = NGame.Instance;
            if (game != null)
            {
                await game.ReturnToMainMenu();
            }
        }).ConfigureAwait(false);
    }

    private async Task SuspendActiveChallengeAsync(string reason)
    {
        var active = _active;
        if (active == null)
        {
            return;
        }

        if (active.IsSuspended)
        {
            GD.Print($"[GongDou STS2] Challenge session already suspended: {reason}.");
            return;
        }

        StopStageTimer(active);
        active.IsSuspended = true;
        PersistActiveChallengeSession(active);
        await GongdouSts2ChallengeMod.RunOnMainThread(
            () => SaveCurrentRunForChallengeSuspendAsync(reason)).ConfigureAwait(false);
        StoreCurrentNativeRunAsChallengeSaveAndRestoreNative(reason);
        await StopRecordingAsync(active.LeaderboardId, active.LaunchSessionId, _cts.Token).ConfigureAwait(false);
        GD.Print($"[GongDou STS2] Challenge session suspended: {reason}.");
    }

    private async Task<bool> TryResumeActiveChallengeFromMainMenuAsync()
    {
        var active = _active;
        if (active == null || !IsAtMainMenuWithoutRun())
        {
            return false;
        }

        if (!active.IsSuspended)
        {
            await SuspendActiveChallengeAsync("resume-active-request").ConfigureAwait(false);
        }

        return await ResumeChallengeRuntimeAsync(active, showNotice: true).ConfigureAwait(false);
    }

    private async Task<bool> TryResumePersistedChallengeFromMainMenuAsync(bool showNotice)
    {
        if (!IsAtMainMenuWithoutRun())
        {
            return false;
        }

        var session = TryLoadPersistedChallengeSession();
        if (session is not { Active: true } ||
            !string.Equals(session.ChallengeType, GongdouSts2ChallengeMod.ChallengeType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!HasChallengeRunSave())
        {
            if (!TryMigrateNativeRunSaveToChallengeSlot(session))
            {
                MarkPersistedChallengeSessionInactive(session, "missing-challenge-run-save");
                if (showNotice)
                {
                    await ShowNoticeAsync("共斗挑战", "找不到上次保存的共斗挑战存档，已取消这次恢复。").ConfigureAwait(false);
                }

                return false;
            }
        }

        var active = CreateRuntimeFromPersistedSession(session);
        _active = active;
        _seriesElapsedBeforeCurrentStageMs = active.SeriesElapsedBeforeStageMs;
        _seriesTurnsBeforeCurrentStage = active.SeriesTurnsBeforeStage;
        var resumed = await ResumeChallengeRuntimeAsync(active, showNotice).ConfigureAwait(false);
        if (!resumed && ReferenceEquals(_active, active))
        {
            _active = null;
        }

        return resumed;
    }

    private async Task<bool> TryResumePersistedChallengeFromMainMenuSafelyAsync(bool showNotice, string reason)
    {
        try
        {
            return await TryResumePersistedChallengeFromMainMenuAsync(showNotice).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to resume persisted challenge session ({reason}): {ex}");
            _active = null;
            TryMarkPersistedChallengeInactive("resume-exception-" + reason);
            RestoreNativeRunSnapshotToCurrent("resume-exception-" + reason);
            if (showNotice)
            {
                await ShowNoticeAsync("共斗挑战", "恢复上次共斗挑战时出错，已跳过该共斗存档。普通/芙莉莲存档不会受影响。").ConfigureAwait(false);
            }

            return false;
        }
    }

    private async Task<bool> ResumeChallengeRuntimeAsync(ChallengeRuntime active, bool showNotice)
    {
        if (!IsAtMainMenuWithoutRun())
        {
            return false;
        }

        if (!HasChallengeRunSave())
        {
            if (!TryMigrateNativeRunSaveToChallengeSlot(active))
            {
                MarkPersistedChallengeSessionInactive(active, "missing-challenge-run-save");
                _active = null;
                if (showNotice)
                {
                    await ShowNoticeAsync("共斗挑战", "找不到上次保存的共斗挑战存档，无法继续。").ConfigureAwait(false);
                }

                return false;
            }
        }

        if (!SwapChallengeSaveIntoNativeSlotForResume("resume-active"))
        {
            MarkPersistedChallengeSessionInactive(active, "run-save-swap-failed");
            _active = null;
            if (showNotice)
            {
                await ShowNoticeAsync("共斗挑战", "恢复共斗挑战存档失败，已跳过这次恢复。普通/芙莉莲存档不会受影响。").ConfigureAwait(false);
            }

            return false;
        }

        var saveResult = SaveManager.Instance.LoadRunSave();
        if (!saveResult.Success || saveResult.SaveData == null)
        {
            RestoreNativeRunSnapshotToCurrent("resume-active-load-failed");
            MarkPersistedChallengeSessionInactive(active, "run-save-load-failed");
            _active = null;
            if (showNotice)
            {
                await ShowNoticeAsync("共斗挑战", "读取共斗挑战存档失败，已跳过该共斗存档。普通/芙莉莲存档不会受影响。").ConfigureAwait(false);
            }

            return false;
        }

        if (!IsMatchingGongdouChallengeSave(saveResult.SaveData, active))
        {
            RestoreNativeRunSnapshotToCurrent("resume-active-mismatch");
            MarkPersistedChallengeSessionInactive(active, "run-save-mismatch");
            _active = null;
            if (showNotice)
            {
                await ShowNoticeAsync("共斗挑战", "当前存档不属于这个共斗挑战，已跳过该共斗存档。普通/芙莉莲存档不会受影响。").ConfigureAwait(false);
            }

            return false;
        }

        if (active.Phase == ChallengeRuntimePhase.Preparation)
        {
            return await ResumePreparationRuntimeAsync(active, saveResult.SaveData, showNotice).ConfigureAwait(false);
        }

        _isStartingChallenge = true;
        _suspendQueued = false;
        DisableCompletedCombatHold();
        try
        {
            StopStageTimer(active);
            active.IsSuspended = false;
            await GongdouSts2ChallengeMod.RunOnMainThread(
                () => _bootstrapper.ResumeSavedChallengeRunAsync(saveResult.SaveData, active.Config, active.Selection)).ConfigureAwait(false);

            active.Stopwatch.Restart();
            active.LatestPlayerHp = saveResult.SaveData.Players.FirstOrDefault()?.CurrentHp ?? active.LatestPlayerHp;
            PersistActiveChallengeSession(active);
            await StartRecordingAsync(active.LeaderboardId, active.LaunchSessionId, _cts.Token).ConfigureAwait(false);
            GD.Print($"[GongDou STS2] Challenge session resumed: leaderboard={active.LeaderboardId}, session={active.LaunchSessionId}.");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to resume challenge run: {ex}");
            await StopRecordingAsync(active.LeaderboardId, active.LaunchSessionId, _cts.Token).ConfigureAwait(false);
            RestoreNativeRunSnapshotToCurrent("resume-active-exception");
            MarkPersistedChallengeSessionInactive(active, "resume-runtime-exception");
            _active = null;
            if (showNotice)
            {
                await ShowNoticeAsync("共斗挑战", "恢复共斗挑战失败，已跳过该共斗存档。普通/芙莉莲存档不会受影响。").ConfigureAwait(false);
            }

            return false;
        }
        finally
        {
            _isStartingChallenge = false;
        }
    }

    private async Task<bool> ResumePreparationRuntimeAsync(
        ChallengeRuntime active,
        SerializableRun save,
        bool showNotice)
    {
        _isStartingChallenge = true;
        _suspendQueued = false;
        DisableCompletedCombatHold();
        PreparedChallengeRun preparedRun;
        try
        {
            StopStageTimer(active);
            active.IsSuspended = false;
            preparedRun = await GongdouSts2ChallengeMod.RunOnMainThread(
                () => _bootstrapper.ResumeSavedPreparationRunAsync(save, active.Config)).ConfigureAwait(false);
            active.Stopwatch.Restart();
            PersistActiveChallengeSession(active);
            await StartRecordingAsync(active.LeaderboardId, active.LaunchSessionId, _cts.Token).ConfigureAwait(false);
            GD.Print($"[GongDou STS2] Challenge preparation resumed: leaderboard={active.LeaderboardId}, session={active.LaunchSessionId}.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to resume challenge preparation: {ex}");
            await StopRecordingAsync(active.LeaderboardId, active.LaunchSessionId, _cts.Token).ConfigureAwait(false);
            RestoreNativeRunSnapshotToCurrent("resume-preparation-exception");
            MarkPersistedChallengeSessionInactive(active, "resume-preparation-exception");
            _active = null;
            if (showNotice)
            {
                await ShowNoticeAsync("共斗挑战", "恢复共斗选牌界面失败，已跳过该共斗存档。").ConfigureAwait(false);
            }

            return false;
        }
        finally
        {
            _isStartingChallenge = false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ContinuePreparationRuntimeAsync(active, preparedRun, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed after resuming challenge preparation: {ex}");
            }
        });
        return true;
    }

    private async Task<bool> ContinuePreparationRuntimeAsync(
        ChallengeRuntime active,
        PreparedChallengeRun preparedRun,
        CancellationToken cancellationToken)
    {
        ChallengeSelection? selection;
        try
        {
            selection = await NativeChallengePreparationFlow.ShowAsync(
                active.Config,
                active.Resources,
                preparedRun.RunState,
                preparedRun.Player,
                closePreparationCover: null).ConfigureAwait(false);
        }
        catch (Exception ex) when (active.IsSuspended)
        {
            GD.Print($"[GongDou STS2] Preparation flow stopped after save-and-quit: {ex.Message}");
            return true;
        }

        if (active.IsSuspended)
        {
            return true;
        }

        if (selection == null)
        {
            await GongdouSts2ChallengeMod.RunOnMainThread(
                () => _bootstrapper.CancelChallengeRun()).ConfigureAwait(false);
            RestoreNativeRunSnapshotToCurrent("resume-preparation-cancelled");
            MarkPersistedChallengeSessionInactive(active, "cancelled-in-preparation");
            _active = null;
            await SubmitImmediateFailureAsync(
                active.LeaderboardId,
                active.LaunchSessionId,
                "cancelled",
                "Challenge was cancelled in the in-game preparation screen.",
                cancellationToken,
                GetStageElapsedMs(active),
                active.Config).ConfigureAwait(false);
            return true;
        }

        ChallengeStartResult startResult;
        Func<Task>? closeTransitionOverlay = null;
        try
        {
            closeTransitionOverlay = await GongdouSts2ChallengeMod.RunOnMainThread(
                () => ChallengePreparationOverlay.ShowTransitionOverlay(
                    "正在进入战斗",
                    $"正在应用所选{active.Config.MaxCards}张卡，并直接进入{active.Config.Enemy.Name}战斗...")).ConfigureAwait(false);

            startResult = await GongdouSts2ChallengeMod.RunOnMainThread(
                () => _bootstrapper.ApplySelectionAndEnterCalcifiedCultistAsync(active.Config, selection, preparedRun)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to enter combat after preparation resume: {ex}");
            await GongdouSts2ChallengeMod.RunOnMainThread(
                () => _bootstrapper.CancelChallengeRun()).ConfigureAwait(false);
            RestoreNativeRunSnapshotToCurrent("resume-preparation-enter-combat-failed");
            MarkPersistedChallengeSessionInactive(active, "start-failed-after-preparation-resume");
            _active = null;
            await SubmitImmediateFailureAsync(
                active.LeaderboardId,
                active.LaunchSessionId,
                "start_failed",
                ex.Message,
                cancellationToken,
                GetStageElapsedMs(active),
                active.Config).ConfigureAwait(false);
            return true;
        }
        finally
        {
            if (closeTransitionOverlay != null)
            {
                await GongdouSts2ChallengeMod.RunOnMainThread(closeTransitionOverlay).ConfigureAwait(false);
            }
        }

        CopySelection(active.Selection, selection);
        active.StartResult = startResult;
        active.Phase = ChallengeRuntimePhase.Combat;
        active.LatestPlayerHp = startResult.PlayerInitialHp;
        active.LatestRound = 1;
        PersistActiveChallengeSession(active);
        GD.Print($"[GongDou STS2] Challenge resumed from preparation and entered combat: leaderboard={active.LeaderboardId}, deck={selection.CardIds.Count}.");
        return true;
    }

    private void PersistActiveChallengeSession(ChallengeRuntime active)
    {
        PersistChallengeSession(CreatePersistedChallengeSession(active, isActive: true, null));
    }

    private static void MarkPersistedChallengeSessionInactive(ChallengeRuntime active, string reason)
    {
        PersistChallengeSession(CreatePersistedChallengeSession(active, isActive: false, reason));
    }

    private static void MarkPersistedChallengeSessionInactive(PersistedChallengeSession session, string reason)
    {
        PersistChallengeSession(session with
        {
            Active = false,
            InactiveReason = reason,
            PersistedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static void TryMarkPersistedChallengeInactive(string reason)
    {
        var session = TryLoadPersistedChallengeSession();
        if (session is { Active: true } &&
            string.Equals(session.ChallengeType, GongdouSts2ChallengeMod.ChallengeType, StringComparison.OrdinalIgnoreCase))
        {
            MarkPersistedChallengeSessionInactive(session, reason);
            GD.Print($"[GongDou STS2] Marked stale persisted challenge session inactive: {reason}.");
        }
    }

    private static void PersistChallengeSession(PersistedChallengeSession session)
    {
        try
        {
            var path = GetPersistedSessionPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(session, JsonOptions.CamelCaseInsensitive));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to persist challenge session: {ex.Message}");
        }
    }

    private static PersistedChallengeSession? TryLoadPersistedChallengeSession()
    {
        try
        {
            var path = GetPersistedSessionPath();
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PersistedChallengeSession>(
                File.ReadAllText(path),
                JsonOptions.CamelCaseInsensitive);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to load persisted challenge session: {ex.Message}");
            return null;
        }
    }

    private static string GetPersistedSessionPath()
    {
        try
        {
            return ResolveProfileScopedFilePath(PersistedSessionFileName);
        }
        catch
        {
            var userDataDir = OS.GetUserDataDir();
            return Path.Combine(
                string.IsNullOrWhiteSpace(userDataDir) ? AppContext.BaseDirectory : userDataDir,
                PersistedSessionFileName);
        }
    }

    private static void PrepareNativeSaveSlotForChallengeStart(string reason)
    {
        CaptureNativeRunSnapshot(reason);
        ClearNativeRunSaveSlot(reason);
    }

    private static bool HasChallengeRunSave()
    {
        return File.Exists(GetChallengeRunSavePath()) || File.Exists(GetChallengeRunSaveBackupPath());
    }

    private static bool SwapChallengeSaveIntoNativeSlotForResume(string reason)
    {
        try
        {
            if (!HasChallengeRunSave())
            {
                return false;
            }

            CaptureNativeRunSnapshot(reason);
            ClearNativeRunSaveSlot(reason);
            CopyFileIfExists(GetChallengeRunSavePath(), GetCurrentRunSavePath());
            CopyFileIfExists(GetChallengeRunSaveBackupPath(), GetCurrentRunSaveBackupPath());
            GD.Print($"[GongDou STS2] Swapped GongDou challenge save into native slot: {reason}.");
            return File.Exists(GetCurrentRunSavePath()) || File.Exists(GetCurrentRunSaveBackupPath());
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to swap challenge save into native slot ({reason}): {ex.Message}");
            RestoreNativeRunSnapshotToCurrent(reason + "-rollback");
            return false;
        }
    }

    private static void StoreCurrentNativeRunAsChallengeSaveAndRestoreNative(string reason)
    {
        try
        {
            var currentRunPath = GetCurrentRunSavePath();
            var currentBackupPath = GetCurrentRunSaveBackupPath();
            if (File.Exists(currentRunPath) || File.Exists(currentBackupPath))
            {
                CopyFileIfExists(currentRunPath, GetChallengeRunSavePath());
                CopyFileIfExists(currentBackupPath, GetChallengeRunSaveBackupPath());
                GD.Print($"[GongDou STS2] Stored native slot as GongDou challenge save: {reason}.");
            }
            else
            {
                GD.PrintErr($"[GongDou STS2] No native run save was present when suspending challenge: {reason}.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to store challenge save ({reason}): {ex.Message}");
        }

        RestoreNativeRunSnapshotToCurrent(reason);
    }

    private static async Task SaveCurrentRunForChallengeSuspendAsync(string reason)
    {
        try
        {
            if (!RunManager.Instance.IsInProgress || !RunManager.Instance.ShouldSave)
            {
                GD.Print($"[GongDou STS2] Skip native run save before suspend: {reason}; inProgress={RunManager.Instance.IsInProgress}, shouldSave={RunManager.Instance.ShouldSave}.");
                return;
            }

            var pending = SaveManager.Instance.CurrentRunSaveTask;
            if (pending != null)
            {
                await pending.ConfigureAwait(false);
            }

            var preFinishedRoom = RunManager.Instance.DebugOnlyGetState()?.CurrentRoom;
            await SaveManager.Instance.SaveRun(preFinishedRoom, saveProgress: false).ConfigureAwait(false);
            GD.Print($"[GongDou STS2] Native run save written before challenge suspend: {reason}.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to save native run before challenge suspend ({reason}): {ex}");
            throw;
        }
    }

    private static bool TryMigrateNativeRunSaveToChallengeSlot(PersistedChallengeSession session)
    {
        try
        {
            if (!SaveManager.Instance.HasRunSave)
            {
                return false;
            }

            var saveResult = SaveManager.Instance.LoadRunSave();
            if (!saveResult.Success ||
                saveResult.SaveData == null ||
                !IsMatchingGongdouChallengeSave(saveResult.SaveData, session))
            {
                return false;
            }

            CopyFileIfExists(GetCurrentRunSavePath(), GetChallengeRunSavePath());
            CopyFileIfExists(GetCurrentRunSaveBackupPath(), GetChallengeRunSaveBackupPath());
            RestoreNativeRunSnapshotToCurrent("migrate-native-challenge-save");
            GD.Print("[GongDou STS2] Migrated legacy native GongDou challenge save into dedicated slot.");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to migrate native challenge save: {ex.Message}");
            return false;
        }
    }

    private static bool TryMigrateNativeRunSaveToChallengeSlot(ChallengeRuntime active)
    {
        return TryMigrateNativeRunSaveToChallengeSlot(CreatePersistedChallengeSession(active, isActive: true, null));
    }

    private static void CaptureNativeRunSnapshot(string reason)
    {
        try
        {
            var currentRunPath = GetCurrentRunSavePath();
            var currentBackupPath = GetCurrentRunSaveBackupPath();
            var snapshot = new NativeRunSaveSnapshot
            {
                HasRunSave = File.Exists(currentRunPath),
                HasRunSaveBackup = File.Exists(currentBackupPath),
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Reason = reason
            };

            if (snapshot.HasRunSave)
            {
                CopyFileIfExists(currentRunPath, GetNativeRunSaveSnapshotPath());
            }

            if (snapshot.HasRunSaveBackup)
            {
                CopyFileIfExists(currentBackupPath, GetNativeRunSaveSnapshotBackupPath());
            }

            WriteTextFile(GetNativeRunSaveSnapshotMetaPath(), JsonSerializer.Serialize(snapshot, JsonOptions.CamelCaseInsensitive));
            GD.Print($"[GongDou STS2] Captured native run save snapshot: {reason}; hasSave={snapshot.HasRunSave}.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to capture native run save snapshot ({reason}): {ex.Message}");
            throw;
        }
    }

    private static void RestoreNativeRunSnapshotToCurrent(string reason)
    {
        try
        {
            var snapshot = ReadNativeRunSaveSnapshot();
            ClearNativeRunSaveSlot(reason);
            if (snapshot is { HasRunSave: true })
            {
                CopyFileIfExists(GetNativeRunSaveSnapshotPath(), GetCurrentRunSavePath());
            }

            if (snapshot is { HasRunSaveBackup: true })
            {
                CopyFileIfExists(GetNativeRunSaveSnapshotBackupPath(), GetCurrentRunSaveBackupPath());
            }

            GD.Print($"[GongDou STS2] Restored native run save slot: {reason}; hasSave={snapshot?.HasRunSave == true}.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to restore native run save slot ({reason}): {ex.Message}");
        }
    }

    private static NativeRunSaveSnapshot? ReadNativeRunSaveSnapshot()
    {
        try
        {
            var path = GetNativeRunSaveSnapshotMetaPath();
            return File.Exists(path)
                ? JsonSerializer.Deserialize<NativeRunSaveSnapshot>(File.ReadAllText(path), JsonOptions.CamelCaseInsensitive)
                : null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to read native run save snapshot metadata: {ex.Message}");
            return null;
        }
    }

    private static void ClearNativeRunSaveSlot(string reason)
    {
        TryDeleteFile(GetCurrentRunSavePath(), reason);
        TryDeleteFile(GetCurrentRunSaveBackupPath(), reason);
    }

    private static string GetCurrentRunSavePath() =>
        ResolveProfileScopedFilePath(Path.Combine("saves", CurrentRunSaveFileName));

    private static string GetCurrentRunSaveBackupPath() => GetCurrentRunSavePath() + ".backup";

    private static string GetChallengeRunSavePath() =>
        ResolveProfileScopedFilePath(Path.Combine("saves", GongdouRunSaveFileName));

    private static string GetChallengeRunSaveBackupPath() => GetChallengeRunSavePath() + ".backup";

    private static string GetNativeRunSaveSnapshotPath() =>
        ResolveProfileScopedFilePath(Path.Combine("saves", NativeRunSaveSnapshotFileName));

    private static string GetNativeRunSaveSnapshotBackupPath() => GetNativeRunSaveSnapshotPath() + ".backup";

    private static string GetNativeRunSaveSnapshotMetaPath() =>
        ResolveProfileScopedFilePath(NativeRunSaveSnapshotMetaFileName);

    private static string ResolveProfileScopedFilePath(string relativePath)
    {
        return NormalizeGodotUserPath(SaveManager.Instance.GetProfileScopedPath(relativePath));
    }

    private static string NormalizeGodotUserPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            return CombineWithUserDataDir(normalized["user://".Length..]);
        }

        if (normalized.StartsWith("user:/", StringComparison.OrdinalIgnoreCase))
        {
            return CombineWithUserDataDir(normalized["user:/".Length..]);
        }

        return path;
    }

    private static string CombineWithUserDataDir(string suffix)
    {
        var userDataDir = OS.GetUserDataDir();
        var relativeSuffix = suffix.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(
            string.IsNullOrWhiteSpace(userDataDir) ? AppContext.BaseDirectory : userDataDir,
            relativeSuffix);
    }

    private static void CopyFileIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void WriteTextFile(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
    }

    private static void TryDeleteFile(string path, string reason)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                GD.Print($"[GongDou STS2] Cleared native save file for challenge slot swap: {reason}; file={Path.GetFileName(path)}.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Failed to clear native save file ({reason}, {Path.GetFileName(path)}): {ex.Message}");
            throw;
        }
    }

    private static PersistedChallengeSession CreatePersistedChallengeSession(
        ChallengeRuntime runtime,
        bool isActive,
        string? inactiveReason)
    {
        return new PersistedChallengeSession
        {
            Active = isActive,
            InactiveReason = inactiveReason,
            ChallengeType = GongdouSts2ChallengeMod.ChallengeType,
            ModVersion = GongdouSts2ChallengeMod.Version,
            Phase = runtime.Phase.ToString(),
            LeaderboardId = runtime.LeaderboardId,
            LaunchSessionId = runtime.LaunchSessionId,
            Config = runtime.Config,
            Resources = runtime.Resources,
            Selection = new PersistedChallengeSelection
            {
                CardIds = runtime.Selection.CardIds.ToArray(),
                PotionIds = runtime.Selection.PotionIds.ToArray(),
                RelicIds = runtime.Selection.RelicIds.ToArray()
            },
            StartResult = new PersistedChallengeStartResult
            {
                DeckSize = runtime.StartResult.DeckSize,
                EnemyInitialHp = runtime.StartResult.EnemyInitialHp,
                PlayerInitialHp = runtime.StartResult.PlayerInitialHp,
                PlayerMaxHp = runtime.StartResult.PlayerMaxHp,
                StartedAt = runtime.StartResult.StartedAt
            },
            StartedAtUtc = runtime.StartedAtUtc,
            EndedAtUtc = runtime.EndedAtUtc,
            PersistedAtUtc = DateTimeOffset.UtcNow,
            StageElapsedMs = GetStageElapsedMs(runtime),
            SeriesElapsedBeforeStageMs = runtime.SeriesElapsedBeforeStageMs,
            SeriesTurnsBeforeStage = runtime.SeriesTurnsBeforeStage,
            LatestRound = runtime.LatestRound,
            LatestPlayerHp = runtime.LatestPlayerHp,
            CardsPlayed = runtime.CardsPlayed,
            PotionsUsed = runtime.PotionsUsed,
            EventCount = runtime.EventCount,
            HasSeenEnemy = runtime.HasSeenEnemy,
            PlayedCards = runtime.PlayedCards.ToArray(),
            UsedPotions = runtime.UsedPotions.ToArray()
        };
    }

    private static ChallengeRuntime CreateRuntimeFromPersistedSession(PersistedChallengeSession session)
    {
        var selection = new ChallengeSelection();
        selection.CardIds.AddRange(session.Selection.CardIds ?? []);
        selection.PotionIds.AddRange(session.Selection.PotionIds ?? []);
        selection.RelicIds.AddRange(session.Selection.RelicIds ?? []);

        var startResult = new ChallengeStartResult
        {
            DeckSize = session.StartResult.DeckSize,
            EnemyInitialHp = session.StartResult.EnemyInitialHp,
            PlayerInitialHp = session.StartResult.PlayerInitialHp,
            PlayerMaxHp = session.StartResult.PlayerMaxHp,
            StartedAt = session.StartResult.StartedAt == default
                ? DateTimeOffset.UtcNow
                : session.StartResult.StartedAt
        };

        var runtime = new ChallengeRuntime
        {
            LeaderboardId = session.LeaderboardId,
            LaunchSessionId = string.IsNullOrWhiteSpace(session.LaunchSessionId)
                ? Guid.NewGuid().ToString("N")
                : session.LaunchSessionId,
            RuleConfig = new LeaderboardRuleConfig
            {
                LeaderboardId = session.LeaderboardId,
                ChallengeType = GongdouSts2ChallengeMod.ChallengeType
            },
            Config = session.Config ?? new Sts2PuzzleConfig(),
            Resources = session.Resources ?? new ResourcePool(),
            Selection = selection,
            StartResult = startResult,
            Stopwatch = new Stopwatch(),
            StartedAtUtc = session.StartedAtUtc == default ? DateTimeOffset.UtcNow : session.StartedAtUtc,
            SeriesElapsedBeforeStageMs = session.SeriesElapsedBeforeStageMs,
            SeriesTurnsBeforeStage = session.SeriesTurnsBeforeStage,
            LatestPlayerHp = session.LatestPlayerHp,
            LatestRound = Math.Max(1, session.LatestRound),
            CardsPlayed = Math.Max(0, session.CardsPlayed),
            PotionsUsed = Math.Max(0, session.PotionsUsed),
            EventCount = Math.Max(0, session.EventCount),
            HasSeenEnemy = session.HasSeenEnemy,
            StageElapsedBeforeStopwatchMs = Math.Max(0, session.StageElapsedMs),
            IsSuspended = true,
            Phase = ParseRuntimePhase(session.Phase)
        };
        runtime.PlayedCards.AddRange(session.PlayedCards ?? []);
        runtime.UsedPotions.AddRange(session.UsedPotions ?? []);
        return runtime;
    }

    private static ChallengeRuntimePhase ParseRuntimePhase(string? phase)
    {
        return Enum.TryParse<ChallengeRuntimePhase>(phase, ignoreCase: true, out var parsed)
            ? parsed
            : ChallengeRuntimePhase.Combat;
    }

    private static void CopySelection(ChallengeSelection target, ChallengeSelection source)
    {
        target.CardIds.Clear();
        target.CardIds.AddRange(source.CardIds);
        target.PotionIds.Clear();
        target.PotionIds.AddRange(source.PotionIds);
        target.RelicIds.Clear();
        target.RelicIds.AddRange(source.RelicIds);
    }

    private static bool IsMatchingGongdouChallengeSave(SerializableRun save, PersistedChallengeSession session)
    {
        return session.Active &&
               session.LeaderboardId > 0 &&
               !string.IsNullOrWhiteSpace(session.LaunchSessionId) &&
               IsMatchingGongdouChallengeSave(
                   save,
                   session.Config ?? new Sts2PuzzleConfig(),
                   session.Selection ?? new PersistedChallengeSelection());
    }

    private static bool IsMatchingGongdouChallengeSave(SerializableRun save, ChallengeRuntime active)
    {
        return IsMatchingGongdouChallengeSave(
            save,
            active.Config,
            new PersistedChallengeSelection
            {
                CardIds = active.Selection.CardIds.ToArray(),
                PotionIds = active.Selection.PotionIds.ToArray(),
                RelicIds = active.Selection.RelicIds.ToArray()
            });
    }

    private static bool IsMatchingGongdouChallengeSave(
        SerializableRun save,
        Sts2PuzzleConfig config,
        PersistedChallengeSelection selection)
    {
        var player = save.Players.FirstOrDefault();
        if (player == null || !IsIroncladChallengePlayer(player.CharacterId))
        {
            return false;
        }

        if (player.MaxHp != config.Player.MaxHp || player.MaxEnergy != config.Player.MaxEnergy)
        {
            return false;
        }

        if (selection.CardIds.Length > 0 &&
            !ContainsExpectedItems(selection.CardIds, player.Deck.Select(card => card.Id), ResolveCardMatchCandidates))
        {
            return false;
        }

        return selection.RelicIds.Length == 0 ||
               ContainsExpectedItems(selection.RelicIds, player.Relics.Select(relic => relic.Id), ResolveRelicMatchCandidates);
    }

    private static bool IsIroncladChallengePlayer(MegaCrit.Sts2.Core.Models.ModelId? characterId)
    {
        var key = NormalizeMatchKey(characterId);
        return key.Contains("ironclad", StringComparison.Ordinal);
    }

    private static bool ContainsExpectedItems(
        IReadOnlyList<string> expectedIds,
        IEnumerable<MegaCrit.Sts2.Core.Models.ModelId?> actualIds,
        Func<string, IEnumerable<string>> resolveCandidates)
    {
        var actualCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in actualIds.SelectMany(ExpandModelIdKeys).Select(NormalizeMatchKey).Where(key => key.Length > 0))
        {
            actualCounts[key] = actualCounts.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        foreach (var expectedId in expectedIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            var matchedKey = resolveCandidates(expectedId)
                .Select(NormalizeMatchKey)
                .FirstOrDefault(key => actualCounts.TryGetValue(key, out var count) && count > 0);
            if (matchedKey == null)
            {
                return false;
            }

            actualCounts[matchedKey]--;
        }

        return true;
    }

    private static IEnumerable<string> ResolveCardMatchCandidates(string id)
    {
        var keys = new List<string> { id, "Gongdou" + id };
        try
        {
            var card = ChallengeCardFactory.CreateById(id);
            keys.AddRange(ExpandModelIdKeys(card.Id));
        }
        catch
        {
            // Unknown ids will be rejected by the caller's multiset check.
        }

        return keys;
    }

    private static IEnumerable<string> ResolveRelicMatchCandidates(string id)
    {
        var keys = new List<string> { id, "Gongdou" + id };
        try
        {
            var relic = ChallengeRelicFactory.CreateById(id);
            keys.AddRange(ExpandModelIdKeys(relic.Id));
        }
        catch
        {
            // Unknown ids will be rejected by the caller's multiset check.
        }

        return keys;
    }

    private static IEnumerable<string> ExpandModelIdKeys(MegaCrit.Sts2.Core.Models.ModelId? id)
    {
        if (id == null)
        {
            yield break;
        }

        yield return id.Entry;
        yield return id.ToString();
    }

    private static string NormalizeMatchKey(MegaCrit.Sts2.Core.Models.ModelId? id)
    {
        return id == null ? string.Empty : NormalizeMatchKey(id.Entry);
    }

    private static string NormalizeMatchKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace(" ", "", StringComparison.Ordinal)
                .Replace("_", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .Trim()
                .ToLowerInvariant();
    }

    private async Task FinalizeChallengeRunSaveSlotAsync(ChallengeRuntime active)
    {
        await GongdouSts2ChallengeMod.RunOnMainThread(() =>
        {
            try
            {
                if (SaveManager.Instance.HasRunSave)
                {
                    var saveResult = SaveManager.Instance.LoadRunSave();
                    if (saveResult.Success &&
                        saveResult.SaveData != null &&
                        !IsMatchingGongdouChallengeSave(saveResult.SaveData, active))
                    {
                        GD.PrintErr("[GongDou STS2] Completed challenge save did not match sidecar strictly; clearing it anyway because the active GongDou run owns the current save slot.");
                    }

                    SaveManager.Instance.DeleteCurrentRun();
                }

                RestoreNativeRunSnapshotToCurrent("challenge-completed");
                GD.Print("[GongDou STS2] Cleared completed GongDou challenge save and restored native save slot.");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Failed to finalize completed challenge save slot: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    private static long GetStageElapsedMs(ChallengeRuntime active)
    {
        return Math.Max(0, active.StageElapsedBeforeStopwatchMs + active.Stopwatch.ElapsedMilliseconds);
    }

    private static void StopStageTimer(ChallengeRuntime active)
    {
        active.StageElapsedBeforeStopwatchMs = GetStageElapsedMs(active);
        active.Stopwatch.Reset();
    }

    private void DebugSkipActiveStageInternal()
    {
        var active = _active;
        if (active == null || _completionQueued || !IsDebugToolsEnabled())
        {
            return;
        }

        if (active.Config.StageIndex >= active.Config.StageCount)
        {
            _ = ShowNoticeAsync("GongDou DEBUG", "当前已经是最后一关，不能继续跳到下一关。");
            return;
        }

        _completionQueued = true;
        RemoveDebugSkipButton();

        _ = Task.Run(async () =>
        {
            try
            {
                await DebugSkipToNextStageAsync(active).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _completionQueued = false;
                GD.PrintErr($"[GongDou STS2] Debug skip stage failed: {ex}");
                await ShowNoticeAsync("GongDou DEBUG", $"跳关失败：{ex.Message}").ConfigureAwait(false);
            }
        });
    }

    private async Task DebugSkipToNextStageAsync(ChallengeRuntime active)
    {
        var nextStage = active.Config.StageIndex + 1;
        active.Stopwatch.Stop();
        active.EndedAtUtc = DateTimeOffset.UtcNow;

        var stageTurns = Math.Max(1, active.LatestRound);
        _seriesElapsedBeforeCurrentStageMs = active.SeriesElapsedBeforeStageMs + active.Stopwatch.ElapsedMilliseconds;
        _seriesTurnsBeforeCurrentStage = active.SeriesTurnsBeforeStage + stageTurns;
        _lastResultLeaderboardId = active.LeaderboardId;
        _lastResultStageIndex = active.Config.StageIndex;
        _lastResultStageCount = active.Config.StageCount;
        _lastResultSeriesElapsedBeforeStageMs = active.SeriesElapsedBeforeStageMs;
        _lastResultSeriesTurnsBeforeStage = active.SeriesTurnsBeforeStage;
        _lastResultStageCleared = true;

        await StopRecordingAsync(active.LeaderboardId, active.LaunchSessionId, _cts.Token).ConfigureAwait(false);

        _active = null;
        _completionQueued = false;
        GD.Print($"[GongDou STS2] DEBUG skipped stage {active.Config.StageIndex}; starting stage {nextStage}.");

        await ReturnHeldChallengeToMainMenuAsync().ConfigureAwait(false);
        await StartLeaderboardChallengeAsync(
            active.LeaderboardId,
            launchSessionId: null,
            ackLaunchContext: false,
            _cts.Token,
            stageOverride: nextStage).ConfigureAwait(false);
    }

    private static bool IsDebugToolsEnabled()
    {
#if DEBUG
        return true;
#else
        var value = System.Environment.GetEnvironmentVariable("GONGDOU_STS2_DEBUG");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "debug", StringComparison.OrdinalIgnoreCase);
#endif
    }

    private static void UpdateDebugSkipButton(ChallengeRuntime active)
    {
        if (!IsDebugToolsEnabled() || active.Config.StageIndex >= active.Config.StageCount)
        {
            RemoveDebugSkipButton();
            return;
        }

        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        var layer = tree.Root.GetNodeOrNull<CanvasLayer>(DebugSkipLayerName);
        Button? button;
        if (layer == null)
        {
            layer = new CanvasLayer
            {
                Name = DebugSkipLayerName,
                Layer = 840
            };

            button = new Button
            {
                Name = DebugSkipButtonName,
                CustomMinimumSize = new Vector2(230, 46)
            };
            button.AnchorLeft = 1;
            button.AnchorRight = 1;
            button.OffsetLeft = -260;
            button.OffsetRight = -24;
            button.OffsetTop = 118;
            button.OffsetBottom = 164;
            button.AddThemeFontSizeOverride("font_size", 18);
            button.Pressed += DebugSkipActiveStage;
            layer.AddChild(button);
            tree.Root.AddChild(layer);
        }
        else
        {
            button = layer.GetNodeOrNull<Button>(DebugSkipButtonName);
        }

        if (button != null)
        {
            button.Text = $"DEBUG 跳到第 {active.Config.StageIndex + 1} 关";
            button.Disabled = _current?._completionQueued ?? true;
        }
    }

    private static void RemoveDebugSkipButton()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        var existing = tree.Root.GetNodeOrNull<CanvasLayer>(DebugSkipLayerName);
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            existing.QueueFree();
        }
    }

    private async Task<bool> TryConsumeLaunchContextAsync(
        CancellationToken cancellationToken,
        bool showNoContextNotice = false)
    {
        var context = await _ipc.ConsumeLaunchContextAsync(cancellationToken).ConfigureAwait(false);
        if (context == null || !int.TryParse(context.LeaderboardId, out var leaderboardId) || leaderboardId <= 0)
        {
            if (showNoContextNotice)
            {
                await ShowNoticeAsync("共斗挑战", "客户端没有可用的 Slay the Spire 2 挑战上下文。").ConfigureAwait(false);
            }

            return false;
        }

        return await StartLeaderboardChallengeAsync(
            leaderboardId,
            context.LaunchSessionId,
            ackLaunchContext: true,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> StartLeaderboardChallengeAsync(
        int leaderboardId,
        string? launchSessionId,
        bool ackLaunchContext,
        CancellationToken cancellationToken,
        int? stageOverride = null)
    {
        if (_isStartingChallenge)
        {
            await ShowNoticeAsync("共斗挑战", "挑战已经在启动或进行中。").ConfigureAwait(false);
            return false;
        }

        if (_active != null)
        {
            if (await TryResumeActiveChallengeFromMainMenuAsync().ConfigureAwait(false))
            {
                return true;
            }

            if (_active != null)
            {
                await ShowNoticeAsync("共斗挑战", "挑战已经在启动或进行中。").ConfigureAwait(false);
                return false;
            }
        }

        var ruleConfig = TryCreateAutotestRuleConfig(leaderboardId);
        if (ruleConfig == null && !_ipc.IsConnected)
        {
            var connected = await _ipc.ConnectAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            if (!connected)
            {
                await ShowNoticeAsync("共斗挑战", "没有连接到共斗客户端。请保持客户端启动后再开始挑战。").ConfigureAwait(false);
                return false;
            }
        }

        ruleConfig ??= await _ipc.GetRuleConfigAsync(leaderboardId, cancellationToken).ConfigureAwait(false);
        if (ruleConfig == null)
        {
            GD.PrintErr($"[GongDou STS2] Rule config not found for leaderboard {leaderboardId}.");
            return false;
        }

        if (!string.Equals(ruleConfig.ChallengeType, GongdouSts2ChallengeMod.ChallengeType, StringComparison.OrdinalIgnoreCase))
        {
            GD.PrintErr($"[GongDou STS2] Unsupported challenge type: {ruleConfig.ChallengeType}");
            return false;
        }

        TryMarkPersistedChallengeInactive("fresh-start");

        var config = Sts2PuzzleConfig.FromRuleConfig(ruleConfig, stageOverride);
        var resources = ResourcePool.FromRuleConfig(ruleConfig, stageOverride);
        if (config.StageIndex <= 1)
        {
            _seriesElapsedBeforeCurrentStageMs = 0;
            _seriesTurnsBeforeCurrentStage = 0;
        }

        var sessionId = launchSessionId ?? Guid.NewGuid().ToString("N");
        var startedAtUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var nativeSaveSlotPrepared = false;

        _isStartingChallenge = true;
        DisableCompletedCombatHold();
        try
        {
            await WaitForGameStartupStableAsync(cancellationToken).ConfigureAwait(false);
            if (ackLaunchContext)
            {
                await _ipc.AckStartedAsync(leaderboardId, launchSessionId, cancellationToken).ConfigureAwait(false);
            }
            PrepareNativeSaveSlotForChallengeStart("start-challenge");
            nativeSaveSlotPrepared = true;
            await StartRecordingAsync(leaderboardId, sessionId, cancellationToken).ConfigureAwait(false);

            PreparedChallengeRun preparedRun;
            Func<Task>? closePreparationCover = null;
            try
            {
                await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => _bootstrapper.PrepareForMainMenuSelection()).ConfigureAwait(false);

                closePreparationCover = await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => ChallengePreparationOverlay.ShowTransitionOverlay(
                        "正在打开准备界面",
                        "正在创建可保存挑战对局，并打开原生卡牌选择器...")).ConfigureAwait(false);

                preparedRun = await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => _bootstrapper.CreateChallengeRunAsync(config)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (closePreparationCover != null)
                {
                    await GongdouSts2ChallengeMod.RunOnMainThread(closePreparationCover).ConfigureAwait(false);
                }

                stopwatch.Stop();
                GD.PrintErr($"[GongDou STS2] Cannot start challenge from current game state: {ex}");
                if (nativeSaveSlotPrepared)
                {
                    RestoreNativeRunSnapshotToCurrent("start-challenge-bootstrap-failed");
                }
                await StopRecordingAsync(leaderboardId, sessionId, cancellationToken).ConfigureAwait(false);
                await SubmitImmediateFailureAsync(
                    leaderboardId,
                    sessionId,
                    "start_failed",
                    ex.Message,
                    cancellationToken,
                    stopwatch.ElapsedMilliseconds,
                    config).ConfigureAwait(false);
                return true;
            }

            var activeRuntime = new ChallengeRuntime
            {
                LeaderboardId = leaderboardId,
                LaunchSessionId = sessionId,
                RuleConfig = ruleConfig,
                Config = config,
                Resources = resources,
                Selection = new ChallengeSelection(),
                StartResult = new ChallengeStartResult
                {
                    DeckSize = 0,
                    EnemyInitialHp = config.Enemy.GetInitialHp(0),
                    PlayerInitialHp = preparedRun.Player.Creature.CurrentHp,
                    PlayerMaxHp = preparedRun.Player.Creature.MaxHp,
                    StartedAt = startedAtUtc
                },
                Stopwatch = stopwatch,
                StartedAtUtc = startedAtUtc,
                SeriesElapsedBeforeStageMs = _seriesElapsedBeforeCurrentStageMs,
                SeriesTurnsBeforeStage = _seriesTurnsBeforeCurrentStage,
                LatestPlayerHp = preparedRun.Player.Creature.CurrentHp,
                LatestRound = 1,
                Phase = ChallengeRuntimePhase.Preparation
            };
            _active = activeRuntime;
            PersistActiveChallengeSession(activeRuntime);

            ChallengeSelection? selection;
            try
            {
                selection = await NativeChallengePreparationFlow.ShowAsync(
                    config,
                    resources,
                    preparedRun.RunState,
                    preparedRun.Player,
                    closePreparationCover).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (activeRuntime.IsSuspended)
                {
                    GD.Print($"[GongDou STS2] Challenge preparation stopped after save-and-quit: {ex.Message}");
                    return true;
                }

                if (closePreparationCover != null)
                {
                    await GongdouSts2ChallengeMod.RunOnMainThread(closePreparationCover).ConfigureAwait(false);
                }

                stopwatch.Stop();
                GD.PrintErr($"[GongDou STS2] Failed during challenge preparation: {ex}");
                await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => _bootstrapper.CancelChallengeRun()).ConfigureAwait(false);
                if (nativeSaveSlotPrepared)
                {
                    RestoreNativeRunSnapshotToCurrent("start-challenge-preparation-failed");
                }
                await StopRecordingAsync(leaderboardId, sessionId, cancellationToken).ConfigureAwait(false);
                await SubmitImmediateFailureAsync(
                    leaderboardId,
                    sessionId,
                    "preparation_failed",
                    ex.Message,
                    cancellationToken,
                    stopwatch.ElapsedMilliseconds,
                    config).ConfigureAwait(false);
                return true;
            }

            if (selection == null)
            {
                if (activeRuntime.IsSuspended)
                {
                    return true;
                }

                if (closePreparationCover != null)
                {
                    await GongdouSts2ChallengeMod.RunOnMainThread(closePreparationCover).ConfigureAwait(false);
                }

                stopwatch.Stop();
                await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => _bootstrapper.CancelChallengeRun()).ConfigureAwait(false);
                if (nativeSaveSlotPrepared)
                {
                    RestoreNativeRunSnapshotToCurrent("start-challenge-selection-cancelled");
                }
                await StopRecordingAsync(leaderboardId, sessionId, cancellationToken).ConfigureAwait(false);
                await SubmitImmediateFailureAsync(
                    leaderboardId,
                    sessionId,
                    "cancelled",
                    "Challenge was cancelled in the in-game preparation screen.",
                    cancellationToken,
                    stopwatch.ElapsedMilliseconds,
                    config).ConfigureAwait(false);
                return true;
            }

            ChallengeStartResult startResult;
            Func<Task>? closeTransitionOverlay = null;
            try
            {
                closeTransitionOverlay = await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => ChallengePreparationOverlay.ShowTransitionOverlay(
                        "正在进入战斗",
                        $"正在应用所选 {config.MaxCards} 张卡，并直接进入{config.Enemy.Name}战斗...")).ConfigureAwait(false);

                startResult = await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => _bootstrapper.ApplySelectionAndEnterCalcifiedCultistAsync(config, selection, preparedRun)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                GD.PrintErr($"[GongDou STS2] Failed to start challenge: {ex}");
                await GongdouSts2ChallengeMod.RunOnMainThread(
                    () => _bootstrapper.CancelChallengeRun()).ConfigureAwait(false);
                if (nativeSaveSlotPrepared)
                {
                    RestoreNativeRunSnapshotToCurrent("start-challenge-enter-combat-failed");
                }
                await StopRecordingAsync(leaderboardId, sessionId, cancellationToken).ConfigureAwait(false);
                await SubmitImmediateFailureAsync(
                    leaderboardId,
                    sessionId,
                    "start_failed",
                    ex.Message,
                    cancellationToken,
                    stopwatch.ElapsedMilliseconds,
                    config).ConfigureAwait(false);
                return true;
            }
            finally
            {
                if (closeTransitionOverlay != null)
                {
                    await GongdouSts2ChallengeMod.RunOnMainThread(closeTransitionOverlay).ConfigureAwait(false);
                }
            }

            _active = new ChallengeRuntime
            {
                LeaderboardId = leaderboardId,
                LaunchSessionId = sessionId,
                RuleConfig = ruleConfig,
                Config = config,
                Resources = resources,
                Selection = selection,
                StartResult = startResult,
                Stopwatch = stopwatch,
                StartedAtUtc = startedAtUtc,
                SeriesElapsedBeforeStageMs = _seriesElapsedBeforeCurrentStageMs,
                SeriesTurnsBeforeStage = _seriesTurnsBeforeCurrentStage,
                LatestPlayerHp = startResult.PlayerInitialHp,
                LatestRound = 1,
                Phase = ChallengeRuntimePhase.Combat
            };
            PersistActiveChallengeSession(_active);

            GD.Print($"[GongDou STS2] Challenge started: leaderboard={leaderboardId}, deck={selection.CardIds.Count}, enemyHp={startResult.EnemyInitialHp}.");
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            GD.PrintErr($"[GongDou STS2] Unexpected challenge start failure: {ex}");
            if (nativeSaveSlotPrepared)
            {
                RestoreNativeRunSnapshotToCurrent("start-challenge-unexpected-failed");
            }

            await StopRecordingAsync(leaderboardId, sessionId, cancellationToken).ConfigureAwait(false);
            if (!ackLaunchContext)
            {
                await ShowNoticeAsync("共斗挑战", $"启动挑战失败：{ex.Message}").ConfigureAwait(false);
            }

            return false;
        }
        finally
        {
            _isStartingChallenge = false;
        }
    }

    private static LeaderboardRuleConfig? TryCreateAutotestRuleConfig(int leaderboardId)
    {
        return IsAutotestPreparationMode(GetAutotestMode())
            ? new LeaderboardRuleConfig
            {
                LeaderboardId = leaderboardId,
                Title = "GongDou STS2 autotest",
                PresetName = $"autotest-{GetAutotestPreparationPresetName()}",
                ChallengeType = GongdouSts2ChallengeMod.ChallengeType
            }
            : null;
    }

    private static async Task WaitForGameStartupStableAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var consecutiveReadyTicks = 0;
        string? lastWaitingReason = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readiness = await GongdouSts2ChallengeMod.RunOnMainThread(() =>
            {
                GongdouSts2ChallengeMod.EnsureLocalizationRegistered();
                var game = NGame.Instance;
                if (game == null)
                {
                    return new GameStartupReadiness(false, "NGame.Instance is not available");
                }

                if (game.LogoAnimation != null)
                {
                    return new GameStartupReadiness(false, "logo animation is still active");
                }

                if (game.MainMenu == null && game.CurrentRunNode == null)
                {
                    return new GameStartupReadiness(false, "neither main menu nor run node is ready");
                }

                var reason = game.CurrentRunNode == null
                    ? "main menu is ready"
                    : "run node is active; bootstrapper will validate or clean it up";
                return new GameStartupReadiness(true, reason);
            }).ConfigureAwait(false);

            if (readiness.Ready)
            {
                consecutiveReadyTicks++;
                if (consecutiveReadyTicks >= 2)
                {
                    GD.Print("[GongDou STS2] Game startup readiness confirmed; starting challenge preparation.");
                    return;
                }
            }
            else
            {
                consecutiveReadyTicks = 0;
                if (!string.Equals(lastWaitingReason, readiness.Reason, StringComparison.Ordinal))
                {
                    lastWaitingReason = readiness.Reason;
                    GD.Print($"[GongDou STS2] Waiting for STS2 startup readiness: {readiness.Reason}.");
                }
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for STS2 main menu startup to stabilize.");
    }

    private async Task<PreparedLoadoutSelection?> ResolveSelectionAsync(
        int leaderboardId,
        string? launchSessionId,
        LeaderboardRuleConfig ruleConfig,
        CancellationToken cancellationToken)
    {
        if (!ruleConfig.RequiresLoadoutSelection)
        {
            return null;
        }

        var request = await _ipc.RequestLoadoutSelectionAsync(leaderboardId, launchSessionId, cancellationToken)
            .ConfigureAwait(false);
        if (request is not { Required: true } || string.IsNullOrWhiteSpace(request.SessionId))
        {
            return null;
        }

        var until = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < until && !cancellationToken.IsCancellationRequested)
        {
            var prepared = await _ipc.GetPreparedSelectionAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
            if (prepared != null)
            {
                return prepared;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for GongDou loadout selection.");
    }

    private async Task StartRecordingAsync(int leaderboardId, string launchSessionId, CancellationToken cancellationToken)
    {
        await StopRecorderOnlyAsync().ConfigureAwait(false);
        _lastRecordingSnapshot = null;

        try
        {
            using var ipcTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ipcTimeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
            await _ipc.RecordingStartAsync(leaderboardId, launchSessionId, ipcTimeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            GD.PrintErr($"[GongDou STS2] Recording start IPC timed out; challenge will continue. {ex.Message}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Recording start IPC failed; challenge will continue. {ex.Message}");
        }

        var recorder = new GodotFrameRecorder();
        _recorder = recorder;
        try
        {
            await recorder.StartAsync(30, cancellationToken, TimeSpan.FromSeconds(12)).ConfigureAwait(false);
            if (string.Equals(recorder.Status, "capture_failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(recorder.Status, "pipe_disconnected", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(recorder.Status, "unavailable", StringComparison.OrdinalIgnoreCase))
            {
                GD.PrintErr($"[GongDou STS2] Viewport recorder did not enter recording state: {recorder.Status}; {recorder.LastError}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Viewport recorder start failed; challenge will continue. {ex.Message}");
        }
    }

    private async Task StopRecordingAsync(int leaderboardId, string launchSessionId, CancellationToken cancellationToken)
    {
        await StopRecorderOnlyAsync().ConfigureAwait(false);

        try
        {
            using var ipcTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ipcTimeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            await _ipc.RecordingStopAsync(leaderboardId, launchSessionId, ipcTimeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            GD.PrintErr($"[GongDou STS2] Recording stop IPC timed out; challenge will continue. {ex.Message}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Recording stop IPC failed; challenge will continue. {ex.Message}");
        }
    }

    private async Task StopRecorderOnlyAsync()
    {
        var recorder = _recorder;
        if (recorder == null)
        {
            return;
        }

        try
        {
            await recorder.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Viewport recorder stop failed; challenge will continue. {ex.Message}");
        }
        finally
        {
            _lastRecordingSnapshot = RecordingSnapshot.From(recorder);
            _recorder = null;
        }

        try
        {
            await recorder.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Viewport recorder dispose failed; challenge will continue. {ex.Message}");
        }
    }

    private void QueueCompletion(bool success, string? failureReason)
    {
        _completionQueued = true;
        _ = GongdouSts2ChallengeMod.RunOnMainThread(() =>
        {
            if (!CombatManager.Instance.IsPaused)
            {
                CombatManager.Instance.Pause();
            }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await CompleteActiveChallengeAsync(success, failureReason, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GongDou STS2] Completion failed: {ex}");
            }
        });
    }

    private async Task CompleteActiveChallengeAsync(bool success, string? failureReason, CancellationToken cancellationToken)
    {
        var active = _active;
        if (active == null)
        {
            return;
        }

        StopStageTimer(active);
        active.EndedAtUtc = DateTimeOffset.UtcNow;
        await StopRecordingAsync(active.LeaderboardId, active.LaunchSessionId, cancellationToken).ConfigureAwait(false);

        var stageTimeMs = GetStageElapsedMs(active);
        var stageTurns = Math.Max(1, active.LatestRound);
        active.CumulativeTimeMs = active.SeriesElapsedBeforeStageMs + stageTimeMs;
        active.CumulativeTurns = active.SeriesTurnsBeforeStage + stageTurns;
        var completedStages = ResolveCompletedStages(active, success);
        var isRankedResult = completedStages > 0;
        var submitOutcome = isRankedResult ? "success" : "failure";
        var progressScore = CalculateProgressScore(completedStages, active.CumulativeTurns);
        var evidence = BuildEvidence(active, success, failureReason);
        JsonNode? resultActionResponse = null;

        var submit = new BattleSubmitRequest
        {
            LeaderboardId = active.LeaderboardId,
            SessionId = active.LaunchSessionId,
            LaunchSessionId = active.LaunchSessionId,
            TimeMs = active.CumulativeTimeMs,
            EventCount = Math.Max(1, active.EventCount),
            Outcome = submitOutcome,
            IsSuccessful = isRankedResult,
            FailureReason = isRankedResult ? null : failureReason,
            Evidence = evidence,
            ExtraDataJson = JsonSerializer.Serialize(new
            {
                active.Config.PuzzleId,
                stageIndex = active.Config.StageIndex,
                stageCount = active.Config.StageCount,
                turns = stageTurns,
                stageTurns,
                cumulativeTurns = active.CumulativeTurns,
                completedStages,
                progressScore,
                rankingMode = "completedStages_then_turns",
                stageCleared = success,
                rankedSuccess = isRankedResult,
                active.CardsPlayed,
                active.PotionsUsed,
                stageTimeMs,
                cumulativeTimeMs = active.CumulativeTimeMs,
                hpLost = active.StartResult.PlayerInitialHp - active.LatestPlayerHp,
                stageChain = new
                {
                    stageCount = active.Config.StageCount,
                    completedStages,
                    stageTurns,
                    cumulativeTurns = active.CumulativeTurns,
                    progressScore
                }
            }, JsonOptions.CamelCaseInsensitive)
        };

        try
        {
            resultActionResponse = await _ipc.SubmitBattleAsync(submit, cancellationToken).ConfigureAwait(false);
            GD.Print($"[GongDou STS2] Challenge completed: success={success}, ranked={isRankedResult}, stage={active.Config.StageIndex}, completedStages={completedStages}, cumulativeTurns={active.CumulativeTurns}, cards={active.CardsPlayed}.");
            if (!success && isRankedResult)
            {
                resultActionResponse = await SendLocalResultAsync(active, success, failureReason, evidence, cancellationToken)
                    .ConfigureAwait(false) ?? resultActionResponse;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Battle submit failed: {ex}");
            resultActionResponse = await SendLocalResultFallbackAsync(active, success, failureReason, evidence, ex, cancellationToken)
                .ConfigureAwait(false);
        }

        if (success && active.Config.StageIndex < active.Config.StageCount)
        {
            _seriesElapsedBeforeCurrentStageMs = active.CumulativeTimeMs;
            _seriesTurnsBeforeCurrentStage = active.CumulativeTurns;
        }

        var actionSessionId = ReadString(resultActionResponse, "resultActionSessionId");
        StartResultActionPolling(actionSessionId);

        await GongdouSts2ChallengeMod.RunOnMainThread(() =>
        {
            if (!CombatManager.Instance.IsPaused)
            {
                CombatManager.Instance.Pause();
            }
        }).ConfigureAwait(false);

        _lastResultLeaderboardId = active.LeaderboardId;
        _lastResultStageIndex = active.Config.StageIndex;
        _lastResultStageCount = active.Config.StageCount;
        _lastResultSeriesElapsedBeforeStageMs = active.SeriesElapsedBeforeStageMs;
        _lastResultSeriesTurnsBeforeStage = active.SeriesTurnsBeforeStage;
        _lastResultStageCleared = success;
        MarkPersistedChallengeSessionInactive(active, success ? "completed" : "failed");
        await FinalizeChallengeRunSaveSlotAsync(active).ConfigureAwait(false);
        _active = null;
        _completionQueued = false;
        _suspendQueued = false;
    }

    private async Task<JsonNode?> SendLocalResultFallbackAsync(
        ChallengeRuntime active,
        bool success,
        string? failureReason,
        Dictionary<string, object?> evidence,
        Exception submitError,
        CancellationToken cancellationToken)
    {
        evidence["submitError"] = submitError.Message;
        return await SendLocalResultAsync(active, success, failureReason, evidence, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<JsonNode?> SendLocalResultAsync(
        ChallengeRuntime active,
        bool success,
        string? failureReason,
        Dictionary<string, object?> evidence,
        CancellationToken cancellationToken)
    {
        var completedStages = ResolveCompletedStages(active, success);
        var isRankedResult = completedStages > 0;
        var outcome = isRankedResult ? "success" : "failure";

        try
        {
            if (!_ipc.IsConnected)
            {
                await _ipc.ConnectAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }

            if (_ipc.IsConnected)
            {
                return await _ipc.SendChallengeResultAsync(new ChallengeResultRequest
                {
                    LeaderboardId = active.LeaderboardId,
                    SessionId = active.LaunchSessionId,
                    Success = isRankedResult,
                    Outcome = outcome,
                    TimeMs = active.CumulativeTimeMs,
                    FailureReason = isRankedResult ? null : failureReason,
                    StageIndex = active.Config.StageIndex,
                    StageCount = active.Config.StageCount,
                    Evidence = evidence
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2] Local result fallback failed: {ex.Message}");
        }

        return null;
    }

    private void StartResultActionPolling(string? resultActionSessionId)
    {
        if (string.IsNullOrWhiteSpace(resultActionSessionId))
        {
            GD.Print("[GongDou STS2] Challenge result overlay did not return an action session id; waiting for client-side result flow.");
            return;
        }

        _ = Task.Run(async () =>
        {
            var deadline = DateTimeOffset.UtcNow.AddMinutes(20);
            while (DateTimeOffset.UtcNow < deadline && !_cts.IsCancellationRequested)
            {
                try
                {
                    if (!_ipc.IsConnected)
                    {
                        await _ipc.ConnectAsync(TimeSpan.FromSeconds(2), _cts.Token).ConfigureAwait(false);
                    }

                    if (_ipc.IsConnected)
                    {
                        var action = await _ipc.ConsumeChallengeResultActionAsync(resultActionSessionId, _cts.Token)
                            .ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(action?.Action))
                        {
                            GD.Print($"[GongDou STS2] Challenge result action consumed: {action.Action}.");
                            if (string.Equals(action.Action, "returnToMenu", StringComparison.OrdinalIgnoreCase))
                            {
                                await ReturnHeldChallengeToMainMenuAsync().ConfigureAwait(false);
                            }
                            else if (string.Equals(action.Action, "restart", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(action.Action, "next", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(action.Action, "continueNext", StringComparison.OrdinalIgnoreCase))
                            {
                                var isRestart = string.Equals(action.Action, "restart", StringComparison.OrdinalIgnoreCase);
                                if (!isRestart && !_lastResultStageCleared)
                                {
                                    GD.Print("[GongDou STS2] Ignored next-stage action because the last stage was not cleared.");
                                    continue;
                                }

                                var targetStageIndex = ResolveResultActionStageIndex(action, isRestart);
                                if (targetStageIndex is not > 0)
                                {
                                    await ShowNoticeAsync("GongDou Challenge", "Cannot resolve target stage for challenge result action.").ConfigureAwait(false);
                                    return;
                                }

                                if (targetStageIndex > Math.Max(1, _lastResultStageCount))
                                {
                                    await ShowNoticeAsync("GongDou Challenge", "There is no next stage for this challenge result.").ConfigureAwait(false);
                                    return;
                                }

                                if (isRestart &&
                                    targetStageIndex == _lastResultStageIndex)
                                {
                                    _seriesElapsedBeforeCurrentStageMs = _lastResultSeriesElapsedBeforeStageMs;
                                    _seriesTurnsBeforeCurrentStage = _lastResultSeriesTurnsBeforeStage;
                                }

                                await PrepareHeldChallengeForImmediateRelaunchAsync().ConfigureAwait(false);
                                var targetLeaderboardId = action.LeaderboardId is > 0
                                    ? action.LeaderboardId.Value
                                    : _lastResultLeaderboardId;
                                if (targetLeaderboardId is > 0)
                                {
                                    await StartLeaderboardChallengeAsync(
                                        targetLeaderboardId.Value,
                                        launchSessionId: null,
                                        ackLaunchContext: false,
                                        _cts.Token,
                                        stageOverride: targetStageIndex).ConfigureAwait(false);
                                }
                                else
                                {
                                    await ShowNoticeAsync("共斗挑战", "重新挑战失败：结算动作缺少排行榜 ID。").ConfigureAwait(false);
                                }
                            }

                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[GongDou STS2] Result action polling failed once: {ex.Message}");
                }

                try
                {
                    await Task.Delay(500, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            GD.Print("[GongDou STS2] Challenge result action polling expired.");
        });
    }

    private int? ResolveResultActionStageIndex(ChallengeResultActionConsumeResult action, bool isRestart)
    {
        if (action.StageIndex is > 0)
        {
            return action.StageIndex.Value;
        }

        if (isRestart)
        {
            return _lastResultStageIndex ?? 1;
        }

        return _lastResultStageCleared && _lastResultStageIndex is int lastStage
            ? lastStage + 1
            : null;
    }

    private static string? ReadString(JsonNode? node, string propertyName)
    {
        if (node is JsonObject obj &&
            obj.TryGetPropertyValue(propertyName, out var value) &&
            value != null)
        {
            try
            {
                return value.GetValue<string>();
            }
            catch
            {
                return value.ToString();
            }
        }

        return null;
    }

    private async Task SubmitImmediateFailureAsync(
        int leaderboardId,
        string launchSessionId,
        string reason,
        string message,
        CancellationToken cancellationToken,
        long timeMs = 0,
        Sts2PuzzleConfig? config = null)
    {
        var failureConfig = config ?? new Sts2PuzzleConfig();
        _lastResultLeaderboardId = leaderboardId;
        _lastResultStageIndex = failureConfig.StageIndex;
        _lastResultStageCount = failureConfig.StageCount;
        _lastResultSeriesElapsedBeforeStageMs = _seriesElapsedBeforeCurrentStageMs;
        _lastResultSeriesTurnsBeforeStage = _seriesTurnsBeforeCurrentStage;
        _lastResultStageCleared = false;

        DisableCompletedCombatHold();
        var recording = GetRecordingSnapshot();
        var evidence = new Dictionary<string, object?>
        {
            ["puzzleId"] = failureConfig.PuzzleId,
            ["challengeType"] = GongdouSts2ChallengeMod.ChallengeType,
            ["stageIndex"] = failureConfig.StageIndex,
            ["stageCount"] = failureConfig.StageCount,
            ["completedStages"] = 0,
            ["stageTurns"] = 0,
            ["cumulativeTurns"] = 0,
            ["progressScore"] = 0,
            ["rankingMode"] = "completedStages_then_turns",
            ["failureReason"] = reason,
            ["message"] = message,
            ["timeMs"] = timeMs,
            ["modVersion"] = GongdouSts2ChallengeMod.Version,
            ["gameVersion"] = ResolveGameVersion(),
            ["recordingStatus"] = recording.Status,
            ["recordingFrames"] = recording.FrameCount,
            ["recordingDroppedFrames"] = recording.DroppedFrames,
            ["recordingError"] = recording.LastError
        };

        var response = await _ipc.SubmitBattleAsync(new BattleSubmitRequest
        {
            LeaderboardId = leaderboardId,
            SessionId = launchSessionId,
            LaunchSessionId = launchSessionId,
            TimeMs = timeMs,
            EventCount = 1,
            Outcome = "failure",
            IsSuccessful = false,
            FailureReason = reason,
            Evidence = evidence,
            ExtraDataJson = JsonSerializer.Serialize(evidence, JsonOptions.CamelCaseInsensitive)
        }, cancellationToken).ConfigureAwait(false);

        StartResultActionPolling(ReadString(response, "resultActionSessionId"));
    }

    private Dictionary<string, object?> BuildEvidence(ChallengeRuntime active, bool success, string? failureReason)
    {
        var stageTurns = Math.Max(1, active.LatestRound);
        var stageTimeMs = GetStageElapsedMs(active);
        var completedStages = ResolveCompletedStages(active, success);
        var isRankedResult = completedStages > 0;
        var progressScore = CalculateProgressScore(completedStages, active.CumulativeTurns);
        var recording = GetRecordingSnapshot();

        return new Dictionary<string, object?>
        {
            ["puzzleId"] = active.Config.PuzzleId,
            ["challengeType"] = GongdouSts2ChallengeMod.ChallengeType,
            ["leaderboardId"] = active.LeaderboardId,
            ["stageIndex"] = active.Config.StageIndex,
            ["stageCount"] = active.Config.StageCount,
            ["completedStages"] = completedStages,
            ["stageTurns"] = stageTurns,
            ["cumulativeTurns"] = active.CumulativeTurns,
            ["progressScore"] = progressScore,
            ["rankingMode"] = "completedStages_then_turns",
            ["loadoutSource"] = active.Config.LoadoutSource,
            ["success"] = isRankedResult,
            ["stageCleared"] = success,
            ["rankedSuccess"] = isRankedResult,
            ["outcome"] = isRankedResult ? "success" : "failure",
            ["failureReason"] = isRankedResult ? null : failureReason,
            ["turns"] = stageTurns,
            ["timeMs"] = active.CumulativeTimeMs > 0 ? active.CumulativeTimeMs : stageTimeMs,
            ["stageTimeMs"] = stageTimeMs,
            ["cumulativeTimeMs"] = active.CumulativeTimeMs,
            ["startedAt"] = active.StartedAtUtc,
            ["endedAt"] = active.EndedAtUtc ?? DateTimeOffset.UtcNow,
            ["combatStartedAt"] = active.StartResult.StartedAt,
            ["modVersion"] = GongdouSts2ChallengeMod.Version,
            ["gameVersion"] = ResolveGameVersion(),
            ["hpLost"] = Math.Max(0, active.StartResult.PlayerInitialHp - active.LatestPlayerHp),
            ["remainingHp"] = active.LatestPlayerHp,
            ["cardsPlayed"] = active.CardsPlayed,
            ["potionsUsed"] = active.PotionsUsed,
            ["deckSize"] = active.StartResult.DeckSize,
            ["selectedCards"] = active.Selection.CardIds.ToArray(),
            ["selectedPotions"] = active.Selection.PotionIds.ToArray(),
            ["selectedRelics"] = active.Selection.RelicIds.ToArray(),
            ["playedCards"] = active.PlayedCards.ToArray(),
            ["usedPotions"] = active.UsedPotions.ToArray(),
            ["enemyStartHp"] = active.StartResult.EnemyInitialHp,
            ["enemyHpRule"] = new
            {
                baseHp = active.Config.Enemy.BaseHp,
                    winCondition = active.Config.WinConditionText,
                active.StartResult.DeckSize,
                initialHp = active.StartResult.EnemyInitialHp
            },
            ["enemyActions"] = BuildEnemyActionsEvidence(active.Config),
            ["enemyName"] = active.Config.Enemy.Name,
            ["puzzleDoc"] = active.Config.PuzzleDoc,
            ["killRound"] = success ? stageTurns : null,
            ["stageChain"] = new
            {
                completedStages,
                stageCount = active.Config.StageCount,
                stageTurns,
                cumulativeTurns = active.CumulativeTurns,
                progressScore
            },
            ["playerHpRemaining"] = active.LatestPlayerHp,
            ["recordingCaptureMode"] = "godot_viewport_raw_rgba",
            ["recordingStatus"] = recording.Status,
            ["recordingFrames"] = recording.FrameCount,
            ["recordingDroppedFrames"] = recording.DroppedFrames,
            ["recordingError"] = recording.LastError
        };
    }

    private RecordingSnapshot GetRecordingSnapshot()
    {
        return _recorder != null
            ? RecordingSnapshot.From(_recorder)
            : _lastRecordingSnapshot ?? RecordingSnapshot.NotStarted;
    }

    private static int ResolveCompletedStages(ChallengeRuntime active, bool success)
    {
        var completedStages = success ? active.Config.StageIndex : active.Config.StageIndex - 1;
        return Math.Clamp(completedStages, 0, Math.Max(1, active.Config.StageCount));
    }

    private static int CalculateProgressScore(int completedStages, int cumulativeTurns)
    {
        if (completedStages <= 0)
        {
            return 0;
        }

        return completedStages * 50_000_000 - Math.Max(0, cumulativeTurns);
    }

    private static object[] BuildEnemyActionsEvidence(Sts2PuzzleConfig config)
    {
        if (config.EnemyActions.Count > 0)
        {
            return config.EnemyActions
                .Select(action => new
                {
                    turn = action.Turn,
                        type = action.Type,
                        amount = action.Damage,
                        armorGain = action.ArmorGain,
                        failureReason = (string?)null,
                    description = action.Description
                })
                .Cast<object>()
                .ToArray();
        }

        return
        [
            new { turn = 1, type = "ritual", amount = 0 },
            new { turn = 2, type = "attack", amount = 9 },
            new { turn = 3, type = "attack", amount = 11 },
            new { turn = 4, type = "attack", amount = 13, failureReason = (string?)null }
        ];
    }

    private static string ResolveGameVersion()
    {
        try
        {
            var executablePath = OS.GetExecutablePath();
            var directory = string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetDirectoryName(executablePath);
            var releaseInfoPath = string.IsNullOrWhiteSpace(directory) ? null : Path.Combine(directory, "release_info.json");
            if (releaseInfoPath != null && File.Exists(releaseInfoPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(releaseInfoPath));
                if (document.RootElement.TryGetProperty("version", out var version) &&
                    version.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(version.GetString()))
                {
                    return version.GetString()!;
                }
            }
        }
        catch
        {
            // Evidence enrichment is best-effort.
        }

        return "unknown";
    }

    private sealed record PersistedChallengeSession
    {
        public bool Active { get; init; }
        public string? InactiveReason { get; init; }
        public string ChallengeType { get; init; } = "";
        public string ModVersion { get; init; } = "";
        public string Phase { get; init; } = nameof(ChallengeRuntimePhase.Combat);
        public int LeaderboardId { get; init; }
        public string LaunchSessionId { get; init; } = "";
        public Sts2PuzzleConfig? Config { get; init; }
        public ResourcePool? Resources { get; init; }
        public PersistedChallengeSelection Selection { get; init; } = new();
        public PersistedChallengeStartResult StartResult { get; init; } = new();
        public DateTimeOffset StartedAtUtc { get; init; }
        public DateTimeOffset? EndedAtUtc { get; init; }
        public DateTimeOffset PersistedAtUtc { get; init; }
        public long StageElapsedMs { get; init; }
        public long SeriesElapsedBeforeStageMs { get; init; }
        public int SeriesTurnsBeforeStage { get; init; }
        public int LatestRound { get; init; } = 1;
        public int LatestPlayerHp { get; init; }
        public int CardsPlayed { get; init; }
        public int PotionsUsed { get; init; }
        public int EventCount { get; init; }
        public bool HasSeenEnemy { get; init; }
        public string[] PlayedCards { get; init; } = [];
        public string[] UsedPotions { get; init; } = [];
    }

    private sealed record PersistedChallengeSelection
    {
        public string[] CardIds { get; init; } = [];
        public string[] PotionIds { get; init; } = [];
        public string[] RelicIds { get; init; } = [];
    }

    private sealed record PersistedChallengeStartResult
    {
        public int DeckSize { get; init; }
        public int EnemyInitialHp { get; init; }
        public int PlayerInitialHp { get; init; }
        public int PlayerMaxHp { get; init; }
        public DateTimeOffset StartedAt { get; init; }
    }

    private sealed record NativeRunSaveSnapshot
    {
        public bool HasRunSave { get; init; }
        public bool HasRunSaveBackup { get; init; }
        public DateTimeOffset CapturedAtUtc { get; init; }
        public string Reason { get; init; } = "";
    }

    private sealed class ChallengeRuntime
    {
        public required int LeaderboardId { get; init; }
        public required string LaunchSessionId { get; init; }
        public required LeaderboardRuleConfig RuleConfig { get; init; }
        public required Sts2PuzzleConfig Config { get; init; }
        public required ResourcePool Resources { get; init; }
        public required ChallengeSelection Selection { get; init; }
        public required ChallengeStartResult StartResult { get; set; }
        public required Stopwatch Stopwatch { get; init; }
        public required DateTimeOffset StartedAtUtc { get; init; }
        public long SeriesElapsedBeforeStageMs { get; init; }
        public int SeriesTurnsBeforeStage { get; init; }
        public long CumulativeTimeMs { get; set; }
        public int CumulativeTurns { get; set; }
        public DateTimeOffset? EndedAtUtc { get; set; }
        public int LatestRound { get; set; }
        public int LatestPlayerHp { get; set; }
        public int CardsPlayed { get; set; }
        public int PotionsUsed { get; set; }
        public int EventCount { get; set; }
        public bool HasSeenEnemy { get; set; }
        public long StageElapsedBeforeStopwatchMs { get; set; }
        public bool IsSuspended { get; set; }
        public ChallengeRuntimePhase Phase { get; set; } = ChallengeRuntimePhase.Combat;
        public List<string> PlayedCards { get; } = [];
        public List<string> UsedPotions { get; } = [];
    }

    private sealed record RecordingSnapshot(
        string Status,
        int FrameCount,
        int DroppedFrames,
        string? LastError)
    {
        public static readonly RecordingSnapshot NotStarted = new("not_started", 0, 0, null);

        public static RecordingSnapshot From(GodotFrameRecorder recorder)
        {
            return new RecordingSnapshot(
                string.IsNullOrWhiteSpace(recorder.Status) ? "unknown" : recorder.Status,
                recorder.FrameCount,
                recorder.DroppedFrames,
                recorder.LastError);
        }
    }

    private sealed record GameStartupReadiness(bool Ready, string Reason);
}
