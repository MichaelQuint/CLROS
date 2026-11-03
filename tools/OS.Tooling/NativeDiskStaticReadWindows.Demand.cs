namespace OS.Tooling;
public static partial class NativeDiskStaticReadWindows
{
    private sealed class Demand(int window, WebRootDiskInventory.Entry entry, WebRootDiskInventory.Sector sector)
    {
        public readonly int Window = window;
        public readonly WebRootDiskInventory.Entry Entry = entry;
        public readonly WebRootDiskInventory.Sector Sector = sector;
        public readonly List<int> Candidates = [];
        public int Index, Remaining, Assigned = -1;
    }
}
