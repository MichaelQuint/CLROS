using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Hash;
using OS.Std.Time;
using OS.Storage;

namespace OS.Tooling;
public static partial class WritableDataImageFixture
{
    /// <summary>Physical ownership decoded independently in the before/after
    /// images. FileData includes allocated cluster slack; a classification does
    /// not authorize modifying any particular file or directory entry.</summary>

    public enum SectorKind
    {
        Fat0,
        Fat1,
        FsInfo,
        RootDirectory,
        FileData
    }
}
