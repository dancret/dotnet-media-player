namespace MediaPlayer.Output;

/// <summary>
/// Defines an abstraction for an audio sink that consumes PCM audio data.
/// Implementations of this interface are responsible for handling audio output,
/// such as writing audio data to a device or processing it in memory.
/// </summary>
/// <remarks>
/// The <see cref="IAudioSink"/> interface is designed to work in conjunction with
/// audio playback systems, providing methods to write audio data and finalize
/// playback for individual tracks. It supports asynchronous operations to handle
/// back-pressure and ensure smooth data flow.
/// </remarks>
public interface IAudioSink : IAsyncDisposable
{
    /// <summary>
    /// Writes a PCM chunk to the underlying audio output.
    /// This may block for back-pressure.
    /// </summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct);

    /// <summary>
    /// Called once per track when the reader reaches end-of-stream.
    /// Allows the sink to flush or finalize any per-track state.
    /// </summary>
    ValueTask CompleteAsync(CancellationToken ct);
}
