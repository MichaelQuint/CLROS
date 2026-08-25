# Step 918 - M02-009 owned GDT/TSS terminal EFI integration

## Summary

Integrated the frozen, host-audited M02-009 C# descriptor-transition stream
into the default-off no-stdlib EFI application. The bounded pre-firmware-exit
profile now allocates disjoint code/data pages, validates the exact stream and
context, enters the stream only after `OS_SMOKE_PASS`, loads and reads back its
owned GDT/TSS, emits its terminal transcript through owned COM1, and remains
halted until the evidence runner terminates QEMU.

## Why

Managed issue M02-009/#49 required real QEMU/OVMF evidence in addition to its
four-path host-only leaf. Supplemental issue #475 owns the central EFI,
project, runner, documentation, test, and evidence-record files needed to cross
that boundary without weakening the leaf's exact ownership contract.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `README.md`
- `docs/efi-build.md`
- `docs/qemu.md`
- `done/step918.md`

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with zero warnings and zero errors at accepted source head
    `5f512951ee452dbefd8d7dc42a6b5e4b641c3f17`.
- Complete `OS.HostTests` execution at that same source head:
  - Started `2026-08-25T14:31:32.4627443Z` and ended
    `2026-08-25T15:06:57.6904660Z`.
  - Duration 2,125.225 seconds; process exit `0`.
  - 3,461 `TEST_START`, 3,462 `TEST_PASS`, zero `TEST_FAIL`, and exactly
    one terminal `TEST_PASS host.tests`.
  - Artifact-verifier section: 19 starts, 19 passes, zero failures.
  - M01-035 historical four-publication profile and separate terminal fifth
    publication profile both passed, including missing, inverted, and
    duplicated terminal-call mutations.
  - The formerly failing legacy dry-run case emitted
    `TEST_PASS qemu M01 raw-observation dry-run is inactive and side-effect free`.
  - M02-009 leaf and all six EFI-integration cases passed, including the
    executable fake-Boot-Services cleanup matrix and hostile fake-QEMU
    transcript matrix.
  - Log: `artifacts/logs/issue-475-m02-009-integration-hosttests-legacy-output-repair.log`;
    444,426 bytes; 6,977 lines; SHA-256
    `C988A0122B36729672ADF99172E8B4937BB4C0D7ED2B68A2EC9D739F81843F55`.
- Cumulative flagged `dotnet build .\src\OS\OS.csproj` through
  `EnableM02OwnedGdtTssTerminalProbe=true`
  - Passed with zero warnings and zero errors when every M01/M02 prerequisite
    was explicitly enabled.
- Focused source-linked M02-009 EFI integration harness
  - Passed all six groups after the final wrapper compatibility repair.
- Full isolated `QemuRunnerTests` module
  - Passed every runner, wrapper, preset, raw-observation, ordering,
    uniqueness, timeout, reset, summary, and hostile-transcript case.
- `scripts/check-naming.ps1`
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- PowerShell AST parse for `scripts/build-efi-shell.ps1`,
  `scripts/test-efi-artifact.ps1`, and `scripts/run-qemu-smoke.ps1`
  - Zero parse errors.
- Default `scripts/run-qemu-smoke.ps1 -DryRun`
  - `TEST_PASS qemu.runner.dryrun` with the historical default marker lists
    and no `terminalProfile=false` output drift.
- Fresh cumulative QEMU/OVMF command at accepted source head `5f512951...`
  with `-BuildEfi` and every M01/M02 prerequisite through
  `-EnableM02OwnedGdtTssTerminalProbe`
  - `TEST_PASS efi.artifact.verify`
  - `TEST_PASS efi.shell.publish`
  - `TEST_PASS imagebuilder.raw`
  - `TEST_PASS qemu.smoke`
- `git diff --check 37254d1fbb67432a523ae89ca66432b1c9fc2f3f..HEAD`
  - Passed after this record-only reconciliation removed its provisional EOF
    whitespace defect.

## Evidence

- Supplemental regression guard #480 merged through PR #481 as
  `12636e3c8f6f53fc1565df3441de3b86e156d61a` after official exact-head review
  #25 and a 3,452-start/3,453-pass/zero-failure HostTests run.
- The frozen four-path leaf merged through PR #482 as
  `37254d1fbb67432a523ae89ca66432b1c9fc2f3f` after official exact-head review
  #26. Its stable patch ID remained
  `46800d5eba873ed0a23e84373519a4e2906228c2`, and all four merged blobs match
  the reviewed head.
- The accepted implementation/evidence source head was
  `5f512951ee452dbefd8d7dc42a6b5e4b641c3f17`, tree
  `7fcd5322b121549a60f086381469ea3f802f7070`. This step reconciliation changes
  only this evidence record after that tested source identity.
- Independent implementation review bound the exact 22-path source diff,
  immutable leaf blobs, default-off profile, allocation/cleanup behavior,
  terminal entry, runner disposition, and marker contract. A final exact-head
  review is required after this record-only commit.
- The host leaf emitted exactly:

  `CLROS_M02_009_HOST_READY contextBytes=168 stubBytes=2987 fnv1a64=C9C04DEE967D640A sha256=424C98FE039EB46B66EC8C3D8BB954EC7BBB24E6434817079F9B2E17478B8504 guestExecution=false lgdt=false ltr=false ownedCom1=false terminalHalt=false`
- The real runner contract contained exactly 59 required, 59 ordered, and 59
  unique records. All three lists were distinct and sequence-identical. The 43
  forbidden records were distinct and absent.
- Serial evidence contained exactly one `TEST_START m02.009`, one exact
  `CLROS_M02_009_READY ... postExitBootServices=false`, and one
  `TEST_PASS m02.009`, with zero `TEST_FAIL` records.
- The streaming runner reported
  `SERIAL_MARKER_STREAM status=Passed terminal=Success`, `orderedSeen=59`,
  `outOfOrder=0`, `uniqueSeen=59`, and `duplicateUnique=0`, followed by
  `QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1`.
- QEMU: `11.0.50 (v11.0.0-12631-g54e84cdc7a)`.
- OVMF: 3,653,632 bytes; SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
- EFI: 529,408 bytes; SHA-256
  `A22F5B2AF24682693C09E361C16EE89271CDB9D71FA5D9BBFA32ABA784F8D443`.
- Serializer object: 336 bytes; SHA-256
  `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.
- Link map: 238,945 bytes; SHA-256
  `2176E53E8A7BB1B367B536F50B608EBA83BA848F2237BB275CF8D01CA3EC2CEE`.
- Link response: 514 bytes; SHA-256
  `5DBCFE344726263BE25D2C32F0AC2C1621597DB24D8AFB3DE00C4E53DF4DA343`.
- Link-evidence JSON: 397,930 bytes; SHA-256
  `1481CCA8FCD9FDA48C3F92D8A8CDB851E05935AAE36A69B3862314A098D254AE`.
- Raw image: 67,108,864 bytes; SHA-256
  `BEB0B3B372BB33957690C1266312B36BF44B3F65C8F73EDD704977A68D8CA2A9`.
- Serial log: 4,244 bytes; 81 lines; SHA-256
  `3AAF960E6ABCB7F27A4580BF8B8D554FDB66EA165F71816C38C7B81A050B6BAF`.
- Summary log: 16,913 bytes; 103 lines; SHA-256
  `2C252C14642C9943D00E4492B80F81C22FB98F8233D6EF071E39892DA6694B32`.
- QEMU stdout: 28,590 bytes; 116 lines; SHA-256
  `0490E16DC622A8A0CD51CEC29160BC08B0EBE9CCCD9D500C498B51DA548ED4DE`.
- QEMU stderr: zero bytes; SHA-256
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Summary status was `pass` with `MISSING_MARKERS none`,
  `OUT_OF_ORDER_MARKERS none`, `DUPLICATE_UNIQUE_MARKERS none`,
  `FORBIDDEN_MARKER_HIT none`, and `FAIL_REASON none`.

## Failures

- The first complete M02-009 leaf run was rejected after M02-008's old
  whole-file documentation hashes produced two failures. Issue #480 replaced
  only those mutable-doc hashes with fail-closed semantic guards; no failed
  run was promoted to M02-009 evidence.
- Preliminary review of the original stacked integration found three defects:
  the runner inherited 11 extra presence-only required markers, aliased
  allocation cleanup could double-free, and durable executable hostile runner
  coverage was missing. The repair added an explicit default-off replacement
  option, exact cleanup planning, and hostile transcript/process fixtures.
- One diagnostic direct OS build omitted the M01 prerequisite properties and
  correctly failed with
  `EnableM02SafeStubProbe requires EnableM01LoadedImageStackProbe=true`. The
  cumulative flagged build with all prerequisites passed.
- Two issue-unique QEMU setup attempts were rejected before acceptance:
  - A nested EFI output directory violated the artifact authority root and
    failed with `authority_initialization_failed`.
  - Legacy Windows PowerShell failed to resolve `Get-FileHash` before EFI
    publication. The accepted commands used PowerShell 7 and authorized
    issue-unique names directly under the established artifact directories.
- The first full integration HostTests attempt at `6115f864...` was rejected:
  662 starts, 654 passes, eight failures, and no terminal host pass. All eight
  were artifact-verifier fixtures missing the new default-false MSBuild
  property. Log SHA-256:
  `BD9313919416352129CC5323D57952FF93BF3F2544AC2D5D336FC8EDAE9E16E3`.
- The second attempt at `939c39bc...` repaired all 19 artifact cases but was
  rejected at M01-035: 1,188 starts, 1,186 passes, three failure records, and
  no terminal host pass. The profile selector had left the new M02 conditional
  active inside the historical four-publication profile. Log SHA-256:
  `C577D2421B290985BEF2CD2967289AC3D98419C46D501AA2BE0EE12BB257A4BA`.
- The third attempt at `70541bce...` passed M01-035 and all M02-009 tests but
  was rejected later by one legacy QEMU-wrapper assertion: 2,380 starts, 2,379
  passes, two aggregate failure records, and no terminal host pass. Printing
  `terminalProfile=false` had changed the historical raw-observation dry-run
  line. The final repair prints that field only for the terminal profile and
  preserves the legacy output byte-for-behavior. Log SHA-256:
  `84AA5D37CDE5084801F61EF1F4CDFC79504EF6DD23CE197C5075538F3DAE64C3`.
- None of the rejected host or QEMU attempts is accepted evidence. The hashes
  in the Evidence section come only from the final source head and passing
  executions.

## Deferred work

- Install an IDT and wire live IST-backed fault entries.
- Make guard pages non-present and enforce executable/write permissions with an
  owned page-table policy.
- Execute a controlled double-fault, page-fault, or NMI probe with bounded
  register capture and panic routing.
- Complete real `ExitBootServices` and prove owned diagnostics after firmware
  services are unavailable.
- Continue the runtime, scheduler, storage, networking, app-model, hosted .NET
  10, and Kestrel prerequisites required for the NativeAOT Minimal API demo.

## Evidence boundary

This step proves only bounded pre-`ExitBootServices` behavior on the recorded
QEMU/OVMF platform: current C# source generated the audited stream, temporary
UEFI code/data pages were prepared, GDTR/TR installation and readback matched
the exact private GDT/TSS contract, terminal records were emitted through the
owned COM1 transport, and the intentionally halted guest was terminated by the
runner. It does not prove page permission enforcement, W^X/NX, an installed
IDT, active IST routing, a triggered or handled CPU fault, real successful
`ExitBootServices`, post-firmware execution, hosted .NET, Kestrel,
cloud-provider support, CloudReady status, security hardening, or production
readiness.
