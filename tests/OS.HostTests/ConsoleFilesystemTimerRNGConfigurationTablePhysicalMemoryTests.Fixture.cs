using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using Core = OS.Boot.ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory;

internal static unsafe partial class ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemoryTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Storage* State;
        public readonly byte* Arena;
        public readonly byte* Tables;
        private readonly byte* _vector;
        public FirstExitImageReferences References;
        public FirmwareAdapterBackends Backends;
        public Fixture()
        {
            State = (Storage*)NativeMemory.AlignedAlloc(4096, 4096);
            Arena = (byte*)NativeMemory.AlignedAlloc(FirstExitOwnedArena.Bytes, 4096);
            Tables = (byte*)NativeMemory.AlignedAlloc(3 * 4096, 4096);
            _vector = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
            Need(State != null && Arena != null && Tables != null && _vector != null, "fixture storage");
            new Span<byte>(State, 4096).Clear();
            new Span<byte>(Tables, 3 * 4096).Clear();
            Need(FirstExitOwnedArena.Materialize(ref State->Arena, Arena, FirstExitOwnedArena.Bytes, (ulong)_vector, 56), "original arena materialized");
            Need(FirstExitOwnedStackStub.Prepare(ref *State->Arena.StackEntry, 0x1000, 0x20, State->Arena.StackBottom, State->Arena.StackTop, FirstExitOwnedArena.LowerCanary, FirstExitOwnedArena.UpperCanary, 4096, &Entry, State, (ulong)sizeof(Storage)), "original owned stack prepared");
            byte* boot = Tables + 4096, map = Tables + 8192;
            *(ulong*)Tables = 0x5453595320494249;
            *(uint*)(Tables + 8) = 0x20000;
            *(uint*)(Tables + 12) = 128;
            *(ulong*)(Tables + 96) = (ulong)boot;
            *(ulong*)boot = 0x56524553544f4f42;
            *(uint*)(boot + 8) = 0x20000;
            *(uint*)(boot + 12) = 384;
            ulong get = (ulong)(delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> )&GetMap;
            ulong exit = (ulong)(delegate* unmanaged<void*, ulong, ulong> )&Exit;
            *(ulong*)(boot + 56) = get;
            *(ulong*)(boot + 232) = exit;
            Seal(Tables, 128);
            Seal(boot, 384);
            ulong low = Math.Min(get, exit) & ~4095UL;
            *(uint*)map = 3;
            *(ulong*)(map + 8) = low;
            *(ulong*)(map + 24) = (Math.Max(get, exit) - low) / 4096 + 1;
            var ownedMap = new FirmwareMemoryMapRecord
            {
                BufferAddress = (ulong)map,
                BufferSize = 48,
                MapSize = 48,
                DescriptorCount = 1,
                DescriptorSize = 48,
                DescriptorVersion = 1,
                Flags = FirmwareMemoryMapRecord.FlagOwnedCopy
            };
            Need(FirstExitFirmwareBinding.Bind(ref *State->Arena.Binding, FirmwareServicesMode.BootServicesAvailable, (void*)123, Tables, 128, boot, 384, ownedMap) == FirstExitBindingStatus.Ready, "original firmware slots bound");
            References = new FirstExitImageReferences
            {
                AdapterState = (ulong)State,
                AdapterStateBytes = (ulong)sizeof(Storage),
                EntryCallback = (ulong)(delegate* unmanaged<FirstExitOwnedStackContext*, void> )&Entry
            };
            Backends = new FirmwareAdapterBackends
            {
                Context = State,
                ContextBytes = (ulong)sizeof(Storage),
                Console = &ConsoleWrite,
                MemoryMap = &CaptureMap,
                FileSystem = &ReadFile,
                Timer = &ReadTimer,
                Random = &FillRandom,
                ConfigurationTable = &FindTable,
                PhysicalMemory = &ReadMemory,
                Framebuffer = &ReadFramebuffer
            };
        }

        public void Bind()
        {
            Need(Core.Bind(ref State->Authority, &State->Arena, References, Backends) == FirmwareAdapterStatus.Success, "bind admitted owner");
            State->Capabilities = new Capabilities
            {
                Console = Core.Console(ref State->Authority),
                Map = Core.MemoryMap(ref State->Authority),
                Files = Core.FileSystem(ref State->Authority),
                Timer = Core.Timer(ref State->Authority),
                Random = Core.Random(ref State->Authority),
                Tables = Core.ConfigurationTable(ref State->Authority),
                Memory = Core.PhysicalMemory(ref State->Authority),
                Framebuffer = Core.Framebuffer(ref State->Authority)
            };
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(State);
            NativeMemory.AlignedFree(Arena);
            NativeMemory.AlignedFree(Tables);
            NativeMemory.AlignedFree(_vector);
        }
    }
}
