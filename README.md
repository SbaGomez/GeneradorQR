# Generador QR

App de escritorio (Windows, .NET 10, WPF) para crear y verificar códigos QR **estáticos**: el QR lleva el destino final adentro, sin pasar por acortadores ni plataformas intermedias, así que no vence nunca.

## Crear
Página web · WhatsApp (enlace oficial `wa.me`, con mensaje opcional) · Wi-Fi · Texto · Email · Llamada · SMS · Contacto (vCard) · Ubicación.

**Ubicación** se elige en un mapa (OpenStreetMap): arranca donde está la PC (ubicación de Windows), y se cambia tocando el mapa, arrastrando el pin o buscando una dirección. El mapa necesita internet; el QR resultante no.

Vista previa en vivo, color del QR y del fondo (avisa si hay poco contraste), corrección de errores, y exportación a **PNG** (512–4096 px), **SVG** (para imprenta) o al portapapeles. "Probar enlace" abre el destino para comprobarlo antes de imprimir.

## Verificar
Abrí, arrastrá o pegá (Ctrl+V) una imagen con un QR —uno tuyo, una foto o una captura— y la app muestra qué contiene y si va **directo** o pasa por un **intermediario** (bit.ly, qrco.de, me-qr, redirecciones con `?url=`…).

## Generar el .exe
```
dotnet publish src/GeneradorQR.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```
Queda un único `publish/GeneradorQR.exe` que no necesita instalar .NET. El icono se regenera con `powershell -File tools/GenerarIcono.ps1`.

## Desarrollo
```
dotnet run --project src/GeneradorQR.App
dotnet test
```
- `src/GeneradorQR.Core`: armado del contenido de cada tipo, generación (QRCoder), lectura (ZXing.Net) y análisis.
- `src/GeneradorQR.App`: interfaz WPF (CommunityToolkit.Mvvm).
- `tests/GeneradorQR.Core.Tests`: tests de xUnit, incluidos ida y vuelta generar → leer.
