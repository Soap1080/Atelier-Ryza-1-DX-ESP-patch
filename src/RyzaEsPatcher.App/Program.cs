using RyzaEsPatcher.Core;

namespace RyzaEsPatcher.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Pone la barra de título y los diálogos del sistema en oscuro. El resto de la ventana
        // lo pintamos nosotros en Theme, porque WinForms no tematiza todos los controles.
        Theme.SetSystemColorMode(UiSettings.Load().DarkMode);

        string? carpetaInicial = null;
        for (var i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--folder") carpetaInicial = args[i + 1];
        }

        Application.Run(new MainForm(carpetaInicial));
    }
}
