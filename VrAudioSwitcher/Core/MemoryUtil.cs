using System.Runtime.InteropServices;

namespace VrAudioSwitcher.Core;

/// <summary>
/// Trims the process working set once startup settles. The app spends ~99% of its
/// life idle in the tray, so releasing startup pages back to the OS keeps its
/// reported memory small (pages fault back in on demand if needed).
/// </summary>
public static class MemoryUtil
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    public static void Trim()
    {
        try { EmptyWorkingSet(GetCurrentProcess()); } catch { /* best effort */ }
    }

    /// <summary>Trim once after <paramref name="delayMs"/>, off the UI thread.</summary>
    public static void TrimDeferred(int delayMs = 2500)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            Trim();
        });
    }
}
