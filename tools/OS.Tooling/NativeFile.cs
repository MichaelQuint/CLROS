// Extracted from the reviewed EFI verifier helper; preserve native lifecycle semantics.
#nullable disable
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling.Native
{
    public sealed class FileIdentity
    {
        public string Key { get; private set; }
        public uint LinkCount { get; private set; }
        public ulong Length { get; private set; }

        internal FileIdentity(string key, uint linkCount, ulong length)
        {
            Key = key;
            LinkCount = linkCount;
            Length = length;
        }
    }

    /// <summary>Host-only snapshots of identity, link count and length on Windows
    /// or Linux x64. Does not own handles or grant file-content authority.</summary>
    public static class NativeFile
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle handle,
            out BY_HANDLE_FILE_INFORMATION information);

        // Linux x86-64 struct stat, matching the builder's musl bits/stat.h.
        // Preserve the complete144-byte output even though only four fields are read.
        [StructLayout(LayoutKind.Explicit, Size = 144)]
        private struct LinuxStat
        {
            [FieldOffset(0)] public ulong Device;
            [FieldOffset(8)] public ulong Inode;
            [FieldOffset(16)] public ulong Links;
            [FieldOffset(48)] public long Bytes;
        }

        [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
        private static extern int LinuxFStat(SafeFileHandle handle, out LinuxStat information);

        /// <summary>Synchronously borrows a live handle and returns an owned
        /// value snapshot. I/O/invalid geometry failures throw; Linux supports
        /// x64 only. The caller retains handle lifetime and must separately
        /// verify unchanged identity/content before trusting borrowed file data.</summary>
        public static FileIdentity GetIdentity(SafeFileHandle handle)
        {
            if (handle == null || handle.IsInvalid || handle.IsClosed)
            {
                throw new ArgumentException("A live file handle is required.", "handle");
            }

            if (OperatingSystem.IsLinux())
            {
                if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    throw new PlatformNotSupportedException("native_file_linux_x64_required");
                if (LinuxFStat(handle, out var value) != 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (value.Bytes < 0 || value.Links == 0 || value.Links > uint.MaxValue)
                    throw new System.IO.IOException("native_file_identity_geometry");
                return new FileIdentity(value.Device.ToString("x16", System.Globalization.CultureInfo.InvariantCulture) + ":" +
                    value.Inode.ToString("x16", System.Globalization.CultureInfo.InvariantCulture), (uint)value.Links, (ulong)value.Bytes);
            }
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("native_file_host_unsupported");

            BY_HANDLE_FILE_INFORMATION information;
            if (!GetFileInformationByHandle(handle, out information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            string key = String.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:x8}:{1:x8}{2:x8}",
                information.VolumeSerialNumber,
                information.FileIndexHigh,
                information.FileIndexLow);
            ulong length =
                ((ulong)information.FileSizeHigh << 32) |
                information.FileSizeLow;
            return new FileIdentity(key, information.NumberOfLinks, length);
        }
    }
}
