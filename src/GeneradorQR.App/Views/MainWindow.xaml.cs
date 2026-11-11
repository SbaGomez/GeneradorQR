using System.Windows;
using System.Windows.Navigation;
using GeneradorQR.App.ViewModels;

namespace GeneradorQR.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void AbrirLink(object sender, RequestNavigateEventArgs e)
    {
        Abrir.Enlace(e.Uri.AbsoluteUri);
        e.Handled = true;
    }
}
