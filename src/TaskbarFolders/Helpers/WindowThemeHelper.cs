using System.Windows;
using System.Windows.Interop;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace Mark.TaskbarFolders.Helpers;

internal static class WindowThemeHelper
{
    public static void Apply(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = dark ? 1 : 0;
        var result = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
        if (result != 0)
        {
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref value, sizeof(int));
        }

        ApplyRoundedCorners(handle);
    }

    public static void ApplyRoundedCorners(Window window) =>
        ApplyRoundedCorners(new WindowInteropHelper(window).Handle);

    private static void ApplyRoundedCorners(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var preference = DwmcpRound;
        DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }
}
