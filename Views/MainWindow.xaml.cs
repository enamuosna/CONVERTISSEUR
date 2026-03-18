using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MXFConverter.Models;
using MXFConverter.Services;

namespace MXFConverter.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ===== DONNÉES LIÉES =====
    public ObservableCollection<ConversionItem> Items { get; } = new();
    public List<string> Formats       { get; } = FFmpegService.SupportedFormats.Keys.ToList();
    public List<string> QualityPresets{ get; } = FFmpegService.QualityPresets.Keys.ToList();

    private bool _isDragOver;
    public bool IsDragOver
    {
        get => _isDragOver;
        set { _isDragOver = value; OnPropertyChanged(); }
    }

    private AppSettings _settings;
    private string _outputDirectory;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _settings        = SettingsService.Load();
        _outputDirectory = _settings.DefaultOutputDir;

        CmbGlobalFormat.ItemsSource   = Formats;
        CmbGlobalFormat.SelectedItem  = _settings.DefaultFormat;
        CmbGlobalQuality.ItemsSource  = QualityPresets;
        CmbGlobalQuality.SelectedItem = _settings.DefaultQuality;
        TxtOutputDir.Text             = _outputDirectory;

        UpdateParallelInfo();
        Items.CollectionChanged += (_, _) => RefreshUI();
        HistoryService.Load(); // pré-charger
    }

    // ===== AJOUT DE FICHIERS =====

    private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title       = "Sélectionner des fichiers vidéo",
            Multiselect = true,
            Filter      = "Vidéos (*.mxf;*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.flv;*.webm)|*.mxf;*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.flv;*.webm|Tous les fichiers|*.*"
        };
        if (dialog.ShowDialog() == true)
            await AddFilesAsync(dialog.FileNames);
    }

    private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Sélectionner un dossier" };
        if (dialog.ShowDialog() == true)
        {
            var exts  = FFmpegService.GetSupportedInputExtensions();
            var files = Directory.GetFiles(dialog.FolderName, "*.*", SearchOption.AllDirectories)
                                 .Where(f => exts.Contains(Path.GetExtension(f).ToLower()))
                                 .ToArray();
            await AddFilesAsync(files);
        }
    }

    private async Task AddFilesAsync(IEnumerable<string> paths)
    {
        SetStatus("Analyse des fichiers...");
        var format  = CmbGlobalFormat.SelectedItem?.ToString()  ?? "MP4";
        var quality = CmbGlobalQuality.SelectedItem?.ToString() ?? "Haute qualité";

        foreach (var path in paths)
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
            var valid = files.Where(f =>
                FFmpegService.GetSupportedInputExtensions()
                             .Contains(Path.GetExtension(f).ToLower())).ToArray();
            _ = AddFilesAsync(valid);
        }
    }

    protected override void OnDragEnter(DragEventArgs e) { IsDragOver = true;  base.OnDragEnter(e); }
    protected override void OnDragLeave(DragEventArgs e) { IsDragOver = false; base.OnDragLeave(e); }

    // ===== CONVERSION PARALLÈLE =====

    private async void BtnConvertAll_Click(object sender, RoutedEventArgs e)
    {
        var pending = Items.Where(i => i.CanConvert).ToList();
        if (!pending.Any()) { SetStatus("Aucun fichier en attente."); return; }

        BtnConvertAll.IsEnabled = false;
        SetStatus($"Conversion de {pending.Count} fichier(s) — {_settings.MaxParallelConversions} en parallèle...");

        // Sémaphore pour limiter la parallélisation
        var semaphore = new SemaphoreSlim(_settings.MaxParallelConversions);
        var tasks = pending.Select(async item =>
        {
            await semaphore.WaitAsync();
            try   { await ConvertItemAsync(item); }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);

        BtnConvertAll.IsEnabled = true;
        RefreshSummary();
        SetStatus($"Terminé — {Items.Count(i => i.Status == ConversionStatus.Terminé)} réussi(s), {Items.Count(i => i.Status == ConversionStatus.Erreur)} erreur(s)");

        if (_settings.AutoOpenOutputDir)
            OpenOutputFolder();
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
        item.Status        = ConversionStatus.En_cours;
        item.Progress      = 0;
        item.StatusMessage = "Initialisation...";
        item.SpeedText     = "";
        item.ETA           = "";

        var stopwatch    = Stopwatch.StartNew();
        var startSize    = item.FileSizeBytes;

        try
        {
            var progress = new Progress<double>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    item.Progress      = p;
                    item.StatusMessage = $"Conversion : {p:F0}%";

                    // Calcul vitesse en MB/s (approximation sur taille d'entrée)
                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    if (elapsed > 0.5)
                    {
                        var processed = startSize * (p / 100.0);
                        item.SpeedText = $"{processed / (1024.0 * 1024) / elapsed:F1} MB/s";
                    }

                    RefreshGlobalProgress();
                });
            });

            await FFmpegService.ConvertAsync(item, progress, item.CancellationSource.Token);
            stopwatch.Stop();

            // Succès
            item.Progress      = 100;
            item.Status        = ConversionStatus.Terminé;
            item.SpeedText     = "";
            item.ETA           = "";
            item.StatusMessage = $"✓ {Path.GetFileName(item.OutputPath)}";

            // Enregistrer dans l'historique
            long outSize = 0;
            try { outSize = new FileInfo(item.OutputPath).Length; } catch { }

            HistoryService.Add(new ConversionRecord
            {
                InputPath       = item.InputPath,
                OutputPath      = item.OutputPath,
                InputFormat     = item.FileExtension,
                OutputFormat    = item.OutputFormat,
                QualityPreset   = item.QualityPreset,
                InputSizeBytes  = item.FileSizeBytes,
                OutputSizeBytes = outSize,
                Duration        = item.Duration,
                ConversionTime  = stopwatch.Elapsed.TotalSeconds,
                Success         = true
            });

            // Supprimer source si paramètre activé
            if (_settings.DeleteSourceOnDone && File.Exists(item.InputPath))
            {
                try { File.Delete(item.InputPath); } catch { }
            }
        }
        catch (OperationCanceledException)
        {
            item.Status        = ConversionStatus.Annulé;
            item.StatusMessage = "Annulé par l'utilisateur";
            item.SpeedText     = "";
            item.ETA           = "";
        }
        catch (Exception ex)
        {
            item.Status        = ConversionStatus.Erreur;
            item.StatusMessage = $"Erreur : {ex.Message}";
            item.SpeedText     = "";
            item.ETA           = "";

            HistoryService.Add(new ConversionRecord
            {
                InputPath      = item.InputPath,
                InputFormat    = item.FileExtension,
                OutputFormat   = item.OutputFormat,
                InputSizeBytes = item.FileSizeBytes,
                Duration       = item.Duration,
                Success        = false,
                ErrorMessage   = ex.Message
            });
        }

        RefreshSummary();
    }

    private void BtnCancelOne_Click(object sender, RoutedEventArgs e)
    {
        var id   = (sender as Button)?.Tag?.ToString();
        Items.FirstOrDefault(i => i.Id == id)?.CancellationSource?.Cancel();
    }

    private void BtnRemoveOne_Click(object sender, RoutedEventArgs e)
    {
        var id   = (sender as Button)?.Tag?.ToString();
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item != null) { item.CancellationSource?.Cancel(); Items.Remove(item); }
    }

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var id   = (sender as Button)?.Tag?.ToString();
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item?.Status == ConversionStatus.Terminé && File.Exists(item.OutputPath))
            Process.Start(new ProcessStartInfo(item.OutputPath) { UseShellExecute = true });
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items) item.CancellationSource?.Cancel();
        Items.Clear();
        SetStatus("File d'attente vidée.");
    }

    // ===== OPTIONS AVANCÉES =====

    private void BtnAdvancedOptions_Click(object sender, RoutedEventArgs e)
    {
        var id   = (sender as Button)?.Tag?.ToString();
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item == null) return;

        var win = new AdvancedOptionsWindow(item.FileName, item.Advanced) { Owner = this };
        if (win.ShowDialog() == true)
            item.Advanced = win.Result;
    }

    // ===== PARAMÈTRES =====

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_settings) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _settings = SettingsService.Load();
            UpdateParallelInfo();
            SetStatus("Paramètres enregistrés.");
        }
    }

    // ===== HISTORIQUE =====

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        new HistoryWindow { Owner = this }.ShowDialog();
    }

    // ===== DOSSIER SORTIE =====

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

    private void BtnOpenOutput_Click(object sender, RoutedEventArgs e) => OpenOutputFolder();

    private void OpenOutputFolder()
    {
        if (Directory.Exists(_outputDirectory))
            Process.Start(new ProcessStartInfo(_outputDirectory) { UseShellExecute = true });
    }

    // ===== PARAMÈTRES GLOBAUX =====

    private void CmbGlobalFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var f = CmbGlobalFormat.SelectedItem?.ToString();
        if (f != null) foreach (var item in Items.Where(i => i.CanConvert)) item.OutputFormat = f;
    }

    private void CmbGlobalQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var q = CmbGlobalQuality.SelectedItem?.ToString();
        if (q != null) foreach (var item in Items.Where(i => i.CanConvert)) item.QualityPreset = q;
    }

    // ===== UI HELPERS =====

    private void RefreshUI()
    {
        bool hasFiles            = Items.Count > 0;
        DropZone.Visibility      = hasFiles ? Visibility.Collapsed : Visibility.Visible;
        FileListPanel.Visibility = hasFiles ? Visibility.Visible   : Visibility.Collapsed;
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
        double avg                = Items.Average(i => i.Progress);
        GlobalProgressBar.Value  = avg;
        TxtGlobalProgress.Text   = $"Global : {avg:F0}%";
    }

    private void UpdateParallelInfo()
        => TxtParallelInfo.Text = $"{_settings.MaxParallelConversions} conversion(s) en parallèle";

    private void SetStatus(string msg) => TxtStatus.Text = msg;

    // ===== FENÊTRE CUSTOM =====

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
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
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
