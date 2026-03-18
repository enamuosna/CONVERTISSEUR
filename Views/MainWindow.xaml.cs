using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MXFConverter.Models;
using MXFConverter.Services;
using Microsoft.Win32;

namespace MXFConverter.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ===== DONNÉES LIÉES =====
    public ObservableCollection<ConversionItem> Items { get; } = new();
    public List<string> Formats { get; } = FFmpegService.SupportedFormats.Keys.ToList();
    public List<string> QualityPresets { get; } = FFmpegService.QualityPresets.Keys.ToList();

    private bool _isDragOver;
    public bool IsDragOver
    {
        get => _isDragOver;
        set { _isDragOver = value; OnPropertyChanged(); }
    }

    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Initialiser les ComboBox globaux
        CmbGlobalFormat.ItemsSource  = Formats;
        CmbGlobalFormat.SelectedItem = "MP4";
        CmbGlobalQuality.ItemsSource  = QualityPresets;
        CmbGlobalQuality.SelectedItem = "Haute qualité";

        TxtOutputDir.Text = _outputDirectory;

        Items.CollectionChanged += (_, _) => RefreshUI();
    }

    // ===== AJOUT DE FICHIERS =====

    private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title      = "Sélectionner des fichiers vidéo",
            Multiselect = true,
            Filter     = "Vidéos (*.mxf;*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.flv;*.webm;*.ts)|*.mxf;*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.flv;*.webm;*.ts|Tous les fichiers|*.*"
        };

        if (dialog.ShowDialog() == true)
            await AddFilesAsync(dialog.FileNames);
    }

    private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Sélectionner un dossier" };
        if (dialog.ShowDialog() == true)
        {
            var extensions = FFmpegService.GetSupportedInputExtensions();
            var files = Directory.GetFiles(dialog.FolderName, "*.*", SearchOption.AllDirectories)
                                 .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                                 .ToArray();
            await AddFilesAsync(files);
        }
    }

    private async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
        SetStatus("Analyse des fichiers...");
        var format  = CmbGlobalFormat.SelectedItem?.ToString()  ?? "MP4";
        var quality = CmbGlobalQuality.SelectedItem?.ToString() ?? "Haute qualité";

        foreach (var path in filePaths)
        {
            if (Items.Any(i => i.InputPath == path)) continue;

            var item = new ConversionItem
            {
                InputPath       = path,
                OutputDirectory = _outputDirectory,
                OutputFormat    = format,
                QualityPreset   = quality,
                FileSizeBytes   = new FileInfo(path).Length
            };

            // Charger infos média en arrière-plan
            try
            {
                var info = await FFmpegService.GetMediaInfoAsync(path);
                item.Duration = info.Duration;
                var vid = info.VideoStreams.FirstOrDefault();
                if (vid != null)
                    item.VideoInfo = $"{vid.Width}×{vid.Height}  {vid.Codec.ToUpper()}  {vid.Framerate:F1} fps";
            }
            catch { item.VideoInfo = "Infos indisponibles"; }

            Items.Add(item);
        }

        RefreshUI();
        SetStatus($"{Items.Count} fichier(s) dans la file d'attente.");
    }

    // ===== DRAG & DROP =====

    private void Window_Drop(object sender, DragEventArgs e)
    {
        IsDragOver = false;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var validFiles = files.Where(f =>
                FFmpegService.GetSupportedInputExtensions()
                             .Contains(Path.GetExtension(f).ToLower())).ToArray();
            _ = AddFilesAsync(validFiles);
        }
    }

    protected override void OnDragEnter(DragEventArgs e) { IsDragOver = true;  base.OnDragEnter(e); }
    protected override void OnDragLeave(DragEventArgs e) { IsDragOver = false; base.OnDragLeave(e); }

    // ===== CONVERSION =====

    private async void BtnConvertAll_Click(object sender, RoutedEventArgs e)
    {
        var pending = Items.Where(i => i.CanConvert).ToList();
        if (!pending.Any())
        {
            SetStatus("Aucun fichier en attente.");
            return;
        }

        BtnConvertAll.IsEnabled = false;
        SetStatus($"Conversion de {pending.Count} fichier(s)...");

        foreach (var item in pending)
            await ConvertItemAsync(item);

        BtnConvertAll.IsEnabled = true;
        RefreshSummary();
        SetStatus("Toutes les conversions sont terminées.");
    }

    private async void BtnConvertOne_Click(object sender, RoutedEventArgs e)
    {
        var id   = (sender as Button)?.Tag?.ToString();
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item != null) await ConvertItemAsync(item);
    }

    private async Task ConvertItemAsync(ConversionItem item)
    {
        item.CancellationSource = new CancellationTokenSource();
        item.Status             = ConversionStatus.En_cours;
        item.Progress           = 0;
        item.StatusMessage      = "Conversion en cours...";

        try
        {
            var progress = new Progress<double>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    item.Progress      = p;
                    item.StatusMessage = $"Conversion : {p:F0}%";
                    RefreshGlobalProgress();
                });
            });

            await FFmpegService.ConvertAsync(item, progress, item.CancellationSource.Token);

            item.Progress      = 100;
            item.Status        = ConversionStatus.Terminé;
            item.StatusMessage = $"✓ {Path.GetFileName(item.OutputPath)}";
        }
        catch (OperationCanceledException)
        {
            item.Status        = ConversionStatus.Annulé;
            item.StatusMessage = "Annulé par l'utilisateur";
        }
        catch (Exception ex)
        {
            item.Status        = ConversionStatus.Erreur;
            item.StatusMessage = $"Erreur : {ex.Message}";
        }

        RefreshSummary();
    }

    private void BtnCancelOne_Click(object sender, RoutedEventArgs e)
    {
        var id   = (sender as Button)?.Tag?.ToString();
        var item = Items.FirstOrDefault(i => i.Id == id);
        item?.CancellationSource?.Cancel();
    }

    private void BtnRemoveOne_Click(object sender, RoutedEventArgs e)
    {
        var id   = (sender as Button)?.Tag?.ToString();
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item != null)
        {
            item.CancellationSource?.Cancel();
            Items.Remove(item);
        }
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
            item.CancellationSource?.Cancel();
        Items.Clear();
        SetStatus("File d'attente vidée.");
    }

    // ===== PARAMÈTRES GLOBAUX =====

    private void CmbGlobalFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var format = CmbGlobalFormat.SelectedItem?.ToString();
        if (format == null) return;
        foreach (var item in Items.Where(i => i.CanConvert))
            item.OutputFormat = format;
    }

    private void CmbGlobalQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var quality = CmbGlobalQuality.SelectedItem?.ToString();
        if (quality == null) return;
        foreach (var item in Items.Where(i => i.CanConvert))
            item.QualityPreset = quality;
    }

    private void BtnOutputDir_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Sélectionner le dossier de sortie" };
        if (dialog.ShowDialog() == true)
        {
            _outputDirectory = dialog.FolderName;
            TxtOutputDir.Text = _outputDirectory;
            foreach (var item in Items.Where(i => i.CanConvert))
                item.OutputDirectory = _outputDirectory;
        }
    }

    // ===== UI HELPERS =====

    private void RefreshUI()
    {
        bool hasFiles  = Items.Count > 0;
        DropZone.Visibility     = hasFiles ? Visibility.Collapsed : Visibility.Visible;
        FileListPanel.Visibility = hasFiles ? Visibility.Visible  : Visibility.Collapsed;
        FileListBox.ItemsSource  = Items;
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        TxtTotalFiles.Text = $"{Items.Count} fichier(s)";
        TxtCompleted.Text  = $"{Items.Count(i => i.Status == ConversionStatus.Terminé)} terminé(s)";
        TxtPending.Text    = $"{Items.Count(i => i.Status == ConversionStatus.En_attente)} en attente";
        TxtErrors.Text     = $"{Items.Count(i => i.Status == ConversionStatus.Erreur)} erreur(s)";
        RefreshGlobalProgress();
    }

    private void RefreshGlobalProgress()
    {
        if (!Items.Any()) { GlobalProgressBar.Value = 0; TxtGlobalProgress.Text = ""; return; }
        double avg = Items.Average(i => i.Progress);
        GlobalProgressBar.Value  = avg;
        TxtGlobalProgress.Text   = $"Global : {avg:F0}%";
    }

    private void SetStatus(string msg) => TxtStatus.Text = msg;

    // ===== FENÊTRE CUSTOM =====

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items) item.CancellationSource?.Cancel();
        Application.Current.Shutdown();
    }

    // ===== INPC =====
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
