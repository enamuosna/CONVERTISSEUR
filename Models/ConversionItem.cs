using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace MXFConverter.Models;

public enum ConversionStatus
{
    En_attente,
    En_cours,
    Terminé,
    Erreur,
    Annulé
}

public class ConversionItem : INotifyPropertyChanged
{
    private double _progress;
    private ConversionStatus _status = ConversionStatus.En_attente;
    private string _statusMessage = "En attente...";
    private string _outputFormat = "MP4";
    private string _qualityPreset = "Haute qualité";

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string InputPath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(InputPath);
    public string FileExtension => Path.GetExtension(InputPath).TrimStart('.').ToUpper();
    public string OutputDirectory { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted => FormatFileSize(FileSizeBytes);
    public TimeSpan Duration { get; set; }
    public string DurationFormatted => Duration.ToString(@"hh\:mm\:ss");
    public string VideoInfo { get; set; } = string.Empty;
    public CancellationTokenSource? CancellationSource { get; set; }

    public string OutputFormat
    {
        get => _outputFormat;
        set { _outputFormat = value; OnPropertyChanged(); }
    }

    public string QualityPreset
    {
        get => _qualityPreset;
        set { _qualityPreset = value; OnPropertyChanged(); }
    }

    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
    }

    public string ProgressText => $"{Progress:F0}%";

    public ConversionStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(CanConvert)); OnPropertyChanged(nameof(CanCancel)); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string StatusColor => Status switch
    {
        ConversionStatus.Terminé => "#22C55E",
        ConversionStatus.En_cours => "#3B82F6",
        ConversionStatus.Erreur => "#EF4444",
        ConversionStatus.Annulé => "#F59E0B",
        _ => "#94A3B8"
    };

    public bool CanConvert => Status is ConversionStatus.En_attente or ConversionStatus.Erreur or ConversionStatus.Annulé;
    public bool CanCancel => Status == ConversionStatus.En_cours;

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
