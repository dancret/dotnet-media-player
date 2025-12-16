using MediaPlayer.Tracks;

namespace MediaPlayer;

/// <summary>
/// Options for spawning ffmpeg to decode audio to raw PCM.
/// </summary>
public record FfmpegOptions
{
    /// <summary>
    /// Path or name of the ffmpeg executable. Defaults to "ffmpeg" (resolved via PATH).
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";
    /// <summary>
    /// PCM sample rate in Hz (default 48000).
    /// </summary>
    public int SampleRate { get; set; } = 48000;
    /// <summary>
    /// Number of channels (default 2).
    /// </summary>
    public int Channels { get; set; } = 2;
    /// <summary>
    /// Sample format string as used by ffmpeg (default "s16le").
    /// </summary>
    public string SampleFormat { get; set; } = "s16le";
    /// <summary>
    /// Whether to hide the ffmpeg banner. Default true.
    /// </summary>
    public bool HideBanner { get; set; } = true;
    /// <summary>
    /// Log level passed to ffmpeg (default "error").
    /// </summary>
    public string LogLevel { get; set; } = "error";
}

/// <summary>
/// Configuration options for using yt-dlp.
/// </summary>
public record YtDlpOptions
{
    /// <summary>
    /// Path or name of the yt-dlp executable. Defaults to "yt-dlp" (resolved via PATH).
    /// </summary>
    public string YtDlpPath { get; set; } = "yt-dlp";
    /// <summary>
    /// Enables usage of cookies.
    /// Will use a combination of <see cref="CookiesFromBrowser"/> and <see cref="CookiesFile"/>.
    /// </summary>
    public bool UseCookies { get; set; } = false;
    /// <summary>
    /// Sets the --cookies-from-browser option. 
    /// </summary>
    public string CookiesFromBrowser { get; set; } = "chrome";
    /// <summary>
    /// Sets the --cookies option.
    /// </summary>
    public string CookiesFile { get; set; } = "cookies.txt";
}

/// <summary>
/// Configuration options for <see cref="ITrackResolver"/>.
/// </summary>
/// <remarks>
/// <para>
/// This record allows customization of <see cref="ITrackResolver"/> behavior, including:
/// <list type="bullet">
/// <item><description>Setting a time-to-live (TTL) duration for cached results.</description></item>
/// </list>
/// </para>
/// </remarks>
public record TrackResolverOptions
{
    /// <summary>
    /// The option defines how long successfully resolved YouTube tracks should remain in the cache when caching is enabled.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.Zero;
}
