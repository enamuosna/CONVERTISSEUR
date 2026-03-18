using System.IO;
using System.Text;
using MXFConverter.Models;

namespace MXFConverter.Services;

public static class ReportService
{
    public static string GenerateCsv(List<ConversionRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date;Fichier;Format Entrée;Format Sortie;Qualité;Taille Entrée;Taille Sortie;Compression;Durée vidéo;Temps conversion;Statut;Erreur");

        foreach (var r in records)
        {
            var ratio = r.InputSizeBytes > 0
                ? $"{(double)r.OutputSizeBytes / r.InputSizeBytes * 100:F0}%"
                : "—";
            sb.AppendLine(string.Join(";",
                r.Date.ToString("dd/MM/yyyy HH:mm:ss"),
                EscapeCsv(r.FileName),
                r.InputFormat,
                r.OutputFormat,
                EscapeCsv(r.QualityPreset),
                r.InputSizeFmt,
                r.OutputSizeFmt,
                ratio,
                r.DurationFmt,
                r.ConvTimeFmt,
                r.Success ? "Succès" : "Erreur",
                EscapeCsv(r.ErrorMessage)
            ));
        }
        return sb.ToString();
    }

    public static string GenerateTxt(List<ConversionRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine("                  MXF CONVERTER PRO — RAPPORT DE CONVERSION");
        sb.AppendLine($"                  Généré le {DateTime.Now:dd/MM/yyyy à HH:mm:ss}");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Statistiques globales
        var success  = records.Count(r => r.Success);
        var errors   = records.Count - success;
        var totalIn  = records.Sum(r => r.InputSizeBytes);
        var totalOut = records.Sum(r => r.OutputSizeBytes);
        var avgTime  = records.Any() ? records.Average(r => r.ConversionTime) : 0;

        sb.AppendLine("RÉSUMÉ GLOBAL");
        sb.AppendLine("─────────────────────────────────────────────────────────────────");
        sb.AppendLine($"  Total conversions  : {records.Count}");
        sb.AppendLine($"  Réussies           : {success}");
        sb.AppendLine($"  Erreurs            : {errors}");
        sb.AppendLine($"  Taille totale (IN) : {FormatSize(totalIn)}");
        sb.AppendLine($"  Taille totale (OUT): {FormatSize(totalOut)}");
        if (totalIn > 0)
            sb.AppendLine($"  Compression moy.   : {(double)totalOut / totalIn * 100:F0}%");
        sb.AppendLine($"  Temps moy./fichier : {avgTime:F0}s");
        sb.AppendLine();

        sb.AppendLine("DÉTAIL DES CONVERSIONS");
        sb.AppendLine("─────────────────────────────────────────────────────────────────");
        foreach (var r in records)
        {
            sb.AppendLine($"  [{(r.Success ? "✓" : "✗")}] {r.FileName}");
            sb.AppendLine($"      Format : {r.InputFormat} → {r.OutputFormat} ({r.QualityPreset})");
            sb.AppendLine($"      Taille : {r.InputSizeFmt} → {r.OutputSizeFmt}");
            sb.AppendLine($"      Durée  : {r.DurationFmt}  |  Conversion : {r.ConvTimeFmt}");
            sb.AppendLine($"      Date   : {r.DateFormatted}");
            if (!r.Success && !string.IsNullOrWhiteSpace(r.ErrorMessage))
                sb.AppendLine($"      Erreur : {r.ErrorMessage}");
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    public static void SaveReport(string content, string filePath)
        => File.WriteAllText(filePath, content, Encoding.UTF8);

    private static string EscapeCsv(string s)
        => string.IsNullOrWhiteSpace(s) ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

    private static string FormatSize(long b)
    {
        if (b < 1024 * 1024) return $"{b / 1024.0:F1} KB";
        if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
        return $"{b / (1024.0 * 1024 * 1024):F2} GB";
    }
}
