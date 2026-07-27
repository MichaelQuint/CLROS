# Step 868 - Canonical M01 EFI source closure and staged build switches

## Summary

Implemented Gitea issue M01-020 by linking the already reviewed M01 memory and
paging leaves/coordinators into the canonical `src/OS/OS.csproj` no-stdlib EFI
compilation and adding three explicit, default-off build switches:

```text
EnableM01LoadedImageStackProbe
  -> EnableM01LiveAllocatorProbe
    -> EnableM01OwnedPagingProbe
```

The EFI project now has one exact 41-item compilation: 38 explicitly linked
`OS.Kernel` sources and the three local EFI sources. The M01 catalog contributes
28 reviewed entries, three explicit prerequisites, and the existing seven
kernel baseline entries. There are no globs, conditional future slots,
project references, managed assembly references, generated-source hooks, or
alternate EFI projects.

The ordinary build still has all three M01 switches set to `false`. This step
builds and verifies the all-three opt-in artifact, but it does not change
`EfiEntry`, invoke any linked M01 coordinator, allocate firmware pages, switch
stacks, load CR3, modify a live PTE/TLB, or execute a guest.

## Why

M01-001 through M01-019 and M01-027 had accumulated host-reviewed value types,
allocator leaves, paging encoders, x64 byte plans, and integration
coordinators, but those sources deliberately remained outside the production
EFI compilation. M01-020 is the serialized `lane:os-project` boundary that
turns those disconnected reviews into one auditable no-stdlib source closure
without prematurely wiring a live boot stage.

The original issue catalog named 28 reviewed sources but omitted
`UefiMemoryType.cs`. Compiling the literal list produced 18 unresolved-type
diagnostics. Adding only `UefiMemoryType.cs` closed that dependency. The
manifest, documentation, preflight marker, and tests preserve the discovery
instead of silently claiming that the incomplete list compiled.

## Files changed

Production/build boundary:

- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`

Focused verification and durable contract:

- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
- `.gitea/backlog/clros-issues.json`
- `docs/efi-build.md`

Narrow historical-firewall migrations:

- `tests/OS.HostTests/BootPageAllocatorFreeCoalescingTests.cs`
- `tests/OS.HostTests/BootPageAllocatorOnePageAllocationTests.cs`
- `tests/OS.HostTests/BootPageAllocatorStateMaterializerTests.cs`
- `tests/OS.HostTests/BootReservationSubtractorTests.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `tests/OS.HostTests/M01LiveAllocatorIntegrationTests.cs`
- `tests/OS.HostTests/M01LoadedImageStackIntegrationTests.cs`
- `tests/OS.HostTests/OwnedBootStackLayoutTests.cs`
- `tests/OS.HostTests/OwnedBootStackSwitchPlanTests.cs`
- `tests/OS.HostTests/PageZeroMappingPolicyTests.cs`
- `tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs`
- `tests/OS.HostTests/X64PageTableEntryTests.cs`

Evidence history:

- `done/step868.md`

The historical tests changed only their superseded “source must be absent”
predicates to require an exact canonical `Include`/`Link`. Their exclusions for
host tests, legacy planners, `EfiEntry`, `BootSequence`, `Probes`, firmware
calls, live execution, and host-only markers remain in force.

No change was made to `src/OS/Boot/EfiEntry.cs`,
`src/OS.Kernel/Boot/BootSequence.cs`,
`src/OS.Kernel/Boot/BootSequencePlan.cs`,
`src/OS.Kernel/Diagnostics/Probes.cs`, `scripts/run-qemu-smoke.ps1`,
`docs/boot.md`, `docs/architecture.md`, `docs/completion-gates.md`,
`README.md`, or any reference snapshot.

## Tests run

All authoritative commands were run from
`C:\Projects\CLROS-M01-020` on branch
`codex/M01-020-efi-source-closure`.

```text
Windows PowerShell AST parse of scripts/build-efi-shell.ps1
  exit 0

dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 --disable-build-servers
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  native process exit 0
  exactly one TEST_START m01.020
  exactly one CLROS_M01_020_READY
  exactly one TEST_PASS m01.020
  zero TEST_FAIL markers
  terminal marker: TEST_PASS host.tests

dotnet build .\CLROS.slnx --nologo -m:1 --disable-build-servers
  exit 0; Build succeeded; 0 warnings; 0 errors

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -PreflightOnly
  exit 0
  EFI_PREFLIGHT_M01_SWITCHES status=ok loadedImageStack=false liveAllocator=false ownedPaging=false
  EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=28 prerequisites=3 compileItems=41 prerequisiteGap=UefiMemoryType
  EFI_PREFLIGHT_OK

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -PreflightOnly -Configuration Debug
  exit 0; evaluated Debug closure passed for both workload profiles

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -PreflightOnly -EnableUefiGoneSimulationProbe -EnableExitBootServicesFreshMapProbe -EnableAcpiPhysicalDiscoveryProbe
  exit 0; all three pre-existing probe properties/symbols were preserved

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe -LogPath artifacts/logs/m01-020-efi-publish.log -EfiOutputPath artifacts/efi/BOOTX64-M01-020.EFI
  exit 0
  EFI_PROBE M01_LOADED_IMAGE_STACK
  EFI_PROBE M01_LIVE_ALLOCATOR
  EFI_PROBE M01_OWNED_PAGING
  TEST_PASS efi.artifact.verify
  TEST_PASS efi.shell.publish

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
  exit 0
  TEST_PASS naming.guard
  TEST_PASS source.firewall

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
  exit 0
  TEST_PASS workload.profile.tooling default single
  TEST_PASS qemu.runner.dryrun

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-gitea-backlog.ps1 -Offline
  exit 0
  SYNC_MANIFEST_VALID milestones=16 labels=68 epics=16 children=316 issues=332
  TEST_PASS gitea.backlog.sync

git diff --check
  exit 0; no whitespace errors
```

The authoritative repository-wide host log is 808,582 bytes and has SHA-256
`8E648352AAC0C020D22FC0FA623B6176F3C480EF1AFD9E298DEFF5570064597B`.
It is preserved locally at
`artifacts/logs/m01-020-host-tests-closure-fde09c7b8870472f9105ae459788be77.log`
and is not added to Git.

## Evidence

The exact canonical XML fingerprints enforced before tool discovery are:

```text
BB9F1DC5FAB00848C7682478B5651B833FA9A66F3C7EE03A9E3A8B425EB865E3  src/OS/OS.csproj
0743A5F401F40744DC0A8C8A4D2D8467D9716A7040DF32865974D1F6693115FA  Directory.Build.props
DE2B50F646CEBD9B29F23F2A74B2AD5FF9D56F80680D6CF5BA826E40DFC959CF  Directory.Build.targets
```

The preflight also independently evaluates the project with `dotnet msbuild`
for `SingleMinimalApi` and `MultiMinimalApi`, the caller-selected `Debug` or
`Release` configuration, and every enabled old/new probe property. It requires
the exact ordered compile closure, zero project/assembly references, the two
expected SDK analyzers only, exact no-stdlib properties, and the exact allowed
symbol set. Custom configurations, unknown switches, and invalid M01
dependency combinations fail before tool discovery.

The active-source firewall removes only exact `!NOSTDLIB_EFI` branches and
fails closed for ambiguous or unsupported preprocessing. It rejects unguarded
host/BCL dependencies and hostile managed forms including:

- `Span`/`ReadOnlySpan`, strings, `nameof`, object, managed arrays of any rank,
  managed delegates, exceptions, reflection, and unavailable BCL constants;
- records, non-static/file/nested classes, generic types/methods/constraints,
  anonymous objects, attributes (including parameter attributes), interfaces,
  events, and dynamic use;
- inline `using` aliases, `namespace System`, unqualified managed root types,
  verbatim identifiers, and Unicode identifier escapes; and
- source/project globs, conditional slots, imports, analyzers, duplicate
  packages, target collisions, `InitialTargets`, target-time file mutation,
  and generated compile items.

The all-three artifact proof is:

```text
9F9A33DCCD2C3B1A7988414B969711F19BDA3672E3058FA74EF492BEED34B850     54,272  BOOTX64-M01-020.EFI
BA8369DBE9B2F996E5A842E2B54C40065111373BA252DCE5966000E8F02530E0     31,913  BOOTX64-M01-020.EFI.link.map
0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F        217  BOOTX64-M01-020.EFI.link.rsp
C87B679DD053953EB20AC588DF53319AFA81E32140B719344CE97BA4CF67C4E2      2,874  m01-020-efi-publish.log
```

Artifact verification reports PE32+ x64 (`0x8664`), EFI application subsystem
10, entry RVA `0x00003730` in `.managed`, same-build `EfiMain` association, and
absent import, bound-import, IAT, and delay-import directories. The linker was
MSVC `14.51.36252.0`; Windows SDK `10.0.28000.0`; ILCompiler/runtime pack
`10.0.8`; QEMU was `11.0.50`; and the OVMF image was 3,653,632 bytes with
SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

Generated EFI, map, response, publish, and host-test files are verification
artifacts under ignored `artifacts/` paths and are not committed.

Reference influence for this issue is a build-shape/test cross-check only:

```text
FFEA08A6E36569089B9FF8849E912F5F2F5898292246D104DEF34BEDCA42D2B8  3,186
C:\Projects\CLROS\references\zerosharp-master\efi-no-runtime\efinoruntime.csproj
```

No ZeroSharp or other reference source was copied. The now-linked
`M01LivePagingIntegration.cs` and `M01OwnedCr3CompositeTransition.cs` retain
their already documented SharpOS architectural influences and independently
authored prior-step provenance; M01-020 only links those reviewed files and
introduces no new algorithm adaptation or license claim.

Three independent read-only reviews found and then rechecked the final diff:
one covered production/project/preflight bypasses, one covered tests/manifest/
documentation honesty, and one covered policy, source firewall, provenance,
secrets, first-party source extensions, and exact path ownership. Their final
reviews reported no remaining blocker.

## Failures

Failures and review findings were retained rather than erased:

- The literal 28-source catalog failed with 18 unresolved
  `UefiMemoryType` diagnostics. Adding only `UefiMemoryType.cs` fixed it.
- The first complete harness reached M01-009 and exposed 14 obsolete
  source-absence assertions in 12 historical tests. They were migrated only
  to exact positive `Include`/`Link` assertions, preserving all live-boundary
  exclusions.
- The next harness reached the durable manifest contract and exposed its
  stale expectation that already-closed M01-027 was still open. The test was
  corrected to the manifest’s pre-existing closed state.
- An early script form accepted an unknown switch until `[CmdletBinding()]`
  made unknown parameters fail.
- Independent review found that evaluated closure checks did not initially
  forward the three legacy probe properties. Forwarding and individual
  regression cases were added.
- Independent review found that preflight initially evaluated `Release` even
  when publish requested another configuration. The caller’s exact
  `Debug`/`Release` choice is now evaluated; unsupported custom configurations
  fail explicitly.
- Conditional required properties, duplicate package IDs, same-name target
  overrides, `InitialTargets`, extra validation-target tasks, and target-time
  file writers could weaken a purely evaluated closure. Canonical XML
  fingerprints plus structural/evaluated checks now reject them.
- Adversarial source review found lexical escapes for nested/file classes,
  records, array ranks, aliases, `namespace System`, `nameof`, anonymous
  objects, attributes, generic holders/methods, Unicode escapes, and verbatim
  identifiers. Each now has a negative fixture.
- One historical test marker still said the EFI closure excluded
  `X64BootIdentityMappingPlan.cs` after it became required. The marker now
  states that the canonical closure includes the planner and excludes host
  tests.
- The manifest evidence boundary said no opt-in variant was built while its
  required command built one. It now states precisely that the variant is
  built but not wired or executed live.
- Intermediate repository-wide runs that began before a review-driven source
  change were deliberately stopped or discarded. The authoritative hash above
  is from the final reviewed, frozen source state.

No failure remains in the authoritative build, preflight, artifact,
repository-wide host harness, naming/source firewall, backlog validation,
QEMU dry run, or diff check.

## Deferred work

- M01-021 owns the first default-off `EfiEntry` loaded-image/owned-stack wiring
  and the corresponding `docs/boot.md` integration.
- M01-022 owns real-QEMU proof of that stage.
- M01-023/M01-024 own the live allocator wiring/proof.
- M01-025/M01-026 own live paging/CR3 wiring and cumulative real-QEMU proof.
- `docs/architecture.md` and historical portions of `docs/boot.md` still
  contain pre-link source-absence language. Those central/boot documentation
  corrections are explicitly deferred to their owning integration lane;
  silently editing forbidden files here would violate M01-020’s contract.
- No real QEMU/OVMF guest was run for M01-020. The QEMU command was a dry run,
  and the produced artifact is build evidence, not guest execution evidence.
- No completion-gate status changes. Real stack switching, allocator state,
  CR3/PTE/TLB effects, owned diagnostics, firmware exit, drivers, workloads,
  hosted .NET, Kestrel, or cloud-provider support remain unproven.

## Evidence boundary

- `CLROS_M01_020_READY` and `TEST_PASS m01.020` prove the exact canonical EFI
  source/build closure and host-side negative contract only.
- The all-three artifact proves that the opt-in symbols compile, NativeAOT
  publish, link, and satisfy the PE/EFI/import/entrypoint checks.
- `EfiEntry`, boot order, and default runtime behavior are unchanged.
- No linked M01 method was invoked and no privileged instruction was executed.
- No firmware page, stack, CR3, PTE, TLB, device, serial port, or guest state
  was touched.
- This step does not advance M01's live gates or any CloudReady/production
  claim.
