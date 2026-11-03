using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using Core = OS.Boot.OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput;
using Status = OS.Boot.OwnedAllocatorRoundTripStatus;

internal static unsafe partial class OwnedAllocatorDescriptorTablesACPISnapshotPanicOutputTests
{
    private static void Need(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    [HostTest("m03.010.allocator.restoration", HostLane.Fast, 0)]
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
            Need(fixture.State.Allocated && fixture.State.Written && fixture.State.Restored && fixture.State.Freed && fixture.State.Page == (ulong)fixture.Backing, "actual page and cleanup milestones");
            Need(owner.AsSpan().SequenceEqual(fixture.OwnerSpan()) && backing.AsSpan().SequenceEqual(fixture.BackingSpan()) && fixture.Owner->IsCompleted && FirstExitOwnedArena.ValidateCanaries(fixture.Arena), "exact owner/page/accounting/canary restoration");
            string serial = Encoding.ASCII.GetString(Output, 0, outputBytes);
            Need(serial.EndsWith("TEST_PASS m03.009\r\n", StringComparison.Ordinal) && !serial.Contains("m03.010", StringComparison.Ordinal) && !serial.Contains("CLROS_M03_010_READY", StringComparison.Ordinal), "actual prerequisite output only; no full issue claim");
            if (i == 2)
            {
                Need(allocated == 0, "warmed operation allocates no managed memory");
                Console.WriteLine("M03_ALLOCATOR_HOST_OBSERVATION allocatedBytes=" + allocated + " restored=true freed=true");
            }
        }
    }

    [HostTest("m03.010.allocator.owner_backing_aliases", HostLane.Fast, 1)]
    private static void OwnerBackingAliases()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        {
            using var fixture = new Fixture();
            FirstExitDiagnosticMemory memory = fixture.Memory;
            M01LiveAllocatorStaticState copy = *fixture.Owner;
            M01LiveAllocatorStaticState* owner = fixture.Owner;
            OwnedAllocatorRoundTripState* operation = fixture.Operation;
            if (scenario == 0)
                owner = &copy;
            if (scenario == 1)
                memory.References.AllocatorStateBytes--;
            if (scenario == 2)
                memory.Allocator = new((ulong)fixture.Backing + 4096, 2, PageProtection.Read | PageProtection.Write);
            if (scenario == 3)
                operation = (OwnedAllocatorRoundTripState*)fixture.Owner;
            if (scenario == 4)
                memory.Pages = (X64PageTableImagePage*)fixture.Operation;
            if (scenario == 5)
                memory.References.CopiedAcpi = (ulong)fixture.Owner;
            if (scenario == 6)
                memory.Allocator = new(memory.References.AdapterState, 2, PageProtection.Read | PageProtection.Write);
            byte[] image = fixture.ImageBytes(), backing = fixture.BackingBytes(), arena = fixture.ArenaBytes();
            Status status = Core.RunAllocatorRoundTrip(ref *operation, ref fixture.Diagnostic, ref fixture.Arena, memory, owner);
            Need(status == (scenario == 2 ? Status.OwnerRejected : Status.AdmissionFailed), "classified pre-admission refusal " + scenario);
            Need(image.AsSpan().SequenceEqual(fixture.ImageSpan()) && backing.AsSpan().SequenceEqual(fixture.BackingSpan()) && arena.AsSpan().SequenceEqual(fixture.ArenaSpan()) && outputBytes == 0 && polls == 0, "invalid owner/backing/alias has no write or output " + scenario);
        }
    }

    [HostTest("m03.010.allocator.stale_owner", HostLane.Fast, 2)]
    private static void StaleOwner()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            using var fixture = new Fixture();
            if (scenario == 0)
                Set(fixture.Owner, "HeaderVersion", 0U);
            if (scenario == 1)
                Set(fixture.Owner, "CoordinatorCheckpoint", M01LiveAllocatorCheckpoint.PageAllocated);
            if (scenario == 2)
                Set(fixture.Owner, "AllocatorStateValue", new BootPageAllocatorState(true, 4096, -1, 0, 0, 0, 2, 8192));
            byte[] owner = fixture.OwnerBytes(), backing = fixture.BackingBytes();
            Need(fixture.Invoke() == Status.OwnerRejected && fixture.State.Status == Status.Empty && fixture.Diagnostic.Status == FirstExitDiagnosticStatus.Empty && outputBytes == 0 && polls == 0 && owner.AsSpan().SequenceEqual(fixture.OwnerSpan()) && backing.AsSpan().SequenceEqual(fixture.BackingSpan()), "stale/nonfree completed-owner contract rejects before mutation " + scenario);
        }
    // A valid completed original owner starts wholly free with at least two
    // pages. Exhaustion is an invalid prerequisite, not a fabricated reachable
    // NoSuitableRange result from this operation.
    }

    [HostTest("m03.010.allocator.consumed_and_diagnostic_failure", HostLane.Fast, 3)]
    private static void ConsumedAndDiagnosticFailure()
    {
        for (int scenario = 0; scenario < 4; scenario++)
        {
            using var fixture = new Fixture();
            if (scenario == 1)
                fixture.Arena.Permanent->Phase = FirstExitAdoptionPhase.Empty;
            if (scenario == 2)
                transmitLimit = 17;
            if (scenario == 3)
                fixture.Diagnostic.Status = FirstExitDiagnosticStatus.Completed;
            byte[] owner = fixture.OwnerBytes(), backing = fixture.BackingBytes();
            Status first = fixture.Invoke();
            Need(first == (scenario == 0 ? Status.Completed : Status.DiagnosticsFailed), "real prerequisite result " + scenario);
            int priorBytes = outputBytes, priorPolls = polls;
            Need(fixture.Invoke() == Status.AlreadyConsumed && outputBytes == priorBytes && polls == priorPolls, "consumed operation never calls reporter again " + scenario);
            if (scenario != 0)
                Need(!fixture.State.Allocated && !fixture.State.Written && !fixture.State.Restored && !fixture.State.Freed, "failed prerequisite has no allocator activity " + scenario);
            if (scenario == 2)
                Need(outputBytes == 17 && fixture.Diagnostic.Status == FirstExitDiagnosticStatus.SerialFailed && Encoding.ASCII.GetString(Output, 0, outputBytes) == "M03_EBS_RETURNED_", "partial serial prefix retained");
            Need(owner.AsSpan().SequenceEqual(fixture.OwnerSpan()) && backing.AsSpan().SequenceEqual(fixture.BackingSpan()) && FirstExitOwnedArena.ValidateCanaries(fixture.Arena), "owner/backing/canaries preserved " + scenario);
        }
    }

    private static readonly byte[] Output = new byte[4096];
    private static int outputBytes, polls, transmitLimit;
    private static bool divisorLatch;
    [UnmanagedCallersOnly]
    private static void Out8(ushort port, byte value)
    {
        if (port == 0x3fb)
            divisorLatch = (value & 0x80) != 0;
        if (port == 0x3f8 && !divisorLatch)
            Output[outputBytes++] = value;
    }

    [UnmanagedCallersOnly]
    private static byte In8(ushort port)
    {
        polls++;
        return port == 0x3fd && outputBytes < transmitLimit ? (byte)0x20 : (byte)0;
    }

    [UnmanagedCallersOnly]
    private static ulong GetMap(ulong* bytes, void* buffer, ulong* key, ulong* width, uint* version)
    {
        *bytes = 40;
        *key = 1;
        *width = 40;
        *version = 1;
        return 0;
    }

    [UnmanagedCallersOnly]
    private static ulong Exit(void* image, ulong key) => key == 1 ? 0UL : 1UL;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void* VirtualAlloc(void* address, nuint bytes, uint type, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(void* address, nuint bytes, uint type);
    // Host-only construction of prior completed M01/native observations, as in
    // the existing first-exit fixture. The actual reporter, table builder and
    // allocator execute over these buffers; no privileged instructions execute.
    private static void Set<T>(M01LiveAllocatorStaticState* owner, string name, T value)
        where T : unmanaged => *(T*)((byte*)owner + Marshal.OffsetOf<M01LiveAllocatorStaticState>(name)) = value;
}
