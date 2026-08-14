using System.Text.Json;

namespace RyzaEsPatcher.Core;

public sealed class BackupException(string message) : Exception(message);

public sealed record BackupFileEntry(string RelativePath, long Size, string Sha256);

public sealed record BackupManifest(
    int SchemaVersion,
    DateTimeOffset CreatedUtc,
    string PatcherVersion,
    string GameVersion,
    IReadOnlyList<BackupFileEntry> Files)
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static BackupManifest? TryParse(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<BackupManifest>(json, Options);
            return parsed is { SchemaVersion: SupportedSchemaVersion, Files.Count: > 0 } ? parsed : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}

public enum BackupValidity { NotFound, Invalid, Incomplete, Valid }

public sealed record BackupStatus(BackupValidity Validity, string Message)
{
    public bool CanRestore => Validity == BackupValidity.Valid;
}

/// <summary>Gestiona la carpeta <c>backup</c> dentro de la instalación del juego.</summary>
public sealed class BackupService(PatchManifest manifest, IFileHasher hasher)
{
    public const string FolderName = "backup";
    public const string ManifestName = "backup.json";

    private static string FolderFor(string gameFolder) => Path.Combine(gameFolder, FolderName);

    private static string ManifestPathFor(string gameFolder) => Path.Combine(FolderFor(gameFolder), ManifestName);

    private static string BackupPathFor(string gameFolder, string relativePath) =>
        Path.Combine(FolderFor(gameFolder), relativePath);

    public BackupStatus Inspect(string gameFolder, CancellationToken ct = default)
    {
        var manifestPath = ManifestPathFor(gameFolder);
        if (!File.Exists(manifestPath))
            return new BackupStatus(BackupValidity.NotFound,
                "No hay copia de seguridad en esta carpeta, así que no puedo quitar el parche.");

        if (BackupManifest.TryParse(File.ReadAllText(manifestPath)) is null)
            return new BackupStatus(BackupValidity.Invalid,
                "La copia de seguridad tiene un backup.json ilegible. No puedo usarla.");

        var faltan = new List<string>();
        var corruptos = new List<string>();

        foreach (var entry in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            var path = BackupPathFor(gameFolder, entry.RelativePath);

            if (!File.Exists(path))
            {
                faltan.Add(entry.FileName);
                continue;
            }

            if (new FileInfo(path).Length != entry.OriginalSize ||
                !hasher.ComputeSha256(path, null, ct).Equals(entry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            {
                corruptos.Add(entry.FileName);
            }
        }

        if (corruptos.Count > 0)
            return new BackupStatus(BackupValidity.Invalid,
                "La copia de seguridad está dañada (" + string.Join(", ", corruptos) + "). No puedo usarla.");

        if (faltan.Count > 0)
            return new BackupStatus(BackupValidity.Incomplete,
                "La copia de seguridad está incompleta, faltan: " + string.Join(", ", faltan) + ".");

        return new BackupStatus(BackupValidity.Valid, "Hay una copia de seguridad completa.");
    }

    /// <summary>
    /// Guarda en <c>backup\</c> los archivos indicados. Nunca sobrescribe una entrada de backup
    /// que ya sea un original válido, para no destruirla si se re-parchea a medias.
    /// </summary>
    public void EnsureBackup(
        string gameFolder,
        IReadOnlyList<PatchFileEntry> entries,
        string patcherVersion,
        IProgress<ProgressReport>? progress,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(FolderFor(gameFolder));

        var pendientes = entries.Where(e => !YaGuardado(gameFolder, e, ct)).ToList();
        var totalBytes = Math.Max(1, pendientes.Sum(e => e.OriginalSize));
        long hechos = 0;

        foreach (var entry in pendientes)
        {
            ct.ThrowIfCancellationRequested();
            var baseBytes = hechos;

            AtomicFile.CopyVerified(
                Path.Combine(gameFolder, entry.RelativePath),
                BackupPathFor(gameFolder, entry.RelativePath),
                entry.OriginalSha256,
                hasher,
                new ProgressAdapter(v => progress?.Report(new ProgressReport(
                    $"Copiando {entry.FileName} a la copia de seguridad…",
                    Math.Min(1.0, (baseBytes + v) / (double)totalBytes)))),
                ct);

            hechos += entry.OriginalSize;
        }

        WriteManifest(gameFolder, patcherVersion, ct);
        progress?.Report(new ProgressReport("Copia de seguridad terminada.", 1.0));
    }

    public void RestoreAll(
        string gameFolder,
        IProgress<ProgressReport>? progress,
        CancellationToken ct = default)
    {
        var status = Inspect(gameFolder, ct);
        if (!status.CanRestore)
            throw new BackupException(status.Message);

        var totalBytes = Math.Max(1, manifest.Files.Sum(e => e.OriginalSize));
        long hechos = 0;

        foreach (var entry in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            var baseBytes = hechos;

            AtomicFile.CopyVerified(
                BackupPathFor(gameFolder, entry.RelativePath),
                Path.Combine(gameFolder, entry.RelativePath),
                entry.OriginalSha256,
                hasher,
                new ProgressAdapter(v => progress?.Report(new ProgressReport(
                    $"Restaurando {entry.FileName}…",
                    Math.Min(1.0, (baseBytes + v) / (double)totalBytes)))),
                ct);

            hechos += entry.OriginalSize;
        }

        progress?.Report(new ProgressReport("Restauración terminada.", 1.0));
    }

    /// <summary>Restaura solo los archivos indicados. Se usa para deshacer un parcheo fallido.</summary>
    public void RestoreFiles(string gameFolder, IEnumerable<PatchFileEntry> entries, CancellationToken ct = default)
    {
        foreach (var entry in entries)
        {
            var origen = BackupPathFor(gameFolder, entry.RelativePath);
            if (!File.Exists(origen)) continue;

            AtomicFile.CopyVerified(
                origen,
                Path.Combine(gameFolder, entry.RelativePath),
                entry.OriginalSha256,
                hasher,
                null,
                ct);
        }
    }

    private bool YaGuardado(string gameFolder, PatchFileEntry entry, CancellationToken ct)
    {
        var path = BackupPathFor(gameFolder, entry.RelativePath);
        return File.Exists(path)
            && new FileInfo(path).Length == entry.OriginalSize
            && hasher.ComputeSha256(path, null, ct).Equals(entry.OriginalSha256, StringComparison.OrdinalIgnoreCase);
    }

    private void WriteManifest(string gameFolder, string patcherVersion, CancellationToken ct)
    {
        var files = manifest.Files
            .Where(e => File.Exists(BackupPathFor(gameFolder, e.RelativePath)))
            .Select(e => new BackupFileEntry(e.RelativePath, e.OriginalSize, e.OriginalSha256))
            .ToList();

        var backupManifest = new BackupManifest(
            BackupManifest.SupportedSchemaVersion,
            DateTimeOffset.UtcNow,
            patcherVersion,
            manifest.GameVersion,
            files);

        ct.ThrowIfCancellationRequested();
        File.WriteAllText(ManifestPathFor(gameFolder), backupManifest.ToJson());
    }

    private sealed class ProgressAdapter(Action<long> handler) : IProgress<long>
    {
        public void Report(long value) => handler(value);
    }
}
