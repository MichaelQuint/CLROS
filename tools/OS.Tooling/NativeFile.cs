// Extracted from the reviewed EFI verifier helper; preserve native lifecycle semantics.
#nullable disable
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling.Native
{
    /// <summary>Host-only snapshots of identity, link count and length on Windows
    /// or Linux x64. Does not own handles or grant file-content authority.</summary>

    public static partial class NativeFile
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out BY_HANDLE_FILE_INFORMATION information);
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
                if (LinuxFStat(handle, out var value) != 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (value.Bytes < 0 || value.Links == 0 || value.Links > uint.MaxValue)
                    throw new System.IO.IOException("native_file_identity_geometry");
                return new FileIdentity(value.Device.ToString("x16", System.Globalization.CultureInfo.InvariantCulture) + ":" + value.Inode.ToString("x16", System.Globalization.CultureInfo.InvariantCulture), (uint)value.Links, (ulong)value.Bytes);
            }

            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("native_file_host_unsupported");
            BY_HANDLE_FILE_INFORMATION information;
            if (!GetFileInformationByHandle(handle, out information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            string key = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:x8}:{1:x8}{2:x8}", information.VolumeSerialNumber, information.FileIndexHigh, information.FileIndexLow);
            ulong length = ((ulong)information.FileSizeHigh << 32) | information.FileSizeLow;
            return new FileIdentity(key, information.NumberOfLinks, length);
        }
    }
}
