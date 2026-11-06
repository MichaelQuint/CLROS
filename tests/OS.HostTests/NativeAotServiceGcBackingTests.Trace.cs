using System.Text;
using OS.Tooling;

internal static partial class NativeAotServiceGcBackingTests
{
    // Independent copied-value vectors, not an original executable/capture.
    private sealed class Trace
    {
        private readonly StringBuilder text = new();
        private ulong ordinal;
        private readonly Dictionary<ulong, ulong> ordinals = [];
        internal Trace Call(ulong nr, ulong address, ulong bytes, ulong protection, ulong value, ulong rip, ulong flags = 0, ulong fd = 4294967295, ulong identity = 4294967297)
        {
            ulong at = ++ordinal, callOrdinal = ordinals.GetValueOrDefault(identity) + 1;
            ordinals[identity] = callOrdinal;
            text.Append($"M16_008_CALL identity={identity} nr={nr} a0={address} a1={bytes} a2={protection} a3={flags} a4={fd} a5=0 rip={rip} rsp=1879047184 flags=514 fs=0 gs=0 clock={at} valueBits={value} action=1 status=1 ordinal={callOrdinal}" + $" waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip={rip} returnRsp=1879047184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}\r\n");
            return this;
        }

        internal Trace Reserve() => Call(9, 0, ArenaBytes + 4096, 0, Reservation, Mmap, 0x22);
        internal Trace FinishArena() => Call(11, Reservation, 4096, 0, 0, Munmap).Call(28, Start, ArenaBytes, 16, 0, Madvise);
        internal Trace Arena() => Reserve().FinishArena();
        public override string ToString() => text.ToString();
    }
}
