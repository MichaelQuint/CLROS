namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Observes the pinned app's bounded eight-writer diagnostic under the process
/// owner's syscall serialization. Owns only counters and eight thread identities;
/// borrows copied serial bytes during Observe and allocates nothing. Exact log
/// stdout rows may span calls; stderr rows span fragments of one writev only.
/// Unrelated framework lines are ignored. Malformed,
/// duplicate, foreign-writer or mismatched writev observations fail permanently.
/// This value creates no descriptor/thread and grants no delivery or HTTP proof.
/// </summary>
public unsafe struct LinuxAppLogObservation
{
    private fixed ulong writers[8];
    private StreamState stdout, stderr;
    private uint stdoutMask, stderrMask, writevRecords;
    private ulong pendingWriter;
    private ulong stderrCallWriter;
    private uint stderrCallBytes;
    private bool pendingStderr, invalidDescriptor, failed;
    private struct StreamState
    {
        internal ulong Writer;
        internal uint Position, Worker, Record;
        internal bool Ignored, CrossedCall;
    }

    /// <summary>Successfully copied complete stdout catalog rows, from zero to32.</summary>
    public readonly uint StdoutRecords => Count(stdoutMask);
    /// <summary>Successfully copied complete stderr catalog rows, from zero to32.</summary>
    public readonly uint StderrRecords => Count(stderrMask);
    /// <summary>Catalog stderr rows joined to actual successful33-byte writev calls.</summary>
    public readonly uint WritevRecords => writevRecords;
    /// <summary>Sticky failure; default state is valid but incomplete.</summary>
    public readonly bool Failed => failed;
    /// <summary>All64 rows,32 writev results and the later invalid-fd errno passed.</summary>
    public readonly bool Complete => !failed && stdoutMask == uint.MaxValue && stderrMask == uint.MaxValue &&
        writevRecords == 32 && invalidDescriptor && !pendingStderr && stdout.Position == 0 && stderr.Position == 0;

    /// <summary>
    /// Reads1..8192 borrowed bytes only after successful copied serial delivery.
    /// The pointer must stay readable for this call; fd must be1/2 and identity
    /// the nonzero verified current thread. The owner must call RecordCall after
    /// every actual syscall, before observing any later call's output. Invalid
    /// input returns false and fails permanently; no bytes are sent or retained.
    /// </summary>
    public bool Observe(uint fd, ulong identity, byte* source, uint bytes)
    {
        if (failed) return false;
        if (fd is not (1 or 2) || identity == 0 || source == null || bytes is 0 or > 8192) return Fail();
        if (fd == 2)
        {
            if (bytes > 8192 - stderrCallBytes || (stderrCallWriter != 0 && stderrCallWriter != identity)) return Fail();
            stderrCallWriter = identity; stderrCallBytes += bytes;
        }
        ref StreamState stream = ref (fd == 1 ? ref stdout : ref stderr);
        const string shape = "CLROS_LOG fd=1 worker=0 record=0\n";
        for (uint i = 0; i < bytes; i++)
        {
            byte value = source[i];
            if (stream.Ignored) { if (value == 10) stream = default; continue; }
            if (stream.Position == 0) stream.Writer = identity;
            else if (stream.Writer != identity) return Fail();
            uint at = stream.Position;
            bool matches = at switch
            {
                13 => value == (byte)('0' + fd),
                22 => value is >= (byte)'0' and <= (byte)'7',
                31 => value is >= (byte)'0' and <= (byte)'3',
                _ => value == shape[(int)at]
            };
            if (!matches)
            {
                if (at >= 9) return Fail();
                stream = default; stream.Ignored = value != 10; continue;
            }
            if (at == 22) stream.Worker = (uint)(value - '0');
            if (at == 31) stream.Record = (uint)(value - '0');
            stream.Position++;
            if (fd == 2 && stream.Position >= 9 && stream.CrossedCall) return Fail();
            if (stream.Position != shape.Length) continue;
            uint bit = 1U << (int)(stream.Worker * 4 + stream.Record);
            ref uint mask = ref (fd == 1 ? ref stdoutMask : ref stderrMask);
            if ((mask & bit) != 0) return Fail();
            uint earlier = (1U << (int)stream.Record) - 1, shift = stream.Worker * 4;
            uint priorOut = (stdoutMask >> (int)shift) & 15, priorError = (stderrMask >> (int)shift) & 15;
            if (priorError != earlier || priorOut != (fd == 1 ? earlier : (earlier << 1) | 1)) return Fail();
            fixed (ulong* owners = writers)
            {
                if (owners[stream.Worker] != 0 && owners[stream.Worker] != identity) return Fail();
                for (uint worker = 0; worker < 8; worker++)
                    if (worker != stream.Worker && owners[worker] == identity) return Fail();
                owners[stream.Worker] = identity;
            }
            if (fd == 2)
            {
                if (pendingStderr) return Fail();
                pendingStderr = true; pendingWriter = identity;
            }
            mask |= bit; stream = default;
        }
        return true;
    }

    /// <summary>
    /// Records the actual syscall result in the same serialized order. A complete
    /// stderr catalog row must belong to this writer's successful fd2 writev of
    /// exactly33 bytes. The pinned later writev on fd1234 must return EBADF9 only
    /// after all rows/results. Missing, reordered or repeated observations cannot
    /// complete the value. Invalid observations return false and fail permanently.
    /// </summary>
    public bool RecordCall(ulong identity, LinuxSyscallFrame call, LinuxSyscallResult result)
    {
        if (failed) return false;
        if (identity == 0) return Fail();
        if (pendingStderr)
        {
            if (identity != pendingWriter || call.Number != 20 || call.Argument0 != 2 ||
                result.Action != LinuxSyscallAction.Return || result.Value != 33 ||
                stderrCallBytes != 33 || stderrCallWriter != identity) return Fail();
            pendingStderr = false; pendingWriter = 0; writevRecords++;
        }
        if (stderr.Position >= 9) return Fail(); // A known catalog row cannot straddle syscalls.
        if (stderr.Position != 0) stderr.CrossedCall = true;
        stderrCallBytes = 0; stderrCallWriter = 0;
        if (call.Number == 20 && call.Argument0 == 1234)
        {
            if (invalidDescriptor || stdoutMask != uint.MaxValue || stderrMask != uint.MaxValue ||
                writevRecords != 32 || result.Action != LinuxSyscallAction.Return || result.Value != -9) return Fail();
            invalidDescriptor = true;
        }
        return true;
    }

    private bool Fail() { failed = true; return false; }
    private static uint Count(uint value) { uint count = 0; while (value != 0) { value &= value - 1; count++; } return count; }
}
