using System.Diagnostics;

namespace RyzaEsPatcher.Core.Tests;

/// <summary>Genera deltas reales en los tests invocando el hdiffz de third_party.</summary>
public static class Hdiffz
{
    public static void Create(string oldFile, string newFile, string diffFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(diffFile)!);

        var psi = new ProcessStartInfo(TestPaths.HdiffzExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-s-64");
        psi.ArgumentList.Add("-c-zstd-21-24");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(oldFile);
        psi.ArgumentList.Add(newFile);
        psi.ArgumentList.Add(diffFile);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"hdiffz falló ({process.ExitCode}):\n{stdout.Result}\n{stderr.Result}");
    }
}
