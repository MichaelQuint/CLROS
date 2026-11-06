// Extracted from the reviewed EFI verifier helper; preserve native lifecycle semantics.
#nullable disable
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling.Native
{
    public static partial class NativeFile
    {
        // Linux x86-64 struct stat, matching the builder's musl bits/stat.h.
        // Preserve the complete144-byte output even though only four fields are read.
        [StructLayout(LayoutKind.Explicit, Size = 144)]
        private struct LinuxStat
        {
            [FieldOffset(0)]
            public ulong Device;
            [FieldOffset(8)]
            public ulong Inode;
            [FieldOffset(16)]
            public ulong Links;
            [FieldOffset(48)]
            public long Bytes;
        }
    }
}
