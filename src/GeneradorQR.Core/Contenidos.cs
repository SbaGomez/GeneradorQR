using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace GeneradorQR.Core;

public enum SeguridadWifi { Wpa, Wep, SinClave }

/// <summary>
/// Builds the text a QR carries for each kind of code. Everything is static: the QR holds the final destination
/// (the web address, the wa.me link, the Wi-Fi credentials…), never a redirect or shortener, so it works forever and
/// without depending on any service. Each method throws <see cref="ContenidoInvalidoException"/> when the input
/// isn't usable.
/// </summary>
public static partial class Contenidos
{
    /// <summary>A web page. Adds https:// when no scheme was typed.</summary>
    public static string Web(string url)
    {
        var texto = (url ?? "").Trim();
        if (texto.Length == 0) throw new ContenidoInvalidoException("Escribí la dirección de la página.");
        if (!texto.Contains("://")) texto = "https://" + texto;

        if (texto.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(texto, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || (!uri.Host.Contains('.') && uri.Host != "localhost"))
            throw new ContenidoInvalidoException("La dirección no es válida (ejemplo: www.mipagina.com).");
        return texto;
    }

    /// <summary>
    /// A WhatsApp chat via the official wa.me link (WhatsApp's own domain, not a third-party redirect), optionally with
    /// a pre-filled message.
    /// </summary>
    public static string WhatsApp(string numero, string? mensaje = null)
    {
        var digitos = SoloDigitos(numero);
        if (digitos.Length == 0) throw new ContenidoInvalidoException("Escribí el número de WhatsApp.");
        if (digitos.Length is < 8 or > 15)
            throw new ContenidoInvalidoException("El número tiene que incluir el código de país (ejemplo: 5491123456789).");

        var link = "https://wa.me/" + digitos;
        var texto = (mensaje ?? "").Trim();
        return texto.Length == 0 ? link : link + "?text=" + Uri.EscapeDataString(texto);
    }

    /// <summary>Free text, shown as-is by the phone's reader.</summary>
    public static string Texto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) throw new ContenidoInvalidoException("Escribí el texto.");
        return texto;
    }

    /// <summary>Wi-Fi network in the standard WIFI: format, which phones join with one tap.</summary>
    public static string Wifi(string red, string? clave, SeguridadWifi seguridad, bool oculta = false)
    {
        if (string.IsNullOrEmpty(red)) throw new ContenidoInvalidoException("Escribí el nombre de la red.");
        clave ??= "";
        if (seguridad != SeguridadWifi.SinClave && clave.Length == 0)
            throw new ContenidoInvalidoException("Escribí la contraseña (o elegí \"Sin contraseña\").");
        if (seguridad == SeguridadWifi.Wpa && clave.Length < 8)
            throw new ContenidoInvalidoException("Una contraseña WPA tiene al menos 8 caracteres.");

        var tipo = seguridad switch { SeguridadWifi.Wpa => "WPA", SeguridadWifi.Wep => "WEP", _ => "nopass" };
        var sb = new StringBuilder("WIFI:T:").Append(tipo).Append(";S:").Append(EscaparWifi(red)).Append(';');
        if (seguridad != SeguridadWifi.SinClave) sb.Append("P:").Append(EscaparWifi(clave)).Append(';');
        if (oculta) sb.Append("H:true;");
        return sb.Append(';').ToString();
    }

    /// <summary>An email ready to send (mailto:), with optional subject and body.</summary>
    public static string Email(string destinatario, string? asunto = null, string? cuerpo = null)
    {
        var mail = ValidarEmail(destinatario, obligatorio: true)!;
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(asunto)) parametros.Add("subject=" + Uri.EscapeDataString(asunto.Trim()));
        if (!string.IsNullOrWhiteSpace(cuerpo)) parametros.Add("body=" + Uri.EscapeDataString(cuerpo.Trim()));
        return "mailto:" + mail + (parametros.Count == 0 ? "" : "?" + string.Join("&", parametros));
    }

    /// <summary>A phone call (tel:).</summary>
    public static string Telefono(string numero) => "tel:" + ValidarTelefono(numero);

    /// <summary>A text message ready to send (SMSTO:, which Android and iPhone both read).</summary>
    public static string Sms(string numero, string? mensaje = null) =>
        "SMSTO:" + ValidarTelefono(numero) + ":" + (mensaje ?? "").Trim();

    /// <summary>A contact card (vCard 3.0) that the phone offers to save to its contacts.</summary>
    public static string Contacto(string nombre, string? apellido, string? telefono, string? email,
        string? empresa = null, string? web = null)
    {
        nombre = (nombre ?? "").Trim();
        apellido = (apellido ?? "").Trim();
        if (nombre.Length == 0 && apellido.Length == 0) throw new ContenidoInvalidoException("Escribí el nombre del contacto.");

        var sb = new StringBuilder();
        void Linea(string linea) => sb.Append(linea).Append("\r\n");
        Linea("BEGIN:VCARD");
        Linea("VERSION:3.0");
        Linea($"N:{EscaparVCard(apellido)};{EscaparVCard(nombre)};;;");
        Linea("FN:" + EscaparVCard(string.Join(" ", new[] { nombre, apellido }.Where(p => p.Length > 0))));
        if (!string.IsNullOrWhiteSpace(empresa)) Linea("ORG:" + EscaparVCard(empresa.Trim()));
        if (!string.IsNullOrWhiteSpace(telefono)) Linea("TEL;TYPE=CELL:" + ValidarTelefono(telefono));
        var mail = ValidarEmail(email, obligatorio: false);
        if (mail is not null) Linea("EMAIL:" + mail);
        if (!string.IsNullOrWhiteSpace(web)) Linea("URL:" + Web(web));
        Linea("END:VCARD");
        return sb.ToString();
    }

    /// <summary>
    /// A point on the map (geo:). Takes the coordinates as Google Maps copies them ("-34.6037, -58.3816"), with dot or
    /// comma decimals.
    /// </summary>
    public static string Ubicacion(string coordenadas)
    {
        if (string.IsNullOrWhiteSpace(coordenadas))
            throw new ContenidoInvalidoException("Elegí un punto en el mapa o pegá las coordenadas.");
        var (lat, lng) = LeerCoordenadas(coordenadas)
            ?? throw new ContenidoInvalidoException("Las coordenadas no son válidas (ejemplo: -34.6037, -58.3816).");
        return "geo:" + lat.ToString("0.######", CultureInfo.InvariantCulture) + ","
            + lng.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>"lat, lng" as typed or pasted (dot or comma decimals); null when it isn't a valid point.</summary>
    public static (double Lat, double Lng)? LeerCoordenadas(string? coordenadas)
    {
        var m = CoordenadasRegex().Match(coordenadas ?? "");
        if (!m.Success
            || !double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(m.Groups[2].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)
            || lat is < -90 or > 90 || lng is < -180 or > 180)
            return null;
        return (lat, lng);
    }

    /// <summary>The "lat, lng" text for a point, with 6 decimals (about 10 cm).</summary>
    public static string FormatearCoordenadas(double lat, double lng) =>
        lat.ToString("0.######", CultureInfo.InvariantCulture) + ", " + lng.ToString("0.######", CultureInfo.InvariantCulture);

    private static string SoloDigitos(string? texto) => new((texto ?? "").Where(char.IsAsciiDigit).ToArray());

    private static string ValidarTelefono(string? numero)
    {
        var texto = (numero ?? "").Trim();
        var digitos = SoloDigitos(texto);
        if (digitos.Length == 0) throw new ContenidoInvalidoException("Escribí el número de teléfono.");
        if (digitos.Length is < 6 or > 15 || texto.Any(c => !char.IsAsciiDigit(c) && " +-().".IndexOf(c) < 0))
            throw new ContenidoInvalidoException("El número de teléfono no es válido.");
        return (texto.StartsWith('+') ? "+" : "") + digitos;
    }

    private static string? ValidarEmail(string? email, bool obligatorio)
    {
        var texto = (email ?? "").Trim();
        if (texto.Length == 0)
            return obligatorio ? throw new ContenidoInvalidoException("Escribí el email.") : null;
        if (!MailAddress.TryCreate(texto, out var direccion) || direccion.Address != texto || !direccion.Host.Contains('.'))
            throw new ContenidoInvalidoException("El email no es válido.");
        return texto;
    }

    // The WIFI: format reserves \ ; , : and " and escapes them with a backslash.
    private static string EscaparWifi(string texto) => EscaparWifiRegex().Replace(texto, @"\$0");

    private static string EscaparVCard(string texto) => texto
        .Replace("\\", "\\\\").Replace(",", "\\,").Replace(";", "\\;")
        .Replace("\r\n", "\\n").Replace("\n", "\\n");

    [GeneratedRegex(@"[\\;,:""]")]
    private static partial Regex EscaparWifiRegex();

    // Two signed decimals separated by a comma, a semicolon or spaces; either may use a decimal comma (the first
    // number's decimals are matched greedily, so "-34,60, -58,38" splits at the comma followed by a space).
    [GeneratedRegex(@"^\s*(-?\d{1,3}(?:[.,]\d+)?)\s*(?:,\s*|;\s*|\s+)(-?\d{1,3}(?:[.,]\d+)?)\s*$")]
    private static partial Regex CoordenadasRegex();
}
