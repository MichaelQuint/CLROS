namespace OS.Tooling;

/// <summary>One reviewed diagnostic selection contract shared by build, verifier and guest runners.</summary>
public sealed class EfiProfile
{
    private readonly Dictionary<string, bool> flags;
    public string BootPolicy { get; }
    public IReadOnlyDictionary<string, bool> Flags => flags;
    public bool IsEnabled(string name) => flags.TryGetValue(Full(name), out bool enabled) && enabled;
    private static string Full(string name) => name.StartsWith("Enable", StringComparison.Ordinal) ? name : "Enable" + name + "Probe";
    private EfiProfile(string bootPolicy, Dictionary<string, bool> flags) { BootPolicy = bootPolicy; this.flags = flags; }
    public IEnumerable<string> CommandArguments() => new[] { "--boot-policy", BootPolicy }.Concat(flags.Where(p => p.Value).Select(p => "--" + p.Key));

    public static EfiProfile Create(string? bootPolicy, IReadOnlyDictionary<string, bool> explicitlySelected, Action<string>? emit = null)
    {
        Dictionary<string, bool> flags = EfiContracts.DiagnosticProbes.ToDictionary(p => p, _ => false, StringComparer.Ordinal);
        foreach (var pair in explicitlySelected)
        {
            if (!flags.ContainsKey(pair.Key)) throw new ToolFailure("diagnostic_probe_unknown", "property=" + pair.Key);
            flags[pair.Key] = pair.Value;
        }
        if (flags["EnableM16002Probe"])
        {
            string[] required = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe"];
            foreach (string name in required)
            {
                if (explicitlySelected.TryGetValue(name, out bool enabled) && !enabled) throw new ToolFailure("m16_002_explicit_false_prerequisite", name);
                flags[name] = true;
            }
            foreach (var flag in flags) if (flag.Value && flag.Key != "EnableM16002Probe" && !required.Contains(flag.Key)) throw new ToolFailure("m16_002_profile_conflict", flag.Key);
        }
        bool diagnostic = flags.Values.Any(v => v);
        if (string.IsNullOrEmpty(bootPolicy)) bootPolicy = diagnostic ? "Diagnostic" : "VerifiedExit";
        if (!bootPolicy.Equals("VerifiedExit", StringComparison.OrdinalIgnoreCase) && !bootPolicy.Equals("Diagnostic", StringComparison.OrdinalIgnoreCase)) throw new ToolFailure("clros_boot_policy_invalid");
        bootPolicy = bootPolicy.Equals("VerifiedExit", StringComparison.OrdinalIgnoreCase) ? "VerifiedExit" : "Diagnostic";
        if (bootPolicy == "VerifiedExit" && diagnostic) throw new ToolFailure("clros_boot_policy_probe_conflict");
        EfiProfile profile = new(bootPolicy, flags);
        profile.Validate(explicitlySelected, emit);
        return profile;
    }

    private void Need(string trigger, string requirement, string reason) { if (IsEnabled(trigger) && !IsEnabled(requirement)) throw new ToolFailure(reason); }
    private void Conflict(string trigger, string conflict, string reason) { if (IsEnabled(trigger) && IsEnabled(conflict)) throw new ToolFailure(reason); }
    private void Set(string trigger, string[] names, bool required, string reason)
    {
        if (!IsEnabled(trigger)) return;
        foreach (string name in names) if (IsEnabled(name) != required) throw new ToolFailure(reason, "property=" + Full(name));
    }
    private static readonly string[] HeapChain = ["M01LoadedImageStack", "M01LiveAllocator", "M04IndependentEndianConformance", "M04SpanOverlapMemoryCastUnsafeWidthConformance", "M04ArithmeticConformance", "M04LayoutContract", "M04ManagedHeapPageRange", "M04RhpNewFast"];
    private static readonly string[] FaultChain = ["M01LoadedImageStack", "M01LiveAllocator", "M01OwnedPaging", "M02SafeStub", "M02CheckedPortIoBinding", "M02Com1OwnedPortIo", "M02OwnedCom1Ready", "M02PanicSafeOwnedCom1"];
    private static readonly string[] DescriptorProfiles = ["M02OwnedX64GdtEncoding", "M02GuardedIstStackAllocation", "M02OwnedGdtTssTerminal", "M02NormalizedInterruptFramePage", "M02OwnedIdtGatePopulation", "M02IdtrContinuedExecution"];
    private static readonly string[] FaultProfiles = ["M02Ud2VectorState", "M02PageFaultCr2ErrorCode", "M02DoubleFaultIst", "M02UnexpectedVectorPanic"];
    private static readonly string[] OptionalFirmware = ["UefiGoneSimulation", "ExitBootServicesFreshMap", "AcpiPhysicalDiscovery"];
    private static readonly string[] AllocationProfiles = ["M04RhpNewFast", "M04RhpNewFastBeforeBindFailure", "M04RhpNewFastExhaustionFailure", "M04SzArrayAllocation", "M04SzArrayAllocationBeforeBind", "M04SzArrayAllocationExhaustion", "M04StringAllocation", "M04StringAllocationBeforeBind", "M04StringAllocationExhaustion", "M04StringAllocationInvalidLength"];
    private static readonly string[] HeapForbidden = [.. FaultChain.Skip(2), .. DescriptorProfiles, .. FaultProfiles, .. OptionalFirmware];

    private void Validate(IReadOnlyDictionary<string, bool> explicitlySelected, Action<string>? emit)
    {
        Need("M03FirmwareAdapterRevocation", "M03FirstRealExitBootServices", "m03_revocation_requires_first_exit");
        Need("M03OwnedSubsystemSurvival", "M03FirstRealExitBootServices", "m03_owned_requires_first_exit");
        Need("M03OwnedSubsystemPanic", "M03OwnedSubsystemSurvival", "m03_owned_panic_requires_survival");
        Conflict("M03OwnedSubsystemSurvival", "M03FirmwareAdapterRevocation", "m03_owned_revocation_conflict");
        Set("M05PhysicalMapping", ["M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival"], true, "m05_mapping_requires_owned_first_exit");
        Set("M05OwnedAcpiSelection", ["M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival"], true, "m05_acpi_requires_owned_first_exit");
        Set("M05OwnedAcpiSelection", ["M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation", "M05PhysicalMapping"], false, "m05_acpi_profile_conflict");
        Set("M05MonotonicClock", ["M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival"], true, "m05_clock_requires_owned_first_exit");
        Set("M05PciEnumeration", ["M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival"], true, "m05_pci_requires_owned_first_exit");
        Set("M06BlockMsix", ["M06PackageRead", "M05ApicSetup"], true, "m06_block_msix_requires_package_apic");
        Set("M06BlockMsix", ["M05VirtioNegotiation", "M05VirtqueuePolling", "M05VirtqueueInterrupt"], false, "m06_block_msix_profile_conflict");
        Need("M06BlockMsixPollingRecovery", "M06BlockMsix", "m06_block_msix_polling_recovery_requires_main");
        Need("M07VirtioNet", "M05VirtqueuePolling", "m07_virtio_net_requires_polling");
        Need("M07ArpReceive", "M07VirtioNet", "m07_arp_receive_requires_net");
        Set("M06PackageRead", ["M06FatRead"], true, "m06_package_read_requires_fat");
        Set("M06FatRead", ["M06GptPartition"], true, "m06_fat_read_requires_gpt");
        Set("M06GptPartition", ["M06LiveBlockContract"], true, "m06_gpt_partition_requires_contract");
        Set("M06LiveBlockContract", ["M06VirtioBlockRead"], true, "m06_live_block_contract_requires_read");
        Set("M06VirtioBlockRead", ["M06VirtioBlockQueueSetup"], true, "m06_block_read_requires_queue");
        Set("M06VirtioBlockQueueSetup", ["M05PciEnumeration"], true, "m06_block_queue_requires_pci");
        Set("M06VirtioBlockQueueSetup", ["M05BarDma", "M05VirtioNegotiation", "M05VirtqueuePolling", "M05VirtqueueInterrupt"], false, "m06_block_queue_profile_conflict");
        if (!IsEnabled("M06BlockMsix")) Conflict("M06VirtioBlockQueueSetup", "M05ApicSetup", "m06_block_queue_profile_conflict");
        Set("M05BarDma", ["M05PciEnumeration"], true, "m05_bar_dma_requires_pci");
        Set("M05ApicSetup", ["M05PciEnumeration"], true, "m05_apic_requires_pci");
        Set("M05ApicSetup", ["M05BarDma", "M05VirtqueuePolling"], false, "m05_apic_profile_conflict");
        if (IsEnabled("M05ApicSetup")) Need("M05VirtioNegotiation", "M05VirtqueueInterrupt", "m05_apic_negotiation_requires_interrupt");
        Set("M05VirtqueueInterrupt", ["M05ApicSetup", "M05VirtioNegotiation"], true, "m05_virtqueue_interrupt_requires_apic_negotiation");
        Conflict("M05VirtqueueInterrupt", "M05VirtqueuePolling", "m05_virtqueue_interrupt_polling_conflict");
        Set("M05VirtqueuePolling", ["M05VirtioNegotiation"], true, "m05_virtqueue_polling_requires_negotiation");
        Set("M05VirtioNegotiation", ["M05PciEnumeration"], true, "m05_virtio_negotiation_requires_pci");
        Conflict("M05BarDma", "M05VirtioNegotiation", "m05_bar_dma_profile_conflict");
        Conflict("M05VirtioNegotiation", "M05BarDma", "m05_virtio_negotiation_profile_conflict");
        Set("M05MonotonicClock", ["M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation", "M05PhysicalMapping", "M05OwnedAcpiSelection"], false, "m05_clock_profile_conflict");
        Set("M05PciEnumeration", ["M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation", "M05PhysicalMapping", "M05OwnedAcpiSelection", "M05MonotonicClock"], false, "m05_pci_profile_conflict");
        Set("M05PhysicalMapping", ["M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation"], false, "m05_mapping_profile_conflict");
        Need("M04ClassConstructorExecution", "M04GcStaticRootMaterialization", "m04_cctor_requires_gc_profile");
        Conflict("M04ClassConstructorExecution", "M04SeededCollectorChurn", "m04_cctor_churn_conflict");
        Need("M04InterfaceDispatch", "M04ClassConstructorExecution", "m04_dispatch_requires_cctor_profile");
        Need("M04X64UnwindMetadata", "M04TypeManagerModuleRegistration", "m04_x64_unwind_metadata_requires_original_module");
        Need("M04SimpleThrowCatch", "M04GcStaticRootMaterialization", "m04_simple_catch_requires_gc_profile");
        Need("M04SingleFinallyCatch", "M04GcStaticRootMaterialization", "m04_single_finally_requires_gc_profile");
        Need("M04Rethrow", "M04GcStaticRootMaterialization", "m04_rethrow_requires_gc_profile");
        Need("M04NestedFinallyCatch", "M04GcStaticRootMaterialization", "m04_nested_finally_requires_gc_profile");
        Need("M04FilteredCatch", "M04GcStaticRootMaterialization", "m04_filter_requires_gc_profile");
        Set("M04FilteredCatch", ["M04ConstructorFailure", "M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow", "M04NestedFinallyCatch", "M04UnhandledException", "M04FrameDispatch"], false, "m04_filter_profile_conflict");
        Need("M04ConstructorFailure", "M04GcStaticRootMaterialization", "m04_ctor_failure_requires_gc_profile");
        Set("M04ConstructorFailure", ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow", "M04NestedFinallyCatch", "M04UnhandledException", "M04FrameDispatch"], false, "m04_ctor_failure_profile_conflict");
        Need("M04NormalFinally", "M04GcStaticRootMaterialization", "m04_normal_finally_requires_gc_profile");
        Set("M04NormalFinally", ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow", "M04NestedFinallyCatch", "M04UnhandledException", "M04FrameDispatch", "M04ConstructorFailure", "M04FilteredCatch", "M04EhCatalog"], false, "m04_normal_finally_profile_conflict");
        Need("M04EhCatalog", "M04FrameDispatch", "m04_eh_catalog_requires_frame_dispatch");
        Need("M04FrameDispatch", "M04GcStaticRootMaterialization", "m04_frames_requires_gc_profile");
        Set("M04FrameDispatch", ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow", "M04NestedFinallyCatch", "M04UnhandledException"], false, "m04_frames_profile_conflict");
        Need("M04UnhandledException", "M04GcStaticRootMaterialization", "m04_unhandled_requires_gc_profile");
        Set("M04UnhandledException", ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow", "M04NestedFinallyCatch"], false, "m04_unhandled_profile_conflict");
        Set("M04NestedFinallyCatch", ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow"], false, "m04_nested_finally_profile_conflict");
        Set("M04Rethrow", ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch"], false, "m04_rethrow_profile_conflict");
        Set("M04SimpleThrowCatch", ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata"], false, "m04_simple_catch_profile_conflict");
        Set("M04SingleFinallyCatch", ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata", "M04SimpleThrowCatch"], false, "m04_single_finally_profile_conflict");
        Need("M04SeededCollectorChurn", "M04GcStaticRootMaterialization", "m04_churn_requires_gc_profile");
        Set("M04GcStaticRootMaterialization", HeapChain, true, "m04_gc_static_profile_invalid");
        Set("M04GcStaticRootMaterialization", [.. HeapForbidden, .. AllocationProfiles.Skip(1), "M03MemoryMapCapture", "M04CheckedReferenceBarrier", "M03FirmwareEntropySeed", "M04TypeManagerModuleRegistration", "M03TypedExitBootServicesCall"], false, "m04_gc_static_profile_conflict");
        Set("M03FirstRealExitBootServices", ["M01LoadedImageStack", "M01LiveAllocator", "AcpiPhysicalDiscovery"], true, "m03_first_exit_requires");
        Set("M03FirstRealExitBootServices", [.. HeapForbidden.Where(n => n != "AcpiPhysicalDiscovery"), .. HeapChain.Skip(2), .. AllocationProfiles, "M03MemoryMapCapture", "M04CheckedReferenceBarrier", "M03FirmwareEntropySeed", "M04TypeManagerModuleRegistration", "M03TypedExitBootServicesCall", "M04GcStaticRootMaterialization"], false, "m03_first_exit_profile_conflict");
        Set("M03TypedExitBootServicesCall", [.. HeapForbidden, .. HeapChain, .. AllocationProfiles, "M03MemoryMapCapture", "M04CheckedReferenceBarrier", "M03FirmwareEntropySeed", "M04TypeManagerModuleRegistration", "M04GcStaticRootMaterialization"], false, "m03_typed_ebs_profile_conflict");
        Set("M03FirmwareEntropySeed", [.. HeapForbidden, .. HeapChain, .. AllocationProfiles, "M03MemoryMapCapture", "M04CheckedReferenceBarrier"], false, "m03_firmware_entropy_profile_conflict");
        Need("M04TypeManagerModuleRegistration", "M01LoadedImageStack", "m04_type_manager_requires_original_loaded_image");
        Set("M04TypeManagerModuleRegistration", [.. HeapForbidden, .. HeapChain.Skip(1), .. AllocationProfiles, "M03MemoryMapCapture", "M04CheckedReferenceBarrier", "M03FirmwareEntropySeed"], false, "m04_type_manager_profile_conflict");
        Set("M04CheckedReferenceBarrier", HeapChain, true, "m04_checked_reference_profile_invalid");
        Set("M04CheckedReferenceBarrier", [.. HeapForbidden, .. AllocationProfiles.Skip(1), "M03MemoryMapCapture", "M03FirmwareEntropySeed", "M04TypeManagerModuleRegistration", "M04GcStaticRootMaterialization", "M03TypedExitBootServicesCall"], false, "m04_checked_reference_profile_conflict");
        Set("M03MemoryMapCapture", [.. HeapForbidden, .. HeapChain, .. AllocationProfiles], false, "m03_memory_map_capture_profile_conflict");
        string reason = "";
        Allocation("M04StringAllocation", "m04_string", ["M04StringAllocationBeforeBind", "M04StringAllocationExhaustion", "M04StringAllocationInvalidLength"], [.. HeapChain, "M04SzArrayAllocation"], [.. HeapForbidden, "M04RhpNewFastBeforeBindFailure", "M04RhpNewFastExhaustionFailure", "M04SzArrayAllocationBeforeBind", "M04SzArrayAllocationExhaustion"], ref reason);
        Allocation("M04SzArrayAllocation", "m04_sz_array", ["M04SzArrayAllocationBeforeBind", "M04SzArrayAllocationExhaustion"], ["M04RhpNewFast", .. HeapChain.Take(7)], [.. HeapForbidden, "M04RhpNewFastBeforeBindFailure", "M04RhpNewFastExhaustionFailure"], ref reason);
        Allocation("M04RhpNewFast", "m04_rhp_new_fast", ["M04RhpNewFastBeforeBindFailure", "M04RhpNewFastExhaustionFailure"], [.. HeapChain.Take(7)], HeapForbidden, ref reason);
        if (reason.Length != 0) throw new ToolFailure(reason);
        Set("M04ManagedHeapPageRange", [.. HeapChain.Take(6)], true, "m04_managed_heap_page_range_requires_explicit_chain");
        Set("M04ManagedHeapPageRange", [.. HeapForbidden.Except(["M02PageFaultCr2ErrorCode", "M02DoubleFaultIst", "M02UnexpectedVectorPanic"])], false, "m04_managed_heap_page_range_forbids_optional_profiles");
        Set("M04LayoutContract", ["M04ArithmeticConformance", "M04SpanOverlapMemoryCastUnsafeWidthConformance", "M04IndependentEndianConformance"], true, "m04_layout_contract_requires_explicit_m04_chain");
        foreach ((string trigger, string prefix) in new[] { ("M04LayoutContract", "m04_layout_contract"), ("M04ArithmeticConformance", "m04_arithmetic_conformance"), ("M04SpanOverlapMemoryCastUnsafeWidthConformance", "m04_span_memory_conformance") })
        {
            Conflict(trigger, "M02OwnedGdtTssTerminal", prefix + "_forbids_owned_gdt_tss_terminal");
            Conflict(trigger, "M02Ud2VectorState", prefix + "_forbids_m02_ud2_vector_state");
        }
        Need("M04ArithmeticConformance", "M04SpanOverlapMemoryCastUnsafeWidthConformance", "m04_arithmetic_conformance_requires_span_memory_conformance");
        Need("M04SpanOverlapMemoryCastUnsafeWidthConformance", "M04IndependentEndianConformance", "m04_span_memory_conformance_requires_independent_endian_conformance");
        Set("M02PageFaultCr2ErrorCode", ["M02DoubleFaultIst", "M02UnexpectedVectorPanic", .. DescriptorProfiles, "M02Ud2VectorState", .. HeapChain.Skip(3).Take(4)], false, "m02_page_fault_cr2_error_code_profile_conflict");
        Set("M02DoubleFaultIst", [.. OptionalFirmware, .. DescriptorProfiles, "M02Ud2VectorState", "M02PageFaultCr2ErrorCode", .. HeapChain.Skip(2), .. AllocationProfiles], false, "m02_double_fault_ist_profile_conflict");
        Set("M02UnexpectedVectorPanic", [.. DescriptorProfiles, "M02Ud2VectorState", "M02PageFaultCr2ErrorCode", "M02DoubleFaultIst", .. HeapChain.Skip(2), .. AllocationProfiles, .. OptionalFirmware], false, "m02_unexpected_vector_panic_profile_conflict");
        foreach ((string trigger, string prefix) in new[] { ("M02Ud2VectorState", "m02_ud2_vector_state"), ("M02PageFaultCr2ErrorCode", "m02_page_fault_cr2_error_code"), ("M02DoubleFaultIst", "m02_double_fault_ist"), ("M02UnexpectedVectorPanic", "m02_unexpected_vector_panic") })
        {
            if (!IsEnabled(trigger)) continue;
            foreach (string prerequisite in FaultChain)
            {
                if (explicitlySelected.TryGetValue(Full(prerequisite), out bool value) && !value) throw new ToolFailure(prefix + "_probe_explicit_false_prerequisite", "property=" + Full(prerequisite));
                flags[Full(prerequisite)] = true;
            }
            emit?.Invoke("EFI_PROFILE_" + prefix.ToUpperInvariant() + "_IMPLICATION status=ok m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true ownedCom1Ready=true panicSafeOwnedCom1=true");
        }
        Conflict("M02NormalizedInterruptFramePage", "M02OwnedGdtTssTerminal", "m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal");
        Conflict("M02OwnedIdtGatePopulation", "M02OwnedGdtTssTerminal", "m02_owned_idt_gate_population_probe_forbids_owned_gdt_tss_terminal");
        Conflict("M02OwnedIdtGatePopulation", "M02NormalizedInterruptFramePage", "m02_owned_idt_gate_population_probe_forbids_normalized_interrupt_frame_page_probe");
        foreach ((string other, string suffix) in new[] { ("M02OwnedGdtTssTerminal", "owned_gdt_tss_terminal"), ("M02NormalizedInterruptFramePage", "normalized_interrupt_frame_page_probe"), ("M02OwnedIdtGatePopulation", "owned_idt_gate_population_probe") }) Conflict("M02IdtrContinuedExecution", other, "m02_idtr_continued_execution_probe_forbids_" + suffix);
        foreach ((string other, string suffix) in new[] { ("M02OwnedX64GdtEncoding", "owned_x64_gdt_encoding_probe"), ("M02GuardedIstStackAllocation", "guarded_ist_stack_allocation_probe"), ("M02OwnedGdtTssTerminal", "owned_gdt_tss_terminal"), ("M02NormalizedInterruptFramePage", "normalized_interrupt_frame_page_probe"), ("M02OwnedIdtGatePopulation", "owned_idt_gate_population_probe"), ("M02IdtrContinuedExecution", "idtr_continued_execution_probe") }) Conflict("M02Ud2VectorState", other, "m02_ud2_vector_state_probe_forbids_" + suffix);
        Need("M01LiveAllocator", "M01LoadedImageStack", "m01_live_allocator_requires_loaded_image_stack");
        Need("M01OwnedPaging", "M01LiveAllocator", "m01_owned_paging_requires_live_allocator");
        foreach ((string trigger, string prerequisite, string reasonCode) in new[] {
            ("M02SafeStub", "M01LoadedImageStack", "m02_safe_stub_requires_loaded_image_stack"), ("M02SafeStub", "M01LiveAllocator", "m02_safe_stub_requires_live_allocator"), ("M02SafeStub", "M01OwnedPaging", "m02_safe_stub_requires_owned_paging"),
            ("M02CheckedPortIoBinding", "M02SafeStub", "m02_checked_binding_requires_safe_stub"), ("M02Com1OwnedPortIo", "M02CheckedPortIoBinding", "m02_com1_owned_port_io_requires_checked_binding"), ("M02OwnedCom1Ready", "M02Com1OwnedPortIo", "m02_owned_com1_ready_requires_com1_owned_port_io"), ("M02PanicSafeOwnedCom1", "M02OwnedCom1Ready", "m02_panic_safe_owned_com1_requires_owned_com1_ready"),
            ("M02OwnedX64GdtEncoding", "M02PanicSafeOwnedCom1", "m02_owned_x64_gdt_encoding_requires_panic_safe_owned_com1"), ("M02GuardedIstStackAllocation", "M02OwnedX64GdtEncoding", "m02_guarded_ist_stack_allocation_requires_owned_x64_gdt_encoding"), ("M02OwnedGdtTssTerminal", "M02GuardedIstStackAllocation", "m02_owned_gdt_tss_terminal_requires_guarded_ist_stack_allocation") }) Need(trigger, prerequisite, reasonCode);
    }

    private void Allocation(string main, string prefix, string[] negatives, string[] required, string[] forbidden, ref string reason)
    {
        int selected = negatives.Count(IsEnabled);
        if (selected != 0 && !IsEnabled(main)) reason = prefix + "_negative_requires_main";
        if (selected > 1) reason = prefix + "_negative_conflict";
        if (IsEnabled(main)) foreach (string name in required) if (!IsEnabled(name)) reason = prefix + "_requires_explicit_chain_" + Full(name);
        if (IsEnabled(main) || selected != 0) foreach (string name in forbidden) if (IsEnabled(name)) reason = prefix + "_conflicting_profile_" + Full(name);
    }
}
