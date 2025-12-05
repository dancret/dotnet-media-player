namespace MediaPlayer.Tracks;

/// <summary>
/// Contract for caching resolved tracks for specific keys, such as normalized URLs or IDs.
/// </summary>
/// <remarks>
/// The <see cref="ITrackRequestCache"/> interface abstracts caching of resolved
/// <see cref="Track"/> collections. It is intentionally simple so it can be backed by
/// in-memory caches, distributed caches, or other mechanisms.
/// The key format is resolver-specific. Each <see cref="ITrackResolver"/> that uses
/// caching is responsible for constructing the keys that make sense for its own domain.
/// </remarks>
public interface ITrackRequestCache
{
    /// <summary>
    /// Attempts to retrieve a cached collection of <see cref="Track"/> instances
    /// for the specified key.
    /// </summary>
    /// <param name="key">The cache key, typically constructed by a resolver.</param>
    /// <param name="ct">A token used to observe cancellation.</param>
    /// <returns>
    /// A value task containing the cached tracks if present, or <see langword="null" /> if
    /// the key is not found or has expired.
    /// </returns>
    ValueTask<IReadOnlyList<Track>?> TryGetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Stores a collection of <see cref="Track"/> instances under the specified key
    /// with a given time-to-live (TTL).
    /// </summary>
    /// <param name="key">The cache key, typically constructed by a resolver.</param>
    /// <param name="tracks">The tracks to cache.</param>
    /// <param name="ttl">The duration after which the entry should be considered expired.</param>
    /// <param name="ct">A token used to observe cancellation.</param>
    /// <returns>A value task representing the asynchronous cache operation.</returns>
    ValueTask SetAsync(string key, IReadOnlyList<Track> tracks, TimeSpan ttl, CancellationToken ct = default);
}