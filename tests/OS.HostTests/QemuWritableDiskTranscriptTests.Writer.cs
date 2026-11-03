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
    private sealed class Writer
    {
        internal readonly List<string> Rows = [];
        internal ulong Submitted, Completed, SubmittedBytes, CompletedBytes, Flushed, Clock = 10000;
        internal readonly F.BlockBoundary Before;
        internal F.BlockBoundary After => new(Owner, Epoch, CombinedWebDataImageFixture.Sectors, Submitted, Completed, SubmittedBytes, CompletedBytes, Flushed);

        internal int StateIoRows;
        internal byte[]? Image;
        internal Writer(ulong baseline = 0)
        {
            Submitted = Completed = baseline;
            Before = After;
            State();
        }

        internal Q.WritableDiskRow[] Selected => Rows.Select((s, i) => new Q.WritableDiskRow(i, s)).ToArray();

        internal void State(bool retired = false, B.Status failure = B.Status.Success)
        {
            Rows.Add(FormattableString.Invariant($"M16_027_STATE owner={Owner} epoch={Epoch} bound=1 status={(byte)failure} running={(retired || failure != B.Status.Success ? 0 : 1)} retired={(retired ? 1 : 0)} dirty={(SubmittedBytes != Flushed ? 1 : 0)} retry={(failure == B.Status.Success ? 0 : 1)} submitted={Submitted} completed={Completed} submittedBytes={SubmittedBytes} completedBytes={CompletedBytes} flushAcknowledgedBytes={Flushed} clock={Clock++}"));
            StateIoRows++;
        }

        internal byte[] Sector(ulong sector) => Image is null ? Enumerable.Range(0, 512).Select(i => (byte)((i * 19 + (int)sector) & 255)).ToArray() : Image.AsSpan(checked((int)(sector * 512)), 512).ToArray();
        private void Io(B.Operation kind, ulong sector, byte flags, B.Status status, bool cancelled, byte[]? bytes, ulong target = 0)
        {
            uint count = status == B.Status.Success && kind != B.Operation.Flush ? 512U : 0;
            Rows.Add(FormattableString.Invariant($"M16_027_IO owner={Owner} epoch={Epoch} generation={Submitted} kind={(byte)kind} sector={sector} flags={flags} status={(byte)status} cancelled={(cancelled ? 1 : 0)} submitted={Submitted} completed={Completed} submittedBytes={SubmittedBytes} completedBytes={CompletedBytes} flushTargetBytes={target} flushAcknowledgedBytes={Flushed} available={unchecked((ushort)Submitted)} consumed={unchecked((ushort)Completed)} completionBytes={count} payloadBytes={(bytes is null ? 0 : 512)} clock={Clock++}") + (bytes is null ? "" : " data=" + Convert.ToHexString(bytes).ToLowerInvariant()));
            StateIoRows++;
        }

        internal void Write(ulong sector, bool error = false, bool pending = false)
        {
            Submitted++;
            SubmittedBytes += 512;
            State();
            byte[] bytes = Sector(sector);
            Io(B.Operation.Write, sector, 1, B.Status.Uninitialized, false, bytes);
            if (pending)
                return;
            Completed++;
            if (!error)
                CompletedBytes += 512;
            State(failure: error ? B.Status.DeviceIoError : B.Status.Success);
            Io(B.Operation.Write, sector, 3, error ? B.Status.DeviceIoError : B.Status.Success, false, bytes);
        }

        internal void Flush()
        {
            ulong target = CompletedBytes;
            Submitted++;
            State();
            Io(B.Operation.Flush, 0, 1, B.Status.Uninitialized, false, null, target);
            Completed++;
            Flushed = target;
            State();
            Io(B.Operation.Flush, 0, 3, B.Status.Success, false, null, target);
        }

        internal void Read(ulong sector, bool cancelled = false, bool held = false)
        {
            Submitted++;
            State();
            Io(B.Operation.Read, sector, 1, B.Status.Uninitialized, false, null);
            if (cancelled)
                Io(B.Operation.Read, sector, 1, B.Status.Uninitialized, true, null);
            Completed++;
            State();
            Io(B.Operation.Read, sector, (byte)(cancelled || held ? 3 : 7), B.Status.Success, cancelled, cancelled || held ? null : Sector(sector));
        }
    }
}
