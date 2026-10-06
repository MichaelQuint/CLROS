using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using Core = OS.Boot.ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory;

internal static unsafe class ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemoryTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("typed operations enter their bound backends and preserve status", Positive);
        Run("copied capabilities observe every terminal authority cutoff", Cutoffs);
        Run("consumed binding rejects early failure and reentry without backend access", EarlyFailure);
        Run("invalid binding and copied or rebound owners fail closed", Identity);
        Run("output aliases and invalid extents reject without backend entry", Ranges);
        Run("unavailable backends still observe original authority first", Unavailable);
    }

    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START m03.008 " + name);
        try { test(); Console.WriteLine("TEST_PASS m03.008 " + name); }
        catch (Exception e) { Console.WriteLine("TEST_FAIL m03.008 " + name + " " + e.Message); throw; }
    }
    private static void Need(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private struct Capabilities
    {
        public FirmwareConsoleCapability Console;
        public FirmwareMemoryMapCapability Map;
        public FirmwareFileSystemCapability Files;
        public FirmwareTimerCapability Timer;
        public FirmwareRandomCapability Random;
        public FirmwareConfigurationTableCapability Tables;
        public FirmwarePhysicalMemoryCapability Memory;
        public FirmwareFramebufferCapability Framebuffer;
    }

    private struct Storage
    {
        public FirstExitOwnedArenaState Arena;
        public FirmwareAdapterAuthority Authority;
        public FirmwareAdapterAuthority OtherAuthority;
        public Capabilities Capabilities;
        public fixed ulong Calls[8];
        public ulong ReturnStatus, SeenHandle, SeenOffset, SeenGuidLow, SeenGuidHigh;
        public uint Reenter;
        public FirmwareAdapterStatus ReenteredStatus;
    }

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
            Need(FirstExitOwnedArena.Materialize(ref State->Arena, Arena, FirstExitOwnedArena.Bytes,
                (ulong)_vector, 56), "original arena materialized");
            Need(FirstExitOwnedStackStub.Prepare(ref *State->Arena.StackEntry, 0x1000, 0x20,
                State->Arena.StackBottom, State->Arena.StackTop,
                FirstExitOwnedArena.LowerCanary, FirstExitOwnedArena.UpperCanary, 4096,
                &Entry, State, (ulong)sizeof(Storage)), "original owned stack prepared");
            byte* boot = Tables + 4096, map = Tables + 8192;
            *(ulong*)Tables = 0x5453595320494249; *(uint*)(Tables + 8) = 0x20000; *(uint*)(Tables + 12) = 128;
            *(ulong*)(Tables + 96) = (ulong)boot;
            *(ulong*)boot = 0x56524553544f4f42; *(uint*)(boot + 8) = 0x20000; *(uint*)(boot + 12) = 384;
            ulong get = (ulong)(delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong>)&GetMap;
            ulong exit = (ulong)(delegate* unmanaged<void*, ulong, ulong>)&Exit;
            *(ulong*)(boot + 56) = get; *(ulong*)(boot + 232) = exit;
            Seal(Tables, 128); Seal(boot, 384);
            ulong low = Math.Min(get, exit) & ~4095UL;
            *(uint*)map = 3; *(ulong*)(map + 8) = low;
            *(ulong*)(map + 24) = (Math.Max(get, exit) - low) / 4096 + 1;
            var ownedMap = new FirmwareMemoryMapRecord { BufferAddress = (ulong)map, BufferSize = 48,
                MapSize = 48, DescriptorCount = 1, DescriptorSize = 48, DescriptorVersion = 1,
                Flags = FirmwareMemoryMapRecord.FlagOwnedCopy };
            Need(FirstExitFirmwareBinding.Bind(ref *State->Arena.Binding,
                FirmwareServicesMode.BootServicesAvailable, (void*)123, Tables, 128, boot, 384, ownedMap)
                == FirstExitBindingStatus.Ready, "original firmware slots bound");
            References = new FirstExitImageReferences { AdapterState = (ulong)State,
                AdapterStateBytes = (ulong)sizeof(Storage), EntryCallback = (ulong)(delegate* unmanaged<FirstExitOwnedStackContext*, void>)&Entry };
            Backends = new FirmwareAdapterBackends { Context = State, ContextBytes = (ulong)sizeof(Storage),
                Console = &ConsoleWrite, MemoryMap = &CaptureMap, FileSystem = &ReadFile, Timer = &ReadTimer,
                Random = &FillRandom, ConfigurationTable = &FindTable, PhysicalMemory = &ReadMemory, Framebuffer = &ReadFramebuffer };
        }

        public void Bind()
        {
            Need(Core.Bind(ref State->Authority, &State->Arena, References, Backends) == FirmwareAdapterStatus.Success,
                "bind admitted owner");
            State->Capabilities = new Capabilities { Console = Core.Console(ref State->Authority),
                Map = Core.MemoryMap(ref State->Authority), Files = Core.FileSystem(ref State->Authority),
                Timer = Core.Timer(ref State->Authority), Random = Core.Random(ref State->Authority),
                Tables = Core.ConfigurationTable(ref State->Authority), Memory = Core.PhysicalMemory(ref State->Authority),
                Framebuffer = Core.Framebuffer(ref State->Authority) };
        }
        public void Dispose()
        { NativeMemory.AlignedFree(State); NativeMemory.AlignedFree(Arena); NativeMemory.AlignedFree(Tables); NativeMemory.AlignedFree(_vector); }
    }

    private static void Seal(byte* table, int size)
    { *(uint*)(table + 16) = 0; *(uint*)(table + 16) = OS.Std.Hash.Crc32.Compute(new ReadOnlySpan<byte>(table, size)); }
    [UnmanagedCallersOnly] private static void Entry(FirstExitOwnedStackContext* context) { }
    [UnmanagedCallersOnly] private static ulong GetMap(ulong* size, void* data, ulong* key, ulong* descriptor, uint* version) => 0;
    [UnmanagedCallersOnly] private static ulong Exit(void* handle, ulong key) => 0;
    [UnmanagedCallersOnly] private static ulong ConsoleWrite(void* context, char* text, ulong count)
    { Storage* s = (Storage*)context; s->Calls[0]++; return count == 2 && text[0] == 'O' && text[1] == 'K' ? s->ReturnStatus : 99; }
    [UnmanagedCallersOnly] private static ulong CaptureMap(void* context, byte* data, ulong capacity, FirmwareAdapterMemoryMapInfo* info)
    { Storage* s = (Storage*)context; s->Calls[1]++; data[0] = 0x11; *info = new() { Bytes = 48, MapKey = 7, DescriptorBytes = 48, DescriptorVersion = 1 }; return s->ReturnStatus; }
    [UnmanagedCallersOnly] private static ulong ReadFile(void* context, ulong handle, ulong offset, byte* data, ulong capacity, ulong* actual)
    { Storage* s = (Storage*)context; s->Calls[2]++; s->SeenHandle = handle; s->SeenOffset = offset; data[1] = 0x22; *actual = 2; return s->ReturnStatus; }
    [UnmanagedCallersOnly] private static ulong ReadTimer(void* context, ulong* value)
    {
        Storage* s = (Storage*)context; s->Calls[3]++;
        if (s->Reenter != 0)
        {
            s->Arena.StackEntry->Phase = 2;
            s->ReenteredStatus = s->Capabilities.Random.Fill((byte*)1, 16).Status;
        }
        *value = 0x1234; return s->ReturnStatus;
    }
    [UnmanagedCallersOnly] private static ulong FillRandom(void* context, byte* data, ulong bytes)
    { Storage* s = (Storage*)context; s->Calls[4]++; data[2] = 0x33; return s->ReturnStatus; }
    [UnmanagedCallersOnly] private static ulong FindTable(void* context, ulong low, ulong high, ulong* table)
    { Storage* s = (Storage*)context; s->Calls[5]++; s->SeenGuidLow = low; s->SeenGuidHigh = high; *table = 0x2345; return s->ReturnStatus; }
    [UnmanagedCallersOnly] private static ulong ReadMemory(void* context, ulong address, byte* destination, ulong bytes)
    { Storage* s = (Storage*)context; s->Calls[6]++; destination[3] = *(byte*)address; return s->ReturnStatus; }
    [UnmanagedCallersOnly] private static ulong ReadFramebuffer(void* context, FirmwareAdapterFramebufferInfo* info)
    { Storage* s = (Storage*)context; s->Calls[7]++; *info = new() { BaseAddress = 0x100000, ByteLength = 64, Width = 4, Height = 4, StridePixels = 4, PixelFormat = 1 }; return s->ReturnStatus; }

    // Exercise complete typed capabilities, including actual reads/writes, from a
    // copied handle set. Denial must preserve every output, not just a marker.
    private static void Exercise(Fixture f, FirmwareAdapterStatus expected)
    {
        Capabilities copies = f.State->Capabilities;
        byte* data = stackalloc byte[256]; new Span<byte>(data, 256).Fill(0xa5);
        char* text = (char*)data; text[0] = 'O'; text[1] = 'K'; data[240] = 0x44;
        byte[] before = new ReadOnlySpan<byte>(data, 256).ToArray();
        ulong* counts = stackalloc ulong[8]; for (int i = 0; i < 8; i++) counts[i] = f.State->Calls[i];
        var map = (FirmwareAdapterMemoryMapInfo*)(data + 80);
        var framebuffer = (FirmwareAdapterFramebufferInfo*)(data + 160);
        FirmwareAdapterCallResult* results = stackalloc FirmwareAdapterCallResult[8];
        results[0] = copies.Console.Write(text, 2);
        results[1] = copies.Map.Capture(data + 16, 48, map);
        results[2] = copies.Files.Read(9, 17, data + 16, 48, (ulong*)(data + 120));
        results[3] = copies.Timer.Read((ulong*)(data + 128));
        results[4] = copies.Random.Fill(data + 16, 48);
        results[5] = copies.Tables.Find(123, 456, (ulong*)(data + 136));
        results[6] = copies.Memory.Read((ulong)data + 240, data + 16, 8);
        results[7] = copies.Framebuffer.Snapshot(framebuffer);
        bool called = expected == FirmwareAdapterStatus.Success || expected == FirmwareAdapterStatus.FirmwareError;
        for (int i = 0; i < 8; i++)
            Need(results[i].Status == expected && results[i].Succeeded == (expected == FirmwareAdapterStatus.Success) &&
                results[i].FirmwareStatus == (called ? f.State->ReturnStatus : 0) &&
                f.State->Calls[i] == counts[i] + (called ? 1UL : 0), "typed operation " + i);
        if (called)
            Need(data[16] == 0x11 && data[17] == 0x22 && data[18] == 0x33 && data[19] == 0x44 &&
                map->Bytes == 48 && map->MapKey == 7 && map->DescriptorVersion == 1 &&
                *(ulong*)(data + 120) == 2 && *(ulong*)(data + 128) == 0x1234 && *(ulong*)(data + 136) == 0x2345 &&
                framebuffer->BaseAddress == 0x100000 && f.State->SeenHandle == 9 && f.State->SeenOffset == 17 &&
                f.State->SeenGuidLow == 123 && f.State->SeenGuidHigh == 456, "typed arguments and outputs");
        else Need(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(data, 256)), "denial leaves all outputs unchanged");
    }

    private static void Positive()
    {
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<FirmwareAdapterAuthority>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<FirmwareAdapterBackends>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<Capabilities>() &&
            sizeof(FirmwareAdapterMemoryMapInfo) == 32 && sizeof(FirmwareAdapterFramebufferInfo) == 32,
            "POD authority/capabilities/results");
        using var f = new Fixture(); f.Bind();
        Exercise(f, FirmwareAdapterStatus.Success);
        f.State->ReturnStatus = 0x8000000000000005;
        Exercise(f, FirmwareAdapterStatus.FirmwareError);
        Need(Core.Bind(ref f.State->Authority, &f.State->Arena, f.References, f.Backends) == FirmwareAdapterStatus.AlreadyBound,
            "bound descriptor cannot be rebound");
    }

    private static void Cutoffs()
    {
        foreach (uint phase in new uint[] { 0, 2, 3, uint.MaxValue })
        {
            using var f = new Fixture(); f.Bind(); f.State->Arena.StackEntry->Phase = phase;
            Exercise(f, FirmwareAdapterStatus.ServicesUnavailable);
        }
        foreach (FirstExitPhase phase in new[] { FirstExitPhase.InFlight, FirstExitPhase.Failed,
            FirstExitPhase.OwnedContinuation, FirstExitPhase.UnexpectedContinuationReturn })
        {
            using var f = new Fixture(); f.Bind();
            // Test raw retained coordinator state, not a synthetic mode passed to
            // the new adapter. These are the accepted state's first two fields.
            *(FirstExitPhase*)f.State->Arena.Transition = phase;
            Exercise(f, FirmwareAdapterStatus.ServicesUnavailable);
        }
        foreach (FirmwareServicesMode mode in new[] { FirmwareServicesMode.BootServicesExited, FirmwareServicesMode.UefiGoneSimulated })
        {
            using var f = new Fixture(); f.Bind();
            *(FirmwareServicesMode*)((byte*)f.State->Arena.Transition + 4) = mode;
            Exercise(f, FirmwareAdapterStatus.ServicesUnavailable);
        }
    }

    private static void EarlyFailure()
    {
        using var f = new Fixture(); f.Bind();
        f.Tables[127] ^= 1;
        Need(FirstExitFirmwareBinding.Consume(ref *f.State->Arena.Binding, FirmwareServicesMode.BootServicesAvailable,
            out _, out _, out _) == FirstExitBindingStatus.Changed && f.State->Arena.Transition->Phase == FirstExitPhase.Empty,
            "CRC failure consumes binding while transition is still empty");
        Exercise(f, FirmwareAdapterStatus.ServicesUnavailable);
        using var reentry = new Fixture(); reentry.Bind(); reentry.State->Reenter = 1;
        ulong value = 0;
        Need(reentry.State->Capabilities.Timer.Read(&value).Succeeded && value == 0x1234 &&
            reentry.State->ReenteredStatus == FirmwareAdapterStatus.ServicesUnavailable && reentry.State->Calls[4] == 0,
            "callback reentry observes original entry cutoff before poison buffer");
        Exercise(reentry, FirmwareAdapterStatus.ServicesUnavailable);
    }

    private static void Identity()
    {
        Need(default(FirmwareConsoleCapability).Write((char*)1, ulong.MaxValue).Status == FirmwareAdapterStatus.Unbound &&
            default(FirmwareTimerCapability).Read((ulong*)1).Status == FirmwareAdapterStatus.Unbound,
            "default capabilities refuse before pointer access");
        using var f = new Fixture();
        FirstExitImageReferences bad = f.References; bad.AdapterStateBytes = 8;
        Need(Core.Bind(ref f.State->Authority, &f.State->Arena, bad, f.Backends) == FirmwareAdapterStatus.InvalidAuthority,
            "authority outside admitted owner");
        Need(Core.Bind(ref *(FirmwareAdapterAuthority*)&f.State->Arena, &f.State->Arena, f.References, f.Backends)
            == FirmwareAdapterStatus.InvalidAuthority, "authority cannot alias owner");
        FirstExitOwnedArenaState copiedArena = f.State->Arena;
        Need(Core.Bind(ref f.State->Authority, &copiedArena, f.References, f.Backends) == FirmwareAdapterStatus.InvalidAuthority,
            "copied owner outside original admitted image extent");
        var changed = f.Backends; changed.Context = (void*)1;
        Need(Core.Bind(ref f.State->Authority, &f.State->Arena, f.References, changed) == FirmwareAdapterStatus.InvalidAuthority,
            "backend context must remain in admitted owner extent");
        changed = f.Backends; changed.Timer = (delegate* unmanaged<void*, ulong*, ulong>)f.Arena;
        Need(Core.Bind(ref f.State->Authority, &f.State->Arena, f.References, changed) == FirmwareAdapterStatus.InvalidAuthority,
            "arena data cannot be a callback");
        Need(new ReadOnlySpan<byte>(&f.State->Authority, sizeof(FirmwareAdapterAuthority)).IndexOfAnyExcept((byte)0) < 0,
            "failed bindings leave authority empty");
        f.Bind();
        f.State->OtherAuthority = f.State->Authority;
        Need(Core.Timer(ref f.State->OtherAuthority).Read((ulong*)1).Status == FirmwareAdapterStatus.InvalidAuthority,
            "copied authority self identity rejects before poison output");
        byte* original = f.State->Arena.Storage;
        *(byte**)&f.State->Arena = (byte*)1;
        Exercise(f, FirmwareAdapterStatus.InvalidAuthority);
        *(byte**)&f.State->Arena = original;
        Exercise(f, FirmwareAdapterStatus.Success);
    }

    private static void Ranges()
    {
        using var f = new Fixture(); f.Bind();
        byte[] authority = new ReadOnlySpan<byte>(f.State, sizeof(Storage)).ToArray();
        byte* buffer = stackalloc byte[64]; ulong value = 0;
        var timer = f.State->Capabilities.Timer;
        Need(timer.Read((ulong*)&timer).Status == FirmwareAdapterStatus.AliasedStorage &&
            timer.Read((ulong*)&f.State->Authority).Status == FirmwareAdapterStatus.AliasedStorage &&
            timer.Read((ulong*)f.Arena).Status == FirmwareAdapterStatus.AliasedStorage,
            "copied capability, original owner and arena output aliases reject");
        Need(f.State->Capabilities.Map.Capture(buffer, 64, (FirmwareAdapterMemoryMapInfo*)buffer).Status == FirmwareAdapterStatus.AliasedStorage &&
            f.State->Capabilities.Files.Read(9, 0, buffer, 64, (ulong*)buffer).Status == FirmwareAdapterStatus.AliasedStorage &&
            f.State->Capabilities.Memory.Read((ulong)buffer, buffer + 1, 8).Status == FirmwareAdapterStatus.AliasedStorage,
            "overlapping independent operation buffers reject");
        Need(timer.Read((ulong*)((byte*)&value + 1)).Status == FirmwareAdapterStatus.InvalidRange &&
            f.State->Capabilities.Random.Fill(buffer, ulong.MaxValue).Status == FirmwareAdapterStatus.InvalidRange &&
            f.State->Capabilities.Console.Write((char*)buffer, ulong.MaxValue).Status == FirmwareAdapterStatus.InvalidRange &&
            f.State->Capabilities.Files.Read(9, ulong.MaxValue, buffer, 8, &value).Status == FirmwareAdapterStatus.InvalidRange,
            "misalignment and overflow reject");
        Need(authority.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(Storage))),
            "invalid calls touch neither authority nor backend counters");
    }

    private static void Unavailable()
    {
        using var f = new Fixture(); f.Backends = default; f.Bind();
        Exercise(f, FirmwareAdapterStatus.BackendUnavailable);
        f.State->Arena.StackEntry->Phase = 2;
        Exercise(f, FirmwareAdapterStatus.ServicesUnavailable);
        Need(f.State->Capabilities.Framebuffer.Snapshot((FirmwareAdapterFramebufferInfo*)1).Status == FirmwareAdapterStatus.ServicesUnavailable,
            "denial precedes unavailable backend and invalid output checks");
    }
}
