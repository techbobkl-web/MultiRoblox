using System.Diagnostics;

namespace MultiRoblox;

internal static class RobloxProcesses
{
    /// <summary>The Roblox game client (RobloxPlayerBeta.exe), as installed from roblox.com or by Bloxstrap.</summary>
    public const string PlayerProcessName = "RobloxPlayerBeta";

    public static int Count()
    {
        var processes = Process.GetProcessesByName(PlayerProcessName);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length;
    }

    /// <summary>Asks each client window to close, then ends any that are still running. Blocking; call off the UI thread.</summary>
    public static (int Closed, int Failed) CloseAll()
    {
        int closed = 0, failed = 0;
        foreach (var process in Process.GetProcessesByName(PlayerProcessName))
        {
            using (process)
            {
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(2000))
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }

                    closed++;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    if (process.HasExitedSafe())
                    {
                        closed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
        }

        return (closed, failed);
    }

    private static bool HasExitedSafe(this Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
