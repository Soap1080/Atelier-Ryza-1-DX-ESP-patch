using System.Diagnostics;

namespace RyzaEsPatcher.Core.Tests;

public class MakePatchesIntegrationTests : IDisposable
{
    private readonly FakeGame _game = new();

    public void Dispose() => _game.Dispose();

    [Fact]
    public void El_manifest_y_los_deltas_generados_reproducen_los_ficheros_parcheados()
    {
        var originales = Path.Combine(_game.Root, "orig");
        var parcheados = Path.Combine(_game.Root, "patched");
        var salida = Path.Combine(_game.Root, "out");

        foreach (var relativePath in FakeGame.RelativePaths)
        {
            EscribirEn(originales, relativePath, _game.OriginalBytes(relativePath));
            EscribirEn(parcheados, relativePath, _game.PatchedBytes(relativePath));
        }

        EjecutarMakePatches(originales, parcheados, salida);

        var manifest = PatchManifest.Parse(File.ReadAllText(Path.Combine(salida, "manifest.json")));
        Assert.Equal(4, manifest.Files.Count);
        Assert.Equal("1.0.0.2", manifest.GameVersion);

        // Aplicar lo generado sobre los originales debe dar exactamente los parcheados.
        _game.WriteAllOriginal();
        var hasher = new FileHasher();
        using var engine = new HPatchEngine();
        var applier = new PatchApplier(
            new DirectoryPatchBundle(manifest, salida),
            engine,
            new BackupService(manifest, hasher),
            hasher);
        var status = new InstallScanner(manifest, hasher).Scan(_game.Folder, null, CancellationToken.None);

        Assert.Equal(InstallState.ReadyToPatch, status.State);
        applier.Install(status, new PatchOptions(CreateBackup: false), null, CancellationToken.None);

        foreach (var relativePath in FakeGame.RelativePaths)
            Assert.Equal(_game.PatchedBytes(relativePath), File.ReadAllBytes(_game.FullPath(relativePath)));
    }

    private static void EscribirEn(string root, string relativePath, byte[] bytes)
    {
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }

    private static void EjecutarMakePatches(string originales, string parcheados, string salida)
    {
        var proyecto = Path.Combine(TestPaths.RepoRoot, "tools", "MakePatches", "MakePatches.csproj");
        var psi = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var arg in new[]
                 {
                     "run", "--project", proyecto, "--",
                     "--original", originales, "--patched", parcheados, "--out", salida,
                 })
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"MakePatches falló:\n{stdout.Result}\n{stderr.Result}");
    }
}
