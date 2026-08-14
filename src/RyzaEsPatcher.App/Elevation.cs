using System.Diagnostics;

namespace RyzaEsPatcher.App;

/// <summary>Relanza el programa con privilegios de administrador conservando la carpeta elegida.</summary>
public static class Elevation
{
    public static bool TryRelaunchAsAdmin(string? gameFolder)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return false;

        var psi = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
        if (!string.IsNullOrWhiteSpace(gameFolder))
        {
            psi.ArgumentList.Add("--folder");
            psi.ArgumentList.Add(gameFolder);
        }

        try
        {
            Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // El usuario canceló el diálogo de control de cuentas de usuario.
            return false;
        }
    }
}
