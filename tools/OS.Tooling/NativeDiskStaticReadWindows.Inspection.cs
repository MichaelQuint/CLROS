namespace OS.Tooling;
public static partial class NativeDiskStaticReadWindows
{
    /// <summary>Read-only copied windows and unique assignments in supplied-window,
    /// logical-sector order. Residual copies retain original IO order and are
    /// deliberately unclassified: their FAT/directory/other ownership remains an
    /// enclosing-consumer obligation. Immutable strings may be shared; no caller
    /// collection, raw image, inventory or device-inspection reference is retained.</summary>

    public sealed class Inspection
    {
        internal Inspection(ReadWindow[] windows, AssignedCopy[] assigned, SectorCopy[] residual)
        {
            Windows = Array.AsReadOnly(windows);
            Assigned = Array.AsReadOnly(assigned);
            Residual = Array.AsReadOnly(residual);
        }

        public IReadOnlyList<ReadWindow> Windows { get; }
        public IReadOnlyList<AssignedCopy> Assigned { get; }
        public IReadOnlyList<SectorCopy> Residual { get; }
        public bool CheckedResidualMetadata => false;
        public bool CheckedSchedulerOrMemory => false;
        public bool GrantsNativeProcessOrHttpAuthority => false;
        public bool AuthenticatedSameWholeImage => false;
    }
}
