using MakePatches;
using RyzaEsPatcher.Core;

var opciones = Argumentos.Parse(args);
if (opciones is null)
{
    Console.Error.WriteLine("""
        Uso:
          MakePatches --original <carpeta> --patched <carpeta> --out <carpeta>
                      [--patch-version 1.0.0] [--game-version 1.0.0.2]

          --original  carpeta con los archivos ORIGINALES del juego (con su estructura)
          --patched   carpeta con los mismos archivos YA PARCHEADOS
          --out       carpeta donde se escriben manifest.json y patches/
        """);
    return 1;
}

string[] rutasRelativas =
[
    "Atelier_Ryza_DX.exe",
    @"Data\PACK00_04_01.PAK",
    @"Data\PACK01.PAK",
    @"Data\PACK02.PAK",
];

var repoRoot = LocalizarRepoRoot();
var hdiffz = HdiffzRunner.LocateExecutable(repoRoot);
var hasher = new FileHasher();

Directory.CreateDirectory(Path.Combine(opciones.Out, "patches"));
var entradas = new List<PatchFileEntry>();

foreach (var relativa in rutasRelativas)
{
    var original = Resolver(opciones.Original, relativa);
    var parcheado = Resolver(opciones.Patched, relativa);

    if (original is null)
    {
        Console.Error.WriteLine($"No encuentro el original de {relativa} en {opciones.Original}");
        return 2;
    }

    if (parcheado is null)
    {
        Console.Error.WriteLine($"No encuentro el parcheado de {relativa} en {opciones.Patched}");
        return 2;
    }

    var nombreDiff = "patches/" + Path.GetFileName(relativa) + ".hdiff";
    var rutaDiff = Path.Combine(opciones.Out, nombreDiff.Replace('/', Path.DirectorySeparatorChar));

    Console.WriteLine($"Generando delta de {Path.GetFileName(relativa)}…");
    HdiffzRunner.Create(hdiffz, original, parcheado, rutaDiff);

    entradas.Add(new PatchFileEntry(
        relativa,
        new FileInfo(original).Length,
        hasher.ComputeSha256(original, null, CancellationToken.None),
        new FileInfo(parcheado).Length,
        hasher.ComputeSha256(parcheado, null, CancellationToken.None),
        nombreDiff,
        hasher.ComputeSha256(rutaDiff, null, CancellationToken.None)));

    Console.WriteLine($"  delta: {new FileInfo(rutaDiff).Length / (1024 * 1024)} MB");
}

var manifest = new PatchManifest(
    PatchManifest.SupportedSchemaVersion,
    "Atelier Ryza DX — Traducción al español",
    opciones.PatchVersion,
    opciones.GameVersion,
    entradas);

var rutaManifest = Path.Combine(opciones.Out, "manifest.json");
File.WriteAllText(rutaManifest, manifest.ToJson());

var totalDeltas = entradas.Sum(e => new FileInfo(
    Path.Combine(opciones.Out, e.DiffResource.Replace('/', Path.DirectorySeparatorChar))).Length);

Console.WriteLine();
Console.WriteLine($"Escrito {rutaManifest}");
Console.WriteLine($"Tamaño total de los deltas: {totalDeltas / (1024 * 1024)} MB");
return 0;

// Se acepta tanto la estructura del juego (Data\PACK01.PAK) como los archivos sueltos en la
// misma carpeta, que es como suelen quedar los parcheados recién generados.
static string? Resolver(string carpeta, string relativa)
{
    var conEstructura = Path.Combine(carpeta, relativa);
    if (File.Exists(conEstructura)) return conEstructura;

    var plano = Path.Combine(carpeta, Path.GetFileName(relativa));
    return File.Exists(plano) ? plano : null;
}

static string LocalizarRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "RyzaEsPatcher.sln"))) return dir.FullName;
        dir = dir.Parent;
    }

    throw new InvalidOperationException("No encuentro la raíz del repositorio (RyzaEsPatcher.sln).");
}
