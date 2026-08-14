using System.ComponentModel;

namespace RyzaEsPatcher.App;

/// <summary>
/// Barra de progreso propia. La del sistema ignora los colores que se le asignan cuando los
/// estilos visuales están activos, así que en modo oscuro quedaría blanca.
/// </summary>
public sealed class ProgressPanel : Control
{
    private double _value;

    public ProgressPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint,
            true);
    }

    /// <summary>Avance entre 0 y 1.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Value
    {
        get => _value;
        set
        {
            var nuevo = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(nuevo - _value) < 0.0005) return;

            _value = nuevo;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = Color.SteelBlue;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.Gray;

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var fondo = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(fondo, ClientRectangle);
        }

        var ancho = (int)Math.Round((Width - 2) * _value);
        if (ancho > 0)
        {
            using var relleno = new SolidBrush(FillColor);
            e.Graphics.FillRectangle(relleno, 1, 1, ancho, Height - 2);
        }

        using var borde = new Pen(BorderColor);
        e.Graphics.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
    }
}
