using System.Buffers.Binary;
using System.IO.Compression;

namespace GeneradorQR.Core;

/// <summary>
/// Minimal PNG writer: always 8-bit RGBA (color type 6), with a real alpha channel on every pixel. Palette PNGs with a
/// tRNS chunk (what QRCoder writes) look transparent in Windows and browsers but CorelDRAW and other design tools
/// ignore tRNS and show the background as white; full RGBA works everywhere.
/// </summary>
internal static class Png
{
    private static readonly uint[] TablaCrc = CrearTablaCrc();

    public static byte[] DesdeBgra(byte[] bgra, int ancho, int alto, int ppp = 300)
    {
        // Rows: filter byte (0 = none) + RGBA pixels.
        var crudo = new byte[alto * (1 + ancho * 4)];
        for (int y = 0, d = 0; y < alto; y++)
        {
            crudo[d++] = 0;
            for (int x = 0, s = y * ancho * 4; x < ancho; x++, s += 4)
            {
                crudo[d++] = bgra[s + 2];
                crudo[d++] = bgra[s + 1];
                crudo[d++] = bgra[s];
                crudo[d++] = bgra[s + 3];
            }
        }

        using var comprimido = new MemoryStream();
        using (var zlib = new ZLibStream(comprimido, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(crudo);

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, ancho);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), alto);
        ihdr[8] = 8; // bits per channel
        ihdr[9] = 6; // RGBA
        Bloque(png, "IHDR", ihdr);

        // Physical size, so design tools import it at print size (300 dpi) instead of a huge 96 dpi image.
        var phys = new byte[9];
        var ppm = (int)Math.Round(ppp / 0.0254);
        BinaryPrimitives.WriteInt32BigEndian(phys, ppm);
        BinaryPrimitives.WriteInt32BigEndian(phys.AsSpan(4), ppm);
        phys[8] = 1; // unit: meter
        Bloque(png, "pHYs", phys);

        Bloque(png, "IDAT", comprimido.ToArray());
        Bloque(png, "IEND", []);
        return png.ToArray();
    }

    private static void Bloque(Stream salida, string tipo, byte[] datos)
    {
        Span<byte> numero = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(numero, datos.Length);
        salida.Write(numero);
        var tipoBytes = System.Text.Encoding.ASCII.GetBytes(tipo);
        salida.Write(tipoBytes);
        salida.Write(datos);
        var crc = Crc(Crc(0xFFFFFFFF, tipoBytes), datos) ^ 0xFFFFFFFF;
        BinaryPrimitives.WriteUInt32BigEndian(numero, crc);
        salida.Write(numero);
    }

    private static uint Crc(uint crc, byte[] datos)
    {
        foreach (var b in datos) crc = TablaCrc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] CrearTablaCrc()
    {
        var tabla = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            tabla[n] = c;
        }
        return tabla;
    }
}
