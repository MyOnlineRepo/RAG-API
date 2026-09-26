using CloudKnowledge.Application.Indexing;
using CloudKnowledge.Tests.Fakes;

namespace CloudKnowledge.Tests.Application;

public sealed class IndexStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly IndexState _state = new(new FixedTimeProvider(Now));

    [Fact]
    public void Starts_as_not_started()
    {
        Assert.Equal(new IndexSnapshot(IndexStatus.NotStarted, 0, null, null), _state.Current);
    }

    [Fact]
    public void MarkReady_records_chunk_count_and_time()
    {
        _state.MarkIndexing();
        _state.MarkReady(42);

        Assert.Equal(new IndexSnapshot(IndexStatus.Ready, 42, Now, null), _state.Current);
    }

    [Fact]
    public void MarkFailed_records_error_and_keeps_last_successful_index()
    {
        _state.MarkReady(42);

        _state.MarkIndexing();
        _state.MarkFailed("boom");

        Assert.Equal(new IndexSnapshot(IndexStatus.Failed, 42, Now, "boom"), _state.Current);
    }

    [Fact]
    public void MarkIndexing_clears_previous_error()
    {
        _state.MarkFailed("boom");

        _state.MarkIndexing();

        Assert.Equal(IndexStatus.Indexing, _state.Current.Status);
        Assert.Null(_state.Current.Error);
    }

    [Fact]
    public void Rejects_invalid_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _state.MarkReady(-1));
        Assert.Throws<ArgumentException>(() => _state.MarkFailed(" "));
    }
}
