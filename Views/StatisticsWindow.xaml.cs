using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using MXFConverter.Models;
using MXFConverter.Services;
using static System.Windows.Media.ColorConverter;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Size = System.Windows.Size;

namespace MXFConverter.Views;

public partial class StatisticsWindow : Window
{
    private readonly List<ConversionRecord> _records;

    // Palette couleurs
    private static readonly string[] Palette =
    {
        "#3B82F6","#22C55E","#F59E0B","#EF4444",
        "#A78BFA","#EC4899","#14B8A6","#F97316"
    };

    public StatisticsWindow()
    {
        InitializeComponent();
        _records = HistoryService.Load();
        Loaded  += (_, _) => BuildDashboard();
    }

    private void BuildDashboard()
    {
        if (!_records.Any())
        {
            KpiTotal.Text = "0"; KpiSuccess.Text = "0%";
            KpiData.Text  = "0"; KpiTime.Text    = "0s";
            return;
        }

        // ── KPI ──
        int total   = _records.Count;
        int success = _records.Count(r => r.Success);
        int errors  = total - success;
        double rate = (double)success / total * 100;
        long totalIn  = _records.Sum(r => r.InputSizeBytes);
        long totalOut = _records.Sum(r => r.OutputSizeBytes);
        double avgTime = _records.Average(r => r.ConversionTime);

        KpiTotal.Text      = total.ToString();
        KpiTotalSub.Text   = $"depuis {_records.Last().Date:dd/MM/yyyy}";
        KpiSuccess.Text    = $"{rate:F0}%";
        KpiSuccessSub.Text = $"{success} réussies / {errors} erreurs";
        KpiData.Text       = FormatSize(totalIn);
        KpiDataSub.Text    = $"{FormatSize(totalOut)} en sortie";
        KpiTime.Text       = avgTime < 60 ? $"{avgTime:F0}s" : $"{avgTime / 60:F1}min";
        KpiTimeSub.Text    = "par fichier";

        // ── Bar chart formats ──
        var byFormat = _records
            .Where(r => r.Success)
            .GroupBy(r => r.OutputFormat)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .ToList();

        DrawBarChart(BarChart, byFormat.Select(g => (g.Key, (double)g.Count())).ToList(), "fichiers");

        // ── Donut ──
        DrawDonut(DonutChart, success, errors);

        // ── Compression par format ──
        var comprByFormat = _records
            .Where(r => r.Success && r.InputSizeBytes > 0)
            .GroupBy(r => r.OutputFormat)
            .Select(g => (g.Key, g.Average(r => (double)r.OutputSizeBytes / r.InputSizeBytes * 100)))
            .OrderByDescending(x => x.Item2)
            .Take(7)
            .ToList();

        DrawBarChart(CompressionChart, comprByFormat, "%", altColor: "#A78BFA");

        // ── Top 5 fichiers ──
        var top5 = _records
            .Where(r => r.Success)
            .OrderByDescending(r => r.InputSizeBytes)
            .Take(5)
            .ToList();

        TopFilesPanel.ItemsSource = top5.Select(r => new
        {
            Name      = r.FileName,
            SizeIn    = r.InputSizeFmt,
            SizeOut   = r.OutputSizeFmt,
            Format    = r.OutputFormat,
            Ratio     = r.CompressionRatio,
            ConvTime  = r.ConvTimeFmt
        });

        TopFilesPanel.ItemTemplate = CreateTopFileTemplate();
    }

    private static void DrawBarChart(Canvas canvas, List<(string Label, double Value)> data,
                                     string unit, string altColor = "#3B82F6")
    {
        canvas.Children.Clear();
        if (!data.Any()) return;

        double maxVal   = data.Max(d => d.Value);
        double canvasW  = 0; // will be ActualWidth after render
        double canvasH  = canvas.Height;
        double barH     = (canvasH - 30) / data.Count - 6;
        double labelW   = 90;

        canvas.Dispatcher.InvokeAsync(() =>
        {
            canvasW = canvas.ActualWidth > 0 ? canvas.ActualWidth : 400;
            double maxBarW = canvasW - labelW - 60;

            for (int i = 0; i < data.Count; i++)
            {
                var (label, value) = data[i];
                double y      = i * (barH + 6);
                double barW   = maxVal > 0 ? value / maxVal * maxBarW : 0;
                string color  = Palette[i % Palette.Length];

                // Label
                var tb = new TextBlock
                {
                    Text       = label.Length > 10 ? label[..10] : label,
                    Foreground = new SolidColorBrush((Color)ConvertFromString("#94A3B8")),
                    FontSize   = 10,
                    Width      = labelW - 4,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Canvas.SetLeft(tb, 0);
                Canvas.SetTop(tb, y + barH / 2 - 6);
                canvas.Children.Add(tb);

                // Barre
                var rect = new Rectangle
                {
                    Width  = Math.Max(barW, 2),
                    Height = barH,
                    Fill   = new SolidColorBrush((Color)ConvertFromString(color)),
                    RadiusX = 3, RadiusY = 3
                };
                Canvas.SetLeft(rect, labelW);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);

                // Valeur
                var val = new TextBlock
                {
                    Text       = $"{value:F0} {unit}",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize   = 9,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(val, labelW + barW + 4);
                Canvas.SetTop(val, y + barH / 2 - 6);
                canvas.Children.Add(val);
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void DrawDonut(Canvas canvas, int success, int errors)
    {
        canvas.Children.Clear();
        int total = success + errors;
        if (total == 0) return;

        canvas.Dispatcher.InvokeAsync(() =>
        {
            double cx     = canvas.ActualWidth / 2;
            double cy     = canvas.ActualHeight / 2;
            double radius = Math.Min(cx, cy) - 10;
            double inner  = radius * 0.55;

            // Arcs
            DrawArc(canvas, cx, cy, radius, inner,
                    0, (double)success / total * 360, "#22C55E");
            DrawArc(canvas, cx, cy, radius, inner,
                    (double)success / total * 360, 360, "#EF4444");

            // Texte centre
            var tb = new TextBlock
            {
                Text       = $"{(double)success / total * 100:F0}%",
                FontSize   = 22, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ConvertFromString("#22C55E"))
            };
            Canvas.SetLeft(tb, cx - 22);
            Canvas.SetTop(tb, cy - 14);
            canvas.Children.Add(tb);

            // Légende
            double ly = canvas.ActualHeight - 28;
            AddLegend(canvas, 10,  ly, "#22C55E", $"Réussies ({success})");
            AddLegend(canvas, 130, ly, "#EF4444", $"Erreurs ({errors})");
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void DrawArc(Canvas canvas, double cx, double cy,
                                  double r, double innerR,
                                  double startDeg, double endDeg, string colorHex)
    {
        if (Math.Abs(endDeg - startDeg) < 0.5) return;

        double startRad = (startDeg - 90) * Math.PI / 180;
        double endRad   = (endDeg   - 90) * Math.PI / 180;

        bool isLarge = (endDeg - startDeg) > 180;

        var path = new System.Windows.Shapes.Path
        {
            Fill = new SolidColorBrush((Color)ConvertFromString(colorHex))
        };

        var geo = new PathGeometry();
        var fig = new PathFigure
        {
            StartPoint = new Point(cx + innerR * Math.Cos(startRad), cy + innerR * Math.Sin(startRad))
        };

        fig.Segments.Add(new LineSegment(
            new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad)), true));
        fig.Segments.Add(new ArcSegment(
            new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad)),
            new Size(r, r), 0, isLarge, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(
            new Point(cx + innerR * Math.Cos(endRad), cy + innerR * Math.Sin(endRad)), true));
        fig.Segments.Add(new ArcSegment(
            new Point(cx + innerR * Math.Cos(startRad), cy + innerR * Math.Sin(startRad)),
            new Size(innerR, innerR), 0, isLarge, SweepDirection.Counterclockwise, true));
        fig.IsClosed = true;
        geo.Figures.Add(fig);
        path.Data = geo;
        canvas.Children.Add(path);
    }

    private static void AddLegend(Canvas c, double x, double y, string color, string text)
    {
        var dot = new Ellipse
        {
            Width = 10, Height = 10,
            Fill  = new SolidColorBrush((Color)ConvertFromString(color))
        };
        Canvas.SetLeft(dot, x); Canvas.SetTop(dot, y + 2);
        c.Children.Add(dot);

        var tb = new TextBlock
        {
            Text       = text,
            Foreground = new SolidColorBrush((Color)ConvertFromString("#94A3B8")),
            FontSize   = 10
        };
        Canvas.SetLeft(tb, x + 14); Canvas.SetTop(tb, y);
        c.Children.Add(tb);
    }

    private static DataTemplate CreateTopFileTemplate()
    {
        var dt = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 6));
        factory.SetValue(Border.BackgroundProperty,
            new SolidColorBrush((Color)ConvertFromString("#0D1117")));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        factory.SetValue(Border.PaddingProperty, new Thickness(10, 7, 10, 7));

        var grid = new FrameworkElementFactory(typeof(Grid));

        var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        grid.AppendChild(col1);
        grid.AppendChild(col2);

        var nameBlock = new FrameworkElementFactory(typeof(TextBlock));
        nameBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        nameBlock.SetValue(TextBlock.ForegroundProperty,
            new SolidColorBrush((Color)ConvertFromString("#F1F5F9")));
        nameBlock.SetValue(TextBlock.FontSizeProperty, 12.0);
        nameBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        nameBlock.SetValue(Grid.ColumnProperty, 0);
        grid.AppendChild(nameBlock);

        var infoBlock = new FrameworkElementFactory(typeof(TextBlock));
        infoBlock.SetValue(Grid.ColumnProperty, 1);
        infoBlock.SetValue(TextBlock.ForegroundProperty,
            new SolidColorBrush((Color)ConvertFromString("#94A3B8")));
        infoBlock.SetValue(TextBlock.FontSizeProperty, 11.0);
        infoBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("SizeIn"));
        grid.AppendChild(infoBlock);

        factory.AppendChild(grid);
        dt.VisualTree = factory;
        return dt;
    }

    // ── Export ──

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title      = "Enregistrer le rapport CSV",
            Filter     = "Fichiers CSV (*.csv)|*.csv",
            FileName   = $"rapport_mxf_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialog.ShowDialog() == true)
        {
            ReportService.SaveReport(ReportService.GenerateCsv(_records), dialog.FileName);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName)
                { UseShellExecute = true });
        }
    }

    private void BtnExportTxt_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title    = "Enregistrer le rapport",
            Filter   = "Fichiers texte (*.txt)|*.txt",
            FileName = $"rapport_mxf_{DateTime.Now:yyyyMMdd_HHmm}.txt"
        };
        if (dialog.ShowDialog() == true)
        {
            ReportService.SaveReport(ReportService.GenerateTxt(_records), dialog.FileName);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName)
                { UseShellExecute = true });
        }
    }

    private static string FormatSize(long b)
    {
        if (b < 1024 * 1024)       return $"{b / 1024.0:F1} KB";
        if (b < 1024L * 1024*1024) return $"{b / (1024.0 * 1024):F1} MB";
        return $"{b / (1024.0 * 1024 * 1024):F2} GB";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
