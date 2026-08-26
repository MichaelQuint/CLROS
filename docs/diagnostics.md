# Diagnostics

## Current Status

The owned diagnostics subsystem remains host-testable except for the accepted
bounded pre-`ExitBootServices` M02-004 configuration, M02-005 marker, M02-006
panic-readiness, M02-009 terminal GDT/TSS, and M02-010 interrupt-entry-page
materialization, plus M02-011 owned-IDT population/readback paths. The accepted
M02-011 guest profile did not load IDTR. M02-007 adds a
host-validated, allocation-free owned GDT encoder. M02-009 adds a host-audited
terminal GDT/TSS context and composite instruction-byte stream, and its
separately merged default-off EFI integration has fresh QEMU/OVMF evidence for
loading and reading back that private GDTR/TR state, emitting the exact owned
COM1 transcript, and remaining in its terminal halt loop. The M02-010 leaf adds
a host-audited 4,096-byte interrupt-entry page that normalizes all 256 vectors
to the semantic prefix `vector`, `hasHardwareErrorCode`, `errorCode`; its
separately integrated default-off EFI profile now has QEMU/OVMF evidence for
materializing and reading back that page in temporary LoaderData memory and
freeing it before ordinary memory-map capture. No generated slot was executed.
The M02-011 leaf now adds a host-audited 4,096-byte owned IDT page containing
all 256 present DPL0 interrupt gates. Every handler is derived as the M02-010
page base plus `vector * 16`; selector `0x0008` is fixed; NMI, double fault,
and page fault use IST3, IST1, and IST2; the other 253 vectors use IST0; and the
API accepts no firmware table or firmware gate source. The leaf itself remains
non-executing. Its accepted separate default-off EFI profile proves guest
population, readback, and release only; it does not load IDTR.
The M02-012 host leaf adds an exact 80-byte caller-owned IDTR transition
context and a frozen 52-byte reversible Windows-x64 instruction stream. The
stream is represented as ordinary bytes only: it captures IF, executes `cli`,
captures the previous IDTR, temporarily loads and reads back the requested
IDTR, restores and reads back the previous IDTR, conditionally restores IF,
and returns cookie `0x4F53494454523132`. Host tests never invoke those bytes.
The leaf has no EFI compile linkage or QEMU guest evidence yet, so it does not
prove that any processor loaded or restored an IDTR or continued execution.
No ordinary
boot phase loads an IDT, installs fault handlers, activates IST routing, routes
a live CPU fault to panic, or preserves an owned serial path across real
`ExitBootServices`. The accepted M01-026 tuple uses firmware-visible serial plus
an issue-specific host QMP/physical-memory capture mechanism while firmware is
still available; that observation mechanism is not OS-owned diagnostics and
does not prove or provide an OS-owned diagnostic channel across real
`ExitBootServices`. The repository now has host contracts for serial
diagnostics readiness, COM1 configuration planning, a COM1-backed platform
serial transport over a fakeable byte-port backend, owned-serial readiness
derivation from COM1 backend prerequisites, aggregate owned COM1 diagnostics
readiness metadata, CPU fault-frame formatting, deterministic CPU fault-entry
frame storage, caller-owned panic text buffering, a bounded allocation-free
panic formatter/reporting route over the checked COM1 backend, x64 IDT gate
descriptors, IDT table planning, required fault-vector metadata, x64
instruction-byte plans for future port-I/O, descriptor-table, and fault-entry
stubs, byte-copy materialization of planned diagnostic stubs into caller-owned
buffers, a page-transition plan contract for future W^X handoff, a host-only
diagnostics-chain readiness aggregate across the current prerequisite
contracts, a post-chain diagnostic-stub invocation readiness guard that
validates resolved COM1 stub addresses against the planned executable payload
range without invoking them, and a value-only diagnostic-stub callable-binding
plan that checks materialized COM1 callable preconditions before any live
COM1/serial invocation.

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
- `OwnedX64GdtContainingNullN64BitCode` is the M02-007 pointer-first encoder for
  a caller-owned 40-byte GDT. It emits an all-zero null descriptor, conventional
  flat 64-bit ring-zero code and data descriptors, and one 16-byte available
  64-bit TSS descriptor at selectors `0x08`, `0x10`, and `0x18`; the resulting
  GDTR limit metadata is 39. It validates destination capacity, nonzero
  canonical 48-bit x64 TSS addresses, and TSS limits from 103 through
  `0xFFFFF` before mutation. It then reads back all 40 bytes and records an
  FNV-1a64 fingerprint without allocation. Host `Span` wrappers are excluded
  under `NOSTDLIB_EFI`; the always-compiled core uses explicit pointers and
  lengths. This owns bytes only: it does not execute `lgdt` or `ltr`, install a
  TSS, change `BootSequence`, or enable a probe.
- `OwnedGDTTSSGeneratedAuditedCSharpStubBytes` is the M02-009 pointer-first,
  allocation-free materializer for a 168-byte caller-owned terminal context and
  a relocation-free 2,987-byte Windows-x64 instruction stream. The context
  freezes a ten-byte load GDTR, ten-byte readback GDTR, readback TR selector,
  40-byte writable GDT, and 104-byte TSS. Its selectors are `0x00`, `0x08`,
  `0x10`, and `0x18`; only `0x18` is loaded by `ltr`; the GDTR limit is 39; and
  every TSS byte is zero except the two-byte I/O-map base at offsets 102-103,
  which encodes 104. The GDT must remain writable because `ltr` changes the TSS
  descriptor access byte from available `0x89` to busy `0x8B`.
- The composite RCX-context stream performs `cli`, `lgdt`, `ltr`, `sgdt`, and
  `str`; compares the full ten-byte GDTR, selector `0x18`, and busy TSS access
  byte; then writes exactly one success or failure transcript through inline
  COM1 THRE polling at `0x3FD` and data writes at `0x3F8`. Marker bytes are
  immediate operands, not address-dependent pointers. The audited instruction
  set uses only volatile `RAX`, `RCX`, and `RDX`, has no call, return, stack, or
  RSP operation, and both paths end in `cli; hlt; jmp` terminal loops. The
  frozen stream fingerprint is FNV-1a64 `C9C04DEE967D640A` and SHA-256
  `424C98FE039EB46B66EC8C3D8BB954EC7BBB24E6434817079F9B2E17478B8504`.
- `PrepareContext`, `Emit`, and `ValidateReadBack` reject null, short,
  misaligned, noncanonical, wrapping, canonical-hole-crossing, and overlapping
  actual or modeled ranges before mutation. Their always-compiled pointer cores
  use POD/status results; host `Span` overloads are excluded under
  `NOSTDLIB_EFI`. This leaf materializes and audits bytes only. Host tests never
  invoke the stream, and this leaf does not change `BootSequence`, `Probes`,
  `EfiEntry`, `OS.csproj`, or any build script.
- The separately merged default-off M02-009 EFI integration consumes that exact
  leaf after allocating disjoint temporary code and writable context pages. Its
  accepted QEMU/OVMF profile enters the stream after `OS_SMOKE_PASS`, observes
  the exact GDTR/TR success transcript through owned COM1, remains terminal,
  and is stopped by the evidence runner. This bounded profile is recorded in
  `done/step918.md`; it remains pre-`ExitBootServices` and is not a default
  `BootSequence` phase.
- `NormalizeErrorCodeNonErrorCodeInterruptFrame` is the M02-010 pointer-first,
  allocation-free materializer for exactly one caller-owned 4,096-byte page of
  256 fixed 16-byte interrupt-entry slots. Every slot pushes an immediate-32
  positive vector before jumping through a checked `rel32` to one caller-named
  common target. Vectors 8, 10, 11, 12, 13, 14, 17, 21, and 30 preserve the
  CPU-pushed error code and add `hasHardwareErrorCode=1`; the remaining 247
  vectors add a synthetic zero error code and
  `hasHardwareErrorCode=0`. Both forms expose the same semantic stack prefix:
  `vector`, `hasHardwareErrorCode`, `errorCode`.
- The M02-010 pointer core validates actual and modeled page alignment,
  canonical x64 ranges, capacity, wrap, canonical-hole crossing, common-target
  overlap, and all 256 relative-jump displacements before writing. It mutates
  only the first 4,096 bytes, calculates an FNV-1a64 fingerprint, immediately
  reads back every byte, and exposes a separate readback operation that reports
  the first mismatching byte. Host `Span` wrappers are excluded under
  `NOSTDLIB_EFI`; the always-compiled path has no managed references,
  allocation, exceptions, or dependency on `FaultVectorCatalog`.
- The separately integrated default-off M02-010 EFI profile links that exact
  pointer core after the M02-009 source and before the M04 conformance sources.
  After firmware console discovery and any enabled M04 pre-memory-map checks,
  it allocates exactly one 4,096-byte UEFI LoaderData page, uses the real page
  address as the modeled address, and names `pageAddress + 4096` as a
  non-overlapping modeled common target. Any EFI-successful allocation becomes
  owned before returned-address validation, so an invalid zero address is also
  released exactly once. It calls `Materialize` once and
  `ValidateReadBack` once in that order, audits all 256 vectors, the nine/247
  hardware/synthetic split, all 4,096 compared bytes, and both
  `428D0353A96B7ACE` fingerprints, then calls `FreePages` exactly once after the
  readback attempt. Ready/pass requires successful cleanup. The final accepted
  QEMU/OVMF run at source head
  `b88a485c8562a0022ffaae760fb47ca9d5addba2` observed the exact guest marker
  once. This profile never calls a generated slot or common target and does not
  install an IDT, IDTR, TSS, or IST.
- `PopulateAllN256IDTVectorsOwnedGatesNever` is the M02-011 pointer-first,
  allocation-free materializer for one caller-owned 4,096-byte IDT page. It
  validates the actual pointer, modeled IDT address, and owned M02-010 stub-page
  address for capacity, 4,096-byte alignment, canonical x64 ranges, wrap,
  canonical-hole crossing, and overlap before the first write. It derives all
  256 unique handlers as `ownedStubPageAddress + vector * 16`, fixes selector
  `0x0008`, present DPL0 interrupt-gate attributes `0x8E`, reserved-zero bytes,
  and the IST3/IST1/IST2 policy for NMI/#DF/#PF. All remaining gates use IST0.
- The M02-011 API deliberately accepts no prior IDT, firmware gate, handler
  array, selector, gate type, DPL, or present-bit input, so copied firmware
  gates are structurally impossible. It writes only the first 4,096 bytes,
  immediately reads every byte back, reports the first mismatch and the
  address-dependent encoded FNV-1a64, and exposes stable normalized ownership
  fingerprint `8514F42EBDFC5D55`. Host `Span` wrappers are excluded under
  `NOSTDLIB_EFI`; the always-compiled core has no managed references,
  allocation, exceptions, or external descriptor dependency.
- The M02-011 leaf itself still creates and validates ordinary bytes only. The
  separately integrated, default-off EFI profile composes it with the frozen
  M02-010 pointer core inside one temporary three-page `EfiLoaderData` arena:
  page zero holds the 256 normalized slots, page one holds the 256 owned IDT
  gates, and page two owns the modeled common-target address at delta 8,192.
  The profile materializes and reads back both 4,096-byte regions, audits the
  M02-010 FNV-1a64 `641BA73668C75E0E` and the M02-011 normalized ownership
  fingerprint `8514F42EBDFC5D55`, accepts no firmware gate input, and releases
  all three pages before ordinary memory-map capture. A real QEMU/OVMF run
  observed the exact M02-011 guest transcript once. This integration still
  does not publish executable memory, load IDTR, install active GDT/TSS/IST
  state, execute a slot or common target, trigger a fault, or operate after
  `ExitBootServices`.
- `EFI_BUFFER_TOO_SMALL`, memory-map descriptor growth, a stale map key, and
  `ExitBootServices` retry exhaustion are deliberately inapplicable to the
  M02-011 profile. Its temporary allocation, two production materializations,
  two readbacks, and exactly-once release all finish before the ordinary
  memory-map capture begins, and the profile never calls `ExitBootServices`.
- `IDTRContinuedExecution` is the M02-012 pointer-first, allocation-free byte
  planner and synthetic evidence auditor. Its exact unmanaged RCX context is
  80 bytes: requested, previous, loaded, and restored ten-byte IDTR images at
  offsets 0, 16, 32, and 48 with six reserved-zero bytes after each image,
  followed by entry and return RFLAGS at offsets 64 and 72. Context and stub
  storage require 16-byte alignment; the independently supplied owned IDT
  address requires 4,096-byte alignment and a complete canonical 4,096-byte
  span. Null, short, misaligned, noncanonical, wrapping,
  canonical-hole-crossing, actual-overlap, modeled-overlap, and IDT-overlap
  failures are status-coded and reject before destination mutation.
- The relocation-free 52-byte stream uses the Microsoft/UEFI x64 ABI with the
  context in `RCX`, only volatile `RAX` and `RDX` scratch registers, balanced
  `pushfq`/register-pop operations, no calls, two `lidt`, three `sidt`, one
  `cli`, one conditional `sti`, and one `ret`. Its frozen FNV-1a64 is
  `AFF59D6480DE7F3C`; its host-verified SHA-256 is
  `41B9015D93354D734EC939531790C573CA1239EBC0413FB512B39A80DFC5E91E`.
  Pointer cores prepare the context, emit the stub, validate all 52 bytes on
  readback, and audit caller-supplied observations without managed allocation.
  Normal host builds add `Span<T>` wrappers under `#if !NOSTDLIB_EFI`; host
  tests compile the exact pointer source with `NOSTDLIB_EFI` and no BCL
  references.
- Synthetic execution audit requires the requested IDTR and all reserved bytes
  to remain exact, cookie `0x4F53494454523132`, a captured previous IDTR that
  differs from the request, loaded readback equal to `{ limit=4095,
  base=ownedIdt }`, restored readback byte-equal to the captured previous
  IDTR, and equality of the IF bit only between entry and return RFLAGS. It
  accepts both entry-IF states and rejects wrong cookie, absent transition,
  loaded/restored mismatch, reserved/request mutation, and IF mismatch. These
  are synthetic host observations, not guest execution evidence.
- The exact host transcript is:

  ```text
  TEST_START m02.012
  CLROS_M02_012_HOST_READY contextBytes=80 stubBytes=52 idtrBytes=10 tableBytes=4096 limit=4095 lidtPlan=2 sidtPlan=3 cliPlan=1 stiPlan=1 returnCookie=4F53494454523132 readBack=true guestExecution=false idtrLoaded=false idtrRestored=false continuedExecution=false faultTriggered=false postExitBootServices=false
  TEST_PASS m02.012
  ```

  The live success boundary is deliberately not claimed by this leaf. A
  separately owned, default-off EFI/QEMU integration must link and publish the
  reviewed bytes, invoke them synchronously, audit the real processor-written
  observations, prove firmware IDTR and IF restoration, release owned
  resources, and complete ordinary smoke before M02-012 can close. Even that
  bounded proof will not establish interrupt/fault delivery, executable common
  dispatch, active GDT/TSS/IST routing, real `ExitBootServices`, or Gate 3.
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
- `PortIoStubPlan` emits deterministic callable x64 COM1 byte-port stubs for unmanaged `Out8` and `In8` function pointers, reusing `PortIoOperationPlan` for the final privileged operation byte and ending each stub with `ret`. The Windows x64 `Out8(ushort port, byte value)` sequence is exactly `88 D0 66 89 CA EE C3`: it copies incoming `DL` to `AL` before loading `CX` into `DX`, so loading the port cannot clobber the byte value. `In8` remains exactly `66 89 CA EC C3`. It rejects default/zero result values and incomplete byte-count metadata as not successful.
- `ManagedX64StubStrategyADRUseExistingAudited` records the allocation-free,
  status-coded M02-001 authoring decision over those exact plans. One
  demonstrated port-I/O family selects `ExistingAuditedBytePlans`; a build-time
  generator becomes eligible only at two independent demonstrated families.
  The current result requires the exact seven-byte `Out8` and five-byte `In8`
  contracts, write-then-execute publication, and no permanent RWX mapping. The
  `BuildTimeGenerator` enum value represents eligibility only; this slice
  implements no generator.
- `ExistingIn8Out8StubBytesRWPagesFinalize` materializes the fixed M02-002
  4,096-byte page into caller-owned, page-aligned storage. The page contains
  `B8 4B 4F 53 4F C3` at offset 0, the existing seven-byte `Out8` plan at
  offset 16, the existing five-byte `In8` plan at offset 32, and zeroes
  everywhere else. Its occupied extent is 37 bytes, its instruction-byte count
  is 18, its full-page FNV-1a64 is `982E5B7E3EE58E56`, and its independently
  host-verified SHA-256 is
  `B45339EB93D346D9FE6B32700F227CFB367AB1D10970DD6655A173AFE54705E2`.
  Always-compiled pointer cores provide materialize, full-page readback, and
  metadata-only finalization operations; normal host builds additionally expose
  `Span<T>` wrappers under `#if !NOSTDLIB_EFI`. A caller-owned value state
  advances only through `Uninitialized`, `Materialized`,
  `ReadBackVerified`, and `FinalizationAudited`, with replay/stale-version
  rejection; state storage that aliases the page is rejected before any caller
  storage changes. The initial state is audited as RW+NX and the final state as
  RO+X. Tests compile the exact pointer source with `NOSTDLIB_EFI`, framework
  references disabled, and zero assembly references, and separately execute
  that exact `NOSTDLIB_EFI` pointer path in a bounded normal host process; the
  zero-corelib artifact itself is compile-closure evidence, not a CLR-runnable
  binary. The implementation changes no live page protection and invokes
  neither the safe entry nor either privileged port-I/O entry.
- `M02SafeStubPageIntegration` is the no-stdlib M02-017 EFI adapter for that
  reviewed page contract. Behind the default-off `M02_SAFE_STUB_PROBE` switch,
  it materializes the complete 4,096-byte page into the existing M01
  protection-probe allocation, verifies the full-page FNV-1a64
  `982E5B7E3EE58E56`, carries the page through the inherited immutable
  publication and temporary owned-root RX boundary, and audits only the
  offset-zero return-cookie entry. The call supplies no port or value
  arguments, never targets offsets 16 or 32, requires return cookie
  `0x4F534F4B`, and records `portIoExecuted=false` only after the inherited
  CR3/CR4/RFLAGS and protection restoration checks have completed.
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
- `Com1PortIoBackendPlan` validates the host-tested prerequisites for an OS-owned COM1 port-I/O backend: serial configuration, byte-port operation plans, the ABI-preserving callable stub order, and executable-stub memory layout capacity. It rejects default/zero result values and incomplete success metadata as successful evidence.
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
- `InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind` consumes only a fully
  initialized callable-binding plan, independently rechecks the configured
  COM1 register window, payload bounds, byte counts, address/offset identity,
  and non-overlapping callable ranges, and only then casts the two addresses to
  the exact `delegate* unmanaged<ushort, byte, void>` `Out8` and
  `delegate* unmanaged<ushort, byte>` `In8` signatures. The returned value-only
  binding invokes each pointer independently, rejects calls outside COM1 ports
  `0x3f8` through `0x3ff` before control transfer, creates no managed delegate,
  and reports a status result after a call returns. Host tests execute safe
  `[UnmanagedCallersOnly]` targets to prove both calls and their argument/return
  flow; they intentionally do not execute the privileged generated `in`/`out`
  bytes or perform live device I/O.
- `M02CheckedPortIoFunctionPointerIntegration` compiles that same production
  binder into the explicit no-stdlib EFI closure. After the accepted M02-017
  safe-offset execution audit has completed and firmware paging state has been
  restored, it derives the checked offset-16/offset-32 addresses from the
  accepted page metadata and requires a ready binding. It returns value-only
  status and never calls `InvokeOut8` or `InvokeIn8`; the guest marker therefore
  reports `guestInvocation=false portIoExecuted=false`.
- `COM1OwnedPortIOBackend` wraps the checked value-only binding with a bounded
  COM1 register window and status-coded `Out8`/`In8` results. Its default
  configuration path performs exactly seven writes in order: IER `0x00`, LCR
  `0x80`, divisor low `0x01`, divisor high `0x00`, LCR `0x03`, FCR `0xC7`, and
  MCR `0x0B`. It rejects an unready binding and ports outside `0x3F8..0x3FF`.
  Host tests use safe unmanaged callbacks and separately prove the existing
  serial writer adapter and line-status read path.
- `M02Com1OwnedPortIoBackendIntegration` compiles that backend into the
  default-off no-stdlib EFI profile. Only after M02-017 safe execution,
  firmware paging/restoration audits, and M02-003 checked binding succeed, it
  calls `ConfigureDefault()` once and requires seven completed writes. Fresh
  QEMU/OVMF evidence emitted exactly one
  `CLROS_M02_004_READY configurationWrites=7 bindingReady=true guestPortIo=true guestIn8=false markerTransport=uefiSerial postExitBootServices=false`.
  The marker still travels through UEFI serial; this slice does not poll `In8`
  or write payload bytes through the owned backend.
- `OwnedCom1ReadyExclusivelyOwnedWriter` is the allocation-free M02-005
  payload writer over `Com1OwnedPortIoBackend`. It configures COM1 with the
  accepted seven-write sequence, polls line-status register `0x3FD` through
  checked `In8`, and writes exactly `OWNED_COM1_READY\r\n` to data register
  `0x3F8` through checked `Out8`. The result records configuration, byte and
  polling counts, the failed byte index, and explicit owned-backend/no-firmware-
  fallback evidence; invalid limits, an unready backend, and transmit timeout
  fail closed. Host tests use safe unmanaged callbacks; the separately claimed
  default-off EFI integration then links this exact source, calls it once after
  the accepted M02-004 path, and fails the optional profile if any count or
  ownership result differs. A development QEMU/OVMF run observed the standalone
  `OWNED_COM1_READY` line exactly once from the checked COM1 transport, followed
  by a distinct UEFI-serial evidence line reporting `guestPortIo=true`,
  `guestIn8=true`, and `postExitBootServices=false`. The exact-head
  implementation and tracker closure are merged; this pre-firmware proof is
  still not owned post-firmware diagnostics.
- `PanicSafeFormattingSerialReportingOwnedCom1` is the M02-006 production
  formatter and reporter over `Com1OwnedPortIoBackend`. It validates bounded
  ASCII subsystem/reason tokens and x64 vector metadata, calculates the complete
  record size before mutation, and writes an exact `OS_SMOKE_FAIL` fault record
  plus CR/LF into caller-owned scratch. The reporting path configures COM1,
  polls THRE through checked `In8(0x3FD)`, and writes every formatted byte
  through checked `Out8(0x3F8)` with status-coded configuration, read, timeout,
  and write failures and no firmware fallback. `EmitReadinessMarker` uses the
  same owned path for one harmless `CLROS_M02_006_READY ...` line. The
  no-stdlib core uses explicit pointers and lengths; its `Span` convenience
  overloads are excluded from the EFI closure. Eleven focused host cases use
  safe unmanaged callbacks and report `guestPortIo=false`.
- The separately claimed default-off M02-006 EFI integration links exactly that
  production source as compile item 55 of 55, after the M02-005 writer. A fresh
  development QEMU/OVMF run configured COM1 with seven writes, performed 127
  checked line-status polls, and emitted the 125 marker bytes plus CR/LF through
  checked COM1. It observed exactly one direct marker followed by exactly one
  fixed UEFI evidence line with `guestPortIo=true`, `guestIn8=true`, and
  `postExitBootServices=false`, all before `TEST_PASS m01.stage3`; the failure
  marker and every `TEST_FAIL` were absent. This is harmless readiness evidence,
  not a triggered CPU fault, live panic route, or post-`ExitBootServices`
  diagnostic path. The exact-head implementation, EFI integration, independent
  review, tracker closure, and canonical manifest closure are merged.
- `DiagnosticStubCallableBindingPlanDiagnosticFormatter` formats initialized
  callable-binding plan metadata into stable caller-owned ASCII
  `DIAGNOSTIC_STUB_CALLABLE_BINDING status=` marker bytes. It reports required
  and available byte counts for short destinations, rejects default or unknown
  status metadata before writing, keeps the format result free of managed
  references, and uses the shared `OS.Std.Text.AsciiDecimalFormatter` and
  `OS.Std.Text.AsciiTextBuilder` helpers for byte-counting and marker writes.
  This is host-tested formatter evidence only; it does not bind delegates,
  invoke generated stubs, perform CPU port I/O, or write serial output.
- `Probes.cs` groups probe definitions by boot smoke, serial smoke, EFI smoke,
  panic smoke, memory-map smoke, staged M01 live memory, UEFI
  `ExitBootServices`, ACPI discovery, storage smoke, and network smoke. It
  exposes a caller-buffer probe catalog (`ProbeCatalogCount`, `TryGetProbe`,
  and `CopyProbeCatalog`) so diagnostics tooling can enumerate the compile-time
  probe surface without allocating or duplicating individual getter calls.
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
  - `EnableM01LoadedImageStackProbe`
  - `EnableM01LiveAllocatorProbe`
  - `EnableM01OwnedPagingProbe`
- Loaded-image and owned-stack integration is stage one, live allocator
  integration is stage two, and owned paging is stage three.
  `ValidateM01LiveProbePolicy` recognizes only the ordered prefixes `000`,
  `100`, `110`, and `111`; it rejects allocator-without-stage-one as
  `LiveAllocatorRequiresLoadedImageStack` and paging-without-allocator as
  `OwnedPagingRequiresLiveAllocator`. The byte-valued
  `M01LiveProbePolicyStatus` keeps zero reserved for `Uninitialized` and uses
  `Success = 1`, `LiveAllocatorRequiresLoadedImageStack = 2`, and
  `OwnedPagingRequiresLiveAllocator = 3`.
- The M01 switches are catalog policy only. They do not link M01 sources into
  the EFI project or change `OS.csproj`, `BootSequence`, `EfiEntry`, build
  scripts, boot order, memory, page tables, or CR3. The page-zero fault trigger
  remains disabled and deferred to M02-014.
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

`tests/OS.HostTests/DiagnosticsTests.cs` covers log prefixes, boot-event
mirroring, panic formatting, page-fault and invalid-opcode register dump
formatting, invalid-opcode/null-dereference probe reason strings,
return-to-kernel panic mode, probe group metadata, safe probe defaults, probe
catalog enumeration/capacity reporting, caller-owned probe catalog diagnostic
formatting, and dangerous probe defaults. It also verifies the exact M01 probe
field names and literal-false compile-time values, fixed catalog indexes
13 through 15, unique catalog membership, the complete eight-row staged-policy
truth table, continued M02-014 null-fault ownership, and host-only marker
ownership. Probe catalog formatter markers include
`TEST_PASS probes catalog formatter formats all definitions`,
`TEST_PASS probes catalog formatter reports destination capacity`, and
`TEST_PASS probes catalog formatter keeps dangerous probes disabled`.

The M01-016 host-policy aggregate emits:

```text
TEST_START m01.016
CLROS_M01_016_READY
TEST_PASS m01.016
```

Any aggregate failure emits `TEST_FAIL m01.016 failures=<count>` and suppresses
the ready/pass markers.

The M01-019 stage-three coordinator host aggregate emits:

```text
TEST_START m01.019
CLROS_M01_019_READY
TEST_PASS m01.019
```

Any aggregate failure emits `TEST_FAIL m01.019 failures=<count>` and suppresses
the ready/pass pair. The fixed stage-three transcript presented by
`BootSequence.RunM01LivePagingIntegration` is host-contract output only. The
default boot sequence remains unchanged and all three M01 `Probes.cs` switches
remain disabled. No marker is emitted from inside the modeled owned-CR3
interval; `M01_OWNED_CR3_ENTERED` is deliberately caller-owned POD state rather
than a serial marker. The post-restoration protection, TLB, constant-return
probe, and page-zero lines report separate, mutually cross-checked host models
that did not independently prove execution when M01-019 closed. The later
accepted M01-026 tuple executed the exact integrated machine-code interval and
recovered the corresponding guest-written register/POD observations through
raw physical-memory capture. That result does not retroactively turn the
isolated M01-019 presenter into live evidence.
`CLROS_M01_LIVE_BOOT_MEMORY_READY` is guest evidence only inside the
source-bound accepted M01-026 tuple; in isolated host output or arbitrary logs,
it remains a reserved contract line.

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
TEST_START m02.007
CLROS_M02_007_READY encoding=hostValidated callerOwned=true readBack=true guestEncoding=false guestLgdt=false guestLtr=false allocationFree=true
TEST_PASS m02.007
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
TEST_START m02.001
TEST_PASS m02.001 selects existing audited plans
TEST_PASS m02.001 verifies exact audited bytes
TEST_PASS m02.001 enables generator only for second family
TEST_PASS m02.001 rejects malformed policies
TEST_PASS m02.001 default result is not success
TEST_PASS m02.001 current decision allocates no managed memory
CLROS_M02_001_READY strategy=ExistingAuditedBytePlans families=1 generatorThreshold=2 generatorEligible=false out8Bytes=7 in8Bytes=5 writeThenExecute=true permanentRwx=false
TEST_PASS m02.001
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
TEST_START m02.003
TEST_PASS m02.003 binds and invokes checked unmanaged out8 and in8 targets
TEST_PASS m02.003 rejects an unready binding plan before invocation
TEST_PASS m02.003 rejects an out8 target whose checked offset mismatches
TEST_PASS m02.003 rejects a callable range outside the checked payload
TEST_PASS m02.003 rejects overlapping checked callable ranges
TEST_PASS m02.003 rejects ports outside the configured com1 register window
TEST_PASS m02.003 default results cannot invoke unmanaged targets
TEST_PASS m02.003 results contain no managed references
CLROS_M02_003_READY
TEST_PASS m02.003
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

### M01-026 paging-observation boundary

The accepted M01-026 run is bound to clean implementation commit
`8f1edea17f2de425520e239af58d37aa3b182146`, tree
`6b5359e36293d770e3dc0d074b1baabf8d4fdbf8`. Its full fresh all-switch
QEMU/OVMF acceptance command ran from `2026-07-30T15:36:47.5722979Z` through
`2026-07-30T15:37:49.8210616Z`, observed all 42 issue markers exactly once and
in order, observed none of the 25 issue failure markers, withheld
`M01_OWNED_CR3_ENTERED` from serial, and emitted both
`TEST_PASS qemu.smoke` and `TEST_PASS m01.026.raw-observations`.

The issue-specific host proxy used QMP to pause the 256 MiB guest and capture
sixteen temporary 16 MiB physical-memory chunks. It decoded and validated the
guest-written preflight, runtime-state, and call-gate PODs from those raw
memory captures, then deleted the chunks, private serial, and scratch
directory before releasing the public terminal marker. The resulting 44-key
sidecar binds CR0 `0x80010033`; firmware CR3 `0xF801000`; temporary owned CR3
`0x100000`; CR4 `0x668`; EFER `0xD00`; physical width 40; and RFLAGS `0x246`.
Firmware CR3, CR4, and RFLAGS match their independently reported post-return
runtime values. The public serial SHA-256 is
`CC51100244A8A20865DB9D16CE5D73B0930F43EA98835DFA57027A00B126C661`;
the raw-observation sidecar SHA-256 is
`6FFEDB0B5998A930E614E3EC0EE68E2B444439AE686A9412F3206760D257F933`;
and the evidence-manifest SHA-256 is
`94A28FE683FC068C88F42AB806B3E9A06DB5467FD35488B6ADF397AC8F8A8188`.
The manifest binds 47 nonempty artifacts, 10 tool identities, 44 required
markers, 26 forbidden markers, and 61 observed markers; all artifact hashes
were independently revalidated.

This is bounded pre-EBS local-QEMU evidence for M01, not an owned diagnostic
transport. It proves no real IDT/fault capture, COM1 port I/O, panic-safe
output, real firmware shutdown, physical-hardware behavior, provider behavior,
or post-firmware survivability. The full tuple and claim boundary are recorded
in `docs/qemu.md`, `docs/boot.md`, and `done/step890.md`.
`done/step888.md` remains immutable historical r12 evidence.

## Known Limitations

The current managed formatter and guard contracts allocate managed strings and are only safe for host tests or normal boot diagnostics. `BootEventRingBuffer` is still the managed string mirror used by normal host-tested boot logging. `BootEventRecordRingBuffer` and `BootEventRecordFormatter` are caller-storage based, but they are not wired into `BootSequence`, a real panic dump, or the live EFI path yet. `PanicTextBuffer` and `PanicSerialReporter` are caller-storage based, but they are not wired into a real panic path yet. `CpuFaultFrame` is a data contract and formatter input only; it is not evidence that CPU handlers can capture those registers yet.

`IdtGateDescriptor`, `IdtTablePlan`, `IdtrDescriptor`, `GdtDescriptorPlan`,
`TssIstPlan`, `GuardedISTStacksDoubleFaultPageFaultNMI`,
`FaultInfrastructureReadiness`, `DescriptorTableInstructionPlan`, and
`DescriptorTableInstallStubPlan` are descriptor encoding, table/range-layout,
instruction-byte, ownership, and readiness contracts only. They do not map
guard/stack pages, execute `lidt`, `lgdt`, or `ltr`, install handlers, configure
a live TSS/IST, or prove QEMU fault behavior.

`OwnedX64GdtContainingNullN64BitCode` strengthens only the caller-owned GDT
encoding boundary. Its fixed descriptor order makes duplicate/cyclic capability
lists, BARs, queues, descriptor leasing, timeouts, reset, and DMA ownership
inapplicable; there is no device transaction in this slice. Applicable failures
are destination range/capacity, null or noncanonical TSS addresses, invalid TSS
limits, default results, and byte-level readback corruption, all of which are
host-tested. PE import policy and hosted-runtime exception behavior remain
outside M02-007. Guest encoding is not the same as loading the table, and neither
host nor future guest marker evidence may be promoted to live `lgdt`, `ltr`,
fault handling, provider, cloud-readiness, Kestrel, or production claims.

`OwnedGDTTSSGeneratedAuditedCSharpStubBytes` strengthens only the host-audited
context and terminal instruction-byte boundary. The host harness does not enter
ring-zero instructions, perform port I/O, load GDTR/TR, observe the hardware-set
busy bit, or enter a halt loop, and no `CLROS_M02_009_READY` or
`TEST_PASS m02.009` line from host execution may be treated as guest evidence.
The separately merged integration links this exact source into one default-off
EFI profile and supplies fresh, source-bound QEMU/OVMF terminal evidence. That
accepted proof is still only pre-`ExitBootServices` on the recorded virtual
platform; it proves no installed IDT, active IST route, triggered fault,
post-firmware execution, physical hardware, provider, cloud-readiness, Kestrel,
or production behavior.

`NormalizeErrorCodeNonErrorCodeInterruptFrame` strengthens only the
caller-owned interrupt-entry byte layout and readback boundary. The leaf alone
does not link itself into EFI, map executable memory, bind IDT gates, or enter a
slot. The separately owned default-off profile now proves guest
materialization, complete production readback, and cleanup in one temporary
LoaderData page on the recorded QEMU/OVMF platform. The common target remains
modeled address metadata used only to encode checked relative jumps. Neither
the host leaf nor the guest integration proves instruction execution,
executable publication, IDT/IDTR/IST installation, a normalized live CPU
frame, fault capture, panic routing, or any numbered completion gate.

`PopulateAllN256IDTVectorsOwnedGatesNever` strengthens the caller-owned IDT-byte
construction and readback boundary. Its 256 gates model future owned selector
and IST state. The separate default-off EFI profile now compiles and calls this
leaf, composes it with the M02-010 normalized-slot leaf in a temporary
three-page LoaderData arena, reads both regions back, and releases the arena.
That source-bound QEMU evidence remains population-only: no M02-010 page is
published executable, no IDTR is loaded, no GDT/TSS/IST state is activated, no
generated slot or common target runs, and no fault is triggered. Gate 2 remains
the only passing numbered gate; Gate 3 and Gate 4 remain `Fail`.

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

`Com1PortIoBackendContract` is address metadata only. It resolves future function-pointer target addresses from planned stub offsets, but it does not create delegates, invoke function pointers, or perform port I/O. `OwnedSerialDiagnosticsReadinessResult` and `OwnedCom1DiagnosticsReadiness` only consume that status-coded metadata to decide whether the guard and marker boundary may be marked ready. `IPlatformSerial`, `GuardedPlatformSerial`, and `Com1PlatformSerialTransport` route host-test bytes to injected/fakeable transports only; they do not prove real COM1 port I/O or output after real `ExitBootServices`. The accepted M02-005 EFI profile executes `OwnedCom1ReadyExclusivelyOwnedWriter` over checked generated `In8`/`Out8` targets under QEMU, and the accepted M02-006 profile executes the harmless readiness method of `PanicSafeFormattingSerialReportingOwnedCom1` through that same checked backend. Both remain bounded pre-`ExitBootServices` marker paths. They do not make those readiness/guard abstractions live after firmware shutdown or prove a live panic route.

`CpuFaultEntryFrameLayout`, `CpuFaultPanicReadiness`, and
`CpuFaultPanicReadinessFormatter` are storage/layout, composition, and
caller-owned marker-format evidence only. They do not prove live interrupt
stubs, CPU register capture, CR2/CR3 reads, IDT installation, owned COM1 panic
output, or QEMU fault behavior.

`BootSequence.RunControlledFaultProbeReadiness` is a marker/readiness helper only. It does not trigger invalid-opcode or null-write probes and is not wired into the default boot path.

`DiagnosticsChainReadiness` and `BootSequence.RunDiagnosticsChainReadiness` are host-tested aggregate readiness/marker contracts only. They do not install descriptor tables, execute generated stubs, perform COM1 port I/O, trigger controlled faults, wire a real panic path, or prove QEMU fault handling.

`DiagnosticStubInvocationReadiness`, `DiagnosticStubCallableBindingPlan`, and
`BootSequence.RunDiagnosticStubInvocationReadiness` remain host-tested
address-range, callable-binding metadata, and marker contracts only.
`InvokeIn8Out8CheckedUnmanagedFunctionPointers` now consumes that checked
metadata and performs real unmanaged `Out8`/`In8` control transfers, but the
accepted host targets are safe `[UnmanagedCallersOnly]` test entry points. This
does not prove that the generated privileged stub bytes were the targets, does
not perform CPU port I/O, does not change page permissions or flush instruction
caches, and does not prove QEMU serial output from an OS-owned COM1 backend.

`DiagnosticStubInvocationReadinessFormatter` is caller-owned marker-format evidence only. It does not write to serial, invoke stubs, mutate page permissions, or replace the managed `BootSequence` marker path.

`DiagnosticStubCallableBindingPlanDiagnosticFormatter` is caller-owned
marker-format evidence only. It does not write to serial, bind delegates,
invoke generated stubs, perform port I/O, mutate page permissions, or replace
the managed `BootSequence` marker path.

`ProbeCatalogDiagnosticFormatter` is caller-owned marker-format evidence only. It does not execute probes, install fault handlers, trigger destructive probes, perform live device I/O, write to serial, or prove QEMU panic behavior.

The M01 live-memory probe switches and
`ValidateM01LiveProbePolicy` are host-tested compile-time policy only. They do
not independently establish EFI compile closure, guest marker ownership, QEMU
execution, loaded-image capture, an owned stack, allocator state, page-table
storage, W^X/NX transitions, TLB invalidation, or a CR3 load. The accepted
M01-026 tuple supplies those scoped observations only for the exact integrated
all-switch source and run recorded above. The
`CLROS_M01_016_READY` marker is emitted only by the host test initializer and
must not be interpreted as guest evidence. Likewise, M01-019 freezes and
presents a host-only stage-three result without enabling any probe or emitting
from the ordinary boot path.

`SerialDiagnosticsGuard` is a readiness contract only. `SerialPortConfiguration`, `Com1SerialWriter`, `Com1PlatformSerialTransport`, `PortIoOperationPlan`, `PortIoStubPlan`, `ManagedX64StubStrategyADRUseExistingAudited`, `ExistingIn8Out8StubBytesRWPagesFinalize`, and owned-serial readiness derivation remain configuration, polling, abstract backend, status, adapter, instruction-byte, authoring-policy, caller-owned page-byte, readback, and metadata-audit contracts. The M02-001 strategy allocates, materializes, protects, binds, and executes no page; it provides no generator implementation and does not perform CPU port I/O or prove serial output survives real firmware shutdown. M02-002 is the consumed host-only page contract; by itself it still proves no EFI linkage or live execution.

M02-017 adds one distinct, source-bound local QEMU/OVMF result from clean,
pushed implementation commit
`cf8ce63c37a9c62a0f8f736adda14b6bd3e19a9a`, tree
`d18f520cee19aa0c89dfb41f326c92afd0284c02`. All six M02 markers occurred
exactly once and in order, the 34 cumulative forbidden markers were absent,
and the ready line reported `temporaryOwnedRootRx=true` and
`portIoExecuted=false`. The public serial SHA-256 is
`6F223E27E4180855DA64F4785C13B9B44649F0C8EF1590AA9B65A89CEF508AD0`.
This proves only pre-`ExitBootServices` materialization, full-page readback,
temporary owned-root execution of offset zero, the return cookie, and
restoration on that recorded virtual platform. It does not bind or execute the
privileged `In8` or `Out8` entries, own COM1, install diagnostics or descriptor
tables, trigger a fault, or advance a numbered completion gate.

M02-003 adds a distinct checked-binding result from clean implementation commit
`84032f4b78804194d3352410a1f2749112d94e5f`, tree
`a02bbe8b8ccf6ce7cd13b8957dd081d5476130b3`. The complete host harness exited
zero with one `TEST_PASS host.tests`, no `TEST_FAIL`, and exact M02-003 host and
EFI-integration marker sets. A fresh all-five-switch QEMU/OVMF run emitted
exactly one
`CLROS_M02_003_READY bindingReady=true out8AddressChecked=true in8AddressChecked=true guestInvocation=false portIoExecuted=false`
after `TEST_PASS m02.017` and before `TEST_PASS m01.stage3`; all 35 forbidden
markers were absent. The EFI and public serial SHA-256 values are respectively
`C7666C1580A1C362DB3165166B005478910C5F3FD53C4F451E1A8F4EB57CE794`
and
`CC1DDEBBD8C5355D9C318BADE9F7778349D4EF6949EFA1160E40A304263AADCC`.
This proves the checked addresses and binder readiness in the no-stdlib guest,
not guest invocation of offsets 16/32, live port I/O, COM1 ownership, or
post-`ExitBootServices` diagnostics. Review and merge remain separate tracker
requirements and this result changes no numbered completion gate.

M02-004 supersedes the old host-only `Out8` byte order for live-use suitability.
The earlier M02-002 ledger remains accurate for its historical bytes, while the
current callable sequence preserves `DL` before `DX` is repurposed and binds the
page to FNV-1a64 `982E5B7E3EE58E56` and SHA-256
`B45339EB93D346D9FE6B32700F227CFB367AB1D10970DD6655A173AFE54705E2`.
The current 485,888-byte EFI artifact has SHA-256
`61CA5D6879DA1AE7DC0A6938AA61AD64DCEFBD61AA937C0DF2D8C3BF0809820E`.
One real QEMU/OVMF run observed the exact M02-004 ready marker once, all 50
ordered/unique cumulative markers, and no forbidden marker. The 2,753-byte
serial log SHA-256 is
`E452EF725FAF34775EE00C310E86245F217CA2638926B70B3194C711A0F2876D`.
This proves seven pre-`ExitBootServices` COM1 configuration writes on that
virtual platform. It does not prove `In8` polling, owned-backend serial payload
transport, post-`ExitBootServices` diagnostics, physical hardware, or a numbered
completion-gate advancement.

M02-005 builds on that accepted configuration path without changing the normal
boot profile. Its production writer repeats the seven configuration writes,
polls THRE through checked `In8(0x3FD)` once per emitted byte in the accepted
QEMU case, and writes the 16 marker bytes plus CR/LF through checked
`Out8(0x3F8)`. The development real-QEMU runner observed all 52 cumulative
required/ordered/unique markers, including exactly one standalone:

```text
OWNED_COM1_READY
```

followed by exactly one separately transported evidence line:

```text
CLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 configurationWrites=7 lineStatusPolls=18 ownedWriter=true ownedMarkerTransport=com1PortIo evidenceMarkerTransport=uefiSerial guestPortIo=true guestIn8=true postExitBootServices=false
```

No `TEST_FAIL` or `M02_OWNED_COM1_READY_FAILED` marker appeared. This proves the
bounded checked COM1 read/write path only on the recorded QEMU/OVMF platform
before `ExitBootServices`. The exact-head implementation and tracker closure are
merged. It proves no panic routing, post-firmware output, physical hardware,
provider behavior, or numbered completion-gate advancement.

M02-006 now has a host-validated production formatter/reporting route over the
same checked backend. The complete host harness emitted:

```text
TEST_START m02.006
CLROS_M02_006_READY panicFormatting=hostValidated ownedCom1Transport=hostCallbacks guestPortIo=false allocationFree=true
TEST_PASS m02.006
TEST_PASS host.tests
```

The captured 874,888-byte host log has SHA-256
`A675033BD054AB13508FBF9BA9E903DF0745170F048E08A3F667D009FD69E9BA`
and contains zero `TEST_FAIL` lines. This proves exact caller-owned formatting,
checked callback routing, THRE polling, timeout and invalid-input failures,
short-buffer atomicity, unmanaged result layouts, and zero hot-path managed
allocations. It does not prove that privileged guest `In8`/`Out8` executed; the
host marker explicitly records `guestPortIo=false`, and real QEMU/OVMF evidence
requires a separately owned EFI integration lane.

M02-007 host tests encode the exact 40-byte table, prove the conventional code
and data bytes plus the full 64-bit TSS base and 20-bit limit, reject all
applicable invalid metadata before mutation, detect the first corrupted readback
byte, preserve bytes beyond the owned prefix, and measure zero managed
allocations across 1,024 pointer-core encodings. Its separately owned default-off
EFI adapter has also executed that same encoder and exact readback under fresh
QEMU/OVMF evidence. That guest result remains pre-`ExitBootServices` and reports
`guestLgdt=false` and `guestLtr=false`; it does not install the GDT or TSS. The
independently reviewed implementation, integration, and manifest closure are
merged, and managed M02-007 is closed.

M02-008 adds a caller-owned guarded-IST arena allocator for double fault, page
fault, and NMI. It deterministically reserves one non-present guard range followed
by one writable, non-executable downward-growing stack range for each fixed IST
index. Initialization rejects null/noncanonical/misaligned arenas, invalid page
multiples, total-size overflow, end-address wrap, noncanonical ends, exhaustion,
reserved-range overlap, missing ownership, and writable-executable metadata before
state mutation. Arena and reserved ranges must keep their inclusive end in the
same x64 canonical half as their start, so a huge range cannot bridge the
noncanonical address hole. Generation-bound leases reject duplicate allocation,
wrong owners, stale ownership, invalid leases, and double release. The production
path uses value/status structs only and allocates no managed memory. A separately
owned default-off EFI integration now allocates a temporary six-page UEFI
LoaderData arena, runs the same layout contract, writes and reads back only the
three stack ranges, releases the lease, and frees the same arena before return.
Fresh real-QEMU/OVMF evidence accepted all 56 required ordered/unique markers
and rejected all 40 forbidden markers. The independently reviewed integration
and manifest closure are merged, and managed M02-008 is closed. The proof does
not change page tables, map guards non-present, write TSS IST slots, load
descriptor tables, install handlers, execute a fault, or cross real
`ExitBootServices`.

M02-009 host tests independently reconstruct all 168 context bytes, decode and
account for all 2,987 instruction-stream bytes, verify every opcode, operand,
branch target, COM1 port, inline marker byte, and terminal halt loop, and freeze
the exact FNV-1a64 and SHA-256 values. They mutate every context and stream byte
to prove first-mismatch rejection and failure atomicity, cover pointer,
capacity, alignment, canonicality, overflow, and overlap boundaries, and prove
zero managed allocation across the pointer path. The emitted host-only evidence
is:

```text
TEST_START m02.009
CLROS_M02_009_HOST_READY contextBytes=168 stubBytes=2987 fnv1a64=C9C04DEE967D640A sha256=424C98FE039EB46B66EC8C3D8BB954EC7BBB24E6434817079F9B2E17478B8504 guestExecution=false lgdt=false ltr=false ownedCom1=false terminalHalt=false
TEST_PASS m02.009
```

The separately owned, default-off EFI integration has now emitted the following
exact success lines, in order, after entering the terminal stream and verifying
GDTR/TR in the guest:

```text
CLROS_M02_009_READY gdtr=true tr=true gdtBytes=40 tssBytes=104 gdtrLimit=39 trSelector=24 ownedCom1=true terminalHalt=true postExitBootServices=false
TEST_PASS m02.009
```

The exact failure transcript is:

```text
M02_OWNED_GDT_TSS_VERIFY_FAILED
TEST_FAIL m02.009 gdtr_or_tr_mismatch
```

An unexpected return from either terminal path remains forbidden. The host-only
leaf does not produce those guest lines; the accepted source-bound QEMU/OVMF
profile recorded in `done/step918.md` produced them exactly once, observed all
59 required/ordered/unique records, observed none of 43 forbidden records, and
ended with runner disposition `runner-terminated` while the guest remained in
the terminal loop. This is bounded pre-`ExitBootServices` evidence only.

M02-010 host tests independently classify all 256 vectors, cross-check every
catalogued CPU exception including `#SX` vector 30, audit every opcode,
immediate, displacement, and padding byte in all 4,096 bytes, and decode the
first, middle, and final jump target. They also prove immediate-32 positive
vectors 128 and 255, exact forward/backward `rel32` boundaries, a poison tail,
every-byte mutation rejection with the exact first mismatch, pointer/`Span`
equality, all storage/address failure classes, default/POD behavior, zero
warmed-path allocation, and focused `NOSTDLIB_EFI` compile closure with an
empty `UIntPtr` core shim. The aggregate emits exactly:

```text
TEST_START m02.010
CLROS_M02_010_HOST_READY vectors=256 hardwareErrorCodeVectors=9 syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 normalizedSlots=3 readBack=true guestMaterialization=false stubExecution=false idtInstalled=false faultTriggered=false
TEST_PASS m02.010
```

The M02-010 host marker is not guest evidence. This leaf does not link the page
into EFI, allocate or publish executable guest memory, install an IDT, execute a
slot or common handler, or trigger a CPU fault.

The separately owned EFI integration emitted this distinct exact guest
transcript once under QEMU/OVMF:

```text
TEST_START m02.010
CLROS_M02_010_READY vectors=256 hardwareErrorCodeVectors=9 syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 normalizedSlots=3 commonTargetDelta=4096 fnv1a64=428D0353A96B7ACE readBack=true guestMaterialization=true loaderData=true pageReleased=true stubExecution=false idtInstalled=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
TEST_PASS m02.010
```

The profile required 17 records, ordered five, required the three transcript
records to be unique, and forbade `OS_SMOKE_FAIL`, every `TEST_FAIL`, and
`M02_NORMALIZED_INTERRUPT_FRAME_PAGE_FAILED`. The accepted 8,358-byte serial
log has SHA-256
`4E03A7CFCF8BD78018E3ABF35897EE3371B6E299FED70F32E56D4CEDE6EEB678`;
the summary reported no missing, out-of-order, duplicated, forbidden, or failed
record. `done/step920.md` preserves the exact source, artifact, and platform
tuple. This remains bounded pre-`ExitBootServices` materialization/readback/
cleanup evidence, not generated-code or fault evidence.

M02-011 host tests independently reconstruct all 256 x64 gate descriptors and
all 4,096 bytes at both a low address and a canonical high-half address,
cross-check the fixed selector and IST policy against the existing GDT/IST
contracts, and prove that every unique 64-bit handler remains inside the one
owned M02-010 page. They mutate every byte, preserve a poison suffix,
reject actual/model address and overlap failures before mutation, compare
pointer and `Span` output, prove zero managed allocations, and compile the exact
pointer source with `NOSTDLIB_EFI` and no BCL references. The frozen host vector
at stub base `0x00200000` is FNV-1a64 `7F505C7D14101415` and SHA-256
`0AD787A760168953C74020131BCAC6CC6FD09B9D68413D487678382846F95C38`.

```text
TEST_START m02.011
CLROS_M02_011_HOST_READY vectors=256 gates=256 gateBytes=16 tableBytes=4096 selector=8 present=256 dpl0=256 interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 nmiIst=3 doubleFaultIst=1 pageFaultIst=2 normalizedFnv1a64=8514F42EBDFC5D55 readBack=true guestPopulation=false idtrLoaded=false stubExecution=false faultTriggered=false postExitBootServices=false
TEST_PASS m02.011
```

The M02-011 host marker remains distinct from guest evidence. The separately
owned default-off EFI integration emitted this exact transcript once under
QEMU/OVMF:

```text
TEST_START m02.011
CLROS_M02_011_READY vectors=256 gates=256 gateBytes=16 tableBytes=4096 selector=8 present=256 dpl0=256 interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 nmiIst=3 doubleFaultIst=1 pageFaultIst=2 normalizedFnv1a64=8514F42EBDFC5D55 readBack=true guestPopulation=true loaderData=true allocationPages=3 allocationReleased=true stubExecution=false idtrLoaded=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
TEST_PASS m02.011
```

The runner required 17 records, ordered the five-record subsequence from
`EFI_CONSOLE_READY` through `UEFI_MEMORY_MAP_READY`, required the three M02-011
records to be unique, and forbade `OS_SMOKE_FAIL`, every `TEST_FAIL`, and
`M02_OWNED_IDT_GATE_POPULATION_FAILED`. The successful record is released only
after both production readbacks and one successful three-page `FreePages` call.
The host fake separately rejects missing services, allocation failure, invalid
or canonical-hole-crossing addresses, mutation in either produced page, and
release failure while proving exactly-once cleanup for every EFI-successful
allocation.

SharpOS remains a reference, not a source or runtime architecture for CLROS.
M02-011 adopts only the general idea of deterministic C# descriptor and stub
byte construction. It copies no SharpOS source, copies none of SharpOS's
firmware vectors 32-255, rejects the reference selector `0x38` in favor of the
owned CLROS selector `0x0008`, and executes neither `sidt` nor `lidt` at this
stage.

## Next Steps

1. Preserve the accepted default-off M02-010 and M02-011 LoaderData
   materialization, readback, cleanup, and guest-marker boundaries. Implement
   executable publication, a common interrupt entry/register-save path, IDTR
   load, active IST routing, and a deliberately triggered fault only in later
   issues that explicitly own and test those boundaries.
2. Map M02-008's planned guard/stack ranges and write the fixed IST indices into
   owned TSS state only in later issues that own page-table and descriptor-table
   installation; the accepted EFI profile remains temporary pre-firmware proof.
3. Preserve the M02-005 bounded owned-COM1 marker proof, then wire the writer
   through `SerialDiagnosticsGuard` only when a later issue owns the live
   post-firmware diagnostic boundary.
4. Add live IDT/GDT/TSS/IST setup only in later issues that own page-table and
   descriptor installation; M02-007 does not execute `lgdt` or `ltr`, and
   M02-008 does not map its planned guard/stack ranges.
5. Add real executable-stub page transitions using the W^X layout, materialization, and page-transition plan contracts.
6. Keep M02-004's configuration-only marker, M02-005's pre-firmware direct
   COM1 payload, M02-006's panic-safe route, and future post-firmware diagnostics
   as distinct evidence.
7. Wire the COM1 backend contract into an owned serial writer only after generated stubs can be safely mapped executable and invoked.
8. Add the controlled null-read and invalid-opcode probes behind `Probes.cs`
   toggles only in their dedicated issues; M02-014 must consume the disabled
   M01-015 intent after owned diagnostics exist.
9. Wire real handler register capture into `CpuFaultFrame` through the deterministic fault-entry layout.
10. Keep M02-003 binding readiness, M02-004 configuration, M02-005 bounded
   payload output, M02-006 panic-safe routing, and future post-EBS claims
   distinct.
