using System.Runtime.InteropServices;
using OS.Kernel.Threading;

internal static unsafe partial class KernelThreadEntryTests
{
    private sealed class Model : IDisposable
    {
        internal readonly byte* Memory = (byte*)NativeMemory.AlignedAlloc(0x20000, 4096);
        internal byte* Code => Memory + 0x1000;

        internal KernelThreadEntry.Plan Plan;
        internal Model()
        {
            new Span<byte>(Memory, 0x20000).Fill(0xa5);
            var owner = new ThreadContextSwitch.Owner
            {
                Initialized = true,
                Key = 1,
                CurrentKey = 1,
                Root = 0x1000,
                CurrentRoot = 0x1000,
                Cr0 = 0x80000001,
                Cr4 = 0x220,
                Efer = 0xd00,
                Flags = 2,
                CpuCount = 1,
                BasicEdx = 0x07000000,
                ExtendedEdx = 0x20100000,
                ObservedMxcsrMask = 0xffbf,
                Cs = 8,
                Ss = 16,
                Tr = 24,
                CodeStart = (ulong)Code,
                CodeBytes = 4096,
                ContextArena = (ulong)Memory + 0x8000,
                ContextArenaBytes = 4096,
                SwitchStackGuardStart = (ulong)Memory + 0x18000,
                SwitchStackBytes = 0x3000
            };
            var initial = new ThreadContextSwitch.Descriptor
            {
                Identity = 0x100000001,
                CurrentIdentity = 0x100000001,
                Context = (X64ThreadContext*)(Memory + 0x8000),
                ContextBytes = 688,
                StackGuardStart = (ulong)Memory + 0x10000,
                StackBytes = 0x3000,
                ResumeRip = (ulong)Code,
                Kind = ThreadContextSwitch.ResumeKind.Entry
            };
            HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(initial.Context, 688, initial.ResumeRip, initial.StackGuardStart + 8192 - 40, 0xffbf));
            Plan = new()
            {
                Owner = owner,
                Initial = initial,
                Entry = (ulong)Code + 0x400,
                Argument = 0xfedcba9876543210,
                Start = 0x100000,
                Control = (ulong)Memory + 0x8400,
                ControlBytes = 64,
                ExitStub = (ulong)Code + 0x800,
                RejectRip = (ulong)Code + 0x700
            };
        }

        public void Dispose() => NativeMemory.AlignedFree(Memory);
    }
}
