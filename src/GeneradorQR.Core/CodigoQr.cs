using System.Globalization;
using QRCoder;

namespace GeneradorQR.Core;

/// <summary>How much of the code can be damaged or covered and still read: more redundancy means denser codes.</summary>
public enum NivelCorreccion { Bajo, Medio, Alto, Maximo }

/// <summary>An encoded QR, ready to be drawn as pixels, PNG or SVG in any colors.</summary>
public sealed class CodigoQr
{
    private readonly QRCodeData _datos;

    private CodigoQr(string contenido, QRCodeData datos)
    {
        Contenido = contenido;
        _datos = datos;
    }

    public string Contenido { get; }

    /// <summary>Side in modules, counting the 4-module white margin on each side that readers need.</summary>
    public int Modulos => _datos.ModuleMatrix.Count;

    public static CodigoQr Crear(string contenido, NivelCorreccion nivel = NivelCorreccion.Medio)
    {
        if (string.IsNullOrEmpty(contenido)) throw new ContenidoInvalidoException("No hay nada para codificar.");
        var ecc = nivel switch
        {
            NivelCorreccion.Bajo => QRCodeGenerator.ECCLevel.L,
            NivelCorreccion.Alto => QRCodeGenerator.ECCLevel.Q,
            NivelCorreccion.Maximo => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.M,
        };
        try
        {
            // UTF-8 so accents and ñ read right on every phone.
            var datos = QRCodeGenerator.GenerateQrCode(contenido, ecc, forceUtf8: true);
            return new CodigoQr(contenido, datos);
        }
        catch (QRCoder.Exceptions.DataTooLongException)
        {
            throw new ContenidoInvalidoException("Es demasiado texto para un QR. Acortalo o bajá la corrección de errores.");
        }
    }

    /// <summary>Whole pixels per module so the image is about <paramref name="ladoPixeles"/> wide.</summary>
    public int PixelesPorModulo(int ladoPixeles) => Math.Max(1, (int)Math.Round(ladoPixeles / (double)Modulos));

    // Colors: colorClaro null means transparent background (only the dark modules are painted).

    /// <summary>Raw BGRA32 pixels (what WPF and the reader take), each module a square of whole pixels.</summary>
    public (byte[] Bgra, int Lado) Pixeles(int pixelesPorModulo, string colorOscuro = "#000000", string? colorClaro = "#FFFFFF")
    {
        ValidarColores(colorOscuro, colorClaro);
        var (ro, go, bo) = LeerColor(colorOscuro)!.Value;
        // Transparent pixels keep white color values, so tools that ignore alpha still see a light background.
        var (rc, gc, bc) = colorClaro is null ? ((byte)255, (byte)255, (byte)255) : LeerColor(colorClaro)!.Value;
        var alfaClaro = colorClaro is null ? (byte)0 : (byte)255;
        var lado = Modulos * pixelesPorModulo;
        var bgra = new byte[lado * lado * 4];
        for (var y = 0; y < lado; y++)
        {
            var fila = _datos.ModuleMatrix[y / pixelesPorModulo];
            for (var x = 0; x < lado; x++)
            {
                var i = (y * lado + x) * 4;
                var oscuro = fila[x / pixelesPorModulo];
                bgra[i] = oscuro ? bo : bc;
                bgra[i + 1] = oscuro ? go : gc;
                bgra[i + 2] = oscuro ? ro : rc;
                bgra[i + 3] = oscuro ? (byte)255 : alfaClaro;
            }
        }
        return (bgra, lado);
    }

    /// <summary>PNG about <paramref name="ladoPixeles"/> wide, 32-bit RGBA (see <see cref="Core.Png"/>).</summary>
    public byte[] Png(int ladoPixeles, string colorOscuro = "#000000", string? colorClaro = "#FFFFFF")
    {
        var (bgra, lado) = Pixeles(PixelesPorModulo(ladoPixeles), colorOscuro, colorClaro);
        return Core.Png.DesdeBgra(bgra, lado, lado);
    }

    /// <summary>
    /// Vector image, sharp at any print size: one path with a square per dark module (rows merged into runs), over a
    /// background rectangle unless it's transparent. 10 units per module.
    /// </summary>
    public string Svg(string colorOscuro = "#000000", string? colorClaro = "#FFFFFF")
    {
        ValidarColores(colorOscuro, colorClaro);
        var n = Modulos;
        var ruta = new System.Text.StringBuilder();
        for (var y = 0; y < n; y++)
        {
            var fila = _datos.ModuleMatrix[y];
            for (var x = 0; x < n; x++)
            {
                if (!fila[x]) continue;
                var inicio = x;
                while (x + 1 < n && fila[x + 1]) x++;
                ruta.Append(CultureInfo.InvariantCulture, $"M{inicio * 10} {y * 10}h{(x - inicio + 1) * 10}v10h-{(x - inicio + 1) * 10}z");
            }
        }
        var lado = n * 10;
        var svg = new System.Text.StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {lado} {lado}\" width=\"{lado}\" height=\"{lado}\" shape-rendering=\"crispEdges\">");
        if (colorClaro is not null) svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{lado}\" height=\"{lado}\" fill=\"{Normalizar(colorClaro)}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"{Normalizar(colorOscuro)}\" d=\"{ruta}\"/></svg>");
        return svg.ToString();
    }

    /// <summary>Parses "#RRGGBB" or "RRGGBB"; null when it isn't a color.</summary>
    public static (byte R, byte G, byte B)? LeerColor(string? hex)
    {
        var texto = (hex ?? "").Trim().TrimStart('#');
        if (texto.Length != 6 || !int.TryParse(texto, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var valor))
            return null;
        return ((byte)(valor >> 16), (byte)(valor >> 8), (byte)valor);
    }

    /// <summary>
    /// Readers need dark modules on a clearly lighter background; inverted or low-contrast codes often fail to scan.
    /// </summary>
    /// <remarks>
    /// With a transparent background (<paramref name="colorClaro"/> null) the code will sit on whatever is behind it,
    /// usually paper or a light design, so the QR color is checked against white.
    /// </remarks>
    public static void ValidarColores(string colorOscuro, string? colorClaro)
    {
        var oscuro = LeerColor(colorOscuro) ?? throw new ContenidoInvalidoException("El color del QR no es válido (ejemplo: #000000).");
        var claro = colorClaro is null ? ((byte)255, (byte)255, (byte)255)
            : LeerColor(colorClaro) ?? throw new ContenidoInvalidoException("El color de fondo no es válido (ejemplo: #FFFFFF).");
        var lo = Luminancia(oscuro);
        var lc = Luminancia(claro);
        if (lo >= lc) throw new ContenidoInvalidoException("El QR tiene que ser más oscuro que el fondo, si no los celulares no lo leen.");
        if ((lc + 0.05) / (lo + 0.05) < 3)
            throw new ContenidoInvalidoException("Hay poco contraste entre el QR y el fondo: usá colores más distintos.");
    }

    // WCAG relative luminance.
    private static double Luminancia((byte R, byte G, byte B) c)
    {
        static double Canal(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Canal(c.R) + 0.7152 * Canal(c.G) + 0.0722 * Canal(c.B);
    }

    private static string Normalizar(string hex)
    {
        var (r, g, b) = LeerColor(hex)!.Value;
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
