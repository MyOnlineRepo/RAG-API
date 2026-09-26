using Microsoft.Extensions.AI;

namespace CloudKnowledge.Tests.Fakes;

/// <summary>Returns a scripted answer and records every request.</summary>
public sealed class FakeChatClient(string answer = "fake answer") : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming is not used by CloudKnowledge.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
