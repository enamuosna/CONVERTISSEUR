namespace MXFConverter.Models;

public class ConversionProfile
{
    public string Id           { get; set; } = Guid.NewGuid().ToString();
    public string Name         { get; set; } = "Nouveau profil";
    public string Description  { get; set; } = "";
    public string Icon         { get; set; } = "🎬";
    public string OutputFormat { get; set; } = "MP4";
    public string QualityPreset{ get; set; } = "Haute qualité";
    public AdvancedOptions Advanced { get; set; } = new();
    public DateTime CreatedAt  { get; set; } = DateTime.Now;
    public bool IsBuiltIn      { get; set; } = false;

    // Profils prédéfinis
    public static List<ConversionProfile> GetBuiltInProfiles() => new()
    {
        new ConversionProfile
        {
            Name = "YouTube 4K",
            Description = "MP4 H.264 4K optimisé pour YouTube",
            Icon = "▶",
            OutputFormat = "MP4",
            QualityPreset = "Haute qualité",
            IsBuiltIn = true,
            Advanced = new AdvancedOptions { Resolution = "3840×2160 (4K)", Framerate = "30" }
        },
        new ConversionProfile
        {
            Name = "Instagram Story",
            Description = "MP4 1080p 30fps vertical",
            Icon = "📱",
            OutputFormat = "MP4",
            QualityPreset = "Qualité standard",
            IsBuiltIn = true,
            Advanced = new AdvancedOptions { Resolution = "1080×1920 (Portrait)", Framerate = "30" }
        },
        new ConversionProfile
        {
            Name = "Archive sans perte",
            Description = "MKV H.264 CRF 0 qualité maximale",
            Icon = "🗄",
            OutputFormat = "MKV",
            QualityPreset = "Sans perte",
            IsBuiltIn = true,
            Advanced = new AdvancedOptions { Resolution = "Original" }
        },
        new ConversionProfile
        {
            Name = "Partage web rapide",
            Description = "MP4 720p compressé pour le web",
            Icon = "🌐",
            OutputFormat = "MP4",
            QualityPreset = "Faible taille",
            IsBuiltIn = true,
            Advanced = new AdvancedOptions { Resolution = "1280×720 (HD)", Framerate = "25" }
        },
        new ConversionProfile
        {
            Name = "Audio MP3 HQ",
            Description = "Extraction audio MP3 320kbps",
            Icon = "🎵",
            OutputFormat = "MP3",
            QualityPreset = "Haute qualité",
            IsBuiltIn = true,
            Advanced = new AdvancedOptions { RemoveAudio = false, UseCustomAudioBitrate = true, AudioBitrate = 320 }
        },
        new ConversionProfile
        {
            Name = "H.265 HEVC 4K",
            Description = "HEVC 4K ultra-compressé haute qualité",
            Icon = "⚡",
            OutputFormat = "MP4",
            QualityPreset = "H.265 (HEVC)",
            IsBuiltIn = true,
            Advanced = new AdvancedOptions { Resolution = "3840×2160 (4K)" }
        },
    };
}
