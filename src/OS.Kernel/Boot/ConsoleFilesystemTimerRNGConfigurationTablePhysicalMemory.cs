using OS.Kernel.Platform;

namespace OS.Boot;

public enum FirmwareAdapterStatus : uint
{
    Unbound, Success, AlreadyBound, InvalidAuthority, ServicesUnavailable,
    BackendUnavailable, InvalidRange, AliasedStorage, FirmwareError
}

public readonly struct FirmwareAdapterCallResult
{
    internal FirmwareAdapterCallResult(FirmwareAdapterStatus status, ulong firmwareStatus = 0)
    { Status = status; FirmwareStatus = firmwareStatus; }

    public FirmwareAdapterStatus Status { get; }
    public ulong FirmwareStatus { get; }
    public bool Succeeded => Status == FirmwareAdapterStatus.Success;
}

public struct FirmwareAdapterMemoryMapInfo
{
    public ulong Bytes, MapKey, DescriptorBytes;
    public uint DescriptorVersion, Reserved;
}

public struct FirmwareAdapterFramebufferInfo
{
    public ulong BaseAddress, ByteLength;
    public uint Width, Height, StridePixels, PixelFormat;
}

// Typed adapter thunks, not a category-switch callback. The integration must
// authenticate their original code/protocol provenance and retain Context in its
// admitted image state. Optional null thunks represent unavailable services.
// Thunks must obey the supplied lengths, not mutate authority, throw or retain
// caller buffers, and return their actual firmware status. They own protocol
// semantics (including partial writes/error outputs), not the revocation guard.
public unsafe struct FirmwareAdapterBackends
{
    public void* Context;
    public ulong ContextBytes;
    public delegate* unmanaged<void*, char*, ulong, ulong> Console;
    public delegate* unmanaged<void*, byte*, ulong, FirmwareAdapterMemoryMapInfo*, ulong> MemoryMap;
    public delegate* unmanaged<void*, ulong, ulong, byte*, ulong, ulong*, ulong> FileSystem;
    public delegate* unmanaged<void*, ulong*, ulong> Timer;
    public delegate* unmanaged<void*, byte*, ulong, ulong> Random;
    public delegate* unmanaged<void*, ulong, ulong, ulong*, ulong> ConfigurationTable;
    public delegate* unmanaged<void*, ulong, byte*, ulong, ulong> PhysicalMemory;
    public delegate* unmanaged<void*, FirmwareAdapterFramebufferInfo*, ulong> Framebuffer;
}

// Retain this original zero-initialized record in the image's adapter state.
// Self detects accidental record copies; OriginalArena/Storage bind the accepted
// owner. These are immutable admission facts, not another revocation Boolean.
public unsafe struct FirmwareAdapterAuthority
{
    internal FirmwareAdapterAuthority* Self;
    internal FirstExitOwnedArenaState* OriginalArena;
    internal byte* OriginalStorage;
    internal ulong AdapterState, AdapterStateBytes;
    internal FirmwareAdapterBackends Backends;
}

/// <summary>
/// Single-thread boot-service capabilities sharing the original first-exit
/// authority. Calls are admitted only before owned-stack entry. Entry phase2,
/// binding consumption, transition failure/in-flight/success all deny without
/// reading a backend or touching any supplied argument/output.
///
/// The caller authenticates the original arena identity and retained RW image
/// extent before Bind (the existing first-exit image/mapping admission), and
/// keeps that memory valid, stable and exclusive. Pointer/extent checks do not
/// prove arbitrary pointer readability, page permissions or malicious-writer
/// resistance. Never reset/rebind/free the authority or original arena. Copy
/// capabilities, not either owner; retain all long-lived copies in AdapterState.
/// The final-map/exit coordinator uses its separate consumed binding and is not
/// an ordinary adapter call. No boot transition or firmware allocation occurs
/// in this core.
/// </summary>
public static unsafe class ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory
{
    private const ulong LowEnd = 0x0000800000000000;

    public static FirmwareAdapterStatus Bind(ref FirmwareAdapterAuthority authority,
        FirstExitOwnedArenaState* originalArena, FirstExitImageReferences retainedReferences,
        FirmwareAdapterBackends backends)
    {
        fixed (FirmwareAdapterAuthority* address = &authority)
        {
            ulong owner = retainedReferences.AdapterState, bytes = retainedReferences.AdapterStateBytes;
            if (!Extent(owner, bytes) || !Contains(owner, bytes, (ulong)address, (ulong)sizeof(FirmwareAdapterAuthority)) ||
                !Contains(owner, bytes, (ulong)originalArena, (ulong)sizeof(FirstExitOwnedArenaState)) ||
                ((ulong)address & 7) != 0 || ((ulong)originalArena & 7) != 0 ||
                Overlap((ulong)address, (ulong)sizeof(FirmwareAdapterAuthority),
                    (ulong)originalArena, (ulong)sizeof(FirstExitOwnedArenaState)))
                return FirmwareAdapterStatus.InvalidAuthority;
            // A partially initialized/copy record is not reusable empty storage.
            for (uint i = 0; i < sizeof(FirmwareAdapterAuthority); i++)
                if (((byte*)address)[i] != 0) return FirmwareAdapterStatus.AlreadyBound;
            if (!originalArena->IsReady || originalArena->Reserved != 0 ||
                !Extent((ulong)originalArena->Storage, FirstExitOwnedArena.Bytes) ||
                ((ulong)originalArena->Storage & 4095) != 0 ||
                Overlap(owner, bytes, (ulong)originalArena->Storage, FirstExitOwnedArena.Bytes))
                return FirmwareAdapterStatus.InvalidAuthority;
            if (!BeforeEntry(originalArena)) return FirmwareAdapterStatus.ServicesUnavailable;
            bool any = backends.Console != null || backends.MemoryMap != null || backends.FileSystem != null ||
                backends.Timer != null || backends.Random != null || backends.ConfigurationTable != null ||
                backends.PhysicalMemory != null || backends.Framebuffer != null;
            if (backends.Context == null ? backends.ContextBytes != 0 || any :
                !Contains(owner, bytes, (ulong)backends.Context, backends.ContextBytes))
                return FirmwareAdapterStatus.InvalidAuthority;
            if (!Callback((ulong)backends.Console, owner, bytes, originalArena->Storage) ||
                !Callback((ulong)backends.MemoryMap, owner, bytes, originalArena->Storage) ||
                !Callback((ulong)backends.FileSystem, owner, bytes, originalArena->Storage) ||
                !Callback((ulong)backends.Timer, owner, bytes, originalArena->Storage) ||
                !Callback((ulong)backends.Random, owner, bytes, originalArena->Storage) ||
                !Callback((ulong)backends.ConfigurationTable, owner, bytes, originalArena->Storage) ||
                !Callback((ulong)backends.PhysicalMemory, owner, bytes, originalArena->Storage) ||
                !Callback((ulong)backends.Framebuffer, owner, bytes, originalArena->Storage))
                return FirmwareAdapterStatus.InvalidAuthority;
            authority.OriginalArena = originalArena;
            authority.OriginalStorage = originalArena->Storage;
            authority.AdapterState = owner;
            authority.AdapterStateBytes = bytes;
            authority.Backends = backends;
            authority.Self = address;
            return FirmwareAdapterStatus.Success;
        }
    }

    public static FirmwareConsoleCapability Console(ref FirmwareAdapterAuthority authority)
    { fixed (FirmwareAdapterAuthority* address = &authority) return new(address); }
    public static FirmwareMemoryMapCapability MemoryMap(ref FirmwareAdapterAuthority authority)
    { fixed (FirmwareAdapterAuthority* address = &authority) return new(address); }
    public static FirmwareFileSystemCapability FileSystem(ref FirmwareAdapterAuthority authority)
    { fixed (FirmwareAdapterAuthority* address = &authority) return new(address); }
    public static FirmwareTimerCapability Timer(ref FirmwareAdapterAuthority authority)
    { fixed (FirmwareAdapterAuthority* address = &authority) return new(address); }
    public static FirmwareRandomCapability Random(ref FirmwareAdapterAuthority authority)
    { fixed (FirmwareAdapterAuthority* address = &authority) return new(address); }
    public static FirmwareConfigurationTableCapability ConfigurationTable(ref FirmwareAdapterAuthority authority)
    { fixed (FirmwareAdapterAuthority* address = &authority) return new(address); }
    public static FirmwarePhysicalMemoryCapability PhysicalMemory(ref FirmwareAdapterAuthority authority)
    { fixed (FirmwareAdapterAuthority* address = &authority) return new(address); }
    public static FirmwareFramebufferCapability Framebuffer(ref FirmwareAdapterAuthority authority)
    { fixed (FirmwareAdapterAuthority* address = &authority) return new(address); }

    internal static FirmwareAdapterStatus Admit(FirmwareAdapterAuthority* authority)
    {
        if (authority == null) return FirmwareAdapterStatus.Unbound;
        if (!Extent((ulong)authority, (ulong)sizeof(FirmwareAdapterAuthority)) || ((ulong)authority & 7) != 0)
            return FirmwareAdapterStatus.InvalidAuthority;
        if (authority->Self != authority ||
            !Contains(authority->AdapterState, authority->AdapterStateBytes, (ulong)authority, (ulong)sizeof(FirmwareAdapterAuthority)) ||
            !Contains(authority->AdapterState, authority->AdapterStateBytes,
                (ulong)authority->OriginalArena, (ulong)sizeof(FirstExitOwnedArenaState)) ||
            ((ulong)authority->OriginalArena & 7) != 0 ||
            !Extent((ulong)authority->OriginalStorage, FirstExitOwnedArena.Bytes) ||
            ((ulong)authority->OriginalStorage & 4095) != 0 ||
            Overlap(authority->AdapterState, authority->AdapterStateBytes,
                (ulong)authority->OriginalStorage, FirstExitOwnedArena.Bytes))
            return FirmwareAdapterStatus.InvalidAuthority;
        FirstExitOwnedArenaState* arena = authority->OriginalArena;
        // Check the retained snapshot before following changed arena pointers.
        if (!arena->IsReady || arena->Reserved != 0 || arena->Storage != authority->OriginalStorage)
            return FirmwareAdapterStatus.InvalidAuthority;
        return BeforeEntry(arena) ? FirmwareAdapterStatus.Success : FirmwareAdapterStatus.ServicesUnavailable;
    }

    private static bool BeforeEntry(FirstExitOwnedArenaState* arena)
        => arena->StackEntry->Phase == 1 && arena->StackEntry->Reserved == 0 && arena->Binding->IsReady &&
            arena->Transition->Phase == FirstExitPhase.Empty &&
            arena->Transition->Mode == FirmwareServicesMode.BootServicesAvailable;

    internal static FirmwareAdapterStatus Buffer(FirmwareAdapterAuthority* authority,
        ulong capability, ulong capabilityBytes, ulong buffer, ulong bytes, ulong alignment = 1)
    {
        if (!Extent(buffer, bytes) || (buffer & (alignment - 1)) != 0) return FirmwareAdapterStatus.InvalidRange;
        if (Overlap(buffer, bytes, authority->AdapterState, authority->AdapterStateBytes) ||
            Overlap(buffer, bytes, (ulong)authority->OriginalStorage, FirstExitOwnedArena.Bytes) ||
            Overlap(buffer, bytes, capability, capabilityBytes)) return FirmwareAdapterStatus.AliasedStorage;
        return FirmwareAdapterStatus.Success;
    }

    internal static FirmwareAdapterCallResult Returned(ulong status)
        => new(status == 0 ? FirmwareAdapterStatus.Success : FirmwareAdapterStatus.FirmwareError, status);

    internal static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a < b + bn && b < a + an;
    private static bool Extent(ulong start, ulong bytes)
        => start != 0 && start < LowEnd && bytes != 0 && bytes <= LowEnd - start;
    private static bool Contains(ulong start, ulong bytes, ulong item, ulong length)
        => Extent(start, bytes) && Extent(item, length) && item >= start && length <= bytes && item - start <= bytes - length;
    private static bool Callback(ulong callback, ulong owner, ulong bytes, byte* storage)
        => callback == 0 || Extent(callback, 1) && !Overlap(callback, 1, owner, bytes) &&
            !Overlap(callback, 1, (ulong)storage, FirstExitOwnedArena.Bytes);
}

public unsafe struct FirmwareConsoleCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareConsoleCapability(FirmwareAdapterAuthority* authority) => _authority = authority;

    public FirmwareAdapterCallResult Write(char* text, ulong characters)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success) return new(status);
        var callback = _authority->Backends.Console;
        if (callback == null) return new(FirmwareAdapterStatus.BackendUnavailable);
        if (characters == 0 || characters > 0x0000400000000000UL) return new(FirmwareAdapterStatus.InvalidRange);
        fixed (FirmwareConsoleCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareConsoleCapability), (ulong)text, characters * 2, 2);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, text, characters));
        }
    }
}

public unsafe struct FirmwareMemoryMapCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareMemoryMapCapability(FirmwareAdapterAuthority* authority) => _authority = authority;

    public FirmwareAdapterCallResult Capture(byte* destination, ulong capacity, FirmwareAdapterMemoryMapInfo* info)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success) return new(status);
        var callback = _authority->Backends.MemoryMap;
        if (callback == null) return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareMemoryMapCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareMemoryMapCapability), (ulong)destination, capacity, 1);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareMemoryMapCapability), (ulong)info, (ulong)sizeof(FirmwareAdapterMemoryMapInfo), 8);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            if (ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Overlap((ulong)destination, capacity, (ulong)info, (ulong)sizeof(FirmwareAdapterMemoryMapInfo)))
                return new(FirmwareAdapterStatus.AliasedStorage);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, destination, capacity, info));
        }
    }
}

public unsafe struct FirmwareFileSystemCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareFileSystemCapability(FirmwareAdapterAuthority* authority) => _authority = authority;

    public FirmwareAdapterCallResult Read(ulong handle, ulong offset, byte* destination, ulong capacity, ulong* actualBytes)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success) return new(status);
        var callback = _authority->Backends.FileSystem;
        if (callback == null) return new(FirmwareAdapterStatus.BackendUnavailable);
        if (handle == 0 || capacity > ulong.MaxValue - offset) return new(FirmwareAdapterStatus.InvalidRange);
        fixed (FirmwareFileSystemCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareFileSystemCapability), (ulong)destination, capacity, 1);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareFileSystemCapability), (ulong)actualBytes, 8, 8);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            if (ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Overlap((ulong)destination, capacity, (ulong)actualBytes, 8))
                return new(FirmwareAdapterStatus.AliasedStorage);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, handle, offset, destination, capacity, actualBytes));
        }
    }
}

public unsafe struct FirmwareTimerCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareTimerCapability(FirmwareAdapterAuthority* authority) => _authority = authority;

    public FirmwareAdapterCallResult Read(ulong* value)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success) return new(status);
        var callback = _authority->Backends.Timer;
        if (callback == null) return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareTimerCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareTimerCapability), (ulong)value, 8, 8);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, value));
        }
    }
}

public unsafe struct FirmwareRandomCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareRandomCapability(FirmwareAdapterAuthority* authority) => _authority = authority;

    public FirmwareAdapterCallResult Fill(byte* destination, ulong bytes)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success) return new(status);
        var callback = _authority->Backends.Random;
        if (callback == null) return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareRandomCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareRandomCapability), (ulong)destination, bytes, 1);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, destination, bytes));
        }
    }
}

public unsafe struct FirmwareConfigurationTableCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareConfigurationTableCapability(FirmwareAdapterAuthority* authority) => _authority = authority;

    public FirmwareAdapterCallResult Find(ulong guidLow, ulong guidHigh, ulong* tableAddress)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success) return new(status);
        var callback = _authority->Backends.ConfigurationTable;
        if (callback == null) return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareConfigurationTableCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareConfigurationTableCapability), (ulong)tableAddress, 8, 8);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, guidLow, guidHigh, tableAddress));
        }
    }
}

public unsafe struct FirmwarePhysicalMemoryCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwarePhysicalMemoryCapability(FirmwareAdapterAuthority* authority) => _authority = authority;

    public FirmwareAdapterCallResult Read(ulong address, byte* destination, ulong bytes)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success) return new(status);
        var callback = _authority->Backends.PhysicalMemory;
        if (callback == null) return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwarePhysicalMemoryCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwarePhysicalMemoryCapability), (ulong)destination, bytes, 1);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwarePhysicalMemoryCapability), (ulong)address, bytes, 1);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            if (ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Overlap((ulong)address, bytes, (ulong)destination, bytes))
                return new(FirmwareAdapterStatus.AliasedStorage);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, address, destination, bytes));
        }
    }
}

public unsafe struct FirmwareFramebufferCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareFramebufferCapability(FirmwareAdapterAuthority* authority) => _authority = authority;

    public FirmwareAdapterCallResult Snapshot(FirmwareAdapterFramebufferInfo* info)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success) return new(status);
        var callback = _authority->Backends.Framebuffer;
        if (callback == null) return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareFramebufferCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(
                _authority, (ulong)self, (ulong)sizeof(FirmwareFramebufferCapability), (ulong)info, (ulong)sizeof(FirmwareAdapterFramebufferInfo), 8);
            if (status != FirmwareAdapterStatus.Success) return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, info));
        }
    }
}
