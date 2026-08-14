using System.Diagnostics;
using System.Reflection;

namespace RyzaEsPatcher.App;

public sealed class AboutForm : Form
{
    public const string DonationUrl = "https://www.paypal.com/donate/?hosted_button_id=LQDFW67ZG2DKQ";

    public AboutForm(string patchVersion, Palette palette)
    {
        Text = "Acerca de";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 448);
        Font = new Font("Segoe UI", 9F);

        var texto = new TextBox
        {
            Location = new Point(12, 12),
            Size = new Size(536, 316),
            Multiline = true,
            ReadOnly = true,
            TabStop = false,
            ScrollBars = ScrollBars.Vertical,
            Text = string.Join(Environment.NewLine,
            [
                "Parche al español de Atelier Ryza: Ever Darkness & the Secret Hideout DX",
                $"Versión del parche: {patchVersion}",
                "Versión del juego compatible: 1.0.0.2",
                string.Empty,
                "Este programa NO contiene archivos del juego. Aplica las diferencias sobre los",
                "archivos de tu propia copia, que debe ser legal.",
                string.Empty,
                "Proyecto de fans, sin ánimo de lucro. No está afiliado ni respaldado por Koei Tecmo",
                "ni por Gust. El juego y sus contenidos pertenecen a sus respectivos propietarios.",
                string.Empty,
                "Créditos",
                "  · Traducción al español y parche: proyecto propio.",
                "  · Motor de parches diferenciales: HDiffPatch, de housisong (licencia MIT).",
                "    https://github.com/sisong/HDiffPatch",
                "  · Las imágenes de menús ya traducidas se obtuvieron de la traducción al español",
                "    de Atelier Ryza (A21). ¡Gracias!",
                "    https://steamcommunity.com/sharedfiles/filedetails/?id=2892108696",
                string.Empty,
                "Si te ha servido, puedes apoyar el proyecto con una donación.",
            ]),
        };

        var donar = new PictureBox
        {
            Location = new Point(12, 340),
            Size = new Size(240, 60),
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            Image = CargarImagenDonacion(),
        };
        donar.Click += (_, _) => AbrirEnlace(DonationUrl);

        var cerrar = new Button
        {
            Text = "Cerrar",
            Location = new Point(458, 406),
            Size = new Size(90, 30),
            DialogResult = DialogResult.OK,
        };

        AcceptButton = cerrar;
        Controls.AddRange([texto, donar, cerrar]);

        Load += (_, _) => Theme.Apply(this, palette);
    }

    private static Image? CargarImagenDonacion()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("donar-con-paypal.png");
        return stream is null ? null : Image.FromStream(stream);
    }

    private static void AbrirEnlace(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MessageBox.Show("No he podido abrir el navegador. El enlace es:" + Environment.NewLine + url);
        }
    }
}
