using System.Diagnostics;

namespace ScreenCatch.Core.Capture;

/// <summary>
/// Reference capture-source implementation that exercises descriptor resolution,
/// timing, start/stop/cancel, and in-memory frame collection.
/// </summary>
public class SyntheticScreenCaptureSource : IScreenCaptureSource
{
    private readonly IFrameProvider _frameProvider;
    private readonly object _sync = new();

    private Task? _captureTask;
    private CancellationTokenSource? _captureCts;
    private List<CaptureFrame> _capturedFrames = new();
    private bool _stopRequested;
    private Exception? _backgroundFailure;

    public SyntheticScreenCaptureSource(IFrameProvider frameProvider)
    {
        _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
    }

    public event EventHandler<CaptureProgress>? Progress;

    public bool IsCapturing { get; private set; }

    public Task StartAsync(CaptureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaptureDescriptorValidator.Validate(request.Descriptor, _frameProvider.Topology);

        lock (_sync)
        {
            if (IsCapturing)
            {
                throw new InvalidOperationException("Capture is already running.");
            }

            _capturedFrames = new List<CaptureFrame>(capacity: request.MaxFrames);
            _backgroundFailure = null;
            _stopRequested = false;
            IsCapturing = true;
            _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _captureTask = Task.Run(() => CaptureLoopAsync(request, _captureCts.Token));
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<CaptureFrame>> StopAsync(CancellationToken cancellationToken = default)
    {
        Task? captureTask;

        lock (_sync)
        {
            if (!IsCapturing && _captureTask is null)
            {
                return _capturedFrames.ToArray();
            }

            _stopRequested = true;
            captureTask = _captureTask;
        }

        if (captureTask is not null)
        {
            await captureTask.ConfigureAwait(false);
        }

        if (_backgroundFailure is not null)
        {
            throw new InvalidOperationException("Capture loop failed.", _backgroundFailure);
        }

        lock (_sync)
        {
            return _capturedFrames.ToArray();
        }
    }

    public async Task CancelAsync()
    {
        Task? captureTask;

        lock (_sync)
        {
            if (!IsCapturing)
            {
                return;
            }

            _captureCts?.Cancel();
            captureTask = _captureTask;
        }

        if (captureTask is not null)
        {
            try
            {
                await captureTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected cancellation path
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CancelAsync().ConfigureAwait(false);

        lock (_sync)
        {
            _captureCts?.Dispose();
            _captureCts = null;
            _captureTask = null;
            IsCapturing = false;
        }
    }

    private async Task CaptureLoopAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        var sourceRect = CaptureGeometryResolver.ResolveDescriptorRect(request.Descriptor, _frameProvider.Topology);
        var stopwatch = Stopwatch.StartNew();
        var nextDue = TimeSpan.Zero;
        var frameIndex = 0;

        try
        {
            while (!_stopRequested && !cancellationToken.IsCancellationRequested && frameIndex < request.MaxFrames)
            {
                var fullFrame = _frameProvider.CaptureFrame(frameIndex, DateTimeOffset.UtcNow);
                var clipped = FrameCropper.CropFrame(fullFrame, sourceRect);

                lock (_sync)
                {
                    _capturedFrames.Add(clipped);
                }

                frameIndex++;
                Progress?.Invoke(this, new CaptureProgress(frameIndex, request.MaxFrames, stopwatch.Elapsed));

                nextDue += request.FrameInterval;
                var delay = nextDue - stopwatch.Elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected cancellation path
        }
        catch (Exception ex)
        {
            _backgroundFailure = ex;
        }
        finally
        {
            lock (_sync)
            {
                IsCapturing = false;
                _stopRequested = false;

                _captureCts?.Dispose();
                _captureCts = null;
                _captureTask = null;
            }
        }
    }
}
