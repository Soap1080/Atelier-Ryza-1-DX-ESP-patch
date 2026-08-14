namespace RyzaEsPatcher.Core;

/// <summary>Determina en qué estado está una instalación del juego respecto a un parche.</summary>
public sealed class InstallScanner(PatchManifest manifest, IFileHasher hasher)
{
    public InstallStatus Scan(
        string gameFolder,
        IProgress<ProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        var files = new List<FileStatus>(manifest.Files.Count);
        var totalBytes = manifest.Files.Sum(f => Math.Max(f.OriginalSize, f.PatchedSize));
        long doneBytes = 0;

        foreach (var entry in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();

            progress?.Report(new ProgressReport(
                $"Comprobando {entry.FileName}…",
                totalBytes == 0 ? 0 : (double)doneBytes / totalBytes));

            files.Add(new FileStatus(entry, DetermineState(gameFolder, entry, ct)));

            doneBytes += Math.Max(entry.OriginalSize, entry.PatchedSize);
        }

        progress?.Report(new ProgressReport("Comprobación terminada.", 1.0));
        return new InstallStatus(gameFolder, Aggregate(files), files);
    }

    private FileState DetermineState(string gameFolder, PatchFileEntry entry, CancellationToken ct)
    {
        var path = Path.Combine(gameFolder, entry.RelativePath);
        if (!File.Exists(path))
            return FileState.Missing;

        // Descarte instantáneo por tamaño: evita hashear cientos de MB en balde.
        var size = new FileInfo(path).Length;
        if (size != entry.OriginalSize && size != entry.PatchedSize)
            return FileState.Unknown;

        var sha = hasher.ComputeSha256(path, null, ct);

        if (size == entry.OriginalSize && sha.Equals(entry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            return FileState.Original;

        if (size == entry.PatchedSize && sha.Equals(entry.PatchedSha256, StringComparison.OrdinalIgnoreCase))
            return FileState.Patched;

        return FileState.Unknown;
    }

    private static InstallState Aggregate(IReadOnlyList<FileStatus> files)
    {
        // El orden importa: "falta un archivo" es más informativo que "no lo reconozco".
        if (files.Any(f => f.State == FileState.Missing)) return InstallState.MissingFiles;
        if (files.Any(f => f.State == FileState.Unknown)) return InstallState.Unrecognized;
        if (files.All(f => f.State == FileState.Patched)) return InstallState.AlreadyPatched;
        if (files.All(f => f.State == FileState.Original)) return InstallState.ReadyToPatch;
        return InstallState.PartiallyPatched;
    }
}
