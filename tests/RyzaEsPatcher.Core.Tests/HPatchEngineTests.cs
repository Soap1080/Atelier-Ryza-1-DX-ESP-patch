namespace RyzaEsPatcher.Core.Tests;

public class HPatchEngineTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ryzahp_").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (string OldFile, string NewFile, string DiffFile) PrepararCaso()
    {
        var oldFile = Path.Combine(_dir, "old.bin");
        var newFile = Path.Combine(_dir, "new.bin");
        var diffFile = Path.Combine(_dir, "d.hdiff");

        var rnd = new Random(99);
        var original = new byte[512 * 1024];
        rnd.NextBytes(original);
        var inserted = new byte[16 * 1024];
        rnd.NextBytes(inserted);

        var patched = new byte[original.Length + inserted.Length];
        original.AsSpan(0, 256 * 1024).CopyTo(patched);
        inserted.CopyTo(patched, 256 * 1024);
        original.AsSpan(256 * 1024).CopyTo(patched.AsSpan(256 * 1024 + inserted.Length));

        File.WriteAllBytes(oldFile, original);
        File.WriteAllBytes(newFile, patched);
        Hdiffz.Create(oldFile, newFile, diffFile);

        return (oldFile, newFile, diffFile);
    }

    [Fact]
    public void Apply_reproduce_el_fichero_parcheado_byte_a_byte()
    {
        var (oldFile, newFile, diffFile) = PrepararCaso();
        var outFile = Path.Combine(_dir, "out.bin");
        using var engine = new HPatchEngine();

        engine.Apply(oldFile, diffFile, outFile, CancellationToken.None);

        Assert.Equal(File.ReadAllBytes(newFile), File.ReadAllBytes(outFile));
    }

    [Fact]
    public void Apply_falla_si_el_fichero_de_origen_tiene_otro_tamano()
    {
        var (_, _, diffFile) = PrepararCaso();
        var otro = Path.Combine(_dir, "otro.bin");
        File.WriteAllBytes(otro, new byte[1024]);
        var outFile = Path.Combine(_dir, "out2.bin");
        using var engine = new HPatchEngine();

        Assert.Throws<PatchEngineException>(() => engine.Apply(otro, diffFile, outFile, CancellationToken.None));
    }

    /// <summary>
    /// hpatchz solo comprueba el TAMAÑO del origen, no su contenido: con un origen del mismo
    /// tamaño pero distinto produce basura sin quejarse. Por eso PatchApplier verifica siempre
    /// el SHA-256 del resultado antes de sustituir nada.
    /// </summary>
    [Fact]
    public void Apply_con_un_origen_del_mismo_tamano_pero_distinto_no_produce_el_resultado_esperado()
    {
        var (_, newFile, diffFile) = PrepararCaso();
        var otro = Path.Combine(_dir, "otro_igual_de_grande.bin");
        File.WriteAllBytes(otro, new byte[512 * 1024]);
        var outFile = Path.Combine(_dir, "out3.bin");
        using var engine = new HPatchEngine();

        try
        {
            engine.Apply(otro, diffFile, outFile, CancellationToken.None);
        }
        catch (PatchEngineException)
        {
            return; // también vale que falle
        }

        Assert.NotEqual(File.ReadAllBytes(newFile), File.ReadAllBytes(outFile));
    }

    [Fact]
    public void El_ejecutable_extraido_se_borra_al_hacer_Dispose()
    {
        string ruta;
        using (var engine = new HPatchEngine())
        {
            ruta = engine.ExecutablePath;
            Assert.True(File.Exists(ruta));
        }

        Assert.False(File.Exists(ruta));
    }
}
