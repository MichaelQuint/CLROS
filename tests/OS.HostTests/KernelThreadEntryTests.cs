using System.Runtime.InteropServices;
using OS.Kernel.Threading;

internal static unsafe partial class KernelThreadEntryTests
{
    // Decode the small emitted instruction subset to observe control flow and
    // ABI arguments independently of the encoder. This executes no native code.
    private static (int Starts, int Workers, bool Exited, int Value) Walk(Model m, byte startResult, int workerResult)
    {
        ulong pc = (ulong)m.Code, rsp = m.Plan.Initial.Context->Rsp, rcx = 0, rdx = 0, r11 = 0;
        uint eax = 0;
        bool equal = false, df = true;
        int starts = 0, workers = 0;
        for (int n = 0; n < 64; n++)
        {
            if (pc == m.Plan.RejectRip)
                return (starts, workers, false, 0);
            HostCheck.True(pc >= (ulong)m.Code && pc < (ulong)m.Code + KernelThreadEntry.Bytes);
            byte* b = (byte*)pc;
            if (*b == 0xfc)
            {
                df = false;
                pc++;
            }
            else if (*(uint*)b == 0x28ec8348)
            {
                rsp -= 40;
                pc += 4;
            }
            else if (*(ushort*)b == 0xb948)
            {
                rcx = *(ulong*)(b + 2);
                pc += 10;
            }
            else if (*(ushort*)b == 0xba48)
            {
                rdx = *(ulong*)(b + 2);
                pc += 10;
            }
            else if (*(ushort*)b == 0xbb49)
            {
                r11 = *(ulong*)(b + 2);
                pc += 10;
            }
            else if (b[0] == 0x41 && b[1] == 0xff && b[2] == 0xd3)
            {
                HostCheck.Equal(0UL, rsp & 15);
                HostCheck.True(!df);
                HostCheck.Equal(m.Plan.Initial.Context->Rsp - 40, rsp);
                if (r11 == m.Plan.Start)
                {
                    HostCheck.Equal(m.Plan.Control, rcx);
                    HostCheck.Equal(m.Plan.Initial.Identity, rdx);
                    starts++;
                    eax = startResult;
                }
                else
                {
                    HostCheck.Equal(m.Plan.Entry, r11);
                    HostCheck.Equal(m.Plan.Argument, rcx);
                    workers++;
                    eax = unchecked((uint)workerResult);
                }

                pc += 3;
            }
            else if (*b == 0x3c)
            {
                equal = (byte)eax == b[1];
                pc += 2;
            }
            else if (*b == 0x0f && (b[1] == 0x84 || b[1] == 0x85))
            {
                bool take = b[1] == 0x84 ? equal : !equal;
                pc = take ? unchecked(pc + 6 + (ulong)(long)*(int*)(b + 2)) : pc + 6;
            }
            else if (*b == 0xba)
            {
                rdx = *(uint*)(b + 1);
                pc += 5;
            }
            else if (*(ushort*)b == 0xc289)
            {
                rdx = eax;
                pc += 2;
            }
            else if (*b == 0xe9)
                pc = unchecked(pc + 5 + (ulong)(long)*(int*)(b + 1));
            else if (*b == 0xe8)
            {
                HostCheck.Equal(m.Plan.ExitStub, unchecked(pc + 5 + (ulong)(long)*(int*)(b + 1)));
                HostCheck.Equal((ulong)m.Code + KernelThreadEntry.ExitResumeOffset, pc + 5);
                HostCheck.Equal(0UL, rsp & 15);
                HostCheck.Equal(m.Plan.Initial.Context->Rsp - 40, rsp);
                return (starts, workers, true, unchecked((int)rdx));
            }
            else
                throw new InvalidOperationException("Unexpected entry adapter instruction");
        }

        throw new InvalidOperationException("Entry adapter did not terminate within bounded decoding");
    }

    [HostTest("kernel.thread entry normal return and deferred cancellation use aligned exit capture")]
    private static void EntryPaths()
    {
        using Model m = new();
        HostCheck.True(KernelThreadEntry.Emit(m.Code, 256, m.Plan));
        HostCheck.True(KernelThreadEntry.Verify(m.Code, 256, m.Plan));
        var normal = Walk(m, 1, int.MinValue + 37);
        HostCheck.Equal(1, normal.Starts);
        HostCheck.Equal(1, normal.Workers);
        HostCheck.True(normal.Exited);
        HostCheck.Equal(int.MinValue + 37, normal.Value);
        var cancelled = Walk(m, 2, 17);
        HostCheck.Equal(1, cancelled.Starts);
        HostCheck.Equal(0, cancelled.Workers);
        HostCheck.True(cancelled.Exited);
        HostCheck.Equal(-1, cancelled.Value);
        foreach (byte invalid in new byte[]
        {
            0,
            3,
            255
        }

        )
        {
            var rejected = Walk(m, invalid, 17);
            HostCheck.Equal(0, rejected.Workers);
            HostCheck.True(!rejected.Exited);
        }

        byte* rejectContinuation = m.Code + KernelThreadEntry.ExitResumeOffset;
        HostCheck.Equal((byte)0xe9, *rejectContinuation);
        HostCheck.Equal(m.Plan.RejectRip, unchecked((ulong)rejectContinuation + 5 + (ulong)(long)*(int*)(rejectContinuation + 1)));
    }

    [HostTest("kernel.thread entry emission rejects bad plans without changing borrowed storage")]
    private static void Admission()
    {
        using Model m = new();
        byte[] before = new ReadOnlySpan<byte>(m.Memory, 0x20000).ToArray();
        for (int field = 0; field < 15; field++)
        {
            var p = m.Plan;
            byte* output = m.Code;
            uint capacity = 256;
            if (field == 0)
                p.Owner.Key = 0;
            if (field == 1)
                p.Initial.CurrentIdentity++;
            if (field == 2)
                p.Initial.Kind = ThreadContextSwitch.ResumeKind.CapturedCall;
            if (field == 3)
                capacity--;
            if (field == 4)
                output++;
            if (field == 5)
                p.CodeOffset = 1;
            if (field == 6)
                p.Entry = (ulong)m.Code + 10;
            if (field == 7)
                p.ExitStub = (ulong)m.Code + 16;
            if (field == 8)
                p.RejectRip = p.ExitStub;
            if (field == 9)
                p.Start = 0;
            if (field == 10)
                p.Control = (ulong)p.Initial.Context;
            if (field == 11)
                p.ControlBytes = 7;
            if (field == 12)
                output = (byte*)p.Control;
            if (field == 13)
                p.ExitStub = p.Owner.CodeStart + p.Owner.CodeBytes - 16;
            if (field == 14)
                p.Start = (ulong)m.Code + 1;
            HostCheck.True(!KernelThreadEntry.Emit(output, capacity, p));
            HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(m.Memory, 0x20000)));
        }

        m.Plan.Initial.Context->Rsp = m.Plan.Initial.StackGuardStart + 4096 + 40;
        HostCheck.True(!KernelThreadEntry.Emit(m.Code, 256, m.Plan));
        m.Plan.Initial.Context->Rsp += 16; // 56 is entry-aligned, but capture would fall below low+32.
        HostCheck.True(!KernelThreadEntry.Emit(m.Code, 256, m.Plan));
        m.Plan.Initial.Context->Rsp += 16; // 72 leaves exactly the captured CALL lower reserve.
        HostCheck.True(KernelThreadEntry.Emit(m.Code, 256, m.Plan));
        var captured = m.Plan.Initial;
        captured.Kind = ThreadContextSwitch.ResumeKind.CapturedCall;
        captured.ResumeRip = (ulong)m.Code + KernelThreadEntry.ExitResumeOffset;
        captured.Context->Rip = captured.ResumeRip;
        captured.Context->Rsp -= 40;
        var liveOwner = m.Plan.Owner;
        liveOwner.GuardsVerified = true;
        liveOwner.MappingsVerified = true;
        HostCheck.Equal(ThreadContextSwitch.Status.Ok, ThreadContextSwitch.AdmitResume(liveOwner, captured));
        m.Plan.Initial.Context->Rip = m.Plan.Initial.ResumeRip;
        m.Plan.Initial.Context->Rsp = m.Plan.Initial.StackGuardStart + 8192 - 40;
        byte* fx = (byte*)m.Plan.Initial.Context + X64ThreadContext.FxSaveOffset;
        fx[4] = 1;
        HostCheck.True(!KernelThreadEntry.Emit(m.Code, 256, m.Plan));
        fx[4] = 0;
        *(uint*)(fx + 24) = 0x3f80;
        HostCheck.True(!KernelThreadEntry.Emit(m.Code, 256, m.Plan));
    }

    [HostTest("kernel.thread entry verification survives capture and rejects byte corruption")]
    private static void VerifyAfterCapture()
    {
        using Model m = new();
        HostCheck.True(KernelThreadEntry.Emit(m.Code, 256, m.Plan));
        m.Plan.Initial.Kind = ThreadContextSwitch.ResumeKind.CapturedCall;
        m.Plan.Initial.ResumeRip = (ulong)m.Code + KernelThreadEntry.ExitResumeOffset;
        m.Plan.Initial.Context->Rip = m.Plan.Initial.ResumeRip;
        m.Plan.Initial.Context->Rsp -= 40;
        HostCheck.True(KernelThreadEntry.Verify(m.Code, 256, m.Plan));
        HostCheck.True(!KernelThreadEntry.Emit(m.Code, 256, m.Plan));
        m.Code[KernelThreadEntry.RunOffset + 2] ^= 1;
        HostCheck.True(!KernelThreadEntry.Verify(m.Code, 256, m.Plan));
    }
}
