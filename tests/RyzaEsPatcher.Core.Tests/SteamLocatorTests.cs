namespace RyzaEsPatcher.Core.Tests;

public class SteamLocatorTests
{
    private const string Vdf = """
    "libraryfolders"
    {
        "0"
        {
            "path"        "C:\\Program Files (x86)\\Steam"
            "label"       ""
            "contentid"   "123"
        }
        "1"
        {
            "path"        "D:\\SteamLibrary"
            "label"       ""
        }
    }
    """;

    [Fact]
    public void ParsePaths_extrae_todas_las_bibliotecas()
    {
        var paths = LibraryFoldersVdf.ParsePaths(Vdf);

        Assert.Equal(2, paths.Count);
        Assert.Contains(@"C:\Program Files (x86)\Steam", paths);
        Assert.Contains(@"D:\SteamLibrary", paths);
    }

    [Fact]
    public void ParsePaths_ignora_otras_claves_con_comillas()
    {
        var paths = LibraryFoldersVdf.ParsePaths(Vdf);

        Assert.DoesNotContain("123", paths);
        Assert.DoesNotContain(string.Empty, paths);
    }

    [Fact]
    public void ParsePaths_devuelve_vacio_con_contenido_basura()
    {
        Assert.Empty(LibraryFoldersVdf.ParsePaths("esto no es un vdf"));
    }

    [Fact]
    public void TryFindGameFolder_no_lanza_aunque_no_haya_Steam()
    {
        var ex = Record.Exception(() => SteamLocator.TryFindGameFolder());

        Assert.Null(ex);
    }
}
