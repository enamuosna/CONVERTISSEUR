using System.IO;
using System.Text.Json;
using MXFConverter.Models;

namespace MXFConverter.Services;

public static class ProfileService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MXFConverter", "profiles.json");

    public static List<ConversionProfile> LoadAll()
    {
        var builtin = ConversionProfile.GetBuiltInProfiles();
        try
        {
            if (File.Exists(_path))
            {
                var json    = File.ReadAllText(_path);
                var custom  = JsonSerializer.Deserialize<List<ConversionProfile>>(json) ?? new();
                builtin.AddRange(custom);
            }
        }
        catch { }
        return builtin;
    }

    public static List<ConversionProfile> LoadCustom()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<List<ConversionProfile>>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    public static void SaveCustom(List<ConversionProfile> profiles)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch { }
    }

    public static void AddCustom(ConversionProfile p)
    {
        var list = LoadCustom();
        list.Add(p);
        SaveCustom(list);
    }

    public static void DeleteCustom(string id)
    {
        var list = LoadCustom().Where(p => p.Id != id).ToList();
        SaveCustom(list);
    }
}
