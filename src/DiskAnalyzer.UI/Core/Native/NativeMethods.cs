using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DiskAnalyzer.Core.Native;

/// <summary>
/// Win32 P/Invoke declarations, FSCTL codes, and NTFS/Win32 data structures.
/// </summary>
public static unsafe class NativeMethods
{
    #region Win32 Constants

    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint GENERIC_EXECUTE = 0x20000000;
    public const uint GENERIC_ALL = 0x10000000;

    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint FILE_SHARE_DELETE = 0x00000004;

    public const uint OPEN_EXISTING = 3;
    public const uint OPEN_ALWAYS = 4;
    public const uint CREATE_ALWAYS = 2;

    public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    public const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;
    public const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;
    public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    public const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    public const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
    public const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
    public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    public const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
    public const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    public const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
    public const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
    public const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
    public const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
    public const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
    public const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
    public const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;

    public const uint ERROR_SUCCESS = 0;
    public const uint ERROR_HANDLE_EOF = 38;
    public const uint ERROR_MORE_DATA = 234;
    public const uint ERROR_NO_MORE_FILES = 18;
    public const uint ERROR_JOURNAL_NOT_ACTIVE = 1179;
    public const uint ERROR_ACCESS_DENIED = 5;

    public const int FIND_FIRST_EX_LARGE_FETCH = 0x00000002;
    public const int FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 0x00000004;

    public const uint FSCTL_GET_NTFS_VOLUME_DATA = 0x00090064;
    public const uint FSCTL_ENUM_USN_DATA = 0x000900B3;
    public const uint FSCTL_READ_USN_JOURNAL = 0x000900BB;
    public const uint FSCTL_QUERY_USN_JOURNAL = 0x000900F4;
    public const uint FSCTL_CREATE_USN_JOURNAL = 0x000900E7;

    public const uint TOKEN_ADJUST_PRIVILEGES = 0x00000020;
    public const uint TOKEN_QUERY = 0x00000008;
    public const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    public const string SE_BACKUP_NAME = "SeBackupPrivilege";
    public const string SE_RESTORE_NAME = "SeRestorePrivilege";
    public const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";

    public const uint FO_MOVE = 0x0001;
    public const uint FO_COPY = 0x0002;
    public const uint FO_DELETE = 0x0003;
    public const uint FO_RENAME = 0x0004;

    public const ushort FOF_MULTIDESTFILES = 0x0001;
    public const ushort FOF_CONFIRMMOUSE = 0x0002;
    public const ushort FOF_SILENT = 0x0004;
    public const ushort FOF_RENAMEONCOLLISION = 0x0008;
    public const ushort FOF_NOCONFIRMATION = 0x0010;
    public const ushort FOF_WANTMAPPINGHANDLE = 0x0020;
    public const ushort FOF_ALLOWUNDO = 0x0040;
    public const ushort FOF_FILESONLY = 0x0080;
    public const ushort FOF_SIMPLEPROGRESS = 0x0100;
    public const ushort FOF_NOCONFIRMMKDIR = 0x0200;
    public const ushort FOF_NOERRORUI = 0x0400;

    public const uint SEE_MASK_DEFAULT = 0x00000000;
    public const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    public const uint SEE_MASK_NOCLOSEPROCESS = 0x00000040;

    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOW = 5;

    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_DISPLAYNAME = 0x000000200;
    public const uint SHGFI_TYPENAME = 0x000000400;
    public const uint SHGFI_ATTRIBUTES = 0x000000800;
    public const uint SHGFI_ICONLOCATION = 0x000001000;
    public const uint SHGFI_EXETYPE = 0x000002000;
    public const uint SHGFI_SYSICONINDEX = 0x000004000;
    public const uint SHGFI_LINKOVERLAY = 0x000008000;
    public const uint SHGFI_SELECTED = 0x000010000;
    public const uint SHGFI_ATTR_SPECIFIED = 0x000020000;
    public const uint SHGFI_LARGEICON = 0x000000000;
    public const uint SHGFI_SMALLICON = 0x000000001;
    public const uint SHGFI_OPENICON = 0x000000002;
    public const uint SHGFI_SHELLICONSIZE = 0x000000004;
    public const uint SHGFI_PIDL = 0x000000008;
    public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    #endregion

    #region NTFS Structures

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NTFS_VOLUME_DATA_BUFFER
    {
        public long VolumeSerialNumber;
        public long NumberSectors;
        public long TotalClusters;
        public long FreeClusters;
        public long TotalReserved;
        public uint BytesPerSector;
        public uint BytesPerCluster;
        public uint BytesPerFileRecordSegment;
        public uint ClustersPerFileRecordSegment;
        public long MftValidDataLength;
        public long MftStartLcn;
        public long Mft2StartLcn;
        public long MftZoneStart;
        public long MftZoneEnd;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct USN_JOURNAL_DATA
    {
        public ulong UsnJournalID;
        public long LowestUsn;
        public long HighestUsn;
        public long MaximumUsn;
        public long MaximumSize;
        public long AllocationDelta;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MFT_ENUM_DATA_V0
    {
        public ulong StartFileReferenceNumber;
        public long LowUsn;
        public long HighUsn;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct USN_RECORD_V2
    {
        public uint RecordLength;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ulong FileReferenceNumber;
        public ulong ParentFileReferenceNumber;
        public long Usn;
        public long TimeStamp;
        public uint Reason;
        public uint SourceInfo;
        public uint SecurityId;
        public uint FileAttributes;
        public ushort FileNameLength;
        public ushort FileNameOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MFT_RECORD_HEADER
    {
        public uint Magic; // "FILE" = 0x454C4946, "BAAD" = 0x44414142
        public ushort UpdateSequenceArrayOffset;
        public ushort UpdateSequenceArraySize;
        public ulong LogSequenceNumber;
        public ushort SequenceNumber;
        public ushort HardLinkCount;
        public ushort AttributeOffset;
        public ushort Flags; // 0x01 = InUse, 0x02 = Directory
        public uint RealSize;
        public uint AllocatedSize;
        public ulong BaseFileRecord;
        public ushort NextAttributeId;
        public ushort Reserved;
        public uint RecordNumber;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ATTRIBUTE_HEADER
    {
        public uint AttributeTypeCode;
        public uint TotalLength;
        public byte NonResidentFlag; // 0 = Resident, 1 = Non-resident
        public byte NameLength;
        public ushort NameOffset;
        public ushort Flags; // 0x0001 = Compressed, 0x4000 = Encrypted, 0x8000 = Sparse
        public ushort AttributeId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RESIDENT_ATTRIBUTE
    {
        public ATTRIBUTE_HEADER Header;
        public uint ValueLength;
        public ushort ValueOffset;
        public byte ResidentFlags;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NON_RESIDENT_ATTRIBUTE
    {
        public ATTRIBUTE_HEADER Header;
        public long StartingVcn;
        public long LastVcn;
        public ushort RunListOffset;
        public ushort CompressionUnitSize;
        public uint Reserved;
        public long AllocatedSize;
        public long RealSize;
        public long InitializedDataSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct STANDARD_INFORMATION
    {
        public long CreationTime;
        public long AlteredTime;
        public long MftChangedTime;
        public long ReadTime;
        public uint DosPermissions;
        public uint MaxVersions;
        public uint VersionNumber;
        public uint ClassId;
        public uint OwnerId;
        public uint SecurityId;
        public ulong QuotaCharged;
        public ulong Usn;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FILE_NAME_ATTRIBUTE
    {
        public ulong ParentDirectory;
        public long CreationTime;
        public long AlteredTime;
        public long MftChangedTime;
        public long ReadTime;
        public long AllocatedSize;
        public long RealSize;
        public uint Flags;
        public uint ReparseTag;
        public byte FileNameLength;
        public byte Namespace; // 0=POSIX, 1=Win32, 2=DOS, 3=Win32AndDOS
    }

    public enum AttributeType : uint
    {
        StandardInformation = 0x10,
        AttributeList = 0x20,
        FileName = 0x30,
        ObjectId = 0x40,
        SecurityDescriptor = 0x50,
        VolumeName = 0x60,
        VolumeInformation = 0x70,
        Data = 0x80,
        IndexRoot = 0x90,
        IndexAllocation = 0xA0,
        Bitmap = 0xB0,
        ReparsePoint = 0xC0,
        EAInformation = 0xD0,
        EA = 0xE0,
        LoggedUtilityStream = 0x100,
        EndOfAttributes = 0xFFFFFFFF
    }

    public enum FileNameNamespace : byte
    {
        Posix = 0,
        Win32 = 1,
        Dos = 2,
        Win32AndDos = 3
    }

    [Flags]
    public enum MftRecordFlags : ushort
    {
        None = 0x0000,
        InUse = 0x0001,
        IsDirectory = 0x0002,
        InExtend = 0x0004,
        IsViewIndex = 0x0008
    }

    #endregion

    #region Win32 Fallback Directory Search Structures

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        public fixed char cFileName[260];
        public fixed char cAlternateFileName[14];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public long ToTicks() => ((long)dwHighDateTime << 32) | dwLowDateTime;

        public DateTime? ToDateTimeUtc()
        {
            long fileTime = ToTicks();
            if (fileTime <= 0) return null;
            try
            {
                return DateTime.FromFileTimeUtc(fileTime);
            }
            catch
            {
                return null;
            }
        }
    }

    public enum FINDEX_INFO_LEVELS
    {
        FindExInfoStandard = 0,
        FindExInfoBasic = 1,
        FindExInfoMaxInfoLevel
    }

    public enum FINDEX_SEARCH_OPS
    {
        FindExSearchNameMatch = 0,
        FindExSearchLimitToDirectories = 1,
        FindExSearchLimitToDevices = 2,
        FindExSearchMaxSearchOp
    }

    #endregion

    #region Token & Security Structures

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privilege;
    }

    #endregion

    #region Shell Integration Structures

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEOPSTRUCTW
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHELLEXECUTEINFOW
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string? lpVerb;
        public string lpFile;
        public string? lpParameters;
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        public fixed char szDisplayName[260];
        public fixed char szTypeName[80];
    }

    #endregion

    #region Kernel32 P/Invoke

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        void* lpInBuffer,
        uint nInBufferSize,
        void* lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetFilePointerEx(
        SafeFileHandle hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReadFile(
        SafeFileHandle hFile,
        void* lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindFirstFileExW(
        string lpFileName,
        FINDEX_INFO_LEVELS fInfoLevelId,
        out WIN32_FIND_DATAW lpFindFileData,
        FINDEX_SEARCH_OPS fSearchOp,
        IntPtr lpSearchFilter,
        int dwAdditionalFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FindNextFileW(
        IntPtr hFindFile,
        out WIN32_FIND_DATAW lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FindClose(IntPtr hFindFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetDiskFreeSpaceExW(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailableToCaller,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetVolumeInformationW(
        string lpRootPathName,
        char* lpVolumeNameBuffer,
        uint nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        char* lpFileSystemNameBuffer,
        uint nFileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetDriveTypeW(string lpRootPathName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetLogicalDrives();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetCurrentProcess();

    #endregion

    #region Advapi32 P/Invoke

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(
        IntPtr ProcessHandle,
        uint DesiredAccess,
        out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool LookupPrivilegeValueW(
        string? lpSystemName,
        string lpName,
        out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    #endregion

    #region Shell32 P/Invoke

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int SHFileOperationW(ref SHFILEOPSTRUCTW lpFileOp);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShellExecuteExW(ref SHELLEXECUTEINFOW lpExecInfo);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfoW(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    #endregion
}
