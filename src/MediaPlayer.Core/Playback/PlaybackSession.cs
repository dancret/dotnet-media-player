using MediaPlayer.Input;
using MediaPlayer.Output;
using MediaPlayer.Tracks;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace MediaPlayer.Playback;

/// <summary>
/// Represents a playback session for managing audio playback of a specific track.
/// This class handles the playback lifecycle, including starting, pausing, resuming,
/// and canceling playback operations. It also manages retries for transient errors
/// and logs the playback process.
/// </summary>
/// <remarks>
/// The <see cref="PlaybackSession"/> class is designed to work with an audio source
/// and sink, facilitating the streaming of audio data. It supports asynchronous
/// operations and ensures proper resource management through the implementation
/// of <see cref="IAsyncDisposable"/>.
/// </remarks>
internal sealed class PlaybackSession(
    Track track,
    IAudioSource source,
    IAudioSink sink,
    ILogger logger)
    : IAsyncDisposable
{
    #region Constants

    // PCM 48kHz / stereo / 16-bit
    private const int SampleRate = 48000;
    private const int Channels = 2;
    private const int BytesPerSample = 2;
    private const int BytesPerSecond = SampleRate * Channels * BytesPerSample;

    private const int DefaultCopyBufferSize = 81920;

    private const int MaxAttempts = 3;
    private const int RetryPauseMs = 200;

    #endregion

    #region Fields

    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly AsyncManualResetEvent _pauseGate = new();

    #endregion

    /// <summary>
    /// Current track that is played.
    /// </summary>
    public Track Track { get; } = track;

    /// <summary>
    /// Utc start time of the track.
    /// </summary>
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Executes the playback session asynchronously, managing the playback lifecycle
    /// including retries for transient errors and handling cancellation requests.
    /// </summary>
    /// <param name="ct">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an
    /// <see cref="PlaybackEndReason"/> indicating the reason for the session's termination and optional details.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the provided <paramref name="ct"/>.</exception>
    /// <exception cref="Exception">Thrown if an unexpected error occurs during the playback session.</exception>
    public Task<PlaybackEndResult> StartAsync(CancellationToken ct) => RunWithRetriesAsync(ct);

    /// <summary>
    /// Pauses the current playback session, transitioning it to a paused state.
    /// </summary>
    /// <remarks>
    /// Allows playback to be resumed later from the same position.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the session cannot be paused in its current state.
    /// </exception>
    public void Pause()
    {
        logger.LogDebug("PlaybackSession.Pause called");
        _pauseGate.Reset();
    }

    /// <summary>
    /// Resumes playback from a paused state.
    /// </summary>
    /// <remarks>
    /// Should only be called when the session is paused.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the session is not paused.
    /// </exception>
    public void Resume()
    {
        logger.LogDebug("PlaybackSession.Resume called");
        _pauseGate.Set();
    }

    /// <summary>
    /// Runs the playback with retry semantics for transient failures.
    /// </summary>
    private async Task<PlaybackEndResult> RunWithRetriesAsync(CancellationToken ct)
    {
        _pauseGate.Set(); // start unpaused

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["sessionId"] = _sessionId,
            ["source"] = Track.Input.ToString(),
            ["trackId"] = Track.Uri
        });

        try
        {
            var attempt = 0;

            while (!ct.IsCancellationRequested)
            {
                attempt++;

                logger.LogInformation(
                    "Starting playback attempt {Attempt}/{MaxAttempts} for track {TrackId}",
                    attempt,
                    MaxAttempts,
                    Track.Uri);

                try
                {
                    LogStart();
                    await RunOnceAsync(ct).ConfigureAwait(false);
                    LogSuccess();
                    return new PlaybackEndResult(PlaybackEndReason.Completed);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    LogCanceled();
                    return new PlaybackEndResult(PlaybackEndReason.Cancelled);
                }
                catch (Exception ex) when (attempt < MaxAttempts && !ct.IsCancellationRequested)
                {
                    await HandleTransientErrorAsync(ex, attempt, ct).ConfigureAwait(false);
                    // fall through to next attempt
                }
                catch (Exception ex)
                {
                    LogFatal(ex);
                    return new PlaybackEndResult(PlaybackEndReason.Failed, ex.Message, ex);
                }
            }

            return ct.IsCancellationRequested
                ? new PlaybackEndResult(PlaybackEndReason.Cancelled)
                : new PlaybackEndResult(PlaybackEndReason.Failed, "Maximum attempts reached");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PlaybackSession failed for {TrackId}", Track.Uri);
            return new PlaybackEndResult(PlaybackEndReason.Failed, ex.Message, ex);
        }
    }

    /// <summary>
    /// Executes a single playback attempt: open reader, copy to sink, complete.
    /// </summary>
    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var reader = await source
            .OpenReaderAsync(Track, ct)
            .ConfigureAwait(false);

        var totalBytes = await CopyAsync(reader, ct).ConfigureAwait(false);

        await sink.CompleteAsync(ct).ConfigureAwait(false);

        logger.LogDebug("Track {TrackId} total bytes={Bytes}", Track.Uri, totalBytes);
    }

    /// <summary>
    /// Core copy loop: reader -> pause gate -> sink.
    /// All pause/back-pressure logic lives here.
    /// </summary>
    private async Task<long> CopyAsync(IAudioTrackReader reader, CancellationToken ct)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(DefaultCopyBufferSize);
        long totalBytes = 0;
        long lastLogBytes = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                logger.LogTrace("Copy loop tick, ct={Cancelled}", ct.IsCancellationRequested);

                // Pause gate ﷿ blocks when paused
                // Must set pause before read, so that we avoid:
                // - reading partial frames
                // - decode partial input
                // - streams closing while pause
                await _pauseGate.WaitAsync(ct).ConfigureAwait(false);
                
                var bytesRead = await reader.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (bytesRead <= 0) break;

                // Back-pressure from sink
                await sink.WriteAsync(buffer.AsMemory(0, bytesRead), ct)
                          .ConfigureAwait(false);

                totalBytes += bytesRead;

                if (ShouldLogProgress(totalBytes, lastLogBytes))
                {
                    LogProgress(totalBytes);
                    lastLogBytes = totalBytes;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return totalBytes;
    }

    #region Logging and Error Handling

    private static bool ShouldLogProgress(long totalBytes, long lastLogBytes)
    {
        // Log every ~1MB or roughly every 5s of audio
        if (totalBytes - lastLogBytes >= 1_000_000)
            return true;

        var posMs = totalBytes * 1000L / BytesPerSecond;
        return posMs % 5000 < 20; // approx every 5s
    }

    private void LogProgress(long totalBytes)
    {
        var posMs = totalBytes * 1000L / BytesPerSecond;

        logger.LogDebug(
            "Playback progress: PositionMs={PositionMs}, TotalBytes={TotalBytes}",
            posMs,
            totalBytes);
    }

    private void LogStart()
        => logger.LogInformation("Session start for {TrackId}", Track.Uri);

    private void LogSuccess()
        => logger.LogInformation("Session finished for {TrackId}", Track.Uri);

    private void LogCanceled()
        => logger.LogInformation("Session canceled for {TrackId}", Track.Uri);

    private void LogFatal(Exception ex)
        => logger.LogError(ex, "Unrecoverable error for {TrackId}", Track.Uri);

    private async Task HandleTransientErrorAsync(
        Exception ex,
        int attempt,
        CancellationToken ct)
    {
        logger.LogWarning(ex, "Transient error on attempt {Attempt}", attempt);

        var delay = TimeSpan.FromMilliseconds(RetryPauseMs * attempt);

        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (Exception ex2)
        {
            logger.LogWarning(ex2, "Failed to delay on attempt {Attempt} for a transient error.", attempt);
        }
    }

    #endregion

    public ValueTask DisposeAsync()
    {
        try
        {
            // Ensure no one is permanently blocked on pause when disposing
            _pauseGate.Set();
        }
        catch
        {
            // ignore
        }

        return ValueTask.CompletedTask;
    }
}
