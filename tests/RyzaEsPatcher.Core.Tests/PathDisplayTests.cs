namespace RyzaEsPatcher.Core.Tests;

public class PathDisplayTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ryzapath_").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void RealCase_recupera_las_mayusculas_reales_de_disco()
    {
        var real = Path.Combine(_dir, "Atelier Ryza DX");
        Directory.CreateDirectory(real);

        var resultado = PathDisplay.RealCase(real.ToLowerInvariant());

        Assert.EndsWith("Atelier Ryza DX", resultado, StringComparison.Ordinal);
    }

    [Fact]
    public void RealCase_convierte_las_barras_de_unix_en_barras_de_Windows()
    {
        var real = Path.Combine(_dir, "Data");
        Directory.CreateDirectory(real);

        var resultado = PathDisplay.RealCase(real.Replace('\\', '/'));

        Assert.DoesNotContain('/', resultado);
        Assert.EndsWith(@"\Data", resultado, StringComparison.Ordinal);
    }

    [Fact]
    public void RealCase_pone_la_letra_de_unidad_en_mayuscula()
    {
        var resultado = PathDisplay.RealCase(_dir.ToLowerInvariant());

        Assert.Equal(char.ToUpperInvariant(_dir[0]), resultado[0]);
    }

    [Fact]
    public void RealCase_no_falla_con_una_ruta_que_no_existe()
    {
        var inventada = Path.Combine(_dir, "no", "existe");

        var resultado = PathDisplay.RealCase(inventada);

        Assert.EndsWith(Path.Combine("no", "existe"), resultado, StringComparison.Ordinal);
    }

    [Fact]
    public void RealCase_devuelve_lo_mismo_con_una_cadena_vacia()
    {
        Assert.Equal(string.Empty, PathDisplay.RealCase(string.Empty));
    }
}
