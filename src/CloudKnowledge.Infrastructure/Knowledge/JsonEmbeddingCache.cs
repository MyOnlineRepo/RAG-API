using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudKnowledge.Application.Configuration;
using CloudKnowledge.Application.Indexing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudKnowledge.Infrastructure.Knowledge;

/// <summary>
/// Embedding cache in a JSON file (<see cref="KnowledgeOptions.EmbeddingCachePath"/>).
/// Key = SHA-256 of "{EmbeddingDeployment}\n{text}", so a different deployment never reuses old vectors.
/// The file is loaded on first use and written atomically (temp file, then move).
/// </summary>
public sealed class JsonEmbeddingCache(
    IOptions<KnowledgeOptions> knowledgeOptions,
    IOptions<AzureOpenAIOptions> azureOptions,
    ILogger<JsonEmbeddingCache> logger) : IEmbeddingCache
{
    private readonly string _path = KnowledgePaths.Resolve(knowledgeOptions.Value.EmbeddingCachePath);
    private readonly string _deployment = azureOptions.Value.EmbeddingDeployment;
    private readonly Lock _lock = new();
    private Dictionary<string, float[]>? _entries;

    public bool TryGet(string text, out ReadOnlyMemory<float> vector)
    {
        lock (_lock)
        {
            var found = Entries.TryGetValue(Key(text), out var values);
            vector = values;
            return found;
        }
    }

    public void Set(string text, ReadOnlyMemory<float> vector)
    {
        lock (_lock)
        {
            Entries[Key(text)] = vector.ToArray();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        string json;
        lock (_lock)
        {
            json = JsonSerializer.Serialize(Entries);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tempPath = _path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _path, overwrite: true);
    }

    private Dictionary<string, float[]> Entries => _entries ??= Load();

    private Dictionary<string, float[]> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(_path)) ?? [];
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Embedding cache {Path} is unreadable and is ignored; embeddings will be recomputed", _path);
            return [];
        }
    }

    private string Key(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{_deployment}\n{text}")));
}
