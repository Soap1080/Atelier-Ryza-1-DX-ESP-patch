namespace RyzaEsPatcher.Core.Tests;

public class PatchApplierTests : IDisposable
{
    private readonly FakeGame _game = new();
    private readonly string _patchFolder;
    private readonly HPatchEngine _engine = new();

    public PatchApplierTests()
    {
        // Deltas reales generados con hdiffz a partir de los contenidos de FakeGame.
        _patchFolder = Path.Combine(_game.Root, "patch");
        var scratch = Path.Combine(_game.Root, "scratch");
        Directory.CreateDirectory(scratch);

        foreach (var entry in _game.Manifest.Files)
        {
            var oldFile = Path.Combine(scratch, entry.FileName + ".old");
            var newFile = Path.Combine(scratch, entry.FileName + ".new");
            File.WriteAllBytes(oldFile, _game.OriginalBytes(entry.RelativePath));
            File.WriteAllBytes(newFile, _game.PatchedBytes(entry.RelativePath));
            Hdiffz.Create(oldFile, newFile, Path.Combine(_patchFolder, entry.DiffResource));
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
        _game.Dispose();
    }

    /// <summary>El manifest de FakeGame no conoce el hash real del delta; aquí se rellena.</summary>
    private PatchManifest ManifestConHashesDeDelta()
    {
        var hasher = new FileHasher();
        var files = _game.Manifest.Files
            .Select(e => e with
            {
                DiffSha256 = hasher.ComputeSha256(
                    Path.Combine(_patchFolder, e.DiffResource.Replace('/', Path.DirectorySeparatorChar)),
                    null,
                    CancellationToken.None),
            })
            .ToList();

        return _game.Manifest with { Files = files };
    }

    private PatchApplier CrearApplier(IPatchEngine? engine = null)
    {
        var hasher = new FileHasher();
        var manifest = ManifestConHashesDeDelta();
        return new PatchApplier(
            new DirectoryPatchBundle(manifest, _patchFolder),
            engine ?? _engine,
            new BackupService(manifest, hasher),
            hasher);
    }

    private InstallStatus Escanear() =>
        new InstallScanner(ManifestConHashesDeDelta(), new FileHasher())
            .Scan(_game.Folder, null, CancellationToken.None);

    [Fact]
    public void Install_deja_los_cuatro_ficheros_parcheados()
    {
        _game.WriteAllOriginal();

        CrearApplier().Install(Escanear(), new PatchOptions(CreateBackup: true), null, CancellationToken.None);

        foreach (var relativePath in FakeGame.RelativePaths)
            Assert.Equal(_game.PatchedBytes(relativePath), File.ReadAllBytes(_game.FullPath(relativePath)));
        Assert.Equal(InstallState.AlreadyPatched, Escanear().State);
    }

    [Fact]
    public void Install_con_backup_deja_los_originales_guardados()
    {
        _game.WriteAllOriginal();

        CrearApplier().Install(Escanear(), new PatchOptions(CreateBackup: true), null, CancellationToken.None);

        var status = new BackupService(ManifestConHashesDeDelta(), new FileHasher())
            .Inspect(_game.Folder, CancellationToken.None);
        Assert.Equal(BackupValidity.Valid, status.Validity);
    }

    [Fact]
    public void Install_sin_backup_no_crea_la_carpeta()
    {
        _game.WriteAllOriginal();

        CrearApplier().Install(Escanear(), new PatchOptions(CreateBackup: false), null, CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(_game.Folder, "backup")));
    }

    [Fact]
    public void Install_solo_toca_los_ficheros_que_estan_sin_parchear()
    {
        _game.WriteAllOriginal();
        _game.WritePatched(@"Data\PACK01.PAK");

        CrearApplier().Install(Escanear(), new PatchOptions(CreateBackup: false), null, CancellationToken.None);

        Assert.Equal(InstallState.AlreadyPatched, Escanear().State);
    }

    [Fact]
    public void Si_falla_el_tercero_y_hay_backup_todo_vuelve_a_estar_como_estaba()
    {
        _game.WriteAllOriginal();
        var motorQueFalla = new EngineFallaEn(_engine, fallarEnLlamada: 3);

        Assert.Throws<PatchFailedException>(() => CrearApplier(motorQueFalla)
            .Install(Escanear(), new PatchOptions(CreateBackup: true), null, CancellationToken.None));

        foreach (var relativePath in FakeGame.RelativePaths)
            Assert.Equal(_game.OriginalBytes(relativePath), File.ReadAllBytes(_game.FullPath(relativePath)));
        Assert.Equal(InstallState.ReadyToPatch, Escanear().State);
    }

    [Fact]
    public void Si_falla_sin_backup_el_error_dice_que_ficheros_quedaron_parcheados()
    {
        _game.WriteAllOriginal();
        var motorQueFalla = new EngineFallaEn(_engine, fallarEnLlamada: 3);

        var ex = Assert.Throws<PatchFailedException>(() => CrearApplier(motorQueFalla)
            .Install(Escanear(), new PatchOptions(CreateBackup: false), null, CancellationToken.None));

        Assert.Contains("Atelier_Ryza_DX.exe", ex.Message);
        Assert.Contains("PACK00_04_01.PAK", ex.Message);
    }

    [Fact]
    public void Un_fallo_no_deja_ficheros_temporales_sueltos()
    {
        _game.WriteAllOriginal();
        var motorQueFalla = new EngineFallaEn(_engine, fallarEnLlamada: 2);

        Assert.Throws<PatchFailedException>(() => CrearApplier(motorQueFalla)
            .Install(Escanear(), new PatchOptions(CreateBackup: true), null, CancellationToken.None));

        var temporales = Directory.GetFiles(
            _game.Folder, "*" + AtomicFile.TempSuffix, SearchOption.AllDirectories);
        Assert.Empty(temporales);
    }

    [Fact]
    public void Un_delta_danado_no_modifica_el_fichero_original()
    {
        _game.WriteAllOriginal();
        var manifest = ManifestConHashesDeDelta();
        var conHashMalo = manifest with
        {
            Files = manifest.Files.Select(e => e with { DiffSha256 = new string('a', 64) }).ToList(),
        };
        var hasher = new FileHasher();
        var applier = new PatchApplier(
            new DirectoryPatchBundle(conHashMalo, _patchFolder), _engine,
            new BackupService(conHashMalo, hasher), hasher);
        var status = new InstallScanner(conHashMalo, hasher).Scan(_game.Folder, null, CancellationToken.None);

        Assert.Throws<PatchFailedException>(
            () => applier.Install(status, new PatchOptions(CreateBackup: false), null, CancellationToken.None));

        foreach (var relativePath in FakeGame.RelativePaths)
            Assert.Equal(_game.OriginalBytes(relativePath), File.ReadAllBytes(_game.FullPath(relativePath)));
    }

    [Fact]
    public void Uninstall_devuelve_el_juego_a_su_estado_original()
    {
        _game.WriteAllOriginal();
        var applier = CrearApplier();
        applier.Install(Escanear(), new PatchOptions(CreateBackup: true), null, CancellationToken.None);

        applier.Uninstall(_game.Folder, null, CancellationToken.None);

        Assert.Equal(InstallState.ReadyToPatch, Escanear().State);
        foreach (var relativePath in FakeGame.RelativePaths)
            Assert.Equal(_game.OriginalBytes(relativePath), File.ReadAllBytes(_game.FullPath(relativePath)));
    }

    [Fact]
    public void Uninstall_sin_backup_falla_con_un_mensaje_claro()
    {
        _game.WriteAllPatched();

        var ex = Assert.Throws<BackupException>(
            () => CrearApplier().Uninstall(_game.Folder, null, CancellationToken.None));

        Assert.Contains("copia de seguridad", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Install_informa_del_progreso_de_principio_a_fin()
    {
        _game.WriteAllOriginal();
        var informes = new List<ProgressReport>();

        CrearApplier().Install(
            Escanear(),
            new PatchOptions(CreateBackup: true),
            new SyncProgress<ProgressReport>(informes.Add),
            CancellationToken.None);

        Assert.NotEmpty(informes);
        Assert.All(informes, r => Assert.InRange(r.Fraction, 0.0, 1.0));
        Assert.Equal(1.0, informes[^1].Fraction, 3);
    }

    /// <summary>Motor que delega en el real pero revienta en la enésima llamada.</summary>
    private sealed class EngineFallaEn(IPatchEngine inner, int fallarEnLlamada) : IPatchEngine
    {
        private int _llamadas;

        public void Apply(string oldFile, string diffFile, string newFile, CancellationToken ct)
        {
            _llamadas++;
            if (_llamadas == fallarEnLlamada)
                throw new PatchEngineException("fallo simulado");

            inner.Apply(oldFile, diffFile, newFile, ct);
        }

        public void Dispose()
        {
            // El motor real lo libera quien lo creó.
        }
    }
}
