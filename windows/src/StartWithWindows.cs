using Microsoft.Win32;

namespace MultiRoblox;

/// <summary>Per-user "start with Windows" via HKCU\...\Run (no admin rights needed).</summary>
internal static class StartWithWindows
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MultiRoblox";

    private static string Command => $"\"{Environment.ProcessPath}\" --minimized";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, Command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Keeps the entry pointing at this exe if the user moved it.</summary>
    public static void RefreshPathIfEnabled()
    {
        if (IsEnabled)
        {
            Set(true);
        }
    }
}
