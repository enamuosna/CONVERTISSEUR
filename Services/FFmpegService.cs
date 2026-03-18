using System.IO;
using MXFConverter.Models;
using Xabe.FFmpeg;

namespace MXFConverter.Services;

public static class FFmpegService
{
    // Dictionnaire des formats supportés avec leurs extensions
    public static readonly Dictionary<string, string> SupportedFormats = new()
    {
        { "MP4",  ".mp4"  },
        { "MOV",  ".mov"  },
        { "MKV",  ".mkv"  },
        { "AVI",  ".avi"  },
        { "WMV",  ".wmv"  },
        { "FLV",  ".flv"  },
        { "WEBM", ".webm" },
        { "TS",   ".ts"   },
        { "MXF",  ".mxf"  },
        { "GIF",  ".gif"  },
        { "MP3",  ".mp3"  },
        { "AAC",  ".aac"  },
        { "WAV",  ".wav"  },
        { "FLAC", ".flac" },
    };

    // Préréglages qualité
    public static readonly Dictionary<string, (string VideoCodec, string CRF, string Preset, string AudioBitrate)> QualityPresets = new()
    {
        { "Haute qualité",    ("libx264", "18", "slow",   "320k") },
        { "Qualité standard", ("libx264", "23", "medium", "192k") },
        { "Faible taille",    ("libx264", "28", "fast",   "128k") },
        { "Sans perte",       ("libx264", "0",  "veryslow","320k") },
        { "H.265 (HEVC)",     ("libx265", "20", "slow",   "256k") },
    };

    public static async Task<IMediaInfo> GetMediaInfoAsync(string filePath)
    {
        return await FFmpeg.GetMediaInfo(filePath);
    }

    public static async Task ConvertAsync(
        ConversionItem item,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var ext = SupportedFormats.GetValueOrDefault(item.OutputFormat, ".mp4");
        var outputFileName = Path.GetFileNameWithoutExtension(item.InputPath) + "_converted" + ext;
        var outputPath = Path.Combine(item.OutputDirectory, outputFileName);
        item.OutputPath = outputPath;

        // Formats audio seulement
        var audioOnlyFormats = new HashSet<string> { "MP3", "AAC", "WAV", "FLAC" };
        bool isAudioOnly = audioOnlyFormats.Contains(item.OutputFormat);

        var preset = QualityPresets.GetValueOrDefault(item.QualityPreset, QualityPresets["Haute qualité"]);
        var mediaInfo = await FFmpeg.GetMediaInfo(item.InputPath, cancellationToken);

        IConversion conversion;

        if (isAudioOnly)
        {
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();
            if (audioStream == null)
                throw new InvalidOperationException("Aucune piste audio trouvée dans le fichier source.");

            conversion = FFmpeg.Conversions.New()
                .AddStream(audioStream)
                .SetOutput(outputPath)
                .SetOverwriteOutput(true);

            // Bitrate audio
            conversion.AddParameter($"-b:a {preset.AudioBitrate}");
        }
        else
        {
            var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();

            conversion = FFmpeg.Conversions.New().SetOverwriteOutput(true);

            if (videoStream != null)
            {
                videoStream.SetCodec(preset.VideoCodec);
                conversion.AddStream(videoStream);
                conversion.AddParameter($"-crf {preset.CRF}");
                conversion.AddParameter($"-preset {preset.Preset}");
            }

            if (audioStream != null)
            {
                audioStream.SetCodec(preset.VideoCodec == "libx265" ? "aac" : "aac");
                conversion.AddStream(audioStream);
                conversion.AddParameter($"-b:a {preset.AudioBitrate}");
            }

            conversion.SetOutput(outputPath);
        }

        if (progress != null)
        {
            conversion.OnProgress += (sender, args) =>
            {
                progress.Report(args.Percent);
            };
        }

        await conversion.Start(cancellationToken);
    }

    public static string[] GetSupportedInputExtensions()
    {
        return new[]
        {
            ".mxf", ".mp4", ".mov", ".mkv", ".avi", ".wmv",
            ".flv", ".webm", ".ts", ".m2ts", ".mpg", ".mpeg",
            ".mp3", ".aac", ".wav", ".flac", ".ogg", ".m4a"
        };
    }
}
