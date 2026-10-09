namespace OS.Tooling;
public static partial class EfiContracts
{
    public static IReadOnlyDictionary<string, string> SourceAdmissionReasons { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["..\\OS.Kernel\\Boot\\VerifiedExitPathOptDefaultRepeatedCleanQEMU.cs"] = "reviewed_normal_exit_policy_identity",
        ["..\\OS.Kernel\\Runtime\\ObjectArrayStringEETypeMethodTableLayoutContracts.cs"] = "reviewed_layout_source_identity",
        ["..\\OS.Kernel\\Runtime\\ManagedHeapPageRangeLiveAllocator.cs"] = "reviewed_heap_source_identity",
        ["..\\OS.Kernel\\Runtime\\GuestTestRhpNewFast.cs"] = "reviewed_rhp_new_fast_source_identity",
        ["..\\OS.Kernel\\Boot\\ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.cs"] = "reviewed_retained_authority_source_identity",
        ["..\\OS.Kernel\\Runtime\\CompilerClassConstructorExecution.cs"] = "reviewed_retained_authority_source_identity",
        ["..\\OS.Kernel\\Runtime\\CompilerInterfaceDispatch.cs"] = "reviewed_dispatch_source_identity",
        ["..\\OS.Kernel\\Runtime\\ExceptionTypeMatch.cs"] = "reviewed_exception_leaf_source_identity",
        ["..\\OS.Kernel\\Runtime\\X64FrameUnwind.cs"] = "reviewed_exception_leaf_source_identity",
        ["..\\OS.Kernel\\Runtime\\NativeAotEhClauseMetadata.cs"] = "reviewed_exception_leaf_source_identity",
        ["..\\OS.Kernel\\Runtime\\X64PERuntimeFunctionUnwindMetadataHostileInput.cs"] = "reviewed_x64_unwind_metadata_source_identity",
        ["..\\OS.Kernel\\Diagnostics\\Probes.cs"] = "reviewed_first_exit_probe_identity",
        ["..\\OS.Kernel\\Boot\\OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput.cs"] = "reviewed_owned_allocator_identity",
        ["..\\OS.Kernel\\Platform\\PostFirmwarePhysicalMappingExplicitUCWCWB.cs"] = "mapping_source_identity",
        ["..\\OS.Kernel\\Boot\\FirstOptRealExitBootServicesTransition.cs"] = "reviewed_first_exit_source_identity",
        ["..\\..\\std\\no-runtime\\shared\\Hash\\Crc32.cs"] = "reviewed_first_exit_source_identity",
        ["..\\..\\std\\no-runtime\\shared\\DeterministicRandom32.cs"] = "reviewed_seeded_churn_source_identity",
        ["..\\OS.Kernel\\Runtime\\SeededAllocationChurnCollectionRootRetentionGuestProbes.cs"] = "reviewed_seeded_churn_source_identity",
        ["Runtime\\SeededCollectorChurnProbe.cs"] = "reviewed_seeded_churn_source_identity",
        ["..\\OS.Kernel\\Diagnostics\\SeparateDisabledDefaultPageFaultPageTables.cs"] = "reviewed_page_fault_tables_source_identity",
        ["..\\OS.Kernel\\Runtime\\TypeManagerModuleMetadataLiveNativeAOTImage.cs"] = "reviewed_type_manager_source_identity",
        ["..\\OS.Kernel\\Runtime\\GCStaticRootsMalformedMetadata.cs"] = "reviewed_gc_static_source_identity",
        ["..\\OS.Kernel\\Runtime\\BoundedNonMovingCollectorExplicitRootEnumeration.cs"] = "reviewed_bounded_collector_source_identity",
        ["..\\OS.Kernel\\Boot\\TypedNoStdlibExitBootServicesCallAdapter.cs"] = "reviewed_typed_ebs_adapter_source_identity",
        ["..\\OS.Kernel\\Boot\\NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs"] = "reviewed_final_map_window_source_identity",
        ["..\\OS.Kernel\\Boot\\BoundedRetryStaleKeyStatus.cs"] = "reviewed_bounded_ebs_retry_source_identity",
    });
}
