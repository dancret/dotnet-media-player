using MediaPlayer.Input;
using MediaPlayer.Output;
using MediaPlayer.Playback;
using MediaPlayer.Tracks;
using Microsoft.Extensions.Logging;

namespace MediaPlayer.Cli;

public sealed class CliPlayer(
    ITrackResolver trackResolver,
    IAudioSource source,
    IAudioSink sink,
    ILogger<CliPlayer> logger)
    : PlayerBase(source, sink, logger)
{
    #region CLI-friendly convenience methods

    /// <summary>
    /// Enqueue one or more local files by path.
    /// </summary>
    public Task EnqueueFilesAsync(params string[] paths)
        => EnqueueFilesAsync((IEnumerable<string>)paths);

    /// <summary>
    /// Enqueue one or more local files by path.
    /// </summary>
    private async Task EnqueueFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var trackRequest = new TrackRequest(path);
            await foreach (var track in trackResolver.ResolveAsync(trackRequest))
            {
                await EnqueueAsync(track);
            }
        }
    }

    /// <summary>
    /// Immediately play a local file, interrupting the current session.
    /// </summary>
    public async Task PlayNowFileAsync(string path)
    {
        var track = await trackResolver.ResolveSingleOrDefaultAsync(new TrackRequest(path));
        if (track is null)
            return;
        await PlayNowAsync(track);
    }

    /// <summary>
    /// Set repeat mode via CLI-style string: off|one|all.
    /// </summary>
    public void SetRepeatModeFromString(string mode)
    {
        switch (mode.ToLowerInvariant())
        {
            case "off":
                RepeatMode = RepeatMode.None;
                break;
            case "one":
            case "track":
                RepeatMode = RepeatMode.One;
                break;
            case "all":
                RepeatMode = RepeatMode.All;
                break;
            default:
                throw new ArgumentException("Unknown repeat mode. Use: off | one | all", nameof(mode));
        }
    }

    /// <summary>
    /// Set shuffle mode via CLI-style string: on|off|toggle.
    /// </summary>
    public void SetShuffleFromString(string mode)
    {
        switch (mode.ToLowerInvariant())
        {
            case "on":
                Shuffle = true;
                break;
            case "off":
                Shuffle = false;
                break;
            case "toggle":
                Shuffle = !Shuffle;
                break;
            default:
                throw new ArgumentException("Unknown shuffle mode. Use: on | off | toggle", nameof(mode));
        }
    }

    /// <summary>
    /// Print a human-friendly status line to the console.
    /// </summary>
    public void PrintStatus()
    {
        Console.WriteLine($"State:   {State}");
        Console.WriteLine($"Repeat:  {RepeatMode}");
        Console.WriteLine($"Shuffle: {Shuffle}");

        var session = CurrentSession;
        if (session is null)
        {
            Console.WriteLine("Current: (none)");
            return;
        }

        var elapsed = DateTimeOffset.UtcNow - session.StartedAt;

        Console.WriteLine("Current session:");
        Console.WriteLine($"  Track:   {session.Track.Title} ({session.Track.Uri})");
        Console.WriteLine($"  Started: {session.StartedAt:HH:mm:ss}");
        Console.WriteLine($"  Elapsed: {elapsed:mm\\:ss}");
    }

    #endregion

    #region PlayerBase hooks

    protected override Task OnStartedAsync()
    {
        Console.WriteLine("CLI player started. Type 'help' for commands.");
        return Task.CompletedTask;
    }

    protected override void OnStateChanged(PlayerState newState)
    {
        // Called before public event in base.
        Console.WriteLine($"[state] {newState}");
    }

    protected override void OnTrackChanged(Track? newTrack)
    {
        if (newTrack is null)
        {
            Console.WriteLine("[track] none (idle)");
        }
        else
        {
            Console.WriteLine($"[track] {newTrack.Title} ({newTrack.Uri})");
        }
    }

    protected override void OnSessionEnded(Track track, PlaybackEndResult result)
    {
        Console.WriteLine($"[session] Ended for '{track.Title}' with reason: {result.Reason}");

        if (result.Error is not null)
        {
            Console.WriteLine($"[session] Error: {result.Error.Message}");
        }

        // This is also where a future retry policy for CLI could live.
        // For now, just log.
    }

    protected override void OnLoopFaulted(Exception ex)
    {
        base.OnLoopFaulted(ex);
        Console.WriteLine($"[error] Playback loop faulted: {ex.Message}");
    }

    #endregion
}
