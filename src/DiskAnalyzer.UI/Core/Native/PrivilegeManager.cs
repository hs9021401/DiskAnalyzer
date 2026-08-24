using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace DiskAnalyzer.Core.Native;

/// <summary>
/// Manages Windows process token privileges required for raw NTFS / MFT / USN access.
/// </summary>
public static class PrivilegeManager
{
    private static readonly Lazy<bool> s_isAdmin = new(CheckIsAdministrator);

    /// <summary>
    /// Gets whether the current process has Administrator privileges.
    /// </summary>
    public static bool IsAdministrator => s_isAdmin.Value;

    private static bool CheckIsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enables SeBackupPrivilege and SeRestorePrivilege for direct raw disk/MFT reading.
    /// </summary>
    public static bool EnableBackupPrivileges()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        bool backup = EnablePrivilege(NativeMethods.SE_BACKUP_NAME);
        bool restore = EnablePrivilege(NativeMethods.SE_RESTORE_NAME);
        return backup || restore;
    }

    /// <summary>
    /// Enables a specific Windows token privilege by name (e.g. "SeBackupPrivilege").
    /// </summary>
    public static bool EnablePrivilege(string privilegeName)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        IntPtr tokenHandle = IntPtr.Zero;
        try
        {
            IntPtr processHandle = NativeMethods.GetCurrentProcess();
            if (!NativeMethods.OpenProcessToken(processHandle, NativeMethods.TOKEN_ADJUST_PRIVILEGES | NativeMethods.TOKEN_QUERY, out tokenHandle))
            {
                return false;
            }

            if (!NativeMethods.LookupPrivilegeValueW(null, privilegeName, out NativeMethods.LUID luid))
            {
                return false;
            }

            var tp = new NativeMethods.TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privilege = new NativeMethods.LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = NativeMethods.SE_PRIVILEGE_ENABLED
                }
            };

            return NativeMethods.AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (tokenHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(tokenHandle);
            }
        }
    }
}
