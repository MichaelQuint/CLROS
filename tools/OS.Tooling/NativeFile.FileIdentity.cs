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
}
