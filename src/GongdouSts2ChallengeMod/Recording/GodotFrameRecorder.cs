using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using Godot;

namespace GongdouSts2ChallengeMod.Recording;

public sealed class GodotFrameRecorder : IAsyncDisposable
{
    public const string VideoPipeName = "gongdou.mod.ipc.v1.video";
    private const int RawMagic = 0x52524248; // HBRR
    private const int PixelFormatRgba32 = 1;
    private const int PipeConnectAttemptTimeoutMs = 1500;
    private const int PipeWriteTimeoutMs = 1500;
    private const int MainThreadCaptureTimeoutMs = 900;
    private const int MaxConsecutiveFrameErrors = 90;
    private const int MaxAdaptiveSkipFrames = 3;
    private readonly CancellationTokenSource _disposeCts = new();
    private NamedPipeClientStream? _pipe;
    private Task? _loopTask;
    private volatile bool _isRunning;
    private int _captureWidth;
    private int _captureHeight;

    public string Status { get; private set; } = "idle";
    public int FrameCount { get; private set; }
    public int DroppedFrames { get; private set; }
    public string? LastError { get; private set; }

    public async Task StartAsync(int fps, CancellationToken cancellationToken)
    {
        await StartAsync(fps, cancellationToken, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
    }

    public async Task StartAsync(int fps, CancellationToken cancellationToken, TimeSpan startupRetryWindow)
    {
        if (_loopTask is { IsCompleted: false })
        {
            return;
        }

        await DisposePipeAsync().ConfigureAwait(false);
        LastError = null;
        FrameCount = 0;
        DroppedFrames = 0;
        _captureWidth = 0;
        _captureHeight = 0;
        Status = "connecting";
        _isRunning = true;
        _loopTask = Task.Run(
            () => ConnectAndCaptureLoopAsync(Math.Clamp(fps, 1, 60), startupRetryWindow, _disposeCts.Token),
            CancellationToken.None);

        try
        {
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _isRunning = false;
        }
    }

    private async Task ConnectAndCaptureLoopAsync(int fps, TimeSpan startupRetryWindow, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + startupRetryWindow;
        var retryDelay = TimeSpan.FromMilliseconds(250);

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pipe = await ConnectPipeOnceAsync(cancellationToken).ConfigureAwait(false);
                Status = "recording";
                LastError = null;
                await CaptureLoopAsync(fps, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                await DisposePipeAsync().ConfigureAwait(false);
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    Status = "waiting_for_pipe";
                    retryDelay = TimeSpan.FromSeconds(1);
                }

                try
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (Status == "connecting" || Status == "waiting_for_pipe")
        {
            Status = "stopped";
        }
    }

    private static async Task<NamedPipeClientStream> ConnectPipeOnceAsync(CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", VideoPipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(PipeConnectAttemptTimeoutMs);
            await pipe.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        if (_loopTask != null)
        {
            try
            {
                await _loopTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // Recording is best-effort. Challenge flow must not hang on video teardown.
            }
        }

        await DisposePipeAsync().ConfigureAwait(false);
        if (Status == "recording" || Status == "connecting" || Status == "waiting_for_pipe")
        {
            Status = "stopped";
        }
    }

    private async Task CaptureLoopAsync(int fps, CancellationToken cancellationToken)
    {
        var frameInterval = TimeSpan.FromMilliseconds(Math.Max(1.0, 1000.0 / fps));
        var stopwatch = Stopwatch.StartNew();
        var nextFrameAt = TimeSpan.Zero;
        var consecutiveErrors = 0;

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            var wait = nextFrameAt - stopwatch.Elapsed;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var frameStartedAt = stopwatch.Elapsed;
                var frame = await GongdouSts2ChallengeMod.RunOnMainThread(() => CaptureFrame())
                    .WaitAsync(TimeSpan.FromMilliseconds(MainThreadCaptureTimeoutMs), cancellationToken)
                    .ConfigureAwait(false);
                if (frame == null)
                {
                    DroppedFrames++;
                    nextFrameAt = stopwatch.Elapsed + frameInterval;
                    continue;
                }

                await WriteRawFrameAsync(frame.Value, cancellationToken).ConfigureAwait(false);
                FrameCount++;
                consecutiveErrors = 0;
                var frameElapsed = stopwatch.Elapsed - frameStartedAt;
                var recoveryInterval = frameInterval;
                if (frameElapsed.Ticks > frameInterval.Ticks * 3 / 2)
                {
                    var skipped = Math.Clamp(
                        (int)Math.Ceiling(frameElapsed.TotalMilliseconds / frameInterval.TotalMilliseconds) - 1,
                        1,
                        MaxAdaptiveSkipFrames);
                    DroppedFrames += skipped;
                    recoveryInterval = TimeSpan.FromTicks(frameInterval.Ticks * (skipped + 1));
                }

                nextFrameAt = stopwatch.Elapsed + recoveryInterval;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                DroppedFrames++;
                LastError = ex.Message;
                Status = "pipe_write_timeout";
                _isRunning = false;
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                DroppedFrames++;
                LastError = ex.Message;
                Status = "pipe_disconnected";
                _isRunning = false;
                break;
            }
            catch (ObjectDisposedException ex)
            {
                DroppedFrames++;
                LastError = ex.Message;
                Status = "pipe_disconnected";
                _isRunning = false;
                break;
            }
            catch (Exception ex)
            {
                DroppedFrames++;
                LastError = ex.Message;
                consecutiveErrors++;
                nextFrameAt = stopwatch.Elapsed + frameInterval;
                if (consecutiveErrors >= MaxConsecutiveFrameErrors)
                {
                    Status = "capture_failed";
                    _isRunning = false;
                    break;
                }
            }
        }
    }

    private CapturedFrame? CaptureFrame()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        var viewport = tree?.Root;
        var texture = viewport?.GetTexture();
        var image = texture?.GetImage();
        if (image == null || image.IsEmpty())
        {
            return null;
        }

        image.Convert(Image.Format.Rgba8);
        var width = image.GetWidth();
        var height = image.GetHeight();
        width -= width % 2;
        height -= height % 2;
        if (width < 64 || height < 64)
        {
            return null;
        }

        if (_captureWidth <= 0 || _captureHeight <= 0)
        {
            _captureWidth = width;
            _captureHeight = height;
        }

        if (width != image.GetWidth() || height != image.GetHeight())
        {
            image.Crop(width, height);
        }

        if (width != _captureWidth || height != _captureHeight)
        {
            // FFmpeg rawvideo uses the first frame size for the whole stream, so keep this run at native first-frame size.
            image.Resize(_captureWidth, _captureHeight, Image.Interpolation.Bilinear);
        }

        return new CapturedFrame(_captureWidth, _captureHeight, image.GetData());
    }

    private async Task WriteRawFrameAsync(CapturedFrame frame, CancellationToken cancellationToken)
    {
        if (_pipe == null || !_pipe.IsConnected)
        {
            throw new IOException("Video pipe is not connected.");
        }

        var header = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), RawMagic);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), frame.Data.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), frame.Width);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), frame.Height);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), PixelFormatRgba32);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20, 4), 0);
        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        writeCts.CancelAfter(PipeWriteTimeoutMs);
        var writeToken = writeCts.Token;

        await _pipe.WriteAsync(header, writeToken).ConfigureAwait(false);
        await _pipe.WriteAsync(frame.Data, writeToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        await StopAsync().ConfigureAwait(false);
        _disposeCts.Dispose();
    }

    private async ValueTask DisposePipeAsync()
    {
        if (_pipe != null)
        {
            await _pipe.DisposeAsync().ConfigureAwait(false);
            _pipe = null;
        }
    }

    private readonly record struct CapturedFrame(int Width, int Height, byte[] Data);
}
