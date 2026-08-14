namespace RyzaEsPatcher.Core;

public sealed record PatchOptions(bool CreateBackup);

public sealed class PatchFailedException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Orquesta el parcheo completo: copia de seguridad, aplicación de los deltas con verificación
/// de hash, y vuelta atrás si algo sale mal.
/// </summary>
public sealed class PatchApplier(
    IPatchBundle bundle,
    IPatchEngine engine,
    BackupService backup,
    IFileHasher hasher)
{
    public void Install(
        InstallStatus status,
        PatchOptions options,
        IProgress<ProgressReport>? progress,
        CancellationToken ct = default)
    {
        if (!status.CanPatch)
            throw new PatchFailedException(status.Message);

        var pendientes = status.FilesToPatch;
        var gameFolder = status.GameFolder;

        // Reparto de la barra: 30 % copia de seguridad, 70 % parcheo.
        var pesoBackup = options.CreateBackup ? 0.30 : 0.0;

        if (options.CreateBackup)
        {
            backup.EnsureBackup(
                gameFolder, pendientes, bundle.Manifest.PatchVersion,
                new ScaledProgress(progress, 0.0, pesoBackup), ct);
        }

        var totalBytes = Math.Max(1, pendientes.Sum(e => e.PatchedSize));
        long hechos = 0;
        var yaParcheados = new List<PatchFileEntry>();

        foreach (var entry in pendientes)
        {
            ct.ThrowIfCancellationRequested();

            var baseFraccion = pesoBackup + (1 - pesoBackup) * (hechos / (double)totalBytes);
            var pesoFichero = (1 - pesoBackup) * (entry.PatchedSize / (double)totalBytes);

            try
            {
                ApplyOne(gameFolder, entry, progress, baseFraccion, pesoFichero, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw Rollback(gameFolder, yaParcheados, entry, options, ex);
            }

            yaParcheados.Add(entry);
            hechos += entry.PatchedSize;
        }

        progress?.Report(new ProgressReport("Parche aplicado.", 1.0));
    }

    public void Uninstall(
        string gameFolder,
        IProgress<ProgressReport>? progress,
        CancellationToken ct = default)
    {
        backup.RestoreAll(gameFolder, progress, ct);
    }

    private void ApplyOne(
        string gameFolder,
        PatchFileEntry entry,
        IProgress<ProgressReport>? progress,
        double baseFraccion,
        double pesoFichero,
        CancellationToken ct)
    {
        var target = Path.Combine(gameFolder, entry.RelativePath);
        var temp = AtomicFile.TempPathFor(target);
        var diffPath = Path.Combine(
            Path.GetTempPath(), "RyzaDX-ParcheES", Guid.NewGuid().ToString("N") + ".hdiff");

        Directory.CreateDirectory(Path.GetDirectoryName(diffPath)!);
        AtomicFile.TryDelete(temp);

        // hpatchz no informa de su avance, pero escribe a un temporal cuyo tamaño final
        // conocemos: sondeando ese tamaño se obtiene una barra de progreso real.
        using var vigilante = new TempFileProgressWatcher(
            temp, entry.PatchedSize,
            fraccion => progress?.Report(new ProgressReport(
                $"Parcheando {entry.FileName}…", baseFraccion + pesoFichero * fraccion)));

        try
        {
            using (var diffStream = bundle.OpenDiff(entry))
            using (var diffFile = File.Create(diffPath))
            {
                diffStream.CopyTo(diffFile);
            }

            using (var check = File.OpenRead(diffPath))
            {
                DiffVerification.EnsureMatches(check, entry.DiffSha256, entry.FileName);
            }

            engine.Apply(target, diffPath, temp, ct);

            var sha = hasher.ComputeSha256(temp, null, ct);
            if (!sha.Equals(entry.PatchedSha256, StringComparison.OrdinalIgnoreCase))
                throw new PatchFailedException(
                    $"El resultado de parchear '{entry.FileName}' no es el esperado. No he tocado el archivo original.");

            AtomicFile.Commit(temp, target);
        }
        finally
        {
            AtomicFile.TryDelete(temp);
            AtomicFile.TryDelete(diffPath);
        }
    }

    private PatchFailedException Rollback(
        string gameFolder,
        IReadOnlyList<PatchFileEntry> yaParcheados,
        PatchFileEntry fallido,
        PatchOptions options,
        Exception causa)
    {
        var cabecera = $"No se pudo parchear '{fallido.FileName}': {causa.Message}";
        var nl = Environment.NewLine;

        if (!options.CreateBackup)
        {
            var lista = yaParcheados.Count == 0
                ? "ninguno"
                : string.Join(", ", yaParcheados.Select(e => e.FileName));

            return new PatchFailedException(
                cabecera + nl + nl +
                "Como no había copia de seguridad, no puedo deshacer los cambios automáticamente." + nl +
                $"Archivos que quedaron parcheados: {lista}." + nl +
                "Puedes dejar el juego como estaba con \"Verificar la integridad de los archivos del juego\" en Steam.",
                causa);
        }

        try
        {
            backup.RestoreFiles(gameFolder, yaParcheados, CancellationToken.None);
            return new PatchFailedException(
                cabecera + nl + nl + "He dejado el juego como estaba usando la copia de seguridad.",
                causa);
        }
        catch (Exception restoreEx)
        {
            return new PatchFailedException(
                cabecera + nl + nl +
                "Además falló la restauración desde la copia de seguridad: " + restoreEx.Message + nl +
                $"Los originales siguen en la carpeta '{BackupService.FolderName}'; puedes copiarlos a mano.",
                causa);
        }
    }

    /// <summary>Reescala los informes de una sub-operación a un tramo de la barra global.</summary>
    private sealed class ScaledProgress(IProgress<ProgressReport>? inner, double offset, double weight)
        : IProgress<ProgressReport>
    {
        public void Report(ProgressReport value) =>
            inner?.Report(value with { Fraction = offset + weight * value.Fraction });
    }

    private sealed class TempFileProgressWatcher : IDisposable
    {
        private readonly Timer _timer;

        public TempFileProgressWatcher(string tempPath, long expectedSize, Action<double> report)
        {
            _timer = new Timer(
                _ =>
                {
                    try
                    {
                        if (!File.Exists(tempPath)) return;
                        var size = new FileInfo(tempPath).Length;
                        report(Math.Clamp(size / (double)Math.Max(1, expectedSize), 0.0, 1.0));
                    }
                    catch (IOException) { }
                },
                null,
                TimeSpan.FromMilliseconds(250),
                TimeSpan.FromMilliseconds(250));
        }

        public void Dispose() => _timer.Dispose();
    }
}
