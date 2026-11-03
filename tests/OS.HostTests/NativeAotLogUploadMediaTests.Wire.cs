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
    private sealed class Wire(byte[] initial)
    {
        private readonly List<string> rows = ["OUTER synthetic=unjoined"];
        private readonly Dictionary<ulong, byte[]> current = [];
        private ulong submitted, completed, submittedBytes, completedBytes, flushed, clock = 10;
        internal F.BlockBoundary Before => new(4096, 99, Combined.Sectors, 0, 0, 0, 0, 0);
        internal F.BlockBoundary After => new(4096, 99, Combined.Sectors, submitted, completed, submittedBytes, completedBytes, flushed);

        internal string[] Rows
        {
            get
            {
                if (rows.Count == 1)
                    State();
                return[..rows, "OUTER_END synthetic=unjoined"];
            }
        }

        private void State(bool error = false) => rows.Add(FormattableString.Invariant($"M16_027_STATE owner=4096 epoch=99 bound=1 status={(byte)(error ? B.Status.DeviceIoError : B.Status.Success)} running={(error ? 0 : 1)} retired=0 dirty={(submittedBytes != flushed ? 1 : 0)} retry={(error ? 1 : 0)} submitted={submitted} completed={completed} submittedBytes={submittedBytes} completedBytes={completedBytes} flushAcknowledgedBytes={flushed} clock={clock++}"));
        private void Io(B.Operation kind, ulong sector, byte flags, B.Status status, byte[]? payload = null, bool cancelled = false, ulong target = 0) => rows.Add(FormattableString.Invariant($"M16_027_IO owner=4096 epoch=99 generation={submitted} kind={(byte)kind} sector={sector} flags={flags} status={(byte)status} cancelled={(cancelled ? 1 : 0)} submitted={submitted} completed={completed} submittedBytes={submittedBytes} completedBytes={completedBytes} flushTargetBytes={target} flushAcknowledgedBytes={flushed} available={unchecked((ushort)submitted)} consumed={unchecked((ushort)completed)} completionBytes={(status == B.Status.Success && kind != B.Operation.Flush ? 512 : 0)} payloadBytes={(payload is null ? 0 : 512)} clock={clock++}") + (payload is null ? "" : " data=" + Convert.ToHexString(payload).ToLowerInvariant()));
        private void Initial()
        {
            if (rows.Count == 1)
                State();
        }

        internal void Write(ulong sector, byte[] payload, bool pending = false)
        {
            Initial();
            submitted++;
            submittedBytes += 512;
            State();
            Io(B.Operation.Write, sector, 1, B.Status.Uninitialized, payload);
            if (pending)
                return;
            completed++;
            completedBytes += 512;
            State();
            Io(B.Operation.Write, sector, 3, B.Status.Success, payload);
            current[sector] = payload.ToArray();
        }

        internal void Read(ulong sector, bool cancelled = false, bool held = false)
        {
            Initial();
            submitted++;
            State();
            Io(B.Operation.Read, sector, 1, B.Status.Uninitialized);
            if (cancelled)
                Io(B.Operation.Read, sector, 1, B.Status.Uninitialized, cancelled: true);
            completed++;
            State();
            byte[] bytes = current.TryGetValue(sector, out var data) ? data : initial.AsSpan(checked((int)sector * 512), 512).ToArray();
            Io(B.Operation.Read, sector, (byte)(cancelled || held ? 3 : 7), B.Status.Success, cancelled || held ? null : bytes, cancelled);
        }

        internal void Flush(bool error = false, bool pending = false)
        {
            Initial();
            ulong target = completedBytes;
            submitted++;
            State();
            Io(B.Operation.Flush, 0, 1, B.Status.Uninitialized, target: target);
            if (pending)
                return;
            completed++;
            if (!error)
                flushed = target;
            State(error);
            Io(B.Operation.Flush, 0, 3, error ? B.Status.DeviceIoError : B.Status.Success, target: target);
        }
    }
}
