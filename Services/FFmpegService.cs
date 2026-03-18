using System.IO;
using MXFConverter.Models;
using Xabe.FFmpeg;

namespace MXFConverter.Services;

public static class FFmpegService
{
    public static readonly Dictionary<string, string> SupportedFormats = new()
    {
        { "MP4",  ".mp4"  }, { "MOV",  ".mov"  }, { "MKV",  ".mkv"  },
        { "AVI",  ".avi"  }, { "WMV",  ".wmv"  }, { "FLV",  ".flv"  },
        { "WEBM", ".webm" }, { "TS",   ".ts"   }, { "MXF",  ".mxf"  },
        { "GIF",  ".gif"  }, { "MP3",  ".mp3"  }, { "AAC",  ".aac"  },
        { "WAV",  ".wav"  }, { "FLAC", ".flac" },
    };

    public static readonly Dictionary<string, (string VideoCodec, string CRF, string Preset, string AudioBitrate)> QualityPresets = new()
    {
        { "Haute qualité",    ("libx264", "18", "slow",    "320k") },
        { "Qualité standard", ("libx264", "23", "medium",  "192k") },
        { "Faible taille",    ("libx264", "28", "fast",    "128k") },
        { "Sans perte",       ("libx264", "0",  "veryslow","320k") },
        { "H.265 (HEVC)",     ("libx265", "20", "slow",    "256k") },
    };

    public static async Task<IMediaInfo> GetMediaInfoAsync(string filePath)
        => await FFmpeg.GetMediaInfo(filePath);

    public static async Task ConvertAsync(
        ConversionItem item,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var ext        = SupportedFormats.GetValueOrDefault(item.OutputFormat, ".mp4");
        var outName    = BuildOutputName(item);
        var outputPath = Path.Combine(item.OutputDirectory, outName + ext);
        item.OutputPath = outputPath;

        var audioOnlyFormats = new HashSet<string> { "MP3", "AAC", "WAV", "FLAC" };
        bool isAudioOnly = audioOnlyFormats.Contains(item.OutputFormat);

        var preset    = QualityPresets.GetValueOrDefault(item.QualityPreset, QualityPresets["Haute qualité"]);
        var mediaInfo = await FFmpeg.GetMediaInfo(item.InputPath, cancellationToken);
        var adv       = item.Advanced;

        var conversion = FFmpeg.Conversions.New().SetOverwriteOutput(true);

        // ── Vidéo ──
        if (!isAudioOnly && !adv.RemoveAudio)
        {
            var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
            if (videoStream != null)
            {
                videoStream.SetCodec(preset.VideoCodec);

                // Résolution
                ApplyResolution(videoStream, adv);

                // Framerate
                if (adv.Framerate != "Original" && double.TryParse(adv.Framerate, out var fps))
                    videoStream.SetFramerate(fps);

                // Rotation / flip
                ApplyRotation(videoStream, adv);

                // Rognage temporel
                if (adv.EnableTrim && adv.TrimEnd > adv.TrimStart)
                {
                    conversion.AddParameter($"-ss {adv.TrimStart:F3}");
                    conversion.AddParameter($"-to {adv.TrimEnd:F3}");
                }

                conversion.AddStream(videoStream);
                conversion.AddParameter(adv.UseCustomVideoBitrate
                    ? $"-b:v {adv.VideoBitrate}k"
                    : $"-crf {preset.CRF}");
                conversion.AddParameter($"-preset {preset.Preset}");
            }
        }

        // ── Audio ──
        if (!adv.RemoveAudio)
        {
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();
            if (audioStream != null)
            {
                audioStream.SetCodec("aac");
                conversion.AddStream(audioStream);
                conversion.AddParameter(adv.UseCustomAudioBitrate
                    ? $"-b:a {adv.AudioBitrate}k"
                    : $"-b:a {preset.AudioBitrate}");
            }
        }


        // ── Filigrane texte ──
        if (!isAudioOnly && adv.EnableWatermark && !string.IsNullOrWhiteSpace(adv.WatermarkText))
        {
            var pos = adv.WatermarkPos switch
            {
                "Haut-gauche"   => "x=20:y=20",
                "Haut-centre"   => "x=(w-text_w)/2:y=20",
                "Haut-droite"   => "x=w-text_w-20:y=20",
                "Centre"        => "x=(w-text_w)/2:y=(h-text_h)/2",
                "Bas-gauche"    => "x=20:y=h-text_h-20",
                "Bas-centre"    => "x=(w-text_w)/2:y=h-text_h-20",
                _               => "x=w-text_w-20:y=h-text_h-20" // Bas-droite
            };
            var alpha   = adv.WatermarkOpacity.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            // Echappement FFmpeg drawtext : ' et : doivent etre precedes de \\
            var escaped = adv.WatermarkText
                            .Replace("'", "\\'")
                            .Replace(":", "\\:");
            var vfParam = $"drawtext=text='{escaped}':fontsize={adv.WatermarkSize}:fontcolor={adv.WatermarkColor}@{alpha}:{pos}";
            conversion.AddParameter($"-vf \"{vfParam}\"");
        }

        conversion.SetOutput(outputPath);

        if (progress != null)
            conversion.OnProgress += (_, args) => progress.Report(args.Percent);

        await conversion.Start(cancellationToken);
    }

    // ── Helpers ──

    private static string BuildOutputName(ConversionItem item)
    {
        var name = Path.GetFileNameWithoutExtension(item.InputPath);
        return $"{name}_converted";
    }

    private static void ApplyResolution(IVideoStream vs, AdvancedOptions adv)
    {
        var (w, h) = adv.Resolution switch
        {
            "3840×2160 (4K)"  => (3840, 2160),
            "1920×1080 (FHD)" => (1920, 1080),
            "1280×720 (HD)"   => (1280, 720),
            "854×480 (SD)"    => (854,  480),
            "640×360"         => (640,  360),
            "Personnalisée"   => (adv.CustomWidth, adv.CustomHeight),
            _                 => (0, 0)
        };
        if (w > 0 && h > 0) vs.SetSize(w, h);
    }

    private static void ApplyRotation(IVideoStream vs, AdvancedOptions adv)
    {
        // Xabe.FFmpeg supporte SetRotate pour rotation simple
        // Pour flip, on utilise AddParameter plus bas
        switch (adv.Rotation)
        {
            case "90° (horaire)":         vs.Rotate(RotateDegrees.Clockwise);        break;
            case "180°":                  vs.Rotate(RotateDegrees.Invert);            break;
            case "270° (anti-horaire)":   vs.Rotate(RotateDegrees.CounterClockwise); break;
        }
    }

    public static string[] GetSupportedInputExtensions() =>
        new[] { ".mxf", ".mp4", ".mov", ".mkv", ".avi", ".wmv",
                ".flv", ".webm", ".ts", ".m2ts", ".mpg", ".mpeg",
                ".mp3", ".aac", ".wav", ".flac", ".ogg", ".m4a" };
}
