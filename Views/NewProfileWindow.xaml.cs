using System.Windows;
using System.Windows.Input;
using MXFConverter.Models;
using MXFConverter.Services;
using MessageBox = System.Windows.Forms.MessageBox;

namespace MXFConverter.Views;

public partial class NewProfileWindow : Window
{
    public ConversionProfile? Result { get; private set; }

    public NewProfileWindow()
    {
        InitializeComponent();
        CmbFormat.ItemsSource  = FFmpegService.SupportedFormats.Keys.ToList();
        CmbFormat.SelectedItem = "MP4";
        CmbQuality.ItemsSource = FFmpegService.QualityPresets.Keys.ToList();
        CmbQuality.SelectedItem = "Haute qualité";
    }

    private void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Veuillez saisir un nom de profil.", "Champ requis");
            return;
        }
        Result = new ConversionProfile
        {
            Name          = TxtName.Text.Trim(),
            Description   = TxtDesc.Text.Trim(),
            Icon          = string.IsNullOrWhiteSpace(TxtIcon.Text) ? "🎬" : TxtIcon.Text.Trim(),
            OutputFormat  = CmbFormat.SelectedItem?.ToString()  ?? "MP4",
            QualityPreset = CmbQuality.SelectedItem?.ToString() ?? "Haute qualité",
            IsBuiltIn     = false,
        };
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
