using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OS.Boot;
using OS.Kernel.Memory;

internal static unsafe partial class X64ProtectionProbeStubPlanTests
{
    private sealed unsafe class ProtectionFixture : IDisposable
    {
        private const ulong InvlpgEntryOffset = 0x100;
        private byte* _probePage;
        private byte* _invlpgPage;
        internal byte* ProbePage => _probePage;
        internal M01014PageTableFixture PageTables { get; }
        internal BootMemoryRegionRecord ProbeAllocation { get; }
        internal BootReservedRange ProbeReservation { get; }
        internal BootMemoryRegionRecord TransitionCodeAllocation { get; }
        internal BootMemoryRegionRecord TableStateAllocation { get; }
        internal X64ProtectionProbeStubPlanResult ProbeStub { get; }
        internal X64PageMapResult ProtectionMutation { get; }
        internal ulong LeafReadbackRaw { get; }
        internal X64InvlpgStubPlanResult InvlpgStub { get; }
        internal X64InvlpgInvocationPlanResult InvlpgInvocation { get; }
        internal X64ProtectionProbeObservation Observation { get; }
        internal X64ProtectionProbeEvent[] Events { get; }

        internal ProtectionFixture()
        {
            _probePage = (byte*)NativeMemory.AlignedAlloc((nuint)X64ProtectionProbeStubPlan.PageSize, (nuint)X64ProtectionProbeStubPlan.PageSize);
            _invlpgPage = (byte*)NativeMemory.AlignedAlloc((nuint)X64InvlpgStubPlan.PageSize, (nuint)X64InvlpgStubPlan.PageSize);
            if (_probePage == null || _invlpgPage == null)
                throw new OutOfMemoryException();
            Fill(_probePage, X64ProtectionProbeStubPlan.PageSize, 0xcc);
            Fill(_invlpgPage, X64InvlpgStubPlan.PageSize, 0xcc);
            ProbeAllocation = Record(UefiMemoryType.LoaderCode, ProbeStart, 1);
            TransitionCodeAllocation = Record(UefiMemoryType.LoaderCode, TransitionCodeStart, 1);
            TableStateAllocation = Record(UefiMemoryType.LoaderData, TableStateStart, 4);
            BootReservedRangeStatus reservationStatus = BootReservedRange.TryCreate(BootReservationKind.PageProtectionProbe, M01014TestData.Physical(ProbeStart), 1, out BootReservedRange reservation);
            if (reservationStatus != BootReservedRangeStatus.Success)
            {
                throw new InvalidOperationException("Probe reservation failed: " + reservationStatus);
            }

            ProbeReservation = reservation;
            X64ProtectionProbeStubPlanResult emitted = X64ProtectionProbeStubPlan.Emit(_probePage, ProbeStart, X64ProtectionProbeStubPlan.PageSize, ProbeAllocation, ProbeReservation, entryOffset: 0);
            ProbeStub = X64ProtectionProbeStubPlan.ValidateReadback(_probePage, ProbeStart, X64ProtectionProbeStubPlan.PageSize, emitted);
            if (!ProbeStub.ReadBackVerified)
            {
                throw new InvalidOperationException("Probe readback failed: " + ProbeStub.Status);
            }

            PageTables = new M01014PageTableFixture(ProbeStart, TableStateStart);
            X64PageMapResult mapped = PageTables.Apply(X64PageMapRequest.CreateMap(PageTables.Root, PageTables.Target, M01014TestData.Physical(ProbeStart), M01014TestData.ReadWrite, PageCachePolicy.WriteBack, PageTables.Context));
            if (!mapped.Succeeded)
            {
                throw new InvalidOperationException("Probe map failed: " + mapped.Status);
            }

            ProtectionMutation = PageTables.Apply(X64PageMapRequest.CreateProtect(PageTables.Root, PageTables.Target, M01014TestData.ReadExecute, X64HardwareStatePolicy.Preserve, PageTables.Context));
            if (!ProtectionMutation.Succeeded)
            {
                throw new InvalidOperationException("Probe protect failed: " + ProtectionMutation.Status);
            }

            LeafReadbackRaw = PageTables.Entries[checked((int)ProtectionMutation.TargetEntryIndex)];
            CreateInvlpgEvidence(PageTables.Target, M01014TestData.ReadExecute, out X64InvlpgStubPlanResult invlpgStub, out X64InvlpgInvocationPlanResult invlpgInvocation);
            InvlpgStub = invlpgStub;
            InvlpgInvocation = invlpgInvocation;
            if (!InvlpgStub.ReadBackVerified || !InvlpgInvocation.Succeeded)
            {
                throw new InvalidOperationException("INVLPG evidence failed: " + InvlpgStub.Status + "/" + InvlpgInvocation.Status);
            }

            Observation = X64ProtectionProbeObservation.CreateModeled(X64ProtectionProbeTerminalKind.ModeledReturn, ProbeStub.EntryAddress, 1, X64ProtectionProbeStubPlan.ExpectedReturnValue, Canary, Canary);
            Events = CreateEvents();
        }

        internal X64ProtectionProbePlanResult Audit(BootMemoryRegionRecord? probeAllocation = null, BootReservedRange? probeReservation = null, BootMemoryRegionRecord? transitionCodeAllocation = null, BootMemoryRegionRecord? tableStateAllocation = null, PageProtection? tableStateProtection = null, X64ProtectionProbeStubPlanResult? probeStub = null, X64PageMapResult? protectionMutation = null, ulong? leafReadbackRaw = null, X64InvlpgStubPlanResult? invlpgStub = null, X64InvlpgInvocationPlanResult? invlpgInvocation = null, X64ProtectionProbeObservation? observation = null, X64ProtectionProbeEvent[]? events = null, ulong? eventCount = null, ulong? eventCapacity = null, bool nullEvents = false)
        {
            X64ProtectionProbeEvent[] selectedEvents = events ?? Events;
            if (nullEvents)
            {
                return X64ProtectionProbeStubPlan.Audit(probeAllocation ?? ProbeAllocation, probeReservation ?? ProbeReservation, transitionCodeAllocation ?? TransitionCodeAllocation, tableStateAllocation ?? TableStateAllocation, tableStateProtection ?? M01014TestData.ReadWrite, probeStub ?? ProbeStub, protectionMutation ?? ProtectionMutation, leafReadbackRaw ?? LeafReadbackRaw, invlpgStub ?? InvlpgStub, invlpgInvocation ?? InvlpgInvocation, observation ?? Observation, null, eventCount ?? (ulong)Events.Length, eventCapacity ?? (ulong)Events.Length);
            }

            fixed (X64ProtectionProbeEvent* eventPointer = selectedEvents)
            {
                return X64ProtectionProbeStubPlan.Audit(probeAllocation ?? ProbeAllocation, probeReservation ?? ProbeReservation, transitionCodeAllocation ?? TransitionCodeAllocation, tableStateAllocation ?? TableStateAllocation, tableStateProtection ?? M01014TestData.ReadWrite, probeStub ?? ProbeStub, protectionMutation ?? ProtectionMutation, leafReadbackRaw ?? LeafReadbackRaw, invlpgStub ?? InvlpgStub, invlpgInvocation ?? InvlpgInvocation, observation ?? Observation, eventPointer, eventCount ?? (ulong)selectedEvents.Length, eventCapacity ?? (ulong)selectedEvents.Length);
            }
        }

        internal X64ProtectionProbePlanResult AuditWithEventPointer(X64ProtectionProbeEvent* events, ulong eventCount, ulong eventCapacity)
        {
            return X64ProtectionProbeStubPlan.Audit(ProbeAllocation, ProbeReservation, TransitionCodeAllocation, TableStateAllocation, M01014TestData.ReadWrite, ProbeStub, ProtectionMutation, LeafReadbackRaw, InvlpgStub, InvlpgInvocation, Observation, events, eventCount, eventCapacity);
        }

        internal void CreateInvlpgEvidence(VirtualAddress target, PageProtection codeProtection, out X64InvlpgStubPlanResult verifiedStub, out X64InvlpgInvocationPlanResult invocation)
        {
            if (X64TlbInvalidationPlan.TryCreate(target, X64TlbInvalidationPlan.PageSize, out X64TlbInvalidationRange range) != X64TlbInvalidationStatus.Success)
            {
                verifiedStub = default;
                invocation = default;
                return;
            }

            X64TlbInvalidationRecord* records = stackalloc X64TlbInvalidationRecord[1];
            X64TlbInvalidationPlanResult materialized = X64TlbInvalidationPlan.Materialize(range, records, 1);
            if (!materialized.Succeeded)
            {
                verifiedStub = default;
                invocation = default;
                return;
            }

            X64InvlpgStubPlanResult emitted = X64InvlpgStubPlan.Emit(_invlpgPage, TransitionCodeStart, X64InvlpgStubPlan.PageSize, M01014TestData.Virtual(TransitionCodeStart), InvlpgEntryOffset, records[0]);
            verifiedStub = X64InvlpgStubPlan.ValidateReadback(_invlpgPage, TransitionCodeStart, X64InvlpgStubPlan.PageSize, emitted);
            X64InvlpgInvocationRequest request = X64InvlpgInvocationRequest.Create(verifiedStub.EntryAddress, records[0], codeProtection, 1);
            invocation = X64InvlpgStubPlan.CreateInvocationPlan(verifiedStub, request);
        }

        public void Dispose()
        {
            PageTables?.Dispose();
            byte* probe = _probePage;
            byte* invlpg = _invlpgPage;
            _probePage = null;
            _invlpgPage = null;
            if (probe != null)
                NativeMemory.AlignedFree(probe);
            if (invlpg != null)
                NativeMemory.AlignedFree(invlpg);
        }

        private static X64ProtectionProbeEvent[] CreateEvents()
        {
            X64ProtectionProbeEvent[] events = new X64ProtectionProbeEvent[X64ProtectionProbeStubPlan.RequiredEventCount];
            for (int index = 0; index < events.Length; index++)
            {
                events[index] = new X64ProtectionProbeEvent((X64ProtectionProbeEventKind)(index + 1), (ulong)index);
            }

            return events;
        }

        private static void Fill(byte* destination, ulong length, byte value)
        {
            for (ulong index = 0; index < length; index++)
                destination[index] = value;
        }
    }
}
