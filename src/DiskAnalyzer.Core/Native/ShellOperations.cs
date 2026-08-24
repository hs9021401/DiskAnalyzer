using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DiskAnalyzer.Core.Native;

/// <summary>
/// Shell integration utilities for file explorer actions, property dialogs, and deletion to recycle bin.
/// </summary>
public static class ShellOperations
{
    /// <summary>
    /// Opens the specified file or folder with the default system handler.
    /// </summary>
    public static bool Open(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Opens Windows Explorer with the specified file or folder selected.
    /// </summary>
    public static bool SelectInExplorer(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Opens a Command Prompt window at the given directory.
    /// </summary>
    public static bool OpenCommandPrompt(string path)
    {
        try
        {
            string dir = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? path);
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = dir,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Opens a PowerShell window at the given directory.
    /// </summary>
    public static bool OpenPowerShell(string path)
    {
        try
        {
            string dir = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? path);
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = dir,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Displays the native Windows Properties dialog for the specified file or folder.
    /// </summary>
    public static bool ShowProperties(string path)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var info = new NativeMethods.SHELLEXECUTEINFOW
            {
                cbSize = Marshal.SizeOf<NativeMethods.SHELLEXECUTEINFOW>(),
                lpVerb = "properties",
                lpFile = path,
                nShow = NativeMethods.SW_SHOW,
                fMask = NativeMethods.SEE_MASK_INVOKEIDLIST
            };

            return NativeMethods.ShellExecuteExW(ref info);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends a file or directory to the Windows Recycle Bin.
    /// </summary>
    public static bool MoveToRecycleBin(string path, bool confirm = false)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            ushort flags = NativeMethods.FOF_ALLOWUNDO;
            if (!confirm)
            {
                flags |= NativeMethods.FOF_NOCONFIRMATION | NativeMethods.FOF_SILENT;
            }

            // Path must be double null-terminated for SHFileOperation
            string pFrom = path + "\0\0";

            var op = new NativeMethods.SHFILEOPSTRUCTW
            {
                wFunc = NativeMethods.FO_DELETE,
                pFrom = pFrom,
                fFlags = flags
            };

            int result = NativeMethods.SHFileOperationW(ref op);
            return result == 0 && !op.fAnyOperationsAborted;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Permanently deletes a file or directory bypassing the Recycle Bin.
    /// </summary>
    public static bool PermanentDelete(string path, bool confirm = false)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            ushort flags = 0;
            if (!confirm)
            {
                flags |= NativeMethods.FOF_NOCONFIRMATION | NativeMethods.FOF_SILENT;
            }

            string pFrom = path + "\0\0";

            var op = new NativeMethods.SHFILEOPSTRUCTW
            {
                wFunc = NativeMethods.FO_DELETE,
                pFrom = pFrom,
                fFlags = flags
            };

            int result = NativeMethods.SHFileOperationW(ref op);
            return result == 0 && !op.fAnyOperationsAborted;
        }
        catch
        {
            return false;
        }
    }
}
