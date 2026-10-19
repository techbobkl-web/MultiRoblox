namespace MultiRoblox;

internal static class Program
{
    private const string AppMutexName = @"Local\MultiRoblox.App.SingleInstance";
    private const string ShowEventName = @"Local\MultiRoblox.App.Show";

    [STAThread]
    private static void Main(string[] args)
    {
        using var appMutex = new Mutex(initiallyOwned: true, AppMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Already running (probably hidden in the tray): ask it to show its window, then quit.
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var showExisting))
            {
                using (showExisting)
                {
                    showExisting.Set();
                }
            }

            return;
        }

        ApplicationConfiguration.Initialize();

        bool startMinimized = args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
        using var form = new MainForm(startMinimized);

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var registration = ThreadPool.RegisterWaitForSingleObject(
            showEvent,
            (_, _) => form.BeginInvoke(form.ShowFromTray),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        Application.Run(form);

        registration.Unregister(null);
        GC.KeepAlive(appMutex);
    }
}
