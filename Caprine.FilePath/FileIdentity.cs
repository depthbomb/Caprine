using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Caprine.FilePath;

internal static partial class FileIdentity
{
    internal static (ulong Device, ulong Low, ulong High) Read(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var handle = CreateFile(path, 0, 7, 0, 3, 0x02000000, 0);
            var info = new WindowsIdentity();
            if (handle.IsInvalid || !GetFileInformationByHandleEx(handle, 18, out info, 24))
            {
                ThrowError(path);
            }

            return (info.Volume, info.Low, info.High);
        }

        if (OperatingSystem.IsLinux())
        {
            if (Statx(-100, path, 0, 0x100, out var info) != 0)
            {
                ThrowError(path);
            }

            if ((info.Mask & 0x100) == 0)
            {
                throw new PlatformNotSupportedException("The filesystem does not expose inode identities.");
            }

            return (((ulong)info.DeviceMajor << 32) | info.DeviceMinor, info.Inode, 0);
        }

        if (OperatingSystem.IsMacOS())
        {
            var result = RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? MacStat64(path, out var info)
                : MacStat(path, out info);
            if (result != 0)
            {
                ThrowError(path);
            }

            return (info.Device, info.Inode, 0);
        }

        throw new PlatformNotSupportedException("File identity is supported on Windows, Linux, and macOS.");
    }

    internal static void CreateHardLink(string path, string target)
    {
        var success = OperatingSystem.IsWindows() ? WindowsLink(path, target, 0) : UnixLink(target, path) == 0;
        if (!success)
        {
            ThrowError(path);
        }
    }

    private static void ThrowError(string path)
    {
        var error = Marshal.GetLastPInvokeError();
        throw new IOException($"Cannot access filesystem identity or create link for '{path}': {new Win32Exception(error).Message}", new Win32Exception(error));
    }

    // Native ABI layouts: FILE_ID_INFO, Linux UAPI statx, and Darwin stat64.
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsIdentity
    {
        internal ulong Volume;
        internal ulong Low;
        internal ulong High;
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStat
    {
        [FieldOffset(0)] internal uint Mask;
        [FieldOffset(32)] internal ulong Inode;
        [FieldOffset(136)] internal uint DeviceMajor;
        [FieldOffset(140)] internal uint DeviceMinor;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct DarwinStat
    {
        [FieldOffset(0)] internal uint Device;
        [FieldOffset(8)] internal ulong Inode;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, out WindowsIdentity info, uint size);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WindowsLink(string path, string target, nint security);

    [LibraryImport("libc", EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Statx(int directory, string path, int flags, uint mask, out LinuxStat info);

    [LibraryImport("libc", EntryPoint = "stat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int MacStat(string path, out DarwinStat info);

    [LibraryImport("libc", EntryPoint = "stat$INODE64", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int MacStat64(string path, out DarwinStat info);

    [LibraryImport("libc", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int UnixLink(string target, string path);
}
