using Microsoft.Win32;

namespace ClipRing;

/// <summary>
/// Awesome
/// Turns "start with Windows" on or off via the current user's Run registry key.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClipRing";

    private static string ExePath => Environment.ProcessPath!;

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value
                && value.Contains(ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, $"\"{ExePath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
