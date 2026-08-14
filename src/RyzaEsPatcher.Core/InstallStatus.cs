namespace RyzaEsPatcher.Core;

/// <summary>Estado de un fichero concreto del juego frente al parche.</summary>
public enum FileState
{
    /// <summary>No existe en la carpeta del juego.</summary>
    Missing,

    /// <summary>Coincide con el original sin parchear.</summary>
    Original,

    /// <summary>Coincide con el resultado del parche.</summary>
    Patched,

    /// <summary>Existe pero no es ni uno ni otro.</summary>
    Unknown,
}

/// <summary>Estado global de la instalación.</summary>
public enum InstallState
{
    MissingFiles,
    Unrecognized,
    AlreadyPatched,
    ReadyToPatch,
    PartiallyPatched,
}

public sealed record FileStatus(PatchFileEntry Entry, FileState State);

public sealed record InstallStatus(
    string GameFolder,
    InstallState State,
    IReadOnlyList<FileStatus> Files)
{
    public bool CanPatch => State is InstallState.ReadyToPatch or InstallState.PartiallyPatched;

    /// <summary>Solo los ficheros que hay que parchear: los que están intactos.</summary>
    public IReadOnlyList<PatchFileEntry> FilesToPatch =>
        Files.Where(f => f.State == FileState.Original).Select(f => f.Entry).ToList();

    public string Message => State switch
    {
        InstallState.MissingFiles =>
            "No parece la carpeta del juego. No encuentro: " +
            string.Join(", ", Files.Where(f => f.State == FileState.Missing).Select(f => f.Entry.FileName)) + ".",

        InstallState.Unrecognized =>
            "Hay archivos que no reconozco (" +
            string.Join(", ", Files.Where(f => f.State == FileState.Unknown).Select(f => f.Entry.FileName)) +
            "). Este parche es para la versión 1.0.0.2 del juego; puede que tengas otra versión o algún otro mod instalado.",

        InstallState.AlreadyPatched =>
            "El juego ya está parcheado al español.",

        InstallState.ReadyToPatch =>
            "Listo para parchear.",

        InstallState.PartiallyPatched =>
            $"El juego está parcheado a medias ({Files.Count(f => f.State == FileState.Patched)} de {Files.Count} archivos). Puedo completar el parcheo.",

        _ => string.Empty,
    };
}
