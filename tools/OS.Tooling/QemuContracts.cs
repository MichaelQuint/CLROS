using System.Linq;

namespace OS.Tooling;

// Reviewed guest protocol records. Each literal is owned once; inventories reuse it.
public static class QemuContracts
{
    public const ulong M07ArpReceiveFeatures = 0x100008000;

    public const string M03TypedAdmissionRecord = "M03_TYPED_EBS_ADMISSION default=6 missingFunction=3 missingHandle=4 simulated=5 exited=5 readyDenied=5 readyPrepare=7 unchanged=1 calls=0";
    public const string M03TypedReadyRecord = "CLROS_M03_004_READY controlled=true realExitBootServices=false states=4 calls=4 reuse=4 reentry=1 mode=0";
    public const string M03FinalMapReadyRecord = "CLROS_M03_005_READY controlled=true realExitBootServices=false captures=1 exits=1 reuseRejected=true";
    public const string M03FinalMapRetryReadyRecord = "CLROS_M03_006_READY controlled=true realExitBootServices=false cases=2 maxAttempts=2 captures=4 exits=4 exhausted=1";
    public const int FirmwarePrefixBytes = 304;
    // Pinned OVMF boot through the explicit modern block fixture at 00:06.0.
    public const int BlockFirmwarePrefixBytes = 240;
    public const string BlockFirmwarePrefixSha256 = "B60C7B9F9AAF6D318D4B9F2F33970E5389E71F180D7B09DE7C189B1FB59CDC57";
    public const string FirmwarePrefixSha256 = "BB50E913C752428AE49AE6C20879196F507926CC685CAD83986494244299D124";
    public static IReadOnlyList<string> RunnerDefaultRequiredMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083]);
    public static int M02UnexpectedCaptureRecordCount => M02TerminalPrefix.Count + 2;
    public static int M02DoubleFaultCaptureRecordCount => M02TerminalPrefix.Count + 14;
    public const string Record000 = @"LIVE_ALLOCATOR_NOT_INSTALLED";
    public const string Record001 = @"BOOT_PAGE_ALLOCATOR_NOT_READY";
    public const string Record002 = @"M01_ARBITRARY_CONVENTIONAL_PAGE_USED";
    public const string Record003 = @"M01_PAGE_OUTSIDE_ALLOCATOR_ARENA";
    public const string Record004 = @"M01_PAGE_TABLE_OUTSIDE_ARENA";
    public const string Record005 = @"M01_PROTECTION_PROBE_ALLOCATION_FAILED";
    public const string Record006 = @"M01_PROTECTION_PROBE_ALIAS";
    public const string Record007 = @"M01_CR3_STUB_READBACK_FAILED";
    public const string Record008 = @"M01_PAGING_MODE_VALIDATION_FAILED";
    public const string Record009 = @"M01_PGE_CLEAR_FAILED";
    public const string Record010 = @"M01_PGE_RESTORE_FAILED";
    public const string Record011 = @"M01_EXECUTABLE_TABLE_DATA";
    public const string Record012 = @"M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED";
    public const string Record013 = @"M01_FIRMWARE_CALLBACK_DURING_OWNED_CR3";
    public const string Record014 = @"M01_FIRMWARE_CR3_RESTORE_FAILED";
    public const string Record015 = @"M01_CR4_RESTORE_FAILED";
    public const string Record016 = @"M01_RFLAGS_RESTORE_FAILED";
    public const string Record017 = @"M01_OWNED_CR3_CANARY_FAILED";
    public const string Record018 = @"M01_RX_PROBE_FAILED";
    public const string Record019 = @"TEST_FAIL";
    public const string Record020 = @"OS_SMOKE_FAIL";
    public const string Record021 = @"M01_INTEGRATION_FAIL";
    public const string Record022 = @"M01_STAGE1_FAIL";
    public const string Record023 = @"M01_STAGE2_FAIL";
    public const string Record024 = @"M01_STAGE3_FAIL";
    public const string Record025 = @"M02_STUB_PAGE_MATERIALIZATION_FAILED";
    public const string Record026 = @"M02_STUB_PAGE_READBACK_FAILED";
    public const string Record027 = @"M02_STUB_PAGE_HASH_MISMATCH";
    public const string Record028 = @"M02_SAFE_PROBE_FAILED";
    public const string Record029 = @"M02_OUT8_EXECUTED";
    public const string Record030 = @"M02_IN8_EXECUTED";
    public const string Record031 = @"M02_PORT_IO_ATTEMPTED";
    public const string Record032 = @"M02_PRIVILEGED_STUB_TARGETED";
    public const string Record033 = @"TEST_FAIL m02.017";
    public const string Record034 = @"M02_CHECKED_PORT_IO_BINDING_FAILED";
    public const string Record035 = @"CLROS_M02_003_READY bindingReady=true out8AddressChecked=true in8AddressChecked=true guestInvocation=false portIoExecuted=false";
    public const string Record036 = @"TEST_START m01.stage1";
    public const string Record037 = @"M01_LOADED_IMAGE_CAPTURED";
    public const string Record038 = @"M01_STACK_SWITCH_STUB_READY";
    public const string Record039 = @"M01_STACK_ARENA_ALLOCATED";
    public const string Record040 = @"M01_STACK_GUARD_RESERVED";
    public const string Record041 = @"M01_STAGE1_MAP_RECAPTURED";
    public const string Record042 = @"M01_STAGE1_RESERVATIONS_VALIDATED";
    public const string Record043 = @"M01_OWNED_STACK_READY";
    public const string Record044 = @"M01_OWNED_STACK_SWITCHED";
    public const string Record045 = @"M01_RSP_CONTAINED";
    public const string Record046 = @"TEST_PASS m01.stage1";
    public const string Record047 = @"TEST_START m01.stage2";
    public const string Record048 = @"M01_ALLOCATOR_ARENA_ALLOCATED";
    public const string Record049 = @"M01_STAGE2_MAP_RECAPTURED";
    public const string Record050 = @"M01_RESERVATIONS_APPLIED";
    public const string Record051 = @"M01_LIVE_ALLOCATED_4K";
    public const string Record052 = @"M01_LIVE_PAGE_WITHIN_ARENA";
    public const string Record053 = @"M01_LIVE_FREED_4K";
    public const string Record054 = @"M01_DOUBLE_FREE_REJECTED";
    public const string Record055 = @"TEST_PASS m01.stage2";
    public const string Record056 = @"TEST_START m01.stage3";
    public const string Record057 = @"M01_CR3_STUB_PAGE_ALLOCATED";
    public const string Record058 = @"M01_PROTECTION_PROBE_PAGE_ALLOCATED";
    public const string Record059 = @"M01_PAGE_TABLE_STATE_ARENA_ALLOCATED";
    public const string Record060 = @"M01_STAGE3_MAP_RECAPTURED";
    public const string Record061 = @"TEST_START m02.017";
    public const string Record062 = @"M02_STUB_PAGE_MATERIALIZED pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56";
    public const string Record063 = @"M02_STUB_PAGE_READBACK_VERIFIED fnv1a64=982E5B7E3EE58E56";
    public const string Record064 = @"M01_CR3_STUB_MATERIALIZED";
    public const string Record065 = @"M01_PAGE_TABLES_BUILT";
    public const string Record066 = @"M01_PAGING_MODE_VALIDATED";
    public const string Record067 = @"M01_PRE_CR3";
    public const string Record068 = @"M01_FIRMWARE_CR3_RESTORED";
    public const string Record069 = @"M01_RFLAGS_RESTORED";
    public const string Record070 = @"M01_PGE_CYCLE_VERIFIED";
    public const string Record071 = @"M01_OWNED_CR3_CANARY_VERIFIED";
    public const string Record072 = @"M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED";
    public const string Record073 = @"M01_POST_CR3";
    public const string Record074 = @"M01_RW_TO_RX_VERIFIED";
    public const string Record075 = @"M01_TLB_INVALIDATION_COMPLETE";
    public const string Record076 = @"M01_RX_PROBE_EXECUTED";
    public const string Record077 = @"M01_PAGE_ZERO_ABSENT";
    public const string Record078 = @"M02_SAFE_PROBE_EXECUTED offset=0 return=0x4F534F4B";
    public const string Record079 = @"CLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56 temporaryOwnedRootRx=true portIoExecuted=false";
    public const string Record080 = @"TEST_PASS m02.017";
    public const string Record081 = @"TEST_PASS m01.stage3";
    public const string Record082 = @"CLROS_M01_LIVE_BOOT_MEMORY_READY";
    public const string Record083 = @"OS_SMOKE_PASS";
    public const string Record084 = @"M02_COM1_CONFIGURATION_FAILED";
    public const string Record085 = @"CLROS_M02_004_READY configurationWrites=7 bindingReady=true guestPortIo=true guestIn8=false markerTransport=uefiSerial postExitBootServices=false";
    public const string Record086 = @"M02_DOUBLE_FAULT_FAILED";
    public const string Record087 = @"CLROS_M02_015_HOST_READY";
    public const string Record088 = @"CLROS_M02_014_READY";
    public const string Record089 = @"M02_PAGE_FAULT_";
    public const string Record090 = @"CLROS_M02_015_EFI_HOST_READY";
    public const string Record091 = @"CLROS_M02_013_READY";
    public const string Record092 = @"OS_BOOT_OK";
    public const string Record093 = @"TEST_PASS boot.sequence";
    public const string Record094 = @"TEST_START m02.015";
    public const string Record095 = @"M02_DF_LAYOUT";
    public const string Record096 = @"M02_DF_MODE";
    public const string Record097 = @"M02_DF_ROOT";
    public const string Record098 = @"M02_DF_ABSENT role=zero";
    public const string Record099 = @"M02_DF_ABSENT role=df";
    public const string Record100 = @"M02_DF_ABSENT role=pf";
    public const string Record101 = @"M02_DF_ABSENT role=nmi";
    public const string Record102 = @"M02_DF_ABSENT role=transition";
    public const string Record103 = @"M02_DF_FRAME";
    public const string Record104 = @"M02_DF_DESCRIPTORS";
    public const string Record105 = @"M02_DF_GPRS";
    public const string Record106 = @"CLROS_M02_015_READY vector=8 hardwareErrorCode=true errorCode=0 ist=1 pageZeroAbsent=true guardPagesAbsent=4 savedState=diagnosticOnly terminalHalt=true postExitBootServices=false";
    public const string Record107 = @"TEST_PASS m02.015";
    public const string Record108 = @"M02_OWNED_COM1_READY_FAILED";
    public const string Record109 = @"M02_PANIC_SAFE_OWNED_COM1_FAILED";
    public const string Record110 = @"M02_OWNED_X64_GDT_ENCODING_FAILED";
    public const string Record111 = @"M02_GUARDED_IST_STACK_ALLOCATION_FAILED";
    public const string Record112 = @"CLROS_M02_008_READY stacks=3 guards=3 doubleFaultIst=1 pageFaultIst=2 nmiIst=3 writableMetadata=true executableMetadata=false allocationFree=true guestArenaAllocated=true guestStackWrites=true guestGuardMappings=false guestTss=false guestIdt=false arenaFreed=true markerTransport=uefiSerial postExitBootServices=false";
    public const string Record113 = @"OWNED_COM1_READY";
    public const string Record114 = @"CLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 configurationWrites=7 lineStatusPolls=18 ownedWriter=true ownedMarkerTransport=com1PortIo evidenceMarkerTransport=uefiSerial guestPortIo=true guestIn8=true postExitBootServices=false";
    public const string Record115 = @"CLROS_M02_006_READY panicFormatter=bounded ownedCom1Transport=true callerScratch=true statusCoded=true firmwareFallback=false";
    public const string Record116 = @"CLROS_M02_006_EVIDENCE markerBytes=125 serialBytes=127 configurationWrites=7 lineStatusPolls=127 markerTransport=com1PortIo evidenceTransport=uefiSerial guestPortIo=true guestIn8=true panicFormatter=linked panicReporter=ownedCom1 callerScratch=true statusCoded=true firmwareFallback=false postExitBootServices=false";
    public const string Record117 = @"CLROS_M02_007_READY encoding=guestValidated callerOwned=true readBack=true gdtBytes=40 tssBytes=104 tableLimit=39 selectors=0,8,16,24 guestEncoding=true guestLgdt=false guestLtr=false allocationFree=true markerTransport=uefiSerial postExitBootServices=false";
    public const string Record118 = @"M02_IDTR_CONTINUED_EXECUTION_FAILED";
    public const string Record119 = @"CLROS_M02_012_HOST_READY contextBytes=80 stubBytes=52 idtrBytes=10 tableBytes=4096 limit=4095 lidtPlan=2 sidtPlan=3 cliPlan=1 stiPlan=1 returnCookie=4F53494454523132 readBack=true guestExecution=false idtrLoaded=false idtrRestored=false continuedExecution=false faultTriggered=false postExitBootServices=false";
    public const string Record120 = @"CLROS_M02_011_READY vectors=256 gates=256 gateBytes=16 tableBytes=4096 selector=8 present=256 dpl0=256 interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 nmiIst=3 doubleFaultIst=1 pageFaultIst=2 normalizedFnv1a64=8514F42EBDFC5D55 readBack=true guestPopulation=true loaderData=true allocationPages=3 allocationReleased=true stubExecution=false idtrLoaded=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false";
    public const string Record121 = @"EFI_CONSOLE_READY";
    public const string Record122 = @"TEST_START m02.012";
    public const string Record123 = @"CLROS_M02_012_READY tableBytes=4096 idtrBytes=10 idtrLimit=4095 policyFnv=8514F42EBDFC5D55 firmwareGatesCopied=0 firmwareIdtrCaptured=true ownedIdtrLoaded=true ownedIdtrReadBack=true firmwareIdtrRestored=true restoreReadBack=true ownedIdtrActive=false continuedExecution=true cliWindow=true ifRestored=true instructionPublished=true allocationsReleased=true wxEnforced=false interruptTriggered=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false";
    public const string Record124 = @"TEST_PASS m02.012";
    public const string Record125 = @"UEFI_MEMORY_MAP_READY";
    public const string Record126 = @"UEFI_MEMORY_MAP_OWNED";
    public const string Record127 = @"BOOT_HANDOFF_READY";
    public const string Record128 = @"UEFI_MEMORY_MAP_NORMALIZED";
    public const string Record129 = @"BOOT_MEMORY_MAP_ACCEPTED";
    public const string Record130 = @"BOOT_MEMORY_REGIONS_READY";
    public const string Record131 = @"BOOT_MEMORY_REGION_COUNT";
    public const string Record132 = @"BOOT_PAGE_ALLOCATOR_READY";
    public const string Record133 = @"BOOT_ACPI_RSDP_HANDOFF_READY";
    public const string Record134 = @"UEFI_ACPI_RSDP_READY";
    public const string Record135 = @"M02_NORMALIZED_INTERRUPT_FRAME_PAGE_FAILED";
    public const string Record136 = @"CLROS_M02_010_READY vectors=256 hardwareErrorCodeVectors=9 syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 normalizedSlots=3 commonTargetDelta=4096 fnv1a64=428D0353A96B7ACE readBack=true guestMaterialization=true loaderData=true pageReleased=true stubExecution=false idtInstalled=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false";
    public const string Record137 = @"TEST_START m02.010";
    public const string Record138 = @"TEST_PASS m02.010";
    public const string Record139 = @"M02_OWNED_GDT_TSS_PREENTRY_FAILED";
    public const string Record140 = @"M02_OWNED_GDT_TSS_VERIFY_FAILED";
    public const string Record141 = @"M02_OWNED_GDT_TSS_UNEXPECTED_RETURN";
    public const string Record142 = @"CLROS_M02_009_READY gdtr=true tr=true gdtBytes=40 tssBytes=104 gdtrLimit=39 trSelector=24 ownedCom1=true terminalHalt=true postExitBootServices=false";
    public const string Record143 = @"TEST_START m02.009";
    public const string Record144 = @"TEST_PASS m02.009";
    public const string Record145 = @"M02_OWNED_IDT_GATE_POPULATION_FAILED";
    public const string Record146 = @"TEST_START m02.011";
    public const string Record147 = @"TEST_PASS m02.011";
    public const string Record148 = @"M02_PAGE_FAULT_CR2_ERROR_CODE_FAILED";
    public const string Record149 = @"CLROS_M02_014_HOST_READY";
    public const string Record150 = @"CLROS_M02_014_PAGE_IMAGE_HOST_READY";
    public const string Record151 = @"CLROS_M02_014_TRANSITION_HOST_READY";
    public const string Record152 = @"CLROS_M02_014_EFI_HOST_READY";
    public const string Record153 = @"TEST_START m02.014";
    public const string Record154 = @"M02_PAGE_FAULT_LAYOUT";
    public const string Record155 = @"M02_PAGE_FAULT_MODE";
    public const string Record156 = @"M02_PAGE_FAULT_ROOT";
    public const string Record157 = @"M02_PAGE_FAULT_ABSENT role=zero";
    public const string Record158 = @"M02_PAGE_FAULT_ABSENT role=df";
    public const string Record159 = @"M02_PAGE_FAULT_ABSENT role=pf";
    public const string Record160 = @"M02_PAGE_FAULT_ABSENT role=nmi";
    public const string Record161 = @"M02_PAGE_FAULT_ABSENT role=transition";
    public const string Record162 = @"M02_PAGE_FAULT_FRAME";
    public const string Record163 = @"CLROS_M02_014_READY vector=14 hardwareErrorCode=true errorCode=0 cr2=0 pageZeroAbsent=true guardPagesAbsent=4 ist=2 rf=true ownedCr3=true hardwareStateCaptured=true faultTriggered=true terminalHalt=true postExitBootServices=false";
    public const string Record164 = @"TEST_PASS m02.014";
    public const string Record165 = @"M02_UD2_VECTOR_STATE_VERIFY_FAILED";
    public const string Record166 = @"TEST_FAIL m02.013 vector_or_register_mismatch";
    public const string Record167 = @"CLROS_M02_013_HOST_READY";
    public const string Record168 = @"CLROS_M02_013_HOST_READY requestBytes=32 resultBytes=72 ud2Bytes=2 vector=6 errorCodePresent=false errorCode=0 auditFields=23 probeEnabled=false executionRequested=false executionAttempted=false syntheticObservation=true guestExecution=false faultTriggered=false idtrLoaded=false postExitBootServices=false";
    public const string Record169 = @"TEST_START m02.013";
    public const string Record170 = @"CLROS_M02_013_READY vector=6 errorCodePresent=false errorCode=0 ud2RipMatched=true registerFields=23 registerStateCaptured=true registerStateMatched=true ownedGdtLoaded=true ownedTssLoaded=true ownedIdtrLoaded=true ownedCom1=true faultTriggered=true terminalHalt=true markerTransport=ownedCom1 postExitBootServices=false";
    public const string Record171 = @"TEST_PASS m02.013";
    public const string Record172 = @"TEST_START efi.main";
    public const string Record173 = @"UEFI_MEMORY_MAP_DESCRIPTOR_COUNT";
    public const string Record174 = @"ACPI_RSDP_SNAPSHOT_READY";
    public const string Record175 = @"ACPI_RSDP_VALIDATED";
    public const string Record176 = @"ACPI_ROOT_SDT_SNAPSHOT_READY";
    public const string Record177 = @"ACPI_ROOT_SDT_VALIDATED";
    public const string Record178 = @"ACPI_MCFG_DISCOVERY_READY";
    public const string Record179 = @"TEST_START m03.002";
    public const string Record180 = @"CLROS_M03_002_TABLE role=rsdp";
    public const string Record181 = @"CLROS_M03_002_TABLE role=root";
    public const string Record182 = @"CLROS_M03_002_TABLE role=mcfg";
    public const string Record183 = @"CLROS_M03_002_TABLE role=apic";
    public const string Record184 = @"CLROS_M03_002_TABLE role=hpet";
    public const string Record185 = @"CLROS_M03_002_RETAIN";
    public const string Record186 = @"CLROS_M03_002_READY firmwareAvailable=true exitBootServices=false";
    public const string Record187 = @"TEST_PASS m03.002";
    public const string Record188 = @"TEST_START boot.sequence";
    public const string Record189 = @"BOOT_PHASE PhaseA.Audit START";
    public const string Record190 = @"BOOT_PHASE PhaseA.Audit PASS";
    public const string Record191 = @"BOOT_PHASE PhaseA.Diagnostics START";
    public const string Record192 = @"BOOT_PHASE PhaseA.Diagnostics PASS";
    public const string Record193 = @"TEST_START m03.003.efi";
    public const string Record194 = @"M03_ENTROPY_DISCOVERY";
    public const string Record195 = @"M03_ENTROPY_ACQUIRE";
    public const string Record196 = @"M03_ENTROPY_RETAIN";
    public const string Record197 = @"CLROS_M03_003_READY bytes=32";
    public const string Record198 = @"TEST_PASS m03.003.efi";
    public const string Record199 = @"TEST_START m03.007";
    public const string Record200 = @"M03_EBS_RETURNED_SUCCESS";
    public const string Record201 = @"M03_PERMANENT_CR3_ADOPTED";
    public const string Record202 = @"M03_POST_EBS_OWNED_DIAGNOSTIC";
    public const string Record203 = @"CLROS_M03_007_READY";
    public const string Record204 = @"TEST_PASS m03.007";
    public const string Record205 = @"TEST_START m03.009";
    public const string Record206 = @"UEFI_EBS_SUCCESS";
    public const string Record207 = @"POST_FIRMWARE_DIAGNOSTICS_READY";
    public const string Record208 = @"CLROS_M03_009_READY";
    public const string Record209 = @"TEST_PASS m03.009";
    public const string Record210 = @"TEST_START m03.001.capture";
    public const string Record211 = @"M03_CAPTURE_AUTH ";
    public const string Record212 = @"M03_CAPTURE_QUERY ";
    public const string Record213 = @"M03_CAPTURE_ALLOC ";
    public const string Record214 = @"M03_CAPTURE_FINAL ";
    public const string Record215 = @"M03_CAPTURE_COPY ";
    public const string Record216 = @"M03_CAPTURE_GUARDS ";
    public const string Record217 = @"M03_CAPTURE_HANDOFF ";
    public const string Record218 = @"CLROS_M03_001_READY firmwareAvailable=true exitBootServices=false";
    public const string Record219 = @"TEST_PASS m03.001.capture";
    public const string Record220 = @"UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ";
    public const string Record221 = @"BOOT_MEMORY_REGION_COUNT ";
    public const string Record222 = @"BOOT_PAGE_ALLOCATOR_READY ";
    public const string Record223 = @"TEST_START m03.010";
    public const string Record224 = @"M03_OWNED 0";
    public const string Record225 = @"M03_OWNED 1";
    public const string Record226 = @"M03_OWNED 2";
    public const string Record227 = @"M03_OWNED 3";
    public const string Record228 = @"M03_OWNED 4";
    public const string Record229 = @"M03_OWNED 5";
    public const string Record230 = @"M03_OWNED 6";
    public const string Record231 = @"CLROS_M03_010_READY";
    public const string Record232 = @"TEST_PASS m03.010";
    public const string Record233 = @"TEST_START m03.011";
    public const string Record234 = @"CLROS_M03_011_READY";
    public const string Record235 = @"TEST_PASS m03.011";
    public const string Record236 = @"M03_OWNED 7";
    public const string Record237 = @"M03_OWNED 8";
    public const string Record238 = @"M03_ADAPTER_ADMITTED 1 0";
    public const string Record239 = @"TEST_START m03.008";
    public const string Record240 = @"M03_ADAPTER_DENIED 0 4 0 1";
    public const string Record241 = @"M03_ADAPTER_DENIED 1 4 0 1";
    public const string Record242 = @"M03_ADAPTER_DENIED 2 4 0 1";
    public const string Record243 = @"M03_ADAPTER_DENIED 3 4 0 1";
    public const string Record244 = @"M03_ADAPTER_DENIED 4 4 0 1";
    public const string Record245 = @"M03_ADAPTER_DENIED 5 4 0 1";
    public const string Record246 = @"M03_ADAPTER_DENIED 6 4 0 1";
    public const string Record247 = @"M03_ADAPTER_DENIED 7 4 0 1";
    public const string Record248 = @"CLROS_M03_008_READY";
    public const string Record249 = @"TEST_PASS m03.008";
    public const string Record250 = @"TEST_START m03.004";
    public const string Record251 = @"M03_TYPED_EBS_IMAGE";
    public const string Record252 = @"M03_TYPED_EBS_ADMISSION";
    public const string Record253 = @"M03_TYPED_EBS_CASE id=0";
    public const string Record254 = @"M03_TYPED_EBS_CASE id=1";
    public const string Record255 = @"M03_TYPED_EBS_CASE id=2";
    public const string Record256 = @"M03_TYPED_EBS_CASE id=3";
    public const string Record257 = @"CLROS_M03_004_READY";
    public const string Record258 = @"TEST_PASS m03.004";
    public const string Record259 = @"TEST_START m03.005";
    public const string Record260 = @"M03_FINAL_MAP_IMAGE";
    public const string Record261 = @"M03_FINAL_MAP_CALL case=0 index=0";
    public const string Record262 = @"M03_FINAL_MAP_RESULT case=0";
    public const string Record263 = @"CLROS_M03_005_READY";
    public const string Record264 = @"TEST_PASS m03.005";
    public const string Record265 = @"TEST_START m03.006";
    public const string Record266 = @"M03_FINAL_MAP_CALL case=1 index=0";
    public const string Record267 = @"M03_FINAL_MAP_CALL case=1 index=1";
    public const string Record268 = @"M03_FINAL_MAP_RESULT case=1";
    public const string Record269 = @"M03_FINAL_MAP_CALL case=2 index=0";
    public const string Record270 = @"M03_FINAL_MAP_CALL case=2 index=1";
    public const string Record271 = @"M03_FINAL_MAP_RESULT case=2";
    public const string Record272 = @"CLROS_M03_006_READY";
    public const string Record273 = @"TEST_PASS m03.006";
    public const string Record274 = @"M04_ENDIAN_CONFORMANCE_FAILED";
    public const string Record275 = @"M04_SPAN_MEMORY_CONFORMANCE_FAILED";
    public const string Record276 = @"M04_ARITHMETIC_CONFORMANCE_FAILED";
    public const string Record277 = @"CLROS_M04_004_READY vectors=57 fingerprint=45FDFA24DD72EA28 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail";
    public const string Record278 = @"CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail";
    public const string Record279 = @"CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4 fingerprint=F03FF59D36F39ECD pointerWidth=8 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail";
    public const string Record280 = @"CLROS_M04_005_READY profile=ilc10.0.12-win-x64-le checks=19 shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail";
    public const string Record281 = @"TEST_START m04.006";
    public const string Record282 = @"M04_HEAP_PAGE_BOUND";
    public const string Record283 = @"M04_HEAP_PAGE_INITIALIZED";
    public const string Record284 = @"M04_HEAP_PAGE_RETAINED";
    public const string Record285 = @"M04_HEAP_PAGE_CURRENT";
    public const string Record286 = @"CLROS_M04_006_READY pages=1 bytes=4096 retained=true allocatorMutated=true zeroReadback=true checkpoint=beforeBootPlan hostConformance=false guestIntegrated=true objectAllocation=false gc=false hardwareNxProved=false postExitBootServices=false gate4=Fail";
    public const string Record287 = @"TEST_PASS m04.006";
    public const string Record288 = @"M04_GC_ROOT 0";
    public const string Record289 = @"M04_GC_ROOT 1";
    public const string Record290 = @"M04_GC_ROOT 7";
    public const string Record291 = @"TEST_START m04.024";
    public const string Record292 = @"M04_CCTOR 0";
    public const string Record293 = @"M04_CCTOR 1";
    public const string Record294 = @"M04_CCTOR 2";
    public const string Record295 = @"M04_CCTOR 3";
    public const string Record296 = @"M04_CCTOR 4";
    public const string Record297 = @"M04_CCTOR 5";
    public const string Record298 = @"M04_CCTOR 6";
    public const string Record299 = @"M04_CCTOR 7";
    public const string Record300 = @"M04_CCTOR 8";
    public const string Record301 = @"CLROS_M04_024_READY";
    public const string Record302 = @"TEST_PASS m04.024";
    public const string Record303 = @"M04_GC_ROOT 3";
    public const string Record304 = @"M04_GC_ROOT 4";
    public const string Record305 = @"M04_GC_ROOT 5";
    public const string Record306 = @"M04_GC_ROOT 6";
    public const string Record307 = @"M04_GC_ROOT 8";
    public const string Record308 = @"M04_COLLECT 0";
    public const string Record309 = @"M04_COLLECT 1";
    public const string Record310 = @"M04_COLLECT 2";
    public const string Record311 = @"M04_COLLECT 3";
    public const string Record312 = @"M04_COLLECT 4";
    public const string Record313 = @"M04_COLLECT 5";
    public const string Record314 = @"M04_COLLECT 6";
    public const string Record315 = @"M04_COLLECT 7";
    public const string Record316 = @"M04_COLLECT 8";
    public const string Record317 = @"CLROS_M04_013_READY";
    public const string Record318 = @"M04_LAYOUT_CONTRACT_FAILED";
    public const string Record319 = @"M04_HEAP_PAGE_RANGE_FAILED";
    public const string Record320 = @"TEST_FAIL m04.006";
    public const string Record321 = @"M04_RHP_";
    public const string Record322 = @"M04_ARRAY_";
    public const string Record323 = @"M04_STRING_";
    public const string Record324 = @"CLROS_M04_007";
    public const string Record325 = @"CLROS_M04_008";
    public const string Record326 = @"CLROS_M04_009";
    public const string Record327 = @"m04.007.rhp_new_fast";
    public const string Record328 = @"m04.008.sz_array";
    public const string Record329 = @"m04.009";
    public const string Record330 = @"TEST_START m04.010.references";
    public const string Record331 = @"M04_REFERENCE_PREP";
    public const string Record332 = @"M04_REFERENCE_BIND";
    public const string Record333 = @"M04_REFERENCE_OBJECT id=1";
    public const string Record334 = @"M04_REFERENCE_OBJECT id=2";
    public const string Record335 = @"M04_REFERENCE_OBJECT id=3";
    public const string Record336 = @"M04_REFERENCE_OBJECT id=4";
    public const string Record337 = @"M04_REFERENCE_OBJECT id=5";
    public const string Record338 = @"M04_REFERENCE_OBJECT id=6";
    public const string Record339 = @"M04_REFERENCE_GEOMETRY";
    public const string Record340 = @"M04_REFERENCE_OPERATION kind=2 calls=18";
    public const string Record341 = @"M04_REFERENCE_OPERATION kind=1 calls=19";
    public const string Record342 = @"M04_REFERENCE_OPERATION kind=2 calls=20";
    public const string Record343 = @"M04_REFERENCE_OPERATION kind=3 calls=22";
    public const string Record344 = @"M04_REFERENCE_OPERATION kind=4 calls=23";
    public const string Record345 = @"M04_REFERENCE_RETAIN";
    public const string Record346 = @"M04_REFERENCE_STACK";
    public const string Record347 = @"CLROS_M04_010_NATIVE_READY allocations=6";
    public const string Record348 = @"TEST_PASS m04.010.references";
    public const string Record349 = @"TEST_START m04.016";
    public const string Record350 = @"CLROS_M04_016_READY";
    public const string Record351 = @"TEST_PASS m04.016";
    public const string Record352 = @"M04_RHP_ATTEMPT";
    public const string Record353 = @"M04_RHP_REJECT";
    public const string Record354 = @"M04_RHP_INTERNAL_REJECT";
    public const string Record355 = @"M04_RHP_FILL";
    public const string Record356 = @"TEST_FAIL m04.007.rhp_new_fast";
    public const string Record357 = @"CLROS_M04_007_READY objects=10 compiledNew=4 direct=6 hostConformance=false guestIntegrated=true gate4=Fail";
    public const string Record358 = @"TEST_START m04.007.rhp_new_fast";
    public const string Record359 = @"M04_RHP_PREP";
    public const string Record360 = @"M04_RHP_BIND";
    public const string Record361 = @"M04_RHP_OBJECT id=1";
    public const string Record362 = @"M04_RHP_READBACK id=1";
    public const string Record363 = @"M04_RHP_OBJECT id=2";
    public const string Record364 = @"M04_RHP_READBACK id=2";
    public const string Record365 = @"M04_RHP_OBJECT id=3";
    public const string Record366 = @"M04_RHP_READBACK id=3";
    public const string Record367 = @"M04_RHP_OBJECT id=4";
    public const string Record368 = @"M04_RHP_READBACK id=4";
    public const string Record369 = @"M04_RHP_OBJECT id=5";
    public const string Record370 = @"M04_RHP_READBACK id=5";
    public const string Record371 = @"M04_RHP_OBJECT id=6";
    public const string Record372 = @"M04_RHP_READBACK id=6";
    public const string Record373 = @"M04_RHP_OBJECT id=7";
    public const string Record374 = @"M04_RHP_READBACK id=7";
    public const string Record375 = @"M04_RHP_OBJECT id=8";
    public const string Record376 = @"M04_RHP_READBACK id=8";
    public const string Record377 = @"M04_RHP_OBJECT id=9";
    public const string Record378 = @"M04_RHP_READBACK id=9";
    public const string Record379 = @"M04_RHP_OBJECT id=10";
    public const string Record380 = @"M04_RHP_READBACK id=10";
    public const string Record381 = @"M04_RHP_RETAIN";
    public const string Record382 = @"TEST_PASS m04.007.rhp_new_fast";
    public const string Record383 = @"M04_CHURN 0";
    public const string Record384 = @"M04_CHURN 1";
    public const string Record385 = @"M04_CHURN 2";
    public const string Record386 = @"M04_CHURN 3";
    public const string Record387 = @"M04_CHURN 4";
    public const string Record388 = @"M04_CHURN 5";
    public const string Record389 = @"M04_CHURN 6";
    public const string Record390 = @"M04_CHURN 7";
    public const string Record391 = @"M04_CHURN 8";
    public const string Record392 = @"M04_CHURN 9";
    public const string Record393 = @"M04_CHURN 10";
    public const string Record394 = @"CLROS_M04_014_READY";
    public const string Record395 = @"TEST_START m04.021.simple-catch";
    public const string Record396 = @"M04_CATCH 0";
    public const string Record397 = @"M04_CATCH 1";
    public const string Record398 = @"M04_CATCH 2";
    public const string Record399 = @"M04_CATCH 3";
    public const string Record400 = @"M04_CATCH 4";
    public const string Record401 = @"M04_CATCH 5";
    public const string Record402 = @"M04_CATCH 6";
    public const string Record403 = @"M04_CATCH 7";
    public const string Record404 = @"M04_CATCH 8";
    public const string Record405 = @"TEST_PASS m04.021.simple-catch";
    public const string Record406 = @"CLROS_M04_SIMPLE_CATCH_READY";
    public const string Record407 = @"TEST_START m04.021.single-finally";
    public const string Record408 = @"M04_FINALLY 0";
    public const string Record409 = @"M04_FINALLY 1";
    public const string Record410 = @"M04_FINALLY 2";
    public const string Record411 = @"M04_FINALLY 3";
    public const string Record412 = @"M04_FINALLY 4";
    public const string Record413 = @"M04_FINALLY 9";
    public const string Record414 = @"M04_FINALLY 10";
    public const string Record415 = @"M04_FINALLY 5";
    public const string Record416 = @"M04_FINALLY 6";
    public const string Record417 = @"M04_FINALLY 7";
    public const string Record418 = @"M04_FINALLY 8";
    public const string Record419 = @"TEST_PASS m04.021.single-finally";
    public const string Record420 = @"CLROS_M04_SINGLE_FINALLY_READY";
    public const string Record421 = @"M04_STRING_ATTEMPT";
    public const string Record422 = @"M04_STRING_REJECT";
    public const string Record423 = @"M04_STRING_INTERNAL_REJECT";
    public const string Record424 = @"M04_STRING_FILL";
    public const string Record425 = @"TEST_FAIL m04.009";
    public const string Record426 = @"CLROS_M04_009_READY allocations=7 usedBytes=200 sharedState=true strings=5 utf16=true terminatorZero=true paddingZero=true priorObjectsPreserved=true";
    public const string Record427 = @"TEST_START m04.009";
    public const string Record428 = @"M04_STRING_PREP";
    public const string Record429 = @"M04_STRING_BIND";
    public const string Record430 = @"M04_STRING_OBJECT id=1";
    public const string Record431 = @"M04_STRING_READBACK id=1";
    public const string Record432 = @"M04_STRING_OBJECT id=2";
    public const string Record433 = @"M04_STRING_READBACK id=2";
    public const string Record434 = @"M04_STRING_OBJECT id=3";
    public const string Record435 = @"M04_STRING_READBACK id=3";
    public const string Record436 = @"M04_STRING_OBJECT id=4";
    public const string Record437 = @"M04_STRING_READBACK id=4";
    public const string Record438 = @"M04_STRING_OBJECT id=5";
    public const string Record439 = @"M04_STRING_READBACK id=5";
    public const string Record440 = @"M04_STRING_OBJECT id=6";
    public const string Record441 = @"M04_STRING_READBACK id=6";
    public const string Record442 = @"M04_STRING_OBJECT id=7";
    public const string Record443 = @"M04_STRING_READBACK id=7";
    public const string Record444 = @"M04_STRING_RETAIN";
    public const string Record445 = @"TEST_PASS m04.009";
    public const string Record446 = @"M04_ARRAY_ATTEMPT";
    public const string Record447 = @"M04_ARRAY_REJECT";
    public const string Record448 = @"M04_ARRAY_INTERNAL_REJECT";
    public const string Record449 = @"M04_ARRAY_FILL";
    public const string Record450 = @"TEST_FAIL m04.008.sz_array";
    public const string Record451 = @"CLROS_M04_008_READY objects=5 fixedNew=2 byteNew=2 longNew=1 used=160 count=5 hostConformance=false guestIntegrated=true gate4=Fail";
    public const string Record452 = @"TEST_START m04.008.sz_array";
    public const string Record453 = @"M04_ARRAY_PREP";
    public const string Record454 = @"M04_ARRAY_BIND";
    public const string Record455 = @"M04_ARRAY_OBJECT id=1";
    public const string Record456 = @"M04_ARRAY_READBACK id=1";
    public const string Record457 = @"M04_ARRAY_OBJECT id=2";
    public const string Record458 = @"M04_ARRAY_READBACK id=2";
    public const string Record459 = @"M04_ARRAY_OBJECT id=3";
    public const string Record460 = @"M04_ARRAY_READBACK id=3";
    public const string Record461 = @"M04_ARRAY_OBJECT id=4";
    public const string Record462 = @"M04_ARRAY_READBACK id=4";
    public const string Record463 = @"M04_ARRAY_OBJECT id=5";
    public const string Record464 = @"M04_ARRAY_READBACK id=5";
    public const string Record465 = @"M04_ARRAY_RETAIN";
    public const string Record466 = @"TEST_PASS m04.008.sz_array";
    public const string Record467 = @"TEST_START m04.011";
    public const string Record468 = @"CLROS_M04_011_IMAGE";
    public const string Record469 = @"CLROS_M04_011_VALIDATE";
    public const string Record470 = @"CLROS_M04_011_REJECT kind=capacity";
    public const string Record471 = @"CLROS_M04_011_REGISTER";
    public const string Record472 = @"CLROS_M04_011_SECTION id=202";
    public const string Record473 = @"CLROS_M04_011_SECTION id=204";
    public const string Record474 = @"CLROS_M04_011_SECTION id=213";
    public const string Record475 = @"CLROS_M04_011_SECTION id=0";
    public const string Record476 = @"CLROS_M04_011_REJECT kind=repeat";
    public const string Record477 = @"CLROS_M04_011_REJECT kind=foreign";
    public const string Record478 = @"CLROS_M04_011_READY";
    public const string Record479 = @"TEST_PASS m04.011";
    public const string Record480 = @"TEST_START m04.020";
    public const string Record481 = @"CLROS_M04_020_DATA stage=0";
    public const string Record482 = @"CLROS_M04_020_DATA stage=1";
    public const string Record483 = @"CLROS_M04_020_DATA stage=2";
    public const string Record484 = @"CLROS_M04_020_DATA stage=3";
    public const string Record485 = @"CLROS_M04_020_DATA stage=4";
    public const string Record486 = @"CLROS_M04_020_DATA stage=5";
    public const string Record487 = @"CLROS_M04_020_DATA stage=6";
    public const string Record488 = @"CLROS_M04_020_DATA stage=7";
    public const string Record489 = @"CLROS_M04_020_DATA stage=8";
    public const string Record490 = @"CLROS_M04_020_DATA stage=9";
    public const string Record491 = @"CLROS_M04_020_DATA stage=10";
    public const string Record492 = @"CLROS_M04_020_DATA stage=11";
    public const string Record493 = @"CLROS_M04_020_DATA stage=12";
    public const string Record494 = @"CLROS_M04_020_DATA stage=13";
    public const string Record495 = @"CLROS_M04_020_DATA stage=14";
    public const string Record496 = @"CLROS_M04_020_DATA stage=15";
    public const string Record497 = @"CLROS_M04_020_DATA stage=16";
    public const string Record498 = @"CLROS_M04_020_DATA stage=17";
    public const string Record499 = @"CLROS_M04_020_DATA stage=18";
    public const string Record500 = @"CLROS_M04_020_READY methods=2 lookups=4 version=1 hostConformance=false guestIntegrated=true unwindExecution=false";
    public const string Record501 = @"TEST_PASS m04.020";
    public const string Record502 = @"APP_PACKAGE_READY";
    public const string Record503 = @"APP_LAUNCH_READY";
    public const string Record504 = @"APP_PAYLOAD_READY";
    public const string Record505 = @"APP_EXEC_MAPPING_READY";
    public const string Record506 = @"APP_EXEC_MEMORY_READY";
    public const string Record507 = @"APP_INVOCATION_READY";
    public const string Record508 = @"APP_ENTRY_READY";

    public static IReadOnlyList<string> M02CheckedPortIoBindingForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024, Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033, Record034]);
    public static IReadOnlyList<string> M02CheckedPortIoBindingReadyMarker { get; } = Array.AsReadOnly<string>([Record035]);
    public static IReadOnlyList<string> M02CheckedPortIoBindingRequiredMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record035, Record081, Record082, Record083]);
    public static IReadOnlyList<string> M02Com1OwnedPortIoForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024, Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033, Record034, Record084]);
    public static IReadOnlyList<string> M02Com1OwnedPortIoReadyMarker { get; } = Array.AsReadOnly<string>([Record085]);
    public static IReadOnlyList<string> M02Com1OwnedPortIoRequiredMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record035, Record085, Record081, Record082, Record083]);
    public static IReadOnlyList<string> M02DoubleFaultIstForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record019, Record020, Record086, Record087, Record088, Record089, Record090, Record091]);
    public static IReadOnlyList<string> M02DoubleFaultIstOrderedMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record094, Record095, Record096, Record097, Record098, Record099, Record100, Record101, Record102, Record103, Record104, Record105, Record106, Record107]);
    public static IReadOnlyList<string> M02DoubleFaultIstReadyMarker { get; } = Array.AsReadOnly<string>([Record106]);
    public static IReadOnlyList<string> M02DoubleFaultIstRequiredMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record094, Record095, Record096, Record097, Record098, Record099, Record100, Record101, Record102, Record103, Record104, Record105, Record106, Record107]);
    public static IReadOnlyList<string> M02DoubleFaultIstUniqueMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record094, Record095, Record096, Record097, Record098, Record099, Record100, Record101, Record102, Record103, Record104, Record105, Record106, Record107]);
    public static IReadOnlyList<string> M02GuardedIstStackAllocationForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024, Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033, Record034, Record084, Record108, Record109, Record110, Record111]);
    public static IReadOnlyList<string> M02GuardedIstStackAllocationReadyMarker { get; } = Array.AsReadOnly<string>([Record112]);
    public static IReadOnlyList<string> M02GuardedIstStackAllocationRequiredMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record035, Record085, Record113, Record114, Record115, Record116, Record117, Record112, Record081, Record082, Record083]);
    public static IReadOnlyList<string> M02IdtrContinuedExecutionForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record118, Record019, Record119, Record120]);
    public static IReadOnlyList<string> M02IdtrContinuedExecutionHostOnlyMarker { get; } = Array.AsReadOnly<string>([Record119]);
    public static IReadOnlyList<string> M02IdtrContinuedExecutionOrderedMarkers { get; } = Array.AsReadOnly<string>([Record121, Record122, Record123, Record124, Record125, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M02IdtrContinuedExecutionReadyMarker { get; } = Array.AsReadOnly<string>([Record123]);
    public static IReadOnlyList<string> M02IdtrContinuedExecutionRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record122, Record123, Record124, Record125, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M02IdtrContinuedExecutionUniqueMarkers { get; } = Array.AsReadOnly<string>([Record122, Record123, Record124, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M02NormalizedInterruptFramePageForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record135, Record019]);
    public static IReadOnlyList<string> M02NormalizedInterruptFramePageReadyMarker { get; } = Array.AsReadOnly<string>([Record136]);
    public static IReadOnlyList<string> M02NormalizedInterruptFramePageRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record137, Record136, Record138, Record125]);
    public static IReadOnlyList<string> M02NormalizedInterruptFramePageUniqueMarkers { get; } = Array.AsReadOnly<string>([Record137, Record136, Record138]);
    public static IReadOnlyList<string> M02OwnedCom1ReadyEvidenceMarker { get; } = Array.AsReadOnly<string>([Record114]);
    public static IReadOnlyList<string> M02OwnedCom1ReadyForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024, Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033, Record034, Record084, Record108]);
    public static IReadOnlyList<string> M02OwnedCom1ReadyRequiredMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record035, Record085, Record113, Record114, Record081, Record082, Record083]);
    public static IReadOnlyList<string> M02OwnedCom1ReadyStandaloneMarker { get; } = Array.AsReadOnly<string>([Record113]);
    public static IReadOnlyList<string> M02OwnedGdtTssTerminalForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024, Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033, Record034, Record084, Record108, Record109, Record110, Record111, Record139, Record140, Record141]);
    public static IReadOnlyList<string> M02OwnedGdtTssTerminalReadyMarker { get; } = Array.AsReadOnly<string>([Record142]);
    public static IReadOnlyList<string> M02OwnedGdtTssTerminalRequiredMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record035, Record085, Record113, Record114, Record115, Record116, Record117, Record112, Record081, Record082, Record083, Record143, Record142, Record144]);
    public static IReadOnlyList<string> M02OwnedIdtGatePopulationForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record145, Record019]);
    public static IReadOnlyList<string> M02OwnedIdtGatePopulationReadyMarker { get; } = Array.AsReadOnly<string>([Record120]);
    public static IReadOnlyList<string> M02OwnedIdtGatePopulationRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record146, Record120, Record147, Record125]);
    public static IReadOnlyList<string> M02OwnedIdtGatePopulationUniqueMarkers { get; } = Array.AsReadOnly<string>([Record146, Record120, Record147]);
    public static IReadOnlyList<string> M02OwnedX64GdtEncodingForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024, Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033, Record034, Record084, Record108, Record109, Record110]);
    public static IReadOnlyList<string> M02OwnedX64GdtEncodingReadyMarker { get; } = Array.AsReadOnly<string>([Record117]);
    public static IReadOnlyList<string> M02OwnedX64GdtEncodingRequiredMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record035, Record085, Record113, Record114, Record115, Record116, Record117, Record081, Record082, Record083]);
    public static IReadOnlyList<string> M02PageFaultCr2ErrorCodeForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record019, Record020, Record148, Record149, Record150, Record151, Record152, Record091]);
    public static IReadOnlyList<string> M02PageFaultCr2ErrorCodeOrderedMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record153, Record154, Record155, Record156, Record157, Record158, Record159, Record160, Record161, Record162, Record163, Record164]);
    public static IReadOnlyList<string> M02PageFaultCr2ErrorCodeReadyMarker { get; } = Array.AsReadOnly<string>([Record163]);
    public static IReadOnlyList<string> M02PageFaultCr2ErrorCodeRequiredMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record153, Record154, Record155, Record156, Record157, Record158, Record159, Record160, Record161, Record162, Record163, Record164]);
    public static IReadOnlyList<string> M02PageFaultCr2ErrorCodeUniqueMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record153, Record154, Record155, Record156, Record157, Record158, Record159, Record160, Record161, Record162, Record163, Record164]);
    public static IReadOnlyList<string> M02PanicSafeOwnedCom1EvidenceMarker { get; } = Array.AsReadOnly<string>([Record116]);
    public static IReadOnlyList<string> M02PanicSafeOwnedCom1ForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024, Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033, Record034, Record084, Record108, Record109]);
    public static IReadOnlyList<string> M02PanicSafeOwnedCom1ReadyMarker { get; } = Array.AsReadOnly<string>([Record115]);
    public static IReadOnlyList<string> M02PanicSafeOwnedCom1RequiredMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record035, Record085, Record113, Record114, Record115, Record116, Record081, Record082, Record083]);
    public static IReadOnlyList<string> M02SafeStubForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024, Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033]);
    public static IReadOnlyList<string> M02SafeStubRequiredMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record081, Record082, Record083]);
    public static IReadOnlyList<string> M02Ud2VectorStateForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record165, Record166, Record019, Record167, Record168, Record136, Record120, Record123]);
    public static IReadOnlyList<string> M02Ud2VectorStateHostOnlyMarker { get; } = Array.AsReadOnly<string>([Record168]);
    public static IReadOnlyList<string> M02Ud2VectorStateHostOnlyMarkerPrefix { get; } = Array.AsReadOnly<string>([Record167]);
    public static IReadOnlyList<string> M02Ud2VectorStateOrderedMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record169, Record170, Record171]);
    public static IReadOnlyList<string> M02Ud2VectorStateReadyMarker { get; } = Array.AsReadOnly<string>([Record170]);
    public static IReadOnlyList<string> M02Ud2VectorStateRequiredMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record169, Record170, Record171]);
    public static IReadOnlyList<string> M02Ud2VectorStateUniqueMarkers { get; } = Array.AsReadOnly<string>([Record092, Record093, Record083, Record169, Record170, Record171]);
    public static IReadOnlyList<string> M03AcpiBootstrapRequiredMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record174, Record175, Record176, Record177, Record178, Record179, Record180, Record181, Record182, Record183, Record184, Record185, Record186, Record187, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M03FirmwareEntropyRequiredMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record193, Record194, Record195, Record196, Record197, Record198, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M03FirstExitRequiredMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record174, Record175, Record176, Record177, Record178, Record179, Record180, Record181, Record182, Record183, Record184, Record185, Record186, Record187, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083, Record199, Record200, Record201, Record202, Record203, Record204, Record205, Record206, Record207, Record208, Record209]);
    public static IReadOnlyList<string> M03FirstExitStageMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055]);
    public static IReadOnlyList<string> M03FirstExitTerminalMarkers { get; } = Array.AsReadOnly<string>([Record199, Record200, Record201, Record202, Record203, Record204, Record205, Record206, Record207, Record208, Record209]);
    public static IReadOnlyList<string> M03MemoryMapCaptureRequiredMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record210, Record211, Record212, Record213, Record214, Record215, Record216, Record217, Record218, Record219, Record125, Record220, Record126, Record127, Record128, Record129, Record130, Record221, Record222, Record133, Record134, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M03NormalVerifiedExitMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record174, Record175, Record176, Record177, Record178, Record179, Record180, Record181, Record182, Record183, Record184, Record185, Record186, Record187, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record188, Record189, Record190, Record191, Record192, Record199, Record200, Record201, Record202, Record203, Record204, Record205, Record206, Record207, Record208, Record209, Record223, Record224, Record225, Record226, Record227, Record228, Record229, Record230, Record231, Record232, Record233, Record234, Record235, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M03OwnedSubsystemMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record174, Record175, Record176, Record177, Record178, Record179, Record180, Record181, Record182, Record183, Record184, Record185, Record186, Record187, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083, Record199, Record200, Record201, Record202, Record203, Record204, Record205, Record206, Record207, Record208, Record209, Record223, Record224, Record225, Record226, Record227, Record228, Record229, Record230, Record231, Record232]);
    public static IReadOnlyList<string> M03OwnedSubsystemPanicMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record174, Record175, Record176, Record177, Record178, Record179, Record180, Record181, Record182, Record183, Record184, Record185, Record186, Record187, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083, Record199, Record200, Record201, Record202, Record203, Record204, Record205, Record206, Record207, Record208, Record209, Record223, Record224, Record225, Record226, Record227, Record228, Record229, Record230, Record236, Record237]);
    public static IReadOnlyList<string> M03RevocationRequiredMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record174, Record175, Record176, Record177, Record178, Record179, Record180, Record181, Record182, Record183, Record184, Record185, Record186, Record187, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083, Record199, Record238, Record200, Record201, Record202, Record203, Record204, Record205, Record206, Record207, Record208, Record209, Record239, Record240, Record241, Record242, Record243, Record244, Record245, Record246, Record247, Record248, Record249]);
    public static IReadOnlyList<string> M03RevocationTerminalMarkers { get; } = Array.AsReadOnly<string>([Record239, Record240, Record241, Record242, Record243, Record244, Record245, Record246, Record247, Record248, Record249]);
    public static IReadOnlyList<string> M03TypedExitBootServicesRequiredMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record250, Record251, Record252, Record253, Record254, Record255, Record256, Record257, Record258, Record259, Record260, Record261, Record262, Record263, Record264, Record265, Record266, Record267, Record268, Record269, Record270, Record271, Record272, Record273, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04ArithmeticConformanceForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record275, Record276, Record019]);
    public static IReadOnlyList<string> M04ArithmeticConformanceReadyMarker { get; } = Array.AsReadOnly<string>([Record277]);
    public static IReadOnlyList<string> M04ArithmeticConformanceRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record125]);
    public static IReadOnlyList<string> M04ArithmeticConformanceUniqueMarkers { get; } = Array.AsReadOnly<string>([Record278, Record279, Record277]);
    public static IReadOnlyList<string> M04CctorRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record288, Record289, Record290, Record291, Record292, Record293, Record294, Record295, Record296, Record297, Record298, Record299, Record300, Record301, Record302, Record303, Record304, Record305, Record306, Record307, Record308, Record309, Record310, Record311, Record312, Record313, Record314, Record315, Record316, Record317, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04CheckedReferenceBarrierForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record275, Record276, Record318, Record019, Record020, Record319, Record320, Record019, Record020, Record321, Record322, Record323, Record324, Record325, Record326, Record327, Record328, Record329]);
    public static IReadOnlyList<string> M04CheckedReferenceBarrierRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record330, Record331, Record332, Record333, Record334, Record335, Record336, Record337, Record338, Record339, Record340, Record341, Record342, Record343, Record344, Record345, Record346, Record347, Record348, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04DispatchRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record288, Record289, Record290, Record291, Record292, Record293, Record294, Record295, Record296, Record297, Record298, Record299, Record300, Record301, Record302, Record303, Record304, Record305, Record306, Record307, Record308, Record309, Record310, Record311, Record312, Record313, Record314, Record315, Record316, Record317, Record349, Record350, Record351, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04GcStaticRootRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record288, Record289, Record290, Record303, Record304, Record305, Record306, Record307, Record308, Record309, Record310, Record311, Record312, Record313, Record314, Record315, Record316, Record317, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04IndependentEndianConformanceForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record019]);
    public static IReadOnlyList<string> M04IndependentEndianConformanceReadyMarker { get; } = Array.AsReadOnly<string>([Record278]);
    public static IReadOnlyList<string> M04IndependentEndianConformanceRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record125]);
    public static IReadOnlyList<string> M04LayoutContractForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record275, Record276, Record318, Record019, Record020]);
    public static IReadOnlyList<string> M04LayoutContractReadyMarker { get; } = Array.AsReadOnly<string>([Record280]);
    public static IReadOnlyList<string> M04LayoutContractRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125]);
    public static IReadOnlyList<string> M04LayoutContractUniqueMarkers { get; } = Array.AsReadOnly<string>([Record278, Record279, Record277, Record280]);
    public static IReadOnlyList<string> M04ManagedHeapPageRangeForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record275, Record276, Record318, Record019, Record020, Record319, Record320]);
    public static IReadOnlyList<string> M04ManagedHeapPageRangeReadyMarker { get; } = Array.AsReadOnly<string>([Record286]);
    public static IReadOnlyList<string> M04ManagedHeapPageRangeRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04RhpNewFastForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record275, Record276, Record318, Record019, Record020, Record319, Record320, Record352, Record353, Record354, Record355, Record356]);
    public static IReadOnlyList<string> M04RhpNewFastReadyMarker { get; } = Array.AsReadOnly<string>([Record357]);
    public static IReadOnlyList<string> M04RhpNewFastRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record358, Record359, Record360, Record361, Record362, Record363, Record364, Record365, Record366, Record367, Record368, Record369, Record370, Record371, Record372, Record373, Record374, Record375, Record376, Record377, Record378, Record379, Record380, Record381, Record357, Record382, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04SeededChurnRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record288, Record289, Record290, Record303, Record304, Record305, Record306, Record307, Record308, Record309, Record310, Record311, Record312, Record313, Record314, Record315, Record316, Record317, Record383, Record384, Record385, Record386, Record387, Record388, Record389, Record390, Record391, Record392, Record393, Record394, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04SimpleCatchRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record288, Record289, Record290, Record303, Record304, Record305, Record306, Record307, Record308, Record309, Record310, Record311, Record312, Record313, Record314, Record315, Record316, Record317, Record395, Record396, Record397, Record398, Record399, Record400, Record401, Record402, Record403, Record404, Record405, Record406, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04SingleFinallyRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record288, Record289, Record290, Record303, Record304, Record305, Record306, Record307, Record308, Record309, Record310, Record311, Record312, Record313, Record314, Record315, Record316, Record317, Record407, Record408, Record409, Record410, Record411, Record412, Record413, Record414, Record415, Record416, Record417, Record418, Record419, Record420, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04SpanMemoryConformanceForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record275, Record019]);
    public static IReadOnlyList<string> M04SpanMemoryConformanceReadyMarker { get; } = Array.AsReadOnly<string>([Record279]);
    public static IReadOnlyList<string> M04SpanMemoryConformanceRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record125]);
    public static IReadOnlyList<string> M04SpanMemoryConformanceUniqueMarkers { get; } = Array.AsReadOnly<string>([Record278, Record279]);
    public static IReadOnlyList<string> M04StringAllocationForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record275, Record276, Record318, Record019, Record020, Record319, Record320, Record352, Record353, Record354, Record355, Record356, Record321, Record324, Record327, Record322, Record325, Record328, Record421, Record422, Record423, Record424, Record425]);
    public static IReadOnlyList<string> M04StringAllocationReadyMarker { get; } = Array.AsReadOnly<string>([Record426]);
    public static IReadOnlyList<string> M04StringAllocationRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record427, Record428, Record429, Record430, Record431, Record432, Record433, Record434, Record435, Record436, Record437, Record438, Record439, Record440, Record441, Record442, Record443, Record444, Record426, Record445, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04SzArrayAllocationForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record274, Record275, Record276, Record318, Record019, Record020, Record319, Record320, Record352, Record353, Record354, Record355, Record356, Record321, Record324, Record327, Record446, Record447, Record448, Record449, Record450]);
    public static IReadOnlyList<string> M04SzArrayAllocationReadyMarker { get; } = Array.AsReadOnly<string>([Record451]);
    public static IReadOnlyList<string> M04SzArrayAllocationRequiredMarkers { get; } = Array.AsReadOnly<string>([Record121, Record278, Record279, Record277, Record280, Record125, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record281, Record282, Record283, Record284, Record285, Record286, Record287, Record452, Record453, Record454, Record455, Record456, Record457, Record458, Record459, Record460, Record461, Record462, Record463, Record464, Record465, Record451, Record466, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04TypeManagerRequiredMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record467, Record468, Record469, Record470, Record471, Record472, Record473, Record474, Record475, Record476, Record477, Record478, Record479, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> M04X64UnwindMetadataRequiredMarkers { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record467, Record468, Record469, Record470, Record471, Record472, Record473, Record474, Record475, Record476, Record477, Record478, Record479, Record480, Record481, Record482, Record483, Record484, Record485, Record486, Record487, Record488, Record489, Record490, Record491, Record492, Record493, Record494, Record495, Record496, Record497, Record498, Record499, Record500, Record501, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);
    public static IReadOnlyList<string> nativeAppRequiredMarkers { get; } = Array.AsReadOnly<string>([Record502, Record503, Record504, Record505, Record506, Record507, Record508]);
    public static IReadOnlyList<string> M02TerminalPrefix { get; } = Array.AsReadOnly<string>([Record172, Record121, Record125, Record173, Record126, Record127, Record128, Record129, Record130, Record131, Record132, Record133, Record134, Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record061, Record062, Record063, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record078, Record079, Record080, Record035, Record085, Record113, Record114, Record115, Record116, Record081, Record082, Record188, Record189, Record190, Record191, Record192, Record092, Record093, Record083]);

    public static IReadOnlyList<string> M01StageOneGuestMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record083]);
    public static IReadOnlyList<string> M01StageTwoGuestMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record083]);
    public static IReadOnlyList<string> M01StageTwoForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record019, Record020, Record021, Record022, Record023]);
    public static IReadOnlyList<string> M01StageThreeGuestMarkers { get; } = Array.AsReadOnly<string>([Record036, Record037, Record038, Record039, Record040, Record041, Record042, Record043, Record044, Record045, Record046, Record047, Record048, Record049, Record050, Record051, Record052, Record053, Record054, Record055, Record056, Record057, Record058, Record059, Record060, Record064, Record065, Record066, Record067, Record068, Record069, Record070, Record071, Record072, Record073, Record074, Record075, Record076, Record077, Record081, Record082, Record083]);
    public static IReadOnlyList<string> M01StageThreeForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record000, Record001, Record002, Record003, Record004, Record005, Record006, Record007, Record008, Record009, Record010, Record011, Record012, Record013, Record014, Record015, Record016, Record017, Record018, Record019, Record020, Record021, Record022, Record023, Record024]);
    public const string Record608 = "M02_STUB_PAGE_MATERIALIZED pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E";
    public const string Record609 = "M02_STUB_PAGE_READBACK_VERIFIED fnv1a64=F85393074A792B1E";
    public const string Record610 = "CLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E temporaryOwnedRootRx=true portIoExecuted=false";
    public static IReadOnlyList<string> M02HistoricalManifestSuccessMarkers { get; } = Array.AsReadOnly<string>([Record061, Record608, Record609, Record078, Record610, Record080]);
    public static IReadOnlyList<string> M02IssueSuccessMarkers { get; } = Array.AsReadOnly<string>([Record061, Record062, Record063, Record078, Record079, Record080]);
    public static IReadOnlyList<string> M02IssueForbiddenMarkers { get; } = Array.AsReadOnly<string>([Record025, Record026, Record027, Record028, Record029, Record030, Record031, Record032, Record033]);
    public static IReadOnlyList<string> M04RethrowRequiredMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M04GcStaticRootRequiredMarkers.Take(M04GcStaticRootRequiredMarkers.Count - 8),
        "TEST_START m04.021.rethrow",
        .. new[] { 0, 1, 2, 3, 4, 9, 10, 5, 14, 11, 12, 13, 6, 7, 8 }.Select(stage => "M04_RETHROW " + stage),
        "TEST_PASS m04.021.rethrow", "CLROS_M04_RETHROW_READY",
        .. M04GcStaticRootRequiredMarkers.TakeLast(8)
    ]);
    public static IReadOnlyList<string> M05PhysicalMappingMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M03OwnedSubsystemMarkers, "TEST_START m05.001", .. Enumerable.Range(0, 7).Select(stage => "M05_MAP " + stage),
        "CLROS_M05_001_READY", "TEST_PASS m05.001"
    ]);
    public static IReadOnlyList<string> M05OwnedAcpiSelectionMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M03OwnedSubsystemMarkers, "TEST_START m05.002", .. Enumerable.Range(0, 4).Select(stage => "M05_ACPI " + stage),
        "CLROS_M05_002_READY", "TEST_PASS m05.002"
    ]);
    public static IReadOnlyList<string> M05PciEnumerationMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M03OwnedSubsystemMarkers,
        "TEST_START m05.004", "M05_PCI 0", "M05_PCI 1", "CLROS_M05_004_READY", "TEST_PASS m05.004",
        "TEST_START m05.005", "M05_PCI 2", "M05_PCI 3", "CLROS_M05_005_READY", "TEST_PASS m05.005",
        "TEST_START m05.006", "M05_PCI 4", "M05_PCI 5", "M05_PCI 6", "CLROS_M05_006_READY", "TEST_PASS m05.006",
        "TEST_START m05.007", "M05_PCI 7", "M05_PCI 8", "CLROS_M05_007_READY", "TEST_PASS m05.007"
    ]);

    public static IReadOnlyList<string> M06VirtioBlockQueueSetupMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M05PciEnumerationMarkers,
        "TEST_START m06.001", "M06_BLK 0", "M06_BLK 1", "M06_BLK 2", "CLROS_M06_001_READY", "TEST_PASS m06.001",
        "TEST_START m06.002", "M06_BLK 3", "M06_BLK 4", "CLROS_M06_002_READY", "TEST_PASS m06.002",
        "TEST_START m06.003", "M06_BLK 5", "M06_BLK 6", "M06_BLK 7", "CLROS_M06_003_READY", "TEST_PASS m06.003",
        "TEST_START m06.004", "M06_BLK 8", "M06_BLK 9", "M06_BLK A", "M06_BLK B", "M06_BLK C", "CLROS_M06_004_READY", "TEST_PASS m06.004"
    ]);

    public static IReadOnlyList<string> M06VirtioBlockReadMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M06VirtioBlockQueueSetupMarkers,
        "TEST_START m06.005", .. Enumerable.Range(0, 6).Select(i => "M06_READ " + i), "CLROS_M06_005_READY", "TEST_PASS m06.005",
        "TEST_START m06.006", .. Enumerable.Range(6, 4).Select(i => "M06_READ " + i), "CLROS_M06_006_READY", "TEST_PASS m06.006"
    ]);

    public static IReadOnlyList<string> M06GptPartitionMarkers(bool primaryHeaderFault = false) => Array.AsReadOnly<string>(
    [
        .. M06LiveBlockContractMarkers,
        "TEST_START m06.009", .. Enumerable.Range(0x80, primaryHeaderFault ? 1 : 33).Select(i => "M06_GPT " + i.ToString("X2")),
        "M06_GPT 00", "M06_GPT 01", "CLROS_M06_009_READY", "TEST_PASS m06.009",
        "TEST_START m06.010", .. primaryHeaderFault ? Enumerable.Range(0x81, 33).Select(i => "M06_GPT " + i.ToString("X2")) : [],
        "M06_GPT 02", "M06_GPT 03", "M06_GPT 04", "CLROS_M06_010_READY", "TEST_PASS m06.010",
        "TEST_START m06.011", "M06_GPT 05", "M06_GPT 06", "CLROS_M06_011_READY", "TEST_PASS m06.011"
    ]);

    public static IReadOnlyList<string> M06FatReadMarkers() => Array.AsReadOnly<string>(
    [
        .. M06GptPartitionMarkers(),
        "TEST_START m06.012", "M06_FAT 80", "M06_FAT 00", "M06_FAT 01",
        .. Enumerable.Range(0x81, 4).Select(i => "M06_FAT " + i.ToString("X2")),
        "M06_FAT 02", "CLROS_M06_012_READY", "TEST_PASS m06.012",
        "TEST_START m06.013", "M06_FAT 85", "M06_FAT 86", "M06_FAT 03", "M06_FAT 04", "M06_FAT 05",
        "CLROS_M06_013_READY", "TEST_PASS m06.013",
        "TEST_START m06.014", .. Enumerable.Range(0x87, 9).Select(i => "M06_FAT " + i.ToString("X2")),
        "M06_FAT 06", "M06_FAT 07", "M06_FAT 08", "CLROS_M06_014_READY", "TEST_PASS m06.014"
    ]);

    public static IReadOnlyList<string> M06PackageReadMarkers(string variant = "Intact")
    {
        if (variant is not ("Intact" or "ManifestVersion" or "PackageChecksum" or "PackageHeader" or "PackageTruncated" or "PackageLoop"))
            throw new ArgumentOutOfRangeException(nameof(variant), variant, "package_image_variant_invalid");
        List<string> markers = [.. M06FatReadMarkers(), "TEST_START m06.015", "M06_PKG 80", "M06_PKG 00",
            .. Enumerable.Range(0x81, 6).Select(i => "M06_PKG " + i.ToString("X2")), "M06_PKG 01",
            "M06_PKG 87", "M06_PKG 88", "M06_PKG 89", "M06_PKG 02",
            "M06_PKG 8A", "M06_PKG 8B", "M06_PKG 8C"];
        if (variant != "ManifestVersion")
        {
            markers.AddRange(["M06_PKG 03", "CLROS_M06_015_READY", "TEST_PASS m06.015", "TEST_START m06.016",
                "M06_PKG 8D", "M06_PKG 8E", "M06_PKG 8F"]);
            if (variant == "Intact")
                markers.AddRange(["M06_PKG 04", "CLROS_M06_016_READY", "TEST_PASS m06.016", "TEST_START m06.017",
                    "M06_PKG 05", "M06_PKG 06", "M06_PKG 07", "CLROS_M06_017_READY", "TEST_PASS m06.017"]);
        }
        markers.Add("TEST_START m06.018");
        if (variant != "Intact") markers.Add("M06_PKG 08");
        markers.AddRange(["M06_PKG 09", "CLROS_M06_018_READY", "TEST_PASS m06.018"]);
        return markers.AsReadOnly();
    }

    public static IReadOnlyList<string> M06LiveBlockContractMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M06VirtioBlockReadMarkers,
        "TEST_START m06.007", .. Enumerable.Range(0, 6).Select(i => "M06_BDEV " + i), "CLROS_M06_007_READY", "TEST_PASS m06.007",
        "TEST_START m06.008", .. Enumerable.Range(6, 8).Select(i => "M06_BDEV " + i.ToString("X")), "CLROS_M06_008_READY", "TEST_PASS m06.008"
    ]);

    public static IReadOnlyList<string> M05VirtioNegotiationMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M05PciEnumerationMarkers,
        "TEST_START m05.012", "M05_VIRTIO 0", "M05_VIRTIO 1", "CLROS_M05_012_READY", "TEST_PASS m05.012",
        "TEST_START m05.013", .. Enumerable.Range(2, 5).Select(i => "M05_VIRTIO " + i), "CLROS_M05_013_READY", "TEST_PASS m05.013",
        "TEST_START m05.014", "M05_VIRTIO 7", "CLROS_M05_014_READY", "TEST_PASS m05.014",
        "TEST_START m05.015", "M05_VIRTIO 8", "M05_VIRTIO 9", "CLROS_M05_015_READY", "TEST_PASS m05.015"
    ]);

    public static IReadOnlyList<string> M05VirtqueuePollingMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M05VirtioNegotiationMarkers,
        "TEST_START m05.016", "M05_QUEUE 0", "CLROS_M05_016_READY", "TEST_PASS m05.016",
        "TEST_START m05.017", "M05_QUEUE 1", "CLROS_M05_017_READY", "TEST_PASS m05.017",
        "TEST_START m05.018", .. Enumerable.Range(2, 4).Select(i => "M05_QUEUE " + i), "CLROS_M05_018_READY", "TEST_PASS m05.018",
        "TEST_START m05.019", "M05_QUEUE 6", "M05_QUEUE 7", "CLROS_M05_019_READY", "TEST_PASS m05.019"
    ]);
    public static IReadOnlyList<string> M05ApicSetupMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M05PciEnumerationMarkers, "TEST_START m05.020", .. Enumerable.Range(0, 4).Select(i => "M05_APIC " + i),
        "CLROS_M05_020_READY", "TEST_PASS m05.020"
    ]);
    public static IReadOnlyList<string> M07VirtioNetMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M05VirtqueuePollingMarkers,
        "TEST_START m07.001", "M07_NET 0", "CLROS_M07_001_READY", "TEST_PASS m07.001",
        "TEST_START m07.002", "M07_NET 1", "CLROS_M07_002_READY", "TEST_PASS m07.002",
        "TEST_START m07.003", "TEST_START m07.004", "TEST_START m07.005",
        .. Enumerable.Range(2, 12).Select(i => "M07_NET " + i.ToString("X", System.Globalization.CultureInfo.InvariantCulture)),
        "CLROS_M07_003_READY", "TEST_PASS m07.003", "CLROS_M07_004_READY", "TEST_PASS m07.004",
        "CLROS_M07_005_READY", "TEST_PASS m07.005"
    ]);
    public static IReadOnlyList<string> M07ArpReceiveMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M07VirtioNetMarkers,
        .. Enumerable.Range(6, 4).Select(i => "TEST_START m07.00" + i),
        .. Enumerable.Range(0, 14).Select(i => "M07_ARP " + i.ToString("X", System.Globalization.CultureInfo.InvariantCulture)),
        .. Enumerable.Range(6, 4).SelectMany(i => new[] { "CLROS_M07_00" + i + "_READY", "TEST_PASS m07.00" + i })
    ]);
    public static IReadOnlyList<string> M06BlockMsixMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M05ApicSetupMarkers, .. M06PackageReadMarkers().Skip(M05PciEnumerationMarkers.Count),
        "TEST_START m06.019", .. Enumerable.Range(0, 9).Select(i => "M06_IRQ " + i),
        "CLROS_M06_019_READY", "TEST_PASS m06.019"
    ]);
    public static IReadOnlyList<string> M05VirtqueueInterruptMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M05ApicSetupMarkers, .. M05VirtioNegotiationMarkers.Skip(M05PciEnumerationMarkers.Count),
        "TEST_START m05.021", .. M05VirtqueuePollingMarkers.Skip(M05VirtioNegotiationMarkers.Count),
        .. Enumerable.Range(0, 8).Select(i => "M05_IRQ " + i), "CLROS_M05_021_READY", "TEST_PASS m05.021"
    ]);

    public static IReadOnlyList<string> M05BarDmaMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M05PciEnumerationMarkers,
        "TEST_START m05.008", .. Enumerable.Range(0, 4).Select(i => "M05_BAR_DMA " + i), "CLROS_M05_008_READY", "TEST_PASS m05.008",
        "TEST_START m05.009", "M05_BAR_DMA 4", "M05_BAR_DMA 5", "CLROS_M05_009_READY", "TEST_PASS m05.009",
        "TEST_START m05.010", .. Enumerable.Range(6, 3).Select(i => "M05_BAR_DMA " + i), "CLROS_M05_010_READY", "TEST_PASS m05.010",
        "TEST_START m05.011", .. Enumerable.Range(9, 4).Select(i => "M05_BAR_DMA " + i), "CLROS_M05_011_READY", "TEST_PASS m05.011"
    ]);

    public static IReadOnlyList<string> M05MonotonicClockMarkers { get; } = Array.AsReadOnly<string>(
    [
        .. M03OwnedSubsystemMarkers, "TEST_START m05.003", .. Enumerable.Range(0, 4).Select(stage => "M05_CLOCK " + stage),
        "CLROS_M05_003_READY", "TEST_PASS m05.003"
    ]);
}
