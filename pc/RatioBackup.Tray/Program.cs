namespace RatioBackup.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "RatioBackup.Tray", out var first);
        if (!first)
        {
            MessageBox.Show("RatioBackup läuft bereits (Symbol unten rechts in der Taskleiste).", "RatioBackup");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
