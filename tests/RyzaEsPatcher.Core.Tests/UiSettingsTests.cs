namespace RyzaEsPatcher.Core.Tests;

public class UiSettingsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ryzaui_").FullName;

    private string Archivo => Path.Combine(_dir, "ui.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void El_modo_oscuro_es_el_predeterminado()
    {
        Assert.True(UiSettings.Default.DarkMode);
        Assert.True(UiSettings.Load(Archivo).DarkMode);
    }

    [Fact]
    public void Save_y_Load_conservan_la_preferencia()
    {
        new UiSettings(DarkMode: false).Save(Archivo);

        Assert.False(UiSettings.Load(Archivo).DarkMode);
    }

    [Fact]
    public void Un_archivo_corrupto_no_rompe_nada_y_vuelve_al_valor_por_defecto()
    {
        File.WriteAllText(Archivo, "{ esto no es json");

        Assert.True(UiSettings.Load(Archivo).DarkMode);
    }

    [Fact]
    public void Save_crea_la_carpeta_si_no_existe()
    {
        var anidado = Path.Combine(_dir, "a", "b", "ui.json");

        new UiSettings(DarkMode: false).Save(anidado);

        Assert.True(File.Exists(anidado));
    }
}
