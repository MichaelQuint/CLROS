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
    private sealed class ModelResult
    {
        public int Exit, FileSyncs, DirectorySyncs, OpenCount, ReopenedReadBytes;
        public long IndependentReadCursor, PositionedOwnerCursor;
        public bool Unlinked;
        public string Witness = "";
        public byte[] SavedFile = [];
        public readonly List<Call> Calls = [];
        public readonly List<ulong> Locks = [];
    }
}
