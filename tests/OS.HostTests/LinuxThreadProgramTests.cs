using OS.Kernel.Threading;
using P = OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram;
using S = OS.Kernel.Threading.ThreadContextSwitch;

internal static unsafe class LinuxThreadProgramTests
{
    private static P.Plan Plan(KernelThreadTableTests.Model m, uint count = 4)
    {
        var o = m.Owner; o.CodeBytes = P.Bytes; o.GuardsVerified = o.MappingsVerified = false;
        var supervisor = m.Supervisor; supervisor.ResumeRip = m.Code + P.DriverResume; supervisor.Context->Rip = supervisor.ResumeRip;
        ulong control = o.ContextArena + 0x7000, helper = o.ContextArena + 0x6000;
        new Span<byte>((void*)helper, 512).Clear(); *(ushort*)helper = 0x027f;
        *(uint*)(helper + 24) = 0x1f80; *(uint*)(helper + 28) = 0xffbf;
        P.Binding* bindings = (P.Binding*)(o.ContextArena + 0x8000);
        for (uint i = 0; i < count && i < P.MaximumWorkers; i++)
        {
            S.Descriptor d = new() { Identity = 0x100000001UL + i, CurrentIdentity = 0x100000001UL + i,
                Context = m.Context((int)i), ContextBytes = 688, StackGuardStart = m.Stack((int)i), StackBytes = 0x3000,
                Kind = S.ResumeKind.Entry, ResumeRip = m.Code + P.Entries + i * P.WorkerStride };
            HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(d.Context, 688, d.ResumeRip,
                d.StackGuardStart + 8192 - 40, 0xffbf));
            ulong argument = o.ContextArena + 0x9000 + i * 8;
            bindings[i] = new() { Context = d, Argument = argument, Entry = new() { Owner = o, Initial = d, CodeOffset = P.Entries + i * P.WorkerStride,
                Entry = m.Code + P.Workers + i * P.WorkerStride, Argument = argument, Start = 0x200000,
                Control = control, ControlBytes = 256, ExitStub = m.Code + P.FromWorker + i * S.StubBytes, RejectRip = m.Code + P.Recovery } };
        }
        return new() { Owner = o, Supervisor = supervisor, Bindings = bindings, Count = count,
            BindingBytes = count * (uint)sizeof(P.Binding), Witness = (X64ThreadContext*)(o.ContextArena + 0x5000),
            Prepare = 0x200010, Gateway = 0x30000000, Recover = 0x200020, TerminalCallback = 0x200030,
            Stub = new() { CallbackContext = control, CallbackBytes = 256, AdmissionCallback = 0x200040,
                HelperFx = helper, JumpSlot = control + 256, RejectRip = m.Code + P.Recovery } };
    }
    private static ulong Target(byte* instruction) => unchecked((ulong)instruction + 5 + (ulong)(long)*(int*)(instruction + 1));

    [HostTest("linux.thread program rebinds an actually reaped generation by replacing only its entry")]
    private static void ReapedEntry()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var p = Plan(m, 2); HostCheck.True(P.Emit((byte*)m.Code, P.Bytes, p));
        p.Owner.GuardsVerified = p.Owner.MappingsVerified = true;
        for (uint i = 0; i < p.Count; i++) p.Bindings[i].Entry.Owner = p.Owner;
        m.Owner = p.Owner; m.Supervisor = p.Supervisor;
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Bind(2, 2, 2));
        for (uint i = 0; i < p.Count; i++)
        {
            var b = p.Bindings[i]; var d = b.Context;
            HostCheck.Equal(d.Identity, m.Table->Create(KernelThreadTableTests.Key, 2, d.Context, d.ContextBytes,
                d.StackGuardStart, d.StackBytes, d.ResumeRip, b.Entry.Entry, b.Argument).Identity.Value);
        }
        var old = p.Bindings[0].Context;
        HostCheck.Equal(old.Identity, m.Dispatch().Value);
        var captured = old; captured.Kind = S.ResumeKind.CapturedCall;
        captured.ResumeRip = m.Code + P.Workers + P.WorkerResume; captured.Context->Rip = captured.ResumeRip; captured.Context->Rsp -= 152;
        p.Bindings[0].Context = p.Bindings[0].Entry.Initial = captured;
        HostCheck.True(m.Table->Exit(KernelThreadTableTests.Key, 2, new(old.Identity), captured, 60).Succeeded); m.Confirm();
        HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p));
        byte[] code = new ReadOnlySpan<byte>((void*)m.Code, (int)P.Bytes).ToArray();
        HostCheck.True(m.Table->Reap(KernelThreadTableTests.Key, 2, new(old.Identity), m.SupervisorRsp).Succeeded);
        HostCheck.True(!P.Verify((byte*)m.Code, P.Bytes, p));
        HostCheck.True(code.AsSpan().SequenceEqual(new ReadOnlySpan<byte>((void*)m.Code, (int)P.Bytes)));
        HostCheck.True(new ReadOnlySpan<byte>(old.Context, (int)old.ContextBytes).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->InspectNextCreateIdentity(KernelThreadTableTests.Key, 2, out var next));
        HostCheck.Equal(old.Identity + (1UL << 32), next.Value);
        old.Identity = old.CurrentIdentity = next.Value; old.Kind = S.ResumeKind.Entry;
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(old.Context, old.ContextBytes,
            old.ResumeRip, old.StackGuardStart + old.StackBytes - 4096 - 40, p.Owner.ObservedMxcsrMask));
        p.Bindings[0].Context = p.Bindings[0].Entry.Initial = old;
        HostCheck.True(!P.Verify((byte*)m.Code, P.Bytes, p)); // Old identity is still embedded in the entry.
        HostCheck.True(KernelThreadEntry.Emit((byte*)m.Code + P.Entries, KernelThreadEntry.Bytes, p.Bindings[0].Entry));
        HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p));
        HostCheck.True(code.AsSpan(0, (int)P.Entries).SequenceEqual(new ReadOnlySpan<byte>((void*)m.Code, (int)P.Entries)));
        HostCheck.True(code.AsSpan((int)(P.Entries + KernelThreadEntry.Bytes)).SequenceEqual(
            new ReadOnlySpan<byte>((void*)(m.Code + P.Entries + KernelThreadEntry.Bytes), (int)(P.Bytes - P.Entries - KernelThreadEntry.Bytes))));
        var b0 = p.Bindings[0];
        HostCheck.Equal(next.Value, m.Table->Create(KernelThreadTableTests.Key, 2, old.Context, old.ContextBytes,
            old.StackGuardStart, old.StackBytes, old.ResumeRip, b0.Entry.Entry, b0.Argument).Identity.Value);
        HostCheck.Equal(KernelThreadTable.Status.StaleIdentity, m.Inspect(new(next.Value - (1UL << 32))).Code);
        HostCheck.Equal(p.Bindings[1].Context.Identity, m.Dispatch().Value); // Other live worker was retained.
    }

    [HostTest("linux.thread program has four distinct real CALL paths with aligned shadow space and full supervisor witness")]
    private static void Flow()
    {
        using KernelThreadTableTests.Model m = new(initialize: false); var p = Plan(m); var before = m.Snapshot();
        HostCheck.True(P.Emit((byte*)m.Code, P.Bytes, p)); HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p));
        HostCheck.True(before.AsSpan(0, 0x10000).SequenceEqual(new ReadOnlySpan<byte>(m.Memory, 0x10000)));
        HostCheck.True(before.AsSpan(0x14000).SequenceEqual(new ReadOnlySpan<byte>(m.Memory + 0x14000, KernelThreadTableTests.Model.Bytes - 0x14000)));
        for (uint i = 0; i < 4; i++)
        {
            byte* driverCall = (byte*)m.Code + i * 512 + 21;
            HostCheck.Equal((byte)0xe8, *driverCall); HostCheck.Equal(m.Code + 0x2000 + i * 1024, Target(driverCall));
            byte* worker = (byte*)m.Code + 0xc00 + i * 256;
            HostCheck.True(new ReadOnlySpan<byte>(worker, 4).SequenceEqual(new byte[] { 0x48, 0x83, 0xec, 0x28 }));
            HostCheck.Equal((byte)0xe9, worker[4]); HostCheck.Equal((ulong)worker + 0x10, Target(worker + 4));
            HostCheck.Equal((byte)0xba, worker[0x1a]); HostCheck.Equal(i, *(uint*)(worker + 0x1b));
            HostCheck.Equal(0x200010UL, *(ulong*)(worker + 0x21));
            HostCheck.True(worker[0x60] == 0x49 && worker[0x61] == 0xbb); HostCheck.Equal(0x30000000UL, *(ulong*)(worker + 0x62));
            HostCheck.True(worker[0x6a] == 0x41 && worker[0x6b] == 0xff && worker[0x6c] == 0xd3);
            HostCheck.Equal((byte)0xe9, worker[0x6d]); HostCheck.Equal((ulong)worker + 0x80, Target(worker + 0x6d));
            HostCheck.Equal((byte)0xe8, worker[0x85]); HostCheck.Equal(m.Code + 0x3000 + i * 1024, Target(worker + 0x85));
            HostCheck.Equal((byte)0xe9, worker[0x8a]); HostCheck.Equal((ulong)worker + 0x10, Target(worker + 0x8a));
            HostCheck.Equal(p.Bindings[i].Argument, *(ulong*)(m.Code + P.Entries + i * P.WorkerStride + 0x62));
        }
        HostCheck.True(*((byte*)m.Code + P.Idle) == 0xfb && *((byte*)m.Code + P.Idle + 1) == 0xf4 && *((byte*)m.Code + P.Idle + 2) == 0xfa);
        ((byte*)m.Code)[P.TimerHandler] = 0x90; HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p));
        ((byte*)m.Code)[P.Workers + 0x60] ^= 1; HostCheck.True(!P.Verify((byte*)m.Code, P.Bytes, p));
    }

    [HostTest("linux.thread program rejects incomplete duplicate stale aliased callback and gateway plans before mutation")]
    private static void Rejects()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        for (int variant = 0; variant < 18; variant++)
        {
            var p = Plan(m);
            switch (variant)
            {
                case 0: p.Count = 0; break;
                case 1: p.Count = 5; break;
                case 2: p.BindingBytes--; break;
                case 3: p.Gateway++; break;
                case 4: p.Gateway = 0x100000000; break;
                case 5: p.Prepare = p.Gateway + 0x600; break;
                case 6: p.Witness = p.Bindings[2].Context.Context; break;
                case 7: p.Stub.CallbackContext = (ulong)p.Bindings; break;
                case 8: p.Bindings[2].Entry.Owner.CurrentRoot++; break;
                case 9: p.Bindings[3].Context.CurrentIdentity++; break;
                case 10: p.Bindings[1].Entry.ExitStub = m.Code + P.FromWorker; break;
                case 11: p.Bindings[0].Entry.Entry = m.Code + P.Workers + 256; break;
                case 12: p.Supervisor.ResumeRip++; break;
                case 13: p.Prepare = p.Bindings[3].Context.StackGuardStart + 4096; break;
                case 14: p.Bindings[3].Context = p.Bindings[0].Context; p.Bindings[3].Entry.Initial = p.Bindings[0].Context; break;
                case 15: p.Bindings[0].Context.Context->Rsp = p.Bindings[0].Context.StackGuardStart + 4096 + 72; break;
                case 16: p.Bindings[2].Argument++; break;
                default: p.Bindings[2].Argument = p.Bindings[2].Entry.Argument = p.Owner.ContextArena + p.Owner.ContextArenaBytes - 4; break;
            }
            var before = m.Snapshot(); HostCheck.True(!P.Emit((byte*)m.Code, P.Bytes, p)); m.Same(before);
        }
    }

    [HostTest("linux.thread program verifies actual captured continuations and bounded counts with unused code sealed")]
    private static void Captures()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        for (uint count = 1; count <= 4; count++)
        {
            var p = Plan(m, count); HostCheck.True(P.Emit((byte*)m.Code, P.Bytes, p));
            for (uint i = count; i < 4; i++)
                HostCheck.True(new ReadOnlySpan<byte>((void*)(m.Code + P.ToWorker + i * 1024), 1024).IndexOfAnyExcept((byte)0xcc) == -1);
            for (uint i = 0; i < count; i++)
            {
                var d = p.Bindings[i].Context; d.Kind = S.ResumeKind.CapturedCall;
                d.ResumeRip = m.Code + P.Workers + i * 256 + 0x8a; d.Context->Rip = d.ResumeRip; d.Context->Rsp -= 152;
                p.Bindings[i].Context = p.Bindings[i].Entry.Initial = d;
            }
            p.Supervisor.ResumeRip = m.Code + (count - 1) * 512 + 26; p.Supervisor.Context->Rip = p.Supervisor.ResumeRip;
            HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p)); HostCheck.True(!P.Emit((byte*)m.Code, P.Bytes, p));
            ulong savedRsp = p.Bindings[0].Context.Context->Rsp;
            p.Bindings[0].Context.Context->Rsp = p.Bindings[0].Context.StackGuardStart + 4096 + 32;
            HostCheck.True(!P.Verify((byte*)m.Code, P.Bytes, p)); p.Bindings[0].Context.Context->Rsp = savedRsp;
            var bad = p.Bindings[0].Context; bad.ResumeRip++; bad.Context->Rip++;
            p.Bindings[0].Context = p.Bindings[0].Entry.Initial = bad;
            HostCheck.True(!P.Verify((byte*)m.Code, P.Bytes, p));
        }
    }
}
