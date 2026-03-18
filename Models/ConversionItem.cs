using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace MXFConverter.Models;

public enum ConversionStatus { En_attente, En_cours, Terminé, Erreur, Annulé }

public class ConversionItem : INotifyPropertyChanged
{
    private double           _progress;
    private ConversionStatus _status        = ConversionStatus.En_attente;
    private string           _statusMessage = "En attente...";
    private string           _outputFormat  = "MP4";
    private string           _qualityPreset = "Haute qualité";
    private string           _eta           = "";
    private string           _speedText     = "";
    private DateTime         _startTime;

    public string   Id              { get; set; } = Guid.NewGuid().ToString();
    public string   InputPath       { get; set; } = string.Empty;
    public string   FileName        => Path.GetFileName(InputPath);
    public string   FileExtension   => Path.GetExtension(InputPath).TrimStart('.').ToUpper();
    public string   OutputDirectory { get; set; } = string.Empty;
    public string   OutputPath      { get; set; } = string.Empty;
    public long     FileSizeBytes   { get; set; }
    public string   FileSizeFormatted => FormatFileSize(FileSizeBytes);
    public TimeSpan Duration        { get; set; }
    public string   DurationFormatted => Duration.ToString(@"hh\:mm\:ss");
    public string   VideoInfo       { get; set; } = string.Empty;
    public AdvancedOptions Advanced { get; set; } = new();
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
        set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); UpdateEta(); }
    }
    public string ProgressText => $"{Progress:F0}%";

    public string ETA
    {
        get => _eta;
        set { _eta = value; OnPropertyChanged(); }
    }
    public string SpeedText
    {
        get => _speedText;
        set { _speedText = value; OnPropertyChanged(); }
    }

    public ConversionStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            if (value == ConversionStatus.En_cours) _startTime = DateTime.Now;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(CanConvert));
            OnPropertyChanged(nameof(CanCancel));
        }
    }
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string StatusColor => Status switch
    {
        ConversionStatus.Terminé  => "#22C55E",
        ConversionStatus.En_cours => "#3B82F6",
        ConversionStatus.Erreur   => "#EF4444",
        ConversionStatus.Annulé   => "#F59E0B",
        _                         => "#94A3B8"
    };
    public string StatusLabel => Status switch
    {
        ConversionStatus.En_attente => "En attente",
        ConversionStatus.En_cours   => "En cours",
        ConversionStatus.Terminé    => "Terminé",
        ConversionStatus.Erreur     => "Erreur",
        ConversionStatus.Annulé     => "Annulé",
        _                           => ""
    };

    public bool CanConvert => Status is ConversionStatus.En_attente or ConversionStatus.Erreur or ConversionStatus.Annulé;
    public bool CanCancel  => Status == ConversionStatus.En_cours;

    private void UpdateEta()
    {
        if (Status != ConversionStatus.En_cours || Progress <= 0) return;
        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        if (elapsed < 1) return;
        var remaining = elapsed / (Progress / 100.0) - elapsed;
        ETA = remaining > 0 ? $"~{TimeSpan.FromSeconds(remaining):mm\\:ss}" : "";
    }

    private static string FormatFileSize(long b)
    {
        if (b < 1024) return $"{b} B";
        if (b < 1024 * 1024) return $"{b / 1024.0:F1} KB";
        if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
        return $"{b / (1024.0 * 1024 * 1024):F2} GB";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
