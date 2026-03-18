using System.IO;

namespace MXFConverter.Models;

public class ConversionRecord
{
    public string   Id             { get; set; } = Guid.NewGuid().ToString();
    public DateTime Date           { get; set; } = DateTime.Now;
    public string   InputPath      { get; set; } = string.Empty;
    public string   OutputPath     { get; set; } = string.Empty;
    public string   InputFormat    { get; set; } = string.Empty;
    public string   OutputFormat   { get; set; } = string.Empty;
    public string   QualityPreset  { get; set; } = string.Empty;
    public long     InputSizeBytes { get; set; }
    public long     OutputSizeBytes{ get; set; }
    public TimeSpan Duration       { get; set; }
    public double   ConversionTime { get; set; } // secondes
    public bool     Success        { get; set; }
    public string   ErrorMessage   { get; set; } = string.Empty;

    // Affichage
    public string FileName        => Path.GetFileName(InputPath);
    public string DateFormatted   => Date.ToString("dd/MM/yyyy HH:mm");
    public string DurationFmt     => Duration.ToString(@"hh\:mm\:ss");
    public string InputSizeFmt    => FormatSize(InputSizeBytes);
    public string OutputSizeFmt   => FormatSize(OutputSizeBytes);
    public string ConvTimeFmt     => ConversionTime < 60
                                        ? $"{ConversionTime:F0}s"
                                        : $"{ConversionTime / 60:F1}min";
    public string CompressionRatio => InputSizeBytes > 0
                                        ? $"{(double)OutputSizeBytes / InputSizeBytes * 100:F0}%"
                                        : "—";
    public string StatusIcon      => Success ? "✓" : "✗";
    public string StatusColor     => Success ? "#22C55E" : "#EF4444";

    private static string FormatSize(long b)
    {
        if (b <= 0) return "—";
        if (b < 1024 * 1024) return $"{b / 1024.0:F1} KB";
        if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
        return $"{b / (1024.0 * 1024 * 1024):F2} GB";
    }
}
