using System.Runtime.InteropServices;
using static Mark.TaskbarFolders.Helpers.NativeMethods;

namespace Mark.TaskbarFolders.Helpers;

internal static class ShellPropertyHelpers
{
    public static IPropertyStore? GetPropertyStore(IShellItem item)
    {
        if (item is not IShellItem2 item2)
        {
            return null;
        }

        var iid = IidIPropertyStore;
        item2.GetPropertyStore(GpsBestEffort, ref iid, out var pointer);
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return (IPropertyStore)Marshal.GetObjectForIUnknown(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    public static string? GetStringProperty(IPropertyStore store, PropertyKey key)
    {
        store.GetValue(ref key, out var value);
        try
        {
            return value.VarType == VtLpwstr && value.PointerValue != IntPtr.Zero
                ? Marshal.PtrToStringUni(value.PointerValue)
                : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    public static void SetStringProperty(IPropertyStore store, PropertyKey key, string value)
    {
        var propertyValue = PropVariant.FromString(value);
        try
        {
            store.SetValue(ref key, ref propertyValue);
            store.Commit();
        }
        finally
        {
            PropVariantClear(ref propertyValue);
        }
    }

    public static string? GetDisplayName(IShellItem2 item)
    {
        item.GetDisplayName(SigdnNormalDisplay, out var pointer);
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    public static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }
}
