using System.Diagnostics;
using System.Security.Cryptography;

namespace RyzaEsPatcher.Core;

/// <summary>
/// Extrae el <c>hpatchz.exe</c> embebido a una carpeta temporal propia (verificando su SHA-256)
/// y lo invoca para aplicar los deltas.
/// </summary>
public sealed class HPatchEngine : IPatchEngine
{
    public const string ExpectedSha256 = "9703C694B5955C576D9F0E26E98B60941F0BBB53B382B1F1988D75D461E580CB";
    private const string ResourceName = "hpatchz.exe";

    private readonly string _tempFolder;
    private bool _disposed;

    public string ExecutablePath { get; }

    public HPatchEngine()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "RyzaDX-ParcheES", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
        ExecutablePath = Path.Combine(_tempFolder, ResourceName);

        try
        {
            Extract(ExecutablePath);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static void Extract(string targetPath)
    {
        using (var resource = typeof(HPatchEngine).Assembly.GetManifestResourceStream(ResourceName))
        {
            if (resource is null)
                throw new PatchEngineException(
                    "Esta compilación no incluye hpatchz.exe. Falta el recurso embebido.");

            using var output = File.Create(targetPath);
            resource.CopyTo(output);
        }

        using var stream = File.OpenRead(targetPath);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new PatchEngineException(
                "El hpatchz.exe extraído no coincide con el esperado. Compilación corrupta.");
    }

    public void Apply(string oldFile, string diffFile, string newFile, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var psi = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-f");           // sobrescribe el archivo de salida si existiera
        psi.ArgumentList.Add(oldFile);
        psi.ArgumentList.Add(diffFile);
        psi.ArgumentList.Add(newFile);

        using var process = Process.Start(psi)
            ?? throw new PatchEngineException("No se pudo iniciar hpatchz.exe.");

        // Las lecturas asíncronas vacían las tuberías mientras esperamos, para que el proceso
        // no se bloquee al llenar el búfer de salida.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using (ct.Register(() => TryKill(process)))
        {
            process.WaitForExit();
        }

        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            var salida = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
            throw new PatchEngineException(
                $"hpatchz.exe falló (código {process.ExitCode}) al parchear '{Path.GetFileName(oldFile)}'.{Environment.NewLine}{salida}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            Directory.Delete(_tempFolder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
