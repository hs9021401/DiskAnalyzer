using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiskAnalyzer.UI.Helpers;

public static class IconHelper
{
    private static readonly ConcurrentDictionary<string, ImageSource?> s_iconCache = new(StringComparer.OrdinalIgnoreCase);

    private static ImageSource? s_folderIcon;
    private static ImageSource? s_driveIcon;
    private static ImageSource? s_fileDefaultIcon;

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static ImageSource? GetFolderIcon()
    {
        if (s_folderIcon != null)
            return s_folderIcon;

        try
        {
            var shinfo = new SHFILEINFO();
            IntPtr ptr = SHGetFileInfo(
                "dummy_folder",
                FILE_ATTRIBUTE_DIRECTORY,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

            if (ptr != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                DestroyIcon(shinfo.hIcon);
                s_folderIcon = bitmap;
                return s_folderIcon;
            }
        }
        catch
        {
            // fallback
        }

        return null;
    }

    public static ImageSource? GetDriveIcon()
    {
        if (s_driveIcon != null)
            return s_driveIcon;

        try
        {
            var shinfo = new SHFILEINFO();
            IntPtr ptr = SHGetFileInfo(
                "C:\\",
                0,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                SHGFI_ICON | SHGFI_SMALLICON);

            if (ptr != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                DestroyIcon(shinfo.hIcon);
                s_driveIcon = bitmap;
                return s_driveIcon;
            }
        }
        catch
        {
            // fallback
        }

        return GetFolderIcon();
    }

    public static ImageSource? GetIconForExtension(string? extension)
    {
        string ext = string.IsNullOrWhiteSpace(extension) ? ".unknown" : extension.Trim();
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        return s_iconCache.GetOrAdd(ext, static key =>
        {
            try
            {
                var shinfo = new SHFILEINFO();
                IntPtr ptr = SHGetFileInfo(
                    key,
                    FILE_ATTRIBUTE_NORMAL,
                    ref shinfo,
                    (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

                if (ptr != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmap.Freeze();
                    DestroyIcon(shinfo.hIcon);
                    return bitmap;
                }
            }
            catch
            {
                // Fallback
            }

            return GetDefaultFileIcon();
        });
    }

    public static ImageSource? GetDefaultFileIcon()
    {
        if (s_fileDefaultIcon != null)
            return s_fileDefaultIcon;

        try
        {
            var shinfo = new SHFILEINFO();
            IntPtr ptr = SHGetFileInfo(
                ".txt",
                FILE_ATTRIBUTE_NORMAL,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

            if (ptr != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                DestroyIcon(shinfo.hIcon);
                s_fileDefaultIcon = bitmap;
                return s_fileDefaultIcon;
            }
        }
        catch
        {
            // fallback
        }

        return null;
    }
}
