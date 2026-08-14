namespace RyzaEsPatcher.Core.Tests;

public class PreflightTests
{
    private static InstallStatus StatusListo(FakeGame game) =>
        new InstallScanner(game.Manifest, new FileHasher()).Scan(game.Folder, null, CancellationToken.None);

    [Fact]
    public void RequiredBytes_con_backup_suma_los_originales_mas_el_mayor_parcheado()
    {
        using var game = new FakeGame();
        var files = game.Manifest.Files;
        var esperado = files.Sum(f => f.OriginalSize) + files.Max(f => f.PatchedSize) + Preflight.MarginBytes;

        Assert.Equal(esperado, Preflight.RequiredBytes(files, createBackup: true));
    }

    [Fact]
    public void RequiredBytes_sin_backup_solo_reserva_el_temporal_mayor()
    {
        using var game = new FakeGame();
        var files = game.Manifest.Files;
        var esperado = files.Max(f => f.PatchedSize) + Preflight.MarginBytes;

        Assert.Equal(esperado, Preflight.RequiredBytes(files, createBackup: false));
    }

    [Fact]
    public void Check_falla_si_no_hay_espacio_suficiente_e_indica_cuanto_falta()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();

        var resultado = Preflight.Check(StatusListo(game), createBackup: true, freeBytesOverride: 1000);

        Assert.False(resultado.Ok);
        Assert.Contains("espacio", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Check_pasa_con_espacio_de_sobra()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();

        var resultado = Preflight.Check(
            StatusListo(game), createBackup: true, freeBytesOverride: 100L * 1024 * 1024 * 1024);

        Assert.True(resultado.Ok);
    }

    [Fact]
    public void Check_falla_si_la_carpeta_no_admite_escritura()
    {
        using var game = new FakeGame();
        game.WriteAllOriginal();
        var status = StatusListo(game) with { GameFolder = Path.Combine(game.Folder, "no-existe") };

        var resultado = Preflight.Check(status, createBackup: false, freeBytesOverride: long.MaxValue);

        Assert.False(resultado.Ok);
        Assert.Contains("administrador", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanWriteTo_es_cierto_en_una_carpeta_temporal()
    {
        var dir = Directory.CreateTempSubdirectory("ryzapf_").FullName;
        try
        {
            Assert.True(Preflight.CanWriteTo(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
