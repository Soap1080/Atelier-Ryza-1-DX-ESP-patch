using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RyzaEsPatcher.App;

/// <summary>
/// Casilla dibujada a mano. La de WinForms, incluso con <see cref="FlatStyle.Flat"/>, pinta el
/// recuadro en claro y la marca en oscuro, así que en modo oscuro el tick apenas se ve.
/// </summary>
public sealed class ThemedCheckBox : CheckBox
{
    private const int BoxSize = 16;
    private const int Gap = 8;

    public ThemedCheckBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint,
            true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BoxBorderColor { get; set; } = Color.Gray;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BoxFillColor { get; set; } = Color.White;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CheckedFillColor { get; set; } = Color.SteelBlue;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CheckMarkColor { get; set; } = Color.White;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DisabledTextColor { get; set; } = Color.Gray;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var texto = TextRenderer.MeasureText(Text, Font);
        return new Size(BoxSize + Gap + texto.Width + 6, Math.Max(BoxSize, texto.Height) + 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        using (var fondo = new SolidBrush(BackColor))
        {
            g.FillRectangle(fondo, ClientRectangle);
        }

        var caja = new Rectangle(1, (Height - BoxSize) / 2, BoxSize, BoxSize);
        var activa = Enabled;

        using (var relleno = new SolidBrush(Checked ? CheckedFillColor : BoxFillColor))
        {
            g.FillRectangle(relleno, caja);
        }

        using (var borde = new Pen(Checked ? CheckedFillColor : BoxBorderColor))
        {
            g.DrawRectangle(borde, caja);
        }

        if (Checked)
        {
            var suavizado = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var marca = new Pen(CheckMarkColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLines(marca,
                [
                    new PointF(caja.Left + 3.5f, caja.Top + 8.0f),
                    new PointF(caja.Left + 6.5f, caja.Top + 11.5f),
                    new PointF(caja.Left + 12.5f, caja.Top + 4.5f),
                ]);
            }

            g.SmoothingMode = suavizado;
        }

        var zonaTexto = new Rectangle(caja.Right + Gap, 0, Width - caja.Right - Gap, Height);
        TextRenderer.DrawText(
            g, Text, Font, zonaTexto, activa ? ForeColor : DisabledTextColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }
}
