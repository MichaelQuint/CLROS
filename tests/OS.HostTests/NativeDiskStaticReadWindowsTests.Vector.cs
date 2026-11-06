using System.Collections;
using OS.Tooling;
using Inventory = OS.Tooling.WebRootDiskInventory;
using Reads = OS.Tooling.NativeDiskReadObservations;
using ReadWindows = OS.Tooling.NativeDiskStaticReadWindows;

internal static partial class NativeDiskStaticReadWindowsTests
{
    private sealed record Vector(Reads.Inspection Device, List<int> Indices, List<string> Rows);
}
