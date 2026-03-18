using System.Windows;
using System.Windows.Input;
using MXFConverter.Models;

namespace MXFConverter.Views;

public partial class AdvancedOptionsWindow : Window
{
    public AdvancedOptions Result { get; private set; } = new();
    private readonly AdvancedOptions _original;

    public AdvancedOptionsWindow(string fileName, AdvancedOptions current)
    {
        InitializeComponent();
        _original    = current;
        TxtTitle.Text = $"Options avancées — {fileName}";

        // Initialiser les listes
        CmbResolution.ItemsSource  = AdvancedOptions.Resolutions;
        CmbFramerate.ItemsSource   = AdvancedOptions.Framerates;
        CmbRotation.ItemsSource    = AdvancedOptions.Rotations;

        LoadValues(current);
    }

    private void LoadValues(AdvancedOptions o)
    {
        CmbResolution.SelectedItem  = o.Resolution;
        CmbFramerate.SelectedItem   = o.Framerate;
        CmbRotation.SelectedItem    = o.Rotation;
        TxtWidth.Text               = o.CustomWidth.ToString();
        TxtHeight.Text              = o.CustomHeight.ToString();
        ChkVideoBitrate.IsChecked   = o.UseCustomVideoBitrate;
        TxtVideoBitrate.Text        = o.VideoBitrate.ToString();
        TxtVideoBitrate.IsEnabled   = o.UseCustomVideoBitrate;
        ChkAudioBitrate.IsChecked   = o.UseCustomAudioBitrate;
        TxtAudioBitrate.Text        = o.AudioBitrate.ToString();
        TxtAudioBitrate.IsEnabled   = o.UseCustomAudioBitrate;
        ChkTrim.IsChecked           = o.EnableTrim;
        TxtTrimStart.Text           = o.TrimStart.ToString("F0");
        TxtTrimEnd.Text             = o.TrimEnd.ToString("F0");
        TrimGrid.Visibility         = o.EnableTrim ? Visibility.Visible : Visibility.Collapsed;
        ChkRemoveAudio.IsChecked    = o.RemoveAudio;
        CustomResGrid.Visibility    = o.Resolution == "Personnalisée" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        Result = new AdvancedOptions
        {
            Resolution          = CmbResolution.SelectedItem?.ToString() ?? "Original",
            Framerate           = CmbFramerate.SelectedItem?.ToString()  ?? "Original",
            Rotation            = CmbRotation.SelectedItem?.ToString()   ?? "Aucune",
            CustomWidth         = int.TryParse(TxtWidth.Text,  out var w) ? w : 1920,
            CustomHeight        = int.TryParse(TxtHeight.Text, out var h) ? h : 1080,
            UseCustomVideoBitrate = ChkVideoBitrate.IsChecked == true,
            VideoBitrate        = int.TryParse(TxtVideoBitrate.Text, out var vb) ? vb : 5000,
            UseCustomAudioBitrate = ChkAudioBitrate.IsChecked == true,
            AudioBitrate        = int.TryParse(TxtAudioBitrate.Text, out var ab) ? ab : 192,
            EnableTrim          = ChkTrim.IsChecked == true,
            TrimStart           = double.TryParse(TxtTrimStart.Text, out var ts) ? ts : 0,
            TrimEnd             = double.TryParse(TxtTrimEnd.Text,   out var te) ? te : 0,
            RemoveAudio         = ChkRemoveAudio.IsChecked == true,
        };
        DialogResult = true;
        Close();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e) => LoadValues(new AdvancedOptions());
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void ChkVideoBitrate_Changed(object sender, RoutedEventArgs e)
        => TxtVideoBitrate.IsEnabled = ChkVideoBitrate.IsChecked == true;

    private void ChkAudioBitrate_Changed(object sender, RoutedEventArgs e)
        => TxtAudioBitrate.IsEnabled = ChkAudioBitrate.IsChecked == true;

    private void ChkTrim_Changed(object sender, RoutedEventArgs e)
        => TrimGrid.Visibility = ChkTrim.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
