using System.Text.RegularExpressions;

namespace OS.Tooling;

public static class QemuProfiles
{
    // Protocol records can contain commas (for example the complete GDT selector list).
    // Only caller-supplied marker arguments use comma-separated list syntax.
    private static string[] Merge(params IEnumerable<string>[] values) => values.SelectMany(x => x)
        .Select(x => x.Trim()).Where(x => x.Length != 0).Distinct(StringComparer.Ordinal).ToArray();
    private static string[] Markers(IEnumerable<string> values) => SmokeContracts.SplitMarkers(values).Distinct(StringComparer.Ordinal).ToArray();
    private static void Require(bool condition, string reason) { if (!condition) throw new ToolFailure(reason); }
    public static QemuSmokePlan Create(QemuSmokeOptions options, Action<string>? emit = null)
    {
        SmokeContracts.ValidateWorkload(options.WorkloadProfile);
        Require(options.DiskFormat is "raw" or "qcow2", "qemu.smoke.invalid_args disk_format=" + options.DiskFormat);
        Require(options.TimeoutSeconds is >= 1 and <= 300, "qemu_timeout_out_of_range");
        Dictionary<string, bool> flags = new(options.Flags, StringComparer.Ordinal);
        bool Selected(string name) => flags.TryGetValue("Enable" + name + "Probe", out bool selected) && selected;
        if (new[] { "M02OwnedGdtTssTerminal", "M02Ud2VectorState", "M02GuardedIstStackAllocation", "M02OwnedX64GdtEncoding", "M02PanicSafeOwnedCom1", "M02OwnedCom1Ready" }.Any(Selected)
            && new[] { "M02Com1OwnedPortIo", "M02CheckedPortIoBinding", "M02SafeStub" }.All(Selected))
        {
            foreach (string name in new[] { "EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM01OwnedPagingProbe" })
            {
                Require(!flags.TryGetValue(name, out bool selected) || selected, "m02_owned_com1_explicit_false_prerequisite property=" + name);
                flags[name] = true;
            }
            emit?.Invoke("QEMU_SCRIPT_M02_OWNED_COM1_READY_PREREQUISITES m01LoadedImageStack=true m01LiveAllocator=true m01OwnedPaging=true");
        }
        EfiProfile profile = EfiProfile.Create(options.BootPolicy, flags, emit);
        bool Enabled(string name) => profile.IsEnabled("Enable" + name + "Probe");
        bool normal = profile.BootPolicy == "VerifiedExit";
        bool build = options.BuildEfi || normal && !options.Explicit.Contains("BuildEfi");
        bool firstExit = Enabled("M03FirstRealExitBootServices");
        bool owned = Enabled("M03OwnedSubsystemSurvival");
        bool ownedPanic = Enabled("M03OwnedSubsystemPanic");
        bool original = build && !options.SkipImageBuild && options.M01RawObservationLog.Length == 0 && !options.RequireNativeAppMarkers;
        var overrides = new[] { "RequireMarker", "RequireOrderedMarker", "RequireUniqueMarker", "ForbidMarker" };
        void Original(string reason, bool forbidMarkers = false, bool forbidPresets = false, bool forbidRawParameter = false)
        {
            Require(original && (!forbidRawParameter || !options.Explicit.Contains("M01RawObservationLog")), reason + (reason is "m03_normal" or "m03_first_exit" ? "_requires_original_capture" : reason == "m04_gc_static" ? "_requires_fresh_build" : "_requires_build_and_original_capture"));
            if (forbidMarkers)
                foreach (string key in overrides) Require(!options.Explicit.Contains(key), reason + "_forbids_override parameter=" + key);
            if (forbidPresets) Require(options.MarkerPreset.Count == 0, reason + "_forbids_override parameter=MarkerPreset");
        }
        if (normal) Original("m03_normal", true, true, true);
        if (firstExit) Original("m03_first_exit", true, true, true);
        foreach (var item in new (string Flag, string Reason)[]
        {
            ("M03TypedExitBootServicesCall", "m03_typed_ebs"), ("M04TypeManagerModuleRegistration", "m04_type_manager"),
            ("M03FirmwareEntropySeed", "m03_firmware_entropy")
        }) if (Enabled(item.Flag)) Original(item.Reason, true, true, item.Flag != "M03FirmwareEntropySeed");
        if (Enabled("M03MemoryMapCapture")) Original("m03_memory_map_capture");
        if (Enabled("M04CheckedReferenceBarrier")) Original("m04_checked_reference");
        if (Enabled("M04GcStaticRootMaterialization")) Original("m04_gc_static");
        if (Enabled("M04ManagedHeapPageRange")) Original("m04_managed_heap_page_range");
        foreach (var flag in profile.Flags.Where(p => p.Value && (p.Key.StartsWith("EnableM01", StringComparison.Ordinal) ||
            p.Key.StartsWith("EnableM02", StringComparison.Ordinal) || p.Key.StartsWith("EnableM04", StringComparison.Ordinal))))
        {
            Require(build, "qemu_profile_requires_build_efi property=" + flag.Key);
            Require(!options.SkipImageBuild, "qemu_profile_forbids_skip_image_build property=" + flag.Key);
        }
        Require(options.M01RawObservationLog.Length == 0 || Enabled("M01OwnedPaging"), "m01_raw_observation_log_requires_owned_paging_probe");
        bool terminalProfile = Enabled("M02OwnedGdtTssTerminal") || Enabled("M02Ud2VectorState") || Enabled("M02PageFaultCr2ErrorCode") || Enabled("M02DoubleFaultIst") || Enabled("M02UnexpectedVectorPanic");
        Require(options.M01RawObservationLog.Length == 0 || !terminalProfile, "m01_raw_observation_log_forbidden_terminal_profile");
        Require(!options.Explicit.Contains("FirmwareRngSource") || Enabled("M03FirmwareEntropySeed"), "firmware_rng_source_requires_entropy_profile");
        if (Enabled("M03FirmwareEntropySeed")) Require(options.FirmwareRngSource is "virtio" or "missing", "m03_firmware_entropy_requires_exact_source");
        if (Enabled("M02UnexpectedVectorPanic")) Require(options.MarkerPreset.Count == 0, "m02_unexpected_vector_panic_forbids_marker_preset");
        if (Enabled("AcpiPhysicalDiscovery") && !firstExit)
        {
            Original("m03_acpi", true);
            Require(options.MarkerPreset.Count == 1 && options.MarkerPreset[0].Trim().ToLowerInvariant() == "acpi", "m03_acpi_requires_exact_preset");
            foreach (var flag in profile.Flags.Where(p => p.Value && p.Key != "EnableAcpiPhysicalDiscoveryProbe"))
                Require(false, "m03_acpi_forbids_optional_profiles property=" + flag.Key);
        }
        string[] required = Markers(options.RequireMarker), ordered = Markers(options.RequireOrderedMarker), unique = Markers(options.RequireUniqueMarker), forbidden = Markers(options.ForbidMarker);
        string[] custom = [.. required, .. ordered, .. unique];
        foreach (var rule in Ownership)
            if (!(rule.Flag == "NormalBoot" ? normal : Enabled(rule.Flag)))
                Require(!custom.Any(m => Regex.IsMatch(QemuTranscripts.Classify(m), rule.Pattern.Replace(" ", "", StringComparison.Ordinal))), rule.Reason + "_requires_explicit_profile");
        void Select(IReadOnlyList<string> records, IEnumerable<string> failures, bool replace = false, IReadOnlyList<string>? once = null)
        {
            required = replace ? Merge(records) : Merge(required, records);
            ordered = Merge(records); unique = Merge(once ?? records); forbidden = Merge(forbidden, failures);
        }
        if (normal) { Select(QemuContracts.M03NormalVerifiedExitMarkers, ["OS_SMOKE_FAIL", "TEST_FAIL", "ACPI_DISCOVERY_FAIL", "M03_OWNED 7", "M03_OWNED 8"], true); }
        else if (owned) { Select(ownedPanic ? QemuContracts.M03OwnedSubsystemPanicMarkers : QemuContracts.M03OwnedSubsystemMarkers,
            ownedPanic ? ["OS_SMOKE_FAIL", "TEST_FAIL", "ACPI_DISCOVERY_FAIL", "CLROS_M03_010_READY", "TEST_PASS m03.010"] : ["OS_SMOKE_FAIL", "TEST_FAIL", "ACPI_DISCOVERY_FAIL", "M03_OWNED 7", "M03_OWNED 8"], true); }
        else if (Enabled("M03FirmwareAdapterRevocation")) Select(QemuContracts.M03RevocationRequiredMarkers, ["OS_SMOKE_FAIL", "TEST_FAIL", "ACPI_DISCOVERY_FAIL"], true);
        else if (firstExit) Select(QemuContracts.M03FirstExitRequiredMarkers, ["OS_SMOKE_FAIL", "TEST_FAIL", "ACPI_DISCOVERY_FAIL"], true);
        else if (Enabled("AcpiPhysicalDiscovery")) Select(QemuContracts.M03AcpiBootstrapRequiredMarkers, ["OS_SMOKE_FAIL", "TEST_FAIL", "ACPI_DISCOVERY_FAIL"]);
        else if (Enabled("M03TypedExitBootServicesCall")) Select(QemuContracts.M03TypedExitBootServicesRequiredMarkers, ["OS_SMOKE_FAIL", "TEST_FAIL", "UEFI_EBS_CALLED", "UEFI_BOOT_SERVICES_EXITED"]);
        else if (Enabled("M04SingleFinallyCatch")) Select(QemuContracts.M04SingleFinallyRequiredMarkers, QemuContracts.M04ManagedHeapPageRangeForbiddenMarkers, true);
        else if (Enabled("M04SimpleThrowCatch")) Select(QemuContracts.M04SimpleCatchRequiredMarkers, QemuContracts.M04ManagedHeapPageRangeForbiddenMarkers, true);
        else if (Enabled("M04InterfaceDispatch")) Select(QemuContracts.M04DispatchRequiredMarkers, Merge(QemuContracts.M04ManagedHeapPageRangeForbiddenMarkers, ["M04_GC_ROOT 2", "M04_GC_ROOT 255", "M04_COLLECT 255", "M04_CCTOR 255", "M04_DISPATCH 255", "TEST_FAIL", "OS_SMOKE_FAIL"]), true);
        else if (Enabled("M04ClassConstructorExecution")) Select(QemuContracts.M04CctorRequiredMarkers, Merge(QemuContracts.M04ManagedHeapPageRangeForbiddenMarkers, ["M04_GC_ROOT 2", "M04_GC_ROOT 255", "M04_COLLECT 255", "M04_CCTOR 255", "TEST_FAIL", "OS_SMOKE_FAIL"]), true);
        else if (Enabled("M04SeededCollectorChurn")) Select(QemuContracts.M04SeededChurnRequiredMarkers, Merge(QemuContracts.M04ManagedHeapPageRangeForbiddenMarkers, ["M04_GC_ROOT 2", "M04_GC_ROOT 255", "M04_COLLECT 255", "M04_CHURN 255", "TEST_FAIL", "OS_SMOKE_FAIL"]));
        else if (Enabled("M04GcStaticRootMaterialization")) Select(QemuContracts.M04GcStaticRootRequiredMarkers, Merge(QemuContracts.M04ManagedHeapPageRangeForbiddenMarkers, ["M04_GC_ROOT 2", "M04_GC_ROOT 255", "TEST_FAIL", "OS_SMOKE_FAIL"]));
        else if (Enabled("M04X64UnwindMetadata")) Select(QemuContracts.M04X64UnwindMetadataRequiredMarkers, ["TEST_FAIL", "OS_SMOKE_FAIL"], true);
        else if (Enabled("M04TypeManagerModuleRegistration")) Select(QemuContracts.M04TypeManagerRequiredMarkers, ["TEST_FAIL", "OS_SMOKE_FAIL"]);
        else if (Enabled("M03FirmwareEntropySeed")) Select(QemuContracts.M03FirmwareEntropyRequiredMarkers, ["TEST_FAIL", "OS_SMOKE_FAIL"]);
        else if (Enabled("M03MemoryMapCapture")) Select(QemuContracts.M03MemoryMapCaptureRequiredMarkers, ["TEST_FAIL", "OS_SMOKE_FAIL", "M03_CAPTURE_FAILURE"]);
        else if (Enabled("M04CheckedReferenceBarrier")) Select(QemuContracts.M04CheckedReferenceBarrierRequiredMarkers, QemuContracts.M04CheckedReferenceBarrierForbiddenMarkers);
        else if (Enabled("M04StringAllocation")) Select(QemuContracts.M04StringAllocationRequiredMarkers, QemuContracts.M04StringAllocationForbiddenMarkers);
        else if (Enabled("M04SzArrayAllocation")) Select(QemuContracts.M04SzArrayAllocationRequiredMarkers, QemuContracts.M04SzArrayAllocationForbiddenMarkers);
        else if (Enabled("M04RhpNewFast")) Select(QemuContracts.M04RhpNewFastRequiredMarkers, QemuContracts.M04RhpNewFastForbiddenMarkers);
        else if (Enabled("M04ManagedHeapPageRange")) Select(QemuContracts.M04ManagedHeapPageRangeRequiredMarkers, QemuContracts.M04ManagedHeapPageRangeForbiddenMarkers);
        else if (Enabled("M04LayoutContract")) Select(QemuContracts.M04LayoutContractRequiredMarkers, QemuContracts.M04LayoutContractForbiddenMarkers, once: QemuContracts.M04LayoutContractUniqueMarkers);
        else if (Enabled("M04ArithmeticConformance")) Select(QemuContracts.M04ArithmeticConformanceRequiredMarkers, QemuContracts.M04ArithmeticConformanceForbiddenMarkers, once: QemuContracts.M04ArithmeticConformanceUniqueMarkers);
        else if (Enabled("M04SpanOverlapMemoryCastUnsafeWidthConformance")) Select(QemuContracts.M04SpanMemoryConformanceRequiredMarkers, QemuContracts.M04SpanMemoryConformanceForbiddenMarkers, once: QemuContracts.M04SpanMemoryConformanceUniqueMarkers);
        else if (Enabled("M04IndependentEndianConformance"))
        {
            required = Merge(required, QemuContracts.M04IndependentEndianConformanceRequiredMarkers);
            ordered = Merge(ordered, QemuContracts.M04IndependentEndianConformanceRequiredMarkers);
            unique = Merge(unique, QemuContracts.M04IndependentEndianConformanceReadyMarker);
            forbidden = Merge(forbidden, QemuContracts.M04IndependentEndianConformanceForbiddenMarkers);
        }
        void Add(IReadOnlyList<string> records, IReadOnlyList<string> failures, IReadOnlyList<string>? once = null, IReadOnlyList<string>? sequence = null)
        {
            required = Merge(required, records); ordered = Merge(ordered, sequence ?? records);
            unique = Merge(unique, once ?? records); forbidden = Merge(forbidden, failures);
        }
        if (Enabled("M02NormalizedInterruptFramePage"))
        {
            ordered = ordered.Where(m => m != "UEFI_MEMORY_MAP_READY").ToArray();
            Add(QemuContracts.M02NormalizedInterruptFramePageRequiredMarkers, QemuContracts.M02NormalizedInterruptFramePageForbiddenMarkers, QemuContracts.M02NormalizedInterruptFramePageUniqueMarkers);
        }
        if (Enabled("M02OwnedIdtGatePopulation"))
        {
            ordered = ordered.Where(m => m != "UEFI_MEMORY_MAP_READY").ToArray();
            Add(QemuContracts.M02OwnedIdtGatePopulationRequiredMarkers, QemuContracts.M02OwnedIdtGatePopulationForbiddenMarkers, QemuContracts.M02OwnedIdtGatePopulationUniqueMarkers);
        }
        if (Enabled("M02IdtrContinuedExecution"))
        {
            required = Merge(QemuContracts.M02IdtrContinuedExecutionRequiredMarkers, required);
            ordered = ordered.Where(m => m != "UEFI_MEMORY_MAP_READY").ToArray();
            Add(QemuContracts.M02IdtrContinuedExecutionRequiredMarkers, QemuContracts.M02IdtrContinuedExecutionForbiddenMarkers, QemuContracts.M02IdtrContinuedExecutionUniqueMarkers, QemuContracts.M02IdtrContinuedExecutionOrderedMarkers);
        }
        if (Enabled("M02OwnedGdtTssTerminal")) { required = [.. QemuContracts.M02OwnedGdtTssTerminalRequiredMarkers]; ordered = required; unique = required; forbidden = [.. QemuContracts.M02OwnedGdtTssTerminalForbiddenMarkers]; }
        else if (Enabled("M02GuardedIstStackAllocation")) Add(QemuContracts.M02GuardedIstStackAllocationRequiredMarkers, QemuContracts.M02GuardedIstStackAllocationForbiddenMarkers);
        else if (Enabled("M02OwnedX64GdtEncoding")) Add(QemuContracts.M02OwnedX64GdtEncodingRequiredMarkers, QemuContracts.M02OwnedX64GdtEncodingForbiddenMarkers);
        else if (Enabled("M02PanicSafeOwnedCom1")) Add(QemuContracts.M02PanicSafeOwnedCom1RequiredMarkers, QemuContracts.M02PanicSafeOwnedCom1ForbiddenMarkers);
        else if (Enabled("M02OwnedCom1Ready")) Add(QemuContracts.M02OwnedCom1ReadyRequiredMarkers, QemuContracts.M02OwnedCom1ReadyForbiddenMarkers);
        else if (Enabled("M02Com1OwnedPortIo")) Add(QemuContracts.M02Com1OwnedPortIoRequiredMarkers, QemuContracts.M02Com1OwnedPortIoForbiddenMarkers);
        else if (Enabled("M02CheckedPortIoBinding")) Add(QemuContracts.M02CheckedPortIoBindingRequiredMarkers, QemuContracts.M02CheckedPortIoBindingForbiddenMarkers);
        else if (Enabled("M02SafeStub")) Add(QemuContracts.M02SafeStubRequiredMarkers, QemuContracts.M02SafeStubForbiddenMarkers);
        if (Enabled("M02Ud2VectorState") || Enabled("M02PageFaultCr2ErrorCode") || Enabled("M02DoubleFaultIst") || Enabled("M02UnexpectedVectorPanic"))
        {
            ordered = ordered.Where(x => x is not ("OS_BOOT_OK" or "TEST_PASS boot.sequence" or "OS_SMOKE_PASS")).ToArray();
            if (Enabled("M02Ud2VectorState")) Add(QemuContracts.M02Ud2VectorStateRequiredMarkers, QemuContracts.M02Ud2VectorStateForbiddenMarkers);
            if (Enabled("M02PageFaultCr2ErrorCode")) Add(QemuContracts.M02PageFaultCr2ErrorCodeRequiredMarkers, QemuContracts.M02PageFaultCr2ErrorCodeForbiddenMarkers);
            if (Enabled("M02DoubleFaultIst")) Add(QemuContracts.M02DoubleFaultIstRequiredMarkers, QemuContracts.M02DoubleFaultIstForbiddenMarkers);
            if (Enabled("M02UnexpectedVectorPanic")) Add(["OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS", "M02_UNEXPECTED_STATE"], []);
        }
        if (options.RequireNativeAppMarkers) required = Merge(required, QemuContracts.nativeAppRequiredMarkers);
        Require(required.Length != 0, "qemu_required_markers_empty");
        string[] presets = Markers(options.MarkerPreset).Select(OS.Tools.QemuRunner.SmokeMarkerPreset.Normalize).Distinct(StringComparer.Ordinal).ToArray();
        return new(profile.BootPolicy, profile.Flags, Array.AsReadOnly(required), Array.AsReadOnly(ordered), Array.AsReadOnly(unique),
            Array.AsReadOnly(forbidden), Array.AsReadOnly(presets), build, normal || firstExit || Enabled("M02OwnedGdtTssTerminal") || Enabled("M04X64UnwindMetadata"));
    }

    private static readonly (string Flag, string Pattern, string Reason)[] Ownership =
    [
        ("NormalBoot", @"^(CLROS_M03_011_READY|TEST_(START|PASS) m03\.011)", "m03_normal"),
        ("M03FirstRealExitBootServices", @"^(M03_EBS_|M03_PERMANENT_|M03_POST_EBS_|CLROS_M03_007|TEST_(START|PASS) m03\.00[79])", "m03_first_exit"),
        ("M03OwnedSubsystemSurvival", @"^(M03_OWNED|CLROS_M03_010|TEST_(START|PASS) m03\.010)", "m03_owned"),
        ("M03FirmwareAdapterRevocation", @"^(M03_ADAPTER_|CLROS_M03_008|TEST_(START|PASS) m03\.008)", "m03_revocation"),
        ("M03TypedExitBootServicesCall", @"^(M03_TYPED_EBS_|CLROS_M03_004|TEST_(START|PASS) m03\.004)", "m03_typed_ebs"),
        ("AcpiPhysicalDiscovery", @"^(CLROS_M03_002_|TEST_(START|PASS) m03\.002)", "m03_acpi"),
        ("M03FirmwareEntropySeed", @"^(M03_ENTROPY_|CLROS_M03_003|TEST_(START|PASS) m03\.003)", "m03_firmware_entropy"),
        ("M03MemoryMapCapture", @"^(M03_CAPTURE_|CLROS_M03_001|TEST_(START|PASS) m03\.001)", "m03_memory_map_capture"),
        ("M04ClassConstructorExecution", @"^(M04_CCTOR|CLROS_M04_024|TEST_(START|PASS) m04\.024)", "m04_cctor"),
        ("M04InterfaceDispatch", @"^(M04_DISPATCH|CLROS_M04_016|TEST_(START|PASS) m04\.016)", "m04_dispatch"),
        ("M04SimpleThrowCatch", @"^(M04_CATCH|CLROS_M04_SIMPLE_CATCH|TEST_(START|PASS) m04\.021\.simple-catch)", "m04_simple_catch"),
        ("M04SingleFinallyCatch", @"^(M04_FINALLY|CLROS_M04_SINGLE_FINALLY|TEST_(START|PASS) m04\.021\.single-finally)", "m04_single_finally"),
        ("M04X64UnwindMetadata", @"^(CLROS_M04_020_|TEST_(START|PASS) m04\.020)", "m04_x64_unwind"),
        ("M04TypeManagerModuleRegistration", @"^(CLROS_M04_011_|TEST_(START|PASS) m04\.011)", "m04_type_manager"),
        ("M04SeededCollectorChurn", @"^(M04_CHURN|CLROS_M04_014_READY)", "m04_churn"),
        ("M04GcStaticRootMaterialization", @"^(M04_GC_ROOT|M04_COLLECT|CLROS_M04_013_READY)", "m04_gc_static"),
        ("M04CheckedReferenceBarrier", @"^(M04_REFERENCE_|CLROS_M04_010_NATIVE|TEST_(START|PASS) m04\.010\.references)", "m04_checked_reference"),
        ("M04StringAllocation", @"(?i)CLROS_M04_009|M04_STRING_|m04\.009", "m04_string"),
        ("M04SzArrayAllocation", @"(?i)CLROS_M04_008|M04_ARRAY_|m04\.008", "m04_sz_array"),
        ("M04RhpNewFast", @"(?i)CLROS_M04_007|M04_RHP_|m04\.007", "m04_rhp_new_fast"),
        ("M04ManagedHeapPageRange", @"(?i)CLROS_M04_006|M04_HEAP_|m04\.006", "m04_managed_heap"),
        ("M04LayoutContract", @"(?i)CLROS_M04_005", "m04_layout_contract"),
        ("M02IdtrContinuedExecution", @"(?i)CLROS_M02_012|TEST_(START|PASS) m02\.012", "m02_idtr_continued_execution"),
        ("M02Ud2VectorState", @"(?i)M02_UD2_|CLROS_M02_013|m02\.013", "m02_ud2_vector_state"),
        ("M02PageFaultCr2ErrorCode", @"(?i)M02_PAGE_FAULT_|CLROS_M02_014|m02\.014", "m02_page_fault"),
        ("M02DoubleFaultIst", @"(?i)M02_DF_|M02_DOUBLE_FAULT_|CLROS_M02_015|m02\.015", "m02_double_fault"),
        ("M02UnexpectedVectorPanic", @"(?i)M02_UNEXPECTED_|CLROS_M02_016|m02\.016", "m02_unexpected_vector_panic")
    ];
}
