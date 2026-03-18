using System.Windows;
using System.Windows.Input;
using MXFConverter.Services;
using MessageBox = System.Windows.MessageBox;

namespace MXFConverter.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();
        LoadHistory();
    }

    private void LoadHistory()
    {
        var records = HistoryService.Load();
        if (records.Count == 0)
        {
            EmptyMsg.Visibility  = Visibility.Visible;
            HistoryList.Visibility = Visibility.Collapsed;
            TxtCount.Text = "0";
            TxtStats.Text = "Aucune conversion enregistrée";
        }
        else
        {
            EmptyMsg.Visibility  = Visibility.Collapsed;
            HistoryList.Visibility = Visibility.Visible;
            HistoryList.ItemsSource = records;
            TxtCount.Text = records.Count.ToString();

            var success      = records.Count(r => r.Success);
            var totalIn      = records.Sum(r => r.InputSizeBytes);
            var totalOut     = records.Sum(r => r.OutputSizeBytes);
            var ratio        = totalIn > 0 ? (double)totalOut / totalIn * 100 : 0;
            TxtStats.Text    = $"{success}/{records.Count} réussies  •  {FormatSize(totalIn)} → {FormatSize(totalOut)} ({ratio:F0}%)";
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show("Vider tout l'historique ?", "Confirmer",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r == MessageBoxResult.Yes)
        {
            HistoryService.Clear();
            LoadHistory();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private static string FormatSize(long b)
    {
        if (b < 1024 * 1024) return $"{b / 1024.0:F1} KB";
        if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
        return $"{b / (1024.0 * 1024 * 1024):F2} GB";
    }
}
