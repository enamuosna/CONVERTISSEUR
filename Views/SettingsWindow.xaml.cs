using System.Windows;
using System.Windows.Input;
using MXFConverter.Models;
using MXFConverter.Services;

namespace MXFConverter.Views;

public partial class SettingsWindow : Window
{
    private AppSettings _settings;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        _settings = current;

        // Formats et qualités
        CmbDefaultFormat.ItemsSource   = FFmpegService.SupportedFormats.Keys.ToList();
        CmbDefaultQuality.ItemsSource  = FFmpegService.QualityPresets.Keys.ToList();

        // Charger valeurs actuelles
        CmbParallel.SelectedIndex      = Math.Clamp(_settings.MaxParallelConversions - 1, 0, 3);
        CmbDefaultFormat.SelectedItem  = _settings.DefaultFormat;
        CmbDefaultQuality.SelectedItem = _settings.DefaultQuality;
        TxtNaming.Text                 = _settings.OutputNaming;
        ChkAutoOpen.IsChecked          = _settings.AutoOpenOutputDir;
        ChkOverwrite.IsChecked         = _settings.OverwriteExisting;
        ChkDeleteSource.IsChecked      = _settings.DeleteSourceOnDone;
        if (GetControl<System.Windows.Controls.CheckBox>("ChkThumbnails") is { } ct)
            ct.IsChecked = _settings.ShowThumbnails;
        if (GetControl<System.Windows.Controls.CheckBox>("ChkShutdown") is { } cs)
            cs.IsChecked = _settings.ShutdownAfterAll;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _settings.MaxParallelConversions = CmbParallel.SelectedIndex + 1;
        _settings.DefaultFormat          = CmbDefaultFormat.SelectedItem?.ToString() ?? "MP4";
        _settings.DefaultQuality         = CmbDefaultQuality.SelectedItem?.ToString() ?? "Haute qualité";
        _settings.OutputNaming           = TxtNaming.Text;
        _settings.AutoOpenOutputDir      = ChkAutoOpen.IsChecked == true;
        _settings.OverwriteExisting      = ChkOverwrite.IsChecked == true;
        _settings.DeleteSourceOnDone     = ChkDeleteSource.IsChecked == true;

        if (GetControl<System.Windows.Controls.CheckBox>("ChkThumbnails") is { } ct2)
            _settings.ShowThumbnails = ct2.IsChecked == true;
        if (GetControl<System.Windows.Controls.CheckBox>("ChkShutdown") is { } cs2)
            _settings.ShutdownAfterAll = cs2.IsChecked == true;
        if (GetControl<System.Windows.Controls.ComboBox>("CmbReportFormat") is { } cr)
        {
            _settings.GenerateReport  = cr.SelectedIndex > 0;
            _settings.ReportFormat    = cr.SelectedIndex == 2 ? "TXT" : "CSV";
        }
        SettingsService.Save(_settings);
        DialogResult = true;
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private T? GetControl<T>(string name) where T : System.Windows.DependencyObject
        => FindName(name) as T;
}
