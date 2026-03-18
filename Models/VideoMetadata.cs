using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MXFConverter.Models;

public class VideoMetadata : INotifyPropertyChanged
{
    private string _title       = "";
    private string _author      = "";
    private string _description = "";
    private string _copyright   = "";
    private string _year        = "";
    private string _comment     = "";

    public string Title       { get => _title;       set { _title       = value; OnPropertyChanged(); } }
    public string Author      { get => _author;      set { _author      = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public string Copyright   { get => _copyright;   set { _copyright   = value; OnPropertyChanged(); } }
    public string Year        { get => _year;        set { _year        = value; OnPropertyChanged(); } }
    public string Comment     { get => _comment;     set { _comment     = value; OnPropertyChanged(); } }

    public bool HasAny => !string.IsNullOrWhiteSpace(Title)
                       || !string.IsNullOrWhiteSpace(Author)
                       || !string.IsNullOrWhiteSpace(Copyright);

    public string ToFFmpegArgs()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Title))       parts.Add($"-metadata title=\"{Title}\"");
        if (!string.IsNullOrWhiteSpace(Author))      parts.Add($"-metadata artist=\"{Author}\"");
        if (!string.IsNullOrWhiteSpace(Description)) parts.Add($"-metadata description=\"{Description}\"");
        if (!string.IsNullOrWhiteSpace(Copyright))   parts.Add($"-metadata copyright=\"{Copyright}\"");
        if (!string.IsNullOrWhiteSpace(Year))        parts.Add($"-metadata date=\"{Year}\"");
        if (!string.IsNullOrWhiteSpace(Comment))     parts.Add($"-metadata comment=\"{Comment}\"");
        return string.Join(" ", parts);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
