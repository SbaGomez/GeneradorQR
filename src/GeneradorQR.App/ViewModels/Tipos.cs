using CommunityToolkit.Mvvm.ComponentModel;
using GeneradorQR.Core;

namespace GeneradorQR.App.ViewModels;

/// <summary>A sidebar entry: one kind of QR to create, or the verifier.</summary>
public interface ISeccion
{
    string Nombre { get; }
    string Icono { get; } // Segoe Fluent Icons glyph
}

/// <summary>One kind of QR: its form fields and how they become the QR's text.</summary>
public abstract class TipoQr : ObservableObject, ISeccion
{
    public abstract string Nombre { get; }
    public abstract string Icono { get; }
    public abstract string Descripcion { get; }
    /// <summary>File name suggested when saving, without extension.</summary>
    public abstract string Archivo { get; }

    /// <summary>The QR's text; throws <see cref="ContenidoInvalidoException"/> while the form is incomplete.</summary>
    public abstract string Construir();
}

public sealed partial class WebQr : TipoQr
{
    public override string Nombre => "Página web";
    public override string Icono => "";
    public override string Descripcion => "Al escanearlo abre la página. El QR lleva la dirección tal cual, sin acortadores.";
    public override string Archivo => "qr-web";
    [ObservableProperty] private string _url = "";
    public override string Construir() => Contenidos.Web(Url);
}

public sealed partial class WhatsAppQr : TipoQr
{
    public override string Nombre => "WhatsApp";
    public override string Icono => "";
    public override string Descripcion => "Abre un chat con tu número, con un mensaje ya escrito si querés. Usa el enlace oficial wa.me.";
    public override string Archivo => "qr-whatsapp";
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _mensaje = "";
    public override string Construir() => Contenidos.WhatsApp(Numero, Mensaje);
}

public sealed partial class WifiQr : TipoQr
{
    public override string Nombre => "Wi-Fi";
    public override string Icono => "";
    public override string Descripcion => "Tus clientes se conectan a la red escaneándolo, sin escribir la contraseña.";
    public override string Archivo => "qr-wifi";
    [ObservableProperty] private string _red = "";
    [ObservableProperty] private string _clave = "";
    [ObservableProperty] private int _seguridad; // index of SeguridadWifi
    [ObservableProperty] private bool _oculta;
    public override string Construir() => Contenidos.Wifi(Red, Clave, (SeguridadWifi)Seguridad, Oculta);
}

public sealed partial class TextoQr : TipoQr
{
    public override string Nombre => "Texto";
    public override string Icono => "";
    public override string Descripcion => "Cualquier texto: el celular lo muestra tal cual.";
    public override string Archivo => "qr-texto";
    [ObservableProperty] private string _texto = "";
    public override string Construir() => Contenidos.Texto(Texto);
}

public sealed partial class EmailQr : TipoQr
{
    public override string Nombre => "Email";
    public override string Icono => "";
    public override string Descripcion => "Abre la app de correo con el destinatario, el asunto y el mensaje listos.";
    public override string Archivo => "qr-email";
    [ObservableProperty] private string _para = "";
    [ObservableProperty] private string _asunto = "";
    [ObservableProperty] private string _cuerpo = "";
    public override string Construir() => Contenidos.Email(Para, Asunto, Cuerpo);
}

public sealed partial class TelefonoQr : TipoQr
{
    public override string Nombre => "Llamada";
    public override string Icono => "";
    public override string Descripcion => "Abre el teléfono con el número marcado.";
    public override string Archivo => "qr-telefono";
    [ObservableProperty] private string _numero = "";
    public override string Construir() => Contenidos.Telefono(Numero);
}

public sealed partial class SmsQr : TipoQr
{
    public override string Nombre => "SMS";
    public override string Icono => "";
    public override string Descripcion => "Abre los mensajes con el número y el texto listos para enviar.";
    public override string Archivo => "qr-sms";
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _mensaje = "";
    public override string Construir() => Contenidos.Sms(Numero, Mensaje);
}

public sealed partial class ContactoQr : TipoQr
{
    public override string Nombre => "Contacto";
    public override string Icono => "";
    public override string Descripcion => "Una tarjeta de contacto: el celular ofrece guardarla en la agenda.";
    public override string Archivo => "qr-contacto";
    [ObservableProperty] private string _nombrePersona = "";
    [ObservableProperty] private string _apellido = "";
    [ObservableProperty] private string _telefono = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _empresa = "";
    [ObservableProperty] private string _web = "";
    public override string Construir() => Contenidos.Contacto(NombrePersona, Apellido, Telefono, Email, Empresa, Web);
}

public sealed partial class UbicacionQr : TipoQr
{
    public override string Nombre => "Ubicación";
    public override string Icono => "";
    public override string Descripcion => "Abre el mapa en el lugar que elijas: tocá el mapa, arrastrá el pin o buscá la dirección.";
    public override string Archivo => "qr-ubicacion";
    [ObservableProperty] private string _coordenadas = "";
    public override string Construir() => Contenidos.Ubicacion(Coordenadas);
}
