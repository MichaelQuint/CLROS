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
    public sealed record SectorChange(ulong Lba, SectorKind Kind, string OldSha256, string NewSha256);
}
