using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace MediaPlayer.Tracks;

/// <summary>
/// Routes <see cref="TrackRequest"/> instances to one of several inner
/// <see cref="ITrackResolver"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This resolver aggregates multiple concrete resolvers (e.g. YouTube, local file,
/// SoundCloud) and selects the first one whose <see cref="ITrackResolver.CanResolve"/>
/// method returns <see langword="true" />.
/// </para>
/// <para>
/// Once a matching resolver is found, all tracks are produced by that resolver.
/// If no resolver can handle the request, an <see cref="InvalidOperationException"/>
/// is thrown from <see cref="ResolveAsync"/>.
/// </para>
/// </remarks>
public sealed class RoutingTrackResolver : ITrackResolver
{
    private readonly IReadOnlyList<ITrackResolver> _innerResolvers;
    private readonly ILogger<RoutingTrackResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingTrackResolver"/> class.
    /// </summary>
    /// <param name="innerResolvers">
    /// The collection of inner resolvers to route to. The order of this collection matters:
    /// the first resolver that can handle a request will be used.
    /// </param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="innerResolvers"/> or <paramref name="logger"/> is <see langword="null" />.
    /// </exception>
    public RoutingTrackResolver(
        IEnumerable<ITrackResolver> innerResolvers,
        ILogger<RoutingTrackResolver> logger)
    {
        if (innerResolvers is null) throw new ArgumentNullException(nameof(innerResolvers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _innerResolvers = innerResolvers.ToArray();
    }

    /// <inheritdoc />
    public string Name => "RoutingTrackResolver";

    /// <inheritdoc />
    public bool CanResolve(TrackRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        foreach (var resolver in _innerResolvers)
        {
            if (resolver.CanResolve(request))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<Track> ResolveAsync(TrackRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        return ResolveInternalAsync(request, ct);
    }

    /// <summary>
    /// Internal async iterator that performs the routing logic and yields tracks from the chosen resolver.
    /// </summary>
    /// <param name="request">The request to resolve.</param>
    /// <param name="ct">A token used to observe cancellation.</param>
    /// <returns>An asynchronous sequence of tracks from the selected resolver.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no inner resolver can handle the specified request.
    /// </exception>
    private async IAsyncEnumerable<Track> ResolveInternalAsync(
        TrackRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var resolver in _innerResolvers)
        {
            if (!resolver.CanResolve(request))
                continue;

            _logger.LogDebug("Routing request '{Raw}' to resolver '{ResolverName}'.",
                request.Raw, resolver.Name);

            await foreach (var track in resolver.ResolveAsync(request, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                yield return track;
            }

            yield break;
        }

        _logger.LogWarning(
            "No track resolver could handle the request: '{Raw}'.",
            request.Raw);

        throw new InvalidOperationException(
            $"No track resolver could handle the request: '{request.Raw}'.");
    }
}