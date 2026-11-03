using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxWritableDiskProofFixture;
using Block = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

internal static unsafe partial class LinuxWritableDiskProofFixtureTests
{
    private sealed class LogUploadResult
    {
        public int Exit, OpenCount;
        public long LogCursor, UploadCursor;
        public string Witness = "";
        public byte[] Log = [], Upload = [];
        public readonly List<Call> Calls = [];
        public readonly List<string> Syncs = [];
    }
}
