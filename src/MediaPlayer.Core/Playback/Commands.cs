using MediaPlayer.Tracks;

namespace MediaPlayer.Playback;

/// <summary>
/// Represents a base record for commands that can be issued to control the playback behavior
/// in the media player. Derived commands specify particular actions such as play, pause, 
/// stop, or enqueue tracks.
/// </summary>
public abstract record PlayerCommand;

/// <summary>
/// Represents a command to enqueue a collection of tracks into the playback queue.
/// </summary>
/// <param name="Tracks">The collection of tracks to be enqueued.</param>
public record EnqueueTracksCmd(IEnumerable<Track> Tracks) : PlayerCommand;

/// <summary>
/// Represents a command to pause the current playback.
/// </summary>
public record PauseCmd() : PlayerCommand;

/// <summary>
/// Represents a command to resume playback after it has been paused.
/// </summary>
public record ResumeCmd() : PlayerCommand;

/// <summary>
/// Represents a command to skip the currently playing track in the media player.
/// </summary>
public record SkipCmd() : PlayerCommand;

/// <summary>
/// Represents a command to immediately play a specific track, bypassing the current playback queue.
/// </summary>
/// <param name="Track">The track to be played immediately.</param>
public record PlayNowCmd(Track Track) : PlayerCommand;

/// <summary>
/// Represents a command to stop all current playback and clear the playback queue.
/// </summary>
public record StopCmd() : PlayerCommand;

/// <summary>
/// Represents a command to clear the entire playback queue, leaving the player in an idle state.
/// </summary>
public record ClearCmd() : PlayerCommand;

/// <summary>
/// Represents a command issued when a playback session has ended, providing the track that finished
/// and the result of the session.
/// </summary>
/// <param name="Track">The track that has finished playback.</param>
/// <param name="Result">The outcome of the playback session (e.g., completed, cancelled, failed).</param>
public record SessionEndedCmd(Track Track, PlaybackEndResult Result) : PlayerCommand;
