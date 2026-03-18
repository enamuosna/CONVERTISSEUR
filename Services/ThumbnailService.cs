using System.IO;
using System.Windows.Media.Imaging;
using Xabe.FFmpeg;

namespace MXFConverter.Services;

public static class ThumbnailService
{
    private static readonly string _cacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MXFConverter", "thumbnails");

    static ThumbnailService() => Directory.CreateDirectory(_cacheDir);

    public static async Task<BitmapImage?> GetThumbnailAsync(string videoPath, int widthPx = 120)
    {
        try
        {
            var hash    = Math.Abs(videoPath.GetHashCode()).ToString("X8");
            var outFile = Path.Combine(_cacheDir, $"{hash}.jpg");

            if (!File.Exists(outFile))
            {
                var info     = await FFmpeg.GetMediaInfo(videoPath);
                var duration = info.Duration.TotalSeconds;
                var seekPos  = Math.Min(duration * 0.1, 3.0); // 10% ou 3s

                var conversion = FFmpeg.Conversions.New()
                    .AddParameter($"-ss {seekPos:F3}")
                    .AddParameter($"-i \"{videoPath}\"")
                    .AddParameter($"-vframes 1")
                    .AddParameter($"-vf scale={widthPx}:-1")
                    .AddParameter($"-q:v 5")
                    .SetOutput(outFile)
                    .SetOverwriteOutput(true);

                await conversion.Start();
            }

            if (!File.Exists(outFile)) return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource        = new Uri(outFile);
            bmp.CacheOption      = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = widthPx;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static void ClearCache()
    {
        try
        {
            foreach (var f in Directory.GetFiles(_cacheDir, "*.jpg"))
                File.Delete(f);
        }
        catch { }
    }
}
