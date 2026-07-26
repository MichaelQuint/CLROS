# Diagnostics

## Current Status

Diagnostics are host-testable only. No real IDT load, fault handlers, GDT/TSS/IST setup, live CPU port-I/O serial writer, or live panic-safe output path exists yet. The repository now has host contracts for serial diagnostics readiness, COM1 configuration planning, a COM1-backed platform serial transport over a fakeable byte-port backend, owned-serial readiness derivation from COM1 backend prerequisites, aggregate owned COM1 diagnostics readiness metadata, CPU fault-frame formatting, deterministic CPU fault-entry frame storage, caller-owned panic text buffering, x64 IDT gate descriptors, IDT table planning, required fault-vector metadata, x64 instruction-byte plans for future port-I/O, descriptor-table, and fault-entry shellcode stubs, byte-copy materialization of planned diagnostic stubs into caller-owned buffers, a page-transition plan contract for future W^X handoff, a host-only diagnostics-chain readiness aggregate across the current prerequisite contracts, a post-chain diagnostic-stub invocation readiness guard that validates resolved COM1 stub addresses against the planned executable payload range without invoking them, and a value-only diagnostic-stub callable-binding plan that checks materialized COM1 callable preconditions before any live COM1/serial invocation.

## Implemented Behavior

- `DiagnosticLog` writes stable `[info]`, `[warn]`, and `[PANIC]` prefixes to an `ISerialSink`.
- `DiagnosticLog` can mirror log lines into `BootEventRingBuffer`.
- `BootEventRecordRingBuffer` records compact caller-owned `BootEventRecord`
  values for future panic-path boot-event snapshots without storing managed
  references or allocating in the record/snapshot path.
- `BootEventRecordFormatter` writes deterministic ASCII `BootEventRecord`
  snapshot lines into `PanicTextBuffer` or caller-owned byte storage without
  allocating in the formatter path.
- `PanicFormatter` emits `OS_SMOKE_FAIL` plus optional `subsystem`, `reason`, and `vector` fields.
- `CpuFaultFrame` records the host-testable CPU fault dump contract for vector, error-code state, RIP, RSP, RBP, RFLAGS, CR2, CR3, and general-purpose registers.
- `PanicFormatter.FormatFault` emits CPU fault dumps as deterministic key/value text in QEMU-friendly order.
- `Panic.Report` supports `ReturnToKernel` mode for host tests and `HaltRequested` status for future non-returning panic paths.
- `SerialDiagnosticsGuard` models firmware-console-only diagnostics versus OS-owned serial diagnostics. It blocks post-firmware diagnostics unless owned serial readiness is explicitly marked.
- `IdtGateDescriptor` defines a host-testable x64 interrupt/trap gate descriptor contract with status-coded validation for vector, handler address, selector, IST index, gate type, DPL, and present bit.
- `FaultVectorCatalog` defines metadata for CPU exception vectors, including the first required vectors: `#DE`, `#UD`, `#DF`, `#NM`, `#TS`, `#NP`, `#SS`, `#GP`, and `#PF`.
- `IdtTablePlan` builds and validates a caller-owned 256-entry IDT descriptor table plan for the required early fault vectors and writes deterministic 4096-byte descriptor storage without loading it.
- `IdtrDescriptor` models the x64 IDTR pointer for the 4096-byte table, including limit/base validation and deterministic packed 10-byte output.
- `GdtDescriptorPlan` models a minimal x64 GDT byte layout for null, kernel code, kernel data, and available 64-bit TSS descriptors.
- `TssIstPlan` models the x64 TSS bytes needed for `RSP0` and IST entries, including double-fault IST stack top, stack-size, and guard-size validation.
- `FaultInfrastructureReadiness` combines serial, IDT, IDTR, GDT, TSS/IST, and double-fault IST readiness into a single pre-privileged-install gate.
- `BootSequence.RunFaultInfrastructureReadiness` emits host-tested `FAULT_INFRA_READY` or `FAULT_INFRA_NOT_READY status=<status>` markers from the readiness result without changing the default boot path.
- `PanicTextBuffer` writes ASCII, decimal, fixed-width hex, CR/LF, and CPU fault
  summaries into caller-provided byte storage with status-coded overflow,
  invalid-ASCII failures, and default/zero buffer-success rejection. Its
  text-field guard uses the shared `OS.Std.Ascii.IsWhiteSpace` helper, and its
  decimal/hex numeric output uses the shared `OS.Std.Text.AsciiTextBuilder`
  helper instead of diagnostics-local formatting loops.
- `PanicSerialReporter` bridges `PanicTextBuffer` to a byte sink with status-coded buffer overflow, invalid ASCII, sink failure, and default/zero result-success rejection.
- `CpuFaultPanicReadiness` composes deterministic `CpuFaultEntryFrameLayout`
  decoding with `PanicSerialReporter` output into one caller-owned readiness
  result. `BootSequence.RunCpuFaultPanicReadiness` emits host-tested
  `CPU_FAULT_PANIC_READY` or `CPU_FAULT_PANIC_NOT_READY ...` markers without
  changing the default boot path.
- `CpuFaultPanicReadinessFormatter` formats the CPU fault panic readiness
  ready/not-ready markers into caller-owned ASCII byte buffers, reports the
  required byte count for undersized destinations, and rejects unknown nested
  status enum values before writing. Its decimal byte counts use the shared
  `OS.Std.Text.AsciiDecimalFormatter` helper, and its marker/decimal writes use
  `OS.Std.Text.AsciiTextBuilder` instead of diagnostics-local decimal or
  span-copy append paths.
- `SerialPortConfiguration` models COM1 defaults, divisor-latch calculation, and deterministic initialization port/value write sequences without performing port I/O. Its configuration, divisor-latch, and initialization-plan results reject default/zero values as successful evidence.
- `Com1SerialWriter` initializes and writes ASCII through an abstract byte port backend, polls transmit readiness, rejects invalid ASCII, reports backend failures without executing real port I/O, and rejects default/zero writer results as successful evidence.
- `Com1PlatformSerialTransport` adapts `Com1SerialWriter` to `IPlatformSerialTransport`. It lazily initializes the writer, exposes itself as an owned serial transport, maps writer initialization, timeout, backend, invalid-ASCII, unavailable-readiness, and partial-write states into `PlatformSerialTransportResult`, and preserves compact encoded COM1 backend status for diagnostics. The current tests use fake byte-port backends only.
- `PortIoOperationPlan` emits deterministic x64 `out dx, al` and `in al, dx` instruction bytes into caller-owned storage for a future C# shellcode port-I/O backend. It rejects default/zero result values and incomplete byte-count metadata as not successful.
- `PortIoStubPlan` emits deterministic callable x64 COM1 byte-port stubs for future unmanaged `Out8` and `In8` delegates, reusing `PortIoOperationPlan` for the final privileged operation byte and ending each stub with `ret`. It rejects default/zero result values and incomplete byte-count metadata as not successful.
- `DescriptorTableInstructionPlan` emits deterministic x64 `lidt [rip+disp32]`, `lgdt [rip+disp32]`, and `ltr ax` instruction bytes into caller-owned storage for future descriptor-table installation stubs.
- `DescriptorTableInstallStubPlan` emits deterministic callable x64 `lidt`, `lgdt`, and `ltr` stubs into caller-owned storage. `lidt` and `lgdt` stubs embed the 10-byte descriptor payload after the executable bytes and use RIP-relative addressing; the `ltr` stub loads a caller-provided TSS selector into `AX`. The descriptor payload copy path uses the shared `OS.Std.Memory.BufferOperations.MemoryCopy` helper after validation and maps unexpected copy-helper failure to the existing output-buffer-too-small status.
- `FaultProbeStubPlan` emits deterministic non-executed destructive fault probe bytes for future QEMU tests: `ud2; ret` and a null-address byte write followed by `ret`. Its metadata is tied to the disabled-by-default destructive probe toggles, and it rejects default/zero result values or incomplete byte-count metadata as successful evidence.
- `NullPageFaultProbePlan` is a separate no-stdlib-compatible, value-only contract for a future supervisor null read. It accepts only successful page-zero absence evidence from a caller-owned four-level image, remains disabled, fixes expected `#PF` vector 14/error-code-zero/CR2-zero intent, requires caller-attested owned diagnostics and handler prerequisites, and rejects default-on, requested-execution, or attempted-execution states. It emits no fault bytes, does not reuse the older null-write plan, dereferences no address, and explicitly defers execution to M02-014.
- `ExecutableStubMemoryPlan` defines the W^X policy and caller-owned layout contract that future executable COM1, descriptor-table, and fault-probe stubs must satisfy before any execution is attempted. It rejects default/zero result values as successful evidence.
- `ExecutableStubBundlePlan` packs stable generated-stub slots for future COM1 port I/O, descriptor-table install, invalid-opcode probe, and null-write probe payloads inside the executable-stub memory plan without allocating, copying, mapping, or executing bytes. It rejects default/zero result values and does not expose slot offsets from uninitialized results as valid evidence.
- `ExecutableStubBundleMaterializer` copies already-planned diagnostic stub bytes into caller-owned payload or full memory-region buffers, applies the executable payload offset when requested, validates failed plans, undersized source/destination buffers, and default/zero result values, and does not allocate executable memory, change page protections, flush instruction caches, or execute bytes.
- `ExecutableStubPageTransitionPlan` validates the future write-then-execute page-transition boundary after diagnostic stub materialization. It rejects failed memory plans, unmaterialized payloads, RWX protection states, invalid aligned ranges, range overflow, copied-byte counts larger than the planned payload, and default/zero result values. It does not call page-protection APIs or execute generated bytes.
- `BootSequence.RunDiagnosticStubMaterializationReadiness` emits host-tested `DIAGNOSTIC_STUBS_MATERIALIZED` or `DIAGNOSTIC_STUBS_NOT_MATERIALIZED status=<status>` markers from materializer evidence without changing the default boot path.
- `BootSequence.RunPostFirmwareDiagnosticsReadiness` emits host-tested `POST_FIRMWARE_DIAGNOSTICS_READY` or `POST_FIRMWARE_DIAGNOSTICS_BLOCKED status=<status>` markers from `SerialDiagnosticsGuardResult` without changing the default boot path.
- `BootSequence.RunExitBootServicesPreflight` emits host-tested `UEFI_EBS_PREFLIGHT_READY` or `UEFI_EBS_PREFLIGHT_BLOCKED diagnostics=<status> plan=<status>` markers from post-firmware diagnostics and `UefiExitBootServicesPlanResult` readiness without calling firmware.
- `FaultEntryStubPlan` emits deterministic, non-executed bytes for future CPU fault-entry stubs for `#UD` and `#PF`. The plan validates vector/error-code metadata, writes caller-owned byte buffers only, and terminates planned bytes with `ud2; ret` so accidental execution remains fail-fast during development.
- `FaultProbeReadiness` validates disabled-by-default destructive probe toggles, invalid-opcode and null-write probe stub emission into caller-owned buffers, and executable-stub memory capacity before any future controlled CPU fault probe is wired into boot.
- `Com1PortIoBackendPlan` validates the host-tested prerequisites for a future OS-owned COM1 port-I/O backend: serial configuration, byte-port operation plans, callable stub plans, and executable-stub memory layout capacity. It rejects default/zero result values and incomplete success metadata as successful evidence.
- `Com1PortIoBackendContract` resolves planned COM1 `Out8` and `In8` stub offsets into absolute future backend addresses from an executable payload base address while rejecting failed readiness, missing slots, invalid COM1 base ports, zero payload bases, address overflow, and default/zero result values. It does not dereference or invoke function pointers.
- `OwnedSerialDiagnosticsReadinessResult` derives OS-owned serial diagnostics readiness from a successful `Com1PortIoBackendPlanResult` and a successful `Com1PortIoBackendContractResult`, rejects default/zero readiness results, and requires resolved COM1 backend addresses. `SerialDiagnosticsGuard.MarkOwnedSerialReady(readiness)` marks owned serial ready only when that derived readiness succeeded.
- `OwnedCom1DiagnosticsReadiness` composes the existing COM1 backend plan, backend contract, owned-serial readiness derivation, `SerialDiagnosticsGuard`, and `BootSequence.RunPostFirmwareDiagnosticsReadiness` marker boundary into one host-tested aggregate result. It reports nested backend, contract, guard, and marker statuses without installing a live port-I/O backend or calling firmware, and rejects default/zero aggregate readiness as success.
- `IPlatformSerial`, `IPlatformSerialTransport`, and `GuardedPlatformSerial` define the host-testable serial-output boundary that distinguishes firmware-backed serial output before UEFI-gone from OS-owned serial required after UEFI-gone simulation.
- `OwnedSerialSmokeMarkers` centralizes the host-tested serial smoke marker contract for `TEST_START`, `TEST_PASS`, `TEST_FAIL`, `OS_BOOT_OK`, `OS_SMOKE_PASS`, and `OS_SMOKE_FAIL`, recognizes valid test-marker prefixes and known smoke markers, formats owned-serial test/panic marker text from bounded ASCII tokens into caller-owned byte buffers through `OS.Std.Text.AsciiTextBuilder`, and leaves line termination and serial writes with `OwnedSerialDiagnostics`.
- `OwnedSerialDiagnostics` formats boot/panic marker lines into caller-owned
  byte storage with CR/LF terminators and writes them through `IPlatformSerial`
  without allocating in the formatter path. Its marker-line formatter appends
  the validated marker bytes plus CR/LF through
  `OS.Std.Text.AsciiTextBuilder` and maps unexpected builder failures back to
  existing status-coded formatter failures. Its formatter/write results reject
  default/zero values as successful evidence. This remains host-tested
  owned-serial marker formatting only; it is not live COM1 port-I/O, real
  `ExitBootServices`, or post-firmware serial-output evidence.
- `DescriptorTableInstallReadiness` validates the host-tested prerequisites for a future descriptor-table install step: IDT, IDTR, GDT, TSS/IST, double-fault IST, and callable install-stub buffer capacity.
- `BootSequence.RunEarlyDiagnosticsReadiness` emits host-tested `EARLY_DIAGNOSTICS_READY` or `EARLY_DIAGNOSTICS_NOT_READY status=<status>` markers from COM1 backend and descriptor-table install readiness results without changing the default boot path.
- `CpuFaultEntryFrameLayout` defines a deterministic 200-byte packed fault-entry frame layout for future x64 interrupt stubs, including vector, error-code metadata, RIP/RSP/RBP/RFLAGS, CR2/CR3, CS/SS, and formatter-compatible general-purpose register fields. It reads and writes caller-owned byte storage only.
- `BootSequence.RunControlledFaultProbeReadiness` emits host-tested `CONTROLLED_FAULT_PROBES_READY` or `CONTROLLED_FAULT_PROBES_NOT_READY status=<status>` markers after early diagnostics and fault-probe readiness both succeed. It does not execute generated stubs or trigger CPU faults.
- `DiagnosticsChainReadiness` composes COM1 backend readiness, descriptor-table install readiness, controlled fault-probe readiness, CPU fault panic readiness, and diagnostic stub materialization into one status-coded result with no managed references. `BootSequence.RunDiagnosticsChainReadiness` emits host-tested `DIAGNOSTICS_CHAIN_READY` or `DIAGNOSTICS_CHAIN_NOT_READY ...` marker metadata without changing the default boot path.
- `DiagnosticStubInvocationReadiness` cross-checks a successful `ExecutableStubPageTransitionPlanResult` against a successful `Com1PortIoBackendContractResult` and requires both resolved COM1 `Out8` and `In8` stub addresses to fall inside the page-transition payload range before any future live invocation is attempted. `BootSequence.RunDiagnosticStubInvocationReadiness` emits host-tested `DIAGNOSTIC_STUB_INVOCATION_READY` or `DIAGNOSTIC_STUB_INVOCATION_NOT_READY ...` marker metadata without changing the default boot path.
- `DiagnosticStubInvocationReadinessFormatter` formats the stub-invocation ready
  and not-ready markers into caller-owned ASCII byte buffers, reports required
  byte counts for undersized destinations, rejects unknown nested status enum
  values before writing, keeps the format result free of managed references,
  and uses the shared `OS.Std.Text.AsciiDecimalFormatter` and
  `OS.Std.Text.AsciiTextBuilder` helpers for marker byte appends, address
  fields, and byte-count fields instead of direct span-copy appends or
  diagnostics-local decimal loops. This is host-tested formatter evidence only;
  it does not execute generated stubs or perform CPU port I/O.
- `DiagnosticStubCallableBindingPlan` composes diagnostic-stub invocation
  readiness, COM1 backend-contract metadata, and materialized stub-byte
  evidence into value-only callable-binding metadata before a future live COM1
  invocation can be attempted. It rejects failed/default readiness, backend
  mismatches, unsupported COM1 base-port metadata, missing materialized COM1
  stub bytes, target ranges that would extend beyond the payload, and
  overlapping `Out8`/`In8` target ranges. It does not create delegates,
  dereference addresses, execute generated stubs, perform port I/O, or write
  serial output.
- `DiagnosticStubCallableBindingPlanDiagnosticFormatter` formats initialized
  callable-binding plan metadata into stable caller-owned ASCII
  `DIAGNOSTIC_STUB_CALLABLE_BINDING status=` marker bytes. It reports required
  and available byte counts for short destinations, rejects default or unknown
  status metadata before writing, keeps the format result free of managed
  references, and uses the shared `OS.Std.Text.AsciiDecimalFormatter` and
  `OS.Std.Text.AsciiTextBuilder` helpers for byte-counting and marker writes.
  This is host-tested formatter evidence only; it does not bind delegates,
  invoke generated stubs, perform CPU port I/O, or write serial output.
- `Probes.cs` groups probe definitions by boot smoke, serial smoke, EFI smoke, panic smoke, memory-map smoke, UEFI `ExitBootServices`, ACPI discovery, storage smoke, and network smoke. It now exposes a caller-buffer probe catalog (`ProbeCatalogCount`, `TryGetProbe`, and `CopyProbeCatalog`) so diagnostics tooling can enumerate the compile-time probe surface without allocating or duplicating individual getter calls.
- `ProbeCatalogDiagnosticFormatter` formats initialized probe definitions and caller-provided probe catalogs into stable caller-owned ASCII `PROBE_CATALOG` marker bytes. It reports required-byte counts for undersized destinations, rejects default/invalid definitions, keeps dangerous probe metadata explicit, and uses `OS.Std.Text.AsciiTextBuilder` for marker writes instead of allocating formatter-owned strings.
- Safe smoke probes are enabled by default only where they are part of the existing marker path:
  - `EmitBootSmokeMarkers`
  - `EmitSerialSmokeMarkers`
  - `EnableEfiSmokeProbe`
  - `EnableMemoryMapSmokeProbe`
- Boundary, destructive, and future device-I/O probes default to `false`:
  - `EnableUefiGoneSimulationProbe`
  - `EnableExitBootServicesFreshMapProbe`
  - `EnableAcpiPhysicalDiscoveryProbe`
  - `EnableNullPointerPanicProbe`
  - `EnableInvalidOpcodePanicProbe`
  - `EnablePacketDumpProbe`
  - `EnableVirtioBlockReadProbe`
  - `EnableVirtioNetworkProbe`
  - `EnableFetchAppHttpTcpExchangeProbe`
- `BootSequence.RunUefiGoneSimulation` emits host-tested `UEFI_GONE_SIM_*` markers without enabling real `ExitBootServices`.
- `EnableFetchAppHttpTcpExchangeProbe` is a disabled-by-default network-smoke
  catalog entry for future boot wiring of
  `BootSequence.RunFetchAppHttpTcpExchangeReadiness`. It does not enable live
  DNS, ARP, TCP, HTTP, virtio-net, PAL socket, or FetchApp execution.
- `scripts/build-efi-shell.ps1 -EnableUefiGoneSimulationProbe` builds a firmware-visible QEMU simulation variant that emits `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `scripts/build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe` builds a firmware-visible ACPI physical discovery probe variant. The probe is disabled by default, runs before `ExitBootServices`, and emits ACPI table-discovery markers only after live RSDP/root SDT/MCFG byte validation succeeds.

## Unsupported Behavior

Real IDT/GDT/TSS/IST setup, page-fault and invalid-opcode handlers, real CR2/CR3/register capture, owned serial hardware output after `ExitBootServices`, panic-safe framebuffer output, panic redaction, and halt/shutdown loops are pending.

## Tests

`tests/OS.HostTests/DiagnosticsTests.cs` covers log prefixes, boot-event mirroring, panic formatting, page-fault and invalid-opcode register dump formatting, invalid-opcode/null-dereference probe reason strings, return-to-kernel panic mode, probe group metadata, safe probe defaults, probe catalog enumeration/capacity reporting, caller-owned probe catalog diagnostic formatting, and dangerous probe defaults. Probe catalog formatter markers include `TEST_PASS probes catalog formatter formats all definitions`, `TEST_PASS probes catalog formatter reports destination capacity`, and `TEST_PASS probes catalog formatter keeps dangerous probes disabled`.

`tests/OS.HostTests/BootEventRecordRingBufferTests.cs` covers the caller-owned
boot-event record ring. Current markers:

```text
TEST_PASS diagnostics.boot_event_record records caller owned events
TEST_PASS diagnostics.boot_event_record overwrites oldest deterministically
TEST_PASS diagnostics.boot_event_record reports snapshot capacity
TEST_PASS diagnostics.boot_event_record rejects missing storage
TEST_PASS diagnostics.boot_event_record default results are not success
TEST_PASS diagnostics.boot_event_record layouts contain no managed references
```

`tests/OS.HostTests/BootEventRecordFormatterTests.cs` covers deterministic
panic-buffer formatting for boot-event records. Current markers:

```text
TEST_PASS diagnostics.boot_event_formatter formats snapshot records
TEST_PASS diagnostics.boot_event_formatter reports destination capacity
TEST_PASS diagnostics.boot_event_formatter rejects invalid records
TEST_PASS diagnostics.boot_event_formatter writes through panic buffer
TEST_PASS diagnostics.boot_event_formatter default result is not success
TEST_PASS diagnostics.boot_event_formatter result contains no managed references
```

Current CPU fault dump markers:

```text
TEST_PASS panic formatter emits page fault register dump
TEST_PASS panic formatter emits invalid opcode register dump
TEST_PASS idt descriptor encodes interrupt gate
TEST_PASS idt descriptor rejects invalid ist index
TEST_PASS idt descriptor rejects null handler address
TEST_PASS fault vectors expose error code policy
TEST_PASS fault vectors include page fault metadata
TEST_PASS gdt descriptor plan encodes kernel segments
TEST_PASS gdt descriptor plan encodes tss descriptor
TEST_PASS gdt descriptor plan rejects invalid inputs
TEST_PASS fault infra readiness accepts complete plan
TEST_PASS fault infra readiness rejects missing serial
TEST_PASS fault infra readiness rejects incomplete descriptor plans
TEST_PASS boot.sequence fault infra readiness emits ready marker
TEST_PASS boot.sequence fault infra readiness emits failure marker
TEST_PASS boot.sequence default markers unchanged by fault readiness helper
TEST_PASS idtr descriptor creates 4096-byte table pointer
TEST_PASS idtr descriptor writes packed bytes
TEST_PASS idtr descriptor rejects invalid inputs
TEST_PASS idt table plan binds required fault vectors
TEST_PASS idt table plan rejects missing required vector
TEST_PASS idt table plan requires double fault ist
TEST_PASS idt table plan writes descriptor bytes
TEST_PASS tss ist plan encodes double fault ist
TEST_PASS tss ist plan encodes rsp0
TEST_PASS tss ist plan rejects invalid inputs
TEST_PASS panic text buffer writes ascii lines
TEST_PASS panic text buffer writes decimal and hex
TEST_PASS panic text buffer reports overflow
TEST_PASS panic text buffer formats fault summary
TEST_PASS panic text buffer default buffer is not success
TEST_PASS panic text buffer uses shared ascii whitespace helper
TEST_PASS panic text buffer uses shared ascii text builder
TEST_PASS panic serial reporter flushes fault summary
TEST_PASS panic serial reporter reports buffer overflow
TEST_PASS panic serial reporter reports sink failure
TEST_PASS panic serial reporter default result is not success
```

Current serial diagnostics guard markers:

```text
TEST_PASS diagnostics serial guard starts firmware only
TEST_PASS diagnostics serial guard blocks post firmware without owned serial
TEST_PASS diagnostics serial guard allows post firmware with owned serial
TEST_PASS diagnostics serial guard rejects invalid transition
TEST_PASS diagnostics serial guard reports serial unavailable
TEST_PASS serial port config exposes com1 defaults
TEST_PASS serial port config calculates divisor latch
TEST_PASS serial port config emits init sequence
TEST_PASS serial port config rejects invalid inputs
TEST_PASS serial port config default results are not success
TEST_PASS com1 writer initializes through port backend
TEST_PASS com1 writer writes ascii bytes when ready
TEST_PASS com1 writer rejects writes before initialization
TEST_PASS com1 writer reports transmit timeout
TEST_PASS com1 writer reports backend failure
TEST_PASS com1 writer default result is not success
TEST_PASS port io plan encodes out8 operation
TEST_PASS port io plan encodes in8 operation
TEST_PASS port io plan rejects invalid inputs
TEST_PASS port io plan default result is not success
TEST_PASS port io stub plan encodes callable out8 stub
TEST_PASS port io stub plan encodes callable in8 stub
TEST_PASS port io stub plan rejects invalid inputs
TEST_PASS port io stub plan default result is not success
TEST_PASS descriptor instruction plan encodes lidt
TEST_PASS descriptor instruction plan encodes lgdt
TEST_PASS descriptor instruction plan encodes ltr
TEST_PASS descriptor instruction plan rejects invalid inputs
TEST_PASS descriptor install stub plan encodes lidt stub
TEST_PASS descriptor install stub plan encodes lgdt stub
TEST_PASS descriptor install stub plan encodes ltr stub
TEST_PASS descriptor install stub plan rejects invalid inputs
TEST_PASS descriptor install stub plan uses shared buffer operations
TEST_PASS fault probe stub plan encodes invalid opcode probe
TEST_PASS fault probe stub plan encodes null write probe
TEST_PASS fault probe stub plan keeps destructive probes disabled by default
TEST_PASS fault probe stub plan rejects invalid inputs
TEST_PASS fault probe stub plan default result is not success
TEST_PASS executable stub memory plan accepts write-then-execute policy
TEST_PASS executable stub memory plan rejects accidental rwx policy
TEST_PASS executable stub memory plan calculates aligned layout
TEST_PASS executable stub memory plan rejects invalid inputs
TEST_PASS executable stub memory plan default result is not success
TEST_PASS executable stub bundle plan packs diagnostics stubs
TEST_PASS executable stub bundle plan calculates aligned slot offsets
TEST_PASS executable stub bundle plan rejects duplicate or unknown slots
TEST_PASS executable stub bundle plan rejects insufficient memory plan
TEST_PASS executable stub bundle plan default result is not success
TEST_PASS fault probe readiness accepts disabled planned probes
TEST_PASS fault probe readiness rejects enabled destructive defaults
TEST_PASS fault probe readiness rejects invalid stub readiness
TEST_PASS fault probe readiness rejects insufficient executable memory plan
TEST_PASS com1 port io backend plan accepts ready prerequisites
TEST_PASS com1 port io backend plan rejects invalid serial config
TEST_PASS com1 port io backend plan rejects missing stub readiness
TEST_PASS com1 port io backend plan rejects insufficient stub memory plan
TEST_PASS com1 port io backend plan default result is not success
TEST_PASS descriptor install readiness accepts complete prerequisites
TEST_PASS descriptor install readiness rejects missing idt readiness
TEST_PASS descriptor install readiness rejects missing tss ist readiness
TEST_PASS descriptor install readiness rejects insufficient stub buffers
TEST_PASS boot.sequence early diagnostics readiness emits ready marker
TEST_PASS boot.sequence early diagnostics readiness rejects serial backend failure
TEST_PASS boot.sequence early diagnostics readiness rejects descriptor readiness failure
TEST_PASS boot.sequence early diagnostics readiness leaves default markers unchanged
TEST_PASS executable stub bundle materializer copies planned diagnostic stubs
TEST_PASS executable stub bundle materializer applies payload base offset
TEST_PASS executable stub bundle materializer rejects failed plans
TEST_PASS executable stub bundle materializer rejects undersized destination or source
TEST_PASS executable stub bundle materializer default result is not success
TEST_PASS executable stub bundle materializer uses shared buffer operations
TEST_PASS executable stub page transition plan accepts write-then-execute
TEST_PASS executable stub page transition plan rejects rwx default
TEST_PASS executable stub page transition plan rejects unmaterialized payload
TEST_PASS executable stub page transition plan rejects failed plans and invalid ranges
TEST_PASS executable stub page transition plan default result is not success
TEST_PASS boot.sequence diagnostic stubs materialized emits ready marker
TEST_PASS boot.sequence diagnostic stubs materialized rejects materializer failure
TEST_PASS boot.sequence diagnostic stubs materialized leaves default markers unchanged
TEST_PASS boot.sequence post-firmware diagnostics readiness emits ready marker
TEST_PASS boot.sequence post-firmware diagnostics readiness blocks firmware-console-only path
TEST_PASS boot.sequence post-firmware diagnostics readiness blocks firmware-services-unavailable path
TEST_PASS boot.sequence post-firmware diagnostics readiness leaves default markers unchanged
TEST_PASS boot.sequence exitboot preflight emits ready marker
TEST_PASS boot.sequence exitboot preflight blocks missing post-firmware diagnostics
TEST_PASS boot.sequence exitboot preflight blocks non-success transition plan
TEST_PASS boot.sequence exitboot preflight leaves default markers unchanged
TEST_PASS fault entry stub plan encodes invalid opcode frame handoff
TEST_PASS fault entry stub plan encodes page fault frame handoff
TEST_PASS fault entry stub plan rejects unsupported vector metadata
TEST_PASS com1 port io backend contract resolves stub addresses
TEST_PASS com1 port io backend contract rejects failed readiness
TEST_PASS com1 port io backend contract rejects missing stub slots
TEST_PASS com1 port io backend contract rejects invalid payload base or overflow
TEST_PASS com1 port io backend contract default result is not success
TEST_PASS diagnostics owned serial readiness accepts com1 backend contract
TEST_PASS diagnostics owned serial readiness rejects failed backend plan
TEST_PASS diagnostics owned serial readiness rejects failed backend contract
TEST_PASS diagnostics owned serial readiness rejects late firmware unavailable
TEST_PASS diagnostics owned serial readiness default result is not success
TEST_PASS diagnostics.owned com1 readiness composes
TEST_PASS diagnostics.owned com1 readiness rejects backend plan
TEST_PASS diagnostics.owned com1 readiness rejects backend contract
TEST_PASS diagnostics.owned com1 readiness emits post firmware marker metadata
TEST_PASS diagnostics.owned com1 readiness default result is not success
TEST_PASS diagnostics.owned_serial writes firmware serial before uefi gone
TEST_PASS diagnostics.owned_serial writes owned serial after uefi gone
TEST_PASS diagnostics.owned_serial rejects firmware serial after uefi gone
TEST_PASS diagnostics.owned_serial reports missing owned transport
TEST_PASS diagnostics.owned_serial reports owned serial unavailable
TEST_PASS diagnostics.owned_serial reports short write
TEST_PASS diagnostics.owned_serial formats boot and panic markers
TEST_PASS diagnostics.owned_serial results contain no managed references
TEST_PASS diagnostics.owned_serial default results are not success
TEST_PASS diagnostics.owned_serial formatter uses shared ascii text builder
TEST_PASS diagnostics.serialmarkers exposes smoke marker constants
TEST_PASS diagnostics.serialmarkers recognizes test marker prefixes
TEST_PASS diagnostics.serialmarkers rejects invalid markers
TEST_PASS diagnostics.serialmarkers preserves no-allocation contract
TEST_PASS diagnostics.serialmarkers uses shared ascii text builder
TEST_PASS diagnostics.com1_transport writes through fake com1 backend
TEST_PASS diagnostics.com1_transport writes owned serial after uefi gone
TEST_PASS diagnostics.com1_transport reports unavailable readiness
TEST_PASS diagnostics.com1_transport maps initialization failure
TEST_PASS diagnostics.com1_transport maps transmit timeout
TEST_PASS diagnostics.com1_transport reports short write
TEST_PASS diagnostics.com1_transport results contain no managed references
TEST_PASS fault entry frame layout writes and reads page fault frame
TEST_PASS fault entry frame layout writes and reads invalid opcode frame
TEST_PASS fault entry frame layout rejects inconsistent error-code metadata
TEST_PASS fault entry frame layout rejects undersized buffers
TEST_PASS fault entry frame layout rejects unknown vectors
TEST_PASS boot.sequence controlled fault probes emit ready marker
TEST_PASS boot.sequence controlled fault probes reject early diagnostics failure
TEST_PASS boot.sequence controlled fault probes reject probe readiness failure
TEST_PASS boot.sequence controlled fault probes leave default markers unchanged
TEST_PASS diagnostics.chain readiness accepts complete chain
TEST_PASS diagnostics.chain readiness rejects serial backend failure
TEST_PASS diagnostics.chain readiness rejects descriptor failure
TEST_PASS diagnostics.chain readiness rejects fault probe failure
TEST_PASS diagnostics.chain readiness rejects panic failure
TEST_PASS diagnostics.chain readiness rejects stub materialization failure
TEST_PASS diagnostics.chain readiness default result is not success
TEST_PASS diagnostics.chain readiness result contains no managed references
TEST_PASS boot.sequence diagnostics chain emits ready marker
TEST_PASS boot.sequence diagnostics chain emits blocked marker
TEST_PASS boot.sequence diagnostics chain leaves default markers unchanged
```

`tests/OS.HostTests/UefiGoneSimulationTests.cs` covers the current host-side `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, `UEFI_GONE_SIM_PASS`, and status-coded diagnostics-blocked marker behavior.

`tests/OS.HostTests/PageZeroMappingPolicyTests.cs` covers the M01-015
page-zero image audit and disabled supervisor-read fault intent. It
cross-checks the production literal vector against
`FaultVectorCatalog.PageFault` in host code only, verifies error-code mask
`0x1f` with expected value zero and expected CR2 zero, rejects missing
page-zero evidence, default-on state, missing caller-attested prerequisites,
requested execution, and attempted execution, and proves that neither
production file nor the host test belongs to either evaluated EFI compile
closure. No EFI binary was inspected or executed. Current aggregate markers
are:

```text
TEST_START m01.015
CLROS_M01_015_READY
TEST_PASS m01.015
```

`artifacts/logs/qemu-smoke-sim.log` is the current firmware-visible simulation evidence and contains `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.

`tests/OS.HostTests/CpuFaultPanicReadinessTests.cs` covers the host-only
fault-entry-frame to panic-serial composition path. Current markers:

```text
TEST_PASS diagnostics.cpu_fault_panic readiness emits os smoke fail
TEST_PASS diagnostics.cpu_fault_panic readiness rejects invalid frame
TEST_PASS diagnostics.cpu_fault_panic readiness reports reporter failure
TEST_PASS diagnostics.cpu_fault_panic readiness default result is not success
TEST_PASS diagnostics.cpu_fault_panic readiness result contains no managed references
TEST_PASS boot.sequence cpu fault panic readiness emits ready marker
TEST_PASS boot.sequence cpu fault panic readiness emits blocked marker
TEST_PASS boot.sequence cpu fault panic readiness preserves default markers
```

`tests/OS.HostTests/CpuFaultPanicReadinessFormatterTests.cs` covers the
caller-owned CPU fault panic readiness marker formatter. Current markers:

```text
TEST_PASS diagnostics.cpu_fault_panic formatter formats ready marker
TEST_PASS diagnostics.cpu_fault_panic formatter formats not-ready marker
TEST_PASS diagnostics.cpu_fault_panic formatter reports short destination
TEST_PASS diagnostics.cpu_fault_panic formatter rejects invalid status
TEST_PASS diagnostics.cpu_fault_panic formatter uses shared ascii text builder
TEST_PASS diagnostics.cpu_fault_panic formatter default result is not success
```

`tests/OS.HostTests/DiagnosticStubInvocationReadinessFormatterTests.cs` covers
the caller-owned diagnostic-stub invocation readiness marker formatter. Current
markers:

```text
TEST_PASS diagnostics.stub_invocation formatter formats ready marker
TEST_PASS diagnostics.stub_invocation formatter formats not-ready marker
TEST_PASS diagnostics.stub_invocation formatter reports short destination
TEST_PASS diagnostics.stub_invocation formatter rejects invalid status
TEST_PASS diagnostics.stub_invocation formatter uses shared ascii text builder
TEST_PASS diagnostics.stub_invocation formatter default result is not success
```

`tests/OS.HostTests/DiagnosticStubCallableBindingPlanTests.cs` covers the
value-only callable-binding preconditions after diagnostic-stub invocation
readiness and before any live COM1 invocation. Current markers:

```text
TEST_PASS diagnostics.stub_callable binding accepts materialized callable targets
TEST_PASS diagnostics.stub_callable binding rejects failed invocation readiness
TEST_PASS diagnostics.stub_callable binding rejects backend mismatch
TEST_PASS diagnostics.stub_callable binding rejects invalid com1 base port
TEST_PASS diagnostics.stub_callable binding rejects target beyond materialized payload
TEST_PASS diagnostics.stub_callable binding rejects overlapping targets
TEST_PASS diagnostics.stub_callable binding default result is not success
TEST_PASS diagnostics.stub_callable binding result contains no managed references
```

`tests/OS.HostTests/DiagnosticStubCallableBindingPlanDiagnosticFormatterTests.cs`
covers the caller-owned diagnostic-stub callable-binding marker formatter.
Current markers:

```text
TEST_PASS diagnostics.stub_callable formatter formats success marker
TEST_PASS diagnostics.stub_callable formatter formats failure marker
TEST_PASS diagnostics.stub_callable formatter reports short destination
TEST_PASS diagnostics.stub_callable formatter rejects default result
TEST_PASS diagnostics.stub_callable formatter rejects invalid status
TEST_PASS diagnostics.stub_callable formatter uses shared ascii text builder
TEST_PASS diagnostics.stub_callable formatter result contains no managed references
```

## Known Limitations

The current managed formatter and guard contracts allocate managed strings and are only safe for host tests or normal boot diagnostics. `BootEventRingBuffer` is still the managed string mirror used by normal host-tested boot logging. `BootEventRecordRingBuffer` and `BootEventRecordFormatter` are caller-storage based, but they are not wired into `BootSequence`, a real panic dump, or the live EFI path yet. `PanicTextBuffer` and `PanicSerialReporter` are caller-storage based, but they are not wired into a real panic path yet. `CpuFaultFrame` is a data contract and formatter input only; it is not evidence that CPU handlers can capture those registers yet.

`IdtGateDescriptor`, `IdtTablePlan`, `IdtrDescriptor`, `GdtDescriptorPlan`, `TssIstPlan`, `FaultInfrastructureReadiness`, `DescriptorTableInstructionPlan`, and `DescriptorTableInstallStubPlan` are descriptor encoding, table-layout, instruction-byte, and readiness contracts only. They do not execute `lidt`, `lgdt`, `ltr`, install handlers, configure a live TSS/IST, or prove QEMU fault behavior.

`FaultProbeStubPlan` is a byte-plan contract only. It intentionally does not execute `ud2` or write to address zero, and it is not QEMU fault evidence.

`NullPageFaultProbePlan` is metadata only and is distinct from
`FaultProbeStubPlan.NullWrite`: it models a supervisor read, not a write, and
contains no executable stream. Its diagnostics-ready and handler-ready fields
are caller attestations, not loaded IDT/COM1 evidence. It is excluded from the
evaluated `SingleMinimalApi` and `MultiMinimalApi` EFI compile closures; no EFI
binary was inspected or executed. It cannot trigger or observe `#PF`, read
CR2, capture an error code, report a panic, or establish QEMU evidence. Actual
null-read execution and captured state remain M02-014 work after owned
diagnostics exist.

`ExecutableStubMemoryPlan` is a policy and layout contract only. It does not allocate executable memory, change page protections, flush instruction caches, or execute generated bytes.

`ExecutableStubBundlePlan` is a layout contract only. It does not copy, map, protect, or execute generated stub bytes.

`ExecutableStubBundleMaterializer` is a byte-copy contract only. It writes generated bytes into caller-owned buffers through the shared `OS.Std.Memory.BufferOperations.MemoryCopy` helper and validates guard conditions, but it does not allocate executable memory, map pages, change page permissions, flush instruction caches, or execute the bytes.

`ExecutableStubPageTransitionPlan` is a transition-plan contract only. It does not invoke firmware or page-table APIs, change W^X/NX state, flush instruction caches, or execute generated stubs.

`FaultEntryStubPlan` is a deterministic byte-plan contract only. It does not install an IDT handler, switch stacks, capture CPU registers, or transfer control to a managed panic handler.

`FaultProbeReadiness`, `Com1PortIoBackendPlan`, `OwnedSerialDiagnosticsReadinessResult`, `OwnedCom1DiagnosticsReadiness`, `GuardedPlatformSerial`, `Com1PlatformSerialTransport`, `OwnedSerialDiagnostics`, and `DescriptorTableInstallReadiness` are readiness/boundary contracts only. They do not install a live CPU port-I/O COM1 backend, execute generated stubs, load descriptor tables, trigger CPU faults, or change CPU state.

`BootSequence.RunEarlyDiagnosticsReadiness` is a marker helper only. It does not copy generated stubs, execute generated stubs, install descriptor tables, or wire live diagnostics into the default boot path.

`BootSequence.RunPostFirmwareDiagnosticsReadiness` and `OwnedCom1DiagnosticsReadiness` are marker/readiness helpers only. They do not configure COM1, invoke port-I/O stubs, mark firmware services exited, call `ExitBootServices`, or prove live post-firmware output.

`BootSequence.RunExitBootServicesPreflight` is a marker/readiness helper only. It does not call firmware, execute generated stubs, or prove live post-firmware output.

`Com1PortIoBackendContract` is address metadata only. It resolves future function-pointer target addresses from planned stub offsets, but it does not create delegates, invoke function pointers, or perform port I/O. `OwnedSerialDiagnosticsReadinessResult` and `OwnedCom1DiagnosticsReadiness` only consume that status-coded metadata to decide whether the guard and marker boundary may be marked ready. `IPlatformSerial`, `GuardedPlatformSerial`, and `Com1PlatformSerialTransport` route host-test bytes to injected/fakeable transports only; they do not prove real COM1 port I/O or output after real `ExitBootServices`.

`CpuFaultEntryFrameLayout`, `CpuFaultPanicReadiness`, and
`CpuFaultPanicReadinessFormatter` are storage/layout, composition, and
caller-owned marker-format evidence only. They do not prove live interrupt
stubs, CPU register capture, CR2/CR3 reads, IDT installation, owned COM1 panic
output, or QEMU fault behavior.

`BootSequence.RunControlledFaultProbeReadiness` is a marker/readiness helper only. It does not trigger invalid-opcode or null-write probes and is not wired into the default boot path.

`DiagnosticsChainReadiness` and `BootSequence.RunDiagnosticsChainReadiness` are host-tested aggregate readiness/marker contracts only. They do not install descriptor tables, execute generated stubs, perform COM1 port I/O, trigger controlled faults, wire a real panic path, or prove QEMU fault handling.

`DiagnosticStubInvocationReadiness`, `DiagnosticStubCallableBindingPlan`, and `BootSequence.RunDiagnosticStubInvocationReadiness` are host-tested address-range, callable-binding metadata, and marker contracts only. They do not dereference generated stub addresses, create delegates, perform CPU port I/O, change page permissions, flush instruction caches, or prove QEMU serial output from an OS-owned COM1 backend. Live unmanaged function-pointer binding and invocation remain future work.

`DiagnosticStubInvocationReadinessFormatter` is caller-owned marker-format evidence only. It does not write to serial, invoke stubs, mutate page permissions, or replace the managed `BootSequence` marker path.

`DiagnosticStubCallableBindingPlanDiagnosticFormatter` is caller-owned
marker-format evidence only. It does not write to serial, bind delegates,
invoke generated stubs, perform port I/O, mutate page permissions, or replace
the managed `BootSequence` marker path.

`ProbeCatalogDiagnosticFormatter` is caller-owned marker-format evidence only. It does not execute probes, install fault handlers, trigger destructive probes, perform live device I/O, write to serial, or prove QEMU panic behavior.

`SerialDiagnosticsGuard` is a readiness contract only. `SerialPortConfiguration`, `Com1SerialWriter`, `Com1PlatformSerialTransport`, `PortIoOperationPlan`, `PortIoStubPlan`, and owned-serial readiness derivation are configuration, polling, abstract backend, status, adapter, and instruction-byte contracts only. They do not execute CPU port I/O instructions or prove serial output survives real firmware shutdown.

## Next Steps

1. Wire `PanicSerialReporter` into a real panic-safe serial path after the owned serial platform boundary has a live COM1 backend.
2. Add an OS-owned COM1 port-I/O backend for `Com1SerialWriter` using the operation, callable-stub, backend-readiness, backend-contract, executable-stub memory, and `IPlatformSerial` contracts, and wire it through `SerialDiagnosticsGuard`.
3. Add IDT/GDT/TSS/IST setup using the descriptor, IDTR, GDT, TSS/IST, readiness, install-readiness, table-plan, vector, descriptor-instruction, and descriptor-install stub contracts.
4. Add real executable-stub page transitions using the W^X layout, materialization, and page-transition plan contracts.
5. Add live diagnostic-stub invocation only after generated stubs can be safely mapped executable, range-checked, callable-binding metadata has been consumed by a real unmanaged binding path, cache-flushed if required, and called through unmanaged function pointers.
6. Wire the COM1 backend contract into an owned serial writer only after generated stubs can be safely mapped executable and invoked.
7. Add the controlled null-read and invalid-opcode probes behind `Probes.cs`
   toggles only in their dedicated issues; M02-014 must consume the disabled
   M01-015 intent after owned diagnostics exist.
8. Wire real handler register capture into `CpuFaultFrame` through the deterministic fault-entry layout.
