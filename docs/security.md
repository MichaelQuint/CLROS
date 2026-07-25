# Security

## Current Status

Security is policy baseline only.

## Implemented Behavior

- The design has no users, SSH, or remote admin surface by default.
- Network services must come from the launched app or explicit manifest.
- `OS.ManifestTool validate --manifest <path>` rejects malformed, oversized, unknown-version, unsafe-path, and duplicate-env-key manifests in host tests.
- `scripts/check-naming.ps1` now includes a source firewall:
  - rejects committed first-party `.c`, `.cpp`, `.h`, `.asm`, and `.s` files under `src/`, `std/`, and `apps/`;
  - rejects `references/` project paths in `OS.slnx`;
  - rejects `references/` project paths from first-party `src/`, `std/`, and `apps/` project files;
  - emits `TEST_START source.firewall`, `TEST_PASS source.firewall`, and `TEST_FAIL source.firewall <path>: <reason>` markers.
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

The AWS metadata policy fixture is not live enforcement. There is no app
supervisor, process isolation, socket firewall, IMDS client, credential cache,
or per-app runtime permission path yet.

## Tests

Manifest validation has host tests in `tests/OS.HostTests/ManifestToolTests.cs`. No in-kernel hardening tests exist.

`tests/OS.HostTests/SourceFirewallTests.cs` verifies that the guard accepts the current tree, rejects a temporary first-party `.c` file, ignores forbidden extensions inside `references/`, rejects `references/` entries in `OS.slnx`, and rejects `references/` project references from first-party project files.

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

## Next Steps

1. Move manifest validation contract into the package/image path and later kernel parser.
2. Add license/provenance metadata for any adapted reference source.
3. Implement real page-permission transitions for generated stubs using the W^X policy, materialization, and transition-plan contracts.
4. Implement real page-permission transitions for native app payload pages using the executable-memory plan contract.
5. Document page permissions and shellcode-stub exceptions.
6. Implement a real entropy plan that seeds a post-firmware pool before `ExitBootServices` and exposes only status-coded kernel-facing APIs.
