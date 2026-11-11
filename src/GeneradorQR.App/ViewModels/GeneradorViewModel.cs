using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeneradorQR.Core;
using Microsoft.Win32;

namespace GeneradorQR.App.ViewModels;

/// <summary>The form of the chosen kind of QR, its look, and the live preview with save/copy.</summary>
public sealed partial class GeneradorViewModel : ObservableObject
{
    public static IReadOnlyList<string> ColoresSugeridos { get; } =
        ["#000000", "#1F2328", "#1565C0", "#0B6E4F", "#128C7E", "#6A1B9A", "#B71C1C", "#E65100"];

    public static IReadOnlyList<int> Tamanos { get; } = [512, 1024, 2048, 4096];

    [ObservableProperty] private TipoQr _tipo;
    [ObservableProperty] private string _colorQr = "#000000";
    [ObservableProperty] private string _colorFondo = "#FFFFFF";
    /// <summary>Only the QR's modules are painted; the background is see-through (PNG and SVG).</summary>
    [ObservableProperty] private bool _fondoTransparente;
    [ObservableProperty] private int _nivel = (int)NivelCorreccion.Medio;
    [ObservableProperty] private int _tamano = 1024;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayQr))]
    [NotifyCanExecuteChangedFor(nameof(GuardarPngCommand), nameof(GuardarSvgCommand), nameof(CopiarCommand), nameof(ProbarCommand))]
    private CodigoQr? _codigo;

    [ObservableProperty] private ImageSource? _vistaPrevia;
    [ObservableProperty] private string? _aviso;
    [ObservableProperty] private string? _estado;

    public GeneradorViewModel(TipoQr tipo)
    {
        _tipo = tipo;
        tipo.PropertyChanged += AlCambiarFormulario;
        Regenerar();
    }

    public bool HayQr => Codigo is not null;

    partial void OnTipoChanged(TipoQr? oldValue, TipoQr newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= AlCambiarFormulario;
        newValue.PropertyChanged += AlCambiarFormulario;
        Regenerar();
    }

    partial void OnColorQrChanged(string value) => Regenerar();
    partial void OnColorFondoChanged(string value) => Regenerar();
    partial void OnFondoTransparenteChanged(bool value) => Regenerar();

    private string? ColorClaro => FondoTransparente ? null : ColorFondo;
    partial void OnNivelChanged(int value) => Regenerar();

    private void AlCambiarFormulario(object? sender, PropertyChangedEventArgs e) => Regenerar();

    private void Regenerar()
    {
        Estado = null;
        try
        {
            var codigo = CodigoQr.Crear(Tipo.Construir(), (NivelCorreccion)Nivel);
            var (bgra, lado) = codigo.Pixeles(codigo.PixelesPorModulo(400), ColorQr, ColorClaro);
            var imagen = BitmapSource.Create(lado, lado, 96, 96, PixelFormats.Bgra32, null, bgra, lado * 4);
            imagen.Freeze();
            Codigo = codigo;
            VistaPrevia = imagen;
            Aviso = null;
        }
        catch (ContenidoInvalidoException ex)
        {
            Codigo = null;
            VistaPrevia = null;
            Aviso = ex.Message;
        }
    }

    [RelayCommand]
    private void ElegirColor(string color) => ColorQr = color;

    [RelayCommand(CanExecute = nameof(HayQr))]
    private void GuardarPng()
    {
        var ruta = PedirRuta("Imagen PNG|*.png", ".png");
        if (ruta is null) return;
        File.WriteAllBytes(ruta, Codigo!.Png(Tamano, ColorQr, ColorClaro));
        Estado = "Guardado en " + ruta;
    }

    [RelayCommand(CanExecute = nameof(HayQr))]
    private void GuardarSvg()
    {
        var ruta = PedirRuta("Imagen vectorial SVG (para imprenta)|*.svg", ".svg");
        if (ruta is null) return;
        File.WriteAllText(ruta, Codigo!.Svg(ColorQr, ColorClaro));
        Estado = "Guardado en " + ruta;
    }

    [RelayCommand(CanExecute = nameof(HayQr))]
    private void Copiar()
    {
        var codigo = Codigo!;
        var (bgra, lado) = codigo.Pixeles(codigo.PixelesPorModulo(Tamano), ColorQr, ColorClaro);
        // The classic bitmap format has no transparency; apps that support it (Word, PowerPoint, Photoshop,
        // browsers…) take the "PNG" format instead, which keeps it.
        var datos = new DataObject();
        datos.SetImage(BitmapSource.Create(lado, lado, 96, 96, PixelFormats.Bgra32, null, bgra, lado * 4));
        datos.SetData("PNG", new MemoryStream(codigo.Png(Tamano, ColorQr, ColorClaro)));
        Clipboard.SetDataObject(datos, copy: true);
        Estado = "Imagen copiada: pegala con Ctrl+V donde quieras.";
    }

    public bool SePuedeProbar => Codigo is not null && Analizador.Analizar(Codigo.Contenido).Enlace is not null;

    /// <summary>Opens what the QR points to, to check it before printing it.</summary>
    [RelayCommand(CanExecute = nameof(SePuedeProbar))]
    private void Probar() => Abrir.Enlace(Analizador.Analizar(Codigo!.Contenido).Enlace!);

    private string? PedirRuta(string filtro, string extension)
    {
        var dialogo = new SaveFileDialog { Filter = filtro, DefaultExt = extension, FileName = Tipo.Archivo + extension, AddExtension = true };
        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }
}

internal static class Abrir
{
    public static void Enlace(string enlace)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(enlace) { UseShellExecute = true });
        }
        catch (Exception)
        {
            MessageBox.Show("Windows no tiene una app para abrir este enlace:\n" + enlace, "Generador QR");
        }
    }
}
