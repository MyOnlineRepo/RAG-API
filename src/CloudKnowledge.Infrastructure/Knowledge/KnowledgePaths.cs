namespace CloudKnowledge.Infrastructure.Knowledge;

public static class KnowledgePaths
{
    /// <summary>
    /// Relative paths are resolved against <see cref="AppContext.BaseDirectory"/> (the build output, which contains
    /// <c>docs/</c>), so the result does not depend on the working directory. Absolute paths are returned unchanged.
    /// </summary>
    public static string Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}
