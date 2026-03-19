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
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MXFConverter.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<ConversionItem> Items { get; } = new();
    public List<string> Formats        { get; } = FFmpegService.SupportedFormats.Keys.ToList();
    public List<string> QualityPresets { get; } = FFmpegService.QualityPresets.Keys.ToList();

    private bool _isDragOver;
    public bool IsDragOver
    {
        get => _isDragOver;
        set { _isDragOver = value; OnPropertyChanged(); }
    }

    private AppSettings _settings;
    private string      _outputDirectory;

    // Conversions de la session courante (pour export rapport)
    private readonly List<ConversionRecord> _sessionRecords = new();

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
        HistoryService.Load();
        Items.CollectionChanged += (_, _) => RefreshUI();
    }

    // ═══ AJOUT FICHIERS ═══

    private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title       = "Sélectionner des fichiers vidéo",
            Multiselect = true,
            Filter      = "Vidéos|*.mxf;*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.flv;*.webm;*.ts|Tous|*.*"
        };
        if (dialog.ShowDialog() == true) await AddFilesAsync(dialog.FileNames);
    }

    private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Sélectionner un dossier" };
        if (dialog.ShowDialog() == true)
        {
            var exts  = FFmpegService.GetSupportedInputExtensions();
            var files = Directory.GetFiles(dialog.FolderName, "*.*", SearchOption.AllDirectories)
                                 .Where(f => exts.Contains(Path.GetExtension(f).ToLower())).ToArray();
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

            // Miniature en arrière-plan
            if (_settings.ShowThumbnails)
                _ = LoadThumbnailAsync(item);
        }

        RefreshUI();
        SetStatus($"{Items.Count} fichier(s) dans la file.");
    }

    private async Task LoadThumbnailAsync(ConversionItem item)
    {
        try
        {
            var bmp = await ThumbnailService.GetThumbnailAsync(item.InputPath);
            if (bmp != null)
                Dispatcher.Invoke(() =>
                {
                    item.Thumbnail    = bmp;
                    item.HasThumbnail = true;
                });
        }
        catch { }
    }

    // ═══ DRAG & DROP ═══

    private void Window_Drop(object sender, DragEventArgs e)
    {
        IsDragOver = false;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var exts  = FFmpegService.GetSupportedInputExtensions();
            _ = AddFilesAsync(files.Where(f => exts.Contains(Path.GetExtension(f).ToLower())));
        }
    }

    protected override void OnDragEnter(DragEventArgs e) { IsDragOver = true;  base.OnDragEnter(e); }
    protected override void OnDragLeave(DragEventArgs e) { IsDragOver = false; base.OnDragLeave(e); }

    // ═══ CONVERSION PARALLÈLE ═══

    private async void BtnConvertAll_Click(object sender, RoutedEventArgs e)
    {
        var pending = Items.Where(i => i.CanConvert).ToList();
        if (!pending.Any()) { SetStatus("Aucun fichier en attente."); return; }

        BtnConvertAll.IsEnabled = false;
        SetStatus($"Conversion de {pending.Count} fichier(s) — {_settings.MaxParallelConversions} en parallèle...");

        var sem = new SemaphoreSlim(_settings.MaxParallelConversions);
        await Task.WhenAll(pending.Select(async item =>
        {
            await sem.WaitAsync();
            try   { await ConvertItemAsync(item); }
            finally { sem.Release(); }
        }));

        BtnConvertAll.IsEnabled = true;
        RefreshSummary();

        int ok  = Items.Count(i => i.Status == ConversionStatus.Terminé);
        int err = Items.Count(i => i.Status == ConversionStatus.Erreur);
        SetStatus($"Terminé — {ok} réussi(s), {err} erreur(s)");

        if (_settings.AutoOpenOutputDir) OpenOutputFolder();

        // Rapport automatique si configuré
        if (_settings.GenerateReport && _sessionRecords.Any())
            AutoExportReport();

        // Arrêt PC si demandé
        if (_settings.ShutdownAfterAll && ok > 0)
        {
            MessageBox.Show("Conversion terminée. Le PC s'éteindra dans 30 secondes.",
                "Arrêt planifié", MessageBoxButton.OK, MessageBoxImage.Information);
            Process.Start("shutdown", "/s /t 30");
        }
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

        var sw    = Stopwatch.StartNew();
        var sizeIn = item.FileSizeBytes;

        try
        {
            var progress = new Progress<double>(p => Dispatcher.Invoke(() =>
            {
                item.Progress      = p;
                item.StatusMessage = $"Conversion : {p:F0}%";
                var secs = sw.Elapsed.TotalSeconds;
                if (secs > 0.5)
                    item.SpeedText = $"{sizeIn * (p / 100.0) / (1024.0 * 1024) / secs:F1} MB/s";
                RefreshGlobalProgress();
            }));

            await FFmpegService.ConvertAsync(item, progress, item.CancellationSource.Token);

            sw.Stop();
            item.Progress      = 100;
            item.Status        = ConversionStatus.Terminé;
            item.SpeedText     = "";
            item.ETA           = "";
            item.StatusMessage = $"✓ {Path.GetFileName(item.OutputPath)}";

            long outSize = 0;
            try { outSize = new FileInfo(item.OutputPath).Length; } catch { }

            var record = new ConversionRecord
            {
                InputPath       = item.InputPath,
                OutputPath      = item.OutputPath,
                InputFormat     = item.FileExtension,
                OutputFormat    = item.OutputFormat,
                QualityPreset   = item.QualityPreset,
                InputSizeBytes  = item.FileSizeBytes,
                OutputSizeBytes = outSize,
                Duration        = item.Duration,
                ConversionTime  = sw.Elapsed.TotalSeconds,
                Success         = true
            };
            HistoryService.Add(record);
            _sessionRecords.Add(record);

            if (_settings.DeleteSourceOnDone && File.Exists(item.InputPath))
                try { File.Delete(item.InputPath); } catch { }
        }
        catch (OperationCanceledException)
        {
            item.Status = ConversionStatus.Annulé;
            item.StatusMessage = "Annulé";
            item.SpeedText = ""; item.ETA = "";
        }
        catch (Exception ex)
        {
            item.Status = ConversionStatus.Erreur;
            item.StatusMessage = $"Erreur : {ex.Message}";
            item.SpeedText = ""; item.ETA = "";

            var record = new ConversionRecord
            {
                InputPath      = item.InputPath,
                InputFormat    = item.FileExtension,
                OutputFormat   = item.OutputFormat,
                InputSizeBytes = item.FileSizeBytes,
                Duration       = item.Duration,
                ConversionTime = sw.Elapsed.TotalSeconds,
                Success        = false,
                ErrorMessage   = ex.Message
            };
            HistoryService.Add(record);
            _sessionRecords.Add(record);
        }

        RefreshSummary();
    }

    private void BtnCancelOne_Click(object sender, RoutedEventArgs e)
        => Items.FirstOrDefault(i => i.Id == (sender as Button)?.Tag?.ToString())
                ?.CancellationSource?.Cancel();

    private void BtnRemoveOne_Click(object sender, RoutedEventArgs e)
    {
        var item = Items.FirstOrDefault(i => i.Id == (sender as Button)?.Tag?.ToString());
        if (item != null) { item.CancellationSource?.Cancel(); Items.Remove(item); }
    }

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var item = Items.FirstOrDefault(i => i.Id == (sender as Button)?.Tag?.ToString());
        if (item?.Status == ConversionStatus.Terminé && File.Exists(item.OutputPath))
            Process.Start(new ProcessStartInfo(item.OutputPath) { UseShellExecute = true });
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.CancellationSource?.Cancel();
        Items.Clear();
        SetStatus("File d'attente vidée.");
    }

    // ═══ OPTIONS AVANCÉES & MÉTADONNÉES ═══

    private void BtnAdvancedOptions_Click(object sender, RoutedEventArgs e)
    {
        var item = Items.FirstOrDefault(i => i.Id == (sender as Button)?.Tag?.ToString());
        if (item == null) return;
        var win = new AdvancedOptionsWindow(item.FileName, item.Advanced) { Owner = this };
        if (win.ShowDialog() == true) item.Advanced = win.Result;
    }

    private void BtnMetadata_Click(object sender, RoutedEventArgs e)
    {
        var item = Items.FirstOrDefault(i => i.Id == (sender as Button)?.Tag?.ToString());
        if (item == null) return;
        var win = new MetadataWindow(item) { Owner = this };
        win.ShowDialog();
    }

    private void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        var item = Items.FirstOrDefault(i => i.Id == (sender as Button)?.Tag?.ToString());
        if (item == null) return;

        if (!System.IO.File.Exists(item.InputPath))
        {
            MessageBox.Show("Le fichier source est introuvable.", "Prévisualisation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var win = new VideoPreviewWindow(item) { Owner = this };
        win.ShowDialog();

        // Si des points de rognage ont été définis, notifier
        if (win.TrimApplied)
            SetStatus($"✓ Rognage appliqué sur {item.FileName} : {win.ResultTrimStart:F1}s → {win.ResultTrimEnd:F1}s");
    }

    // ═══ FENÊTRES SECONDAIRES ═══

    private void BtnStats_Click(object sender, RoutedEventArgs e)
        => new StatisticsWindow { Owner = this }.ShowDialog();

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
        => new HistoryWindow { Owner = this }.ShowDialog();

    private void BtnScheduler_Click(object sender, RoutedEventArgs e)
    {
        var win = new SchedulerWindow { Owner = this };
        win.ConversionTriggered += () => Dispatcher.Invoke(() =>
        {
            SetStatus("⏰ Planificateur : démarrage automatique des conversions...");
            BtnConvertAll_Click(this, new RoutedEventArgs());
        });
        win.Show();

        // Afficher badge planificateur
        ScheduleBadge.Visibility = Visibility.Visible;
        TxtScheduleBadge.Text    = $"Planifié {win.Title}";
    }

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

    // ═══ EXPORT RAPPORT ═══

    private void BtnExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (!_sessionRecords.Any())
        {
            MessageBox.Show("Aucune conversion dans la session courante.", "Rapport",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title    = "Enregistrer le rapport",
            Filter   = "Rapport texte (*.txt)|*.txt|CSV (*.csv)|*.csv",
            FileName = $"rapport_session_{DateTime.Now:yyyyMMdd_HHmm}"
        };
        if (dialog.ShowDialog() != true) return;

        string content = dialog.FilterIndex == 2
            ? ReportService.GenerateCsv(_sessionRecords)
            : ReportService.GenerateTxt(_sessionRecords);

        ReportService.SaveReport(content, dialog.FileName);
        Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        SetStatus($"Rapport exporté : {Path.GetFileName(dialog.FileName)}");
    }

    private void AutoExportReport()
    {
        try
        {
            var name = $"rapport_{DateTime.Now:yyyyMMdd_HHmm}.{_settings.ReportFormat.ToLower()}";
            var path = Path.Combine(_outputDirectory, name);
            var content = _settings.ReportFormat == "CSV"
                ? ReportService.GenerateCsv(_sessionRecords)
                : ReportService.GenerateTxt(_sessionRecords);
            ReportService.SaveReport(content, path);
            SetStatus($"Rapport généré automatiquement : {name}");
        }
        catch { }
    }

    // ═══ DOSSIER SORTIE ═══

    private void BtnOutputDir_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Sélectionner le dossier de sortie" };
        if (dialog.ShowDialog() != true) return;
        _outputDirectory = dialog.FolderName;
        TxtOutputDir.Text = _outputDirectory;
        foreach (var item in Items.Where(i => i.CanConvert))
            item.OutputDirectory = _outputDirectory;
    }

    private void BtnOpenOutput_Click(object sender, RoutedEventArgs e) => OpenOutputFolder();

    private void OpenOutputFolder()
    {
        if (Directory.Exists(_outputDirectory))
            Process.Start(new ProcessStartInfo(_outputDirectory) { UseShellExecute = true });
    }

    // ═══ PARAMÈTRES GLOBAUX ═══

    private void CmbGlobalFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var f = CmbGlobalFormat.SelectedItem?.ToString();
        if (f != null) foreach (var i in Items.Where(x => x.CanConvert)) i.OutputFormat = f;
    }

    private void CmbGlobalQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var q = CmbGlobalQuality.SelectedItem?.ToString();
        if (q != null) foreach (var i in Items.Where(x => x.CanConvert)) i.QualityPreset = q;
    }

    // ═══ UI HELPERS ═══

    private void RefreshUI()
    {
        bool has = Items.Count > 0;
        DropZone.Visibility      = has ? Visibility.Collapsed : Visibility.Visible;
        FileListPanel.Visibility = has ? Visibility.Visible   : Visibility.Collapsed;
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
        double avg              = Items.Average(i => i.Progress);
        GlobalProgressBar.Value = avg;
        TxtGlobalProgress.Text  = $"Global : {avg:F0}%";
    }

    private void UpdateParallelInfo()
        => TxtParallelInfo.Text = $"{_settings.MaxParallelConversions} en parallèle";

    private void SetStatus(string msg) => TxtStatus.Text = msg;

    // ═══ FENÊTRE CUSTOM ═══

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.CancellationSource?.Cancel();
        Application.Current.Shutdown();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
