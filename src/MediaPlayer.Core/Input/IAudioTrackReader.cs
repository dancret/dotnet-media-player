namespace MediaPlayer.Input;

/// <summary>
/// Defines an interface for reading audio track data in PCM format asynchronously.
/// </summary>
/// <remarks>
/// The <see cref="MediaPlayer.Input.IAudioTrackReader"/> interface provides functionality
/// to read PCM-encoded audio data into a buffer asynchronously. It is designed to be used
/// in conjunction with an <see cref="MediaPlayer.Input.IAudioSource"/> to facilitate
/// audio playback or processing scenarios. Implementations of this interface are expected
/// to handle resource management and support asynchronous disposal.
/// </remarks>
public interface IAudioTrackReader : IAsyncDisposable
{
    /// <summary>
    /// Reads PCM bytes into <paramref name="buffer"/> asynchronously.
    /// Returns the number of bytes read, or 0 on end-of-stream.
    /// </summary>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct);
}
