using OS.Kernel.Threading;
using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;
/// <summary>C#-authored cooperative kernel workers around the separately owned
/// CPL3 gateway. Emits unpublished bytes only. The IRQ-off native owner admits
/// RX code/callbacks, guarded stacks, descriptor/MSR/root restoration and actual
/// scheduler identities. No user image, mapping, thread or transfer is created.
/// Only the existing x87/SSE Win64 CALL context ABI is supported; no user IRQ
/// capture, asynchronous preemption, XSAVE, SMP or upstream runtime proof.</summary>

public static unsafe partial class LinuxThreadProgram
{
    public const uint Bytes = 53248, MaximumWorkers = 16, DriverStride = 512, DriverResume = 26;
    public const uint Entries = 0x2000, Workers = 0x3000, WorkerStride = 256;
    public const uint Loop = 0x10, EnterUser = 0x60, Handoff = 0x80, WorkerResume = 0x8a;
    public const uint Recovery = 0x4000, Terminal = 0x4200, TimerHandler = 0x4400;
    public const uint IrqSaveDisable = 0x4600, IrqRestore = 0x4610, StackPointer = 0x4620, Idle = 0x4630;
    public const uint ToWorker = 0x5000, FromWorker = 0x9000;
    public const uint UserFaultCapture = 0x4800;
    public const uint InitialFixedStackBytes = 168, CapturedFixedStackBytes = 80;
    // One retained native pool: RX program, supervisor RW/NX metadata, guarded
    // 16-page switch stack, sixteen guarded worker stacks (four data pages,
    // eight for signal/runtime callbacks, sixteen for continuing web), guarded
    // four-page IRQ stack and private IDT. The owner publishes/restores leaves;
    // these offsets alone grant no mappings, executable authority or lifetime.
    public const uint CodePages = Bytes / 4096, MetadataPage = CodePages, MetadataPages = 28;
    /// <summary>Control ceiling for the selected writable mechanical profile,
    /// checked against its actual native sizeof before metadata publication.
    /// The reviewed control is 2448 bytes; this ceiling grants no mappings or
    /// fit for web/static profiles and does not replace allocator end checks.</summary>

    public const uint WritableControlMaximumBytes = 2560;
    public const uint SwitchGuardPage = MetadataPage + MetadataPages, SwitchPages = 18;
    public const uint WorkerPage = SwitchGuardPage + SwitchPages;
    /// <summary>Bounded signal/runtime callback extent including both unmapped
    /// guards. Layout values grant no page publication or execution authority.</summary>

    public const uint SignalWorkerPages = 10;
    /// <summary>Retained async socket name for the same guarded callback extent.</summary>

    public const uint AsyncWorkerPages = SignalWorkerPages;
#if M10_004_PROBE
    // The web interval and mapping scratch arrays enlarge the measured native
    // Protect/Apply/table-admission chain. Sixteen data pages retain margin;
    // both unmapped guards and every derived worker/IRQ/pool bound stay in force.
    public const uint WorkerPages = 18;
#elif M16_009_PROBE || M16_012_PROBE || M07_027_PROBE
    // Pending signal delivery after partial pipe completion retains callbacks
    // around descriptor/page audits, as async completion does. Both original
    // native paths exhausted four usable pages. Keep eight between both guards;
    // the switch/IRQ stacks and mechanical thread-only profile stay unchanged.
    public const uint WorkerPages = SignalWorkerPages;
#else
    public const uint WorkerPages = 6;
#endif
    public const uint IrqGuardPage = WorkerPage + MaximumWorkers * WorkerPages, IrqPages = 6;
    public const uint IrqTopPage = IrqGuardPage + IrqPages - 1, IdtPage = IrqGuardPage + IrqPages, PoolPages = IdtPage + 1;
    /// <summary>Selected signal/runtime pool extent for host transcript checks;
    /// includes all worker guards, the IRQ extent and private IDT.</summary>

    public const uint SignalPoolPages = WorkerPage + MaximumWorkers * SignalWorkerPages + IrqPages + 1;
    /// <summary>Identify only guard leaves inside this fixed pool. Publication
    /// remains the external IRQ-off owner's responsibility. Out-of-range pages
    /// return false and must separately fail the owner's extent admission.</summary>

    public static bool IsGuardPage(uint page) => page == SwitchGuardPage || page == WorkerPage - 1 || page >= WorkerPage && page < IrqGuardPage && ((page - WorkerPage) % WorkerPages == 0 || (page - WorkerPage) % WorkerPages == WorkerPages - 1) || page == IrqGuardPage || page == IrqTopPage;
    /// <summary>Describe the next contiguous supervisor mapping in the retained
    /// low-4GiB pool, skipping guards. Code is RX; usable metadata/stacks are
    /// RW/NX. The caller owns inactive-root construction and retains these values
    /// through every memory-service rebuild. This does not publish code or change
    /// the active kernel root. Invalid extent/cursor returns false and zero outputs.</summary>

    public static bool TrySupervisorMapping(ulong pool, uint page, out X64UserMappingRange mapping, out uint nextPage)
    {
        mapping = default;
        nextPage = 0;
        if (pool == 0 || (pool & 4095) != 0 || pool > 0x100000000UL - PoolPages * 4096UL || page >= PoolPages)
            return false;
        while (page < PoolPages && IsGuardPage(page))
            page++;
        if (page == PoolPages)
            return false;
        uint start = page;
        bool code = page < CodePages;
        do
        {
            page++;
        }
        while (page < PoolPages && !IsGuardPage(page) && (page < CodePages) == code);
        mapping = new()
        {
            VirtualStart = pool + start * 4096UL,
            PhysicalStart = pool + start * 4096UL,
            Pages = page - start,
            Protection = PageProtection.Read | (code ? PageProtection.Execute : PageProtection.Write),
            Cache = PageCachePolicy.WriteBack,
            User = false
        };
        nextPage = page;
        return true;
    }

    /// <summary>Validate every component and dry encode before any mutation.
    /// Each worker starts at its complete entry with space for all 168 fixed
    /// entry/worker/gateway stack bytes. The owner separately proves additional
    /// native callback stack budgets. Captured continuations retain at least
    /// 80 lower bytes for the next fixed gateway frame. Caller storage must be writable
    /// and unpublished; emission changes no context/control/stack or gateway.</summary>

    public static bool Emit(byte* output, uint capacity, Plan plan) => Inputs(output, capacity, plan, true) && Encode(null, plan, false) && Encode(output, plan, false);
    /// <summary>Compare against current retained entry/captured bindings without
    /// mutation. The independent timer region is excluded and must be checked
    /// by its owner. Matching bytes establish no native execution authority.</summary>

    public static bool Verify(byte* output, uint capacity, Plan plan) => Inputs(output, capacity, plan, false) && Encode(output, plan, true);
    private static bool Inputs(byte* output, uint capacity, Plan p, bool initial)
    {
        var o = p.Owner;
        if (capacity < Bytes || !Range((ulong)output, Bytes) || ((ulong)output & 15) != 0 || o.CodeBytes != Bytes || ThreadContextSwitch.AdmitPlan(o) != ThreadContextSwitch.Status.Ok || ThreadContextSwitch.AdmitResumePlan(o, p.Supervisor) != ThreadContextSwitch.Status.Ok || p.Count == 0 || p.Count > MaximumWorkers || p.BindingBytes < p.Count * (uint)sizeof(Binding) || ((ulong)p.Bindings & 7) != 0 || !Contains(o.ContextArena, o.ContextArenaBytes, (ulong)p.Bindings, p.BindingBytes) || p.Supervisor.Kind != ThreadContextSwitch.ResumeKind.CapturedCall || p.Supervisor.ResumeRip < o.CodeStart + DriverResume || (p.Supervisor.ResumeRip - o.CodeStart - DriverResume) % DriverStride != 0 || (p.Supervisor.ResumeRip - o.CodeStart - DriverResume) / DriverStride >= p.Count || p.Stub.RejectRip != o.CodeStart + Recovery || ((ulong)p.Witness & 15) != 0 || !Contains(o.ContextArena, o.ContextArenaBytes, (ulong)p.Witness, X64ThreadContext.Bytes) || !Callback(p, p.Prepare) || !Callback(p, p.Recover) || !Callback(p, p.TerminalCallback) || !Callback(p, p.Stub.AdmissionCallback) || p.Gateway < 4096 || p.Gateway >= 0x100000000 || (p.Gateway & 4095) != 0 || !Range(p.Gateway, X64LinuxUserTransition.CodeBytes) || Overlap(p.Gateway, X64LinuxUserTransition.CodeBytes, o.CodeStart, Bytes) || Overlap(p.Gateway, X64LinuxUserTransition.CodeBytes, o.ContextArena, o.ContextArenaBytes) || Overlap(p.Gateway, X64LinuxUserTransition.CodeBytes, o.SwitchStackGuardStart, o.SwitchStackBytes) || Overlap((ulong)output, Bytes, p.Gateway, X64LinuxUserTransition.CodeBytes) || Overlap((ulong)output, Bytes, o.ContextArena, o.ContextArenaBytes) || Overlap((ulong)output, Bytes, o.SwitchStackGuardStart, o.SwitchStackBytes) || Overlap((ulong)output, Bytes, p.Supervisor.StackGuardStart, p.Supervisor.StackBytes))
            return false;
        ulong* retained = stackalloc ulong[10]
        {
            (ulong)p.Bindings,
            p.BindingBytes,
            (ulong)p.Witness,
            X64ThreadContext.Bytes,
            p.Stub.CallbackContext,
            p.Stub.CallbackBytes,
            p.Stub.HelperFx,
            512,
            p.Stub.JumpSlot,
            8
        };
        for (uint i = 0; i < 5; i++)
        {
            if (retained[i * 2 + 1] == 0 || Overlap(retained[i * 2], retained[i * 2 + 1], (ulong)p.Supervisor.Context, p.Supervisor.ContextBytes))
                return false;
            for (uint j = 0; j < i; j++)
                if (Overlap(retained[i * 2], retained[i * 2 + 1], retained[j * 2], retained[j * 2 + 1]))
                    return false;
        }

        for (uint i = 0; i < p.Count; i++)
        {
            var b = p.Bindings[i];
            var d = b.Context;
            var e = b.Entry;
            uint entry = Entries + i * WorkerStride, worker = Workers + i * WorkerStride;
            if (!KernelThreadProgram.SameOwner(o, e.Owner) || !Same(d, e.Initial) || e.CodeOffset != entry || e.Entry != o.CodeStart + worker || e.Argument != b.Argument || !Contains(o.ContextArena, o.ContextArenaBytes, b.Argument, 8) || e.Control != p.Stub.CallbackContext || e.ControlBytes != p.Stub.CallbackBytes || e.ExitStub != o.CodeStart + FromWorker + i * ThreadContextSwitch.StubBytes || e.RejectRip != p.Stub.RejectRip || !Callback(p, e.Start) || !KernelThreadEntry.Inputs(output + entry, WorkerStride, e, initial) || d.Context->Rsp < d.StackGuardStart + ThreadContextSwitch.Page + (d.Kind == ThreadContextSwitch.ResumeKind.Entry ? InitialFixedStackBytes : CapturedFixedStackBytes) || (d.Kind == ThreadContextSwitch.ResumeKind.Entry ? d.ResumeRip != o.CodeStart + entry : d.ResumeRip != o.CodeStart + worker + WorkerResume && d.ResumeRip != o.CodeStart + entry + KernelThreadEntry.ExitResumeOffset) || Overlap((ulong)output, Bytes, d.StackGuardStart, d.StackBytes) || Overlap(p.Gateway, X64LinuxUserTransition.CodeBytes, d.StackGuardStart, d.StackBytes) || StackCallback(p, d.StackGuardStart, d.StackBytes))
                return false;
            for (uint j = 0; j < 5; j++)
                if (Overlap(retained[j * 2], retained[j * 2 + 1], (ulong)d.Context, d.ContextBytes))
                    return false;
            for (uint j = 0; j < i; j++)
            {
                var other = p.Bindings[j].Context;
                if (d.Identity == other.Identity || Overlap((ulong)d.Context, d.ContextBytes, (ulong)other.Context, other.ContextBytes) || Overlap(d.StackGuardStart, d.StackBytes, other.StackGuardStart, other.StackBytes))
                    return false;
            }

            for (uint direction = 0; direction < 2; direction++)
            {
                var stub = p.Stub;
                stub.CodeOffset = (direction == 0 ? ToWorker : FromWorker) + i * ThreadContextSwitch.StubBytes;
                if (!ThreadContextSwitch.StubInputs(output + stub.CodeOffset, ThreadContextSwitch.StubBytes, o, direction == 0 ? p.Supervisor : d, direction == 0 ? d : p.Supervisor, stub, true))
                    return false;
            }
        }

        return !Overlap(p.Gateway, X64LinuxUserTransition.CodeBytes, p.Supervisor.StackGuardStart, p.Supervisor.StackBytes) && !StackCallback(p, p.Supervisor.StackGuardStart, p.Supervisor.StackBytes);
    }

    private static bool Encode(byte* output, Plan p, bool compare)
    {
        ulong code = p.Owner.CodeStart;
        for (uint i = 0; i < MaximumWorkers; i++)
        {
            uint driver = i * DriverStride, entry = Entries + i * WorkerStride, worker = Workers + i * WorkerStride;
            ThreadContextSwitch.StubWriter d = new(output == null ? null : output + driver, code + driver, compare, DriverStride);
            ThreadContextSwitch.StubWriter w = new(output == null ? null : output + worker, code + worker, compare, WorkerStride);
            if (i < p.Count)
            {
                d.Bytes(0xfa1e0ff3, 4);
                d.Bytes(0x9c, 1);
                d.Bytes(0x57565553, 4);
                d.Bytes(0x5741564155415441, 8);
                d.Bytes(0x20ec8348, 4);
                d.Call(code + ToWorker + i * ThreadContextSwitch.StubBytes);
                ThreadContextSwitch.WriteWitness(ref d, (ulong)p.Witness, code + driver + DriverResume);
                d.Bytes(0x20c48348, 4);
                d.Bytes(0x5c415d415e415f41, 8);
                d.Bytes(0x5b5d5e5f, 4);
                d.Bytes(0x9d, 1);
                d.Bytes(0x00000001b8, 5);
                d.Bytes(0xc3, 1);
                if (!KernelThreadEntry.Encode(output == null ? null : output + entry, p.Bindings[i].Entry, compare))
                    return false;
                w.Bytes(0x28ec8348, 4);
                w.Jump(code + worker + Loop);
                w.Pad(Loop); // CALL-aligned shadow; never execute INT3 padding.
                w.Immediate(1, p.Stub.CallbackContext);
                w.Bytes(0xba, 1);
                w.Bytes(i, 4);
                w.Immediate(11, p.Prepare);
                w.Bytes(0xd3ff41, 3);
                w.Bytes(0x013c, 2);
                w.Bytes(0x840f, 2);
                w.Displacement(code + worker + EnterUser, 4);
                w.Bytes(0x023c, 2);
                w.Reject(code + Recovery);
                w.Jump(code + worker + Handoff);
                w.Pad(EnterUser);
                w.Immediate(11, p.Gateway);
                w.Bytes(0xd3ff41, 3);
                w.Jump(code + worker + Handoff);
                w.Pad(Handoff);
                w.Bytes(0xba, 1);
                w.Bytes(0, 4);
                w.Call(code + FromWorker + i * ThreadContextSwitch.StubBytes);
                w.Jump(code + worker + Loop);
            }
            else
            {
                ThreadContextSwitch.StubWriter unused = new(output == null ? null : output + entry, code + entry, compare, WorkerStride);
                unused.Pad();
                if (!unused.Valid)
                    return false;
            }

            d.Pad();
            w.Pad();
            if (!d.Valid || !w.Valid)
                return false;
            for (uint direction = 0; direction < 2; direction++)
            {
                var stub = p.Stub;
                stub.CodeOffset = (direction == 0 ? ToWorker : FromWorker) + i * ThreadContextSwitch.StubBytes;
                ThreadContextSwitch.StubWriter s = new(output == null ? null : output + stub.CodeOffset, code + stub.CodeOffset, compare);
                if (i < p.Count)
                {
                    var context = p.Bindings[i].Context;
                    if (!ThreadContextSwitch.EncodeStub(ref s, p.Owner, direction == 0 ? p.Supervisor : context, direction == 0 ? context : p.Supervisor, stub))
                        return false;
                }
                else
                {
                    s.Pad();
                    if (!s.Valid)
                        return false;
                }
            }
        }

        ThreadContextSwitch.StubWriter r = new(output == null ? null : output + Recovery, code + Recovery, compare, 512);
        r.Bytes(0xfc, 1);
        r.Fx(true, p.Stub.HelperFx);
        r.Immediate(1, p.Stub.CallbackContext);
        r.Immediate(2, (ulong)p.Supervisor.Context);
        r.Immediate(11, p.Recover);
        r.Bytes(0x20ec8348, 4);
        r.Bytes(0xd3ff41, 3);
        r.Bytes(0x20c48348, 4);
        r.Bytes(0x013c, 2);
        r.Reject(code + Terminal);
        ThreadContextSwitch.Restore(ref r, (ulong)p.Supervisor.Context, p.Stub.JumpSlot, code + Terminal);
        r.Pad();
        if (!r.Valid)
            return false;
        ThreadContextSwitch.StubWriter terminal = new(output == null ? null : output + Terminal, code + Terminal, compare, 512);
        terminal.Bytes(0xfafc, 2);
        terminal.Fx(true, p.Stub.HelperFx);
        terminal.Immediate(1, p.Stub.CallbackContext);
        terminal.Immediate(11, p.TerminalCallback);
        terminal.Bytes(0x20ec8348, 4);
        terminal.Bytes(0xd3ff41, 3);
        terminal.Bytes(0x0b0f, 2);
        terminal.Pad();
        if (!terminal.Valid)
            return false;
        if (!compare && output != null)
            for (uint i = TimerHandler; i < TimerHandler + 512; i++)
                output[i] = 0xcc;
        ThreadContextSwitch.StubWriter helpers = new(output == null ? null : output + IrqSaveDisable, code + IrqSaveDisable, compare, 512);
        helpers.Bytes(0xc3fa589c, 4);
        helpers.Pad(0x10);
        helpers.Bytes(0xc39d51, 3);
        helpers.Pad(0x20);
        helpers.Bytes(0xe08948, 3);
        helpers.Bytes(0x08c08348, 4);
        helpers.Bytes(0xc3, 1);
        helpers.Pad(0x30);
        helpers.Bytes(0xfaf4fb, 3);
        helpers.Bytes(0x00000001b8, 5);
        helpers.Bytes(0xc3, 1);
        helpers.Pad();
        if (!helpers.Valid)
            return false;
        ThreadContextSwitch.StubWriter gap = new(output == null ? null : output + UserFaultCapture, code + UserFaultCapture, compare, 0x800);
        if (p.SeparateUserFaultCapture)
            return true;
        gap.Pad();
        return gap.Valid;
    }

    private static bool Same(ThreadContextSwitch.Descriptor a, ThreadContextSwitch.Descriptor b) => a.Identity == b.Identity && a.CurrentIdentity == b.CurrentIdentity && a.Context == b.Context && a.ContextBytes == b.ContextBytes && a.StackGuardStart == b.StackGuardStart && a.StackBytes == b.StackBytes && a.ResumeRip == b.ResumeRip && a.Kind == b.Kind;
    private static bool Callback(Plan p, ulong address) => Range(address, 1) && !Contains(p.Owner.CodeStart, Bytes, address, 1) && !Contains(p.Owner.ContextArena, p.Owner.ContextArenaBytes, address, 1) && !Contains(p.Owner.SwitchStackGuardStart, p.Owner.SwitchStackBytes, address, 1) && !Contains(p.Gateway, X64LinuxUserTransition.CodeBytes, address, 1);
    private static bool StackCallback(Plan p, ulong start, ulong bytes)
    {
        if (Contains(start, bytes, p.Prepare, 1) || Contains(start, bytes, p.Recover, 1) || Contains(start, bytes, p.TerminalCallback, 1) || Contains(start, bytes, p.Stub.AdmissionCallback, 1))
            return true;
        for (uint i = 0; i < p.Count; i++)
            if (Contains(start, bytes, p.Bindings[i].Entry.Start, 1))
                return true;
        return false;
    }

    private static bool Range(ulong start, ulong bytes) => start != 0 && bytes != 0 && bytes - 1 <= ulong.MaxValue - start && (start >> 48 == ((start & (1UL << 47)) == 0 ? 0UL : 0xffffUL)) && ((start + bytes - 1) >> 48 == (((start + bytes - 1) & (1UL << 47)) == 0 ? 0UL : 0xffffUL)) && ((start ^ (start + bytes - 1)) & (1UL << 47)) == 0;
    private static bool Contains(ulong outer, ulong bytes, ulong inner, ulong count) => Range(inner, count) && inner >= outer && inner - outer <= bytes && count <= bytes - (inner - outer);
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a <= b + (bn - 1) && b <= a + (an - 1);
}
