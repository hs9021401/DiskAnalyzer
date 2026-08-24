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
            path = path.TrimEnd('\\');

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
            try
            {
                // Fallback: Open with Explorer
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
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
            // Method 1: Use Win32 SHFileOperation
            ushort flags = NativeMethods.FOF_ALLOWUNDO;
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
            if (result == 0 && !op.fAnyOperationsAborted)
            {
                return true;
            }
        }
        catch { }

        // Method 2: Direct File / Directory deletion if Recycle Bin fails
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return !File.Exists(path);
            }
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                return !Directory.Exists(path);
            }
        }
        catch { }

        return !File.Exists(path) && !Directory.Exists(path);
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
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                if (!File.Exists(path)) return true;
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                if (!Directory.Exists(path)) return true;
            }
        }
        catch { }

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
            return (result == 0 && !op.fAnyOperationsAborted) || (!File.Exists(path) && !Directory.Exists(path));
        }
        catch
        {
            return !File.Exists(path) && !Directory.Exists(path);
        }
    }
}
