using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RyzaEsPatcher.Core;

/// <summary>Lee las rutas de biblioteca de un <c>libraryfolders.vdf</c> de Steam.</summary>
public static partial class LibraryFoldersVdf
{
    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex PathEntry();

    public static IReadOnlyList<string> ParsePaths(string vdfContent)
    {
        var result = new List<string>();
        foreach (Match match in PathEntry().Matches(vdfContent))
        {
            // En los .vdf las barras van escapadas.
            var path = match.Groups[1].Value.Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path))
                result.Add(path);
        }

        return result;
    }
}

/// <summary>Intenta localizar la instalación del juego sin molestar al usuario.</summary>
public static class SteamLocator
{
    public const string GameExeName = "Atelier_Ryza_DX.exe";

    /// <summary>Devuelve la carpeta del juego, o null si no la encuentra. Nunca lanza.</summary>
    public static string? TryFindGameFolder()
    {
        try
        {
            foreach (var library in GetLibraryFolders())
            {
                var common = Path.Combine(library, "steamapps", "common");
                if (!Directory.Exists(common)) continue;

                foreach (var candidate in Directory.EnumerateDirectories(common))
                {
                    if (File.Exists(Path.Combine(candidate, GameExeName)))
                        return candidate;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return null;
    }

    private static IEnumerable<string> GetLibraryFolders()
    {
        var steamPath = ReadSteamPath();
        if (steamPath is null) yield break;

        yield return steamPath;

        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;

        foreach (var path in LibraryFoldersVdf.ParsePaths(File.ReadAllText(vdf)))
            yield return path;
    }

    private static string? ReadSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch (System.Security.SecurityException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (IOException) { return null; }
    }
}
