# Security

[Step1617](../done/step1617.md) repairs the writable final clock after verified NIC retirement and retains a fresh source-matched native publication. Expanded byte checks include callbacks and the terminal fault/NMI path; unresolved targets, conditional stack probes and interrupt bounds still precede guest activation. Nine focused Fast checks pass. Writable guest I/O, full Blazor, reboot persistence, SQLite durability and continuous service remain open; all original failures are preserved.


[Step1615](../done/step1615.md) integrates full process/storage exclusion, exact writable PID binding, retired borrowed-filesystem cleanup, bounded socket observers, the separate FAT32 encoder and identity-bound mutable image clones. The coherent root graph passes353 Fast checks and one fake-media Native control, unchanged/zero reuse. Fresh Linux HTTP r04 at7c0fa44b passes ten responses, three startup rejections, canceled-body cleanup and a distinct console verifier;246 original files/241 declared output hashes are independently verified. Native writable activation, full Blazor, reboot persistence and SQLite durability remain open. Historical failures remain failed.


[Step1614](../done/step1614.md) Actual v6 timing r14 passes four responses and exact64KiB with joined intentional cancellation:34.575s phase=16.506s poll+13.374s dispatch outside poll+4.695s supervisor outside poll. It does not qualify full Blazor or normal cleanup. LinuxHTTP r03 is independently joined FAILED before app launch; the pinned BusyBox setsid invocation repair passes19 Fast controls and requires freshLinuxr04. The P helper is integrated; its separate failed ordinary allocation experiment remains unimported/open.

[Step1610](../done/step1610.md) The repaired HTTP owner authenticates sealed archived recipe/output identities before selecting current tools. Exact current paths/hashes and actual prelink libraries are held, and every fresh publication input remains mandatory with no archived-copy fallback. Host tamper/missing/replacement controls pass. V5 copied observations and successful finite responses grant no new isolation, guest locking/flush or durability authority.

[Step1600](../done/step1600.md) Borrowed filesystem admission validates the complete retained filesystem/port/engine/block/DMA/MMIO/observer tree. Busy cannot publish output or EOF. Global full-tree descriptor/thread hooks are continuing, and writable/read-only coexistence admission remains unchanged. Native writable integration is not qualified.

[Step1597](../done/step1597.md) Writable path metadata has host-only support with copied confined paths and original 144-byte mapping/capacity protection through real waits and cancellation. Explicit root0700/file0600 and uid/gid0 values describe the confined mount policy; they are not persisted FAT32 Unix permissions or a multi-user security boundary. Shared-owner readonly-range preparation grants no second device owner or new copy/I/O authority. Native writable activation, persistence and isolation remain unqualified.

The production appliance profile has no guest interactive shell, terminal, SSH,
general command interpreter or command-execution administration endpoint.
Management uses the deployment environment's lifecycle/configuration boundary;
this is a design requirement, not a claim of implemented provider integration.
Host tooling and bounded diagnostic output do not authorize an interactive guest
console. Application endpoints still need their own authentication and input
validation. Both Linux and future custom PE adapters must enforce the same core
capability, user-memory, ownership, quota and cleanup rules; format selection
must not bypass protection. See the [core/adapter contract](architecture.md#core-services-and-application-adapters).

[Step1571](../done/step1571.md) records three complete owned positive mechanical
file captures across both disk variants. Error/cancellation/concurrent progress,
full disk HTTP and all writable/durability/isolation controls remain outstanding;
these finite positive witnesses grant no broader security acceptance.

The [October19 review](repo-audit.md#october-19-whole-project-review) adds
[#778](http://192.168.4.35:3000/admin/CLROS/issues/778) for peer-controlled unread
data retaining fd-less bulk TCP capacity, and
[#780](http://192.168.4.35:3000/admin/CLROS/issues/780) for concurrent tracker data
loss. Both are reproduced in local host fixtures; no live attack was attempted.
#778 unread-close retirement and #779 resource-peak reconstruction were later
qualified by step1481 and are closed. #780 remains open as of October23; protect
tracker writes with fresh versioned reads and readback. Step1556 adds finite
quota termination/churn, not general isolation or production security.

The isolated immutable `/app/wwwroot` service confines path traversal and uses
generation-bound descriptors. A pending read pins only its originally admitted
writable output prefix; mapping changes that overlap it reject until consume
or cancellation revokes the copy. Observer rejection or uncertain device reset
retains DMA. Host controls cover these contracts; guest disk, write, locking,
durability and isolation acceptance remain separate.
[Step1570](../done/step1570.md) joins mechanical call windows to original sector
bytes as development evidence. Its original outer command remains failed;
fresh owned capture, pending cancellation/error controls and the disk/browser
demonstration remain required. No writable or multi-app security scope advances.

[Step1437](../done/step1437.md) qualifies the bounded DRBG/TCP-key ownership
contracts defined in [step1424](../done/step1424.md).
The construction follows [RFC9293 section3.4.1](https://www.rfc-editor.org/rfc/rfc9293.html#section-3.4.1)
with HMAC-SHA256 as the tuple PRF and a4-microsecond monotonic component, plus
an ASCII `CLROS.TCP.ISN.v1` domain. Network-order local IPv4/port and remote
IPv4/port identify the tuple. A distinct pre-exit firmware acquisition supplies
the32-byte private TCP key; it is not guest AT_RANDOM, a DRBG key or getrandom
output. Failure admits no continuing network; no clock/counter entropy fallback
exists. Temporary capture payloads and firmware callbacks are cleared even on
partial initialization failure. Original native erasure and actual guest shutdown/secret release now join in
step1437. Seed/reseed/source-failure controls remain explicit host evidence.

Conservative startup and key-replacement quiet time is240 seconds, consistent
with [RFC6528 section3](https://www.rfc-editor.org/rfc/rfc6528.html#section-3)'s
2MSL replacement guidance. It begins at the first actual monotonic network
borrow, not a fabricated pre-exit timestamp. Replacement requires a drained
lease, a greater epoch and newly acquired entropy; equality checking does not
establish freshness. Existing TIME-WAIT is retained. Erasure waits for network
drain and worker detachment; packet/output/worker extents exclude secret storage.
Host oracle/wrap tests establish arithmetic and ownership only. They do not
establish entropy quality, cryptographic certification, whole-stack safety,
continuing guest operation, isolation, cloud or production security.

The default-off [M16-005 diagnostic](../done/step1201.md) qualifies one mechanical
CPL3 boundary: immutable user/supervisor mappings, complete bounded write-buffer
preflight, dedicated guarded kernel stack, checked return state, HLT/supervisor
fault containment and kernel restoration. It establishes owned NULL FS/GS
selectors before preserving its baseline. This single-CPU, IRQ-off x87/SSE
excursion supplies no general process isolation, scheduler, signal handling,
continuing quotas, arbitrary NativeAOT workload or production-security proof.

The [NativeAOT-first profile](workload-profiles.md) admits only manifest-bound
immutable executables and explicitly authorized endpoints/resources.
Its restricted Linux adapter does not authorize a shell, SSH, arbitrary exec,
remote workload acquisition or a general POSIX environment.
Qualification must test user/kernel protection, W^X, hostile pointers and
signal contexts, descriptor ownership, quotas and cross-process failures.
No shell is attack-surface reduction, not proof that app exploits cannot
occur or that CLROS is more secure than hardened Linux/Windows. No new
security mechanism is implemented by this architecture change.

## Architecture review security work

Open [review issues](workload-profiles.md#architecture-review-implementation-order-2026-10-16)
place ordinary connection timeouts and unsupported RX packet content behind
local failure boundaries before the Kestrel socket ladder. Unsafe DMA metadata
and broken ownership still require fail-closed handling. The host RX
reproduction does not establish an externally reachable attack on every backend.

M16-017 owns actual CPL3 invalid-opcode/divide-fault containment. Source review
found vectors 6/0 retain the terminal kernel path while the user transition
replaces GP/PF entries. This is a static finding; a real guest reproduction,
correct normalized frame provenance, SIGILL/SIGFPE or explicit workload-exit
policy, supervisor progress and checked reap are required. CPL0 faults remain
terminal. M14-013/#298 must additionally keep another protected application
serving external HTTP during the fault.

Step1437 qualifies M11-012/#240 before repeated-service acceptance: reviewed
HMAC-DRBG known answers and independent seed/reseed/failure controls, actual
guest requests larger than32 bytes and18,708 total bytes, bounded `getrandom`
semantics and shutdown zeroization. Neither a larger reservoir nor statistical output tests establish
cryptographic security. M16-016 and M14-006/#291 provide explicit single-app and
per-process commit budgets. Host-side effects/context/ownership analysis
(M11-019), independently checked ABI generation (M11-020) and explicit capability
generation (M11-021) supplement hardware isolation and runtime validation;
annotations alone are not safety proofs.

The second pass adds [M07-032 handshake-reset cleanup](http://192.168.4.35:3000/admin/CLROS/issues/757) and
[M07-033 continuing TCP ISNs](http://192.168.4.35:3000/admin/CLROS/issues/758). The latter must use an independently
owned secret supplied by M11-012, preserve old-segment protections, and avoid
both predictable diagnostic seeds and permanent admission exhaustion.
The host tests establish availability defects, not remote exploitability.

[M16-019 owned realtime](http://192.168.4.35:3000/admin/CLROS/issues/759) is also a workload capability: the pinned
PAL silently converts failed realtime reads into epoch time, observed in retained
development HTTP. Certificate/token validity would depend on trustworthy time;
no such exploit was tested. [M16-020](http://192.168.4.35:3000/admin/CLROS/issues/760) prevents actionable signals
being stranded behind renewed waits; a guest GC deadlock remains unproved.
[Step1438](../done/step1438.md) adds a development-only pending-signal recheck
after new pipe/epoll/futex waits. The stolen-readiness and initial-wait host
controls pass; original native and actual guest scheduling remain pending.
[M16-021](http://192.168.4.35:3000/admin/CLROS/issues/761) must either enforce restricted SIGPIPE admission or honor
the accepted live disposition. Successful declarations must agree with behavior
across the signal, I/O, time and workload owners.

## Current Status

Security includes a policy baseline and finite qualified user/kernel mapping,
descriptor ownership, network containment, entropy ownership and quota cleanup
controls. These are bounded profile results, not general exploit resistance,
multi-app isolation, cryptographic certification or production readiness.
See steps1201/1380/1481/1556 and `docs/completion-gates.md`.

## Implemented Behavior

- The design has no users, SSH, or remote admin surface by default.
- Network services must come from the launched app or explicit manifest.
- `OS.ManifestTool validate --manifest <path>` rejects malformed, oversized, unknown-version, unsafe-path, and duplicate-env-key manifests in host tests.
- `dotnet run --project tools/OS.Tooling -- repo check` includes a source firewall:
  - rejects committed first-party `.c`, `.cpp`, `.h`, `.asm`, and `.s` files under `src/`, `std/`, and `apps/`;
  - rejects `references/` project paths in `CLROS.slnx`;
  - rejects `references/` project paths from first-party `src/`, `std/`, and `apps/` project files;
  - emits `TEST_START source.firewall`, `TEST_PASS source.firewall`, and `TEST_FAIL source.firewall <path>: <reason>` markers.
- `tools/OS.X64TextSerializerGenerator` is the C# 14/.NET 11 host-build leaf
  included in `CLROS.slnx`. M01-034 imports its audited targets into the EFI
  project without a managed `ProjectReference`; only the exact generated native
  object enters the native link.
  - Its canonical output is exactly 336 bytes with SHA-256
    `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.
  - The `.text$clrossm` payload is exactly 20 bytes:
    `53 B8 00 00 00 00 B9 00 00 00 00 0F A2 B8 43 4C 52 53 5B C3`.
    The section is code, readable, executable, 16-byte aligned, and not
    writable.
  - `.xdata$clrossm` contains the exact 8-byte unwind record
    `01 01 01 00 01 30 00 00`. `.pdata$clrossm` contains one exact 12-byte
    runtime-function record. Both sections are initialized data, readable,
    4-byte aligned, nonwritable, nonexecutable, and not code.
  - The object contains exactly three `IMAGE_REL_AMD64_ADDR32NB`
    relocations: the runtime-function begin and end fields target the one
    external function symbol, while the unwind field targets one local static
    `.xdata$clrossm` base symbol. Text and unwind data have no relocations.
  - The command writes one complete sibling temporary file, atomically renames
    it without overwrite, and re-reads the final bytes before reporting
    success. Failure cleanup removes only its uniquely named temporary file;
    it never deletes a destination pathname after publication. Generated
    objects remain ephemeral; CLROS commits neither an `.obj` nor native
    source.
  - M01-034 binds the exact object through a no-stdlib
    `RuntimeImport`/`InternalCall`, retains it explicitly in the native link,
    and independently verifies the final PE/map/evidence association. The
    native link requires `/BREPRO`; `/FORCE` and every `/FORCE:*` unresolved
    binding escape are rejected.
  - The final serializer bytes must be executable and readable but not
    writable. Its matching exception-directory entry and unwind bytes must be
    readable, nonwritable, and nonexecutable. The verifier fails closed on
    writable code, writable or executable unwind data, overlapping sections,
    malformed/ambiguous runtime-function entries, ordinary imports, fallback
    binding, stale hashes, and source/evidence mismatches.
  - One repository-wide, cross-process and cross-session exclusive file lock
    at `artifacts/.locks/efi-publish.lock` is acquired before any preflight or
    publish output. The evidence build read-locks its complete source closure
    before authoritative validation and removes only the exact non-reparse
    RID-specific managed/native output directories.
  - Restore uses a fresh local source containing five exact SHA-256-pinned
    package archives and a generated `NuGet.Config` that clears ordinary,
    fallback, and audit sources. Packages expand into a fresh isolated root;
    the build validates the exact version set and 900 stable package files,
    validates six package metadata records, and keeps all 906 files read-locked
    through verification. Nine package target/task inputs receive an additional
    exact hash contract.
  - The five generated NuGet/MSBuild restore files are read-locked and parsed
    as data. Both restore and publish disable default project-extension imports
    and exclude generated package imports. A post-restore evaluation must
    contain exactly 44 compile items, zero project references, zero references,
    and two SDK analyzers before the no-restore, nonincremental publish begins.
  - The nested host generator removes inherited AOT/package globals and
    explicitly replaces the restore/project-extension globals it needs with
    canonical child values before its package-free build. Automatic response
    files cannot influence preflight evaluation.
  - Every MSBuild surface pins the verified SDK/extension roots, neutralizes
    the user-extension root, and disables Common.props, Common.targets, CSharp,
    and cross-targeting wildcard imports. Restore, evaluation, publish, and SDK
    recheck run inside startup-gated kill-on-close Job Objects with bounded
    output, explicit timeouts, node reuse disabled, and confirmed whole-tree
    termination.
  - Link-evidence schema 3 records the restore archive/source, generated
    restore, full package-tree, package-build-input, evaluated-item, source,
    toolchain, and artifact closures. The independent verifier recomputes those
    identities, validates the version-4 assets graph and task bindings, and
    rejects any extra package, import surface, mutable input, or noncanonical
    compiler/runtime path.
  - Source and output ancestor chains are reparse-checked. Final evidence files
    must be ordinary single-link files with distinct Windows file identities.
    The canonical 57-source closure is also single-link and identity-distinct.
    Source, package, tool, and output read handles deny mutation/replacement
    through verification, and both producer and verifier rehash held inputs
    immediately before success. Package and tool inputs are not claimed to
    have globally unique file identities.
  - The parent assigns a startup-gated wrapper to a kill-on-close Windows Job
    Object before releasing it to invoke the independent verifier. Timeout,
    bounded output-drain failure, or inability to confirm whole-job
    termination fails closed. A future-dated stale managed assembly, hostile
    default project extension, hung verifier descendant, or
    wall-clock-dependent native output is not accepted as evidence.
  - The standalone verifier independently creates its post-restore MSBuild
    child suspended, assigns it to a kill-on-close Job Object before resume,
    bounds strict UTF-8 output and elapsed time, and confirms that the complete
    descendant tree has exited. It accepts only its repository's exact
    `src/OS/OS.csproj`, exact `EfiEntry.cs`, and exact 57-source closure;
    copied artifact bundles remain inspectable without substituting an
    alternate source contract.
- M01-035 source-wires checked direct calls to that immutable serializer at
  all four current `EfiEntry` generated-instruction publication epochs. A
  root-owned primitive mask survives the one-way stack switch and may advance
  only `0 -> 1 -> 3 -> 7 -> 15`. Every pre-execution epoch-state mismatch or
  wrong cookie emits `M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED` before
  fail-closed cleanup, return, or terminal spin. The aggregate
  `M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED` marker is source-gated on mask
  `15` only after the call-gate observation audit and continuation canary, and
  immediately before `M01_POST_CR3`. Both markers remain unobserved guest
  contracts until M01-026.
- `ExecutableStubMemoryPlan` defines the first host-tested W^X policy contract for future C#-emitted executable stubs. It accepts write-then-execute layout planning, rejects accidental RWX page intent by default, calculates page-aligned caller-owned layout metadata, validates page size/alignment/payload inputs, and rejects default/zero result-success cases without allocating memory or changing page protections.
- `ExecutableStubBundlePlan` defines the first host-tested deterministic layout for future generated diagnostic stubs inside a caller-owned executable-stub payload region. It packs stable COM1, descriptor-table, and controlled-fault-probe slots while rejecting duplicate, unknown, zero-length, invalid-memory-plan, insufficient-memory-plan, overflow, and default/zero result-success cases without copying or executing bytes.
- `ExecutableStubBundleMaterializer` defines the first host-tested byte-copy boundary for planned generated diagnostic stubs. It writes only into caller-owned storage, validates failed plans, undersized source/destination buffers, and default/zero result-success cases, preserves guard bytes in tests, and still avoids executable allocation, page-protection changes, instruction-cache flushes, and stub execution.
- `ExecutableStubPageTransitionPlan` defines the host-tested transition boundary after materialization. It requires write-then-execute protections, rejects RWX intent, validates aligned caller-owned ranges, rejects default/zero result-success cases, and still avoids page-protection calls or execution.
- `NativeAppExecutableMemoryPlan` defines the first host-tested W^X/NX planning contract for future native app payload executable bytes. It consumes `NativeAppPayloadMappingPlan`, calculates page-aligned executable-map metadata, requires writable staging followed by executable/final protections without write access, and rejects failed mapping plans, zero executable content, invalid page size/alignment, unaligned executable offsets, accidental RWX policy, unsafe protection states, invalid entry offsets, and overflow without allocating pages or changing live permissions.
- `OSHostMemoryProtectionPlan` defines the first PAL-facing host-tested page-protection planning contract for future `OSHost_ProtectPages` behavior. It accepts page-aligned write-then-execute transitions, rejects RWX intent, rejects unaligned or overflowing ranges, rejects executable-to-writable transitions, and rejects default/zero results as success evidence without allocating pages, changing page tables, calling OS APIs, or exporting ABI symbols.
- `IPlatformRandom` and `GuardedPlatformRandom` define a host-tested firmware RNG access guard. The adapter fills caller-owned buffers only while UEFI boot services are available, denies calls in UEFI-gone simulation and after marked `ExitBootServices`, rejects empty destinations without touching the source, and reports source failures without throwing from kernel-facing paths.
- `AwsImdsAppMetadataPolicyPlanner` defines the first host-tested EC2 metadata
  access policy fixture. It denies direct app metadata access by default,
  requires supervisor-mediated access, and keeps IAM role credentials disabled
  unless an explicit future policy enables them.

## Unsupported Behavior

General manifest-driven process launch, general W^X/NX enforcement, panic
redaction, debug/release log separation, full license scanning and SBOM
generation remain pending. Bounded Linux ELF admission/mappings and continuing
DRBG/TCP-key ownership already have scoped evidence; they do not establish
arbitrary process isolation or entropy quality.

M01-034 remains linked-artifact host/build evidence. M01-035 now adds checked
`EfiEntry` source calls against that exact binding and a fresh all-switch build,
but this still does not prove that the serializer executed, that any call
returned its success cookie, or that either new marker appeared in a guest.
M01-035 runs no QEMU smoke, guest boot, or QEMU dry-run smoke workflow. Its
mandatory EFI preflight only queries `qemu-system-x86_64 --version` and
`qemu-img --version` for tool discovery; those probes do not exercise live
paging, empirically exclude speculative code store bypass, establish cross-core
or DMA-written-code publication, enforce live page-table protections, or
advance a completion gate.
Final PE section permissions are a required static property, not evidence that
live W^X/NX page mappings have been installed.

The AWS metadata policy fixture is not live enforcement. There is no app
supervisor, process isolation, socket firewall, IMDS client, credential cache,
or per-app runtime permission path yet.

## Tests

Manifest validation has host tests in `tests/OS.HostTests/ManifestToolTests.cs`. No in-kernel hardening tests exist.

`tests/OS.HostTests/SourceFirewallTests.cs` verifies that the guard accepts the current tree, rejects a temporary first-party `.c` file, ignores forbidden extensions inside `references/`, rejects `references/` entries in `CLROS.slnx`, and rejects `references/` project references from first-party project files.

`tests/OS.HostTests/M01InstructionPublicationSerializerGeneratorTests.cs`
clean-builds and invokes the standalone generator through a bounded subprocess,
then independently parses the COFF bytes rather than trusting the writer's
object model. The group covers exact header, section, symbol, relocation,
payload, unwind, runtime-function, ABI, deterministic-byte, stable-hash, and
checkout-path behavior. Its fail-closed cases cover default/zero and malformed
configuration, flags and permissions, missing/extra/mutated symbols or
relocations, malformed unwind or function ranges, truncation, out-of-bounds and
overflowing offsets/counts, capacity limits, path traversal, and stale output.
The actual generator failure path is exercised with a destination-directory
collision and must preserve that destination while leaving no unique temporary
file. Separately, the bounded subprocess harness is tested against synthetic
partial output, output overflow, and timeout children; those cases validate the
host runner's failure classification and process-tree termination, not an
intentional hang inside the generator.

M01-033 emits:

```text
TEST_START m01.033
CLROS_M01_033_READY bytes=336 sha256=DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B
TEST_PASS m01.033
TEST_FAIL m01.033 <reason>
```

`tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs` structurally
selects the disabled, stage-one, stage-two, and all-switch source profiles and
requires direct serializer-call cardinalities `0`, `2`, `2`, and `4`. Its eight
groups check exact call ownership and final write/readback/binding order,
success-cookie consumption, the shared root-state lifetime and exact
`0 -> 1 -> 3 -> 7 -> 15` sequence, continuation observation, byte-authored
marker text, immediate epoch-three and epoch-four execution windows, and
failure-marker domination. Mutation cases reject missing, duplicate, early,
late, branch-only, ignored, synthetic, wrong-cookie, wrong-binding,
dynamically generated, CPUID-stream-only, LFENCE/MFENCE/volatile/lock,
ordinary-call, firmware-callback, reset, reconstruction, regression, and
partial-mask substitutes. The aggregate markers are:

```text
TEST_START m01.035
CLROS_M01_035_READY
TEST_PASS m01.035
TEST_FAIL m01.035 <reason>
```

These markers are host-source evidence and do not report runtime cookie or
guest instruction-publication observations.

The host-tool inventory boundary enumerates the current five expected projects
and emits `TEST_PASS tools.project boundary keeps expected host tools`.

### M01-033 reference provenance

The recorded SharpOS owner grant and upstream CC0 evidence permit this bounded
adaptation. The two exact local inputs reviewed for M01-033 are:

- `references/SharpOS-main/bootasm/CoffStub.Generator/CoffWriter.cs` —
  SHA-256
  `3604F93946085BD7052A7350A382DEE0CD41BEE69DB9A8AB5C8C88B207C930D1`.
- `references/SharpOS-main/bootasm/CoffStub.Generator/EmitCoffStubsTask.cs`
  — SHA-256
  `9F52110767B74FF819205889ED8F3798B704958A058BA01B903E5794BCC01D41`.

The adaptation boundary is limited to the high-level concepts of explicit
little-endian field emission, a deterministic zero timestamp, COFF long-name
string-table encoding, and fail-closed host-side generation. CLROS independently
implements and tests the three-section layout, relocations, unwind and
runtime-function records, all bounds and overflow checks, and atomic output.
The reference explicitly lacks the multiple-section and relocation behavior
required here; no reference artifact or output is CLROS evidence.

`tests/OS.HostTests/NativeAppPayloadMappingPlanTests.cs` verifies only deterministic native payload range planning. It does not prove W^X/NX enforcement, executable page permission transitions, relocation safety, or native app entry invocation.

`tests/OS.HostTests/NativeAppExecutableMemoryPlanTests.cs` verifies host-only native app executable-memory permission planning. It does not prove live page allocation, live W^X/NX enforcement, executable page permission transitions, relocation safety, or native app entry invocation.

Current executable-stub policy markers:

- `TEST_PASS executable stub memory plan accepts write-then-execute policy`
- `TEST_PASS executable stub memory plan rejects accidental rwx policy`
- `TEST_PASS executable stub memory plan calculates aligned layout`
- `TEST_PASS executable stub memory plan rejects invalid inputs`
- `TEST_PASS executable stub memory plan default result is not success`
- `TEST_PASS executable stub bundle plan packs diagnostics stubs`
- `TEST_PASS executable stub bundle plan calculates aligned slot offsets`
- `TEST_PASS executable stub bundle plan rejects duplicate or unknown slots`
- `TEST_PASS executable stub bundle plan rejects insufficient memory plan`
- `TEST_PASS executable stub bundle materializer copies planned diagnostic stubs`
- `TEST_PASS executable stub bundle materializer applies payload base offset`
- `TEST_PASS executable stub bundle materializer rejects failed plans`
- `TEST_PASS executable stub bundle materializer rejects undersized destination or source`
- `TEST_PASS executable stub bundle materializer default result is not success`
- `TEST_PASS executable stub page transition plan accepts write-then-execute`
- `TEST_PASS executable stub page transition plan rejects rwx default`
- `TEST_PASS executable stub page transition plan rejects unmaterialized payload`
- `TEST_PASS executable stub page transition plan rejects failed plans and invalid ranges`
- `TEST_PASS executable stub page transition plan default result is not success`

Current native app executable-memory policy markers:

- `TEST_PASS appmodel executable memory plan aligns payload mapping`
- `TEST_PASS appmodel executable memory plan rejects rwx policy`
- `TEST_PASS appmodel executable memory plan rejects invalid alignment`
- `TEST_PASS appmodel executable memory plan rejects zero executable content`
- `TEST_PASS appmodel executable memory plan rejects overflow`

Current PAL memory-protection planning markers:

- `TEST_PASS pal.memory protect accepts write then execute`
- `TEST_PASS pal.memory protect rejects rwx`
- `TEST_PASS pal.memory protect rejects unaligned range`
- `TEST_PASS pal.memory protect rejects overflow`
- `TEST_PASS pal.memory protect rejects invalid transition`
- `TEST_PASS pal.memory protect default result is not success`

Current firmware RNG guard markers:

- `TEST_PASS platform.random fill allowed before UEFI gone`
- `TEST_PASS platform.random fill denied in UEFI-gone simulation`
- `TEST_PASS platform.random fill denied after ExitBootServices`
- `TEST_PASS platform.random fill rejects empty destination`
- `TEST_PASS platform.random fill reports source failure`
- `TEST_PASS platform.random fill reports short read`
- `TEST_PASS platform.random result contains no managed references`

Current AWS metadata policy markers:

- `TEST_PASS cloud.aws.imds denies app by default`
- `TEST_PASS cloud.aws.imds allows supervisor mediated request`

## Known Limitations

The current manifest validation is host-side only and does not prove any in-guest hardening behavior.

The source firewall is a provenance/build-boundary guard only. It does not replace a full license scanner or legal review.

`ExecutableStubMemoryPlan` is a policy and layout contract only. It does not allocate executable memory, call page-protection APIs, switch pages from writable to executable, flush instruction caches, or execute generated bytes.

`ExecutableStubBundlePlan` is a layout contract only. It does not allocate memory, copy generated bytes, change page protections, flush instruction caches, or execute generated stubs.

`ExecutableStubBundleMaterializer` is a materialization contract only. It copies bytes into normal caller-owned buffers for later execution preparation, but it does not allocate executable memory, change pages from writable to executable, flush instruction caches, or call into those bytes.

`ExecutableStubPageTransitionPlan` is a validation contract only. It does not modify page tables, call firmware memory-protection services, flush instruction caches, or execute generated stubs.

`NativeAppExecutableMemoryPlan` is a policy and layout contract only. It does not allocate executable memory, copy payload bytes, call page-protection APIs, switch pages from writable to executable, flush instruction caches, relocate code, or execute native app bytes.

`OSHostMemoryProtectionPlan` is a PAL validation contract only. It does not implement `OSHost_ProtectPages`, allocate pages, update page tables, modify live permissions, flush instruction caches, or prove runtime W^X/NX enforcement.

`GuardedPlatformRandom` is a firmware RNG guard only. It does not implement a kernel entropy pool, deterministic/random seed management, post-`ExitBootServices` RNG, TLS, cryptographic APIs, or Minimal API/Kestrel cryptographic prerequisites.

`OS.X64TextSerializerGenerator`, M01-034, and M01-035 prove deterministic
object construction, static same-build PE/map/unwind identity, and checked
source ordering at the four current publication epochs. They do not prove
invocation, a returned success cookie, runtime serialization, empirical SCSB
exclusion, cross-core or DMA publication, or live page permissions. Those
execution boundaries remain with M01-026 and later hardware evidence.

## Next Steps

1. Preserve bounded mapping, user-pointer, descriptor, entropy/key and quota
   controls with their original native and hostile-input evidence.
2. Complete local Gate14 before advancing general process termination/isolation.
   #749 user-fault containment and #761 SIGPIPE policy precede the later
   two-app crash-survival and per-process resource claims.
3. Finish #780 tracker conflict protection; keep versioned updates and readback
   until its fix is accepted. Finish #781/#782 before trusting new disk fixtures.
4. Retain license/provenance records and explicit unsupported security features;
   no provider, production or superiority claim follows from finite controls.
