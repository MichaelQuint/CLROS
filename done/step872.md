# Step 872 - M01-024 pre-EBS allocator-arena QEMU proof

## Summary

Completed Gitea issue M01-024's evidence boundary. The QEMU smoke wrapper now
accepts a separately fail-closed `-EnableM01LiveAllocatorProbe` selector,
forwards it only into a fresh dual-probe EFI build, and rejects stage-two
selection without stage one, without `-BuildEfi`, or with
`-SkipImageBuild`.

The focused runner tests now enforce the complete cumulative stage-one and
stage-two transcript across success, missing/reordered/duplicate checkpoints,
structured adapter failures, all forbidden boundaries, timeout, reset, and
conflicting terminal states.

From clean implementation commit
`f7437521799e9fe974a9de4560db248f94addf6b`, the authoritative real run:

- rebuilt a PE32+ x64 EFI application with both M01 probe symbols;
- built a fresh 64 MiB raw GPT/FAT32 image;
- booted it under QEMU `11.0.50` and the recorded OVMF bytes;
- observed all 21 cumulative guest markers exactly once and in order;
- observed none of the nine forbidden markers;
- reported `TEST_PASS qemu.smoke`; and
- produced a passing, source-clean evidence manifest covering 26 artifact and
  tool inputs.

This is a narrowly scoped pre-`ExitBootServices` proof. It is not the M01 exit
criterion and changes no completion-gate state.

## Why

M01-020 had already source-linked the audited M01 allocator coordinator into
the EFI project. M01-023 then wired it behind the nested default-off profile
and proved its build/source contract. That did not establish that firmware
granted the explicit arena or that guest execution completed the metadata
cycle.

M01-024 crosses exactly that one live boundary. A passing marker transcript is
accepted only when it is bound to the reviewed source commit, a freshly
verified EFI hash, an exact runner command, a hashed serial log, ordered and
unique marker enforcement, forbidden-marker enforcement, clean Git state, and
the recorded tool/firmware inputs.

The guest markers do not print physical addresses. Therefore the confinement
claim is source-bound semantic evidence:

```text
reviewed source commit
  -> same-build verified EFI artifact
  -> exact artifact hash
  -> ordered and unique guest checkpoints
  -> hashed serial log and evidence manifest
```

`M01_LIVE_ALLOCATED_4K` means the reviewed coordinator mutated allocator
metadata for one 4 KiB range inside the exact two-page arena. It does not mean
that CLROS read, wrote, or zeroed the physical page's bytes.

Two reference snapshots were used only as independent test-pattern influences:

- `references/nativeaot-patcher-main/tests/Cosmos.TestRunner.Engine/Hosts/QemuX64Host.cs`
  (13,222 bytes, SHA-256
  `150A8C8D11D4EA61ABDF2AA9FCF989E295A8678653A574E4021FEA133ABB9A14`)
  informed bounded QEMU timeout, serial capture, process cleanup, and
  deterministic classification cases.
- `references/abanu-master/src/Abanu.Lib/Tests/BinaryBuddyAllocator_TestImplementation.cs`
  (22,627 bytes, SHA-256
  `22AF15CD46CBAEB28E4C8C1C18FF624C98B1EBFAA3ED0904D187263ACB2AFF7A`)
  informed exact one-cycle accounting and repeated-free rejection scenarios.

No implementation source, API, build dependency, or binary from either
snapshot was copied into first-party CLROS code.

## Files changed

- `scripts/run-qemu-smoke.ps1`
  - accepts `-EnableM01LiveAllocatorProbe`;
  - emits the exact dual-probe selection state;
  - rejects stage two without stage one, without a fresh EFI build, or with a
    skipped image build; and
  - forwards the nested switch exactly once to `build-efi-shell.ps1`.
- `tests/OS.HostTests/QemuRunnerTests.cs`
  - freezes the exact 21-marker cumulative guest chain and all nine forbidden
    boundaries;
  - covers dual-switch forwarding and all three invalid selections;
  - covers complete success, cross-stage/in-stage reordering, duplicate
    stage/operation markers, and every missing stage-two marker;
  - models EFI allocation, geometry, map recapture, stale key, descriptor,
    reservation, map-ledger, coordinator, confinement, free, and double-free
    failure classifications; and
  - covers timeout, reset, and conflicting terminal behavior.
- `docs/qemu.md`
  - records the dual-switch behavior, focused host tests, exact QEMU tuple,
    artifact hashes, and limitations.
- `docs/boot.md`
  - records the guest transcript, toolchain, artifact hashes, source-bound
    semantic proof, and profile-specific allocator boundary.
- `docs/architecture.md`
  - distinguishes the M01-024 live metadata cycle from the default path and
    from still-host-only/general allocator behavior.
- `done/step872.md`
  - preserves this immutable ledger.

No change was made to `src/OS/Boot/EfiEntry.cs`, `src/OS/OS.csproj`,
`BootSequence.cs`, `BootSequencePlan.cs`, `Probes.cs`,
`build-efi-shell.ps1`, `README.md`, `docs/completion-gates.md`, or
`references/**`.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
  --disable-build-servers`
  - passed with zero warnings and zero errors before the accepted run.
- Full built host harness from clean implementation commit:

  ```powershell
  .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe
  ```

  - ran from `2026-07-27T21:10:46.8843873Z` through
    `2026-07-27T21:20:59.1554028Z`;
  - exit code `0`;
  - exactly one `TEST_PASS host.tests`;
  - zero `TEST_FAIL` lines;
  - 822,018-byte log;
  - SHA-256
    `BF1ED174F13520996D6ECF44F2AD573D78FEADF1E8B2338F4FB9B2B35BD6B58C`.
- Focused M01-024 host markers included:

  ```text
  TEST_PASS qemu smoke script forwards M01 stage-two evidence controls
  TEST_PASS qemu smoke script rejects invalid M01 stage-two probe selection
  TEST_PASS qemu M01 stage-two ordered and unique evidence succeeds
  TEST_PASS qemu M01 stage-two rejects cross-stage and operation reordering
  TEST_PASS qemu M01 stage-two rejects duplicate start pass and operation markers
  TEST_PASS qemu M01 stage-two rejects every missing stage-two marker
  TEST_PASS qemu M01 stage-two rejects adapter failure classifications
  TEST_PASS qemu M01 stage-two rejects every forbidden boundary marker
  TEST_PASS qemu M01 stage-two timeout is deterministic
  TEST_PASS qemu M01 stage-two reset is rejected
  TEST_PASS qemu M01 stage-two conflicting terminal is rejected
  ```

- Issue-specific dry-run:

  ```powershell
  powershell -NoProfile -ExecutionPolicy Bypass `
    -File .\scripts\run-qemu-smoke.ps1 `
    -DryRun `
    -BuildEfi `
    -EnableM01LoadedImageStackProbe `
    -EnableM01LiveAllocatorProbe `
    -RequireMarker "<exact 21-marker chain>" `
    -RequireOrderedMarker "<exact 21-marker chain>" `
    -RequireUniqueMarker "<exact 21-marker chain>"
  ```

  - passed and showed exact forwarding of both selectors and all three marker
    lists.
- `dotnet build .\CLROS.slnx --nologo -m:1 --disable-build-servers`
  - passed with zero warnings and zero errors before the accepted source-bound
    run.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - passed `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - passed `TEST_PASS qemu.runner.dryrun`.
- `git diff --check`
  - passed for the implementation commit.

The authoritative real command is preserved verbatim in
`artifacts/logs/m01-024/f7437521799e-20260727T210930Z/qemu-command.txt`. It
uses:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-qemu-smoke.ps1 `
  -BuildEfi `
  -EnableM01LoadedImageStackProbe `
  -EnableM01LiveAllocatorProbe `
  -EfiArtifact "<evidence-root>\BOOTX64.EFI" `
  -Image "<evidence-root>\clros.raw" `
  -SerialLog "<evidence-root>\serial.log" `
  -SummaryLog "<evidence-root>\qemu-smoke.summary.log" `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -RequireMarker "<exact 21-marker chain>" `
  -RequireOrderedMarker "<exact 21-marker chain>" `
  -RequireUniqueMarker "<exact 21-marker chain>" `
  -ForbidMarker "<exact nine-marker list>"
```

It ran from `2026-07-27T21:21:20.2271722Z` through
`2026-07-27T21:21:43.5137043Z` and exited `0`.

## Evidence

The exact cumulative guest sequence was:

```text
TEST_START m01.stage1
M01_LOADED_IMAGE_CAPTURED
M01_STACK_SWITCH_STUB_READY
M01_STACK_ARENA_ALLOCATED
M01_STACK_GUARD_RESERVED
M01_STAGE1_MAP_RECAPTURED
M01_STAGE1_RESERVATIONS_VALIDATED
M01_OWNED_STACK_READY
M01_OWNED_STACK_SWITCHED
M01_RSP_CONTAINED
TEST_PASS m01.stage1
TEST_START m01.stage2
M01_ALLOCATOR_ARENA_ALLOCATED
M01_STAGE2_MAP_RECAPTURED
M01_RESERVATIONS_APPLIED
M01_LIVE_ALLOCATED_4K
M01_LIVE_PAGE_WITHIN_ARENA
M01_LIVE_FREED_4K
M01_DOUBLE_FREE_REJECTED
TEST_PASS m01.stage2
OS_SMOKE_PASS
```

The nine forbidden markers were:

```text
LIVE_ALLOCATOR_NOT_INSTALLED
BOOT_PAGE_ALLOCATOR_NOT_READY
M01_ARBITRARY_CONVENTIONAL_PAGE_USED
M01_PAGE_OUTSIDE_ALLOCATOR_ARENA
TEST_FAIL
OS_SMOKE_FAIL
M01_INTEGRATION_FAIL
M01_STAGE1_FAIL
M01_STAGE2_FAIL
```

The runner reported:

```text
SERIAL_MARKER_STREAM status=Passed terminal=Success bytes=1511 lines=43 invalidBytes=15 invalidLines=1 orderedSeen=21 outOfOrder=0 uniqueSeen=21 duplicateUnique=0
QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1
QEMU_SMOKE_STATUS pass
MISSING_MARKERS none
OUT_OF_ORDER_MARKERS none
DUPLICATE_UNIQUE_MARKERS none
FORBIDDEN_MARKER_HIT none
FAIL_REASON none
TEST_PASS qemu.smoke
```

The invalid-byte/line count was bounded firmware noise and contained no
accepted marker. The negative process exit is the expected result after the
runner had accepted the complete transcript, drained serial for 750 ms, and
terminated the still-running nonreturning opt-in guest.

The EFI verifier reported:

```text
EFI_ARTIFACT_PE format=PE32+ machine=0x8664 subsystem=10 entryRva=0x0001e930 entrySection=.managed
EFI_ARTIFACT_IMPORTS import=absent bound-import=absent iat=absent delay-import=absent
EFI_ARTIFACT_ENTRY_CONTRACT symbol=EfiMain managedSymbol=OS_OS_Boot_EfiEntry__EfiMain association=same-build-linker-map rva=0x0001e930
TEST_PASS efi.artifact.verify
TEST_PASS efi.shell.publish
```

The accepted tool tuple was:

- .NET SDK `10.0.302`;
- MSVC linker `14.51.36252.0`;
- Windows SDK `10.0.28000.0`;
- ILCompiler `10.0.8` (file `10.0.826.23019`);
- NativeAOT runtime pack `10.0.8`;
- QEMU/qemu-img `11.0.50`
  (`v11.0.0-12631-g54e84cdc7a`); and
- OVMF length 3,653,632 bytes, SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

The ignored evidence root is
`artifacts/logs/m01-024/f7437521799e-20260727T210930Z/`.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `BOOTX64.EFI` | 208,384 | `ED1FDD62FF50ABE9AB30BBFAC85EBB7C095D3F31720C2571F31C5413726EFA13` |
| `BOOTX64.EFI.link.json` | 1,817 | `C3E20E72FAF49C804080E706DA77A91C69B64B24DD89DE991EBCA7CF6AC9F46E` |
| `BOOTX64.EFI.link.map` | 93,728 | `CBFBCA6826373C335B5715A7234D36C9837D254257CD1AFA783F94DABA1F765D` |
| `BOOTX64.EFI.link.rsp` | 217 | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| `clros.raw` | 67,108,864 | `51D00F4B3302461C0FFF2466BAA26A7909C891B9E5434A3B2CE78374D9F6FF4B` |
| `serial.log` | 1,511 | `2C74EE4CC10475D85EAA090A1382BEF08F9C114FD213A5AEB6E487F4405BD1EC` |
| `qemu-smoke.summary.log` | 4,495 | `19925845684DBBEA93962C762B2653BFA2224BBFF9707B3E4C38D882F6E46FF5` |
| `qemu-smoke.stdout.log` | 17,806 | `BC7D34774F8029BABC713E3AB19B0615E5BF9C896A2072965F53B74BDC76739C` |
| `efi-shell-publish.log` | 2,968 | `61FF1F02A57D560A557EF64783B3C80170FCB66055B4B0736D3FF192EC7FA612` |
| `host-tests.stdout.log` | 822,018 | `BF1ED174F13520996D6ECF44F2AD573D78FEADF1E8B2338F4FB9B2B35BD6B58C` |
| `evidence-manifest.json` | 13,621 | `9FDB9B72EB708B2A1184725E009A168FBD0BC3FEA3B8FCB0EA93C7F416031A58` |

The manifest:

- independently derived source commit
  `f7437521799e9fe974a9de4560db248f94addf6b`;
- verified `source.dirty=false`;
- recorded exit code `0` and classification `pass`;
- required 23 serial markers (the explicit 21 plus the runner's two
  additional baseline markers);
- rejected all nine forbidden markers;
- derived 40 observed markers from the hashed serial bytes; and
- hashed 26 evidence artifacts and tool inputs.

An independent read-only review found no remaining issue-contract defect in
the runner/test implementation. A second read-only review verified parity
between all three 21-marker lists, the nine forbidden markers, wrapper and
command paths, tool versions, PowerShell parsing, clean source state, and the
manifest's fail-closed preconditions.

## Failures

- The first development harness attempt exposed an existing
  line-ending-sensitive M01-023 source test because the unchanged
  `EfiEntry.cs` checkout had CRLF working-tree bytes. The file was normalized
  mechanically to LF without changing its Git content; it is absent from the
  final diff. The authoritative clean-source harness then passed.
- Review found synthetic stage-two failure transcripts whose field schema did
  not exactly match the production adapter and a mistaken adapter-14/15
  boundary. The focused tests were corrected before the implementation commit,
  and independent review found no remaining issue.
- Before manifest generation, independent wrapper review found that the local
  evidence script hashed exit-code files but did not fail closed on their
  contents. The ignored wrapper was corrected to require QEMU exit `0`, host
  exit `0`, exactly one host/QEMU pass terminal, no `TEST_FAIL`, and all six
  passing summary invariants before hashing or invoking the manifest writer.
- The recorded command artifact initially used `powershell` while the wrapper
  invoked `powershell.exe`; this exactness mismatch was corrected before
  manifest generation.
- No authoritative real-QEMU attempt failed or was discarded. The first
  source-clean accepted attempt passed.

## Deferred work

- M01-025 owns the next default-off EFI adapter for separately allocated
  CR3/invalidation code, protection-probe code, and page-table/state storage.
  M01-026 owns the cumulative real-QEMU paging proof.
- The retained arena exists only in the successful opt-in pre-EBS state.
  M01-024 does not establish ownership after firmware exit or a general
  allocator API.
- No physical page content was read, written, zeroed, mapped, protected, or
  accessed.
- The marker stream does not carry raw arena/page addresses; its confinement
  claim is source-commit and verified-artifact bound.
- Guard-page enforcement, RW-to-RX transition, paging, CR3/TLB operations,
  page-zero absence, NX/W^X, owned COM1, descriptor/fault handling, PCI,
  storage, networking, application execution, hosted .NET, Kestrel, AWS,
  Azure, the M01 exit criterion, cloud readiness, and production readiness
  remain unproven.
- `docs/completion-gates.md` and `README.md` remain unchanged.
