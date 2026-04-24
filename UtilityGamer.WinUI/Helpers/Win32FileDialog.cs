using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace UtilityGamer.WinUI.Helpers;

/// <summary>
/// Wraps the Win32 IFileOpenDialog COM interface for use in unpackaged WinUI 3 apps,
/// where the WinRT FileOpenPicker is not available.
/// </summary>
public static class Win32FileDialog
{
    /// <summary>
    /// Opens a native file-picker dialog filtered to .exe files.
    /// Returns the selected path, or null if the user cancelled.
    /// </summary>
    public static string? PickExeFile(nint ownerHwnd)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogCoClass();
        try
        {
            dialog.GetOptions(out var options);
            options |= FosFlags.ForceFilesystem
                     | FosFlags.PathMustExist
                     | FosFlags.FileMustExist;
            dialog.SetOptions(options);

            var filter = new[]
            {
                new FilterSpec { pszName = "Executable files (*.exe)", pszSpec = "*.exe" },
                new FilterSpec { pszName = "All files (*.*)",          pszSpec = "*.*"   }
            };
            dialog.SetFileTypes((uint)filter.Length, filter);
            dialog.SetFileTypeIndex(1);
            dialog.SetTitle("Select game executable");

            var hr = dialog.Show(ownerHwnd);
            if (hr != 0) return null;

            dialog.GetResult(out var item);
            try
            {
                item.GetDisplayName(SigdnFlags.FileSystemPath, out var path);
                return path;
            }
            finally
            {
                if (item != null)
                    Marshal.ReleaseComObject(item);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    [ClassInterface(ClassInterfaceType.None)]
    private class FileOpenDialogCoClass { }

    [ComImport]
    [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(nint parent);
        void SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(nint pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FosFlags fos);
        void GetOptions(out FosFlags pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(nint pFilter);
        void GetResults(out nint ppenum);
        void GetSelectedItems(out nint ppsai);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IBindCtx pbc, ref Guid bhid, ref Guid riid, out nint ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SigdnFlags sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FilterSpec
    {
        public string pszName;
        public string pszSpec;
    }

    [Flags]
    private enum FosFlags : uint
    {
        OverwritePrompt    = 0x00000002,
        StrictFileTypes    = 0x00000004,
        NoChangeDir        = 0x00000008,
        PickFolders        = 0x00000020,
        ForceFilesystem    = 0x00000040,
        AllNonStorageItems = 0x00000080,
        NoValidate         = 0x00000100,
        AllowMultiSelect   = 0x00000200,
        PathMustExist      = 0x00000800,
        FileMustExist      = 0x00001000,
        CreatePrompt       = 0x00002000,
        ShareAware         = 0x00004000,
        NoReadOnlyReturn   = 0x00008000,
        NoTestFileCreate   = 0x00010000,
        HideMruPlaces      = 0x00020000,
        HideAllNamespaces  = 0x00040000,
        NoDereferenceLinks = 0x00100000,
        DontAddToRecent    = 0x02000000,
        ForceShowHidden    = 0x10000000,
        DefaultNoMiniMode  = 0x20000000,
    }

    private enum SigdnFlags : uint
    {
        NormalDisplay             = 0x00000000,
        ParentRelativeParsing     = 0x80018001,
        DesktopAbsoluteParsing    = 0x80028000,
        ParentRelativeEditing     = 0x80031001,
        DesktopAbsoluteEditing    = 0x8004c000,
        FileSystemPath            = 0x80058000,
        Url                       = 0x80068000,
    }
}
