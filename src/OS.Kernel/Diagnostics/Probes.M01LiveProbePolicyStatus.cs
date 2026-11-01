namespace OS.Kernel.Diagnostics;
#if M16_027_TIMING_PROBE

#else
#endif
#if M16_027_PROBE

#else
#endif
#if M16_023_PROBE

#else
#endif
#if M16_024_PROBE

#else
#endif
#if M16_025_PROBE

#else
#endif
#if M10_018_TIMING_PROBE

#else
#endif
#if M10_018_FULL_PROBE

#else
#endif
#if M10_018_PROBE

#else
#endif
#if M10_017_PROBE

#else
#endif
#if M16_019_UNAVAILABLE_PROBE

#else
#endif
#if M16_019_INVALID_PROBE

#else
#endif
#if M10_004_PROBE

#else
#endif
#if M16_011_PROBE

#else
#endif
#if M16_012_PROBE

#else
#endif
#if M16_013_PROBE

#else
#endif
#if M16_014_PROBE

#else
#endif
#if M09_012_PROBE

#else
#endif
#if M09_011_PROBE

#else
#endif
#if M09_010_PROBE

#else
#endif
#if M09_009_PROBE

#else
#endif
#if M09_006_PROBE

#else
#endif
#if M09_005_PROBE

#else
#endif
#if M09_004_PROBE

#else
#endif
#if M16_010_PROBE

#else
#endif
#if M16_009_PROBE

#else
#endif
#if M16_008_PROBE

#else
#endif
#if M16_007_PROBE

#else
#endif
#if M16_006_PROBE

#else
#endif
#if M16_005_PROBE

#else
#endif
#if M16_004_PROBE

#else
#endif
#if M16_003_PROBE

#else
#endif
#if M16_002_PROBE

#else
#endif
#if M04_SEEDED_COLLECTOR_CHURN_PROBE

#else
#endif
#if M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE

#else
#endif
#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE

#else
#endif
#if M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE

#else
#endif
#if M03_OWNED_SUBSYSTEM_PANIC_PROBE

#else
#endif
#if M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE

#else
#endif
#if M04_INTERFACE_DISPATCH_PROBE

#else
#endif
#if M04_X64_UNWIND_METADATA_PROBE

#else
#endif
#if M04_SIMPLE_THROW_CATCH_PROBE

#else
#endif
#if M04_SINGLE_FINALLY_CATCH_PROBE

#else
#endif
#if M04_RETHROW_PROBE

#else
#endif
#if M04_NESTED_FINALLY_CATCH_PROBE

#else
#endif
#if M04_UNHANDLED_EXCEPTION_PROBE

#else
#endif
#if M04_FRAME_DISPATCH_PROBE

#else
#endif
#if M04_NORMAL_FINALLY_PROBE

#else
#endif
#if M04_EH_CATALOG_PROBE

#else
#endif
#if M04_CONSTRUCTOR_FAILURE_PROBE

#else
#endif
#if M04_FILTERED_CATCH_PROBE

#else
#endif
#if M05_OWNED_ACPI_SELECTION_PROBE

#else
#endif
#if M05_PHYSICAL_MAPPING_PROBE

#else
#endif
#if M05_MONOTONIC_CLOCK_PROBE

#else
#endif
#if M05_PCI_ENUMERATION_PROBE

#else
#endif
#if M05_BAR_DMA_PROBE

#else
#endif
#if M05_APIC_SETUP_PROBE

#else
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE

#else
#endif
#if M05_VIRTQUEUE_POLLING_PROBE

#else
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE

#else
#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#else
#endif
#if M06_VIRTIO_BLOCK_READ_PROBE

#else
#endif
#if M06_FAT_READ_PROBE

#else
#endif
#if M06_PACKAGE_READ_PROBE

#else
#endif
#if M06_BLOCK_MSIX_PROBE

#else
#endif
#if M06_BLOCK_MSIX_POLLING_RECOVERY_PROBE

#else
#endif
#if M07_VIRTIO_NET_PROBE

#else
#endif
#if M07_026_PROBE

#else
#endif
#if M07_027_PROBE

#else
#endif
#if M07_028_PROBE

#else
#endif
#if M07_ARP_RECEIVE_PROBE

#else
#endif
#if M06_GPT_PARTITION_PROBE

#else
#endif
#if M06_LIVE_BLOCK_CONTRACT_PROBE

#else
#endif
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
public enum M01LiveProbePolicyStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    LiveAllocatorRequiresLoadedImageStack = 2,
    OwnedPagingRequiresLiveAllocator = 3
}
#endif
