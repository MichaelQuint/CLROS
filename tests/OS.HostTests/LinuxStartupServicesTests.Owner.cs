using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using OS.Std.Hash;

internal static unsafe partial class LinuxStartupServicesTests
{
    private sealed class Owner : IDisposable
    {
        public readonly LinuxMemoryServicesTests.Owner Memory;
        public readonly LinuxStartupServices.State* State;
        public readonly Model* Context;
        public readonly byte* Entropy;
        public readonly uint EntropyBytes;
        public Owner() : this(new LinuxMemoryServicesTests.Owner())
        {
        }

        public Owner(uint entropyBytes) : this(new LinuxMemoryServicesTests.Owner(), entropyBytes)
        {
        }

        public Owner(LinuxMemoryServicesTests.Owner memory, uint entropyBytes = 32)
        {
            Memory = memory;
            Memory.Begin();
            HostCheck.Equal((long)Output, LinuxMemoryServices.Map(Memory.State, Output, 4096, 3, 0x100022, ulong.MaxValue, 0));
            State = (LinuxStartupServices.State*)NativeMemory.AlignedAlloc((nuint)sizeof(LinuxStartupServices.State), 16);
            *State = default;
            Context = (Model*)NativeMemory.AlignedAlloc((nuint)sizeof(Model), 16);
            *Context = new()
            {
                Memory = Memory.Memory,
                Ticks = 10000
            };
            EntropyBytes = entropyBytes;
            Entropy = (byte*)NativeMemory.Alloc(entropyBytes);
            for (uint i = 0; i < entropyBytes; i++)
                Entropy[i] = (byte)(i % 251 + 1);
        }

        public LinuxStartupServices.Admission Admission() => new()
        {
            Memory = Memory.State,
            User = new()
            {
                Context = Context,
                PageAccess = &Permissions
            },
            CopyToUser = &Copy,
            ReadClock = &Clock,
            MemoryBarrier = &Fence,
            FrequencyHz = 100000000,
            PhysicalBytes = 256 * 1024 * 1024,
            AvailableBytes = 64 * 1024 * 1024,
            HeapStart = Heap,
            HeapBytes = 2 * 1024 * 1024,
            StackBytes = 65536,
            ProcessId = 1,
            Entropy = Entropy,
            CapturedEntropyBytes = EntropyBytes,
            SingleCpuValidated = true,
            FirmwareExited = true
        };
        public void Begin() => HostCheck.True(LinuxStartupServices.Begin(State, Admission()));
        public long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0) => LinuxStartupServices.Dispatch(State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2, Argument3 = a3, Argument4 = a4, Argument5 = a5 });
        public void Dispose()
        {
            Memory.Dispose();
            NativeMemory.AlignedFree(State);
            NativeMemory.AlignedFree(Context);
            NativeMemory.Free(Entropy);
        }
    }
}
