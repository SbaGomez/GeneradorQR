using System.Text;
using GeneradorQR.Core;

namespace GeneradorQR.Core.Tests;

public class CodigoQrTests
{
    [Theory]
    [InlineData("https://wa.me/5491123456789?text=Hola%2C%20%C2%BFprecio%3F")]
    [InlineData("WIFI:T:WPA;S:Casa\\;Pepe;P:clave1234;;")]
    [InlineData("Año, mañana, acción: ñandú")]
    public void Lo_generado_se_lee_igual(string contenido)
    {
        var qr = CodigoQr.Crear(contenido);
        var (bgra, lado) = qr.Pixeles(qr.PixelesPorModulo(400));
        Assert.Equal([contenido], LectorQr.Leer(bgra, lado, lado));
    }

    [Fact]
    public void Se_lee_con_colores()
    {
        var qr = CodigoQr.Crear("https://mipagina.com", NivelCorreccion.Maximo);
        var (bgra, lado) = qr.Pixeles(8, "#1565C0", "#FFF8E1");
        Assert.Equal(["https://mipagina.com"], LectorQr.Leer(bgra, lado, lado));
    }

    [Fact]
    public void Fondo_transparente_solo_pinta_los_modulos_y_se_lee()
    {
        var qr = CodigoQr.Crear("https://mipagina.com");
        var (bgra, lado) = qr.Pixeles(6, "#000000", null);
        var alfas = Enumerable.Range(0, lado * lado).Select(i => bgra[i * 4 + 3]).ToHashSet();
        Assert.Equal(new byte[] { 0, 255 }, alfas.Order().ToArray());
        Assert.Equal(0, bgra[3]); // the quiet zone corner is see-through
        Assert.Equal(["https://mipagina.com"], LectorQr.Leer(bgra, lado, lado));
    }

    [Fact]
    public void Svg_transparente_no_tiene_rectangulo_de_fondo()
    {
        var qr = CodigoQr.Crear("hola");
        Assert.DoesNotContain("<rect", qr.Svg("#000000", null));
        Assert.Contains("<rect", qr.Svg("#000000", "#FFFFFF"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Png_es_rgba_con_alfa_real(bool transparente)
    {
        // Full RGBA (color type 6), not a palette + tRNS, which CorelDRAW shows with a white background.
        var png = CodigoQr.Crear("hola").Png(256, "#000000", transparente ? null : "#FFFFFF");
        var (ancho, alto, tipoColor, rgba) = LeerPng(png);
        Assert.Equal((8, 6), (png[24], tipoColor));
        Assert.Equal(ancho, alto);

        var alfas = Enumerable.Range(0, ancho * alto).Select(i => rgba[i * 4 + 3]).ToHashSet();
        Assert.Equal(transparente ? new byte[] { 0, 255 } : new byte[] { 255 }, alfas.Order().ToArray());
        Assert.Equal((byte)(transparente ? 0 : 255), rgba[3]); // margin corner
    }

    [Fact]
    public void Png_se_lee_igual_que_lo_generado()
    {
        var (ancho, alto, _, rgba) = LeerPng(CodigoQr.Crear("https://wa.me/5491123456789").Png(400, "#0B6E4F", null));
        var bgra = new byte[rgba.Length];
        for (var i = 0; i < rgba.Length; i += 4) (bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]) = (rgba[i + 2], rgba[i + 1], rgba[i], rgba[i + 3]);
        Assert.Equal(["https://wa.me/5491123456789"], LectorQr.Leer(bgra, ancho, alto));
    }

    // Decodes the PNGs this app writes (8-bit RGBA, filter 0), checking every chunk's CRC on the way.
    private static (int Ancho, int Alto, byte TipoColor, byte[] Rgba) LeerPng(byte[] png)
    {
        int ancho = 0, alto = 0; byte tipo = 0;
        using var idat = new MemoryStream();
        for (var i = 8; i < png.Length;)
        {
            var n = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(i));
            var nombre = Encoding.ASCII.GetString(png, i + 4, 4);
            var crc = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(i + 8 + n));
            Assert.Equal(System.IO.Hashing.Crc32.HashToUInt32(png.AsSpan(i + 4, 4 + n)), crc);
            if (nombre == "IHDR")
            {
                ancho = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(i + 8));
                alto = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(i + 12));
                tipo = png[i + 17];
            }
            if (nombre == "IDAT") idat.Write(png, i + 8, n);
            i += 12 + n;
        }
        idat.Position = 0;
        using var zlib = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
        using var crudo = new MemoryStream();
        zlib.CopyTo(crudo);
        var filas = crudo.ToArray();
        var rgba = new byte[ancho * alto * 4];
        for (var y = 0; y < alto; y++)
        {
            Assert.Equal(0, filas[y * (1 + ancho * 4)]);
            Array.Copy(filas, y * (1 + ancho * 4) + 1, rgba, y * ancho * 4, ancho * 4);
        }
        return (ancho, alto, tipo, rgba);
    }

    [Fact]
    public void Transparente_igual_exige_un_color_oscuro() =>
        Assert.Throws<ContenidoInvalidoException>(() => CodigoQr.ValidarColores("#DDDDDD", null));

    [Fact]
    public void Una_imagen_sin_qr_no_devuelve_nada()
    {
        var blanco = Enumerable.Repeat((byte)255, 200 * 200 * 4).ToArray();
        Assert.Empty(LectorQr.Leer(blanco, 200, 200));
    }

    [Fact]
    public void Png_y_svg_tienen_el_formato_correcto()
    {
        var qr = CodigoQr.Crear("hola");
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], qr.Png(512)[..4]);
        var svg = qr.Svg("#112233");
        Assert.Contains("<svg", svg);
        Assert.Contains("#112233", svg);
    }

    [Theory]
    [InlineData("#FFFFFF", "#000000")] // inverted
    [InlineData("#BBBBBB", "#FFFFFF")] // low contrast
    [InlineData("rojo", "#FFFFFF")]
    public void Rechaza_colores_que_no_se_leen(string oscuro, string claro) =>
        Assert.Throws<ContenidoInvalidoException>(() => CodigoQr.ValidarColores(oscuro, claro));

    [Fact]
    public void Demasiado_texto() =>
        Assert.Throws<ContenidoInvalidoException>(() => CodigoQr.Crear(new string('x', 5000)));
}

public class AnalizadorTests
{
    [Theory]
    [InlineData("https://gymup.sebastiangomez.com.ar/", Veredicto.Directo, "Página web")]
    [InlineData("https://bit.ly/3abc", Veredicto.Intermediario, "Enlace con intermediario")]
    [InlineData("https://qrco.de/bfXyz", Veredicto.Intermediario, "Enlace con intermediario")]
    [InlineData("https://app.me-qr.com/l/abc", Veredicto.Intermediario, "Enlace con intermediario")]
    [InlineData("https://tracker.com/go?url=https://destino.com", Veredicto.Intermediario, "Redirección")]
    [InlineData("https://wa.me/5491123456789", Veredicto.Directo, "WhatsApp")]
    [InlineData("WIFI:T:WPA;S:Casa;P:clave1234;;", Veredicto.SinEnlace, "Red Wi-Fi")]
    [InlineData("BEGIN:VCARD\r\nVERSION:3.0\r\nEND:VCARD", Veredicto.SinEnlace, "Contacto")]
    [InlineData("geo:-34.6,-58.38", Veredicto.Directo, "Ubicación")]
    [InlineData("hola", Veredicto.SinEnlace, "Texto")]
    public void Clasifica(string contenido, Veredicto veredicto, string tipo)
    {
        var a = Analizador.Analizar(contenido);
        Assert.Equal((veredicto, tipo), (a.Veredicto, a.Tipo));
    }

    [Fact]
    public void Muestra_el_nombre_de_la_red() =>
        Assert.Equal("Red Wi-Fi \"Casa;Pepe\"", Analizador.Analizar(@"WIFI:T:WPA;S:Casa\;Pepe;P:x;;").Titulo);
}
