using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Storage;
using OS.Tooling;
using F = OS.Tooling.LinuxWritableDiskProofFixture;
using Q = OS.Tooling.QemuTranscripts;
using B = OS.Storage.WritableBlockSession;

// Synthetic copied protocol/value controls only. No native producer, device,
// process, CPU execution, physical persistence or reboot authority is supplied.
internal static unsafe partial class QemuWritableDiskTranscriptTests
{
    private sealed class Oversize : IReadOnlyList<Q.WritableDiskRow>
    {
        public int Count => Q.MaximumWritableDiskRows + 1;

        public Q.WritableDiskRow this[int index] => throw new InvalidOperationException();
        public IEnumerator<Q.WritableDiskRow> GetEnumerator() => throw new InvalidOperationException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
