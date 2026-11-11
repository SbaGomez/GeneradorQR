namespace GeneradorQR.Core;

public enum Veredicto
{
    /// <summary>Goes straight to its destination; works forever without depending on anyone.</summary>
    Directo,
    /// <summary>Goes through a shortener, a QR platform or a redirect, which can stop working, charge or change it.</summary>
    Intermediario,
    /// <summary>Not a link (Wi-Fi, contact, plain text…): nothing to redirect.</summary>
    SinEnlace,
}

/// <summary>What a scanned QR holds, in plain words.</summary>
/// <param name="Tipo">Short kind shown as a chip ("Página web", "WhatsApp"…).</param>
/// <param name="Enlace">What the "Abrir" button opens, when the content is something Windows can open.</param>
public sealed record AnalisisQr(string Tipo, Veredicto Veredicto, string Titulo, string Detalle, string? Enlace);

/// <summary>Tells what a QR's text is and whether it goes through a middleman.</summary>
public static class Analizador
{
    // Shorteners and "dynamic QR" platforms: the code holds their address and they redirect to the real one.
    private static readonly string[] Intermediarios =
    [
        "bit.ly", "bitly.com", "tinyurl.com", "goo.gl", "t.ly", "ow.ly", "is.gd", "buff.ly", "cutt.ly", "rb.gy",
        "rebrand.ly", "short.io", "shorturl.at", "s.id", "tiny.cc", "lnkd.in", "t.co", "v.gd", "shorte.st", "adf.ly",
        "qrco.de", "qr-code-generator.com", "qr.io", "qr.codes", "qrfy.com", "qrfy.io", "me-qr.com", "l.ead.me",
        "ead.me", "qrs.ly", "scan.page", "qr1.be", "qr.link", "qrcodechimp.com", "uqr.me", "beaconstac.com",
        "qrcode.tiger", "qrtiger.com", "qrstud.io", "qr.page", "qr.net", "flowcode.com", "flow.page", "linktr.ee",
        "qrcg.link", "q-r.to", "qr.st", "qrd.by", "hovercode.com", "hov.to", "unitag.io", "qrgo.page",
    ];

    // Query parameters that usually carry the real destination of a redirect.
    private static readonly string[] ParametrosRedireccion = ["url", "u", "redirect", "redirect_uri", "target", "dest", "destination", "link", "to", "goto", "next", "r"];

    public static AnalisisQr Analizar(string contenido)
    {
        var texto = contenido.Trim();
        var minus = texto.ToLowerInvariant();

        if ((minus.StartsWith("http://") || minus.StartsWith("https://")) && Uri.TryCreate(texto, UriKind.Absolute, out var uri))
            return AnalizarWeb(uri, texto);

        if (minus.StartsWith("wifi:"))
        {
            var red = Campo(texto, "S:");
            return new("Red Wi-Fi", Veredicto.SinEnlace, red is null ? "Red Wi-Fi" : $"Red Wi-Fi \"{red}\"",
                "Al escanearlo el celular se conecta a la red, sin escribir la contraseña.", null);
        }
        if (minus.StartsWith("mailto:"))
            return new("Email", Veredicto.Directo, "Email a " + texto[7..].Split('?')[0],
                "Abre la app de correo con el mensaje listo para enviar.", texto);
        if (minus.StartsWith("tel:"))
            return new("Llamada", Veredicto.Directo, "Llamar al " + texto[4..],
                "Abre el teléfono con el número marcado.", texto);
        if (minus.StartsWith("smsto:") || minus.StartsWith("sms:"))
            return new("SMS", Veredicto.Directo, "SMS al " + texto.Split(':')[1],
                "Abre los mensajes con el texto listo para enviar.", null);
        if (minus.StartsWith("begin:vcard") || minus.StartsWith("mecard:"))
            return new("Contacto", Veredicto.SinEnlace, "Tarjeta de contacto",
                "El celular ofrece guardar el contacto en la agenda.", null);
        if (minus.StartsWith("geo:"))
        {
            var coordenadas = texto[4..].Split('?')[0];
            return new("Ubicación", Veredicto.Directo, "Ubicación " + coordenadas, "Abre el mapa en ese punto.",
                "https://www.google.com/maps?q=" + Uri.EscapeDataString(coordenadas));
        }
        return new("Texto", Veredicto.SinEnlace, "Texto", "Contiene texto que el celular muestra tal cual.", null);
    }

    private static AnalisisQr AnalizarWeb(Uri uri, string texto)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.")) host = host[4..];

        var intermediario = Intermediarios.FirstOrDefault(d => host == d || host.EndsWith("." + d));
        if (intermediario is not null)
            return new("Enlace con intermediario", Veredicto.Intermediario, $"Pasa por un intermediario ({uri.Host})",
                "Es un acortador o una plataforma de QR dinámicos: el código lleva a ese servicio y él redirige al destino. " +
                "Si el servicio cierra, cobra o cambia el destino, el QR deja de funcionar.", texto);

        if (host is "wa.me" or "api.whatsapp.com" or "whatsapp.com" or "web.whatsapp.com" || host == "chat.whatsapp.com")
        {
            var titulo = host == "chat.whatsapp.com" ? "Grupo de WhatsApp" :
                host == "wa.me" && uri.AbsolutePath.Trim('/').Length > 0 ? "WhatsApp al +" + uri.AbsolutePath.Trim('/') : "WhatsApp";
            return new("WhatsApp", Veredicto.Directo, titulo,
                "Es un enlace oficial de WhatsApp: abre el chat directo, sin pasar por terceros.", texto);
        }

        var parametros = System.Web.HttpUtility.ParseQueryString(uri.Query);
        foreach (var clave in parametros.AllKeys)
        {
            var valor = clave is null ? null : parametros[clave];
            if (clave is not null && valor is not null && ParametrosRedireccion.Contains(clave.ToLowerInvariant())
                && (valor.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || valor.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                return new("Redirección", Veredicto.Intermediario, $"Redirige desde {uri.Host}",
                    $"Primero pasa por {uri.Host} y de ahí va a {valor}.", texto);
        }

        return new("Página web", Veredicto.Directo, "Va directo a " + uri.Host,
            "El QR lleva la dirección final de la página, sin acortadores ni redirecciones de por medio.", texto);
    }

    // Value of a "K:value;" field in the WIFI: format, honoring backslash escapes.
    private static string? Campo(string texto, string clave)
    {
        var inicio = Math.Max(texto.IndexOf(":" + clave, StringComparison.OrdinalIgnoreCase), texto.IndexOf(";" + clave, StringComparison.OrdinalIgnoreCase));
        if (inicio < 0) return null;
        var sb = new System.Text.StringBuilder();
        for (var i = inicio + 1 + clave.Length; i < texto.Length; i++)
        {
            if (texto[i] == '\\' && i + 1 < texto.Length) sb.Append(texto[++i]);
            else if (texto[i] == ';') break;
            else sb.Append(texto[i]);
        }
        return sb.ToString();
    }
}
