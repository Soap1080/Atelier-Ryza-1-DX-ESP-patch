using System.Security.Cryptography;

namespace RyzaEsPatcher.Core.Tests;

public class FileHasherTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ryzatest_").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ComputeSha256_coincide_con_el_hash_de_referencia()
    {
        var path = Path.Combine(_dir, "a.bin");
        var data = new byte[3 * 1024 * 1024];
        new Random(7).NextBytes(data);
        File.WriteAllBytes(path, data);
        var esperado = Convert.ToHexString(SHA256.HashData(data));

        var obtenido = new FileHasher().ComputeSha256(path, null, CancellationToken.None);

        Assert.Equal(esperado, obtenido);
    }

    [Fact]
    public void ComputeSha256_informa_del_avance_en_bytes()
    {
        var path = Path.Combine(_dir, "b.bin");
        File.WriteAllBytes(path, new byte[3 * 1024 * 1024]);
        long ultimo = 0;

        new FileHasher().ComputeSha256(path, new SyncProgress<long>(v => ultimo = v), CancellationToken.None);

        Assert.Equal(3 * 1024 * 1024, ultimo);
    }

    [Fact]
    public void ComputeSha256_respeta_la_cancelacion()
    {
        var path = Path.Combine(_dir, "c.bin");
        File.WriteAllBytes(path, new byte[8 * 1024 * 1024]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => new FileHasher().ComputeSha256(path, null, cts.Token));
    }
}
