# Step 880 - M01-025 reversible owned-CR3 stage-three source wiring

## Summary

Completed the M01-025 source/build boundary for the default-off, cumulative
stage-three owned-paging probe.

`EfiEntry` now consumes the reviewed M01-029 through M01-032 leaf contracts and
wires the complete pre-EBS source path:

- Requires the stage-one stack and stage-two allocator prerequisites.
- Allocates separate EFI LoaderCode backing for the composite/call gate and
  protection probe.
- Selects and allocates the exact EFI LoaderData table/state arena at a fixed
  address.
- Recaptures the firmware memory map and updates the reservation ledger.
- Materializes and reads back the physical page-table arena and audited
  generated-byte plans.
- Performs bounded architectural, binding, one-shot, callback-exclusion,
  marker-topology, and post-return observation checks.
- Routes the accepted path through the reviewed call gate while leaving the
  feature disabled by default.
- Treats every `EFI_SUCCESS` allocation as owned immediately, even if later
  CLROS validation rejects the returned address.
- Releases rejected or partially initialized allocations in reverse
  table/probe/code order and continues later release attempts when an earlier
  `FreePages` call fails.

The complete host harness and a fresh all-three-switch NativeAOT EFI publish
passed. This is source/build/artifact evidence only. No generated stage-three
byte, firmware allocation callback, page-table switch, or CR3 transition was
executed by this step.

## Why

M01-024 preserved the last cumulative real-QEMU evidence, but its guest did not
contain a live stage-three adapter. M01-025 is the serialized integration slice
that connects the separately reviewed planning, binding, physical-arena, and
call-gate contracts without duplicating their paging logic in `EfiEntry`.

The parked implementation commit
`4b42f11dd5a8cb30d91a58f88a2f0cbb17bdbe0e` could not be promoted when the
complete harness exposed an unrelated M01-021 mutation-selector scope defect.
After PRs `#407` and `#408` repaired and reclosed that dependency chain, current
`master` was merged into the M01-025 branch and the complete M01-025 gate was
rerun.

Independent review of the resumed source then found a real pre-entry cleanup
defect: allocation ownership flags were set only after CLROS geometry and
overlap validation. If firmware returned `EFI_SUCCESS` with an invalid address,
the adapter correctly rejected the address but did not regard the allocation
as owned, so cleanup could leak it. The final source assigns each ownership flag
directly from the EFI status before validating the returned address. The
rejected-versus-failed status and every cleanup decision now use that
authoritative state.

The review also distinguished ordinary same-processor self-modifying-code
control-transfer behavior from stronger immutable-image-text instruction
publication serialization. The first transfer into a newly written preflight
stream is not preceded by a serializer resident in immutable EFI image text.
M01-025 therefore records that limitation explicitly and does not claim Intel
Speculative Code Store Bypass hardening.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
  - Adds the cumulative stage-three adapter and its guarded default-off
    invocation path.
  - Performs exactly three checked EFI allocations and validates their types,
    geometry, non-overlap, ownership, and fixed-address table-arena result.
  - Recaptures the firmware memory map and reservation ledger before table
    construction.
  - Consumes the reviewed preparation, live binding, table arena, composite,
    call-gate, protection-probe, and post-return observer contracts.
  - Carries authoritative allocation ownership through all 21 pre-entry
    terminal paths.
  - Attempts cleanup in table, probe, code order without suppressing later
    attempts after a release failure.
  - Documents the immutable-text first-publication serialization limitation.
- `tests/OS.HostTests/M01LivePagingEfiEntryTests.cs`
  - Adds the 16-check M01-025 source/build contract suite.
  - Verifies exact allocation, map, arena, byte-publication, gate, callback,
    marker, one-shot, security-cookie, and default-off source topology.
  - Verifies all 21 `FailBeforeEntry` sites carry the exact address, count, and
    status-derived ownership arguments.
  - Adds a 16-scenario cleanup oracle evaluated under all eight possible
    table/probe/code release-failure masks, for 128 host model evaluations.
  - Adds representative source mutations for allocation ownership, status
    selection, cleanup order/counts, early cleanup return, and one-shot state.
  - Revalidates the M01-029 through M01-032 leaf contracts.
- `docs/boot.md`
  - Records the implemented M01-025 source path, evidence, limitations, and
    next prerequisite.
- `docs/efi-build.md`
  - Records the all-three-switch build/artifact evidence and its non-guest
    boundary.
- `done/step880.md`
  - Preserves the implementation, review correction, validation, failures,
    hashes, and deferred boundary.

No solution, project, kernel, standard-library, application, QEMU runner,
completion-gate, README, reference, or historical done-step source changed in
the implementation pull request.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Exit code: 0.
  - Zero warnings, zero errors, and zero stderr bytes.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Exit code: 0.
  - Zero warnings, zero errors, and zero stderr bytes.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Native exit code: 0.
  - Zero stderr bytes.
  - Exactly one `TEST_START m01.025`.
  - Each of the 16 named M01-025 check start/pass pairs occurred exactly once.
  - Exactly one `CLROS_M01_025_READY`.
  - Exactly one terminal `TEST_PASS m01.025`.
  - Exactly one final `TEST_PASS host.tests`.
  - Zero `TEST_FAIL` marker lines.
  - Zero `M01_021_DIAGNOSTIC`,
    `M01_019_BINDING_DIAGNOSTIC`,
    `M01_019_NOSTDLIB_COMPILE_DIAGNOSTIC`,
    `M01_030_PREPARATION_DIAGNOSTIC`, and
    `M01_031_ARENA_DIAGNOSTIC` occurrences.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe
  -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe`
  - Exit code: 0.
  - Zero stderr bytes.
  - Verified all three M01 switches were enabled in the same build.
  - Passed `efi.artifact.verify` and `efi.shell.publish`.
  - Verified PE32+ x64, EFI application subsystem 10, a file-backed
    `.managed` entrypoint, same-build `EfiMain`, and absent ordinary, bound,
    IAT, and delay imports.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - Exit code: 0.
  - Zero stderr bytes.
  - Passed `naming.guard` and `source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - Exit code: 0.
  - Zero stderr bytes.
  - Passed `qemu.runner.dryrun`.
  - This validated command construction only and is not boot evidence.
- Focused M01-025 runner against the final frozen source:
  - Passed all 16 M01-025 checks.
  - Emitted `CLROS_M01_025_READY` and `TEST_PASS m01.025`.
  - This ignored diagnostic runner was not used as the closure gate.
- `git diff --check`
  - Passed on the final pull-request diff.

## Evidence

Final reviewed source snapshot before documentation:

- Branch base after dependency repair:
  `e3cb5941d0aed051050433d519ffcfa95ed8c89f`.
- `src/OS/Boot/EfiEntry.cs`:
  - Size: 357,642 bytes.
  - SHA-256:
    `7ABACD8A86CAB40962D7A39C89D6775EF6C81C4FC21A486CA9339A821240193F`.
- `tests/OS.HostTests/M01LivePagingEfiEntryTests.cs`:
  - Size: 133,899 bytes.
  - SHA-256:
    `978D1097560E7084978840AF07A004797BAE93445715628B058F386A6010ECE2`.
- Independent frozen-source review confirmed:
  - EFI-success ownership precedes validation for code, probe, and table
    allocations.
  - All 21 pre-entry failure calls carry the authoritative ownership state.
  - Reverse cleanup continues after any individual `FreePages` failure.
  - No remaining production blocker was found in allocation ownership or
    cleanup.
  - The source/control-flow oracle and mutation suite are sufficient for this
    issue's source/build boundary when described without claiming fake-EFI or
    live-firmware execution.

Authoritative final-tree full-host evidence:

- UTC interval:
  `2026-07-28T11:25:45.8776369Z` through
  `2026-07-28T11:34:24.2000179Z`.
- Local ignored stdout:
  `artifacts/evidence/m01-025/final-20260728T105630Z/host-harness-final-tree.stdout.log`.
- Stdout size: 834,334 bytes.
- Stdout SHA-256:
  `E9377EECB18F71A8EC329ACAA14F049757A4FD2887B76C85F6F3065B7BE24A1F`.
- Local ignored stderr:
  `artifacts/evidence/m01-025/final-20260728T105630Z/host-harness-final-tree.stderr.log`.
- Stderr size: 0 bytes.
- Stderr SHA-256:
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Recorded native exit code: 0.
- Final non-empty line: `TEST_PASS host.tests`.

Fresh all-three-switch EFI evidence:

- UTC interval:
  `2026-07-28T11:16:39.8903673Z` through
  `2026-07-28T11:16:45.3253012Z`.
- Build stdout SHA-256:
  `B76F099F11773CA0B4F4392F46452727F006371091B61CCB6B55A6C048AD5BBA`.
- Build stderr size: 0 bytes.
- `artifacts/efi/BOOTX64.EFI`:
  - Size: 456,192 bytes.
  - Entry RVA: `0x00049c50`.
  - SHA-256:
    `0A674D1628D48C478D616F43548DA18BC9673D4BECBEC0D875C7ECDA05F646CB`.
- `artifacts/efi/BOOTX64.EFI.link.map`:
  - Size: 200,000 bytes.
  - SHA-256:
    `B3A9772F07B579986DF4F3B726ADC1AB378FDE4F79E80B9B497DEAA166C11037`.
- `artifacts/efi/BOOTX64.EFI.link.rsp`:
  - Size: 217 bytes.
  - SHA-256:
    `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F`.
- `artifacts/efi/BOOTX64.EFI.link.json`:
  - Size: 1,817 bytes.
  - SHA-256:
    `405CF854F218423A0283F0DFCF1148FF7E7944F8AAC9F360FC2687A37884AA62`.
- Toolchain:
  - MSVC linker 14.51.36252.0, SHA-256
    `F233B8E337CEC96A69868A8CDE676808BFA81152493968D0B27B7CD0DAAC15BE`.
  - Windows SDK 10.0.28000.0.
  - ILCompiler package 10.0.8 / file 10.0.826.23019, SHA-256
    `DBCF62CC6C5240BC3441C9F6388CC670E2C113961BDBC8042E04080B5C296A95`.
  - NativeAOT runtime pack 10.0.8.
  - QEMU 11.0.50.
  - OVMF size 3,653,632 bytes, SHA-256
    `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

Additional final command logs are local ignored evidence under
`artifacts/evidence/m01-025/final-20260728T105630Z`. Generated EFI artifacts,
focused-runner scaffolding, and logs are not committed.

## Failures

- The parked implementation's first complete harness correctly failed in
  M01-021 because that test's old global mutation selector changed an earlier
  M01-025 token rather than the stage-one probe under test. M01-021 PR `#407`
  fixed the selector, PR `#408` reclosed the reviewed dependency chain, and the
  full M01-025 gate then passed.
- During resumed independent review, the first merged snapshot was rejected
  because successful-but-invalid firmware allocation outputs could be leaked.
  A partial full-host diagnostic run was terminated and was not used as
  evidence. The ownership and cleanup source was repaired, the focused suite
  passed, and a second independent frozen-source review accepted the fix before
  the authoritative full gate began.
- The first post-fix full harness emitted its final host pass with empty stderr,
  but the evidence wrapper used `Start-Process -Wait` and remained blocked
  after the child exited because of Windows descendant-process waiting
  behavior. That log was preserved as diagnostic-only. The unchanged,
  already-built harness was rerun by direct native invocation and recorded the
  authoritative exit code zero and hashes above.
- The first EFI-log census used a line-end-sensitive regular expression and
  displayed zero counts even though the required CRLF-terminated markers were
  visibly present. Exact line comparison then verified every required marker
  once.
- A later exploratory forbidden-text census matched the words `TEST_FAIL`
  inside two passing QEMU negative-test names. Exact terminal-prefix checking
  verified that there were zero actual `TEST_FAIL` marker lines.
- No failed diagnostic run, generated EFI artifact, or temporary focused
  runner is committed.

## Deferred work

M01-025 proves source/control-flow ordering, status-derived allocation
ownership, propagation through all 21 pre-entry failures, a 16-scenario by
eight-release-failure-mask cleanup oracle, reverse-cleanup source structure,
one-shot source state, mutation resistance, compatibility with the reviewed
M01-029 through M01-032 leaf contracts, and a fresh all-switch EFI artifact.

It does not execute an EFI callback, allocate or free a firmware page, invoke
generated stage-three bytes, change CR3, inspect live PTE/TLB behavior, enforce
live RW-to-RX, prove page zero absent, run real QEMU/OVMF, survive
`ExitBootServices`, close a completion gate, or prove cloud/production
readiness.

The current dynamically written streams also lack an architecturally
serializing publication boundary executed from immutable EFI image text before
their first fetch. A new P0 prerequisite chain will separate:

1. A deterministic C# AMD64 COFF executable-text serializer generator.
2. Canonical linker and final-artifact integration.
3. Exact `EfiEntry` publication-epoch call ordering.

Those issues must complete before M01-026 performs the next cumulative
real-QEMU run. QEMU compatibility evidence will not itself be described as
empirical proof that transient stale-byte execution is impossible on affected
physical processors. No completion-gate status changes in this step.
