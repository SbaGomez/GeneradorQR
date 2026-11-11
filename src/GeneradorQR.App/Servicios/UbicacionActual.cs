using Windows.Devices.Geolocation;

namespace GeneradorQR.App.Servicios;

/// <summary>Where this PC is, from Windows' location service (Wi-Fi/IP based on most desktops).</summary>
public static class UbicacionActual
{
    /// <summary>Null when location is off, denied in Windows' privacy settings, or takes too long.</summary>
    public static async Task<(double Lat, double Lng)?> Obtener()
    {
        try
        {
            if (await Geolocator.RequestAccessAsync() != GeolocationAccessStatus.Allowed) return null;
            var localizador = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var tarea = localizador.GetGeopositionAsync(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10)).AsTask();
            if (await Task.WhenAny(tarea, Task.Delay(TimeSpan.FromSeconds(12))) != tarea) return null;
            var punto = (await tarea).Coordinate.Point.Position;
            return (punto.Latitude, punto.Longitude);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
