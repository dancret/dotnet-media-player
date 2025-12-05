using MediaPlayer.Input;
using MediaPlayer.Output;
using MediaPlayer.Tracks;
using Microsoft.Extensions.Logging;

namespace MediaPlayer.Playback;

/// <summary>
/// Represents the base class for implementing a media player. 
/// Provides core playback functionalities such as managing playback state, 
/// handling tracks, and interacting with playback commands.
/// </summary>
/// <remarks>
/// This abstract class serves as a foundation for creating custom media players. 
/// It manages playback operations, event handling, and interaction with the playback loop.
/// </remarks>
public abstract class PlayerBase
{
    #region Fields

    /// <summary>
    /// Represents the playback loop responsible for managing the playback queue,
    /// handling playback commands, and controlling the state of the media player.
    /// </summary>
    private readonly PlaybackLoop _loop;

    /// <summary>
    /// A <see cref="CancellationTokenSource"/> that defines the lifetime of the player instance,
    /// allowing controlled shutdown and cleanup of resources. It is used to manage the player’s
    /// asynchronous operations and ensure proper cancellation when the player is disposed or stopped.
    /// </summary>
    private readonly CancellationTokenSource _lifetimeCts = new();

    /// <summary>
    /// Represents the underlying asynchronous task managing the playback loop's lifecycle.
    /// This task is responsible for executing playback commands, maintaining playback continuity,
    /// and reacting to changes in the playback state or queue.
    /// </summary>
    private Task? _loopTask;

    /// <summary>
    /// Indicates whether the player has been started and is currently in an active or operational state.
    /// Used to ensure certain operations, such as initialization or playback, are only performed once.
    /// </summary>
    private bool _started;

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the state of the player changes, transitioning between
    /// states such as idle, playing, paused, or stopped. This event provides
    /// the updated state through its event arguments.
    /// </summary>
    public event EventHandler<PlayerState>? StateChanged;

    /// <summary>
    /// Occurs when the currently playing track in the media player changes.
    /// This event is triggered whenever a new track starts playing, the playback is stopped,
    /// or a track is skipped or updated.
    /// </summary>
    /// <remarks>
    /// Subscribers can use this event to respond to changes in the playback state
    /// and retrieve information about the new track, which may be <c>null</c> if no track is active.
    /// </remarks>
    public event EventHandler<Track?>? TrackChanged;

    /// <summary>
    /// Occurs when a playback session ends, providing the result of the session.
    /// </summary>
    /// <remarks>
    /// This event is triggered at the conclusion of a playback session, allowing
    /// subscribers to receive information about the outcome, such as success, error,
    /// or cancellation. It is primarily used to monitor the lifecycle of playback
    /// sessions and take appropriate actions based on the result.
    /// </remarks>
    public event EventHandler<PlaybackEndResult>? SessionEnded;

    /// <summary>
    /// Event triggered when the playback loop encounters an unhandled exception.
    /// </summary>
    /// <remarks>
    /// This event indicates that the playback loop has faulted, resulting in an error state.
    /// Subscribers can handle this event to respond to errors and perform recovery actions.
    /// </remarks>
    public event EventHandler<Exception>? LoopFaulted;

    #endregion

    #region Properties

    /// <summary>
    /// Logger instance for <see cref="PlayerBase"/>.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Represents the current state of the player, indicating whether it is idle, playing, paused, or stopped.
    /// This property is updated as the player's state transitions and triggers the <see cref="StateChanged"/> event when modified.
    /// </summary>
    public PlayerState State { get; private set; } = PlayerState.Idle;

    /// <summary>
    /// Gets or sets the repeat mode for playback. The value determines if playback will repeat the current track,
    /// all tracks in the queue, or not repeat at all.
    /// </summary>
    public RepeatMode RepeatMode
    {
        get => _loop.RepeatMode;
        set => _loop.RepeatMode = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether shuffle mode is enabled,
    /// allowing tracks in the playback queue to be played in a random order
    /// instead of the default sequential order.
    /// </summary>
    public bool Shuffle
    {
        get => _loop.Shuffle;
        set => _loop.Shuffle = value;
    }

    /// <summary>
    /// Provides information about the currently active playback session, including the current track,
    /// the player's state, and the session's start time. Returns null if no session is active.
    /// </summary>
    public CurrentSessionInfo? CurrentSession => _loop.CurrentSession;
    
    /// <summary>
    /// Gets a snapshot of the current playback queue.
    /// </summary>
    /// <remarks>
    /// This property provides a read-only view of the tracks currently enqueued for playback. 
    /// The snapshot reflects the state of the queue at the time it is accessed and does not 
    /// dynamically update as the queue changes.
    /// </remarks>
    /// <value>
    /// A read-only list of <see cref="Track"/> objects representing the current playback queue.
    /// </value>
    public IReadOnlyList<Track> QueueSnapshot => _loop.QueueSnapshot;

    #endregion

    #region C-tor

    protected PlayerBase(
        IAudioSource source,
        IAudioSink sink,
        ILogger logger,
        int queueCapacity = 256)
    {
        Logger = logger;

        _loop = new PlaybackLoop(source, sink, logger, queueCapacity);

        // Wire loop events into player-level events + hooks
        _loop.OnStateChanged += (_, state) =>
        {
            State = state;
            OnStateChanged(state);
            StateChanged?.Invoke(this, state);
        };

        _loop.OnTrackChanged += (_, track) =>
        {
            OnTrackChanged(track);
            TrackChanged?.Invoke(this, track);
        };

        _loop.OnSessionEnded += (_, e) =>
        {
            // hook for subclasses
            OnSessionEnded(e.Track, e.Result);

            // public event for consumers
            SessionEnded?.Invoke(this, e.Result);
        };
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts the playback loop asynchronously.
    /// </summary>
    /// <param name="ct">
    /// A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// This method initializes and starts the playback loop. If the player has already been started,
    /// later calls to this method will have no effect.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the provided <paramref name="ct"/>.
    /// </exception>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_started) return;
        _started = true;

        var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token, ct);
        _loopTask = RunLoopAsync(linked.Token);
        await OnStartedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the playback asynchronously.
    /// </summary>
    /// <param name="ct">
    /// A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous stop operation.
    /// </returns>
    /// <remarks>
    /// This method attempts to enqueue a stop command to the playback loop. 
    /// If an error occurs while sending the stop command, it is logged using the <see cref="Logger"/>.
    /// </remarks>
    public async Task StopAsync(CancellationToken ct = default)
    {
        try
        {
            await _loop.EnqueueCommandAsync(new StopCmd()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while sending Stop command");
        }
    }

    /// <summary>
    /// Adds a single track to the playback queue asynchronously.
    /// </summary>
    /// <param name="track">
    /// The <see cref="Track"/> to be added to the playback queue. 
    /// This represents a media item with properties such as URI, title, and input type.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. 
    /// The task completes when the track has been successfully enqueued.
    /// </returns>
    /// <remarks>
    /// This method enqueues a single track into the playback queue. 
    /// It internally calls the overload that accepts a collection of tracks.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="track"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the player is in an invalid state for enqueueing tracks.
    /// </exception>
    public Task EnqueueAsync(Track track)
        => EnqueueAsync([track]);

    /// <summary>
    /// Asynchronously adds a collection of tracks to the playback queue.
    /// </summary>
    /// <param name="tracks">The collection of <see cref="Track"/> objects to enqueue.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Tracks are processed before being enqueued, and additional actions may be performed after enqueuing.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tracks"/> is <c>null</c>.</exception>
    public async Task EnqueueAsync(IEnumerable<Track> tracks)
    {
        var prepared = await OnBeforeEnqueueAsync(tracks).ConfigureAwait(false);
        if (!prepared.Any()) return;

        await _loop.EnqueueCommandAsync(new EnqueueTracksCmd(prepared)).ConfigureAwait(false);
        await OnAfterEnqueueAsync(prepared).ConfigureAwait(false);
    }

    /// <summary>
    /// Immediately plays the specified track, interrupting the current playback if necessary.
    /// </summary>
    /// <param name="track">The <see cref="Track"/> to be played immediately.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method prepares the track for playback by invoking <see cref="OnBeforePlayNowAsync(Track)"/> 
    /// and performs any necessary post-playback actions using <see cref="OnAfterPlayNowAsync(Track)"/>.
    /// </remarks>
    public async Task PlayNowAsync(Track track)
    {
        var prepared = await OnBeforePlayNowAsync(track).ConfigureAwait(false);
        if (prepared is null) return;

        await _loop.EnqueueCommandAsync(new PlayNowCmd(prepared)).ConfigureAwait(false);
        await OnAfterPlayNowAsync(prepared).ConfigureAwait(false);
    }

    public Task SkipAsync()
        => _loop.EnqueueCommandAsync(new SkipCmd());

    public Task PauseAsync()
        => _loop.EnqueueCommandAsync(new PauseCmd());

    public Task ResumeAsync()
        => _loop.EnqueueCommandAsync(new ResumeCmd());

    public Task ClearAsync()
        => _loop.EnqueueCommandAsync(new ClearCmd());

    /// <summary>
    /// Releases the resources used by the player asynchronously.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// This method performs a graceful shutdown of the player, ensuring all playback operations
    /// are stopped and resources are properly disposed. If the playback loop is running, it will
    /// attempt to finalize it. Any unmanaged or managed resources utilized by the base class
    /// and derived implementations will also be disposed of.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown if shutting down the playback loop is canceled during the operation.
    /// </exception>
    public virtual async ValueTask DisposeAsync()
    {
        // First, try a soft stop (stop playback but keep loop alive if we weren't shutting down).
        await StopAsync().ConfigureAwait(false);

        // Now we are really shutting down: cancel the player lifetime and wait for the loop to end.
        // ReSharper disable once MethodHasAsyncOverload
        _lifetimeCts.Cancel();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected during shutdown
            }
            catch (Exception ex)
            {
                OnLoopFaulted(ex);
                LoopFaulted?.Invoke(this, ex);
            }
        }

        _lifetimeCts.Dispose();

        await _loop.DisposeAsync().ConfigureAwait(false);
    }

    #endregion

    #region Protected Hooks (extension points)

    protected virtual Task OnStartedAsync() => Task.CompletedTask;

    protected virtual void OnStateChanged(PlayerState newState) { }

    protected virtual void OnTrackChanged(Track? newTrack) { }

    protected virtual void OnSessionEnded(Track track, PlaybackEndResult result) { }

    protected virtual Task<IEnumerable<Track>> OnBeforeEnqueueAsync(IEnumerable<Track> tracks)
        => Task.FromResult(tracks);

    protected virtual Task OnAfterEnqueueAsync(IEnumerable<Track> tracks)
        => Task.CompletedTask;

    protected virtual Task<Track?> OnBeforePlayNowAsync(Track track)
        => Task.FromResult<Track?>(track);

    protected virtual Task OnAfterPlayNowAsync(Track track)
        => Task.CompletedTask;

    protected virtual void OnLoopFaulted(Exception ex)
    {
        Logger.LogError(ex, "Playback loop faulted");
    }

    protected virtual async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await _loop.RunAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            OnLoopFaulted(ex);
            LoopFaulted?.Invoke(this, ex);
        }
    }

    #endregion
}
