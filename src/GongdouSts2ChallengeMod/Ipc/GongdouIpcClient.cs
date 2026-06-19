using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GongdouSts2ChallengeMod.Models;

namespace GongdouSts2ChallengeMod.Ipc;

public sealed class GongdouIpcClient : IAsyncDisposable
{
    public const string ControlPipeName = "gongdou.mod.ipc.v1";
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private NamedPipeClientStream? _pipe;
    private long _nextId;

    public bool IsConnected => _pipe?.IsConnected == true;

    public async Task<bool> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return true;
        }

        await DisposePipeAsync().ConfigureAwait(false);
        var pipe = new NamedPipeClientStream(
            ".",
            ControlPipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            await pipe.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);
            _pipe = pipe;
            return true;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            return false;
        }
    }

    public Task<LaunchContext?> ConsumeLaunchContextAsync(CancellationToken cancellationToken)
    {
        var payload = new
        {
            executableName = GongdouSts2ChallengeMod.ExecutableName,
            executableAliases = new[] { "SlayTheSpire2.exe", "Slay the Spire 2.exe" }
        };
        return RequestAsync<LaunchContext>("mod.launch.consumeContext", payload, cancellationToken);
    }

    public Task<List<LeaderboardSummary>?> ListLeaderboardsAsync(CancellationToken cancellationToken)
    {
        var payload = new
        {
            executableName = GongdouSts2ChallengeMod.ExecutableName,
            executableAliases = new[] { "SlayTheSpire2.exe", "Slay the Spire 2.exe" }
        };
        return RequestAsync<List<LeaderboardSummary>>("mod.leaderboard.list", payload, cancellationToken);
    }

    public Task<LeaderboardRuleConfig?> GetRuleConfigAsync(int leaderboardId, CancellationToken cancellationToken)
    {
        return RequestAsync<LeaderboardRuleConfig>("mod.leaderboard.getRuleConfig", new { leaderboardId }, cancellationToken);
    }

    public Task<JsonNode?> AckStartedAsync(int leaderboardId, string? launchSessionId, CancellationToken cancellationToken)
    {
        return RequestJsonAsync("mod.launch.ackStarted", new
        {
            leaderboardId,
            executableName = GongdouSts2ChallengeMod.ExecutableName,
            launchSessionId
        }, cancellationToken);
    }

    public Task<LoadoutRequestResult?> RequestLoadoutSelectionAsync(
        int leaderboardId,
        string? launchSessionId,
        CancellationToken cancellationToken)
    {
        return RequestAsync<LoadoutRequestResult>("mod.loadout.requestSelection", new
        {
            leaderboardId,
            executableName = GongdouSts2ChallengeMod.ExecutableName,
            launchSessionId
        }, cancellationToken);
    }

    public Task<PreparedLoadoutSelection?> GetPreparedSelectionAsync(string? sessionId, CancellationToken cancellationToken)
    {
        return RequestAsync<PreparedLoadoutSelection>("mod.loadout.getPreparedSelection", new { sessionId }, cancellationToken);
    }

    public Task<JsonNode?> SubmitBattleAsync(BattleSubmitRequest request, CancellationToken cancellationToken)
    {
        return RequestJsonAsync("mod.battle.submit", request, cancellationToken);
    }

    public Task<JsonNode?> SendChallengeResultAsync(ChallengeResultRequest request, CancellationToken cancellationToken)
    {
        return RequestJsonAsync("mod.challenge.result", request, cancellationToken);
    }

    public Task<ChallengeResultActionConsumeResult?> ConsumeChallengeResultActionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        return RequestAsync<ChallengeResultActionConsumeResult>(
            "mod.challenge.resultAction.consume",
            new { sessionId },
            cancellationToken);
    }

    public Task<JsonNode?> RecordingStartAsync(int leaderboardId, string sessionId, CancellationToken cancellationToken)
    {
        return RequestJsonAsync("mod.recording.start", new
        {
            leaderboardId,
            sessionId,
            source = "sts2_mod_internal",
            executableName = GongdouSts2ChallengeMod.ExecutableName,
            executableAliases = new[] { "SlayTheSpire2.exe", "Slay the Spire 2.exe" },
            processNames = new[] { "SlayTheSpire2", "SlayTheSpire2.exe" },
            windowTitles = new[] { "Slay the Spire 2", "SlayTheSpire2" },
            allowExternalCapture = false
        }, cancellationToken);
    }

    public Task<JsonNode?> RecordingStopAsync(int leaderboardId, string sessionId, CancellationToken cancellationToken)
    {
        return RequestJsonAsync("mod.recording.stop", new
        {
            leaderboardId,
            sessionId,
            source = "sts2_mod_internal",
            executableName = GongdouSts2ChallengeMod.ExecutableName
        }, cancellationToken);
    }

    private async Task<T?> RequestAsync<T>(string method, object payload, CancellationToken cancellationToken)
    {
        var data = await RequestJsonAsync(method, payload, cancellationToken).ConfigureAwait(false);
        if (data == null)
        {
            return default;
        }

        return data.Deserialize<T>(JsonOptions.CamelCaseInsensitive);
    }

    private async Task<JsonNode?> RequestJsonAsync(string method, object payload, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new IOException("GongDou control pipe is not connected.");
        }

        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var envelope = new IpcRequest
            {
                Id = Interlocked.Increment(ref _nextId).ToString(),
                Method = method,
                Payload = JsonSerializer.SerializeToNode(payload, JsonOptions.CamelCaseInsensitive)
            };

            await WriteFrameAsync(_pipe!, envelope, cancellationToken).ConfigureAwait(false);
            var response = await ReadFrameAsync<IpcResponse>(_pipe!, cancellationToken).ConfigureAwait(false);
            if (response == null)
            {
                throw new IOException("GongDou IPC response was empty.");
            }

            if (!response.Ok)
            {
                throw new InvalidOperationException($"{response.Code}: {response.Message}");
            }

            return response.Data;
        }
        catch
        {
            await DisposePipeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private static async Task WriteFrameAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions.CamelCaseInsensitive);
        var payload = Encoding.UTF8.GetBytes(json);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T?> ReadFrameAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException($"Invalid IPC frame length: {length}");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions.CamelCaseInsensitive);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposePipeAsync().ConfigureAwait(false);
        _requestLock.Dispose();
    }

    private async ValueTask DisposePipeAsync()
    {
        if (_pipe != null)
        {
            await _pipe.DisposeAsync().ConfigureAwait(false);
            _pipe = null;
        }
    }

    private sealed record IpcRequest
    {
        public string Id { get; init; } = "";
        public string Method { get; init; } = "";
        public JsonNode? Payload { get; init; }
    }

    private sealed record IpcResponse
    {
        public string Id { get; init; } = "";
        public bool Ok { get; init; }
        public string? Code { get; init; }
        public string? Message { get; init; }
        public JsonNode? Data { get; init; }
    }
}

public sealed record LoadoutRequestResult
{
    public bool Required { get; init; }
    public string? SessionId { get; init; }
    public JsonNode? Session { get; init; }
}

public sealed record BattleSubmitRequest
{
    public int LeaderboardId { get; init; }
    public string? SessionId { get; init; }
    public string? LaunchSessionId { get; init; }
    public long TimeMs { get; init; }
    public int EventCount { get; init; }
    public string Outcome { get; init; } = "";
    public bool IsSuccessful { get; init; }
    public string? FailureReason { get; init; }
    public string ExtraDataJson { get; init; } = "{}";
    public Dictionary<string, object?> Evidence { get; init; } = new();
}

public sealed record ChallengeResultRequest
{
    public int LeaderboardId { get; init; }
    public string SessionId { get; init; } = "";
    public bool Success { get; init; }
    public string Outcome { get; init; } = "";
    public long TimeMs { get; init; }
    public string? FailureReason { get; init; }
    public int? StageIndex { get; init; }
    public int? StageCount { get; init; }
    public Dictionary<string, object?> Evidence { get; init; } = new();
}

public sealed record ChallengeResultActionConsumeResult
{
    public string? SessionId { get; init; }
    public string? Action { get; init; }
    public int? LeaderboardId { get; init; }
    public int? StageIndex { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed record LeaderboardSummary
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public bool IsActive { get; init; } = true;
    public int? PresetId { get; init; }
    public string? PresetName { get; init; }
}
