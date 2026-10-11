# Diagnostics

The development `EnableM06PackageReadProbe` defaults false and requires FAT.
It owns `M06_PKG` rows and `m06.015`..`m06.018` markers. Fresh sector receipts use
epochs134..197 only after reset, bus-master disable, DMA release and allocator
restoration. Controlled malformed-input success requires a known typed failure,
completed cleanup and valid canaries; it does not pass the interrupted positive
manifest/package stage. The exact chosen image variant determines the permitted
failure kind/code. [Step1154](../done/step1154.md) records focused host controls and
active no-stdlib compilation. [Step1155](../done/step1155.md) records the completed
cleanup audit correction, strict native review, six package guest variants and
seven ancestor regressions; renewed full-host qualification remains pending.

The qualified `EnableM06FatReadProbe` defaults false and requires the GPT profile.
It owns `M06_FAT` records and `m06.012`..`m06.014` markers. The reviewed inventory
contains40 host catalog entries. [Step1152](../done/step1152.md) records full-host,
native and 241-record guest qualification with sector/image association.

`EnableM06GptPartitionProbe` defaults false and requires the live block contract.
Its intended 207/208-record intact/primary-CRC-fault captures append issues009-011
and `M06_GPT` rows: `80` onward bind individual sector CRCs and fresh epochs;
`00`-`06` bind header/array validation, table choice, partition extent and cleanup.
Every read receipt is emitted only after reset and original allocator restoration.
Exact order and image association are checked. [Step1150](../done/step1150.md)
accepts both guest captures, native review and the full-host gate. Failed
development attempts remain in steps1148-1149.

`EnableM06LiveBlockContractProbe` defaults false and requires the read profile.
The accepted 158-record capture appends issue007/008 markers and `M06_BDEV` rows
0-D: transport, cleanup, original Info, protective MBR, Span/default pinning and
local rejection controls. Strict transcript, actual LBA0 image checks, full-host
and native qualification pass in [step1147](../done/step1147.md). All four fresh
contract/read/queue/ordinary captures retain the reviewed EFI bytes.

`EnableM06VirtioBlockReadProbe` defaults false and requires the block queue
diagnostic. Its accepted 138-record profile appends `M06_READ` rows 0-9 and
issue005/006 start/ready/pass markers. It records one read, capacity, used-ring
completion, publication fences, CRC/data association and reset/allocator cleanup.
[Step1143](../done/step1143.md) records full-host, native and guest qualification.
Strict transcript and actual image checks both gate wrapper success.

`EnableM06VirtioBlockQueueSetupProbe` defaults false. Its accepted 122-record
stream binds block identity, quiescence, feature policy, queue address readbacks
and original allocator restoration. Exact markers and scope are in
[storage](cstorage.md#bounded-modern-block-queue-profile); [step1140](../done/step1140.md)
records full-host, native and final guest qualification.

`EnableM05ApicSetupProbe` and `EnableM05VirtqueueInterruptProbe` default false.
Their exact 104/157-record streams bind controller masking, one interrupt/EOI,
IF and IDTR restoration, and queue cleanup. Full-host, strict native and real
guest qualification is accepted in [step1137](../done/step1137.md). The validator
also rejects contradictory queue/IRQ acquisition counts. Prerequisites and
limits are in [drivers](drivers.md).

The bounded split-queue batch #109-#112/#617 is accepted in
[step1133](../done/step1133.md): 4,251 full-host checks, strict native
review, a 139-record real queue diagnostic and a 79-record ordinary
regression. One ordered TX publication completes, then observed reset and
bus-master disable precede DMA release and exact allocator restoration.
No packet delivery, receive, interrupts, general networking or Gate 6
completion is claimed. See [drivers](drivers.md) for the lifetime boundary.

The bounded Virtio negotiation batch #105-#108/#614 is accepted in
[step1131](../done/step1131.md): 4,230 full-host checks, strict native
review, a 119-record real diagnostic capture and a 79-record ordinary
regression. The default-off profile stops at FEATURES_OK and resets the
device with bus mastering disabled. No queues, device DMA transfer,
interrupts, packet/block I/O or Gate 6 completion is claimed.

The bounded BAR/DMA batch #101-#104/#612 is accepted in
[step1129](../done/step1129.md): 4,208 full-host checks, strict native review,
a 122-record real diagnostic capture, and a 79-record ordinary regression.
The default-off profile proves quiescent BAR sizing/restoration and owned-page
address/buffer lifetime. No device DMA transfer or Gate 6 completion is claimed.

## Current Status

`EnableM05PciEnumerationProbe` is default off; its permitted dependent device
profiles are listed in [drivers](drivers.md). Unrelated M05 diagnostics, owned
panic and revocation remain exclusive. Its 97-record stream appends nine
`M05_PCI` rows plus start/ready/pass groups for `m05.004` through `m05.007`.
Rows require bounded ECAM authority, a present header, repeatable complete window
scan, explicit capacity rejection and malformed capability controls. Full-host,
native and real guest acceptance is recorded in [step1127](../done/step1127.md).

`EnableM05MonotonicClockProbe` defaults false and is exclusive with the other
M05 diagnostics. Its 83-record stream adds four `M05_CLOCK` records and the
`m05.003`/`CLROS_M05_003_READY` markers only after owned subsystem success and
bounded HPET motion checks. Full-host, native and guest qualification, including
the ordinary regression, are accepted in [step1125](../done/step1125.md).

`EnableM05OwnedAcpiSelectionProbe` defaults false. Its 83-record diagnostic
stream appends three selections and one rejected-length control to owned
survival, followed by `CLROS_M05_002_READY` and `TEST_PASS m05.002`.
The parser requires exact correspondence with the original retained descriptors
and rejects malformed, unselected or trailing records. The real guest capture and
ordinary regression are accepted in [step1123](../done/step1123.md).

`EnableM04RethrowProbe` and `EnableM05PhysicalMappingProbe` are false by default.
The rethrow profile extends the GC stream to 88 application records and proves
same-object catch/rethrow/outer continuation. Mapping extends owned survival to
86 records and requires positive cache-policy, leaf, readback and neighbor checks
before `TEST_PASS m05.001`. The C# parsers reject unselected claims and malformed
or reordered records. [Step1120](../done/step1120.md) records full-host and
guest qualification with independent image association.

Ordinary verified firmware exit is accepted under #68/#594 in [step1094](../done/step1094.md). The frozen source `3c986e83447e2955efb25566ecd611676f054b30` passes the original full host gate (3,903 paired groups plus the suite summary), three fresh ordinary 79-record QEMU/OVMF captures and five explicit diagnostic regressions. Native, raw transcript, source and post-capture GPT/FAT/EFI associations pass.

Normal exit is a separate boot policy, not an enabled diagnostic probe. All probe properties remain false. Its 79-record stream ends with the three M03-011 completion records followed by ordinary success, all after the positive owned checks. Explicit diagnostic profiles preserve their earlier record contracts and evidence limits.

The accepted finite `10.0.12` compiler qualification is recorded in
[EFI build](efi-build.md#current-toolchain-migration). Historical diagnostic
captures below remain source-bound. Dangerous probes retain their false
defaults, and no new guest result follows from the version change alone.

The accepted probe catalogue has 24 entries at `ead4fca`: M03-008 and
M04-024 occupy indices 17 and 18, unwind metadata is index 20, owned survival
and panic are indices 21/22, and simple catch is index 23. The earlier accepted
`4ef299f` source had 23 entries ([step1069](../done/step1069.md)); that historical
count remains source-specific. The destructive, default-off M04-016 dispatch
entry is index 19 in
`runtime.dispatch`. Integration #572 is accepted at `d5bef46` in
[step1063](../done/step1063.md). M03-009 adds five owned-writer records to the
first-exit/revocation profiles, now accepted at 66/78 application records.
Dispatch passes its full 158-record stream and null-receiver terminal control;
churn's current-image canary/static-payload controls also pass. All destructive
profiles remain default-off. Development and preserved failures remain in
steps1054/1056; the first failed timed-stop control remains distinct from the
three passing fresh controls.

The destructive, default-off M04-020 entry is index 20 in
`runtime.unwind-metadata`. Its new child emits a 22-record tail: start, 19
canonical decimal `CLROS_M04_020_DATA` rows, ready, pass. Those rows describe the
original owner, measured metadata snapshot, actual sampled method PCs and
function/unwind ranges, plus stack/storage preservation. Four successful range
lookups and two expected misses remain distinct from unwinding execution.
The complete 67-record guest profile, unchanged 45-record TypeManager profile
and 21-record default profile pass at `4625fd7` in
[step1071](../done/step1071.md), with original full host and independent native,
stack, raw transcript, disk and lifecycle acceptance. Step1057 retains the
earlier development evidence.

Owned survival and panic occupy indices 21 and 22, accepted in
[step1074](../done/step1074.md). The default-off simple-catch child is index 23;
[step1075](../done/step1075.md) accepts its 82-record collecting-GC profile at
`ead4fca`, including the real catch/continuation and local-input admission
rejection. These finite profiles do not establish general exception handling.

The default-off single-finally sibling occupies index 24, for 25 catalogue
entries. [Step1088](../done/step1088.md) accepts its complete 84-record ordinary
guest capture after enabled-image review: one compiler cleanup, typed catch and
compiled continuation, with local-input admission rejection. General EH remains
incomplete.

The preceding accepted revocation profile extended
the first-exit transcript to 73 application records: an admitted pre-entry
console operation, then eight post-exit `M03_ADAPTER_DENIED` rows with unchanged
outputs and no additional backend calls, and `CLROS_M03_008_READY`.
The constructor profile extends GC-static diagnostics to 82 records, including
`M04_CCTOR` stages 0 through 8 and `CLROS_M04_024_READY`. The runner binds its
runtime seed to the original admitted module handle XOR heap backing, validates
eight once-only constructor invocations and compiler-emitted helper reentry
counts (five at depth one, three at depth two), and rejects failure stage 255
or unrequested child output. Both final record contracts and the original full
host gate are accepted at `2b5a4b0`. See [step1053](../done/step1053.md) for exact
source/native/raw/lifecycle/disk association and
the [profile definitions](efi-build.md#retained-firmware-and-constructor-profiles).

M03-007 has accepted default-off terminal continuation evidence at `e64e9dc`
using owned COM1, descriptor tables, main/IST stacks and CR3 after the real
firmware exit. Its probe remains catalogued as destructive in `Probes.cs`; see
the [profile command](efi-build.md#first-real-exit-profile-m03-007) and
[step1051](../done/step1051.md). The final review associates the retained bounded
native/negative scenarios with the actual accepted image; debugger-injected
faults are not spontaneous or general interrupt-service evidence. This does not
change the historical M02 boundary or broader Gate3 **Fail** status, and does not
prove persistent fault recovery, arbitrary nested faults or general firmware
adapter revocation.

The bounded M02 technical exit is now supported by the separately accepted
pre-`ExitBootServices` local-QEMU experiments for owned COM1, private descriptor
state and controlled UD2/PF/DF/vector-32 delivery. All 20 keyed children have
reviewed closure evidence; [step960](../done/step960.md) records the epic audit
and [step959](../done/step959.md) the current combined host/guest acceptance.
Each experiment retains its exact admission, restoration or terminal-halt
contract. This does not establish ordinary persistent fault handling,
asynchronous IRQ/NMI service, nested recovery or post-firmware diagnostics.

The owned diagnostics subsystem remains host-testable except for the accepted
bounded pre-`ExitBootServices` M02-004 configuration, M02-005 marker, M02-006
panic-readiness, M02-009 terminal GDT/TSS, M02-010 interrupt-entry-page
materialization, M02-011 owned-IDT population/readback, M02-012 reversible IDTR
transition, and M02-013 terminal invalid-opcode profile. The accepted M02-011
guest profile did not load IDTR. The M02-012 adapter has host,
default/enabled NativeAOT artifact, same-build verifier, dry-run, and accepted
real QEMU/OVMF evidence for the bounded reversible transition and
ordinary-smoke continuation. M02-007 adds a
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
The default-off EFI adapter now source-links the frozen leaf, materializes the
dependent M02-010/M02-011 bytes, publishes and synchronously invokes the exact
  transition on the guest path, audits restoration, releases its owned pages,
  and then resumes ordinary memory-map capture. The accepted 2026-08-26
  QEMU/OVMF serial tuple proves those exact bounded observations on the recorded
  virtual platform; artifact and host evidence alone still do not substitute
  for that tuple.
The M02-013 host leaf adds a disabled-by-default, allocation-free value contract
for a two-byte `ud2` observation. Its 32-byte request is bound to
`Probes.EnableInvalidOpcodePanicProbe`, requires caller attestations for owned
serial, GDT/TSS, IDT, and the invalid-opcode handler, and rejects enabled,
requested, or attempted execution. Its 72-byte result can audit only synthetic
`CpuFaultFrame` values, with deterministic first-mismatch reporting across all
23 fields. The leaf emits or invokes no bytes and supplies no guest fault
evidence. Its separate default-off EFI integration now has accepted real
QEMU/OVMF evidence for a terminal pre-firmware vector-6 delivery through fresh
owned GDT/TSS/IDT state, exact integer/control-register capture, and exact
allocation-free owned-COM1 output. This profile is not a reusable boot phase and
does not safely return.

M02-014 now adds the disabled host null-read contract
`SeparateDisabledDefaultPageFaultCR2ErrorCode`. It consumes M01-015 supplied-image
absence evidence and audits five synthetic page-fault fields. It executes no
fault and adds no live diagnostics capability. The separate #517 default-off
EFI adapter now has accepted source-bound QEMU evidence for one terminal
supervisor null read, vector 14, CR2/error zero and IST2 capture. See
[step944](../done/step944.md); this does not enable ordinary fault handling.

M02-015 now has a non-executing context, armed-IDT audit and terminal byte
emitter in `DoubleFaultISTPathSilentRebootOrTriple`. Its four caller buffers
remain fixed at 8,192 stream bytes and 4,096 bytes each for context, normalized
slots and IDT. Complete extent, wrap and disjointness checks precede writes.
The leaf derives C3/D10/T43 and preserves the four guard holes. It verifies the
canonical all-present IDT, clears only vector 14 Present at byte 229, then
independently audits all 4,096 final bytes: 255 gates remain present and vector
8 retains selector 8/IST1. The original all-present validator stays unchanged.

Host tests independently decode the null-read escalation path, every normalized
slot, early CR2/GPR saves and the current IST1 RSP. The complete 192-byte capture
is 48 hardware bytes plus 16 normalization bytes plus 128 software bytes.
Undefined saved DF program state and CR2 remain diagnostic-only; current
root/selectors, original IST1 and fresh descriptor/gate observations determine
the checks. The measured synthetic output contains 14 CRLF records, 2,643 ASCII
bytes and 84 numeric fields. Finite COM1 polling and terminal halt bytes are
never executed by the host tests. The actual 20-source no-stdlib compile has
zero AssemblyRefs. [Step945](../done/step945.md) records the focused evidence and
independent review. The complete settled-source host run in
[step949](../done/step949.md) passes 3,573 starts/3,574 passes with zero failures;
it includes all nine double-fault host groups and the actual no-stdlib closure.

The supplied page-image summary does not establish C/D identity, current table
contents or lifetime. The separately scoped #521 EFI adapter now allocates the
exact C3/D10/T43 arenas, verifies complete extents and disjoint scratch buffers,
and freshly audits the six mapped ranges against the original table backing
immediately before final byte readback and instruction publication. It retains
the unchanged host leaf and all-present IDT validator. The adapter enters at
stream offset 768 with the owned context in RCX; after attempted entry it has
no firmware cleanup or return path. Its unique probe authority defaults false.

[step950](../done/step950.md) records implementation and artifact evidence.
The fresh settled-source full host suite passes in [step951](../done/step951.md),
with 3,580 starts, 3,581 passes and no failures. Separate fresh default, page-fault
regression and double-fault QEMU captures have accepted source/artifact, native,
whole raw transcript, lifecycle and disk-accounting reviews. The bounded DF
experiment captured vector 8/error 0 and the full 192-byte current-handler span
on original IST1. Its same-image audit/readback/publication path and current
descriptor/root/gate observations bind that result to the original C/D/T backing.
Saved DF state and CR2 remain diagnostic-only. There is no independent live
memory dump or general mapping/lifetime or recovery proof. Host/synthetic markers
alone remain insufficient. PR #522 merge and #55/#521 closure are separate
tracker actions; the evidence record does not assert those actions already ran.

M02-016 now includes the reviewed C2/D7 unexpected-vector leaf and the
default-off `M02UnexpectedVectorPanicAdapter` source checkpoint in
[step956](../done/step956.md). Its six leaf operations prepare context, emit and
read back the stream, decode a capture, validate supplied state, and report
through the checked COM1 backend. Host tests inspect ordinary bytes; they do
not execute privileged instructions.

The fixed layout retains two code pages and expands data to seven pages.
D5+D6 form an 8 KiB transition/callback stack, with a 16-byte bottom sentinel.
The 1,028-byte stream, 184-byte capture and 232-byte callback entry depth retain
their previous roles; capture starts at D+28488 and initial callback RSP at
D+28440. The earlier 4 KiB layout demonstrably overflowed in the isolated
NativeAOT study recorded in step953. Neither the new capacity nor the entry
depth proves the complete current compiled reporting chain fits. The separate
same-image proof accepted in [step959](../done/step959.md) bounds all113 rooted
methods, ten jump tables/218 entries, accesses and ABI/SIMD behavior at5,208
bytes including the bridge, below the8,176-byte budget. It retains the trusted
firmware/NativeAOT SSE entry prerequisite and does not observe a post-report
sentinel or establish general memory lifetime safety.

The adapter retains original image, callback, request, checked backend and
allocation authority outside mutable C/D scratch. Exact original callback
arguments are checked before capture reads. Full readback precedes sentinel
installation, final publication and one terminal INT0x20/vector32/IST0 entry.
The finite cleanup contract excludes the original image, I/O and temporary
scratch ranges; ambiguous allocations are retained rather than freed.
After entry there is no firmware return, reset, free or recovery. Firmware
mappings and CR3 remain in place; the sentinel is not a guard page and its
pre-report check cannot observe later reporting calls.

Output remains the 650-byte/24-field metadata record followed by the existing
unexpected-vector panic record. Two checked TEMT handoffs each permit at most
65,536 reads; per-byte THRE remains limited to128. Controlled tests cover
queued/final bytes, last-poll success, exhaustion and truthful counters.
The byte-return IN/void-return OUT binding cannot inject a returned checked
failure status; those defensive branches remain unexercised through that API.

Focused evidence separately covers the complete11-pair D7 leaf (including
12-source no-stdlib/zero AssemblyRefs), seven new integration groups,
10 publication groups, the repaired13-pair loaded-image module and all165
runner groups. The first three completed within a later-failed combined run;
the loaded-image retry passed separately. Default/enabled NativeAOT artifacts
have zero OS AssemblyRefs. These source, controlled-process and artifact checks
alone do not constitute a passed full suite or real panic. Step959 separately
accepts the complete3,636/3,637/0 host run and nine fresh guest profiles at
`dee6df2`. Its panic lane has76 exact records, full current-state/native
associations, wrapper0/source-bound runner19, QEMU signed-1/owned termination,
measured760ms quiet and empty stderr. The original port targets and exact
650-byte metadata are preserved. Review/parser failures remain recorded.
Gates3/4 remain **Fail**. [QEMU](qemu.md) documents the distinct expected-panic
classification; merge and tracker closure remain separate actions.

No ordinary boot phase loads an IDT, installs fault handlers, activates IST
routing, directs a live CPU fault to panic, or preserves an owned serial path
across real
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

  The default-off `EnableM02IdtrContinuedExecutionProbe` EFI adapter now links
  this exact leaf after M02-010 and M02-011. It makes exactly two successful
  allocation calls: two contiguous `EfiLoaderCode` pages and one disjoint
  `EfiLoaderData` page. Code page zero owns the normalized slots; code page one
  owns `48 83 C4 18 48 CF` at offset zero and the 52-byte transition at offset
  `0x40`; the data page owns the complete IDT; and the 80-byte context remains
  in 16-byte-aligned caller stack storage. Preparation/readback completes
  before `X64InstructionPublication.Serialize()` and invocation through only
  `delegate* unmanaged<IDTRContinuedExecutionContext*, ulong>`.
- Immediately after return and before any C# or firmware callback, the adapter
  independently requires loaded IDTR `{ limit=4095, base=ownedIdt }`, raw
  byte equality between previous/restored IDTR, a previous IDTR distinct from
  the owned IDT, and entry/return IF equality. Failure takes a callback-free
  spin. This rejects an untouched all-zero context and prevents freeing a page
  that may still be active. Once those safety facts hold, wrong-cookie or other
  full-audit failure can safely release data then code and emit the exact
  failure transcript. Pre-invocation cleanup also releases in reverse order
  and suppresses a code free for any hostile equal or partial allocation-range
  overlap.
- Ready/pass is emitted only after the production audit succeeds and both
  reverse-order frees return success. The exact guest transcript is:

  ```text
  TEST_START m02.012
  CLROS_M02_012_READY tableBytes=4096 idtrBytes=10 idtrLimit=4095 policyFnv=8514F42EBDFC5D55 firmwareGatesCopied=0 firmwareIdtrCaptured=true ownedIdtrLoaded=true ownedIdtrReadBack=true firmwareIdtrRestored=true restoreReadBack=true ownedIdtrActive=false continuedExecution=true cliWindow=true ifRestored=true instructionPublished=true allocationsReleased=true wxEnforced=false interruptTriggered=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
  TEST_PASS m02.012
  ```

  Default and enabled no-stdlib NativeAOT publishes passed the same-build
  verifier with 62 explicit compile items, compile-key SHA-256
  `9B243B135C05442631A0323654FDBE07C827F043703330D8BAA9E83A78A21757`,
  and 75 source-authority inputs, closure SHA-256
  `F1543BCDE0DF38127068CF692188A8510DF9E41A61E49FA5CC5A90C785C37A26`.
  Focused host tests prove the default artifact excludes the adapter, the
  enabled artifact includes it, hostile fake-firmware cleanup/fail-closed
  behavior, exact marker composition, and transcript rejection. The accepted
  2026-08-26 QEMU/OVMF run observed all 17 ordered records, all six unique
  records exactly once, none of the five forbidden records, and ordinary
  continuation through `OS_SMOKE_PASS`. Its 8,462-byte serial log has SHA-256
  `4E97D1D61EAF5FCB5371F05889734D8A8103C1C3CD0126B5E0A6A5519A8947C6`.
  This is processor evidence only for the recorded temporary transition; it
  does not establish interrupt/fault delivery, executable common dispatch,
  active GDT/TSS/IST routing, real `ExitBootServices`, or Gate 3.
- `DisabledDefaultUd2VectorState` is the M02-013 value-only host contract. Its
  exact version-1 request/result layouts are 32 and 72 bytes with no managed
  references. The request models exactly the two bytes beginning at a nonzero
  canonical x64 address and rejects address wrap or a range that enters the
  canonical hole. It aliases vector 6 from `FaultVectorCatalog.InvalidOpcode`
  and its default state from `Probes.EnableInvalidOpcodePanicProbe` rather than
  duplicating either authority.
- `Plan` returns ready metadata only when the destructive probe remains off,
  neither execution-request nor execution-attempt state is present, and all
  four prerequisite attestations are supplied. `AuditSyntheticObservation`
  additionally fixes the expected state to vector 6, no hardware error code,
  zero error code, and RIP equal to the modeled first `ud2` byte. It compares
  vector, error-code metadata, error code, RIP/RSP/RBP/RFLAGS, CR2/CR3, and all
  general-purpose registers in a stable 23-field order, reporting the first
  mismatch field and expected/observed values without allocation.
- Focused host tests prove enum ordinals, unmanaged layouts and field offsets,
  default/result integrity, hostile header/flag/reserved bytes, safety-state
  rejection order, every missing prerequisite, low/high canonical boundaries,
  overflow and canonical-hole crossing, all expected-state rules, every frame
  mismatch, first-mismatch precedence, zero managed allocation, and exact-source
  no-stdlib compilation. A source firewall proves that neither the legacy
  `ud2; ret` byte plan nor any generated-byte invocation, I/O, descriptor load,
  EFI entry, or boot-sequence path is referenced. The host-only transcript is:

  ```text
  TEST_START m02.013
  CLROS_M02_013_HOST_READY requestBytes=32 resultBytes=72 ud2Bytes=2 vector=6 errorCodePresent=false errorCode=0 auditFields=23 probeEnabled=false executionRequested=false executionAttempted=false syntheticObservation=true guestExecution=false faultTriggered=false idtrLoaded=false postExitBootServices=false
  TEST_PASS m02.013
  ```

  This transcript is host evidence only. It does not demonstrate a processor
  executing `ud2`, vector-6 delivery, a loaded owned IDTR, register capture from
  hardware, continuation after a fault, QEMU behavior, or any numbered gate.
- `DisabledDefaultUd2VectorStateTransition` is the separate M02-013 privileged
  byte-stream contract. Its 4,096-byte stream contains the M02-010 vector slots,
  a common vector-6 handler, owned-COM1 writer, GDT/TSS/IDT activation
  transition, deliberate `ud2`, and a terminal `cli`/`hlt` loop. The transition
  seeds and verifies live pre-fault `RFLAGS=0x46`; the exact expected saved
  fault-frame value is `0x10046` because fault-class exception delivery sets RF
  in the saved image. The frozen stream has FNV-1a64
  `E5070E16F23533A3` and SHA-256
  `DB402C072CBD5E9C82F36D1802AAB526C347077BDD147031D83E83B563E195B7`.
- The default-off `EnableM02Ud2VectorStateProbe` EFI adapter composes that
  stream with fresh owned GDT/TSS/IDT/context/IST storage after ordinary
  `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`. Its accepted
  2026-08-26 QEMU/OVMF transcript reports vector 6, no hardware error code,
  exact UD2 RIP, all 23 integer/control-register fields captured and matched,
  owned GDTR/TR/IDTR and COM1, and terminal halt. The generated path writes:

  ```text
  TEST_START m02.013
  CLROS_M02_013_READY vector=6 errorCodePresent=false errorCode=0 ud2RipMatched=true registerFields=23 registerStateCaptured=true registerStateMatched=true ownedGdtLoaded=true ownedTssLoaded=true ownedIdtrLoaded=true ownedCom1=true faultTriggered=true terminalHalt=true markerTransport=ownedCom1 postExitBootServices=false
  TEST_PASS m02.013
  ```

  The 3,837-byte accepted serial log has SHA-256
  `39DF159E4990CEE91574B6240ED77375B10DAC8B43338BA232FC965DF11D2E3E`.
  The evidence is terminal, pre-`ExitBootServices`, and specific to the
  recorded QEMU/OVMF tuple. It does not establish recovery, continuation,
  persistent descriptor ownership, W^X/NX, post-firmware diagnostics, or a
  numbered-gate promotion.
- `SeparateDisabledDefaultPageFaultCR2ErrorCode` is the M02-014 host-only
  null-read contract. Its unmanaged request consumes a successful M01-015
  `NullPageFaultProbePlanResult`; default results, mapping intent without
  supplied-image absence evidence, and corrupted access/privilege/error/CR2
  expectations fail closed. The destructive default aliases
  `Probes.EnableNullPointerPanicProbe`. Enabled, requested, or attempted
  execution is rejected. Owned serial, GDT/TSS, IDT, and page-fault-handler
  readiness are explicit caller attestations, not active processor evidence.
- Its `Plan` checks a canonical four-level x64 instruction address outside
  page zero. This address is metadata: it proves no executable byte span,
  installed page table, ownership lifetime, or alignment requirement.
  `AuditSyntheticObservation` compares vector 14, hardware-error-code presence,
  the entire 64-bit error value equal to zero, CR2 equal to zero, and RIP equal
  to that address, in that order. The result reports the first mismatch and
  compared-field count. No low-bit mask hides extended/reserved error bits,
  and the old null-write error code 2 is rejected.
- The exact zero-error policy applies only to the planned ordinary supervisor
  data read of a nonpresent page. It is not a general page-fault rule. Intel's
  [SDM revision 090, Volume 3A, Interrupt 14, pp. 7-44–7-46](https://cdrdv2-public.intel.com/874240/325462-090-sdm-vol-1-2abcd-3abcd-4.pdf)
  defines error bits, CR2, and the saved instruction pointer. A real handler
  must preserve CR2 before another page fault overwrites it. This host leaf
  does not validate saved flags/RF, CR3, stacks, GPRs, frame layout, or hardware
  capture. A later adapter must account for RF being set in the saved image
  for an ordinary fault-class exception, as specified in Intel's
  [SDM Volume 3B, section 18.3.1.1, p. 18-9](https://cdrdv2-public.intel.com/825743/325462-sdm-vol-1-2abcd-3abcd-4.pdf).
- Focused tests cover all 128 execution/readiness combinations, M01-015
  authority and corruption rejection, canonical/page-zero boundaries, vector
  values -1 through 256, every error/CR2 bit, deterministic mismatch precedence,
  unmanaged values, and zero allocation across 30,000 warmed operations.
  Accepted comparisons always report `IsGuestEvidence=false`,
  `CapturedHardwareState=false`, and `FaultTriggered=false`. The source emits
  no bytes, accesses no pointer, installs no handler, and triggers no fault.
  Its distinct host transcript is:

  ```text
  TEST_START m02.014
  CLROS_M02_014_HOST_READY vector=14 errorCodePresent=true errorCode=0 cr2=0 access=read auditFields=5 probeEnabled=false syntheticObservation=true hardwareStateCaptured=false faultTriggered=false guestExecution=false postExitBootServices=false
  TEST_PASS m02.014
  ```

  The separate step944 adapter evidence covers its bounded active mapping,
  complete instruction spans, live frame capture and terminal guest behavior.
  General mapping/lifetime authority and recovery remain unsupported. This
  nonexecuting leaf does not change any completion gate.
- `SeparateDisabledDefaultPageFaultPageTables` is the separate #510
  nonactivating page-image prerequisite for a terminal diagnostics root. It
  builds and independently audits a caller-supplied four-level identity image:
  one to eight disjoint
  low-canonical ranges of one to sixteen 4 KiB pages, supervisor RX or RW+NX,
  write-back only. A distinct caller-owned table arena is mapped RW+NX.
  Backing pointers are separate from modeled physical addresses; the helper
  owns neither storage nor resource lifetimes.
- The arena requires at least `1 + 6 * (rangeCount + 1)` and at most 55 pages.
  Each resource interval and the arena are shorter than 2 MiB, so each touches
  at most two regions at each nonroot hierarchy level. One root plus six
  nonroot tables per interval bounds the complete image, including its own
  arena mapping. The conservative capacity bound is checked before mutation;
  unused pages remain zero.
- `Build` validates ranges, supplied mode preconditions, capacities, pointer
  spans and aliases before writing. Independent `Audit` checks metadata,
  unique owned hierarchy targets, exact identity addresses and permissions,
  complete coverage and zero unused entries. Cycles, foreign/duplicate table
  targets, large pages, surplus empty subtrees, extra mappings and malformed
  flags fail. The existing page-zero policy checks these same buffers.
  Audit requires pristine entries, including clear accessed/dirty bits; it
  is not an audit of a hierarchy already modified by hardware. Results are
  non-owning snapshots requiring a fresh audit after memory or lifetime
  changes, and do not authorize loading CR3. Guest, installed-root, active
  permission, TLB and fault indicators remain false. Guard ranges are not
  an input or enforcement claim.
- Focused tests independently walk raw entries, cross hierarchy boundaries,
  exercise maximum layouts and corrupt metadata/storage, preserve canaries
  and rejected preflight inputs, and measure zero allocation across 64 warmed
  build/audit iterations. The actual source and ten existing dependencies
  also compile with zero framework or external assembly references. Step930
  records focused evidence; step933 adds accepted complete host execution at
  source `5f6108ca363f03c7ebad8dafca7ae78e1d3a0db2`. The separate layout
  guest tuple does not execute this page-image helper. The later, separate
  step944 tuple supplies the bounded CR3 activation and vector-14 guest proof.
- `SeparateDisabledDefaultPageFaultTransition` is the separate #513
  nonexecuting host API for the terminal emitter. It prepares a 4096-byte
  context and materializes/readbacks
  an 8192-byte instruction/data stream for the fixed C3/D10/T43 image, six
  ranges, 52 mapped leaves and five absent addresses. Status-coded methods
  validate caller spans, mode/PAT and the available #510 snapshot before
  writing; no method executes the generated instructions.
- Independent byte decoding and synthetic interpretation cover the qualified
  CPL0 entry, descriptor/root readback, private-stack transition, early CR2
  and full 192-byte frame capture, bounded active-root absence walks and finite
  COM1 polling. The complete 12-line, 2053-byte transcript has 57 fields from
  modeled captured state. The stream has 361 instructions/1910 instruction
  bytes; its 3264-byte context bound includes reserved output capacity.
- Step936 records focused parent tests, all-byte corruption/preservation and
  allocation checks, independent LLVM decoding, and the actual 17-source
  no-stdlib closure with zero assembly references. Step939 adds the accepted
  complete host gate at source `4b18b4f`; the separate heap QEMU matrix does
  not execute this emitter. This is data and compiler evidence, not a real
  fault, CR3 switch, descriptor activation or UART write.
  Synthetic bad-CPL rejection does not prove ring-3 recovery: diagnostics and
  terminal halt still require privilege. The default-off adapter now binds
  exact current backing and freshly audits the six derived ranges; snapshots
  and fingerprints do not establish ownership, lifetime or tamper resistance.
  Step944 supplies the separate default/enabled guest acceptance; the earlier
  heap tuple and standalone host API do not acquire that execution evidence.

- The separate #517 EFI adapter now prepares that frozen transition under the
  default-off `EnableM02PageFaultCr2ErrorCodeProbe` option. It validates the complete
  five-buffer temporary set before writing query code, captures actual mode/PAT,
  initializes C3/D10/T43 resources and re-audits the actual table backing against
  the exact six derived mappings immediately before final readback/publication.
  The closed stream owns the sole terminal start and ready records. Cleanup is
  reverse, once only and restricted to pre-entry ownership; the terminal path has
  no firmware callback or return.

  The focused host run has eight starts, nine passes and no failures across the
  new EFI and unchanged transition modules, including actual default/enabled
  NativeAOT artifact checks. Step941 records that implementation/compiler
  checkpoint. Step944 adds the accepted complete host gate (3,558 starts,
  3,559 passes, no failures), fresh default and terminal page-fault QEMU captures,
  and separate pristine/booted image accounting at source `7c71e72`. The actual
  enabled transcript has 12 terminal records and 57 numeric fields, with no
  firmware-return suffix. Gates 3/4 remain Fail; merge and issue closure are
  separate tracker transactions recorded after this evidence checkpoint.

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
- `dotnet run --project tools/OS.Tooling -- efi build -BootPolicy Diagnostic -EnableUefiGoneSimulationProbe` builds a firmware-visible QEMU simulation variant that emits `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `dotnet run --project tools/OS.Tooling -- efi build -BootPolicy Diagnostic -EnableAcpiPhysicalDiscoveryProbe` builds a firmware-visible ACPI physical discovery probe variant. The probe is disabled by default, runs before `ExitBootServices`, and emits ACPI table-discovery markers only after live RSDP/root SDT/MCFG byte validation succeeds.

## Unsupported Behavior

Default, reusable, recoverable IDT/GDT/TSS/IST setup; page-fault handling;
post-`ExitBootServices` register capture and owned serial output; panic-safe
framebuffer output; panic redaction; and general halt/shutdown policy remain
pending. M02-013 is narrower: one default-off pre-firmware terminal profile has
accepted QEMU evidence for a deliberate invalid-opcode delivery through fresh
owned descriptor state, integer/control-state capture, direct owned-COM1 output,
and a nonreturning halt. M02-014 additionally has one explicit pre-firmware
terminal null-read profile with vector-14/CR2/error and owned-root/IST2 capture
in step944. Neither profile is the default interrupt or panic subsystem, and
bounded runner termination does not measure indefinite halt duration.

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

The current managed formatter and guard contracts allocate managed strings and
are only safe for host tests or normal boot diagnostics. `BootEventRingBuffer`
is still the managed string mirror used by normal host-tested boot logging.
`BootEventRecordRingBuffer` and `BootEventRecordFormatter` are caller-storage
based, but they are not wired into `BootSequence`, a real panic dump, or the
live EFI path yet. `PanicTextBuffer` and `PanicSerialReporter` are likewise not
wired into a real panic path.

`CpuFaultFrame` remains only the managed data contract and formatter input; the
M02-013 terminal profile uses its own frozen no-stdlib context layout to capture
and compare the corresponding 23 integer/control fields. That bounded evidence
does not wire a reusable CPU handler into `CpuFaultFrame`.

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

`DisabledDefaultUd2VectorState` itself does not strengthen that byte plan into
live execution. It accepts only caller-supplied synthetic frames while the
probe is disabled and while execution is neither requested nor attempted. Its
readiness booleans are attestations, not observations of installed GDT/TSS/IDT
state or owned serial. The host marker explicitly reports guest execution,
fault triggering, and IDTR loading as false. The separate default-off M02-013
EFI adapter now supplies bounded terminal evidence for live vector-6 delivery,
integer/control-register capture, and owned-COM1 reporting on one recorded
QEMU/OVMF platform. It deliberately does not return; safe continuation,
descriptor restoration, persistent descriptor ownership, W^X/NX, and
post-`ExitBootServices` diagnostics remain deferred.

`NullPageFaultProbePlan` is metadata only and is distinct from
`FaultProbeStubPlan.NullWrite`: it models a supervisor read, not a write, and
contains no executable stream. Its diagnostics-ready and handler-ready fields
are caller attestations, not loaded IDT/COM1 evidence. Its original standalone
host evidence did not include an EFI artifact or execution. The later #517
source integration includes the plan in the explicit EFI compile closure.
That inclusion does not let the metadata leaf trigger or observe `#PF`, read
CR2, capture an error code, or report a panic. Those operations belong to the
separate explicit M02-014 terminal profile; its implementation/artifact
checks and actual guest evidence remain distinct from the plan's host proof.

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

1. Preserve the accepted default-off M02-010 and M02-011 returning LoaderData
   materialization, readback, cleanup, and guest-marker boundaries and M02-013's
   separate terminal fault-delivery evidence. Later accepted common-entry
   terminal profiles remain distinct from future recovery/return policy and
   default descriptor ownership.
2. Preserve later accepted opt-in profiles' installed guard/IST/TSS observations
   separately from M02-008's allocation-only leaf. Continuing stack and
   descriptor lifecycles need their own ownership evidence.
3. Keep M02-005's bounded pre-firmware owned-COM1 marker proof separate from
   M03-009's accepted post-exit writer in [step1063](../done/step1063.md).
   Neither promotes the host `SerialDiagnosticsGuard` contract into a default
   continuing diagnostic service.
4. Add default IDT/GDT/TSS/IST lifecycle and safe recovery only in work that owns
   those continuing-execution boundaries. The accepted installed terminal
   profiles do not change M02-007's encoding-only or M02-008's allocation-only
   leaf contracts.
5. Add real executable-stub page transitions using the W^X layout, materialization, and page-transition plan contracts.
6. Keep M02-004's configuration-only marker, M02-005's pre-firmware direct
   COM1 payload, M02-006's panic-safe route, and accepted bounded post-firmware
   diagnostics as distinct evidence.
7. Preserve the original admitted COM1 callback and owned writer authority when
   extending the accepted diagnostic paths; additional routes require their own
   publication, lifetime and permission checks.
8. Preserve the M02-013 host request/audit contract and its accepted default-off
   terminal EFI/QEMU profile as distinct evidence. Keep the exact host and guest
   markers separate. Safe continuation and default descriptor restoration or
   lifecycle remain separate work; bounded post-firmware diagnostic acceptance
   does not establish those behaviors.
9. Keep the controlled null-read profile explicit through
   `EnableM02PageFaultCr2ErrorCodeProbe`. It consumes the disabled M01-015
   intent; `Probes.EnableNullPointerPanicProbe` remains false in the narrow
   `NullPointerProbeAuthority.cs` partial-class file. Keep the M02-014 adapter's
   actual guest evidence separate from host intent and source inclusion.
10. A future default or recoverable handler needs its own register-capture
    contract in `CpuFaultFrame`; M02-013's private terminal context does not
    establish that boundary.
11. Keep M02-003 binding readiness, M02-004 configuration, M02-005 bounded
   payload output, M02-006 panic-safe routing and accepted bounded post-EBS
   diagnostics distinct from general continuing-execution claims.

## Bounded throw/catch diagnostic

The accepted `ead4fca` integration places the default-off simple-catch probe at
catalogue index 23, after the two owned-subsystem profiles. Its complete
collecting-GC transcript requires 82 application records and 71
required/ordered/unique anchors. Nine numeric child stages bind original
module/stack/receipt authority, a local input rejected by the production
capture-admission predicate, the captured and selected caller contexts, typed
handler, actual catch entry and actual returned continuation. Stage 2 changes
only a local RIP scalar; it does not mutate the live capture or enter a handler.
`CLROS_M04_SIMPLE_CATCH_READY` and `TEST_PASS m04.021.simple-catch` still require
the real compiled catch and continuation. Failures remain
`TEST_FAIL m04.021.simple-catch <reason>`. The earlier 81-record development
guest is preserved in [step1066](../done/step1066.md). [Step1075](../done/step1075.md)
accepts the original common gate and current 82-record guest with final native,
raw, lifecycle and disk association. Local-input admission rejection does not
prove terminal rejection after live context corruption or general exception
recovery.

## Single-finally development transcript

The default-off #589 profile inserts fourteen lines after the existing 70-line
GC profile's `CLROS_M04_013_READY` and before `TEST_START boot.sequence`, for 84
complete application lines. Start is `TEST_START m04.021.single-finally`; the
eleven `M04_FINALLY` rows have exactly four uppercase, 16-digit hexadecimal
fields prefixed with `0x`, followed by pass and `CLROS_M04_SINGLE_FINALLY_READY`.
The stage order is deliberately `0,1,2,3,4,9,10,5,6,7,8`:

| Stage | Four fields |
| --- | --- |
| 0 | Module handle, retained snapshot, used bytes, function count |
| 1 | Original exception, method table, allocation count 9, live objects 3 |
| 2 | Original raw RIP, locally rejected RIP xor 1, original RSP, rejection result 0 |
| 3 | Raw throw RIP/RSP, unwound parent RIP/RSP |
| 4 | Matched base type, typed handler, parent RBP, throw return slot |
| 9 | Original exception, parent stamp address, seed+2, cleanup count 1 |
| 10 | Cleanup handler, typed handler, same stamp address, same stamp value |
| 5 | Original exception, seed, observed stamp, catch count 1 |
| 6 | Compiled continuation RIP, parent RSP/RBP, catch count 1 |
| 7 | Original exception, seed, result seed+1, continuation count 1 |
| 8 | Stack-check count 8, lowest witness, both original canaries |

The native associations are Thrower allocation 40 plus return 8, parent RBP at
parent RSP+64, stamp at RBP-24, and Catcher offsets 32/71/94/47 for raw return,
cleanup, catch and continuation. The verifier admits only the exact 11-byte
two-clause table and three associated unwind rows. Metadata kind 1 alone does
not prove finally; frozen C# and native contribution review provide provenance.

The strict parser rejects incomplete/control-bearing lines, missing/duplicate/
reordered rows, wrong identities or frame/stamp associations, disguised failures
and all post-terminal noise. Local scalar rejection is distinct from corrupting
a live context. Host grammar tests alone do not prove execution. The accepted
ordinary guest capture in [step1088](../done/step1088.md) binds all 84 records to
the reviewed native image and retained disk payload. Its raw serial SHA256 is
`567B4E6684039F55ED2EB728FBBA3054A81C64998D7F1702B14D9ABDB4854F6C`;
it has all 73 ordered/unique markers and no post-terminal bytes. Fourteen altered
copies and unselected-profile replay are rejected by the production parser on
the host; these are not additional guest executions. Explicit profile selection
and collecting-GC ancestry remain required. The fixture runs before firmware exit.

## Owned subsystem result and panic profiles

Issue #67 adds two destructive, default-off children at catalogue indices 21
and 22. Positive output preserves the original 66 first-exit application records
and adds ten: test-start, one allocator row, five original copied-ACPI rows, a
descriptor row, ready and pass. The panic form has 77 application records: it
replaces ready/pass with armed, entered and the terminal fault record. With the
fixed firmware preamble, the respective stream counts are 78 and 79. Every
`M03_OWNED` stage contains eight fixed-width hexadecimal fields tied to the
original authorities.

[Step1074](../done/step1074.md) accepts both actual guests at `4ef299f`, with
independent native and raw/process/disk review. The panic capture binds the
faulting instruction and guard address to original CR3 and the 184-byte IST2
frame. The expected-panic mode requires at least 750 ms of measured unchanged serial
output and owned teardown. Runner exit 19 becomes wrapper exit 0 only after the
exact terminal transcript; the accepted capture measures 757 ms. No owned-survival
success tail or bytes follow the fault. This remains distinct from ordinary boot
success, recovery and the unchanged M02 panic mode. [Step1067](../done/step1067.md) preserves development failures.
