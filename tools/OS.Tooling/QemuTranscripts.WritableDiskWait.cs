using System.Globalization;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using B = OS.Storage.WritableBlockSession;
using Fixture = OS.Tooling.LinuxWritableDiskProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    public readonly record struct WritableDiskWait(int WindowFirstRecordIndex, int CallRecordIndex, int BeginRecordIndex, int CompleteRecordIndex, ulong Identity, ulong Ordinal, ulong Sequence, ulong RequestOwner, ulong RequestEpoch, ulong RequestGeneration, uint OutputPrefix, long Value);
}
