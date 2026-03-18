using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace MXFConverter.Services;

public static class TrayService
{
    private static NotifyIcon? _icon;
    private static Window?     _mainWindow;

    public static void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;

        _icon = new NotifyIcon
        {
            Text    = "MXF Converter Pro",
            Visible = false,
            Icon    = SystemIcons.Application,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Ouvrir", null, (_, _) => RestoreWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) =>
        {
            _icon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });

        _icon.ContextMenuStrip  = menu;
        _icon.DoubleClick      += (_, _) => RestoreWindow();
    }

    public static void MinimizeToTray()
    {
        if (_icon == null || _mainWindow == null) return;
        _mainWindow.Hide();
        _icon.Visible = true;
        _icon.ShowBalloonTip(2000, "MXF Converter Pro",
            "L'application continue en arrière-plan.", ToolTipIcon.Info);
    }

    public static void RestoreWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        if (_icon != null) _icon.Visible = false;
    }

    public static void ShowBalloon(string title, string message)
    {
        if (_icon == null) return;
        _icon.Visible = true;
        _icon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public static void Dispose()
    {
        _icon?.Dispose();
    }
}
