using OS.Boot;

namespace OS.Kernel.Runtime;

public enum CompilerClassConstructorStatus : byte
{
    Uninitialized = 0, Success = 1, StorageRejected = 2, ModuleRejected = 3,
    EagerTableRejected = 4, TargetRejected = 5, ContextRejected = 6,
    CapacityExceeded = 7, AlreadyBound = 8, NotBound = 9, StateChanged = 10,
    EagerNotStarted = 11, ExecutionInProgress = 12, InvocationRejected = 13,
    InitializationFailed = 14
}

public struct CompilerClassConstructorProgress
{
    public uint EagerCount, EagerCompleted, LazyCount, LazyCompleted, ActiveLazyCount;
    public bool EagerFinished;
}

/// <summary>
/// Pinned ILC 10.0.12 win-x64 managed-void execution, under exclusive single-thread
/// access. The original registered image, owner and caller-owned runner storage
/// must remain mapped and stable for the entire lifetime, including callbacks.
/// Image-contained storage requires registered mutable-data admission. External
/// storage retains the caller's writable-mapping/lifetime obligation. Numeric
/// checks do not probe mappings or authenticate caller-forged/reset records.
/// EnsureLazy constructors must return normally or terminate. The split lazy
/// operations record a caller-reported normal return or failure; they neither
/// execute exceptions nor authenticate exception roots/caches. Contention and
/// thread-static initialization are not implemented here.
/// </summary>
public static unsafe partial class CompilerClassConstructorExecution
{
    public const uint MaximumEagerConstructors = 64;
    public const uint MaximumLazyConstructors = 64;
    public const ulong EagerTargetBytes = 16;
    public const ulong LazyRecordBytes = 24;
    private const ulong Bound = 0x4D30343032344331;
    private const uint None = 0xFFFFFFFF;
    private const uint Running = 1, Complete = 2, Failed = 3;

    private struct State
    {
        internal ulong Phase, Self, Capacity, ModuleOwner, ModuleCapacity, ModuleHandle;
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal M01LoadedImageVirtualMappingEvidence Mapping;
        internal ulong EagerTable;
        internal uint EagerCapacity, LazyCapacity, EagerCount, EagerCompleted;
        internal uint EagerPhase, LazyCount, ActiveLazy;
        internal bool EagerPresent;
    }

    // Eager identity is the table entry/order, not its callable address: ILC or
    // the linker may fold distinct constructor bodies to the same target.
    private struct EagerTarget { internal ulong Entry, Target; }
    private struct LazyRecord { internal ulong Context, Target; internal uint Phase, Parent; }

    public static ulong StateBytes => (ulong)sizeof(State);

    public static ulong RequiredStorage(uint eagerCapacity, uint lazyCapacity) =>
        eagerCapacity > MaximumEagerConstructors || lazyCapacity > MaximumLazyConstructors ||
        (eagerCapacity == 0 && lazyCapacity == 0) ? 0 :
        StateBytes + eagerCapacity * EagerTargetBytes + lazyCapacity * LazyRecordBytes;

    /// <summary>
    /// Called only after module/heap/static storage publication. Resolves section
    /// 205 through that same owner and validates every rel32 cell and target before
    /// publishing any binding. No constructor is called here. Storage is initially
    /// zero and distinct from owner, registered extents and compiler contexts.
    /// </summary>
    public static CompilerClassConstructorStatus Bind(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* moduleOwner, ulong moduleCapacity,
        ulong moduleHandle, byte* runnerStorage, ulong runnerCapacity,
        uint eagerCapacity, uint lazyCapacity)
    {
        ulong required = RequiredStorage(eagerCapacity, lazyCapacity);
        if (required == 0 || !StorageSpan(runnerStorage, runnerCapacity) || runnerCapacity < required)
            return CompilerClassConstructorStatus.StorageRejected;
        TypeManagerModuleResult table = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, moduleOwner, moduleCapacity, moduleHandle, 205);
        if (!table.Succeeded && table.Status != TypeManagerModuleStatus.SectionAbsent)
            return CompilerClassConstructorStatus.ModuleRejected;

        State candidate = default;
        candidate.Self = (ulong)runnerStorage; candidate.Capacity = runnerCapacity;
        candidate.ModuleOwner = (ulong)moduleOwner; candidate.ModuleCapacity = moduleCapacity;
        candidate.ModuleHandle = moduleHandle; candidate.Capture = capture; candidate.Mapping = mapping;
        candidate.EagerCapacity = eagerCapacity; candidate.LazyCapacity = lazyCapacity;
        candidate.ActiveLazy = None; candidate.EagerPresent = table.Succeeded;
        candidate.EagerTable = table.Section;
        if (table.Length < 0 || (table.Length & 3) != 0 ||
            (table.Length != 0 && (table.Section & 3) != 0))
            return CompilerClassConstructorStatus.EagerTableRejected;
        candidate.EagerCount = (uint)table.Length / 4;
        if (candidate.EagerCount > eagerCapacity) return CompilerClassConstructorStatus.CapacityExceeded;
        if (!StorageRange(&candidate)) return CompilerClassConstructorStatus.StorageRejected;
        if (table.Length != 0 && (!Range(&candidate, table.Section, (ulong)table.Length,
                TypeManagerRegisteredRangeKind.ReadableSectionData) ||
            Overlap(candidate.Self, runnerCapacity, table.Section, (ulong)table.Length)))
            return CompilerClassConstructorStatus.EagerTableRejected;
        for (uint i = 0; i < candidate.EagerCount; i++)
        {
            ulong entry = table.Section + i * 4UL;
            if (!Relative32(entry, out ulong target) || !Executable(&candidate, target))
                return CompilerClassConstructorStatus.TargetRejected;
        }
        for (ulong i = 0; i < required; i++)
            if (runnerStorage[i] != 0) return CompilerClassConstructorStatus.AlreadyBound;

        // No callbacks or further fallible admission after the first write.
        EagerTarget* eager = (EagerTarget*)(runnerStorage + sizeof(State));
        for (uint i = 0; i < candidate.EagerCount; i++)
        {
            ulong entry = table.Section + i * 4UL;
            Relative32(entry, out ulong target);
            eager[i].Entry = entry; eager[i].Target = target;
        }
        candidate.Phase = Bound;
        *(State*)runnerStorage = candidate;
        return CompilerClassConstructorStatus.Success;
    }

    /// <summary>Executes the validated eager snapshot once, in section order.</summary>
    public static CompilerClassConstructorStatus RunEager(byte* storage, ulong capacity)
    {
        CompilerClassConstructorStatus status = Validate(storage, capacity, out State* state);
        if (status != CompilerClassConstructorStatus.Success) return status;
        if (state->EagerPhase == Complete) return CompilerClassConstructorStatus.Success;
        if (state->EagerPhase == Running) return CompilerClassConstructorStatus.ExecutionInProgress;
        state->EagerPhase = Running;
        EagerTarget* targets = Eager(state);
        for (uint i = 0; i < state->EagerCount; i++)
        {
            status = Validate(storage, capacity, out state);
            if (status != CompilerClassConstructorStatus.Success) return status;
            ((delegate*<void>)targets[i].Target)();
            status = Validate(storage, capacity, out state);
            if (status != CompilerClassConstructorStatus.Success) return status;
            if (state->ActiveLazy != None) return CompilerClassConstructorStatus.StateChanged;
            state->EagerCompleted = i + 1;
        }
        state->EagerPhase = Complete;
        return CompilerClassConstructorStatus.Success;
    }

    /// <summary>
    /// Invokes an absolute managed entry from an aligned eight-byte compiler
    /// context. Reentry into the current single-thread active chain succeeds
    /// without invoking or clearing that context, preserving partial statics.
    /// Unknown zero contexts are rejected; only a recorded normal return grants
    /// completed state. The compiler's own zero-word fast path remains separate.
    /// </summary>
    public static CompilerClassConstructorStatus EnsureLazy(byte* storage, ulong capacity, ulong contextAddress)
    {
        CompilerClassConstructorStatus status = BeginLazy(storage, capacity, contextAddress, out var invocation);
        if (status != CompilerClassConstructorStatus.Success || !invocation.RequiresInvocation) return status;
        ((delegate*<void>)invocation.Target)();
        return FinishLazy(storage, capacity, invocation, returnedNormally: true);
    }

    public static CompilerClassConstructorStatus Observe(byte* storage, ulong capacity,
        out CompilerClassConstructorProgress progress)
    {
        progress = default;
        CompilerClassConstructorStatus status = Validate(storage, capacity, out State* state);
        if (status != CompilerClassConstructorStatus.Success) return status;
        progress.EagerCount = state->EagerCount; progress.EagerCompleted = state->EagerCompleted;
        progress.EagerFinished = state->EagerPhase == Complete; progress.LazyCount = state->LazyCount;
        LazyRecord* records = Lazy(state);
        for (uint i = 0; i < state->LazyCount; i++)
            if (records[i].Phase == Complete) progress.LazyCompleted++;
            else if (records[i].Phase == Running) progress.ActiveLazyCount++;
        return CompilerClassConstructorStatus.Success;
    }

    private static CompilerClassConstructorStatus Validate(byte* storage, ulong capacity, out State* state)
    {
        state = null;
        if (!StorageSpan(storage, capacity) || capacity < StateBytes)
            return CompilerClassConstructorStatus.StorageRejected;
        State* s = (State*)storage;
        if (s->Phase == 0) return CompilerClassConstructorStatus.NotBound;
        ulong required = RequiredStorage(s->EagerCapacity, s->LazyCapacity);
        if (s->Phase != Bound || s->Self != (ulong)storage || s->Capacity != capacity ||
            required == 0 || capacity < required || s->EagerCount > s->EagerCapacity ||
            s->EagerCompleted > s->EagerCount || s->LazyCount > s->LazyCapacity || s->EagerPhase > Complete ||
            (s->EagerPhase == 0 && (s->EagerCompleted != 0 || s->LazyCount != 0)) ||
            (s->EagerPhase == Complete && s->EagerCompleted != s->EagerCount))
            return CompilerClassConstructorStatus.StateChanged;
        TypeManagerModuleResult table = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            s->Capture, s->Mapping, (byte*)s->ModuleOwner, s->ModuleCapacity, s->ModuleHandle, 205);
        if (!table.Succeeded && table.Status != TypeManagerModuleStatus.SectionAbsent)
            return CompilerClassConstructorStatus.ModuleRejected;
        if (s->EagerPresent != table.Succeeded || s->EagerTable != table.Section ||
            table.Length != (int)(s->EagerCount * 4) || !StorageRange(s))
            return CompilerClassConstructorStatus.StateChanged;
        if (s->EagerCount != 0 && !Range(s, table.Section, (ulong)table.Length,
                TypeManagerRegisteredRangeKind.ReadableSectionData))
            return CompilerClassConstructorStatus.EagerTableRejected;
        EagerTarget* eager = Eager(s);
        for (uint i = 0; i < s->EagerCount; i++)
        {
            ulong entry = table.Section + i * 4UL;
            if (eager[i].Entry != entry || !Relative32(entry, out ulong target) || eager[i].Target != target)
                return CompilerClassConstructorStatus.StateChanged;
            if (!Executable(s, target)) return CompilerClassConstructorStatus.TargetRejected;
        }
        LazyRecord* records = Lazy(s);
        uint running = 0;
        for (uint i = 0; i < s->LazyCount; i++)
        {
            LazyRecord record = records[i];
            if (!Context(s, record.Context) || !Executable(s, record.Target))
                return CompilerClassConstructorStatus.StateChanged;
            for (uint j = 0; j < i; j++)
                if (records[j].Context == record.Context) return CompilerClassConstructorStatus.StateChanged;
            ulong word = *(ulong*)record.Context;
            if (record.Phase == Complete)
            {
                if (word != 0 || record.Parent != None) return CompilerClassConstructorStatus.StateChanged;
            }
            else if (record.Phase == Running)
            {
                if (word != record.Target) return CompilerClassConstructorStatus.StateChanged;
                running++;
            }
            else if (record.Phase == Failed)
            {
                if (word != record.Target || record.Parent != None) return CompilerClassConstructorStatus.StateChanged;
            }
            else return CompilerClassConstructorStatus.StateChanged;
        }
        uint current = s->ActiveLazy, depth = 0;
        while (current != None)
        {
            if (current >= s->LazyCount || depth++ >= running || records[current].Phase != Running)
                return CompilerClassConstructorStatus.StateChanged;
            current = records[current].Parent;
        }
        if (depth != running) return CompilerClassConstructorStatus.StateChanged;
        state = s;
        return CompilerClassConstructorStatus.Success;
    }

    private static bool StorageRange(State* s)
    {
        if (Overlap(s->Self, s->Capacity, s->ModuleOwner, s->ModuleCapacity) ||
            s->Mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success)
            return false;
        return !Overlap(s->Self, s->Capacity, image, s->Capture.Range.ImageSize) ||
            Range(s, s->Self, s->Capacity, TypeManagerRegisteredRangeKind.MutableData);
    }

    private static bool Context(State* s, ulong context) => (context & 7) == 0 &&
        !Overlap(context, 8, s->Self, s->Capacity) &&
        Range(s, context, 8, TypeManagerRegisteredRangeKind.MutableData);

    private static bool Executable(State* s, ulong target) => target != 0 &&
        !Overlap(target, 1, s->Self, s->Capacity) &&
        Range(s, target, 1, TypeManagerRegisteredRangeKind.ExecutableEntry);

    private static bool Range(State* s, ulong address, ulong bytes, TypeManagerRegisteredRangeKind kind) =>
        TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(s->Capture, s->Mapping,
            (byte*)s->ModuleOwner, s->ModuleCapacity, s->ModuleHandle, address, bytes, kind) == TypeManagerModuleStatus.Success;

    private static EagerTarget* Eager(State* s) => (EagerTarget*)((byte*)s + sizeof(State));
    private static LazyRecord* Lazy(State* s) => (LazyRecord*)((byte*)s + sizeof(State) + s->EagerCapacity * EagerTargetBytes);
    private static bool StorageSpan(byte* storage, ulong bytes) => storage != null &&
        ((ulong)storage & 7) == 0 && bytes != 0 && (ulong)storage <= ulong.MaxValue - bytes;
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) =>
        an != 0 && bn != 0 && (a <= b ? b - a < an : a - b < bn);
    private static bool Relative32(ulong entry, out ulong target)
    {
        long displacement = *(int*)entry;
        if (displacement >= 0)
        {
            ulong offset = (ulong)displacement;
            target = entry <= ulong.MaxValue - offset ? entry + offset : 0;
            return target != 0;
        }
        ulong distance = (ulong)-displacement;
        target = entry >= distance ? entry - distance : 0;
        return target != 0;
    }
}
