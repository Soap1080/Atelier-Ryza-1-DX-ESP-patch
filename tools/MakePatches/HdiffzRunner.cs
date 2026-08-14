using System.Diagnostics;
using System.Security.Cryptography;

namespace MakePatches;

/// <summary>Invoca el hdiffz de third_party para generar un delta.</summary>
public static class HdiffzRunner
{
    public const string ExpectedSha256 = "F5ED7AC622A2DAF4A31CC21FFA8EA1717F92323E79EF5AE695C5C9238A282F52";

    public static string LocateExecutable(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "third_party", "hdiffpatch", "win-x64", "hdiffz.exe");
        if (!File.Exists(path))
            throw new FileNotFoundException($"No encuentro hdiffz.exe en '{path}'.");

        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"El hdiffz.exe de third_party no coincide con el SHA-256 esperado (obtenido {actual}).");

        return path;
    }

    /// <summary>
    /// Modo stream (<c>-s-64</c>) para que la memoria no dependa del tamaño del archivo: hace
    /// falta con PACK00_04_01.PAK, de 715 MB. hdiffz verifica de serie que el delta reproduce
    /// el destino.
    /// </summary>
    public static void Create(string exePath, string oldFile, string newFile, string diffFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(diffFile)!);

        var psi = new ProcessStartInfo(exePath) { UseShellExecute = false };
        psi.ArgumentList.Add("-s-64");
        psi.ArgumentList.Add("-c-zstd-21-24");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(oldFile);
        psi.ArgumentList.Add(newFile);
        psi.ArgumentList.Add(diffFile);

        using var process = Process.Start(psi)!;
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"hdiffz falló con código {process.ExitCode}.");
    }
}

internal sealed record Argumentos(
    string Original,
    string Patched,
    string Out,
    string PatchVersion,
    string GameVersion)
{
    public static Argumentos? Parse(string[] args)
    {
        string? original = null, patched = null, salida = null;
        var patchVersion = "1.0.0";
        var gameVersion = "1.0.0.2";

        if (args.Length % 2 != 0) return null;

        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--original": original = args[i + 1]; break;
                case "--patched": patched = args[i + 1]; break;
                case "--out": salida = args[i + 1]; break;
                case "--patch-version": patchVersion = args[i + 1]; break;
                case "--game-version": gameVersion = args[i + 1]; break;
                default: return null;
            }
        }

        if (original is null || patched is null || salida is null) return null;
        return new Argumentos(original, patched, salida, patchVersion, gameVersion);
    }
}
