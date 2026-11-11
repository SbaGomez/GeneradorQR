using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GeneradorQR.App.ViewModels;

/// <summary>The sidebar: the kinds of QR to create plus the verifier, and which one is open.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        Tipos = [new WebQr(), new WhatsAppQr(), new WifiQr(), new TextoQr(), new EmailQr(), new TelefonoQr(), new SmsQr(), new ContactoQr(), new UbicacionQr()];
        Secciones = [.. Tipos, Verificador];
        Generador = new GeneradorViewModel(Tipos[0]);
        _seccion = Tipos[0];
        _pantalla = Generador;
    }

    public IReadOnlyList<TipoQr> Tipos { get; }
    public IReadOnlyList<ISeccion> Secciones { get; }
    public GeneradorViewModel Generador { get; }
    public VerificadorViewModel Verificador { get; } = new();

    /// <summary>Selected sidebar entry (a <see cref="TipoQr"/> or the verifier).</summary>
    [ObservableProperty] private ISeccion _seccion;

    /// <summary>What the content area shows: the generator or the verifier.</summary>
    [ObservableProperty] private object _pantalla;

    partial void OnSeccionChanged(ISeccion value)
    {
        if (value is TipoQr tipo)
        {
            Generador.Tipo = tipo;
            Pantalla = Generador;
        }
        else Pantalla = Verificador;
    }

    /// <summary>Ctrl+V anywhere outside a text box: check the copied image.</summary>
    [RelayCommand]
    private async Task PegarImagen()
    {
        Seccion = Verificador;
        await Verificador.PegarCommand.ExecuteAsync(null);
    }
}
