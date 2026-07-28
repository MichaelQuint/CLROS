# Security

## Current Status

Security is policy baseline only.

## Implemented Behavior

- The design has no users, SSH, or remote admin surface by default.
- Network services must come from the launched app or explicit manifest.
- `OS.ManifestTool validate --manifest <path>` rejects malformed, oversized, unknown-version, unsafe-path, and duplicate-env-key manifests in host tests.
- `scripts/check-naming.ps1` now includes a source firewall:
  - rejects committed first-party `.c`, `.cpp`, `.h`, `.asm`, and `.s` files under `src/`, `std/`, and `apps/`;
  - rejects `references/` project paths in `CLROS.slnx`;
  - rejects `references/` project paths from first-party `src/`, `std/`, and `apps/` project files;
  - emits `TEST_START source.firewall`, `TEST_PASS source.firewall`, and `TEST_FAIL source.firewall <path>: <reason>` markers.
- `tools/OS.X64TextSerializerGenerator` is the C# 14/.NET 10 host-build leaf
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

In-kernel manifest loading, live W^X/NX enforcement, panic redaction, debug/release log split, a post-firmware entropy pool, full license scanning, and SBOM generation are pending.

M01-034 is linked-artifact host/build evidence. It does not modify or call from
`EfiEntry`, execute the serializer, prove the success cookie at runtime, boot
the artifact under QEMU, exercise live paging, enforce live page-table
protections, close the speculative code store bypass gap, or advance a
completion gate. Final PE section permissions are a required static property,
not evidence that live W^X/NX page mappings have been installed.

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

`OS.X64TextSerializerGenerator` and the M01-034 integration prove deterministic
object construction plus static same-build PE/map/unwind identity. They do not
prove invocation, a returned success cookie, runtime serialization, or live
page permissions. Those boundaries remain with M01-035 and M01-026.

## Next Steps

1. Complete M01-035: place the linked serializer call and checked success-cookie
   transition before every current generated-code publication transfer without
   weakening the M01-034 static artifact checks.
2. Move manifest validation contract into the package/image path and later kernel parser.
3. Add license/provenance metadata for any adapted reference source.
4. Implement real page-permission transitions for generated stubs using the W^X policy, materialization, and transition-plan contracts.
5. Implement real page-permission transitions for native app payload pages using the executable-memory plan contract.
6. Document page permissions and shellcode-stub exceptions.
7. Implement a real entropy plan that seeds a post-firmware pool before `ExitBootServices` and exposes only status-coded kernel-facing APIs.
