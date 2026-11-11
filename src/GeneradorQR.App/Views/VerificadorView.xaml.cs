using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeneradorQR.App.ViewModels;

namespace GeneradorQR.App.Views;

public partial class VerificadorView : UserControl
{
    public VerificadorView() => InitializeComponent();

    private VerificadorViewModel Vm => (VerificadorViewModel)DataContext;

    private void AlArrastrar(object sender, DragEventArgs e)
    {
        var sirve = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap);
        e.Effects = sirve ? DragDropEffects.Copy : DragDropEffects.None;
        Zona.Stroke = sirve ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("InputBorderBrush");
        e.Handled = true;
    }

    private async void AlSoltar(object sender, DragEventArgs e)
    {
        Zona.Stroke = (Brush)FindResource("InputBorderBrush");
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } archivos)
            await Vm.CargarArchivo(archivos[0]);
        else if (e.Data.GetData(DataFormats.Bitmap) is BitmapSource imagen)
            await Vm.Cargar(imagen);
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        Zona.Stroke = (Brush)FindResource("InputBorderBrush");
    }
}

/// <summary>Visible when the bound value is a non-empty string, collapsed otherwise.</summary>
public sealed class Visible : IValueConverter
{
    public static Visible SiHay { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
