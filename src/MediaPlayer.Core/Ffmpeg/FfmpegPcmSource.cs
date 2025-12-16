using System.Diagnostics;
using System.Globalization;
using MediaPlayer.Input;
using Microsoft.Extensions.Logging;

namespace MediaPlayer.Ffmpeg;

/// <summary>
/// Internal helper to start a ffmpeg process that decodes an input (file or URL)
/// to raw PCM written to stdout, wrapped as an <see cref="IAudioTrackReader"/>.
/// </summary>
internal static class FfmpegPcmSource
{
    public static IAudioTrackReader StartPcmReader(
        string inputSpecifier,
        FfmpegOptions? options,
        ILogger logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inputSpecifier))
            throw new ArgumentException("Input specifier must not be null or empty.", nameof(inputSpecifier));

        options ??= new FfmpegOptions();

        var psi = new ProcessStartInfo
        {
            FileName = options.FfmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true
        };

        if (options.HideBanner)
        {
            psi.ArgumentList.Add("-hide_banner");
        }

        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add(options.LogLevel);

        // input
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(inputSpecifier);

        // audio-only, raw PCM
        psi.ArgumentList.Add("-vn"); // no video
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(options.SampleFormat);
        psi.ArgumentList.Add("-ac");
        psi.ArgumentList.Add(options.Channels.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add(options.SampleRate.ToString(CultureInfo.InvariantCulture));

        // write to stdout
        psi.ArgumentList.Add("pipe:1");

        var process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Failed to start ffmpeg process using '{options.FfmpegPath}'.");
        }

        // drain stderr asynchronously to avoid deadlocks
        process.ErrorDataReceived += ProcessOnErrorDataReceived;

        void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            logger.LogTrace(e.Data);
        }

        process.BeginErrorReadLine();

        // Hook cancellation to kill ffmpeg
        if (ct.CanBeCanceled)
        {
            RegisterCancellation(process, logger, ct);
        }

        return new FfmpegTrackReader(logger, process);
    }

    /// <summary>
    /// Links a cancellation token to terminate the specified process when cancellation is requested.
    /// </summary>
    /// <param name="process">The <see cref="Process"/> to terminate.</param>
    /// <param name="logger">Logger instance to track errors.</param>
    /// <param name="ct">The <see cref="CancellationToken"/> to monitor for cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static void RegisterCancellation(Process process, ILogger logger, CancellationToken ct)
    {
        try
        {
            ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to kill ffmpeg process.");
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to register cancellation for ffmpeg process.");
        }
    }

    /// <summary>
    /// Wraps a ffmpeg process writing raw PCM to stdout.
    /// </summary>
    private sealed class FfmpegTrackReader(ILogger logger, Process process) : IAudioTrackReader
    {
        private readonly Process _process = process ?? throw new ArgumentNullException(nameof(process));
        private readonly Stream _stdout = process.StandardOutput.BaseStream;
        private bool _disposed;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FfmpegTrackReader));
            if (buffer.Length == 0) return 0;

            return await _stdout.ReadAsync(buffer, ct).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                await _stdout.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to dispose ffmpeg stdout.");
            }

            try
            {
                if (!_process.HasExited)
                {
                    // give ffmpeg a moment to exit gracefully
                    if (!_process.WaitForExit(2000))
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to kill ffmpeg process.");
            }
            finally
            {
                _process.Dispose();
            }
        }
    }
}
