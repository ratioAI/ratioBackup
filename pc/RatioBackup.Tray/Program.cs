namespace RatioBackup.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "RatioBackup.Tray", out var first);
        if (!first)
        {
            // Nach einem Update startet die neue Version, während die alte noch beendet wird: kurz warten.
            var acquired = args.Contains("--after-update") && WaitFor(mutex, TimeSpan.FromSeconds(20));
            if (!acquired)
            {
                MessageBox.Show("RatioBackup läuft bereits (Symbol unten rechts in der Taskleiste).", "RatioBackup");
                return;
            }
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }

    private static bool WaitFor(Mutex m, TimeSpan timeout)
    {
        try { return m.WaitOne(timeout); }
        catch (AbandonedMutexException) { return true; }
    }
}
