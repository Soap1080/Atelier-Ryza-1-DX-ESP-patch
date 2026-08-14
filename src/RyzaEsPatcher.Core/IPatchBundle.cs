using System.Reflection;
using System.Security.Cryptography;

namespace RyzaEsPatcher.Core;

/// <summary>Origen de los deltas y del manifest de un parche.</summary>
public interface IPatchBundle
{
    PatchManifest Manifest { get; }

    Stream OpenDiff(PatchFileEntry entry);
}

/// <summary>Parche embebido como recursos en un ensamblado (la compilación de release).</summary>
public sealed class EmbeddedPatchBundle : IPatchBundle
{
    public const string ManifestResourceName = "patch.manifest.json";

    private readonly Assembly _assembly;

    public PatchManifest Manifest { get; }

    private EmbeddedPatchBundle(Assembly assembly, PatchManifest manifest)
    {
        _assembly = assembly;
        Manifest = manifest;
    }

    /// <summary>Carga el parche embebido. Devuelve false con un motivo si la compilación no lo trae.</summary>
    public static bool TryLoad(Assembly assembly, out EmbeddedPatchBundle? bundle, out string? error)
    {
        bundle = null;
        error = null;

        using var stream = assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream is null)
        {
            error = "Esta compilación no incluye ningún parche (es una compilación de desarrollo). " +
                    "Genera los parches con la herramienta MakePatches y vuelve a compilar.";
            return false;
        }

        using var reader = new StreamReader(stream);
        try
        {
            var manifest = PatchManifest.Parse(reader.ReadToEnd());

            foreach (var entry in manifest.Files)
            {
                using var diff = assembly.GetManifestResourceStream(ResourceNameFor(entry));
                if (diff is null)
                {
                    error = $"Falta el parche embebido de '{entry.FileName}'.";
                    return false;
                }
            }

            bundle = new EmbeddedPatchBundle(assembly, manifest);
            return true;
        }
        catch (PatchManifestException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary><c>patches/PACK01.PAK.hdiff</c> se embebe con el nombre lógico <c>patch.PACK01.PAK.hdiff</c>.</summary>
    internal static string ResourceNameFor(PatchFileEntry entry) =>
        "patch." + Path.GetFileName(entry.DiffResource);

    public Stream OpenDiff(PatchFileEntry entry) =>
        _assembly.GetManifestResourceStream(ResourceNameFor(entry))
        ?? throw new PatchEngineException($"Falta el parche embebido de '{entry.FileName}'.");
}

/// <summary>Parche leído de una carpeta del disco. Se usa en los tests y al preparar una release.</summary>
public sealed class DirectoryPatchBundle(PatchManifest manifest, string folder) : IPatchBundle
{
    public PatchManifest Manifest { get; } = manifest;

    public Stream OpenDiff(PatchFileEntry entry)
    {
        var path = Path.Combine(folder, entry.DiffResource.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new PatchEngineException($"No encuentro el parche '{path}'.");

        return File.OpenRead(path);
    }
}

/// <summary>Comprobación del SHA-256 de un delta antes de aplicarlo.</summary>
public static class DiffVerification
{
    public static void EnsureMatches(Stream diff, string expectedSha256, string fileName)
    {
        var actual = Convert.ToHexString(SHA256.HashData(diff));
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new PatchEngineException($"El parche de '{fileName}' está dañado.");
    }
}
