namespace GeneradorQR.Core;

/// <summary>What the user typed can't become a QR yet; the message is shown as-is under the form.</summary>
public sealed class ContenidoInvalidoException(string mensaje) : Exception(mensaje);
