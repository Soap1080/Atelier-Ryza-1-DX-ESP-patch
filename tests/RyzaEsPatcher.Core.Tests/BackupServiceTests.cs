namespace RyzaEsPatcher.Core.Tests;

public class BackupServiceTests
{
    private static BackupService Service(FakeGame game) => new(game.Manifest, new FileHasher());

    private static void HacerBackupCompleto(FakeGame game) =>
        Service(game).EnsureBackup(game.Folder, game.Manifest.Files, "1.0.0", null, CancellationToken.None);

    [Fact]
    public void Sin_carpeta_backup_no_se_puede_restaurar()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();

        var status = Service(game).Inspect(game.Folder, CancellationToken.None);

        Assert.Equal(BackupValidity.NotFound, status.Validity);
        Assert.False(status.CanRestore);
    }

    [Fact]
    public void EnsureBackup_copia_los_cuatro_ficheros_conservando_la_estructura()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();

        HacerBackupCompleto(game);

        var backup = Path.Combine(game.Folder, "backup");
        Assert.True(File.Exists(Path.Combine(backup, "Atelier_Ryza_DX.exe")));
        Assert.True(File.Exists(Path.Combine(backup, "Data", "PACK01.PAK")));
        Assert.True(File.Exists(Path.Combine(backup, "backup.json")));
        Assert.Equal(
            game.OriginalBytes(@"Data\PACK01.PAK"),
            File.ReadAllBytes(Path.Combine(backup, "Data", "PACK01.PAK")));
    }

    [Fact]
    public void Un_backup_completo_es_valido()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        HacerBackupCompleto(game);

        var status = Service(game).Inspect(game.Folder, CancellationToken.None);

        Assert.Equal(BackupValidity.Valid, status.Validity);
        Assert.True(status.CanRestore);
    }

    [Fact]
    public void Un_backup_al_que_le_falta_un_fichero_es_incompleto()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        HacerBackupCompleto(game);
        File.Delete(Path.Combine(game.Folder, "backup", "Data", "PACK02.PAK"));

        var status = Service(game).Inspect(game.Folder, CancellationToken.None);

        Assert.Equal(BackupValidity.Incomplete, status.Validity);
        Assert.False(status.CanRestore);
        Assert.Contains("PACK02.PAK", status.Message);
    }

    [Fact]
    public void Un_backup_con_un_fichero_corrupto_no_es_valido()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        HacerBackupCompleto(game);
        var ruta = Path.Combine(game.Folder, "backup", "Data", "PACK01.PAK");
        var bytes = File.ReadAllBytes(ruta);
        bytes[10] ^= 0xFF;
        File.WriteAllBytes(ruta, bytes);

        var status = Service(game).Inspect(game.Folder, CancellationToken.None);

        Assert.Equal(BackupValidity.Invalid, status.Validity);
        Assert.Contains("PACK01.PAK", status.Message);
    }

    [Fact]
    public void EnsureBackup_no_sobrescribe_una_entrada_ya_valida()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        HacerBackupCompleto(game);
        // El juego ya está parcheado; un segundo EnsureBackup no debe pisar el original guardado.
        game.WritePatched(@"Data\PACK01.PAK");

        Service(game).EnsureBackup(game.Folder, game.Manifest.Files, "1.0.0", null, CancellationToken.None);

        Assert.Equal(
            game.OriginalBytes(@"Data\PACK01.PAK"),
            File.ReadAllBytes(Path.Combine(game.Folder, "backup", "Data", "PACK01.PAK")));
    }

    [Fact]
    public void RestoreAll_devuelve_los_ficheros_a_su_estado_original()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        HacerBackupCompleto(game);
        game.WriteAllPatched();

        Service(game).RestoreAll(game.Folder, null, CancellationToken.None);

        foreach (var relativePath in FakeGame.RelativePaths)
            Assert.Equal(game.OriginalBytes(relativePath), File.ReadAllBytes(game.FullPath(relativePath)));
    }

    [Fact]
    public void RestoreAll_falla_si_el_backup_no_es_valido()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();

        Assert.Throws<BackupException>(
            () => Service(game).RestoreAll(game.Folder, null, CancellationToken.None));
    }

    [Fact]
    public void RestoreFiles_restaura_solo_los_indicados()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        HacerBackupCompleto(game);
        game.WriteAllPatched();
        var soloUno = game.Manifest.Files.Where(f => f.RelativePath == @"Data\PACK01.PAK").ToList();

        Service(game).RestoreFiles(game.Folder, soloUno, CancellationToken.None);

        Assert.Equal(
            game.OriginalBytes(@"Data\PACK01.PAK"),
            File.ReadAllBytes(game.FullPath(@"Data\PACK01.PAK")));
        Assert.Equal(
            game.PatchedBytes(@"Data\PACK02.PAK"),
            File.ReadAllBytes(game.FullPath(@"Data\PACK02.PAK")));
    }
}
