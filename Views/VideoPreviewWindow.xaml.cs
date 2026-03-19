using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using MXFConverter.Models;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MXFConverter.Views;

public partial class VideoPreviewWindow : Window
{
    private readonly ConversionItem _item;
    private bool   _isPlaying       = false;
    private bool   _isDraggingSeek  = false;
    private bool   _isDraggingStart = false;
    private bool   _isDraggingEnd   = false;
    private double _totalSeconds    = 0;
    private double _trimStartSec    = 0;
    private double _trimEndSec      = 0;
    private bool   _mediaLoaded     = false;

    private readonly DispatcherTimer _timer;

    public double ResultTrimStart { get; private set; }
    public double ResultTrimEnd   { get; private set; }
    public bool   TrimApplied     { get; private set; }

    public VideoPreviewWindow(ConversionItem item)
    {
        InitializeComponent();
        _item = item;

        _trimStartSec = item.Advanced.TrimStart;
        _trimEndSec   = item.Advanced.TrimEnd;

        TxtWindowTitle.Text = item.FileName;
        TxtResolution.Text  = item.VideoInfo.Length > 0 ? item.VideoInfo.Split(' ')[0] : "Vidéo";
        TxtCodecInfo.Text   = item.FileExtension;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += Timer_Tick;

        PreviewKeyDown += OnKeyDown;
        Focusable       = true;

        Loaded += (_, _) => { Focus(); LoadVideo(); };
    }

    private void LoadVideo()
    {
        try
        {
            TxtLoading.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            Player.Source = new Uri(_item.InputPath);
            Player.Volume = SldVolume.Value;
            Player.Play();
            Player.Pause();
        }
        catch (Exception ex) { ShowError($"Impossible de charger : {ex.Message}"); }
    }

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        _totalSeconds = Player.NaturalDuration.HasTimeSpan
            ? Player.NaturalDuration.TimeSpan.TotalSeconds : 0;
        _mediaLoaded          = true;
        TxtLoading.Visibility = Visibility.Collapsed;
        if (_trimEndSec <= 0 || _trimEndSec > _totalSeconds) _trimEndSec = _totalSeconds;
        TxtTrimStart.Text = SecsToStr(_trimStartSec);
        TxtTrimEnd.Text   = SecsToStr(_trimEndSec);
        UpdateTrimDisplay();
        BuildTimeLabels();
        _timer.Start();
        SetStatus($"Durée totale : {SecsToTime(_totalSeconds)}");
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e)
    {
        _isPlaying = false; TxtPlayIcon.Text = "▶";
        PlayingBadge.Visibility = Visibility.Collapsed;
        Player.Position = TimeSpan.Zero;
    }

    private void Player_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        => ShowError($"Erreur lecture : {e.ErrorException?.Message}");

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_mediaLoaded || _isDraggingSeek) return;
        var pos = Player.Position.TotalSeconds;
        UpdateTimecode(pos);
        UpdatePlayHead(pos);
    }

    private void UpdateTimecode(double pos)
    {
        var posTs   = TimeSpan.FromSeconds(pos);
        var totalTs = TimeSpan.FromSeconds(_totalSeconds);
        TxtTimecode.Text      = $"{posTs:hh\\:mm\\:ss} / {totalTs:hh\\:mm\\:ss}";
        TxtTimecodeRight.Text = $"{posTs:hh\\:mm\\:ss\\.fff} / {totalTs:hh\\:mm\\:ss}";
    }

    private void UpdatePlayHead(double posSecs)
    {
        if (_totalSeconds <= 0) return;
        Canvas.SetLeft(PlayHead, posSecs / _totalSeconds * GetTimelineWidth() - 1.5);
    }

    // ═══ TIMELINE ═══

    private void Timeline_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSeek = true;
        SeekToMouse(e.GetPosition(TimelineCanvas).X);
        TimelineCanvas.CaptureMouse();
    }

    private void Timeline_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingSeek && e.LeftButton == MouseButtonState.Pressed)
            SeekToMouse(e.GetPosition(TimelineCanvas).X);
        else if (_isDraggingStart && e.LeftButton == MouseButtonState.Pressed)
        {
            _trimStartSec = Math.Max(0, Math.Min(XToSecs(e.GetPosition(TimelineCanvas).X), _trimEndSec - 0.1));
            TxtTrimStart.Text = SecsToStr(_trimStartSec);
            UpdateTrimDisplay();
        }
        else if (_isDraggingEnd && e.LeftButton == MouseButtonState.Pressed)
        {
            _trimEndSec = Math.Max(_trimStartSec + 0.1, Math.Min(XToSecs(e.GetPosition(TimelineCanvas).X), _totalSeconds));
            TxtTrimEnd.Text = SecsToStr(_trimEndSec);
            UpdateTrimDisplay();
        }
    }

    private void Timeline_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSeek = _isDraggingStart = _isDraggingEnd = false;
        TimelineCanvas.ReleaseMouseCapture();
    }

    private void MarkerStart_MouseDown(object sender, MouseButtonEventArgs e)
    { _isDraggingStart = true; e.Handled = true; TimelineCanvas.CaptureMouse(); }

    private void MarkerEnd_MouseDown(object sender, MouseButtonEventArgs e)
    { _isDraggingEnd = true; e.Handled = true; TimelineCanvas.CaptureMouse(); }

    private void SeekToMouse(double x)
    {
        if (_totalSeconds <= 0) return;
        Player.Position = TimeSpan.FromSeconds(Math.Max(0, Math.Min(XToSecs(x), _totalSeconds)));
        UpdateTimecode(Player.Position.TotalSeconds);
        UpdatePlayHead(Player.Position.TotalSeconds);
    }

    private double XToSecs(double x) => GetTimelineWidth() > 0 ? (x / GetTimelineWidth()) * _totalSeconds : 0;
    private double SecsToX(double secs) => _totalSeconds > 0 ? (secs / _totalSeconds) * GetTimelineWidth() : 0;

    private void UpdateTrimDisplay()
    {
        if (!_mediaLoaded) return;
        Canvas.SetLeft(MarkerStart, SecsToX(_trimStartSec) - 1.5);
        Canvas.SetLeft(MarkerEnd,   SecsToX(_trimEndSec)   - 1.5);
        Canvas.SetLeft(TrimZone, SecsToX(_trimStartSec));
        TrimZone.Width       = Math.Max(0, SecsToX(_trimEndSec) - SecsToX(_trimStartSec));
        double dur           = _trimEndSec - _trimStartSec;
        TxtTrimDuration.Text = dur > 0 ? SecsToTime(dur) : "--:--:--";
        TxtStartLabel.Text   = SecsToTime(_trimStartSec);
        TxtEndLabel.Text     = SecsToTime(_trimEndSec);
        TipStart.Text        = $"IN : {SecsToTime(_trimStartSec)}";
        TipEnd.Text          = $"OUT : {SecsToTime(_trimEndSec)}";
    }

    private void BuildTimeLabels()
    {
        TimeLabels.Children.Clear();
        if (_totalSeconds <= 0) return;
        double step = _totalSeconds / 8;
        for (int i = 0; i <= 8; i++)
        {
            double secs = i * step;
            var tb = new TextBlock
            {
                Text       = SecsToTime(secs),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B5563")),
                FontSize   = 9
            };
            Canvas.SetLeft(tb, SecsToX(secs) - 16);
            TimeLabels.Children.Add(tb);
        }
    }

    // ═══ CONTRÔLES ═══

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e) => TogglePlay();

    private void TogglePlay()
    {
        if (!_mediaLoaded) return;
        if (_isPlaying) { Player.Pause(); TxtPlayIcon.Text = "▶"; PlayingBadge.Visibility = Visibility.Collapsed; }
        else            { Player.Play();  TxtPlayIcon.Text = "⏸"; PlayingBadge.Visibility = Visibility.Visible;   }
        _isPlaying = !_isPlaying;
    }

    private void BtnGoStart_Click(object sender, RoutedEventArgs e) => Player.Position = TimeSpan.Zero;
    private void BtnGoEnd_Click(object sender, RoutedEventArgs e)   => Player.Position = TimeSpan.FromSeconds(_totalSeconds - 0.1);
    private void BtnBack5_Click(object sender, RoutedEventArgs e)   => Player.Position = TimeSpan.FromSeconds(Math.Max(0, Player.Position.TotalSeconds - 5));
    private void BtnFwd5_Click(object sender, RoutedEventArgs e)    => Player.Position = TimeSpan.FromSeconds(Math.Min(_totalSeconds, Player.Position.TotalSeconds + 5));

    private void CmbSpeed_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Player == null) return;
        Player.SpeedRatio = CmbSpeed.SelectedIndex switch { 0 => 0.25, 1 => 0.5, 3 => 1.5, 4 => 2.0, _ => 1.0 };
    }

    private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Player == null) return;
        Player.Volume      = SldVolume.Value;
        TxtVolumeIcon.Text = SldVolume.Value == 0 ? "🔇" : SldVolume.Value < 0.5 ? "🔉" : "🔊";
    }

    private void BtnCapture_Click(object sender, RoutedEventArgs e)
    {
        if (!_mediaLoaded) return;
        bool was = _isPlaying;
        if (was) { Player.Pause(); _isPlaying = false; }
        try
        {
            var dlg = new SaveFileDialog { Title = "Enregistrer le frame", Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg", FileName = $"frame_{SecsToStr(Player.Position.TotalSeconds).Replace(".", "-")}" };
            if (dlg.ShowDialog() == true)
            {
                var rtb = new RenderTargetBitmap((int)Player.ActualWidth, (int)Player.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(Player);
                BitmapEncoder enc = dlg.FilterIndex == 2 ? new JpegBitmapEncoder { QualityLevel = 95 } : new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using var s = File.OpenWrite(dlg.FileName);
                enc.Save(s);
                SetStatus($"Frame capturé : {Path.GetFileName(dlg.FileName)}");
            }
        }
        catch (Exception ex) { SetStatus($"Erreur : {ex.Message}"); }
        if (was) { Player.Play(); _isPlaying = true; TxtPlayIcon.Text = "⏸"; }
    }

    // ═══ ROGNAGE ═══

    private void BtnSetStart_Click(object sender, RoutedEventArgs e)
    { _trimStartSec = Player.Position.TotalSeconds; TxtTrimStart.Text = SecsToStr(_trimStartSec); UpdateTrimDisplay(); SetStatus($"Point IN : {SecsToTime(_trimStartSec)}"); }

    private void BtnSetEnd_Click(object sender, RoutedEventArgs e)
    { _trimEndSec = Player.Position.TotalSeconds; TxtTrimEnd.Text = SecsToStr(_trimEndSec); UpdateTrimDisplay(); SetStatus($"Point OUT : {SecsToTime(_trimEndSec)}"); }

    private void BtnGoToStart_Click(object sender, RoutedEventArgs e) => Player.Position = TimeSpan.FromSeconds(_trimStartSec);
    private void BtnGoToEnd_Click(object sender, RoutedEventArgs e)   => Player.Position = TimeSpan.FromSeconds(_trimEndSec);

    private void BtnClearTrim_Click(object sender, RoutedEventArgs e)
    { _trimStartSec = 0; _trimEndSec = _totalSeconds; TxtTrimStart.Text = SecsToStr(0); TxtTrimEnd.Text = SecsToStr(_totalSeconds); UpdateTrimDisplay(); SetStatus("Points effacés."); }

    private void TrimStart_TextChanged(object sender, TextChangedEventArgs e)
    { if (double.TryParse(TxtTrimStart.Text, out var v)) { _trimStartSec = Math.Max(0, Math.Min(v, _trimEndSec - 0.1)); UpdateTrimDisplay(); } }

    private void TrimEnd_TextChanged(object sender, TextChangedEventArgs e)
    { if (double.TryParse(TxtTrimEnd.Text, out var v)) { _trimEndSec = Math.Max(_trimStartSec + 0.1, Math.Min(v, _totalSeconds)); UpdateTrimDisplay(); } }

    private void BtnApplyTrim_Click(object sender, RoutedEventArgs e)
    {
        ResultTrimStart = _trimStartSec; ResultTrimEnd = _trimEndSec; TrimApplied = true;
        _item.Advanced.EnableTrim = (_trimEndSec - _trimStartSec) < _totalSeconds - 0.5;
        _item.Advanced.TrimStart  = _trimStartSec;
        _item.Advanced.TrimEnd    = _trimEndSec;
        SetStatus($"✓ Rognage : {SecsToTime(_trimStartSec)} → {SecsToTime(_trimEndSec)} ({SecsToTime(_trimEndSec - _trimStartSec)})");
    }

    // ═══ CLAVIER ═══

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:  TogglePlay();                                  e.Handled = true; break;
            case Key.Left:   BtnBack5_Click(this, new RoutedEventArgs());   e.Handled = true; break;
            case Key.Right:  BtnFwd5_Click(this, new RoutedEventArgs());    e.Handled = true; break;
            case Key.Home:   BtnGoStart_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.End:    BtnGoEnd_Click(this, new RoutedEventArgs());    e.Handled = true; break;
            case Key.I:      BtnSetStart_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.O:      BtnSetEnd_Click(this, new RoutedEventArgs());   e.Handled = true; break;
            case Key.Escape: Close(); break;
        }
    }

    // CORRECTION : référence directe au Canvas nommé dans le XAML
    // (et non MarkerStart.Parent qui était un Canvas sans nom, inaccessible en code)
    private double GetTimelineWidth() => TimelineCanvas.ActualWidth;

    private static string SecsToTime(double s)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, s));
        return ts.TotalHours >= 1 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
    }

    private static string SecsToStr(double s) => $"{s:F2}";

    private void ShowError(string msg)
    { TxtLoading.Visibility = Visibility.Collapsed; ErrorPanel.Visibility = Visibility.Visible; TxtError.Text = msg; }

    private void SetStatus(string msg) => TxtBottomStatus.Text = msg;

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closed(object sender, EventArgs e)
    { _timer.Stop(); Player.Stop(); Player.Source = null; }
}
