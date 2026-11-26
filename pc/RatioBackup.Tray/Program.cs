using System;
using System.Linq;
using System.Threading;

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
                // Läuft schon: das vorhandene Fenster nach vorne holen.
                SingleInstance.SignalShow();
                return;
            }
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();

        try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
        // Sicherheitsnetz: Hängt noch ein Hintergrund-Thread, den Prozess trotzdem beenden.
        Environment.Exit(0);
    }

    private static bool WaitFor(Mutex m, TimeSpan timeout)
    {
        try { return m.WaitOne(timeout); }
        catch (AbandonedMutexException) { return true; }
    }
}

/// <summary>Zweiter Start: dem laufenden Programm über ein benanntes Ereignis sagen, dass es sein Fenster zeigen soll.</summary>
internal static class SingleInstance
{
    private const string EventName = "RatioBackup.Tray.Show";

    public static void SignalShow()
    {
        try
        {
            using var ev = EventWaitHandle.OpenExisting(EventName);
            ev.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
    }

    public static void Listen(Action onShow)
    {
        var ev = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        var t = new Thread(() =>
        {
            while (true)
            {
                ev.WaitOne();
                onShow();
            }
        }) { IsBackground = true };
        t.Start();
    }
}
