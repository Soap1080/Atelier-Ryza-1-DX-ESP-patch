using System.Security.Cryptography;

namespace RyzaEsPatcher.Core.Tests;

/// <summary>Instalación de juego falsa, con manifest coherente, para los tests.</summary>
public sealed class FakeGame : IDisposable
{
    public static readonly string[] RelativePaths =
    [
        "Atelier_Ryza_DX.exe",
        @"Data\PACK00_04_01.PAK",
        @"Data\PACK01.PAK",
        @"Data\PACK02.PAK",
    ];

    private readonly Dictionary<string, byte[]> _original = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _patched = new(StringComparer.OrdinalIgnoreCase);

    public string Root { get; }

    public string Folder { get; }

    public PatchManifest Manifest { get; }

    public FakeGame()
    {
        Root = Directory.CreateTempSubdirectory("ryzagame_").FullName;
        Folder = Path.Combine(Root, "game");
        Directory.CreateDirectory(Folder);

        var entries = new List<PatchFileEntry>();
        var rnd = new Random(1234);

        foreach (var relativePath in RelativePaths)
        {
            var original = new byte[64 * 1024];
            rnd.NextBytes(original);

            // El "parcheado" inserta 4 KB en el medio: cambia el tamaño y desplaza el resto,
            // igual que ocurre de verdad al reempaquetar un .PAK.
            var inserted = new byte[4 * 1024];
            rnd.NextBytes(inserted);
            var patched = new byte[original.Length + inserted.Length];
            original.AsSpan(0, 32 * 1024).CopyTo(patched);
            inserted.CopyTo(patched, 32 * 1024);
            original.AsSpan(32 * 1024).CopyTo(patched.AsSpan(32 * 1024 + inserted.Length));

            _original[relativePath] = original;
            _patched[relativePath] = patched;

            entries.Add(new PatchFileEntry(
                relativePath,
                original.Length, Convert.ToHexString(SHA256.HashData(original)),
                patched.Length, Convert.ToHexString(SHA256.HashData(patched)),
                "patches/" + Path.GetFileName(relativePath) + ".hdiff",
                Convert.ToHexString(SHA256.HashData(inserted))));
        }

        Manifest = new PatchManifest(1, "Parche de prueba", "1.0.0", "1.0.0.2", entries);
    }

    public byte[] OriginalBytes(string relativePath) => _original[relativePath];

    public byte[] PatchedBytes(string relativePath) => _patched[relativePath];

    public string FullPath(string relativePath) => Path.Combine(Folder, relativePath);

    public void WriteOriginal(string relativePath) => Write(relativePath, _original[relativePath]);

    public void WritePatched(string relativePath) => Write(relativePath, _patched[relativePath]);

    /// <summary>Mismo tamaño que el original pero contenido distinto: obliga a hashear para descartarlo.</summary>
    public void WriteGarbage(string relativePath)
    {
        var bytes = (byte[])_original[relativePath].Clone();
        bytes[0] ^= 0xFF;
        Write(relativePath, bytes);
    }

    public void WriteWrongSize(string relativePath) => Write(relativePath, [1, 2, 3]);

    public void WriteAllOriginal()
    {
        foreach (var p in RelativePaths) WriteOriginal(p);
    }

    public void WriteAllPatched()
    {
        foreach (var p in RelativePaths) WritePatched(p);
    }

    public void Delete(string relativePath) => File.Delete(FullPath(relativePath));

    private void Write(string relativePath, byte[] bytes)
    {
        var full = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
