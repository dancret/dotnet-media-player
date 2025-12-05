namespace MediaPlayer.Tracks;

/// <summary>
/// Resolves <see cref="TrackRequest"/> instances into one or more <see cref="Track"/>s.
/// </summary>
/// <remarks>
/// Implementations are responsible for:
/// <list type="bullet">
///   <item>
///     <description>Determining whether they can handle a given request (via <see cref="CanResolve"/>)</description>
///   </item>
///   <item>
///     <description>Resolving the request into zero or more tracks (via <see cref="ResolveAsync"/>)</description>
///   </item>
/// </list>
/// Typical resolvers include YouTube, local file, SoundCloud, etc. A higher-level
/// <see cref="RoutingTrackResolver"/> can aggregate multiple resolvers.
/// </remarks>
public interface ITrackResolver
{
    /// <summary>
    /// Gets a human-readable name for this resolver (e.g. "YouTube" or "LocalFile").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines whether this resolver is capable of handling the specified request.
    /// </summary>
    /// <param name="request">The request to inspect.</param>
    /// <returns>
    /// <see langword="true" /> if this resolver can handle the request; otherwise, <see langword="false" />.
    /// </returns>
    /// <remarks>
    /// This method should be fast and must not perform network or long-running I/O operations.
    /// It is intended as a cheap pre-check, not as the actual resolution work.
    /// </remarks>
    bool CanResolve(TrackRequest request);

    /// <summary>
    /// Resolves the given request into zero or more <see cref="Track"/> instances.
    /// </summary>
    /// <param name="request">The request to resolve.</param>
    /// <param name="ct">A token used to observe cancellation.</param>
    /// <returns>
    /// An asynchronous sequence of resolved tracks. The sequence may be empty if
    /// no tracks could be resolved for the request.
    /// </returns>
    IAsyncEnumerable<Track> ResolveAsync(TrackRequest request, CancellationToken ct = default);
}