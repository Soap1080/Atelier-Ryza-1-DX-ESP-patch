using System.Security.Cryptography;

namespace RyzaEsPatcher.Core;

public interface IFileHasher
{
    /// <summary>SHA-256 en hexadecimal mayúsculas. <paramref name="bytesRead"/> recibe el acumulado.</summary>
    string ComputeSha256(string path, IProgress<long>? bytesRead, CancellationToken ct);
}

public sealed class FileHasher : IFileHasher
{
    private const int BufferSize = 1024 * 1024;

    public string ComputeSha256(string path, IProgress<long>? bytesRead, CancellationToken ct)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
        using var sha = SHA256.Create();

        var buffer = new byte[BufferSize];
        long total = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, read, null, 0);
            total += read;
            bytesRead?.Report(total);
        }

        sha.TransformFinalBlock(buffer, 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }
}
