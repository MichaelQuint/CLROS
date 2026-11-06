using System.Collections;
using OS.Tooling;
using Inventory = OS.Tooling.WebRootDiskInventory;
using Reads = OS.Tooling.NativeDiskReadObservations;
using ReadWindows = OS.Tooling.NativeDiskStaticReadWindows;

internal static partial class NativeDiskStaticReadWindowsTests
{
    private readonly record struct Plan(ulong Sector, int At, bool Cancelled = false);
}
