using MediaPlayer.Tracks;

namespace MediaPlayer.Playback;

/// <summary>
/// Represents information about the current playback session, including the track being played,
/// the state of the player, and the timestamp when the session started.
/// </summary>
/// <param name="Track">The media track currently being played.</param>
/// <param name="State">The current state of the playback session.</param>
/// <param name="StartedAt">The timestamp indicating when the playback session started.</param>
public sealed record CurrentSessionInfo(
    Track Track,
    PlayerState State,
    DateTimeOffset StartedAt
);

/// <summary>
/// Provides data for the event triggered when a playback session ends.
/// </summary>
public sealed class SessionEndedEventArgs(Track track, PlaybackEndResult result) : EventArgs
{
    public Track Track { get; } = track ?? throw new ArgumentNullException(nameof(track));
    public PlaybackEndResult Result { get; } = result;
}

/// <summary>
/// Describes the outcome of a playback session, including the termination reason, optional details, and any error.
/// </summary>
/// <param name="Reason">The reason the session ended.</param>
/// <param name="Details">Optional additional information about the termination.</param>
/// <param name="Error">An optional exception if an error occurred.</param>
public readonly record struct PlaybackEndResult(PlaybackEndReason Reason, string? Details = null, Exception? Error = null);