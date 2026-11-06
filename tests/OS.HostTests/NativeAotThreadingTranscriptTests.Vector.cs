using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling;

internal static partial class NativeAotThreadingTranscriptTests
{
    internal sealed class Vector(bool pool)
    {
        internal readonly bool Pool = pool;
        internal readonly List<string> Rows = [];
        internal readonly List<int> Lengths = [];
        internal readonly Thread Main = new(1, 1)
        {
            Bottom = 0x6fe80000,
            Top = 0x70000000,
            Fs = 0x100000120,
            Rsp = 0x6ffffff0
        };
        internal Thread Current = null!;
        internal int Epoch, Transfers;
        internal ulong Clock;
        internal Vector Start()
        {
            Rows.AddRange(["TEST_START m16-009", "M16_006_POLICY physical=268435456 available=134217728 poolPages=8192 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel", "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=" + (Pool ? 1577712 : 1320176) + " package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler", "M16_005_ARMED root=8192 cs=59 ss=51 kernelStack=1048576 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null", "M16_008_BOUND root=12288 pool=2097152 workers=" + (Pool ? 8 : 4) + " code=53248 metadata=114688 userImage=728 timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack", "M16_008_BUDGET started=0 epochs=8192 steps=16384 nanos=300000000000"]);
            Select(Main);
            Main.Fs = 0;
            Call(9, 0x100000000, [0, 488, 3, 0x22, ulong.MaxValue, 0]);
            Main.Fs = 0x100000120;
            Call(158, 0, [0x1002, Main.Fs]);
            Call(218, 1, [0x500100]);
            foreach (ulong sig in new ulong[]
            {
                2,
                3,
                8,
                11,
                13,
                18,
                35
            }

            )
                Call(13, 0, [sig, 0x500400, 0, 8]);
            Call(318, 16, [0x500600, 16, 0]);
            Call(324, 0, [16, 0, 0]);
            Call(25, -14, [0x6fe7f000, 4096, 8192, 0]);
            Call(293, 0, [0x500500, 0x80000]);
            return this;
        }

        internal void Select(Thread thread)
        {
            Current = thread;
            if (thread.Rsp == 0)
                thread.Rsp = thread.Top - 64;
        }

        internal Thread Clone(int slot, int tid, ulong generation = 1)
        {
            Thread child = new(slot, tid, generation);
            child.Fs = child.Top + 0x800;
            Call(9, (long)child.Bottom, [0, 0x20000, 3, 0x22, ulong.MaxValue, 0]);
            Call(56, tid, [0x7d0f00, child.Top, 0x500280, 0x500300 + (uint)tid * 4, child.Fs]);
            return child;
        }

        internal void Call(ulong nr, long value, ulong[]? args = null, ulong action = 1, uint source = 0, ulong deadline = 0, ulong? returnedRsp = null, ulong? returnedRip = null, ulong? depth = null, ulong? frame = null, ulong? instructionRip = null, ulong flags = 514)
        {
            ulong[] a = [0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234];
            if (args != null)
                args.CopyTo(a, 0);
            ulong seq = source == 0 ? 0 : ++Current.Sequence;
            ulong rip = instructionRip ?? 0x400202;
            Rows.Add($"M16_008_CALL identity={Current.Id} nr={nr} a0={a[0]} a1={a[1]} a2={a[2]} a3={a[3]} a4={a[4]} a5={a[5]} rip={rip} rsp={Current.Rsp} flags={flags} fs={Current.Fs} gs=0 clock={Clock} valueBits={unchecked((ulong)value)} action={action} status={(action == 3 ? 9 : action is 2 or 4 ? 2 : 1)} ordinal={++Current.Ordinal} waitSource={source} waitDeadline={deadline} waitSequence={seq}" + $" signalDepth={depth ?? Current.Depth} signalFrame={frame ?? Current.Frame} blockedSignals={Current.Mask} returnRip={returnedRip ?? rip} returnRsp={returnedRsp ?? Current.Rsp} returnFlags={flags} returnFs={Current.Fs} returnGs=0 returnValueBits={unchecked((ulong)value)}");
        }

        internal void Wait(ulong address, ulong deadline = 0) => Call(202, 0, [address, 128, 2, deadline == 0 ? 0UL : 0x500600], 3, 1, deadline);
        internal void Resume(long value) => Rows.Add($"M16_008_RESUME identity={Current.Id} valueBits={unchecked((ulong)value)} status=1");
        internal void Wake(ulong address) => Call(202, 1, [address, 129, 1]);
        internal void Irq(ulong now, int ready, int active, ulong rip = 0x400202, ulong flags = 514, ulong value = 0, bool direct = false)
        {
            Clock = now;
            Rows.Add($"M16_009_IRQ identity={Current.Id} epoch={++Epoch} nanos={now} rip={rip} rsp={Current.Rsp} flags={flags} fs={Current.Fs} gs=0 valueBits={value}");
            if (!direct)
                Supervisor(1, ready, active);
        }

        internal void Drain(ulong now)
        {
            Clock = now;
            Rows.Add($"M16_009_DRAIN identity={Current.Id} epoch={++Epoch} nanos={now}");
        }

        internal void Tick(ulong now, int ready)
        {
            Clock = now;
            Rows.Add($"M16_008_TICK epoch={++Epoch} nanos={now} ready={ready}");
        }

        internal void Supervisor(int action, int ready, int active)
        {
            Transfers += 2;
            Rows.Add($"M16_008_SUPERVISOR identity={Current.Id} action={action} transfers={Transfers} ready={ready} active={active}");
        }

        internal void Exit(int action, int ready, int active, long clearTid = 0)
        {
            Rows.Add($"M16_008_EXIT identity={Current.Id} clearTidResult={unchecked((ulong)clearTid)}");
            Supervisor(action, ready, active);
            Rows.Add($"M16_008_REAP old={Current.Id} next={Current.Id + (1UL << 32)} context=zero stack=zero slot=zero");
        }

        internal void Output()
        {
            foreach (string line in (Pool ? NativeAotThreadingTranscriptTests.Pool : Suspension).Split('\n')[..^1])
            {
                int length = Encoding.UTF8.GetByteCount(line) + 1;
                Lengths.Add(length);
                Rows.Add($"M16_005_WRITE fd=1 bytes={length} data={line}");
                Call(1, length, [1, 0x500400, (uint)length]);
            }
        }

        internal void End(int reaped)
        {
            if (!Rows[^1].StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal))
                Rows.Add($"M16_008_BUDGET_END nanos={Clock}");
            Rows.Add($"M16_008_OWNER_END reaped={reaped} transfers={Transfers} irqs={Epoch} exit=0 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
            Rows.AddRange(["M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009"]);
        }
    }
}
