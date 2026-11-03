using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Std.Hash;
using OS.Storage;

namespace OS.Tooling;

public sealed record FatImageFixtureReceipt(string OriginalSha256, string FinalSha256, string EfiSha256, ulong PartitionFirstLba, ulong PartitionLastLba, uint RootExtension, uint FileFirst, uint FileSecond, uint FileThird, uint FileBytes, uint FileCrc32, string LongName);
