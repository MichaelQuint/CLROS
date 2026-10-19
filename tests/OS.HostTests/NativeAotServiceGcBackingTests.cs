using System.Text;
using OS.Tooling;

internal static class NativeAotServiceGcBackingTests
{
    private const ulong Reservation = 0x100185000, Start = Reservation + 4096, ArenaBytes = 5 * 16777216UL;
    private const ulong Mmap = 0x47059a, Munmap = 0x470691, Madvise = 0x470541, Mprotect = 0x47064a;
    private const ulong NoMemory = unchecked((ulong)-12L);

    // Independent copied-value vectors, not an original executable/capture.
    private sealed class Trace
    {
        private readonly StringBuilder text = new(); private ulong ordinal;
        internal Trace Call(ulong nr, ulong address, ulong bytes, ulong protection, ulong value, ulong rip,
            ulong flags = 0, ulong fd = 4294967295)
        {
            ulong at = ++ordinal;
            text.Append($"M16_008_CALL identity=4294967297 nr={nr} a0={address} a1={bytes} a2={protection} a3={flags} a4={fd} a5=0 rip={rip} rsp=1879047184 flags=514 fs=0 gs=0 clock={at} valueBits={value} action=1 status=1 ordinal={at}" +
                $" waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip={rip} returnRsp=1879047184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}\r\n");
            return this;
        }
        internal Trace Arena() => Call(9, 0, ArenaBytes + 4096, 0, Reservation, Mmap, 0x22)
            .Call(11, Reservation, 4096, 0, 0, Munmap).Call(28, Start, ArenaBytes, 16, 0, Madvise);
        public override string ToString() => text.ToString();
    }
    private static QemuTranscripts.ServiceGcBackingInspection Read(string raw) =>
        QemuTranscripts.InspectNativeAotServiceGcBackingForTests(Encoding.UTF8.GetBytes(raw));
    private static QemuTranscripts.ServiceGcBackingInspection Read(Trace trace) => Read(trace.ToString());
    private static void Reject(string raw)
    { bool rejected = false; try { _ = Read(raw); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }

    [HostTest("GC backing identifies the complete arena sequence and returns independent copied counters")]
    private static void ArenaAndCopies()
    {
        var trace = new Trace().Arena().Call(10, Start, 8192, 3, 0, Mprotect);
        byte[] raw = Encoding.UTF8.GetBytes(trace.ToString()), before = raw.ToArray();
        var result = QemuTranscripts.InspectNativeAotServiceGcBackingForTests(raw);
        HostCheck.True(result.ArenaIdentified); HostCheck.Equal(Start, result.ArenaStart);
        HostCheck.Equal(ArenaBytes, result.ArenaBytes); HostCheck.Equal(8192UL, result.LastBackedBytes);
        HostCheck.Equal(8192UL, result.PeakBackedBytes); HostCheck.Equal(0, result.RevokedPages);
        HostCheck.True(raw.AsSpan().SequenceEqual(before)); Array.Clear(raw);
        HostCheck.Equal(8192UL, result.LastBackedBytes);
        HostCheck.True(!Read(new Trace().Call(228, 0, 4096, 0, 0, 4198402)).ArenaIdentified);
    }

    [HostTest("GC backing rejects substituted trim dump order ownership and duplicate arena sequences")]
    private static void SequenceAndBindings()
    {
        string raw = new Trace().Arena().ToString();
        foreach (string changed in new[]
        {
            raw.Replace($"a0={Reservation} a1=4096", $"a0={Reservation} a1=8192", StringComparison.Ordinal),
            raw.Replace($"a0={Start} a1={ArenaBytes} a2=16", $"a0={Start} a1={ArenaBytes} a2=17", StringComparison.Ordinal),
            raw.Replace($"rip={Munmap}", "rip=4198402", StringComparison.Ordinal),
            raw.Replace("ordinal=2", "ordinal=3", StringComparison.Ordinal),
            raw.Replace($"nr=11 a0={Reservation}", $"nr=28 a0={Reservation}", StringComparison.Ordinal),
            new Trace().Arena().Arena().ToString()
        }) Reject(changed);
        HostCheck.True(!Read(raw.Replace($"rip={Mmap}", "rip=4198402", StringComparison.Ordinal)).ArenaIdentified);
        HostCheck.True(!Read(raw.Replace($"a1={ArenaBytes + 4096}", $"a1={ArenaBytes + 8192}", StringComparison.Ordinal)).ArenaIdentified);
        HostCheck.True(!Read(raw.Replace("a4=4294967295", "a4=3", StringComparison.Ordinal)).ArenaIdentified);
    }

    [HostTest("GC backing retains inaccessible pages until actual discard and preserves known fixed decommit ownership")]
    private static void RetainDiscardAndRecommit()
    {
        var trace = new Trace().Arena().Call(10, Start, 8192, 3, 0, Mprotect).Call(10, Start, 8192, 0, 0, Mprotect);
        HostCheck.Equal(8192UL, Read(trace).LastBackedBytes);
        trace.Call(28, Start, 8192, 4, 0, Madvise); HostCheck.Equal(0UL, Read(trace).LastBackedBytes);
        trace.Call(10, Start, 4096, 1, 0, Mprotect).Call(28, Start, 4096, 4, 0, Madvise);
        HostCheck.Equal(4096UL, Read(trace).LastBackedBytes);
        trace.Call(9, Start, 4096, 0, Start, Mmap, 0x32); HostCheck.Equal(0UL, Read(trace).LastBackedBytes);
        trace.Call(10, Start, 4096, 3, 0, Mprotect);
        var result = Read(trace); HostCheck.Equal(4096UL, result.LastBackedBytes);
        HostCheck.Equal(8192UL, result.PeakBackedBytes); HostCheck.Equal(0, result.RevokedPages);
    }

    [HostTest("GC backing irrevocably excludes holes foreign replacements and remapped intervals")]
    private static void LostOwnership()
    {
        var trace = new Trace().Arena().Call(10, Start, 12288, 3, 0, Mprotect)
            .Call(11, Start + 4096, 4096, 0, 0, Munmap)
            .Call(9, 0, 4096, 3, Start + 4096, Mmap, 0x22)
            .Call(10, Start + 4096, 4096, 3, 0, Mprotect);
        HostCheck.Equal(8192UL, Read(trace).LastBackedBytes);
        trace.Call(9, Start, 4096, 3, Start, Mmap, 0x32).Call(10, Start, 4096, 3, 0, Mprotect);
        HostCheck.Equal(4096UL, Read(trace).LastBackedBytes);
        trace.Call(25, Start + 8192, 4096, 4096, Start + 8192, 4198402);
        var result = Read(trace); HostCheck.Equal(0UL, result.LastBackedBytes);
        HostCheck.Equal(12288UL, result.PeakBackedBytes); HostCheck.Equal(3, result.RevokedPages);
    }

    [HostTest("GC backing preserves failed operations excludes foreign callbacks and rejects impossible physical bounds")]
    private static void FailuresAndBounds()
    {
        var trace = new Trace().Arena().Call(10, Start, 4096, 3, 0, Mprotect)
            .Call(10, Start, 8192, 3, NoMemory, Mprotect).Call(11, Start, 4096, 0, NoMemory, Munmap)
            .Call(9, Start, 4096, 0, NoMemory, Mmap, 0x32).Call(28, Start, 4096, 4, NoMemory, Madvise);
        HostCheck.Equal(4096UL, Read(trace).LastBackedBytes);
        trace.Call(10, Start, 4096, 0, 0, 4198402);
        HostCheck.Equal(0UL, Read(trace).LastBackedBytes); HostCheck.Equal(1, Read(trace).RevokedPages);
        var unknown = new Trace().Arena().Call(10, Start, 4096, 3, 0, Mprotect).Call(28, Start, 4096, 8, 0, Madvise);
        HostCheck.Equal(0UL, Read(unknown).LastBackedBytes); HostCheck.Equal(1, Read(unknown).RevokedPages);
        Reject(new Trace().Arena().Call(10, Start, 16777216 + 4096, 3, 0, Mprotect).ToString());
        Reject(new Trace().Arena().Call(10, Start + 1, 4096, 3, 0, Mprotect).ToString());
        Reject(new Trace().Arena().Call(10, Start, 0, 3, 0, Mprotect).ToString());
    }
}
