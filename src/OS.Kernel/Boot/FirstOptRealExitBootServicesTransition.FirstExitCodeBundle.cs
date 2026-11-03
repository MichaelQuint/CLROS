using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
/// <summary>
/// Materialize and read back one three-page retained code allocation. Callers own
/// writable backing during preparation and must separately publish it RX, retain
/// its mappings and validate native stack budgets. No generated function is called
/// here. Keep the original arena/context and loaded image stable and exclusive.
/// </summary>

public static unsafe class FirstExitCodeBundle
{
    public const uint Bytes = 3 * 4096;
    public const uint FaultOffset = 4096;
    public const uint Out8Offset = 4352;
    public const uint In8Offset = 4368;
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE
#if M07_026_PROBE
    // Continuing networking also selects the legacy PCI/queue helpers at4608.
    public const uint ClockAccessOffset = 5600;
#else
    public const uint ClockAccessOffset = 4608;
#endif
#endif
#if M16_004_PROBE
    public const uint LinuxReadBackOffset = 4800;
#endif
#if M05_PCI_ENUMERATION_PROBE
#if M16_008_PROBE
    // The combined Linux/clock/APIC profile retains distinct helper bodies.
    public const uint PciReadOffset = 5504;
#else
    public const uint PciReadOffset = 4608;
#endif
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
#if M16_008_PROBE
    public const uint PciBarOffset = 5520;
#else
    public const uint PciBarOffset = 4624;
#endif
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
    public const uint QueueFenceOffset = 4672;
#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE
    public const uint MappingPublishOffset = 4608;
    public const uint MappingReadMsrOffset = 4800;
    public const uint MappingWritePatOffset = 4832;
    public const uint MappingReadCapabilitiesOffset = 5120;
#endif
#if M16_006_PROBE
    // Clock occupies 4608..4703; Linux read-back occupies 4800..4839.
    public const uint ApicObservationOffset = 4848;
#elif M05_APIC_SETUP_PROBE
    public const uint ApicObservationOffset = 4688;
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
    public const uint MsixInterruptOffset = 4784;
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
    public const uint CooperativeObserveOffset = 6000, CooperativeFxOffset = 6512;
    public const uint CooperativeSegmentsOffset = 6544, CooperativePublishOffset = 6720;
#endif
    public const uint StackEntryOffset = 8192;
    public const uint PermanentOffset = 8448;
    private const ulong LowEnd = 0x0000800000000000;
    public static bool Materialize(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback)
    {
        if (!Admit(code, capacity, ref arena, capture, mapping, callback, 0))
            return false;
        for (uint index = 0; index < Bytes; index++)
            code[index] = 0;
        bool emitted = NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(code, 4096, (ulong)code, (ulong)code + FaultOffset).Succeeded && FirstExitTerminalFaultStub.Emit(code + FaultOffset, Out8Offset - FaultOffset, (ulong)arena.Fault, callback, out _) && PortIoStubPlan.Out8.Emit(code + Out8Offset, PortIoStubPlan.Out8RequiredByteLength).Succeeded && PortIoStubPlan.In8.Emit(code + In8Offset, PortIoStubPlan.In8RequiredByteLength).Succeeded && FirstExitOwnedStackStub.Emit(code + StackEntryOffset, PermanentOffset - StackEntryOffset, out _) && FirstExitPermanentCr3Stub.Emit(code + PermanentOffset, Bytes - PermanentOffset, out _) &&
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE
            X64OwnedClockAccess.Emit(code + ClockAccessOffset, StackEntryOffset - ClockAccessOffset) &&
#endif
#if M16_004_PROBE
            X64LinuxElfReadBackBridge.Emit(code + LinuxReadBackOffset, StackEntryOffset - LinuxReadBackOffset) &&
#endif
#if M05_PCI_ENUMERATION_PROBE
            OS.Drivers.X64PciConfigurationRead.Emit(code + PciReadOffset, StackEntryOffset - PciReadOffset) &&
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
            OS.Drivers.X64PciBarAccess.Emit(code + PciBarOffset, StackEntryOffset - PciBarOffset) &&
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
            OS.Drivers.X64VirtqueueMemoryFence.Emit(code + QueueFenceOffset, StackEntryOffset - QueueFenceOffset) &&
#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE
            X64PhysicalMappingPublication.Emit(code + MappingPublishOffset, MappingReadMsrOffset - MappingPublishOffset) &&
            X64PhysicalMappingPublication.EmitReadMsr(code + MappingReadMsrOffset, MappingWritePatOffset - MappingReadMsrOffset) &&
            X64PhysicalMappingPublication.EmitWritePat(code + MappingWritePatOffset, MappingReadCapabilitiesOffset - MappingWritePatOffset) &&
            X64PhysicalMappingPublication.EmitReadCapabilities(code + MappingReadCapabilitiesOffset, StackEntryOffset - MappingReadCapabilitiesOffset) &&
#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE
            OS.Drivers.X64ApicObservation.Emit(code + ApicObservationOffset, StackEntryOffset - ApicObservationOffset) &&
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
            OS.Drivers.X64MsixInterrupt.Emit(code + MsixInterruptOffset, StackEntryOffset - MsixInterruptOffset) &&
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
            OS.Kernel.Threading.ThreadContextSwitch.EmitObserver(code + CooperativeObserveOffset,
                OS.Kernel.Threading.ThreadContextSwitch.ObserverBytes) &&
            OS.Kernel.Threading.ThreadContextSwitch.EmitFxSnapshot(code + CooperativeFxOffset,
                OS.Kernel.Threading.ThreadContextSwitch.FxSnapshotBytes) &&
            OS.Kernel.Threading.ThreadContextSwitch.EmitOwnedSegments(code + CooperativeSegmentsOffset,
                OS.Kernel.Threading.ThreadContextSwitch.OwnedSegmentsBytes) &&
            X64PhysicalMappingPublication.EmitGuardPublication(code + CooperativePublishOffset, StackEntryOffset - CooperativePublishOffset) &&
#endif
        CheckBytes(code, (ulong)arena.Fault, callback);
        if (!emitted)
        {
            arena.Fault->Phase = 4;
            return false;
        }

        // Only the actual read-back-checked canonical port stub bytes authorize
        // this binding. Publication/execution authority is still the adapter's job.
        var binding = InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(CheckedPortIoFunctionPointerBindingRequest.Create(true, Com1OwnedPortIoBackend.BasePort, (nuint)(code + Out8Offset), (nuint)(code + In8Offset + PortIoStubPlan.In8RequiredByteLength), PortIoStubPlan.Out8RequiredByteLength + PortIoStubPlan.In8RequiredByteLength, 0, In8Offset - Out8Offset, (nuint)(code + Out8Offset), (nuint)(code + In8Offset), PortIoStubPlan.Out8RequiredByteLength, PortIoStubPlan.In8RequiredByteLength, PortIoStubPlan.Out8RequiredByteLength, PortIoStubPlan.In8RequiredByteLength));
        if (!FirstExitTerminalFaultStub.Prepare(ref *arena.Fault, new Com1OwnedPortIoBackend(binding)))
        {
            arena.Fault->Phase = 4;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check the complete byte layout before publication/entry, including padding
    /// and embedded context/callback addresses. Original data must remain unchanged;
    /// this is not an allocation certificate or a live permission observation.
    /// </summary>

    public static bool ValidateReadBack(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback) => Admit(code, capacity, ref arena, capture, mapping, callback, 1) && arena.Fault->Backend.IsReady && CheckBytes(code, (ulong)arena.Fault, callback);
    public static bool ValidateInstalledReadBack(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback) => ValidateInstalledReadBack(code, capacity, ref arena, capture, mapping, callback, arena.StackTop, 0);
    /// <summary>Read back the installed code and original descriptor storage
    /// while an IRQ-off owner borrows exactly TSS.RSP0 and IST4. Expected stack
    /// tops come from the authenticated owner, never from the fields under test.
    /// The caller retains both stacks and proves their mappings, active tables
    /// and borrow lifetime. All other bytes retain their original checks. False
    /// changes nothing; this supplies no execution or restoration authority.</summary>

    public static bool ValidateInstalledReadBack(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback, ulong expectedRsp0, ulong expectedIst4) => Admit(code, capacity, ref arena, capture, mapping, callback, 1, true, expectedRsp0, expectedIst4) && arena.Fault->Backend.IsReady && CheckBytes(code, (ulong)arena.Fault, callback);
    private static bool Admit(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback, uint phase, bool installed = false, ulong expectedRsp0 = 0, ulong expectedIst4 = 0)
    {
        ulong address = (ulong)code, data = (ulong)arena.Storage;
        if (capacity != Bytes || address < 4096 || (address & 4095) != 0 || address >= LowEnd - Bytes || !arena.IsReady || data < 4096 || (data & 4095) != 0 || data >= LowEnd - FirstExitOwnedArena.Bytes || FirstExitDescriptors.Overlaps(address, Bytes, data, FirstExitOwnedArena.Bytes))
            return false;
        fixed (FirstExitOwnedArenaState* state = &arena)
            if (FirstExitDescriptors.Overlaps(address, Bytes, (ulong)state, (ulong)sizeof(FirstExitOwnedArenaState)) || FirstExitDescriptors.Overlaps(data, FirstExitOwnedArena.Bytes, (ulong)state, (ulong)sizeof(FirstExitOwnedArenaState)))
                return false;
        SeparatePageFaultMappingRange* imageRanges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitImageMappings.MaximumSections + 1];
        if (!FirstExitImageMappings.WriteRanges(capture, mapping, imageRanges, FirstExitImageMappings.MaximumSections + 1, out ulong count) || !FirstExitImageMappings.Contains(imageRanges, count, callback, 1, PageProtection.Read | PageProtection.Execute) || FirstExitDescriptors.Overlaps(address, Bytes, imageRanges[0].Start, capture.Range.ImageSize) || FirstExitDescriptors.Overlaps(data, FirstExitOwnedArena.Bytes, imageRanges[0].Start, capture.Range.ImageSize) || !(installed ? FirstExitOwnedArena.ValidateInstalledDescriptors(ref arena, address, expectedRsp0, expectedIst4) : FirstExitOwnedArena.ValidateDescriptors(ref arena, address)) || arena.Fault->Phase != phase || arena.Fault->Reserved != 0)
            return false;
        return FirstExitTerminalFaultStub.RequiredBytes > 0 && FirstExitTerminalFaultStub.RequiredBytes <= Out8Offset - FaultOffset && FirstExitOwnedStackStub.RequiredBytes > 0 && FirstExitOwnedStackStub.RequiredBytes <= PermanentOffset - StackEntryOffset && FirstExitPermanentCr3Stub.RequiredBytes > 0 && FirstExitPermanentCr3Stub.RequiredBytes <= 1024
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
            && OS.Kernel.Threading.ThreadContextSwitch.ObserverBytes > 0 &&
                OS.Kernel.Threading.ThreadContextSwitch.ObserverBytes <= CooperativeFxOffset - CooperativeObserveOffset
            && OS.Kernel.Threading.ThreadContextSwitch.FxSnapshotBytes > 0 &&
                OS.Kernel.Threading.ThreadContextSwitch.FxSnapshotBytes <= CooperativeSegmentsOffset - CooperativeFxOffset
            && OS.Kernel.Threading.ThreadContextSwitch.OwnedSegmentsBytes > 0 &&
                OS.Kernel.Threading.ThreadContextSwitch.OwnedSegmentsBytes <= CooperativePublishOffset - CooperativeSegmentsOffset
            && X64PhysicalMappingPublication.GuardRequiredBytes > 0 &&
                X64PhysicalMappingPublication.GuardRequiredBytes <= StackEntryOffset - CooperativePublishOffset
#endif
        ;
    }

    private static bool CheckBytes(byte* code, ulong context, ulong callback)
    {
        if (!NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(code, 4096, (ulong)code, (ulong)code + FaultOffset).Succeeded)
            return false;
        byte* scratch = stackalloc byte[1024];
        if (!FirstExitTerminalFaultStub.Emit(scratch, 1024, context, callback, out uint faultBytes) || !Equal(scratch, code + FaultOffset, faultBytes) || !PortIoStubPlan.Out8.Emit(scratch, PortIoStubPlan.Out8RequiredByteLength).Succeeded || !Equal(scratch, code + Out8Offset, PortIoStubPlan.Out8RequiredByteLength) || !PortIoStubPlan.In8.Emit(scratch, PortIoStubPlan.In8RequiredByteLength).Succeeded || !Equal(scratch, code + In8Offset, PortIoStubPlan.In8RequiredByteLength) || !FirstExitOwnedStackStub.Emit(scratch, 1024, out uint entryBytes) || !Equal(scratch, code + StackEntryOffset, entryBytes) || !FirstExitPermanentCr3Stub.Emit(scratch, 1024, out uint permanentBytes) || !Equal(scratch, code + PermanentOffset, permanentBytes))
            return false;
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE
        if (!X64OwnedClockAccess.Validate(code + ClockAccessOffset, X64OwnedClockAccess.Bytes))
            return false;
#endif
#if M16_004_PROBE
        if (!X64LinuxElfReadBackBridge.Validate(code + LinuxReadBackOffset, X64LinuxElfReadBackBridge.Bytes))
            return false;
#endif
#if M05_PCI_ENUMERATION_PROBE
        if (!OS.Drivers.X64PciConfigurationRead.Validate(code + PciReadOffset, OS.Drivers.X64PciConfigurationRead.Bytes))
            return false;
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        if (!OS.Drivers.X64PciBarAccess.Validate(code + PciBarOffset, OS.Drivers.X64PciBarAccess.Bytes))
            return false;
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        if (!OS.Drivers.X64VirtqueueMemoryFence.Validate(code + QueueFenceOffset, OS.Drivers.X64VirtqueueMemoryFence.Bytes))
            return false;
#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE
        if (!X64PhysicalMappingPublication.ValidateReadBack(code + MappingPublishOffset, X64PhysicalMappingPublication.RequiredBytes) ||
            !X64PhysicalMappingPublication.EmitReadMsr(scratch, 1024) ||
            !Equal(scratch, code + MappingReadMsrOffset, X64PhysicalMappingPublication.ReadMsrBytes) ||
            !X64PhysicalMappingPublication.ValidateWritePatReadBack(code + MappingWritePatOffset, X64PhysicalMappingPublication.WritePatBytes) ||
            !X64PhysicalMappingPublication.EmitReadCapabilities(scratch, 1024) ||
            !Equal(scratch, code + MappingReadCapabilitiesOffset, X64PhysicalMappingPublication.ReadCapabilitiesBytes))
            return false;
#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE
        if (!OS.Drivers.X64ApicObservation.Validate(code + ApicObservationOffset, OS.Drivers.X64ApicObservation.Bytes))
            return false;
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
        if (!OS.Drivers.X64MsixInterrupt.Validate(code + MsixInterruptOffset, OS.Drivers.X64MsixInterrupt.Bytes))
            return false;
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
        if (!OS.Kernel.Threading.ThreadContextSwitch.EmitObserver(scratch, OS.Kernel.Threading.ThreadContextSwitch.ObserverBytes) ||
            !Equal(scratch, code + CooperativeObserveOffset, OS.Kernel.Threading.ThreadContextSwitch.ObserverBytes) ||
            !OS.Kernel.Threading.ThreadContextSwitch.EmitFxSnapshot(scratch, OS.Kernel.Threading.ThreadContextSwitch.FxSnapshotBytes) ||
            !Equal(scratch, code + CooperativeFxOffset, OS.Kernel.Threading.ThreadContextSwitch.FxSnapshotBytes) ||
            !OS.Kernel.Threading.ThreadContextSwitch.EmitOwnedSegments(scratch, OS.Kernel.Threading.ThreadContextSwitch.OwnedSegmentsBytes) ||
            !Equal(scratch, code + CooperativeSegmentsOffset, OS.Kernel.Threading.ThreadContextSwitch.OwnedSegmentsBytes) ||
            !X64PhysicalMappingPublication.ValidateGuardPublication(code + CooperativePublishOffset, X64PhysicalMappingPublication.GuardRequiredBytes))
            return false;
#endif
        // Most retained bytes are zero padding. Only nonzero bytes need the
        // export-membership checks; each exported body was checked above.
        for (uint index = 4096; index < Bytes; index++)
            if (code[index] != 0 && !Inside(index, FaultOffset, faultBytes) && !Inside(index, Out8Offset, PortIoStubPlan.Out8RequiredByteLength) && !Inside(index, In8Offset, PortIoStubPlan.In8RequiredByteLength) && !Inside(index, StackEntryOffset, entryBytes) &&
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE
                !Inside(index, ClockAccessOffset, X64OwnedClockAccess.Bytes) &&
#endif
#if M16_004_PROBE
                !Inside(index, LinuxReadBackOffset, X64LinuxElfReadBackBridge.Bytes) &&
#endif
#if M05_PCI_ENUMERATION_PROBE
                !Inside(index, PciReadOffset, OS.Drivers.X64PciConfigurationRead.Bytes) &&
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
                !Inside(index, PciBarOffset, OS.Drivers.X64PciBarAccess.Bytes) &&
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
                !Inside(index, QueueFenceOffset, OS.Drivers.X64VirtqueueMemoryFence.Bytes) &&
#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE
                !Inside(index, MappingPublishOffset, X64PhysicalMappingPublication.RequiredBytes) &&
                !Inside(index, MappingReadMsrOffset, X64PhysicalMappingPublication.ReadMsrBytes) &&
                !Inside(index, MappingWritePatOffset, X64PhysicalMappingPublication.WritePatBytes) &&
                !Inside(index, MappingReadCapabilitiesOffset, X64PhysicalMappingPublication.ReadCapabilitiesBytes) &&
#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE
                !Inside(index, ApicObservationOffset, OS.Drivers.X64ApicObservation.Bytes) &&
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
                !Inside(index, MsixInterruptOffset, OS.Drivers.X64MsixInterrupt.Bytes) &&
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
                !Inside(index, CooperativeObserveOffset, OS.Kernel.Threading.ThreadContextSwitch.ObserverBytes) &&
                !Inside(index, CooperativeFxOffset, OS.Kernel.Threading.ThreadContextSwitch.FxSnapshotBytes) &&
                !Inside(index, CooperativeSegmentsOffset, OS.Kernel.Threading.ThreadContextSwitch.OwnedSegmentsBytes) &&
                !Inside(index, CooperativePublishOffset, X64PhysicalMappingPublication.GuardRequiredBytes) &&
#endif
            !Inside(index, PermanentOffset, permanentBytes))
                return false;
        return true;
    }

    private static bool Inside(uint offset, uint start, uint bytes) => offset >= start && offset - start < bytes;
    private static bool Equal(byte* left, byte* right, uint bytes)
    {
        for (uint index = 0; index < bytes; index++)
            if (left[index] != right[index])
                return false;
        return true;
    }
}
