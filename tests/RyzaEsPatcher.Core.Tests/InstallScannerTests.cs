namespace RyzaEsPatcher.Core.Tests;

public class InstallScannerTests
{
    private static InstallStatus Scan(FakeGame game) =>
        new InstallScanner(game.Manifest, new FileHasher())
            .Scan(game.Folder, null, CancellationToken.None);

    [Fact]
    public void Todos_los_originales_da_ReadyToPatch()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();

        var status = Scan(game);

        Assert.Equal(InstallState.ReadyToPatch, status.State);
        Assert.True(status.CanPatch);
        Assert.Equal(4, status.FilesToPatch.Count);
        Assert.All(status.Files, f => Assert.Equal(FileState.Original, f.State));
    }

    [Fact]
    public void Todos_parcheados_da_AlreadyPatched_y_no_deja_parchear()
    {
        using var game = new FakeGame();
        game.WriteAllPatched();

        var status = Scan(game);

        Assert.Equal(InstallState.AlreadyPatched, status.State);
        Assert.False(status.CanPatch);
        Assert.Empty(status.FilesToPatch);
    }

    [Fact]
    public void Mezcla_de_original_y_parcheado_da_PartiallyPatched_y_deja_completar()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        game.WritePatched(@"Data\PACK01.PAK");

        var status = Scan(game);

        Assert.Equal(InstallState.PartiallyPatched, status.State);
        Assert.True(status.CanPatch);
        Assert.Equal(3, status.FilesToPatch.Count);
        Assert.DoesNotContain(status.FilesToPatch, e => e.RelativePath == @"Data\PACK01.PAK");
    }

    [Fact]
    public void Un_fichero_que_falta_da_MissingFiles_y_lo_nombra()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        game.Delete(@"Data\PACK02.PAK");

        var status = Scan(game);

        Assert.Equal(InstallState.MissingFiles, status.State);
        Assert.False(status.CanPatch);
        Assert.Contains("PACK02.PAK", status.Message);
    }

    [Fact]
    public void Un_fichero_de_contenido_desconocido_da_Unrecognized()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        game.WriteGarbage("Atelier_Ryza_DX.exe");

        var status = Scan(game);

        Assert.Equal(InstallState.Unrecognized, status.State);
        Assert.False(status.CanPatch);
        Assert.Contains("1.0.0.2", status.Message);
    }

    [Fact]
    public void Un_tamano_distinto_se_descarta_sin_hashear()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        game.WriteWrongSize(@"Data\PACK01.PAK");
        var hasher = new ContandoHasher();

        var status = new InstallScanner(game.Manifest, hasher)
            .Scan(game.Folder, null, CancellationToken.None);

        Assert.Equal(InstallState.Unrecognized, status.State);
        Assert.Equal(3, hasher.Llamadas); // los otros tres sí se hashean
    }

    [Fact]
    public void Una_carpeta_vacia_da_MissingFiles()
    {
        using var game = new FakeGame();

        var status = Scan(game);

        Assert.Equal(InstallState.MissingFiles, status.State);
        Assert.All(status.Files, f => Assert.Equal(FileState.Missing, f.State));
    }

    [Fact]
    public void Faltar_tiene_prioridad_sobre_no_reconocido()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        game.WriteGarbage(@"Data\PACK01.PAK");
        game.Delete(@"Data\PACK02.PAK");

        var status = Scan(game);

        Assert.Equal(InstallState.MissingFiles, status.State);
    }

    [Fact]
    public void El_progreso_llega_hasta_uno()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        var informes = new List<ProgressReport>();

        new InstallScanner(game.Manifest, new FileHasher())
            .Scan(game.Folder, new SyncProgress<ProgressReport>(informes.Add), CancellationToken.None);

        Assert.NotEmpty(informes);
        Assert.Equal(1.0, informes[^1].Fraction, 3);
    }

    private sealed class ContandoHasher : IFileHasher
    {
        private readonly FileHasher _real = new();

        public int Llamadas { get; private set; }

        public string ComputeSha256(string path, IProgress<long>? bytesRead, CancellationToken ct)
        {
            Llamadas++;
            return _real.ComputeSha256(path, bytesRead, ct);
        }
    }
}
