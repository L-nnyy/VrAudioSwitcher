using Microsoft.Win32;

namespace VrAudioSwitcher.Core;

/// <summary>
/// Manages "launch at Windows logon" via the per-user Run key, while cooperating
/// with the Task Manager Startup tab. Task Manager records enable/disable state in
/// the StartupApproved\Run key (a binary blob whose first byte's low bit means
/// "disabled"). We honour that flag so toggling from Task Manager sticks, and our
/// own toggle clears it.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "VrAudioSwitcher";

    /// <summary>True when the Run entry exists and Task Manager hasn't disabled it.</summary>
    public static bool IsEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        if (run?.GetValue(ValueName) is not string) return false;
        return !IsDisabledByTaskManager();
    }

    public static void SetEnabled(bool enabled)
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (run == null) return;

        if (enabled)
        {
            var exe = Environment.ProcessPath ?? Application.ExecutablePath;
            run.SetValue(ValueName, $"\"{exe}\"");
            ClearTaskManagerDisable();
        }
        else
        {
            run.DeleteValue(ValueName, throwOnMissingValue: false);
            // Drop any stale StartupApproved record so a re-enable starts clean.
            ClearTaskManagerDisable();
        }
    }

    private static bool IsDisabledByTaskManager()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: false);
        if (approved?.GetValue(ValueName) is byte[] data && data.Length > 0)
            return (data[0] & 0x01) != 0; // low bit set => disabled
        return false;
    }

    private static void ClearTaskManagerDisable()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
