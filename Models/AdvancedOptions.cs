using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MXFConverter.Models;

public class AdvancedOptions : INotifyPropertyChanged
{
    // Résolution
    private string _resolution = "Original";
    public string Resolution
    {
        get => _resolution;
        set { _resolution = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCustomResolution)); }
    }

    private int _customWidth  = 1920;
    private int _customHeight = 1080;
    public int CustomWidth  { get => _customWidth;  set { _customWidth  = value; OnPropertyChanged(); } }
    public int CustomHeight { get => _customHeight; set { _customHeight = value; OnPropertyChanged(); } }
    public bool IsCustomResolution => Resolution == "Personnalisée";

    // Débit vidéo
    private bool   _useCustomVideoBitrate = false;
    private int    _videoBitrate = 5000; // kbps
    public bool UseCustomVideoBitrate
    {
        get => _useCustomVideoBitrate;
        set { _useCustomVideoBitrate = value; OnPropertyChanged(); }
    }
    public int VideoBitrate { get => _videoBitrate; set { _videoBitrate = value; OnPropertyChanged(); } }

    // Débit audio
    private bool _useCustomAudioBitrate = false;
    private int  _audioBitrate = 192; // kbps
    public bool UseCustomAudioBitrate
    {
        get => _useCustomAudioBitrate;
        set { _useCustomAudioBitrate = value; OnPropertyChanged(); }
    }
    public int AudioBitrate { get => _audioBitrate; set { _audioBitrate = value; OnPropertyChanged(); } }

    // Fréquence d'images
    private string _framerate = "Original";
    public string Framerate { get => _framerate; set { _framerate = value; OnPropertyChanged(); } }

    // Rognage temporel
    private bool   _enableTrim   = false;
    private double _trimStart    = 0;
    private double _trimEnd      = 0;
    public bool   EnableTrim  { get => _enableTrim;  set { _enableTrim  = value; OnPropertyChanged(); } }
    public double TrimStart   { get => _trimStart;   set { _trimStart   = value; OnPropertyChanged(); OnPropertyChanged(nameof(TrimStartFmt)); } }
    public double TrimEnd     { get => _trimEnd;     set { _trimEnd     = value; OnPropertyChanged(); OnPropertyChanged(nameof(TrimEndFmt)); } }
    public string TrimStartFmt => TimeSpan.FromSeconds(TrimStart).ToString(@"hh\:mm\:ss");
    public string TrimEndFmt   => TimeSpan.FromSeconds(TrimEnd).ToString(@"hh\:mm\:ss");

    // Rotation
    private string _rotation = "Aucune";
    public string Rotation { get => _rotation; set { _rotation = value; OnPropertyChanged(); } }

    // Audio
    private bool _removeAudio = false;
    public bool RemoveAudio { get => _removeAudio; set { _removeAudio = value; OnPropertyChanged(); } }

    // Listes de valeurs disponibles
    public static readonly string[] Resolutions = { "Original", "3840×2160 (4K)", "1920×1080 (FHD)", "1280×720 (HD)", "854×480 (SD)", "640×360", "Personnalisée" };
    public static readonly string[] Framerates  = { "Original", "60", "30", "25", "24", "15" };
    public static readonly string[] Rotations   = { "Aucune", "90° (horaire)", "180°", "270° (anti-horaire)", "Miroir H", "Miroir V" };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
