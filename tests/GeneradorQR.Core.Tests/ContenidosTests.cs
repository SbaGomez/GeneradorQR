using GeneradorQR.Core;

namespace GeneradorQR.Core.Tests;

public class ContenidosTests
{
    [Theory]
    [InlineData("www.mipagina.com", "https://www.mipagina.com")]
    [InlineData("  https://mipagina.com/menu?x=1 ", "https://mipagina.com/menu?x=1")]
    [InlineData("http://mipagina.com.ar", "http://mipagina.com.ar")]
    public void Web_agrega_https_y_respeta_lo_escrito(string entrada, string esperado) =>
        Assert.Equal(esperado, Contenidos.Web(entrada));

    [Theory]
    [InlineData("")]
    [InlineData("hola")]
    [InlineData("mi pagina.com")]
    [InlineData("ftp://archivos.com")]
    public void Web_rechaza_direcciones_invalidas(string entrada) =>
        Assert.Throws<ContenidoInvalidoException>(() => Contenidos.Web(entrada));

    [Fact]
    public void WhatsApp_usa_wa_me_con_solo_digitos_y_mensaje_codificado() =>
        Assert.Equal("https://wa.me/5491123456789?text=Hola%2C%20%C2%BFprecio%3F",
            Contenidos.WhatsApp("+54 9 11 2345-6789", "Hola, ¿precio?"));

    [Fact]
    public void WhatsApp_sin_mensaje_no_lleva_parametros() =>
        Assert.Equal("https://wa.me/5491123456789", Contenidos.WhatsApp("5491123456789", "  "));

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    public void WhatsApp_pide_numero_con_codigo_de_pais(string numero) =>
        Assert.Throws<ContenidoInvalidoException>(() => Contenidos.WhatsApp(numero));

    [Fact]
    public void Wifi_escapa_los_caracteres_reservados() =>
        Assert.Equal(@"WIFI:T:WPA;S:Casa\;Pepe;P:cla\:ve\,12;;", Contenidos.Wifi("Casa;Pepe", "cla:ve,12", SeguridadWifi.Wpa));

    [Fact]
    public void Wifi_abierta_y_oculta() =>
        Assert.Equal("WIFI:T:nopass;S:Bar;H:true;;", Contenidos.Wifi("Bar", "ignorada", SeguridadWifi.SinClave, oculta: true));

    [Fact]
    public void Wifi_wpa_exige_8_caracteres() =>
        Assert.Throws<ContenidoInvalidoException>(() => Contenidos.Wifi("Bar", "corta", SeguridadWifi.Wpa));

    [Fact]
    public void Email_con_asunto_y_cuerpo() =>
        Assert.Equal("mailto:ventas@negocio.com?subject=Consulta%20de%20precio&body=Hola",
            Contenidos.Email("ventas@negocio.com", "Consulta de precio", "Hola"));

    [Fact]
    public void Email_invalido() =>
        Assert.Throws<ContenidoInvalidoException>(() => Contenidos.Email("ventas@negocio"));

    [Fact]
    public void Telefono_y_sms() =>
        Assert.Equal(("tel:+541123456789", "SMSTO:1123456789:Hola"),
            (Contenidos.Telefono("+54 11 2345-6789"), Contenidos.Sms("11 2345 6789", "Hola")));

    [Fact]
    public void Contacto_arma_vcard_escapada()
    {
        var vcard = Contenidos.Contacto("Juan", "Pérez", "+54 11 2345 6789", "juan@mail.com", "Pérez, Hnos; SA", "juan.com");
        Assert.Equal(
            "BEGIN:VCARD\r\nVERSION:3.0\r\nN:Pérez;Juan;;;\r\nFN:Juan Pérez\r\nORG:Pérez\\, Hnos\\; SA\r\n" +
            "TEL;TYPE=CELL:+541123456789\r\nEMAIL:juan@mail.com\r\nURL:https://juan.com\r\nEND:VCARD\r\n", vcard);
    }

    [Theory]
    [InlineData("-34.6037, -58.3816", "geo:-34.6037,-58.3816")]
    [InlineData("-34.6037,-58.3816", "geo:-34.6037,-58.3816")]
    [InlineData("-34,6037, -58,3816", "geo:-34.6037,-58.3816")]
    [InlineData("-34.6037 -58.3816", "geo:-34.6037,-58.3816")]
    public void Ubicacion_acepta_el_formato_de_google_maps(string entrada, string esperado) =>
        Assert.Equal(esperado, Contenidos.Ubicacion(entrada));

    [Theory]
    [InlineData("")]
    [InlineData("Av. Corrientes 1234")]
    [InlineData("95, 10")]
    public void Ubicacion_invalida(string entrada) =>
        Assert.Throws<ContenidoInvalidoException>(() => Contenidos.Ubicacion(entrada));
}
