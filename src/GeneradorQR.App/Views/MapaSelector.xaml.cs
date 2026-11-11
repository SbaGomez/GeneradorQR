using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using GeneradorQR.App.Servicios;
using GeneradorQR.App.ViewModels;
using GeneradorQR.Core;
using Microsoft.Web.WebView2.Core;

namespace GeneradorQR.App.Views;

/// <summary>
/// OpenStreetMap map to pick the point of a location QR: click or drag the pin, or search an address. Starts where
/// the PC is. The page and Leaflet are embedded in the exe and served from https://mapa.local; only the map tiles
/// and address searches go to openstreetmap.org (the QR itself never depends on them).
/// </summary>
public partial class MapaSelector : UserControl
{
    private const string Origen = "https://mapa.local/";
    private static Task<CoreWebView2Environment>? _entorno;

    private UbicacionQr? _vm;
    private bool _iniciado;
    private bool _listo;
    // Last text the map itself wrote, so that change doesn't bounce back as a "move the map" call.
    private string? _escritoPorElMapa;

    public MapaSelector()
    {
        InitializeComponent();
        Loaded += async (_, _) => await Iniciar();
        Unloaded += (_, _) => Web.Dispose(); // the view is rebuilt when coming back: free the browser process
        DataContextChanged += (_, e) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= AlCambiarVm;
            _vm = e.NewValue as UbicacionQr;
            if (_vm is not null) _vm.PropertyChanged += AlCambiarVm;
        };
    }

    private async Task Iniciar()
    {
        if (_iniciado) return;
        _iniciado = true;
        try
        {
            // Browser profile in the user's AppData (next to the exe isn't writable once installed in Program Files).
            _entorno ??= CoreWebView2Environment.CreateAsync(null,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeneradorQR", "WebView2"));
            await Web.EnsureCoreWebView2Async(await _entorno);
        }
        catch (Exception)
        {
            Cargando.Text = "No se pudo mostrar el mapa (falta Microsoft Edge WebView2). Podés escribir las coordenadas a mano.";
            return;
        }

        var core = Web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.AddWebResourceRequestedFilter(Origen + "*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += Servir;
        core.WebMessageReceived += AlRecibirMensaje;
        core.NewWindowRequested += (_, e) => { e.Handled = true; Abrir.Enlace(e.Uri); }; // attribution link
        core.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess) return;
            Web.Visibility = Visibility.Visible;
            _listo = true;
            await AlCargar();
        };
        core.Navigate(Origen + "mapa.html");
    }

    private async Task AlCargar()
    {
        if (Contenidos.LeerCoordenadas(_vm?.Coordenadas) is { } punto)
        {
            await Mover(punto.Lat, punto.Lng, 16);
            return;
        }
        await IrAMiUbicacion();
    }

    private async Task IrAMiUbicacion()
    {
        await Estado("Buscando tu ubicación…");
        if (await UbicacionActual.Obtener() is { } aca)
        {
            await Estado(null);
            await Mover(aca.Lat, aca.Lng, 16);
            Elegir(aca.Lat, aca.Lng);
        }
        else
            await Estado("No pude saber dónde estás (revisá que la ubicación esté activada en Windows). Tocá el mapa para elegir el lugar.", 7);
    }

    private void Elegir(double lat, double lng)
    {
        if (_vm is null) return;
        _escritoPorElMapa = Contenidos.FormatearCoordenadas(lat, lng);
        _vm.Coordenadas = _escritoPorElMapa;
    }

    private async void AlRecibirMensaje(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var json = JsonDocument.Parse(e.WebMessageAsJson);
        var raiz = json.RootElement;
        switch (raiz.GetProperty("tipo").GetString())
        {
            case "punto":
                Elegir(raiz.GetProperty("lat").GetDouble(), raiz.GetProperty("lng").GetDouble());
                break;
            case "miUbicacion":
                await IrAMiUbicacion();
                break;
        }
    }

    // Coordinates typed or pasted by hand: move the pin there.
    private async void AlCambiarVm(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UbicacionQr.Coordenadas) || _vm!.Coordenadas == _escritoPorElMapa) return;
        if (Contenidos.LeerCoordenadas(_vm.Coordenadas) is { } punto) await Mover(punto.Lat, punto.Lng, null);
    }

    private async Task Mover(double lat, double lng, int? zoom)
    {
        if (!_listo) return;
        await Web.ExecuteScriptAsync(FormattableString.Invariant($"mover({lat}, {lng}, {(zoom is null ? "undefined" : zoom.ToString())})"));
    }

    private async Task Estado(string? texto, int segundos = 0)
    {
        if (!_listo) return;
        await Web.ExecuteScriptAsync($"estado({JsonSerializer.Serialize(texto)}, {segundos})");
    }

    // Serves mapa.html and Leaflet from the resources embedded in the exe.
    private void Servir(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var entorno = Web.CoreWebView2.Environment;
        var nombre = new Uri(e.Request.Uri).AbsolutePath.TrimStart('/');
        var recurso = typeof(MapaSelector).Assembly.GetManifestResourceStream("Mapa/" + nombre);
        if (recurso is null)
        {
            e.Response = entorno.CreateWebResourceResponse(null, 404, "Not Found", "");
            return;
        }
        var tipo = Path.GetExtension(nombre) switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            _ => "application/octet-stream",
        };
        e.Response = entorno.CreateWebResourceResponse(recurso, 200, "OK", "Content-Type: " + tipo);
    }
}
