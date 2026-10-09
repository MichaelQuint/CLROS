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

        public static FileIdentity GetIdentity(SafeFileHandle handle)
        {
            if (handle == null || handle.IsInvalid || handle.IsClosed)
            {
                throw new ArgumentException("A live file handle is required.", "handle");
            }

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
