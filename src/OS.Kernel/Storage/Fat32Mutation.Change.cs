using System;
using OS.Std.Binary;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class Fat32Mutation
{
    /// <summary>Copied write identity; Before/After sector bytes live at the same
    /// index in the caller's two journal buffers. Repeated LBAs at different
    /// stages are deliberate. Before always denotes the original snapshot.</summary>
    public struct Change
    {
        public ulong Sector; public Stage Phase;
    }
}
