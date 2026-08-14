using System.Runtime.InteropServices;

namespace RyzaEsPatcher.App;

/// <summary>Colores de un tema. Los estados de archivo tienen color propio.</summary>
public sealed record Palette(
    Color Window,
    Color Surface,
    Color Text,
    Color Muted,
    Color Border,
    Color Accent,
    Color ButtonFace,
    Color ButtonHover,
    Color DisabledText,
    Color StateOriginal,
    Color StatePatched,
    Color StateBad);

/// <summary>
/// WinForms no tiene modo oscuro real, así que se pintan los colores a mano y se pide a Windows
/// la barra de título oscura por DWM.
/// </summary>
public static class Theme
{
    public static readonly Palette Dark = new(
        Window: Color.FromArgb(32, 32, 32),
        Surface: Color.FromArgb(45, 45, 45),
        Text: Color.FromArgb(232, 232, 232),
        Muted: Color.FromArgb(168, 168, 168),
        Border: Color.FromArgb(70, 70, 70),
        Accent: Color.FromArgb(76, 154, 255),
        ButtonFace: Color.FromArgb(58, 58, 58),
        ButtonHover: Color.FromArgb(75, 75, 75),
        DisabledText: Color.FromArgb(120, 120, 120),
        StateOriginal: Color.FromArgb(122, 214, 122),
        StatePatched: Color.FromArgb(118, 178, 255),
        StateBad: Color.FromArgb(255, 123, 114));

    public static readonly Palette Light = new(
        Window: Color.FromArgb(243, 243, 243),
        Surface: Color.White,
        Text: Color.FromArgb(26, 26, 26),
        Muted: Color.FromArgb(85, 85, 85),
        Border: Color.FromArgb(200, 200, 200),
        Accent: Color.FromArgb(10, 110, 209),
        ButtonFace: Color.FromArgb(230, 230, 230),
        ButtonHover: Color.FromArgb(214, 214, 214),
        DisabledText: Color.FromArgb(150, 150, 150),
        StateOriginal: Color.FromArgb(0, 110, 0),
        StatePatched: Color.FromArgb(0, 70, 170),
        StateBad: Color.FromArgb(170, 0, 0));

    public static Palette For(bool dark) => dark ? Dark : Light;

    /// <summary>Paleta activa. La usa el manejador de botones deshabilitados.</summary>
    private static Palette _current = Light;

    public static void Apply(Form form, Palette palette)
    {
        _current = palette;
        form.BackColor = palette.Window;
        form.ForeColor = palette.Text;
        ApplyToChildren(form, palette);
        UseDarkTitleBar(form, palette == Dark);
        form.Invalidate(invalidateChildren: true);
    }

    private static void ApplyToChildren(Control parent, Palette palette)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = palette.ButtonFace;
                    button.FlatAppearance.BorderColor = palette.Border;
                    button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
                    // Un botón plano deshabilitado casi no se distingue, así que se atenúa a mano.
                    button.EnabledChanged -= AtenuarBotonDeshabilitado;
                    button.EnabledChanged += AtenuarBotonDeshabilitado;
                    AtenuarBotonDeshabilitado(button, EventArgs.Empty);
                    break;

                case TextBox textBox:
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.BackColor = palette.Surface;
                    textBox.ForeColor = palette.Text;
                    break;

                case ListView listView:
                    listView.BackColor = palette.Surface;
                    listView.ForeColor = palette.Text;
                    break;

                case ThemedCheckBox checkBox:
                    checkBox.BackColor = palette.Window;
                    checkBox.ForeColor = palette.Text;
                    checkBox.BoxBorderColor = palette.Muted;
                    checkBox.BoxFillColor = palette.Surface;
                    checkBox.CheckedFillColor = palette.Accent;
                    checkBox.CheckMarkColor = Color.White;
                    checkBox.DisabledTextColor = palette.DisabledText;
                    checkBox.Invalidate();
                    break;

                case ProgressPanel progress:
                    progress.BackColor = palette.Surface;
                    progress.FillColor = palette.Accent;
                    progress.BorderColor = palette.Border;
                    break;

                case PictureBox:
                    break;

                default:
                    control.BackColor = palette.Window;
                    control.ForeColor = palette.Text;
                    break;
            }

            if (control.HasChildren) ApplyToChildren(control, palette);
        }
    }

    private static void AtenuarBotonDeshabilitado(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;

        button.ForeColor = button.Enabled ? _current.Text : _current.DisabledText;
    }

    /// <summary>
    /// Modo de color de la aplicación (barra de título, bordes y diálogos del sistema).
    /// Hay que llamarlo antes de crear ninguna ventana.
    /// </summary>
    public static void SetSystemColorMode(bool dark)
    {
#pragma warning disable WFO5001 // API experimental de .NET 9
        Application.SetColorMode(dark ? SystemColorMode.Dark : SystemColorMode.Classic);
#pragma warning restore WFO5001
    }

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Cambia el color de la barra de título de una ventana ya creada, para que el interruptor
    /// de modo oscuro tenga efecto sin reiniciar.
    /// </summary>
    public static void UseDarkTitleBar(Form form, bool dark)
    {
        if (!form.IsHandleCreated) return;

        var value = dark ? 1 : 0;
        if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
    }
}
