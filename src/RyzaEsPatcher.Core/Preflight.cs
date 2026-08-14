namespace RyzaEsPatcher.Core;

public sealed record PreflightResult(bool Ok, string Message)
{
    public static PreflightResult Success() => new(true, string.Empty);
}

/// <summary>Comprobaciones que se hacen antes de tocar un solo byte.</summary>
public static class Preflight
{
    /// <summary>Margen de seguridad para no dejar el disco a cero.</summary>
    public const long MarginBytes = 256L * 1024 * 1024;

    public static bool CanWriteTo(string folder)
    {
        if (!Directory.Exists(folder)) return false;

        var probe = Path.Combine(folder, ".ryzapatch-write-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }

    public static long GetFreeBytes(string folder)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))!).AvailableFreeSpace;
        }
        catch (ArgumentException) { return 0; }
        catch (IOException) { return 0; }
    }

    public static long RequiredBytes(IReadOnlyList<PatchFileEntry> filesToPatch, bool createBackup)
    {
        if (filesToPatch.Count == 0) return MarginBytes;

        var temporal = filesToPatch.Max(f => f.PatchedSize);
        var backup = createBackup ? filesToPatch.Sum(f => f.OriginalSize) : 0;
        return backup + temporal + MarginBytes;
    }

    public static PreflightResult Check(InstallStatus status, bool createBackup, long? freeBytesOverride = null)
    {
        if (!CanWriteTo(status.GameFolder))
            return new PreflightResult(false,
                "No tengo permiso para escribir en la carpeta del juego. Cierra el juego y Steam, " +
                "y vuelve a abrir este programa como administrador.");

        var required = RequiredBytes(status.FilesToPatch, createBackup);
        var free = freeBytesOverride ?? GetFreeBytes(status.GameFolder);

        if (free < required)
            return new PreflightResult(false,
                $"No hay espacio suficiente en el disco del juego: hacen falta unos {Mb(required)} MB " +
                $"y solo hay {Mb(free)} MB libres." +
                (createBackup
                    ? " Puedes desmarcar la copia de seguridad para necesitar menos espacio."
                    : string.Empty));

        return PreflightResult.Success();
    }

    private static long Mb(long bytes) => bytes / (1024 * 1024);
}
