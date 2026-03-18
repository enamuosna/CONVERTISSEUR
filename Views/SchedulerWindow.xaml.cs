using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace MXFConverter.Views;

public partial class SchedulerWindow : Window
{
    // Callback déclenché à l'heure programmée
    public event Action? ConversionTriggered;

    private DispatcherTimer? _countdownTimer;
    private DateTime         _scheduledTime;
    private bool             _isScheduled;

    public SchedulerWindow()
    {
        InitializeComponent();
        BtnCancelSched.IsEnabled = false;

        // Proposer l'heure actuelle + 5 min par défaut
        TxtTime.Text = DateTime.Now.AddMinutes(5).ToString("HH:mm");
    }

    private void ChkEnable_Changed(object sender, RoutedEventArgs e)
    {
        bool enabled = ChkEnable.IsChecked == true;
        TxtTime.IsEnabled    = enabled;
        CmbRepeat.IsEnabled  = enabled;
    }

    private void BtnSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (ChkEnable.IsChecked != true)
        {
            ShowStatus("Activez le planificateur d'abord.", "#F59E0B");
            return;
        }

        if (!TimeSpan.TryParse(TxtTime.Text, out var time))
        {
            ShowStatus("Format invalide. Utilisez HH:mm (ex: 22:30)", "#EF4444");
            return;
        }

        var now       = DateTime.Now;
        _scheduledTime = new DateTime(now.Year, now.Month, now.Day, time.Hours, time.Minutes, 0);

        // Si l'heure est déjà passée aujourd'hui → demain
        if (_scheduledTime <= now)
            _scheduledTime = _scheduledTime.AddDays(1);

        _isScheduled = true;
        BtnCancelSched.IsEnabled = true;
        CountdownBorder.Visibility = Visibility.Visible;

        StartCountdown();
        ShowStatus($"Planifié pour {_scheduledTime:dd/MM/yyyy à HH:mm}", "#22C55E");
        DialogResult = true;
    }

    private void BtnCancelSchedule_Click(object sender, RoutedEventArgs e)
    {
        StopCountdown();
        _isScheduled                = false;
        BtnCancelSched.IsEnabled    = false;
        CountdownBorder.Visibility  = Visibility.Collapsed;
        ShowStatus("Planification annulée.", "#F59E0B");
    }

    private void StartCountdown()
    {
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            var remaining = _scheduledTime - DateTime.Now;

            if (remaining <= TimeSpan.Zero)
            {
                StopCountdown();
                TxtCountdown.Text = "Démarrage !";
                ConversionTriggered?.Invoke();

                if (CmbRepeat.SelectedIndex == 1)       // Chaque jour
                    _scheduledTime = _scheduledTime.AddDays(1);
                else if (CmbRepeat.SelectedIndex == 2)  // Chaque semaine
                    _scheduledTime = _scheduledTime.AddDays(7);
                else
                    _isScheduled = false;

                if (_isScheduled) StartCountdown();
            }
            else
            {
                TxtCountdown.Text = remaining.ToString(@"hh\:mm\:ss");
            }
        };
        _countdownTimer.Start();
    }

    private void StopCountdown()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
    }

    private void ShowStatus(string msg, string colorHex)
    {
        TxtScheduleStatus.Text = msg;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
    }

    protected override void OnClosed(EventArgs e)
    {
        // NE PAS stopper le timer — il doit continuer même fenêtre fermée
        // Le timer tourne dans le Dispatcher de la fenêtre, donc on ne l'arrête pas ici
        base.OnClosed(e);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
