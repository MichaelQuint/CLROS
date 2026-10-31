#if NOSTDLIB_EFI && M04_INTERFACE_DISPATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// The generic arguments are metadata-only reference hierarchies. Their variance
// affects dispatch identity while the actual calling convention stays scalar.
internal class DispatchArgumentBase { }
internal class DispatchArgumentDerived : DispatchArgumentBase { }
internal interface IDispatchPlain { ulong Sum(ulong a, ulong b, ulong c, ulong d); }
internal interface IDispatchVirtual { ulong Virtual(ulong a, ulong b, ulong c, ulong d); }
internal interface IDispatchNoise { ulong Noise(ulong a, ulong b, ulong c, ulong d); }
internal interface IDispatchProducer<out T> { ulong Produce(ulong a, ulong b, ulong c, ulong d); }
internal interface IDispatchConsumer<in T> { ulong Consume(ulong a, ulong b, ulong c, ulong d); }

// Base owns the nonvirtual interface implementation. The two derived receiver
// layouts retain one scalar field and deliberately order other interfaces
// differently. Calling Noise ensures that interface survives compiler trimming.
internal class DispatchOwnerBase : IDispatchPlain, IDispatchVirtual
{
    protected readonly ulong seed;
    internal DispatchOwnerBase(ulong value) { RhpNewFastAllocation.DispatchConstructed(value); seed = value; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Sum(ulong a, ulong b, ulong c, ulong d) => seed + 100 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual ulong Virtual(ulong a, ulong b, ulong c, ulong d) => seed + a + b + c + d;
}
internal class DispatchReceiverA : DispatchOwnerBase, IDispatchNoise,
    IDispatchProducer<DispatchArgumentDerived>, IDispatchConsumer<DispatchArgumentBase>
{
    internal DispatchReceiverA(ulong value) : base(value) { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override ulong Virtual(ulong a, ulong b, ulong c, ulong d) => seed + 200 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Produce(ulong a, ulong b, ulong c, ulong d) => seed + 400 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Consume(ulong a, ulong b, ulong c, ulong d) => seed + 600 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Noise(ulong a, ulong b, ulong c, ulong d) => seed + 800 + a + 3 * b + 5 * c + 7 * d;
}
internal class DispatchReceiverB : DispatchOwnerBase, IDispatchConsumer<DispatchArgumentDerived>,
    IDispatchProducer<DispatchArgumentBase>, IDispatchNoise
{
    internal DispatchReceiverB(ulong value) : base(value) { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override ulong Virtual(ulong a, ulong b, ulong c, ulong d) => seed + 300 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Produce(ulong a, ulong b, ulong c, ulong d) => seed + 500 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Consume(ulong a, ulong b, ulong c, ulong d) => seed + 700 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Noise(ulong a, ulong b, ulong c, ulong d) => seed + 900 + a + 3 * b + 5 * c + 7 * d;
}

internal static unsafe partial class RhpNewFastAllocation
{
    // The pinned .NET 11 ABI contributes five zero {MethodTable,Code} cells;
    // Bind requires the exact section-210 extent and section-209 metadata.
    // The final original COFF must independently confirm these extents.
    // A and B own ordinary virtual slots and have no sealed-table field.
    private const ulong DispatchCellContributionBytes = 80;
    private const ulong DispatchBaseSealedBytes = 4;
    private struct DispatchStorage { internal fixed ulong Words[4096]; }
    private struct DispatchCells { internal fixed ulong Values[5]; }
    private struct DispatchReceipts { internal RhpNewFastResult A, B; }
    private static DispatchStorage s_dispatchStorage;
    private static DispatchCells s_dispatchCells;
    private static DispatchReceipts s_dispatchReceipts;
    private static ulong s_dispatchPhase, s_dispatchSeed, s_dispatchAllocations, s_dispatchConstructors;
    private static ulong s_dispatchCalls, s_dispatchExpected, s_dispatchStackChecks, s_dispatchMinimumWitness;
    private static ulong s_dispatchOriginalRoot, s_dispatchOriginalNode, s_dispatchOriginalTag;

    [System.Runtime.RuntimeImport("*", "CLROS_InterfaceDispatch_GetCellSection")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern ulong DispatchCellSection();

    [System.Runtime.RuntimeImport("*", "CLROS_InterfaceDispatch_GetInitialStub")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern ulong DispatchInitialStub();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunCompilerInterfaceDispatch()
    {
        if (s_dispatchPhase != 0 || s_gcPhase != 5 || s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            s_control.ConstructorCount != 7 || s_collectorStores != 7 || s_cctorPhase != 1 ||
            !ReadGcRoot().Succeeded || !CollectorReceiptValid(5) ||
            Bits(GcStaticFixture.Get()) != NodeReceipt(5).ObjectReference ||
            ReadStaticCollectorTag(false) != CollectorCookie + 6)
            RejectDispatch(1, CompilerInterfaceDispatchStatus.NotBound);
        s_dispatchPhase = 1;
        s_dispatchSeed = ((s_gcHandle ^ (ulong)s_access.Backing ^ s_gcStack.InitialRsp) & 0xFFFF) + 1;
        s_dispatchOriginalRoot = ReadGcRoot().ObjectReference;
        s_dispatchOriginalNode = NodeReceipt(5).ObjectReference;
        s_dispatchOriginalTag = ReadStaticCollectorTag(false);
        DispatchText(0);
        if (!CheckDispatchStack()) RejectDispatch(3, CompilerInterfaceDispatchStatus.StateChanged);
        ulong cellSection = DispatchCellSection(), initialStub = DispatchInitialStub();
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        fixed (DispatchStorage* storage = &s_dispatchStorage)
        {
            // Only entry zero is an admission. The other two inventory rows
            // explicitly describe owners without a sealed table.
            CompilerInterfaceDispatchSealedTable* owners = stackalloc CompilerInterfaceDispatchSealedTable[3];
            owners[0] = DispatchOwnerRecord(DispatchBaseType(), true);
            owners[1] = DispatchOwnerRecord(DispatchAType(), false);
            owners[2] = DispatchOwnerRecord(DispatchBType(), false);
            if (CompilerInterfaceDispatch.RequiredStorage(64, 64) > (ulong)sizeof(DispatchStorage))
                RejectDispatch(4, CompilerInterfaceDispatchStatus.StorageRejected);
            CompilerInterfaceDispatchStatus bound = CompilerInterfaceDispatch.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, s_access, &collecting->State,
                (ulong)sizeof(GcCollectingStorage), (byte*)storage, (ulong)sizeof(DispatchStorage),
                initialStub, cellSection, DispatchCellContributionBytes, 64, 64, owners, 1);
            if (bound != CompilerInterfaceDispatchStatus.Success) RejectDispatch(5, bound);
            DispatchReceiverA a = NewDispatchA(s_dispatchSeed + 11);
            DispatchReceiverB b = NewDispatchB(s_dispatchSeed + 22);
            if (s_dispatchAllocations != 2 || s_dispatchConstructors != 2 ||
                Bits(a) != s_dispatchReceipts.A.ObjectReference || Bits(b) != s_dispatchReceipts.B.ObjectReference ||
                collecting->State.AllocationCount != 10 || collecting->State.UsedBytes != 176 ||
                s_control.ConstructorCount != 7)
                RejectDispatch(6, CompilerInterfaceDispatchStatus.ReceiverRejected);
            CompilerInterfaceDispatchStatus observed = CompilerInterfaceDispatch.Observe((byte*)storage,
                (ulong)sizeof(DispatchStorage), out CompilerInterfaceDispatchProgress before);
            if (observed != CompilerInterfaceDispatchStatus.Success || before.CellCount != 5 ||
                before.CacheCount != 0 || before.Resolutions != 0 || before.CacheHits != 0)
                RejectDispatch(7, observed);
            DispatchLine(0, s_gcHandle, (ulong)storage, (ulong)sizeof(DispatchStorage), s_dispatchSeed);
            DispatchLine(1, cellSection, DispatchCellContributionBytes, initialStub, before.CellCount);
            DispatchLine(2, Bits(a), Bits(b), DispatchAType(), DispatchBType());
            DispatchLine(3, s_dispatchReceipts.A.AllocationCount, s_dispatchReceipts.B.AllocationCount,
                s_dispatchReceipts.A.BytesConsumed, s_dispatchReceipts.B.BytesConsumed);
            DispatchLine(4, s_dispatchOriginalRoot, s_dispatchOriginalNode, s_dispatchOriginalTag, (ulong)type);
            DispatchLine(5, s_gcStack.Start, s_gcStack.End, s_gcStack.InitialRsp, s_dispatchMinimumWitness);
            for (uint i = 0; i < 3; i++)
                DispatchLine(6 + i, owners[i].DeclaringType, owners[i].Table, owners[i].Bytes,
                    *(ushort*)(owners[i].DeclaringType + 16));
            DispatchLine(9, s_control.ConstructorCount, s_dispatchConstructors, collecting->State.AllocationCount, collecting->State.UsedBytes);
            s_dispatchPhase = 2;
            RunDispatchCalls(a, b);
            observed = CompilerInterfaceDispatch.Observe((byte*)storage, (ulong)sizeof(DispatchStorage), out CompilerInterfaceDispatchProgress final);
            if (observed != CompilerInterfaceDispatchStatus.Success || final.Resolutions != 15 || final.CacheCount != 10 ||
                final.CacheHits != 5 || s_dispatchCalls != 15 || s_dispatchExpected != 15 ||
                s_dispatchAllocations != 2 || s_dispatchConstructors != 2 ||
                !DispatchBaselineIntact() || !CheckDispatchStack() || s_dispatchStackChecks != 17)
                RejectDispatch(8, observed);
            DispatchLine(70, final.Resolutions, final.CacheCount, final.CacheHits, final.MetadataWords);
            DispatchLine(71, s_control.ConstructorCount, s_collectorStores, ReadGcRoot().ObjectReference, ReadStaticCollectorTag(false));
            DispatchLine(72, s_dispatchStackChecks, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8), s_dispatchMinimumWitness);
            s_dispatchPhase = 3;
            DispatchText(1); DispatchText(2);
        }
    }

    // Ordinary Win64 callback called only by the generated R11 bridge. A failed
    // core result returns zero: the bridge's local fail-stop cannot tail-jump it.
    // Core admission precedes expected-fixture comparisons, including null input.
    [System.Runtime.RuntimeExport("CLROS_InterfaceDispatch_Resolve")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong DispatchCallback(ulong receiver, ulong cell)
    {
        if (s_dispatchPhase != 2 || s_dispatchCalls >= 15 || s_dispatchCalls != s_dispatchExpected ||
            !CheckDispatchStack() || !DispatchBaselineIntact())
            return DispatchFailure(20, CompilerInterfaceDispatchStatus.StateChanged);
        fixed (DispatchStorage* storage = &s_dispatchStorage)
        {
            CompilerInterfaceDispatchStatus status = BootSequence.RunInterfaceDispatch((byte*)storage,
                (ulong)sizeof(DispatchStorage), receiver, cell, out CompilerInterfaceDispatchResolution resolved);
            if (status != CompilerInterfaceDispatchStatus.Success) return DispatchFailure(21, status);
            ulong index = s_dispatchExpected, operation = index / 3;
            bool second = index % 3 == 1;
            RhpNewFastResult receipt = second ? s_dispatchReceipts.B : s_dispatchReceipts.A;
            if (receiver != receipt.ObjectReference || resolved.ReceiverType != receipt.MethodTableToken ||
                resolved.AllocationSequence != receipt.AllocationCount || resolved.RequestedInterface != DispatchRequested(operation) ||
                resolved.ImplementedInterface != DispatchImplemented(operation, second) || resolved.CacheHit != (index % 3 == 2))
                return DispatchFailure(22, CompilerInterfaceDispatchStatus.StateChanged);
            ulong expectedOwner = operation <= 1 ? DispatchBaseType() : receipt.MethodTableToken;
            if (resolved.DeclaringType != expectedOwner)
                return DispatchFailure(23, CompilerInterfaceDispatchStatus.StateChanged);
            ushort ownerSlots = *(ushort*)(resolved.DeclaringType + 16);
            ulong expectedInterfaceSlot = operation == 2 || operation == 3 ? 1UL : 0UL;
            ulong expectedImplementationSlot = operation == 0 ? 1UL : operation - 1;
            if (resolved.InterfaceSlot != expectedInterfaceSlot || resolved.ImplementationSlot != expectedImplementationSlot)
                return DispatchFailure(24, CompilerInterfaceDispatchStatus.SlotRejected);
            if (operation != 0)
            {
                if (resolved.ImplementationSlot >= ownerSlots ||
                    resolved.Target != *(ulong*)(receipt.MethodTableToken + 24 + resolved.ImplementationSlot * 8UL))
                    return DispatchFailure(24, CompilerInterfaceDispatchStatus.SlotRejected);
            }
            else if (resolved.ImplementationSlot < ownerSlots)
                return DispatchFailure(25, CompilerInterfaceDispatchStatus.SlotRejected);
            fixed (DispatchCells* cells = &s_dispatchCells)
            {
                if (index % 3 == 0) cells->Values[operation] = cell;
                else if (cells->Values[operation] != cell) return DispatchFailure(26, CompilerInterfaceDispatchStatus.CellChanged);
            }
            s_dispatchCalls++;
            DispatchLine(10 + index * 4, receiver, cell, resolved.Target, resolved.AllocationSequence);
            DispatchLine(11 + index * 4, resolved.ReceiverType, resolved.RequestedInterface, resolved.ImplementedInterface, resolved.DeclaringType);
            DispatchLine(12 + index * 4, resolved.InterfaceSlot, resolved.ImplementationSlot, resolved.CacheHit ? 1UL : 0UL, s_dispatchCalls);
            return resolved.Target;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunDispatchCalls(DispatchReceiverA first, DispatchReceiverB second)
    {
        for (ulong index = 0; index < 15; index++)
        {
            s_dispatchExpected = index;
            ulong operation = index / 3, a = s_dispatchSeed + index + 1, b = a + 1, c = a + 2, d = a + 3;
            bool useSecond = index % 3 == 1;
            ulong actual;
            if (operation == 0) actual = useSecond ? DispatchPlain(second, a, b, c, d) : DispatchPlain(first, a, b, c, d);
            else if (operation == 1) actual = useSecond ? DispatchVirtual(second, a, b, c, d) : DispatchVirtual(first, a, b, c, d);
            else if (operation == 2) actual = useSecond ? DispatchProducer(second, a, b, c, d) : DispatchProducer(first, a, b, c, d);
            else if (operation == 3) actual = useSecond ? DispatchConsumer(second, a, b, c, d) : DispatchConsumer(first, a, b, c, d);
            else actual = useSecond ? DispatchNoise(second, a, b, c, d) : DispatchNoise(first, a, b, c, d);
            ulong offset = operation == 0 ? 100UL : operation * 200 + (useSecond ? 100UL : 0UL);
            ulong expected = s_dispatchSeed + (useSecond ? 22UL : 11UL) + offset + a + 3 * b + 5 * c + 7 * d;
            if (s_dispatchCalls != index + 1 || actual != expected) RejectDispatch(30, CompilerInterfaceDispatchStatus.StateChanged);
            DispatchLine(13 + index * 4, actual, expected, operation, d);
        }
        s_dispatchExpected = 15;
    }

    // Each no-inline opaque consumer has one real interface callsite. Its fourth
    // explicit integer parameter is passed on the Win64 stack after `this`.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong DispatchPlain(IDispatchPlain value, ulong a, ulong b, ulong c, ulong d) => value.Sum(a, b, c, d);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong DispatchVirtual(IDispatchVirtual value, ulong a, ulong b, ulong c, ulong d) => value.Virtual(a, b, c, d);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong DispatchProducer(IDispatchProducer<DispatchArgumentBase> value, ulong a, ulong b, ulong c, ulong d) => value.Produce(a, b, c, d);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong DispatchConsumer(IDispatchConsumer<DispatchArgumentDerived> value, ulong a, ulong b, ulong c, ulong d) => value.Consume(a, b, c, d);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong DispatchNoise(IDispatchNoise value, ulong a, ulong b, ulong c, ulong d) => value.Noise(a, b, c, d);
    [MethodImpl(MethodImplOptions.NoInlining)] private static DispatchReceiverA NewDispatchA(ulong seed) => new DispatchReceiverA(seed);
    [MethodImpl(MethodImplOptions.NoInlining)] private static DispatchReceiverB NewDispatchB(ulong seed) => new DispatchReceiverB(seed);
    [MethodImpl(MethodImplOptions.NoInlining)] private static ulong DispatchBaseType() => (ulong)Internal.Runtime.MethodTable.Of<DispatchOwnerBase>();
    [MethodImpl(MethodImplOptions.NoInlining)] private static ulong DispatchAType() => (ulong)Internal.Runtime.MethodTable.Of<DispatchReceiverA>();
    [MethodImpl(MethodImplOptions.NoInlining)] private static ulong DispatchBType() => (ulong)Internal.Runtime.MethodTable.Of<DispatchReceiverB>();

    private static ulong DispatchRequested(ulong operation) => operation == 0 ? (ulong)Internal.Runtime.MethodTable.Of<IDispatchPlain>() :
        operation == 1 ? (ulong)Internal.Runtime.MethodTable.Of<IDispatchVirtual>() :
        operation == 2 ? (ulong)Internal.Runtime.MethodTable.Of<IDispatchProducer<DispatchArgumentBase>>() :
        operation == 3 ? (ulong)Internal.Runtime.MethodTable.Of<IDispatchConsumer<DispatchArgumentDerived>>() :
        (ulong)Internal.Runtime.MethodTable.Of<IDispatchNoise>();
    private static ulong DispatchImplemented(ulong operation, bool second) =>
        operation == 2 && !second ? (ulong)Internal.Runtime.MethodTable.Of<IDispatchProducer<DispatchArgumentDerived>>() :
        operation == 3 && !second ? (ulong)Internal.Runtime.MethodTable.Of<IDispatchConsumer<DispatchArgumentBase>>() : DispatchRequested(operation);

    internal static void DispatchConstructed(ulong seed)
    {
        if (s_dispatchPhase != 1 || s_gcPhase != 5 || s_dispatchConstructors >= 2 ||
            s_dispatchAllocations != s_dispatchConstructors + 1 ||
            seed != s_dispatchSeed + (s_dispatchConstructors == 0 ? 11UL : 22UL))
            RejectDispatch(31, CompilerInterfaceDispatchStatus.ReceiverRejected);
        s_dispatchConstructors++;
    }

    // The sole new allocation role is available after the accepted collector
    // graph finishes. It admits exactly A followed by B on the original owner;
    // reference-free compiler metadata is checked before the real RhpNewFast call.
    private static ulong AllocateDispatchReceiver(ulong token)
    {
        if (s_dispatchPhase != 1 || s_gcPhase != 5 || s_collectorPhase != 7 || s_dispatchAllocations >= 2 ||
            s_dispatchConstructors != s_dispatchAllocations ||
            token != (s_dispatchAllocations == 0 ? DispatchAType() : DispatchBType()) || !DispatchBaselineIntact())
            RejectDispatch(32, CompilerInterfaceDispatchStatus.ReceiverRejected);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* collecting = &s_gcCollecting)
        {
            CollectorTypeMapResult map = BoundedNonMovingCollectorExplicitRootEnumeration.InspectType(
                NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, s_gcImage, token);
            if (!map.Succeeded || map.Prefix.BaseSize != 24 || map.SeriesCount != 0)
                RejectDispatch(33, CompilerInterfaceDispatchStatus.MetadataRejected);
            RhpNewFastResult result = GuestTestRhpNewFast.TryAllocate(s_access, &collecting->State,
                (ulong)sizeof(GcCollectingStorage), NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, map.Prefix, token);
            if (!result.AllocationSucceeded || result.AllocationCount != s_dispatchAllocations + 9 || result.BytesConsumed != 24 ||
                GcReadWord(result.MappedAllocationStart) != 0 || GcReadWord(result.ObjectReference) != token ||
                GcReadWord(result.ObjectReference + 8) != 0)
                RejectDispatch(34, CompilerInterfaceDispatchStatus.ReceiverRejected);
            if (s_dispatchAllocations == 0) s_dispatchReceipts.A = result;
            else s_dispatchReceipts.B = result;
            s_dispatchAllocations++;
            return result.ObjectReference;
        }
    }

    private static CompilerInterfaceDispatchSealedTable DispatchOwnerRecord(ulong owner, bool baseOwner)
    {
        if (!DispatchMetadata(owner, 24))
            RejectDispatch(35, CompilerInterfaceDispatchStatus.SlotRejected);
        uint flags = *(uint*)owner;
        uint slots = *(ushort*)(owner + 16), interfaces = *(ushort*)(owner + 18);
        if (flags != (baseOwner ? 0x50440000U : 0x50040000U) || *(uint*)(owner + 4) != 24 ||
            slots != (baseOwner ? 1U : 4U) || interfaces != (baseOwner ? 2U : 5U))
            RejectDispatch(36, CompilerInterfaceDispatchStatus.UnsupportedType);
        // Static, non-finalizable class: vtable/interface pointers, module and
        // writable-data relative words, dispatch map, then Base's sealed word.
        ulong offset = 36 + (slots + (ulong)interfaces) * 8;
        if (!DispatchMetadata(owner, offset + (baseOwner ? 4UL : 0UL)))
            RejectDispatch(37, CompilerInterfaceDispatchStatus.MetadataRejected);
        if (!baseOwner) return new() { DeclaringType = owner };
        ulong entry = owner + offset;
        long displacement = *(int*)entry;
        ulong table = displacement >= 0 ? entry <= ulong.MaxValue - (ulong)displacement ? entry + (ulong)displacement : 0
            : entry >= (ulong)-displacement ? entry - (ulong)-displacement : 0;
        if (table == 0 || !DispatchMetadata(table, DispatchBaseSealedBytes)) RejectDispatch(38, CompilerInterfaceDispatchStatus.SlotRejected);
        return new() { DeclaringType = owner, Table = table, Bytes = DispatchBaseSealedBytes };
    }

    private static bool DispatchMetadata(ulong address, ulong bytes)
    {
        fixed (GcTypeStorage* type = &s_gcType)
            return TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, address, bytes,
                TypeManagerRegisteredRangeKind.ImmutableMetadata) == TypeManagerModuleStatus.Success;
    }

    private static bool DispatchBaselineIntact() => s_gcPhase == 5 && s_collectorPhase == 7 &&
        s_gcStack.Checks == 10 && s_control.ConstructorCount == 7 && s_collectorStores == 7 &&
        ReadGcRoot().Succeeded && ReadGcRoot().ObjectReference == s_dispatchOriginalRoot && CollectorReceiptValid(5) &&
        Bits(GcStaticFixture.Get()) == s_dispatchOriginalNode && ReadStaticCollectorTag(false) == s_dispatchOriginalTag;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CheckDispatchStack()
    {
        ulong witness = 0, address = (ulong)&witness;
        ulong authority = 0, start = 0, end = 0, initialRsp = 0;
        if (s_dispatchPhase == 0 || s_dispatchPhase > 2 || s_dispatchStackChecks >= 17 ||
            !GcStackContext(address, out authority, out start, out end, out initialRsp) ||
            authority != s_gcStack.Authority || start != s_gcStack.Start || end != s_gcStack.End ||
            initialRsp != s_gcStack.InitialRsp || address > s_gcStack.Witness ||
            GcReadWord(start) != GcStackWord0 || GcReadWord(start + 8) != GcStackWord1) return false;
        if (s_dispatchMinimumWitness == 0 || address < s_dispatchMinimumWitness) s_dispatchMinimumWitness = address;
        s_dispatchStackChecks++;
        return true;
    }

    private static void DispatchLine(ulong stage, ulong a, ulong b, ulong c, ulong d)
    {
        Write((byte)'M'); Write((byte)'0'); Write((byte)'4'); Write((byte)'_');
        Write((byte)'D'); Write((byte)'I'); Write((byte)'S'); Write((byte)'P');
        Write((byte)'A'); Write((byte)'T'); Write((byte)'C'); Write((byte)'H'); Write(32);
        Decimal(stage); Write(32); Hex(a); Write(32); Hex(b); Write(32); Hex(c); Write(32); Hex(d); End();
    }

    private static void DispatchText(uint kind)
    {
        ulong a, b, c; uint bytes;
        if (kind == 0) { a = 0x4154535F54534554; b = 0x302E34306D205452; c = 0x000000000A0D3631; bytes = 20; }
        else if (kind == 1) { a = 0x304D5F534F524C43; b = 0x45525F3631305F34; c = 0x0000000A0D594441; bytes = 21; }
        else { a = 0x5341505F54534554; b = 0x31302E34306D2053; c = 0x00000000000A0D36; bytes = 19; }
        for (uint i = 0; i < bytes; i++) Write((byte)((i < 8 ? a : i < 16 ? b : c) >> ((int)(i & 7) * 8)));
    }

    private static ulong DispatchFailure(ulong reason, CompilerInterfaceDispatchStatus status)
    {
        DispatchLine(255, reason, s_dispatchPhase, s_dispatchCalls, (ulong)status);
        s_dispatchPhase = 255;
        return 0;
    }
    private static void RejectDispatch(ulong reason, CompilerInterfaceDispatchStatus status)
    {
        DispatchFailure(reason, status);
        while (true) System.RuntimeFailFast.Halt();
    }
}
#endif
