using MediaPlayer.Tracks;

namespace MediaPlayer.Playback;

/// <summary>
/// Represents a queue structure for managing playback tracks.
/// Provides methods for adding, removing, and inspecting tracks in the queue.
/// Tracks can be added to the end or the front of the queue and optionally shuffled during retrieval.
/// </summary>
public sealed class TrackQueue
{
    private readonly LinkedList<Track> _list = [];

    public int Count => _list.Count;

    /// <summary>
    /// Creates a snapshot of the current tracks in the playback queue.
    /// </summary>
    /// <returns>A read-only list containing the tracks in the queue at the time of the snapshot creation.</returns>
    public IReadOnlyList<Track> Snapshot() => _list.ToList();

    /// <summary>
    /// Adds a collection of tracks to the end of the playback queue.
    /// </summary>
    /// <param name="tracks">The collection of tracks to be added to the queue.</param>
    public void EnqueueRange(IEnumerable<Track> tracks)
    {
        foreach (var t in tracks) _list.AddLast(t);
    }

    /// <summary>
    /// Adds the specified track to the front of the playback queue. This ensures the track will be played next.
    /// </summary>
    /// <param name="track">The track to be added to the front of the queue.</param>
    public void EnqueueFront(Track track) => _list.AddFirst(track);

    private Track? DequeueNext()
    {
        if (_list.First is null) return null;
        var t = _list.First.Value;
        _list.RemoveFirst();
        return t;
    }

    /// <summary>
    /// Removes and returns the next track from the queue for playback. If shuffling is enabled,
    /// a random track is selected and removed from the queue; otherwise, the track at the front of the queue is returned.
    /// </summary>
    /// <param name="shuffle">Indicates whether the next track should be randomly selected from the queue.</param>
    /// <returns>The dequeued track if available; otherwise, null if the queue is empty.</returns>
    public Track? DequeueNext(bool shuffle)
    {
        if (_list.Count == 0) return null;
        if (!shuffle) return DequeueNext();

        // pick a random node
        var index = Random.Shared.Next(_list.Count);
        var node = _list.First!;
        for (int i = 0; i < index; i++)
        {
            node = node.Next!;
        }

        var track = node.Value;
        _list.Remove(node);
        return track;
    }

    public void Clear() => _list.Clear();

    /// <summary>
    /// Removes all tracks from the playback queue that have the specified unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the tracks to be removed.</param>
    public void RemoveDuplicatesById(string id)
    {
        var node = _list.First;
        while (node is not null)
        {
            var next = node.Next;
            if (node.Value.Uri == id) _list.Remove(node);
            node = next;
        }
    }
}