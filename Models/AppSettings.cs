namespace MXFConverter.Models;

public class AppSettings
{
    public int    MaxParallelConversions { get; set; } = 2;
    public string DefaultFormat         { get; set; } = "MP4";
    public string DefaultQuality        { get; set; } = "Haute qualité";
    public string DefaultOutputDir      { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    public bool   AutoOpenOutputDir     { get; set; } = false;
    public bool   ShowNotifications     { get; set; } = true;
    public bool   DeleteSourceOnDone    { get; set; } = false;
    public bool   OverwriteExisting     { get; set; } = true;
    public string OutputNaming          { get; set; } = "{name}_converted";
    public bool   ShowThumbnails        { get; set; } = true;
    public bool   MinimizeToTray        { get; set; } = false;
    public bool   ShutdownAfterAll      { get; set; } = false;
    public string Theme                 { get; set; } = "Dark";   // Dark | Light
    public bool   GenerateReport        { get; set; } = false;
    public string ReportFormat          { get; set; } = "CSV";    // CSV | TXT
}
