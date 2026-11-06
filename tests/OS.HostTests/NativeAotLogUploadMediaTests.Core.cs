using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using OS.Tooling;
using B = OS.Storage.WritableBlockSession;
using Combined = OS.Tooling.CombinedWebDataImageFixture;
using Data = OS.Tooling.WritableDataImageFixture;
using F = OS.Tooling.LinuxWritableDiskProofFixture;
using Q = OS.Tooling.QemuTranscripts;

// Synthetic copied protocol/image vectors, not engine or SQLite execution.
// The internal pure seam cannot supply public pinned-B8 package authority.
internal static partial class NativeAotLogUploadMediaTests
{
    private delegate Q.NativeAotLogUploadMediaReport Core(string[] rows, int first, int last, F.BlockBoundary before, F.BlockBoundary after, int packageBytes, string packageHash, string phase, ReadOnlySpan<byte> initialBytes, ReadOnlySpan<byte> finalBytes, Combined.Inspection initial, Combined.Inspection final);
}
