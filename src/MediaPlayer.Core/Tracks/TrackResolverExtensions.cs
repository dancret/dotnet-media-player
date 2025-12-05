namespace MediaPlayer.Tracks;

/// <summary>
/// Extension helpers for working with <see cref="ITrackResolver"/>.
/// </summary>
public static class TrackResolverExtensions
{
    /// <summary>
    /// Resolves a single <see cref="Track"/> for the given request, or <see langword="null" />
    /// if the resolver produces no tracks.
    /// </summary>
    /// <param name="resolver">The underlying resolver to delegate to.</param>
    /// <param name="request">The request to resolve.</param>
    /// <param name="ct">A token used to observe cancellation.</param>
    /// <returns>
    /// A task containing the first resolved track, or <see langword="null" /> if no tracks are produced.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="resolver"/> or <paramref name="request"/> is <see langword="null" />.
    /// </exception>
    public static async ValueTask<Track?> ResolveSingleOrDefaultAsync(
        this ITrackResolver resolver,
        TrackRequest request,
        CancellationToken ct = default)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        if (request is null) throw new ArgumentNullException(nameof(request));

        await foreach (var track in resolver.ResolveAsync(request, ct).ConfigureAwait(false))
        {
            return track;
        }

        return null;
    }
}