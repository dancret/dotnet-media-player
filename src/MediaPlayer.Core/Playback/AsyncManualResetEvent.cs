namespace MediaPlayer.Playback;

/// <summary>
/// Lightweight async manual-reset event. Use to pause/resume pipelines without busy waiting.
/// </summary>
public sealed class AsyncManualResetEvent
{
    // Run continuations asynchronously to avoid executing them inline on Set()
    private volatile TaskCompletionSource<bool> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Waits until the event is Set, or the token is canceled.
    /// </summary>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        // .NET 6+ has Task.WaitAsync(CancellationToken)
        return _tcs.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Puts the event into the signaled state, releasing all current and future waiters
    /// until the next Reset().
    /// </summary>
    public void Set()
    {
        var tcs = _tcs;
        if (!tcs.Task.IsCompleted)
        {
            tcs.TrySetResult(true);
        }
    }

    /// <summary>
    /// Puts the event into the non-signaled state. Future waiters will block until Set().
    /// </summary>
    public void Reset()
    {
        if (_tcs.Task.IsCompleted)
        {
            Interlocked.Exchange(ref _tcs, new(TaskCreationOptions.RunContinuationsAsynchronously));
        }
    }
}
