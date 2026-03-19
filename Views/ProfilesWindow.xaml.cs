using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MXFConverter.Models;
using MXFConverter.Services;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace MXFConverter.Views;

public partial class ProfilesWindow : Window
{
    public ConversionProfile? SelectedProfile { get; private set; }

    public ProfilesWindow()
    {
        InitializeComponent();

        // Convertisseur BoolToVisibility inversé
        Resources["InverseBoolVisibility"] = new InverseBoolToVisibilityConverter();
        Reload();
    }

    private void Reload() => ProfilesList.ItemsSource = ProfileService.LoadAll();

    private void BtnApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        var id = (sender as Button)?.Tag?.ToString();
        SelectedProfile = ProfileService.LoadAll().FirstOrDefault(p => p.Id == id);
        if (SelectedProfile != null)
        {
            DialogResult = true;
            Close();
        }
    }

    private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var id = (sender as Button)?.Tag?.ToString();
        if (id == null) return;
        var r = MessageBox.Show("Supprimer ce profil ?", "Confirmer",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r == MessageBoxResult.Yes)
        {
            ProfileService.DeleteCustom(id);
            Reload();
        }
    }

    private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
    {
        var win = new NewProfileWindow { Owner = this };
        if (win.ShowDialog() == true && win.Result != null)
        {
            ProfileService.AddCustom(win.Result);
            Reload();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}

// Convertisseur WPF pour inverser bool → Visibility
public class InverseBoolToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c)
        => (v is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotImplementedException();
}
