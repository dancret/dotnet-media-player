using System.Diagnostics;
using System.Globalization;

namespace MediaPlayer.Output;

/// <summary>
/// Audio sink that pipes raw PCM into a ffplay process for playback.
/// </summary>
public sealed class FfplaySink : IAudioSink
{
    private readonly string _ffplayPath;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly string _sampleFormat;
    private readonly object _sync = new();

    private Process? _process;
    private Stream? _stdin;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="FfplaySink"/> using ffplay on PATH,
    /// s16le stereo at 48 kHz.
    /// </summary>
    public FfplaySink(
        string ffplayPath = "ffplay",
        int sampleRate = 48_000,
        int channels = 2,
        string sampleFormat = "s16le")
    {
        _ffplayPath = ffplayPath ?? throw new ArgumentNullException(nameof(ffplayPath));
        _sampleRate = sampleRate;
        _channels = channels;
        _sampleFormat = sampleFormat ?? throw new ArgumentNullException(nameof(sampleFormat));
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FfplaySink));
        if (buffer.Length == 0) return;

        EnsureProcessStarted();

        var stdin = _stdin;
        if (stdin is null)
            throw new InvalidOperationException("ffplay process stdin is not available.");

        // Let the stream handle back-pressure naturally.
        await stdin.WriteAsync(buffer, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// For now, we don't need to do anything special per track; ffplay
    /// just keeps reading PCM continuously.
    /// </summary>
    public ValueTask CompleteAsync(CancellationToken ct)
    {
        // No per-track flushing or markers in the first version.
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Process? process;
        Stream? stdin;

        lock (_sync)
        {
            process = _process;
            stdin = _stdin;
            _process = null;
            _stdin = null;
        }

        if (stdin is not null)
        {
            try
            {
                await stdin.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            try
            {
                await stdin.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        if (process is not null)
        {
            try
            {
                // Closing stdin should let ffplay exit.
                try
                {
                    if (!process.HasExited)
                    {
                        process.StandardInput.Close();
                    }
                }
                catch
                {
                    // ignore
                }

                if (!process.HasExited)
                {
                    if (!process.WaitForExit(2000))
                    {
                        try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    }
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void EnsureProcessStarted()
    {
        if (_process is { HasExited: false } && _stdin is not null)
            return;

        lock (_sync)
        {
            if (_process is { HasExited: false } && _stdin is not null)
                return;

            // Clean up any old dead process
            try
            {
                _stdin?.Dispose();
            }
            catch
            {
                // ignore
            }

            try
            {
                _process?.Dispose();
            }
            catch
            {
                // ignore
            }

            _stdin = null;
            _process = null;

            var psi = new ProcessStartInfo
            {
                FileName = _ffplayPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            };

            // -nodisp: no video window
            // -autoexit: exit when stdin closes
            psi.ArgumentList.Add("-nodisp");
            psi.ArgumentList.Add("-autoexit");

            // PCM format flags
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(_sampleFormat);
            psi.ArgumentList.Add("-ac");
            psi.ArgumentList.Add(_channels.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add(_sampleRate.ToString(CultureInfo.InvariantCulture));

            // Read from stdin
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add("-");

            var process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException($"Failed to start ffplay process using '{_ffplayPath}'.");
            }

            // Drain stderr to avoid buffers filling up; plug into logging later if you like.
            process.ErrorDataReceived += static (_, _) => { /* ignore for now */ };
            process.BeginErrorReadLine();

            _process = process;
            _stdin = process.StandardInput.BaseStream;
        }
    }
}
