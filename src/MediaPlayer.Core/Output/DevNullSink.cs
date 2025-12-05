namespace MediaPlayer.Output;

/// <summary>
/// Sink for testing purposes.
/// </summary>
public sealed class DevNullSink : IAudioSink
{
    public int Bytes { get; private set; }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        Bytes += buffer.Length;
        
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CompleteAsync(CancellationToken ct) => ValueTask.CompletedTask;
}