using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace MediaPlayer.Tracks;

/// <summary>
/// Resolves <see cref="Track"/> instances from local file paths.
/// </summary>
/// <remarks>
/// This resolver treats the <see cref="TrackRequest.Raw"/> value as a file system path.
/// It can optionally be hinted via <see cref="TrackRequest.InputHint"/> = <see cref="TrackInput.LocalFile"/>.
/// </remarks>
public sealed class LocalFileTrackResolver : ITrackResolver
{
    private readonly ILogger<LocalFileTrackResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileTrackResolver"/> class.
    /// </summary>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="logger"/> is <see langword="null" />.
    /// </exception>
    public LocalFileTrackResolver(ILogger<LocalFileTrackResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "LocalFile";

    /// <inheritdoc />
    public bool CanResolve(TrackRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (request.InputHint is TrackInput.LocalFile)
            return true;

        if (string.IsNullOrWhiteSpace(request.Raw))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(request.Raw);
            return File.Exists(fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _logger.LogDebug(ex, "LocalFileTrackResolver could not interpret '{Raw}' as a valid file path.", request.Raw);
            return false;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Track> ResolveAsync(
        TrackRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        ct.ThrowIfCancellationRequested();

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(request.Raw);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _logger.LogWarning(ex,
                "LocalFileTrackResolver failed to get full path for '{Raw}'.", request.Raw);
            yield break;
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning(
                "LocalFileTrackResolver could not find file '{Path}'.",
                fullPath);
            yield break;
        }

        var fileName = Path.GetFileNameWithoutExtension(fullPath);

        var track = new Track(
            Uri: fullPath,
            Title: fileName,
            Input: TrackInput.LocalFile,
            DurationHint: null);

        _logger.LogDebug(
            "LocalFileTrackResolver resolved file '{Path}' as track '{Title}'.",
            fullPath, fileName);

        yield return track;

        await Task.CompletedTask;
    }
}
