using OS.Std.Memory;
using OS.Storage;

namespace OS.Pal;

public sealed partial class OSHostPackageFileTable
{
    private struct PackageFileSlot
    {
        public bool InUse;
        public OSHostHandle File;
        public OsPackageFileEntry Entry;
        public static PackageFileSlot Create(OSHostHandle file, OsPackageFileEntry entry)
        {
            return new PackageFileSlot
            {
                InUse = true,
                File = file,
                Entry = entry
            };
        }
    }
}
