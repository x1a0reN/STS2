using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace GongdouSts2FrierenMod.Diagnostics;

internal static class ModDiagnosticReporter
{
    private const string PipeName = "gongdou.mod.ipc.v1";
    private const string ExecutableName = "SlayTheSpire2.exe";
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastSentAtBySignature = new(StringComparer.Ordinal);
    private static int _globalHandlersInstalled;

    public static void InstallGlobalHandlers(string source, string version)
    {
        if (Interlocked.Exchange(ref _globalHandlersInstalled, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception ?? new InvalidOperationException(StringifyExceptionObject(args.ExceptionObject));
            ReportException($"{source}.unhandled", version, exception, "fatal");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ReportException($"{source}.task", version, args.Exception, "error");
            args.SetObserved();
        };
    }

    public static void ReportException(string source, string version, Exception exception, string severity = "error")
    {
        Report(source, version, severity, exception.GetType().FullName ?? exception.GetType().Name, exception.Message, exception.ToString());
    }

    private static void Report(
        string source,
        string version,
        string severity,
        string exceptionType,
        string message,
        string stack)
    {
        var signature = $"{source}:{exceptionType}:{message}";
        var now = DateTimeOffset.UtcNow;
        if (LastSentAtBySignature.TryGetValue(signature, out var lastSentAt) && now - lastSentAt < TimeSpan.FromMinutes(10))
        {
            return;
        }
        LastSentAtBySignature[signature] = now;

        _ = Task.Run(async () =>
        {
            try
            {
                await SendAsync(source, version, severity, exceptionType, message, stack, now).ConfigureAwait(false);
            }
            catch
            {
                // Diagnostics must never affect the game process.
            }
        });
    }

    private static async Task SendAsync(
        string source,
        string version,
        string severity,
        string exceptionType,
        string message,
        string stack,
        DateTimeOffset capturedAt)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
        await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);

        var request = new
        {
            id = Guid.NewGuid().ToString("N"),
            method = "mod.diagnostics.upload",
            payload = new
            {
                source,
                version,
                severity,
                exceptionType,
                message,
                stack,
                executableName = ExecutableName,
                executableAliases = new[] { "SlayTheSpire2.exe", "Slay the Spire 2.exe" },
                capturedAt = capturedAt.ToString("O")
            }
        };

        await WriteFrameAsync(pipe, request, timeout.Token).ConfigureAwait(false);
        await ReadAndIgnoreResponseAsync(pipe, timeout.Token).ConfigureAwait(false);
    }

    private static async Task WriteFrameAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value);
        var payload = Encoding.UTF8.GetBytes(json);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadAndIgnoreResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > 1024 * 1024)
        {
            return;
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private static string StringifyExceptionObject(object? value)
    {
        return value == null ? "Unhandled exception object was null." : value.ToString() ?? value.GetType().FullName ?? "Unknown unhandled exception.";
    }
}
