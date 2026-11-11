using ZXing;
using ZXing.Common;

namespace GeneradorQR.Core;

/// <summary>Reads the QR codes in an image (a saved QR, a photo, a screenshot of a flyer…).</summary>
public static class LectorQr
{
    /// <summary>The text of every QR found in the BGRA32 pixels, in reading order; empty when there is none.</summary>
    public static IReadOnlyList<string> Leer(byte[] bgra, int ancho, int alto)
    {
        var lector = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true, // light-on-dark codes
                PossibleFormats = [BarcodeFormat.QR_CODE],
                CharacterSet = "UTF-8",
            },
        };
        var fuente = new RGBLuminanceSource(bgra, ancho, alto, RGBLuminanceSource.BitmapFormat.BGRA32);

        var resultados = lector.DecodeMultiple(fuente);
        if (resultados is null || resultados.Length == 0)
        {
            var uno = lector.Decode(fuente);
            return uno is null ? [] : [uno.Text];
        }
        return resultados
            .OrderBy(r => r.ResultPoints.Length == 0 ? 0 : r.ResultPoints.Min(p => p.Y))
            .ThenBy(r => r.ResultPoints.Length == 0 ? 0 : r.ResultPoints.Min(p => p.X))
            .Select(r => r.Text)
            .Distinct()
            .ToList();
    }
}
