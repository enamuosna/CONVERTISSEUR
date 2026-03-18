namespace MXFConverter.Services;

public static class NotificationService
{
    private static bool _enabled = true;

    public static void SetEnabled(bool enabled) => _enabled = enabled;

    public static void NotifyConversionComplete(int success, int errors)
    {
        if (!_enabled) return;
        try
        {
            // Utilise MessageBox en fallback (Toast nécessite MSIX/AppId déclaré)
            // Pour une app non-packagée, on utilise un popup discret non-bloquant
            ShowBalloon(
                "MXF Converter Pro",
                errors == 0
                    ? $"✅ {success} fichier(s) convertis avec succès !"
                    : $"⚠ {success} réussi(s), {errors} erreur(s)");
        }
        catch { }
    }

    public static void NotifyError(string fileName, string message)
    {
        if (!_enabled) return;
        try
        {
            ShowBalloon("MXF Converter Pro — Erreur", $"❌ {fileName}\n{message}");
        }
        catch { }
    }

    private static void ShowBalloon(string title, string message)
    {
        // Notification via System.Windows.Forms.NotifyIcon (tray)
        // Le TrayService s'en charge ; ici on expose juste la logique
        TrayService.ShowBalloon(title, message);
    }
}
