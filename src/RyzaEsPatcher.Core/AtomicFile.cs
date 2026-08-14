namespace RyzaEsPatcher.Core;

/// <summary>
/// Escritura segura: siempre se escribe primero a un temporal en la misma carpeta (y por tanto
/// en el mismo volumen) y solo se sustituye el destino cuando el contenido es correcto.
/// </summary>
public static class AtomicFile
{
    public const string TempSuffix = ".ryzapatch.tmp";

    public static string TempPathFor(string targetPath) => targetPath + TempSuffix;

    /// <summary>Sustituye <paramref name="targetPath"/> por <paramref name="tempPath"/>.</summary>
    public static void Commit(string tempPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Copia comprobando el SHA-256 del resultado antes de sustituir el destino.</summary>
    public static void CopyVerified(
        string source,
        string target,
        string expectedSha256,
        IFileHasher hasher,
        IProgress<long>? bytesCopied,
        CancellationToken ct)
    {
        var temp = TempPathFor(target);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        TryDelete(temp);

        try
        {
            CopyStream(source, temp, bytesCopied, ct);

            var actual = hasher.ComputeSha256(temp, null, ct);
            if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException(
                    $"La copia de '{Path.GetFileName(source)}' no coincide con el hash esperado.");

            Commit(temp, target);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void CopyStream(string source, string target, IProgress<long>? bytesCopied, CancellationToken ct)
    {
        const int BufferSize = 1024 * 1024;
        using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
        using var output = new FileStream(
            target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

        var buffer = new byte[BufferSize];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            total += read;
            bytesCopied?.Report(total);
        }
    }
}
