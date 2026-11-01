using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
// Original image-resident objects and callback entries supplied by the adapter.
// Coverage does not establish their provenance, readiness or native stack use.
public struct FirstExitImageReferences
{
#if M16_025_PROBE
    public ulong NativeDiskBarStart, NativeDiskArenaStart, NativeDiskArenaPages;
#endif
    public ulong EntryCallback, DiagnosticCallback, FaultCallback;
    public ulong AdapterState, AdapterStateBytes;
    public ulong AllocatorState, AllocatorStateBytes;
    public ulong CopiedAcpi, CopiedAcpiBytes;
    // Optional in host-only compositions. The EFI adapter must retain its
    // original installed NativeAOT stack-probe page, outside the loaded image.
    public ulong RuntimeSupportCode, RuntimeSupportCodeBytes;
#if M07_026_PROBE
    public ulong NetworkArenaStart, NetworkArenaPages;
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
    public ulong CooperativePoolStart, CooperativePoolPages;
#endif
#if M16_002_PROBE
    public ulong LinuxPackageStart, LinuxPackagePages;
#endif
#if M16_004_PROBE
    public ulong LinuxBackingStart, LinuxBackingPages, LinuxUserTablesStart, LinuxUserTablePages;
#endif
#if M16_005_PROBE
    public ulong LinuxSyscallCodeStart, LinuxSyscallDataStart;
    public ulong LinuxMemoryPoolStart, LinuxMemoryPoolPages, LinuxMemoryCandidateStart;
    public ulong LinuxDescriptorStart, LinuxDescriptorPages;
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
    // Original retained ACPI HPET GAS, admitted against the owned firmware map.
    public ulong HpetDevicePage;
#endif
#if M05_PCI_ENUMERATION_PROBE
    public ulong EcamStart, EcamPages;
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
    public ulong VirtioBarStart;
#endif
#if M05_APIC_SETUP_PROBE
    public ulong LocalApicPage, IoApicPage;
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
    public ulong MsixPage;
#endif
#if M05_PHYSICAL_MAPPING_PROBE
    // Original EFI allocation containing one exclusive aligned 2 MiB test span.
    public ulong MappingScratchBacking, MappingScratchBackingPages, MappingScratchStart;
#endif
}
#if M16_025_PROBE

#endif
#if M07_026_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M16_002_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_005_PROBE

#endif
#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#endif
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M16_025_PROBE

#endif
#if M07_026_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M16_002_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_005_PROBE

#endif
#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M10_017_PROBE || M10_018_PROBE || M16_027_PROBE

#else
#endif
#if M16_002_PROBE




#endif
#if M16_004_PROBE








#endif
#if M05_PHYSICAL_MAPPING_PROBE








#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE



#endif
#if M05_PCI_ENUMERATION_PROBE




#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE





#if M16_025_PROBE






#endif
#endif
#if M05_APIC_SETUP_PROBE




#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE


#endif
#if M16_005_PROBE








#endif
#if M16_006_PROBE







#endif
#if M16_007_PROBE




#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
#if M16_008_PROBE

#else

#endif





#endif
#if M07_026_PROBE



#endif
#if M07_026_PROBE

#if M16_025_PROBE



#endif
#endif
#if M16_002_PROBE

#endif
#if M16_004_PROBE


#endif
#if M16_005_PROBE




#endif
#if M16_006_PROBE








#endif
#if M16_007_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE


#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE




#if M16_025_PROBE



#else

#endif
#else

#endif
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE


#endif
#if M05_APIC_SETUP_PROBE


#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE
#if M07_026_PROBE


#else

#endif
#endif
#if M16_004_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE
#if M16_008_PROBE


#else

#endif
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
#if M16_008_PROBE

#else

#endif
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE




#endif
#if M16_006_PROBE


#elif M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE


#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE

#endif
#if M16_004_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE




#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE







#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE








#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE

#endif
#if M16_004_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE






#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE







#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE

#endif
#if M16_004_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE




#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE




#endif
