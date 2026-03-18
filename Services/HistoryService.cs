using System.IO;
using System.Text.Json;
using MXFConverter.Models;

namespace MXFConverter.Services;

public static class HistoryService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MXFConverter", "history.json");

    private static List<ConversionRecord> _cache = new();

    public static List<ConversionRecord> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                _cache = JsonSerializer.Deserialize<List<ConversionRecord>>(json) ?? new();
            }
        }
        catch { _cache = new(); }
        return _cache;
    }

    public static void Add(ConversionRecord record)
    {
        _cache.Insert(0, record); // plus récent en premier
        if (_cache.Count > 500) _cache.RemoveAt(_cache.Count - 1);
        Save();
    }

    public static void Clear()
    {
        _cache.Clear();
        Save();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch { }
    }
}
