using System.Runtime.InteropServices;

namespace VrAudioSwitcher.Audio;

// Low-level Windows Core Audio (MMDevice) interop.
//
// Enumeration + reading the current default uses the documented MMDevice API.
// Setting the default endpoint uses IPolicyConfig::SetDefaultEndpoint, an
// undocumented but stable interface Windows itself uses from the Sound control
// panel. Kept minimal and dependency-free on purpose.

public enum EDataFlow
{
    Render = 0,
    Capture = 1,
    All = 2,
}

public enum ERole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2,
}

internal static class AudioConstants
{
    public const uint DEVICE_STATE_ACTIVE = 0x00000001;
    public const int STGM_READ = 0x00000000;

    // PKEY_Device_FriendlyName
    public static readonly Guid PKEY_Device_FriendlyName_FmtId =
        new("a45c254e-df1c-4efd-8020-67d146a850e0");
    public const int PKEY_Device_FriendlyName_Pid = 14;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FmtId;
    public int Pid;
}

// Minimal PROPVARIANT: we only ever read a string (VT_LPWSTR = 31).
[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant
{
    [FieldOffset(0)] public short vt;
    [FieldOffset(8)] public IntPtr pointerValue;

    public string? GetString() =>
        pointerValue == IntPtr.Zero ? null : Marshal.PtrToStringUni(pointerValue);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice? endpoint);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

    // Remaining methods unused — left unbound (vtable order preserved is enough here
    // because we never call past GetDevice).
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int Item(uint index, out IMMDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object iface);

    [PreserveSig]
    int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

    [PreserveSig]
    int GetState(out uint state);
}

[ComImport]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetAt(uint index, out PropertyKey key);

    [PreserveSig]
    int GetValue(ref PropertyKey key, out PropVariant value);

    [PreserveSig]
    int SetValue(ref PropertyKey key, ref PropVariant value);

    [PreserveSig]
    int Commit();
}

[ComImport]
[Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class PolicyConfigClientComObject { }

// IPolicyConfig (Windows 7+). Earlier methods are stubbed only to preserve the
// vtable layout so SetDefaultEndpoint lands on the correct slot; they are never
// invoked.
[ComImport]
[Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
    [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
    [PreserveSig] int ResetDeviceFormat(IntPtr a);
    [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
    [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
    [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
    [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
    [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
    [PreserveSig] int GetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
    [PreserveSig] int SetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

    [PreserveSig] int SetEndpointVisibility(IntPtr a, int b);
}
