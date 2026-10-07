using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using Core = OS.Boot.OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput;
using Status = OS.Boot.OwnedAllocatorRoundTripStatus;

internal static unsafe class OwnedAllocatorDescriptorTablesACPISnapshotPanicOutputTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Group("restoration", Restoration);
        Group("owner_backing_aliases", OwnerBackingAliases);
        Group("stale_owner", StaleOwner);
        Group("consumed_and_diagnostic_failure", ConsumedAndDiagnosticFailure);
    }

    private static void Group(string name, Action test)
    {
        string marker = "m03.010.allocator." + name;
        Console.WriteLine("TEST_START " + marker);
        try { test(); Console.WriteLine("TEST_PASS " + marker); }
        catch (Exception error) { Console.WriteLine("TEST_FAIL " + marker + " " + error.Message); throw; }
    }
    private static void Need(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static void Restoration()
    {
        // Warm the actual operation before measuring only that operation. Fixture
        // allocation and all host assertions remain outside the measured interval.
        for (int i = 0; i < 3; i++)
        {
            using var fixture = new Fixture();
            byte[] owner = fixture.OwnerBytes();
            byte[] backing = fixture.BackingBytes();
            long before = GC.GetAllocatedBytesForCurrentThread();
            Status status = fixture.Invoke();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Need(status == Status.Completed && fixture.State.Status == Status.Completed, "round trip completes");
            Need(fixture.State.Allocated && fixture.State.Written && fixture.State.Restored && fixture.State.Freed &&
                fixture.State.Page == (ulong)fixture.Backing, "actual page and cleanup milestones");
            Need(owner.AsSpan().SequenceEqual(fixture.OwnerSpan()) && backing.AsSpan().SequenceEqual(fixture.BackingSpan()) &&
                fixture.Owner->IsCompleted && FirstExitOwnedArena.ValidateCanaries(fixture.Arena), "exact owner/page/accounting/canary restoration");
            string serial = Encoding.ASCII.GetString(Output, 0, outputBytes);
            Need(serial.EndsWith("TEST_PASS m03.009\r\n", StringComparison.Ordinal) &&
                !serial.Contains("m03.010", StringComparison.Ordinal) && !serial.Contains("CLROS_M03_010_READY", StringComparison.Ordinal),
                "actual prerequisite output only; no full issue claim");
            if (i == 2)
            {
                Need(allocated == 0, "warmed operation allocates no managed memory");
                Console.WriteLine("M03_ALLOCATOR_HOST_OBSERVATION allocatedBytes=" + allocated + " restored=true freed=true");
            }
        }
    }

    private static void OwnerBackingAliases()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        {
            using var fixture = new Fixture();
            FirstExitDiagnosticMemory memory = fixture.Memory;
            M01LiveAllocatorStaticState copy = *fixture.Owner;
            M01LiveAllocatorStaticState* owner = fixture.Owner;
            OwnedAllocatorRoundTripState* operation = fixture.Operation;
            if (scenario == 0) owner = &copy;
            if (scenario == 1) memory.References.AllocatorStateBytes--;
            if (scenario == 2) memory.Allocator = new((ulong)fixture.Backing + 4096, 2, PageProtection.Read | PageProtection.Write);
            if (scenario == 3) operation = (OwnedAllocatorRoundTripState*)fixture.Owner;
            if (scenario == 4) memory.Pages = (X64PageTableImagePage*)fixture.Operation;
            if (scenario == 5) memory.References.CopiedAcpi = (ulong)fixture.Owner;
            if (scenario == 6) memory.Allocator = new(memory.References.AdapterState, 2, PageProtection.Read | PageProtection.Write);
            byte[] image = fixture.ImageBytes(), backing = fixture.BackingBytes(), arena = fixture.ArenaBytes();
            Status status = Core.RunAllocatorRoundTrip(ref *operation, ref fixture.Diagnostic, ref fixture.Arena, memory, owner);
            Need(status == (scenario == 2 ? Status.OwnerRejected : Status.AdmissionFailed), "classified pre-admission refusal " + scenario);
            Need(image.AsSpan().SequenceEqual(fixture.ImageSpan()) && backing.AsSpan().SequenceEqual(fixture.BackingSpan()) &&
                arena.AsSpan().SequenceEqual(fixture.ArenaSpan()) && outputBytes == 0 && polls == 0,
                "invalid owner/backing/alias has no write or output " + scenario);
        }
    }

    private static void StaleOwner()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            using var fixture = new Fixture();
            if (scenario == 0) Set(fixture.Owner, "HeaderVersion", 0U);
            if (scenario == 1) Set(fixture.Owner, "CoordinatorCheckpoint", M01LiveAllocatorCheckpoint.PageAllocated);
            if (scenario == 2)
                Set(fixture.Owner, "AllocatorStateValue", new BootPageAllocatorState(true, 4096, -1, 0, 0, 0, 2, 8192));
            byte[] owner = fixture.OwnerBytes(), backing = fixture.BackingBytes();
            Need(fixture.Invoke() == Status.OwnerRejected && fixture.State.Status == Status.Empty &&
                fixture.Diagnostic.Status == FirstExitDiagnosticStatus.Empty && outputBytes == 0 && polls == 0 &&
                owner.AsSpan().SequenceEqual(fixture.OwnerSpan()) && backing.AsSpan().SequenceEqual(fixture.BackingSpan()),
                "stale/nonfree completed-owner contract rejects before mutation " + scenario);
        }
        // A valid completed original owner starts wholly free with at least two
        // pages. Exhaustion is an invalid prerequisite, not a fabricated reachable
        // NoSuitableRange result from this operation.
    }

    private static void ConsumedAndDiagnosticFailure()
    {
        for (int scenario = 0; scenario < 4; scenario++)
        {
            using var fixture = new Fixture();
            if (scenario == 1) fixture.Arena.Permanent->Phase = FirstExitAdoptionPhase.Empty;
            if (scenario == 2) transmitLimit = 17;
            if (scenario == 3) fixture.Diagnostic.Status = FirstExitDiagnosticStatus.Completed;
            byte[] owner = fixture.OwnerBytes(), backing = fixture.BackingBytes();
            Status first = fixture.Invoke();
            Need(first == (scenario == 0 ? Status.Completed : Status.DiagnosticsFailed), "real prerequisite result " + scenario);
            int priorBytes = outputBytes, priorPolls = polls;
            Need(fixture.Invoke() == Status.AlreadyConsumed && outputBytes == priorBytes && polls == priorPolls,
                "consumed operation never calls reporter again " + scenario);
            if (scenario != 0)
                Need(!fixture.State.Allocated && !fixture.State.Written && !fixture.State.Restored && !fixture.State.Freed,
                    "failed prerequisite has no allocator activity " + scenario);
            if (scenario == 2)
                Need(outputBytes == 17 && fixture.Diagnostic.Status == FirstExitDiagnosticStatus.SerialFailed &&
                    Encoding.ASCII.GetString(Output, 0, outputBytes) == "M03_EBS_RETURNED_", "partial serial prefix retained");
            Need(owner.AsSpan().SequenceEqual(fixture.OwnerSpan()) && backing.AsSpan().SequenceEqual(fixture.BackingSpan()) &&
                FirstExitOwnedArena.ValidateCanaries(fixture.Arena), "owner/backing/canaries preserved " + scenario);
        }
    }

    private static readonly byte[] Output = new byte[4096];
    private static int outputBytes, polls, transmitLimit;
    private static bool divisorLatch;
    [UnmanagedCallersOnly]
    private static void Out8(ushort port, byte value)
    {
        if (port == 0x3fb) divisorLatch = (value & 0x80) != 0;
        if (port == 0x3f8 && !divisorLatch) Output[outputBytes++] = value;
    }
    [UnmanagedCallersOnly]
    private static byte In8(ushort port)
    { polls++; return port == 0x3fd && outputBytes < transmitLimit ? (byte)0x20 : (byte)0; }
    [UnmanagedCallersOnly]
    private static ulong GetMap(ulong* bytes, void* buffer, ulong* key, ulong* width, uint* version)
    { *bytes = 40; *key = 1; *width = 40; *version = 1; return 0; }
    [UnmanagedCallersOnly]
    private static ulong Exit(void* image, ulong key) => key == 1 ? 0UL : 1UL;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void* VirtualAlloc(void* address, nuint bytes, uint type, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(void* address, nuint bytes, uint type);

    // Host-only construction of prior completed M01/native observations, as in
    // the existing first-exit fixture. The actual reporter, table builder and
    // allocator execute over these buffers; no privileged instructions execute.
    private static void Set<T>(M01LiveAllocatorStaticState* owner, string name, T value) where T : unmanaged
        => *(T*)((byte*)owner + Marshal.OffsetOf<M01LiveAllocatorStaticState>(name)) = value;

    private sealed class Fixture : IDisposable
    {
        private byte* image, code, arenaData;
        private ulong* entries;
        internal byte* Backing;
        internal FirstExitDiagnosticMemory Memory;
        internal M01LiveAllocatorStaticState* Owner => (M01LiveAllocatorStaticState*)(image + 0x4400);
        internal OwnedAllocatorRoundTripState* Operation => (OwnedAllocatorRoundTripState*)(image + 0x4080);
        internal ref OwnedAllocatorRoundTripState State => ref *Operation;
        internal ref FirstExitDiagnosticState Diagnostic => ref *(FirstExitDiagnosticState*)(image + 0x4040);
        internal ref FirstExitOwnedArenaState Arena => ref *(FirstExitOwnedArenaState*)(image + 0x4010);
        internal Status Invoke() => Core.RunAllocatorRoundTrip(ref State, ref Diagnostic, ref Arena, Memory, Owner);
        internal ReadOnlySpan<byte> OwnerSpan() => new(Owner, sizeof(M01LiveAllocatorStaticState));
        internal ReadOnlySpan<byte> BackingSpan() => new(Backing, 8192);
        internal ReadOnlySpan<byte> ImageSpan() => new(image, 65536);
        internal ReadOnlySpan<byte> ArenaSpan() => new(arenaData, (int)FirstExitOwnedArena.Bytes);
        internal byte[] OwnerBytes() => OwnerSpan().ToArray();
        internal byte[] BackingBytes() => BackingSpan().ToArray();
        internal byte[] ImageBytes() => ImageSpan().ToArray();
        internal byte[] ArenaBytes() => ArenaSpan().ToArray();

        internal Fixture()
        {
            Need(OperatingSystem.IsWindows(), "fixture requires Windows low identity table backing");
            try
            {
                image = Allocate(65536); code = Allocate(FirstExitCodeBundle.Bytes);
                arenaData = Allocate(FirstExitOwnedArena.Bytes); Backing = Allocate(8192);
                for (int i = 0; i < 8192; i++) Backing[i] = (byte)(i * 31 + 9);
                for (uint hint = 0x30000000; hint < 0x60000000 && entries == null; hint += 0x01000000)
                    entries = (ulong*)VirtualAlloc((void*)(nuint)hint, 55 * 4096, 0x3000, 4);
                Need(entries != null, "low identity table allocation");
                PrepareImage(); PrepareOwner(); PrepareReporter();
                outputBytes = polls = 0; transmitLimit = int.MaxValue; divisorLatch = false;
            }
            catch { Dispose(); throw; }
        }

        private static byte* Allocate(uint bytes)
        {
            byte* result = (byte*)NativeMemory.AlignedAlloc(bytes, 4096);
            if (result == null) throw new OutOfMemoryException();
            new Span<byte>(result, (int)bytes).Clear(); return result;
        }

        private void PrepareImage()
        {
            *(ushort*)image = 0x5a4d; *(uint*)(image + 60) = 0x80;
            byte* pe = image + 0x80;
            *(uint*)pe = 0x4550; *(ushort*)(pe + 4) = 0x8664;
            *(ushort*)(pe + 6) = 2; *(ushort*)(pe + 20) = 240; *(ushort*)(pe + 22) = 2;
            byte* optional = pe + 24;
            *(ushort*)optional = 0x20b; *(uint*)(optional + 16) = 0x1000;
            *(uint*)(optional + 32) = 4096; *(uint*)(optional + 36) = 512;
            *(uint*)(optional + 56) = 65536; *(uint*)(optional + 60) = 512;
            *(ushort*)(optional + 68) = 10; *(uint*)(optional + 108) = 16;
            byte* text = optional + 240;
            *(uint*)(text + 8) = 4096; *(uint*)(text + 12) = 0x1000;
            *(uint*)(text + 16) = 4096; *(uint*)(text + 36) = 0x60000020;
            byte* data = text + 40;
            *(uint*)(data + 8) = 0x9000; *(uint*)(data + 12) = 0x4000;
            *(uint*)(data + 16) = 512; *(uint*)(data + 36) = 0xc0000040;
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)image, 65536);
            Memory.Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Memory.Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Memory.Capture);
            Memory.Code = code;
            Memory.References = new() { EntryCallback = (ulong)image + 0x1008, DiagnosticCallback = (ulong)image + 0x1010,
                FaultCallback = (ulong)image + 0x1000, AdapterState = (ulong)image + 0x4000, AdapterStateBytes = 512,
                AllocatorState = (ulong)Owner, AllocatorStateBytes = 312, CopiedAcpi = (ulong)image + 0xb000, CopiedAcpiBytes = 4096 };
            Memory.Allocator = new((ulong)Backing, 2, PageProtection.Read | PageProtection.Write);
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary((ulong)entries, out PhysicalAddress root);
            X64PatMappingToken.TryCreate(0x0006_0501_0007_0406, 0, 3, 4, out X64PatMappingToken pat);
            Memory.Tables = new(root, 55, X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat));
            Memory.Pages = (X64PageTableImagePage*)(image + 0x8000); Memory.PageCapacity = 55;
            Memory.Entries = entries; Memory.EntryCapacity = 55 * 512;
        }

        private void PrepareOwner()
        {
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary((ulong)Backing, out PhysicalAddress start);
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary((ulong)Backing + 4096, out PhysicalAddress end);
            Need(BootReservedRange.TryCreate(BootReservationKind.AllocatorArena, start, 2, out var reservation) == BootReservedRangeStatus.Success &&
                BootReservationSourceRange.TryCreate(0, start, 2, out _) == BootReservationSourceRangeStatus.Success, "typed allocator geometry");
            BootReservationSourceRange.TryCreate(0, start, 2, out var range);
            Set(Owner, "HeaderSize", 312U); Set(Owner, "HeaderVersion", M01LiveAllocatorStaticState.CurrentVersion);
            Set(Owner, "CoordinatorStatus", M01LiveAllocatorStatus.Success);
            Set(Owner, "CoordinatorCheckpoint", M01LiveAllocatorCheckpoint.DoubleFreeRejected);
            Set(Owner, "DoubleFreeStatusValue", BootPageAllocatorMutationStatus.FreeRangeNotAllocated);
            Set(Owner, "StageOneStateAddressValue", (ulong)image + 0x5000);
            Set(Owner, "StageOneStateCapacityValue", M01LoadedImageStackStaticState.RequiredStorageBytes);
            Set(Owner, "RefreshedMapKeyValue", 1UL); Set(Owner, "RefreshedMapGenerationValue", 1UL);
            Set(Owner, "ArenaReservationValue", reservation); Set(Owner, "ArenaSourceRangeValue", range); Set(Owner, "FreeRangeValue", range);
            Set(Owner, "FreeNodeValue", new BootPageFreeListNode(0, 0, (ulong)Backing, (ulong)Backing + 8192, 2, 8192, -1));
            Set(Owner, "AllocatorStateValue", new BootPageAllocatorState(true, 4096, 0, 1, 2, 8192, 0, 0));
            Set(Owner, "AllocatedPageStartValue", start); Set(Owner, "AllocatedPageEndExclusiveValue", end);
            Set(Owner, "InitialFreePagesValue", 2UL); Set(Owner, "InitialFreeBytesValue", 8192UL);
            Need(Owner->IsCompleted, "modeled prior completed owner");
        }

        private void PrepareReporter()
        {
            var references = Memory.References;
            Need(FirstExitOwnedArena.Materialize(ref Arena, arenaData, FirstExitOwnedArena.Bytes, (ulong)code, 56), "owned arena");
            Need(FirstExitCodeBundle.Materialize(code, FirstExitCodeBundle.Bytes, ref Arena, Memory.Capture, Memory.Mapping, references.FaultCallback), "retained generated code");
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            Need(FirstExitRetainedMappings.WriteRanges(ref Arena, code, Memory.Capture, Memory.Mapping, references,
                Memory.Allocator, Memory.Tables, ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count), "retained declarations");
            Need(SeparateDisabledDefaultPageFaultPageTables.BuildRetainedImage(Memory.Tables, ranges, count,
                FirstExitRetainedMappings.MaximumRanges, Memory.Pages, 55, entries, 55 * 512).Succeeded, "actual retained table backing");
            Need(FirstExitOwnedStackStub.Prepare(ref *Arena.StackEntry, 0x100000, 0x668, Arena.StackBottom, Arena.StackTop,
                FirstExitOwnedArena.LowerCanary, FirstExitOwnedArena.UpperCanary, 196608,
                (delegate* unmanaged<FirstExitOwnedStackContext*, void>)references.EntryCallback, (void*)references.AdapterState, references.AdapterStateBytes), "entry context");
            Need(FirstExitPermanentCr3Stub.Prepare(ref *Arena.Permanent, 0x100000, (ulong)entries, 0x668,
                Arena.StackBottom, Arena.StackTop, 4096, FirstExitOwnedArena.LowerCanary, FirstExitOwnedArena.UpperCanary,
                (delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void>)references.DiagnosticCallback,
                (void*)references.AdapterState, Arena.Descriptors), "continuation context");
            BoundedExitRetryState retry = default;
            var attempt = BoundedRetryStaleKeyStatus.Execute(ref retry, FirmwareServicesMode.BootServicesAvailable,
                &GetMap, &Exit, (void*)123, Arena.Map, FirstExitOwnedArena.MapBytes);
            Need(attempt.Succeeded, "controlled exit result");
            *(BoundedExitRetryResult*)((byte*)Arena.Transition + Marshal.OffsetOf<FirstExitTransitionState>("AttemptValue")) = attempt;
            *(uint*)Arena.Transition = (uint)FirstExitPhase.OwnedContinuation;
            ((uint*)Arena.Transition)[1] = (uint)FirmwareServicesMode.BootServicesExited;
            *(uint*)Arena.Binding = 2;
            Arena.StackEntry->Phase = 2; Arena.StackEntry->ObservedRsp = Arena.StackTop - FirstExitOwnedStackStub.CallStackBytes;
            Arena.Permanent->Phase = FirstExitAdoptionPhase.OwnedDiagnostic;
            Arena.Permanent->ObservedCr3 = (ulong)entries; Arena.Permanent->ObservedCr4 = 0x668 & ~0x80UL;
            Arena.Permanent->ObservedRsp = Arena.StackTop - 4096 - 8;
            FirstExitDescriptorContext* descriptor = Arena.Descriptors;
            descriptor->Gdt[13] |= 1; descriptor->Gdt[21] |= 1; descriptor->Gdt[29] |= 2;
            descriptor->Gdt[descriptor->FirmwareCs + 5] |= 1;
            for (int i = 0; i < 16; i++) { descriptor->ObservedGdtr[i] = descriptor->Gdtr[i]; descriptor->ObservedIdtr[i] = descriptor->FinalIdtr[i]; }
            descriptor->ObservedTr = 24; descriptor->ObservedCs = 8;
            descriptor->ObservedDs = descriptor->ObservedEs = descriptor->ObservedSs = 16; descriptor->Prepared = 2;
            nuint output = (nuint)(delegate* unmanaged<ushort, byte, void>)&Out8;
            nuint input = (nuint)(delegate* unmanaged<ushort, byte>)&In8;
            nuint first = Math.Min(output, input), end = Math.Max(output + 7, input + 5);
            Arena.Fault->Backend = new Com1OwnedPortIoBackend(InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(
                CheckedPortIoFunctionPointerBindingRequest.Create(true, 0x3f8, first, end, 12,
                    output - first, input - first, output, input, 7, 5, 7, 5)));
        }

        public void Dispose()
        {
            if (entries != null) { VirtualFree(entries, 0, 0x8000); entries = null; }
            NativeMemory.AlignedFree(image); image = null; NativeMemory.AlignedFree(code); code = null;
            NativeMemory.AlignedFree(arenaData); arenaData = null; NativeMemory.AlignedFree(Backing); Backing = null;
        }
    }
}
