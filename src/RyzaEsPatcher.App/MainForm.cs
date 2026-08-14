using System.Reflection;
using RyzaEsPatcher.Core;

namespace RyzaEsPatcher.App;

public sealed class MainForm : Form
{
    private readonly Label _etiquetaRuta = new();
    private readonly TextBox _rutaTexto = new();
    private readonly Button _examinar = new();
    private readonly ListView _ficheros = new();
    private readonly ThemedCheckBox _backup = new();
    private readonly ThemedCheckBox _modoOscuro = new();
    private readonly Label _estado = new();
    private readonly ProgressPanel _barra = new();
    private readonly Button _parchear = new();
    private readonly Button _quitar = new();
    private readonly Button _acerca = new();

    private readonly EmbeddedPatchBundle? _bundle;
    private readonly string? _bundleError;
    private readonly FileHasher _hasher = new();

    private Palette _paleta;
    private InstallStatus? _status;
    private BackupStatus? _backupStatus;
    private bool _ocupado;

    public MainForm(string? carpetaInicial)
    {
        EmbeddedPatchBundle.TryLoad(Assembly.GetExecutingAssembly(), out _bundle, out _bundleError);

        var preferencias = UiSettings.Load();
        _paleta = Theme.For(preferencias.DarkMode);

        ConstruirInterfaz();
        _modoOscuro.Checked = preferencias.DarkMode;

        Load += (_, _) => AplicarTema();

        Shown += async (_, _) =>
        {
            AplicarTema();

            if (_bundle is null)
            {
                MostrarMensaje(_bundleError ?? "Esta compilación no incluye ningún parche.");
                return;
            }

            var carpeta = carpetaInicial ?? SteamLocator.TryFindGameFolder();
            if (carpeta is not null)
            {
                _rutaTexto.Text = PathDisplay.RealCase(carpeta);
                await RefrescarAsync();
            }
            else
            {
                MostrarMensaje("Elige la carpeta donde tienes instalado el juego.");
            }
        };
    }

    private void ConstruirInterfaz()
    {
        Text = "Parche al español — Atelier Ryza DX";
        if (CargarIcono() is { } icono) Icon = icono;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 392);
        Font = new Font("Segoe UI", 9F);

        _etiquetaRuta.Text = "Carpeta del juego:";
        _etiquetaRuta.AutoSize = true;
        _etiquetaRuta.Location = new Point(12, 16);

        _rutaTexto.Location = new Point(120, 12);
        _rutaTexto.Size = new Size(386, 25);
        _rutaTexto.ReadOnly = true;
        _rutaTexto.TabStop = false;

        _examinar.Text = "Examinar…";
        _examinar.Location = new Point(514, 11);
        _examinar.Size = new Size(94, 27);
        _examinar.Click += async (_, _) => await ExaminarAsync();

        _ficheros.Location = new Point(12, 50);
        _ficheros.Size = new Size(596, 118);
        _ficheros.View = View.Details;
        _ficheros.FullRowSelect = true;
        _ficheros.MultiSelect = false;
        _ficheros.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _ficheros.BorderStyle = BorderStyle.FixedSingle;
        _ficheros.OwnerDraw = true;
        _ficheros.Columns.Add("Archivo", 370);
        _ficheros.Columns.Add("Estado", 204);
        _ficheros.HandleCreated += (_, _) => AjustarColumnas();
        _ficheros.DrawColumnHeader += DibujarCabecera;
        _ficheros.DrawItem += (_, e) => e.DrawDefault = false;
        _ficheros.DrawSubItem += DibujarCelda;

        _backup.Text = "Hacer copia de seguridad (carpeta backup)";
        _backup.Location = new Point(12, 178);
        _backup.AutoSize = true;
        _backup.Checked = true;

        _modoOscuro.Text = "Modo oscuro";
        _modoOscuro.AutoSize = true;
        _modoOscuro.Location = new Point(508, 178);
        _modoOscuro.CheckedChanged += (_, _) =>
        {
            _paleta = Theme.For(_modoOscuro.Checked);
            new UiSettings(_modoOscuro.Checked).Save();
            Theme.SetSystemColorMode(_modoOscuro.Checked);
            AplicarTema();
        };

        _estado.Location = new Point(12, 208);
        _estado.Size = new Size(596, 62);
        _estado.Text = string.Empty;

        _barra.Location = new Point(12, 276);
        _barra.Size = new Size(596, 20);

        _parchear.Text = "Parchear al español";
        _parchear.Location = new Point(12, 312);
        _parchear.Size = new Size(180, 36);
        _parchear.Enabled = false;
        _parchear.Click += async (_, _) => await ParchearAsync();

        _quitar.Text = "Quitar parche";
        _quitar.Location = new Point(200, 312);
        _quitar.Size = new Size(150, 36);
        _quitar.Enabled = false;
        _quitar.Click += async (_, _) => await QuitarAsync();

        _acerca.Text = "Acerca de";
        _acerca.Location = new Point(514, 312);
        _acerca.Size = new Size(94, 36);
        _acerca.Click += (_, _) =>
        {
            using var dialogo = new AboutForm(_bundle?.Manifest.PatchVersion ?? "desarrollo", _paleta);
            dialogo.ShowDialog(this);
        };

        Controls.AddRange(
        [
            _etiquetaRuta, _rutaTexto, _examinar, _ficheros, _backup, _modoOscuro,
            _estado, _barra, _parchear, _quitar, _acerca,
        ]);
    }

    private static Icon? CargarIcono()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icono.ico");
        return stream is null ? null : new Icon(stream);
    }

    private void AplicarTema()
    {
        Theme.Apply(this, _paleta);
        _estado.ForeColor = _paleta.Muted;
        _ficheros.Invalidate();
        PintarEstado();
    }

    /// <summary>
    /// La última columna ocupa lo que queda: si no, la franja sobrante de la cabecera la pinta
    /// Windows con su propio color y en modo oscuro se ve un recuadro blanco.
    /// </summary>
    private void AjustarColumnas()
    {
        var restante = _ficheros.ClientSize.Width - _ficheros.Columns[0].Width;
        _ficheros.Columns[1].Width = Math.Max(120, restante);
    }

    private void DibujarCabecera(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var fondo = new SolidBrush(_paleta.Window))
        {
            e.Graphics.FillRectangle(fondo, e.Bounds);
        }

        using (var borde = new Pen(_paleta.Border))
        {
            e.Graphics.DrawLine(borde, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        TextRenderer.DrawText(
            e.Graphics, e.Header!.Text, Font,
            Rectangle.Inflate(e.Bounds, -6, 0), _paleta.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private void DibujarCelda(object? sender, DrawListViewSubItemEventArgs e)
    {
        using (var fondo = new SolidBrush(_paleta.Surface))
        {
            e.Graphics.FillRectangle(fondo, e.Bounds);
        }

        TextRenderer.DrawText(
            e.Graphics, e.SubItem!.Text, Font,
            Rectangle.Inflate(e.Bounds, -6, 0), e.SubItem.ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private async Task ExaminarAsync()
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = "Elige la carpeta del juego (la que contiene Atelier_Ryza_DX.exe)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (Directory.Exists(_rutaTexto.Text))
            dialogo.SelectedPath = _rutaTexto.Text;

        if (dialogo.ShowDialog(this) != DialogResult.OK) return;

        _rutaTexto.Text = PathDisplay.RealCase(dialogo.SelectedPath);
        await RefrescarAsync();
    }

    private async Task RefrescarAsync()
    {
        if (_bundle is null) return;

        var carpeta = _rutaTexto.Text;
        if (!Directory.Exists(carpeta)) return;

        var manifest = _bundle.Manifest;
        var progreso = new Progress<ProgressReport>(MostrarProgreso);

        using (new Operacion(this))
        {
            try
            {
                var resultado = await Task.Run(() =>
                {
                    var scan = new InstallScanner(manifest, _hasher)
                        .Scan(carpeta, progreso, CancellationToken.None);
                    var backup = new BackupService(manifest, _hasher)
                        .Inspect(carpeta, CancellationToken.None);
                    return (Scan: scan, Backup: backup);
                });

                _status = resultado.Scan;
                _backupStatus = resultado.Backup;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _status = null;
                _backupStatus = null;
                MostrarMensaje("No he podido leer la carpeta: " + ex.Message);
                return;
            }
        }

        PintarEstado();
    }

    private void PintarEstado()
    {
        _ficheros.Items.Clear();
        AjustarColumnas();
        if (_status is null) return;

        foreach (var file in _status.Files)
        {
            var texto = file.State switch
            {
                FileState.Original => "original (sin parchear)",
                FileState.Patched => "ya parcheado",
                FileState.Missing => "no encontrado",
                _ => "no reconocido",
            };

            var color = file.State switch
            {
                FileState.Original => _paleta.StateOriginal,
                FileState.Patched => _paleta.StatePatched,
                _ => _paleta.StateBad,
            };

            var item = new ListViewItem(file.Entry.RelativePath)
            {
                UseItemStyleForSubItems = false,
                ForeColor = _paleta.Text,
            };
            item.SubItems.Add(texto, color, _paleta.Surface, Font);
            _ficheros.Items.Add(item);
        }

        var mensaje = _status.Message;
        if (_backupStatus is { CanRestore: true })
            mensaje += "  Hay una copia de seguridad, así que puedes quitar el parche.";

        MostrarMensaje(mensaje);
        _parchear.Enabled = _status.CanPatch && !_ocupado;
        _quitar.Enabled = _backupStatus?.CanRestore == true && !_ocupado;
        _barra.Value = 0;
    }

    private async Task ParchearAsync()
    {
        if (_status is null || _bundle is null) return;

        var preflight = Preflight.Check(_status, _backup.Checked);
        if (!preflight.Ok)
        {
            if (preflight.Message.Contains("administrador", StringComparison.OrdinalIgnoreCase))
            {
                var respuesta = MessageBox.Show(
                    this,
                    preflight.Message + Environment.NewLine + Environment.NewLine +
                    "¿Quieres que lo reinicie como administrador?",
                    "Hacen falta permisos",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (respuesta == DialogResult.Yes && Elevation.TryRelaunchAsAdmin(_status.GameFolder))
                    Application.Exit();

                return;
            }

            MessageBox.Show(this, preflight.Message, "No puedo continuar",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_backup.Checked)
        {
            var respuesta = MessageBox.Show(
                this,
                "Vas a parchear sin copia de seguridad." + Environment.NewLine + Environment.NewLine +
                "Si más adelante quieres volver al inglés, este programa no podrá hacerlo: tendrías " +
                "que usar \"Verificar la integridad de los archivos del juego\" en Steam." +
                Environment.NewLine + Environment.NewLine + "¿Seguir de todos modos?",
                "Sin copia de seguridad",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (respuesta != DialogResult.Yes) return;
        }

        var status = _status;
        var opciones = new PatchOptions(_backup.Checked);

        await EjecutarOperacionAsync(
            (applier, progreso, ct) => applier.Install(status, opciones, progreso, ct),
            exito:
                "¡Listo! El juego ya está en español." + Environment.NewLine + Environment.NewLine +
                "IMPORTANTE: entra en el juego y pon el idioma en English. La traducción vive dentro " +
                "de los archivos del idioma inglés, así que con el juego en inglés verás el español.",
            tituloExito: "Parche aplicado");
    }

    private async Task QuitarAsync()
    {
        if (_status is null || _bundle is null) return;

        var respuesta = MessageBox.Show(
            this,
            "Voy a restaurar los archivos originales desde la carpeta backup. ¿Seguimos?",
            "Quitar el parche",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (respuesta != DialogResult.Yes) return;

        var carpeta = _status.GameFolder;

        await EjecutarOperacionAsync(
            (applier, progreso, ct) => applier.Uninstall(carpeta, progreso, ct),
            exito:
                "Parche quitado: el juego ha vuelto a su estado original." +
                Environment.NewLine + Environment.NewLine +
                "Si quieres recuperar espacio en disco, ya puedes borrar la carpeta \"backup\".",
            tituloExito: "Parche quitado");
    }

    private async Task EjecutarOperacionAsync(
        Action<PatchApplier, IProgress<ProgressReport>, CancellationToken> accion,
        string exito,
        string tituloExito)
    {
        var bundle = _bundle!;
        var progreso = new Progress<ProgressReport>(MostrarProgreso);

        using (new Operacion(this))
        {
            try
            {
                await Task.Run(() =>
                {
                    using var engine = new HPatchEngine();
                    var applier = new PatchApplier(
                        bundle, engine, new BackupService(bundle.Manifest, _hasher), _hasher);
                    accion(applier, progreso, CancellationToken.None);
                });

                MessageBox.Show(this, exito, tituloExito, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Algo ha salido mal",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        await RefrescarAsync();
    }

    private void MostrarProgreso(ProgressReport report)
    {
        _barra.Value = report.Fraction;
        _estado.Text = report.Message;
    }

    private void MostrarMensaje(string mensaje) => _estado.Text = mensaje;

    /// <summary>Bloquea la interfaz mientras dura una operación larga.</summary>
    private sealed class Operacion : IDisposable
    {
        private readonly MainForm _form;

        public Operacion(MainForm form)
        {
            _form = form;
            _form._ocupado = true;
            _form._examinar.Enabled = false;
            _form._parchear.Enabled = false;
            _form._quitar.Enabled = false;
            _form._backup.Enabled = false;
            _form.Cursor = Cursors.WaitCursor;
        }

        public void Dispose()
        {
            _form._ocupado = false;
            _form._examinar.Enabled = true;
            _form._backup.Enabled = true;
            _form.Cursor = Cursors.Default;
        }
    }
}
