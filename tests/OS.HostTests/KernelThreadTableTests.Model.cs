using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Threading;
using OS.Pal;
using S = OS.Kernel.Threading.KernelThreadTable.Status;
using T = OS.Kernel.Threading.KernelThreadTable.State;
using W = OS.Kernel.Threading.KernelThreadTable.WaitKind;
using C = OS.Kernel.Threading.KernelThreadTable.Completion;

internal static unsafe partial class KernelThreadTableTests
{
    internal sealed class Model : IDisposable
    {
        internal const int Bytes = 0x90000;
        private readonly bool _extended;
        internal readonly byte* Memory = (byte*)NativeMemory.AlignedAlloc(Bytes, 4096);
        internal KernelThreadTable* Table => (KernelThreadTable*)Memory;
        internal KernelThreadSlot* Slots => (KernelThreadSlot*)(Memory + 0x2000);
        internal OSHostHandle* Ready => (OSHostHandle*)(Memory + 0x4000);
        internal DeadlineQueue.Entry* Deadlines => (DeadlineQueue.Entry*)(Memory + 0x5000);
        internal ulong Code => (ulong)Memory + 0x10000;
        internal ulong SupervisorRsp => Supervisor.StackGuardStart + 8192 - 64;

        internal ThreadContextSwitch.Owner Owner;
        internal ThreadContextSwitch.Descriptor Supervisor;
        internal Model(uint capacity = 3, uint ready = 3, uint deadlines = 3, bool initialize = true, uint generation = 1, ulong sequence = 0)
        {
            _extended = capacity > 8;
            new Span<byte>(Memory, Bytes).Fill(0xa5);
            *Table = default;
            Owner = new()
            {
                Initialized = true,
                GuardsVerified = true,
                MappingsVerified = true,
                Key = Key,
                CurrentKey = Key,
                Root = 0x1000,
                CurrentRoot = 0x1000,
                Flags = 2,
                Cr0 = 0x80010033,
                Cr4 = 0x220,
                Efer = 0xd00,
                CpuCount = 1,
                BasicEdx = 0x07000000,
                ExtendedEdx = 0x20100000,
                ObservedMxcsrMask = 0xffbf,
                Cs = 8,
                Ss = 16,
                Tr = 24,
                ContextArena = (ulong)Memory + 0x20000,
                ContextArenaBytes = 0x10000,
                CodeStart = Code,
                CodeBytes = 0x10000,
                SwitchStackGuardStart = (ulong)Memory + 0x30000,
                SwitchStackBytes = 0x3000
            };
            Supervisor = new()
            {
                Identity = 0x1ffffffff,
                CurrentIdentity = 0x1ffffffff,
                Context = (X64ThreadContext*)(Memory + 0x20000),
                ContextBytes = 688,
                StackGuardStart = (ulong)Memory + 0x34000,
                StackBytes = 0x3000,
                ResumeRip = Code + 0x80,
                Kind = ThreadContextSwitch.ResumeKind.CapturedCall
            };
            HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(Supervisor.Context, 688, Supervisor.ResumeRip, SupervisorRsp, 0xffbf));
            for (int i = 0; i < (_extended ? 16 : 8); i++)
                Prepare(i);
            if (initialize)
                HostCheck.Equal(S.Ok, Bind(capacity, ready, deadlines, generation, sequence));
        }

        internal S Bind(uint capacity = 3, uint ready = 3, uint deadlines = 3, uint generation = 1, ulong sequence = 0, ulong flags = 2, ulong key = Key, KernelThreadSlot* slots = null, OSHostHandle* readyBuffer = null) => Table->Initialize(key, flags, Owner, Supervisor, slots == null ? Slots : slots, capacity * (uint)sizeof(KernelThreadSlot), capacity, readyBuffer == null ? Ready : readyBuffer, ready * 8, ready, Deadlines, deadlines * (uint)sizeof(DeadlineQueue.Entry), deadlines, 0, generation, sequence);
        internal X64ThreadContext* Context(int i) => (X64ThreadContext*)(Memory + 0x21000 + i * (_extended ? 0x2b0 : 0x1000));
        internal ulong Stack(int i) => (ulong)Memory + 0x40000 + (uint)i * 0x4000;
        internal void Prepare(int i) => HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(Context(i), 688, Code + 0x100, Stack(i) + 8192 - 72, 0xffbf));
        internal KernelThreadTable.Result Create(int i) => Table->Create(Key, 2, Context(i), 688, Stack(i), 0x3000, Code + 0x100, Code + 0x400 + (uint)i * 0x40, 0xabcd0000 + (uint)i);
        internal KernelThreadTable.Result Inspect(OSHostHandle id) => Table->Inspect(Key, 2, id);
        internal OSHostHandle Dispatch()
        {
            var r = Table->Dispatch(Key, 2);
            HostCheck.True(r.Succeeded);
            return r.Identity;
        }

        internal ThreadContextSwitch.Descriptor Capture(OSHostHandle id)
        {
            var r = Inspect(id);
            HostCheck.True(r.Succeeded);
            var d = r.Context;
            d.Kind = ThreadContextSwitch.ResumeKind.CapturedCall;
            d.ResumeRip = Code + 0x240;
            d.Context->Rip = d.ResumeRip;
            d.Context->Rsp = d.StackGuardStart + d.StackBytes - 4096 - 64;
            return d;
        }

        internal void Confirm() => HostCheck.Equal(S.Ok, Table->ConfirmSupervisor(Key, 2, SupervisorRsp));
        internal void Exit(OSHostHandle id, int value = 0)
        {
            HostCheck.True(Table->Exit(Key, 2, id, Capture(id), value).Succeeded);
            Confirm();
        }

        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Memory, Bytes).ToArray();
        internal void Same(byte[] before) => HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(Memory, Bytes)));
        // No code or hardware borrows exist in this isolated host model. Discarding
        // the arena here is not evidence for production off-stack Release/Reap.
        public void Dispose() => NativeMemory.AlignedFree(Memory);
    }
}
