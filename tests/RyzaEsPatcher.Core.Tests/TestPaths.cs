namespace RyzaEsPatcher.Core.Tests;

/// <summary>Localiza recursos del repositorio desde el directorio de salida de los tests.</summary>
public static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string HdiffzExe => Path.Combine(
        RepoRoot, "third_party", "hdiffpatch", "win-x64", "hdiffz.exe");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RyzaEsPatcher.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "No se encontró RyzaEsPatcher.sln subiendo desde " + AppContext.BaseDirectory);
    }
}

/// <summary>IProgress síncrono: <see cref="Progress{T}"/> usa el SynchronizationContext y no sirve en tests.</summary>
public sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
