namespace CloudKnowledge.Application.Indexing;

public enum IndexStatus
{
    NotStarted,
    Indexing,
    Ready,
    Failed,
}

public sealed record IndexSnapshot(IndexStatus Status, int ChunkCount, DateTimeOffset? LastIndexedAt, string? Error);

/// <summary>
/// Current state of the knowledge index, shared by the indexing service and the readiness check.
/// Every transition replaces an immutable snapshot, so readers always see a consistent state.
/// </summary>
public sealed class IndexState(TimeProvider timeProvider)
{
    private IndexSnapshot _current = new(IndexStatus.NotStarted, 0, null, null);

    public IndexSnapshot Current => Volatile.Read(ref _current);

    /// <summary>Keeps the previous chunk count and timestamp so a re-index still shows what was indexed last.</summary>
    public void MarkIndexing() =>
        Update(previous => previous with { Status = IndexStatus.Indexing, Error = null });

    public void MarkReady(int chunkCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chunkCount);
        var now = timeProvider.GetUtcNow();
        Update(_ => new IndexSnapshot(IndexStatus.Ready, chunkCount, now, null));
    }

    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Update(previous => previous with { Status = IndexStatus.Failed, Error = error });
    }

    private void Update(Func<IndexSnapshot, IndexSnapshot> transition)
    {
        IndexSnapshot previous;
        do
        {
            previous = Current;
        }
        while (Interlocked.CompareExchange(ref _current, transition(previous), previous) != previous);
    }
}
