using System;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;

namespace ApkShellextIntegration {
    // Test the actual Windows COM interfaces, not just public .NET methods.
    // The test assembly deliberately has no project or DLL reference to ApkShellext2.
    [ComImport, Guid("000214FA-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IExtractIconW {
        [PreserveSig]
        int GetIconLocation(uint flags, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,
            int cchMax, out int iconIndex, out uint outputFlags);
        [PreserveSig]
        int Extract([MarshalAs(UnmanagedType.LPWStr)] string file, uint index,
            out IntPtr large, out IntPtr small, uint packedSizes);
    }

    [ComImport, Guid("00021500-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IQueryInfo {
        [PreserveSig]
        int GetInfoTip(uint flags, [MarshalAs(UnmanagedType.LPWStr)] out string info);
        [PreserveSig]
        int GetInfoFlags(out int flags);
    }

    [ComImport, Guid("b824b49d-22ac-4161-ac8a-9916e8fa3f7f")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInitializeWithStream {
        [PreserveSig]
        int Initialize(IStream stream, uint mode);
    }

    [ComImport, Guid("e357fccd-a995-4576-b01f-234630154e96")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IThumbnailProvider {
        [PreserveSig]
        int GetThumbnail(uint width, out IntPtr bitmap, out int alphaType);
    }

    [ComImport, Guid("000214e8-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellExtInit {
        void Initialize(IntPtr folderPidl, IntPtr dataObject, IntPtr classKey);
    }

    [ComImport, Guid("000214e4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IContextMenu {
        [PreserveSig]
        int QueryContextMenu(IntPtr menu, uint indexMenu, int firstId, int lastId, uint flags);
        [PreserveSig]
        int InvokeCommand(IntPtr info);
        [PreserveSig]
        int GetCommandString(int id, uint flags, int reserved, StringBuilder buffer, int characters);
    }

    internal static class Native {
        internal static readonly Guid Icon = new Guid("1F869CEE-4FDA-35D9-896F-43975A87D1F6");
        internal static readonly Guid Thumbnail = new Guid("d747c5a7-2f66-4b7d-8301-8531838e4ed3");
        internal static readonly Guid Tip = new Guid("946435a5-fe96-416d-99db-e94ee9fb46c8");
        internal static readonly Guid Context = new Guid("dcb629fc-f86f-456f-8e24-98b9b2643a9b");

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr icon);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyMenu(IntPtr handle);

        [DllImport("user32.dll")]
        internal static extern int GetMenuItemCount(IntPtr menu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetMenuString(IntPtr menu, uint item, StringBuilder result,
            int maxCount, uint flags);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        internal static extern int SHCreateStreamOnFileEx(
            [MarshalAs(UnmanagedType.LPWStr)] string path, uint mode, uint attributes,
            [MarshalAs(UnmanagedType.Bool)] bool create, IntPtr template,
            [MarshalAs(UnmanagedType.Interface)] out IStream result);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SHFILEINFO {
            public IntPtr hIcon;
            public int iIcon;
            public uint attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string displayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string typeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SHGetFileInfo(string path, uint attributes,
            ref SHFILEINFO info, uint infoSize, uint flags);

        internal static void Check(int hr, string operation) {
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);
        }

        internal static void Release(object instance) {
            if (instance != null && Marshal.IsComObject(instance))
                Marshal.FinalReleaseComObject(instance);
        }

        internal static object Create(Guid clsid) {
            Type comType = Type.GetTypeFromCLSID(clsid, true);
            object instance = Activator.CreateInstance(comType);
            if (instance == null) throw new Exception("COM activation returned null: " + clsid);
            return instance;
        }
    }
}
