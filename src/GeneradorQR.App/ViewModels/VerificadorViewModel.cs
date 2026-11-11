using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeneradorQR.Core;
using Microsoft.Win32;

namespace GeneradorQR.App.ViewModels;

/// <summary>One QR found in the checked image.</summary>
public sealed record QrLeido(string Contenido, AnalisisQr Analisis);

/// <summary>
/// Reads the QR in an image (opened, dropped or pasted) and says what it holds and whether it goes straight to its
/// destination or through a shortener/redirect.
/// </summary>
public sealed partial class VerificadorViewModel : ObservableObject, ISeccion
{
    // Big photos are scaled down before reading: plenty for a QR and much faster.
    private const int LadoMaximo = 1800;

    public string Nombre => "Verificar QR";
    public string Icono => "";

    [ObservableProperty] private ImageSource? _imagen;
    [ObservableProperty] private bool _leyendo;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _estado;

    public ObservableCollection<QrLeido> Resultados { get; } = [];

    [RelayCommand]
    private async Task AbrirArchivo()
    {
        var dialogo = new OpenFileDialog
        {
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|Todos los archivos|*.*",
            Title = "Elegí la imagen del QR",
        };
        if (dialogo.ShowDialog() == true) await CargarArchivo(dialogo.FileName);
    }

    [RelayCommand]
    private async Task Pegar()
    {
        if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } imagen) await Cargar(imagen);
        else if (Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList() is { Count: > 0 } archivos) await CargarArchivo(archivos[0]!);
        else MostrarError("No hay ninguna imagen copiada. Copiá una (por ejemplo con Win+Shift+S) y volvé a pegar.");
    }

    public async Task CargarArchivo(string ruta)
    {
        BitmapSource imagen;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // don't keep the file locked
            bitmap.UriSource = new Uri(Path.GetFullPath(ruta));
            bitmap.EndInit();
            imagen = bitmap;
        }
        catch (Exception)
        {
            MostrarError("No se pudo abrir el archivo como imagen.");
            return;
        }
        await Cargar(imagen);
    }

    public async Task Cargar(BitmapSource imagen)
    {
        Imagen = imagen;
        Resultados.Clear();
        Error = null;
        Estado = null;
        Leyendo = true;
        try
        {
            var (bgra, ancho, alto) = Pixeles(imagen);
            var textos = await Task.Run(() => LectorQr.Leer(bgra, ancho, alto));
            foreach (var texto in textos) Resultados.Add(new QrLeido(texto, Analizador.Analizar(texto)));
            if (Resultados.Count == 0)
                Error = "No se encontró ningún QR en la imagen. Probá con una imagen más nítida que muestre el código completo.";
        }
        finally
        {
            Leyendo = false;
        }
    }

    [RelayCommand]
    private void Copiar(QrLeido qr)
    {
        Clipboard.SetText(qr.Contenido);
        Estado = "Contenido copiado.";
    }

    [RelayCommand]
    private void AbrirEnlace(QrLeido qr)
    {
        if (qr.Analisis.Enlace is { } enlace) Abrir.Enlace(enlace);
    }

    private void MostrarError(string mensaje)
    {
        Imagen = null;
        Resultados.Clear();
        Estado = null;
        Error = mensaje;
    }

    private static (byte[] Bgra, int Ancho, int Alto) Pixeles(BitmapSource imagen)
    {
        var escala = Math.Min(1.0, LadoMaximo / (double)Math.Max(imagen.PixelWidth, imagen.PixelHeight));
        BitmapSource fuente = escala < 1 ? new TransformedBitmap(imagen, new ScaleTransform(escala, escala)) : imagen;
        var bgra32 = new FormatConvertedBitmap(fuente, PixelFormats.Bgra32, null, 0);
        int ancho = bgra32.PixelWidth, alto = bgra32.PixelHeight;
        var pixeles = new byte[ancho * alto * 4];
        bgra32.CopyPixels(pixeles, ancho * 4, 0);

        // Transparent PNGs: put them on white, or a black-on-transparent code reads as all black.
        for (var i = 0; i < pixeles.Length; i += 4)
        {
            var a = pixeles[i + 3];
            if (a == 255) continue;
            for (var c = 0; c < 3; c++) pixeles[i + c] = (byte)((pixeles[i + c] * a + 255 * (255 - a)) / 255);
            pixeles[i + 3] = 255;
        }
        return (pixeles, ancho, alto);
    }
}
