using System.Text.Json;
using System.Text.Json.Serialization;

namespace RyzaEsPatcher.Core;

/// <summary>Error al leer o validar un manifest de parche.</summary>
public sealed class PatchManifestException : Exception
{
    public PatchManifestException(string message) : base(message) { }
    public PatchManifestException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Un fichero del juego que el parche modifica.</summary>
public sealed record PatchFileEntry(
    string RelativePath,
    long OriginalSize,
    string OriginalSha256,
    long PatchedSize,
    string PatchedSha256,
    string DiffResource,
    string DiffSha256)
{
    /// <summary>Nombre del fichero sin carpetas, para mostrar en la interfaz.</summary>
    [JsonIgnore]
    public string FileName => Path.GetFileName(RelativePath);
}

/// <summary>Descripción completa de un parche: qué ficheros toca y con qué deltas.</summary>
public sealed record PatchManifest(
    int SchemaVersion,
    string PatchName,
    string PatchVersion,
    string GameVersion,
    IReadOnlyList<PatchFileEntry> Files)
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static PatchManifest Parse(string json)
    {
        PatchManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PatchManifest>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new PatchManifestException("El manifest del parche no es JSON válido.", ex);
        }

        if (manifest is null)
            throw new PatchManifestException("El manifest del parche está vacío.");

        manifest.Validate();
        return manifest;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    private void Validate()
    {
        if (SchemaVersion != SupportedSchemaVersion)
            throw new PatchManifestException(
                $"Versión de manifest no soportada: {SchemaVersion} (se esperaba {SupportedSchemaVersion}).");

        if (Files is null || Files.Count == 0)
            throw new PatchManifestException("El manifest no contiene ningún fichero.");

        foreach (var file in Files)
        {
            if (string.IsNullOrWhiteSpace(file.RelativePath))
                throw new PatchManifestException("Hay una entrada sin 'relativePath'.");

            if (Path.IsPathRooted(file.RelativePath) || file.RelativePath.Contains(".."))
                throw new PatchManifestException($"Ruta relativa no válida: '{file.RelativePath}'.");

            if (string.IsNullOrWhiteSpace(file.DiffResource))
                throw new PatchManifestException($"'{file.RelativePath}' no indica 'diffResource'.");

            if (file.OriginalSize <= 0 || file.PatchedSize <= 0)
                throw new PatchManifestException($"Tamaños no válidos en '{file.RelativePath}'.");

            RequireSha256(file.OriginalSha256, file.RelativePath, "originalSha256");
            RequireSha256(file.PatchedSha256, file.RelativePath, "patchedSha256");
            RequireSha256(file.DiffSha256, file.RelativePath, "diffSha256");
        }
    }

    private static void RequireSha256(string value, string relativePath, string field)
    {
        if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit))
            throw new PatchManifestException(
                $"'{field}' de '{relativePath}' no es un SHA-256 hexadecimal de 64 caracteres.");
    }
}
