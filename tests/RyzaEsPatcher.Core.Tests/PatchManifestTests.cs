namespace RyzaEsPatcher.Core.Tests;

public class PatchManifestTests
{
    private const string ValidJson = """
    {
      "schemaVersion": 1,
      "patchName": "Atelier Ryza DX — Traducción al español",
      "patchVersion": "1.0.0",
      "gameVersion": "1.0.0.2",
      "files": [
        {
          "relativePath": "Atelier_Ryza_DX.exe",
          "originalSize": 100,
          "originalSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "patchedSize": 100,
          "patchedSha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
          "diffResource": "patches/Atelier_Ryza_DX.exe.hdiff",
          "diffSha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
        }
      ]
    }
    """;

    [Fact]
    public void Parse_lee_las_cabeceras_y_los_ficheros()
    {
        var manifest = PatchManifest.Parse(ValidJson);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("1.0.0.2", manifest.GameVersion);
        Assert.Single(manifest.Files);
        Assert.Equal("Atelier_Ryza_DX.exe", manifest.Files[0].RelativePath);
        Assert.Equal(100, manifest.Files[0].OriginalSize);
    }

    [Fact]
    public void FileName_devuelve_solo_el_nombre_del_fichero()
    {
        var entry = new PatchFileEntry(
            @"Data\PACK01.PAK", 1, new string('a', 64), 1, new string('b', 64),
            "patches/x.hdiff", new string('c', 64));

        Assert.Equal("PACK01.PAK", entry.FileName);
    }

    [Fact]
    public void Parse_rechaza_una_version_de_esquema_desconocida()
    {
        var json = ValidJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");

        var ex = Assert.Throws<PatchManifestException>(() => PatchManifest.Parse(json));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void Parse_rechaza_un_manifest_sin_ficheros()
    {
        var json = ValidJson.Replace("\"files\": [", "\"files2\": [");

        Assert.Throws<PatchManifestException>(() => PatchManifest.Parse(json));
    }

    [Fact]
    public void Parse_rechaza_un_hash_que_no_es_sha256_hexadecimal()
    {
        var json = ValidJson.Replace(new string('a', 64), "no-es-un-hash");

        Assert.Throws<PatchManifestException>(() => PatchManifest.Parse(json));
    }

    [Fact]
    public void Parse_rechaza_json_invalido()
    {
        Assert.Throws<PatchManifestException>(() => PatchManifest.Parse("{ esto no es json"));
    }

    [Fact]
    public void ToJson_y_Parse_hacen_round_trip()
    {
        var original = PatchManifest.Parse(ValidJson);

        var vuelta = PatchManifest.Parse(original.ToJson());

        Assert.Equal(original.Files[0].PatchedSha256, vuelta.Files[0].PatchedSha256);
        Assert.Equal(original.PatchVersion, vuelta.PatchVersion);
    }
}
