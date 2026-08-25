# Step 916 - Restore race-stable host firmware evidence

## Summary

Restored the canonical host-test baseline by making OVMF validation and
evidence collection one immutable, single-open operation. The same bounded
stream now supplies the firmware length, `_FVH` signature probe, and SHA-256,
and the resulting evidence object is reused by selection and final marker
emission. The M01 raw-observation verifier now uses the same module-independent
.NET stream-hashing pattern instead of PowerShell `Get-FileHash`.

The repair also reconciles two stale M02-008 documentation hashes that were
proven to predate later accepted documentation changes. The final complete
host harness is green with 3,452 starts, 3,453 passes, zero failures, and one
terminal `TEST_PASS host.tests`.

## Why

The real installed OVMF image passed production preflight, but valid 65,536-byte
synthetic images could be validated and then independently reopened for hash
evidence, producing `EFI_PREFLIGHT_OVMF evidence_unavailable`. That made the
canonical host baseline fail for a host-tool race rather than an operating
system defect.

Clearing that first failure exposed two independent historical baseline defects:
the M02-008 integration fixture still expected hashes from before its canonical
docs changed, and the QEMU raw-observation verifier still depended on Windows
PowerShell module autoload for `Get-FileHash`. Issue #474 explicitly expanded
to each newly reachable, evidence-proven host-only repair before those paths
were changed.

## Files changed

- `scripts/build-efi-shell.ps1`: replaces split OVMF validation/hash reads with
  `Get-OvmfCodeImageEvidence`, one read-only stream, an invocation-local
  immutable evidence cache, and distinct `missing`, `invalid`,
  `evidence_unavailable`, and `ok` states.
- `scripts/run-qemu-smoke.ps1`: hashes raw-observation files through one
  `FileStream` and one `SHA256` instance with deterministic nested disposal,
  preserving uppercase hexadecimal output without module autoload.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`: adds malformed,
  path-with-spaces, poisoned-module, concurrent-reader, unavailable-evidence,
  and repeated-handle-release cases while preserving existing selection and
  source-lock contracts.
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`: updates only
  the two stale canonical documentation SHA-256 literals.
- `tests/OS.HostTests/QemuRunnerTests.cs`: source-binds the stream hash helper
  and executes the real extracted verifier under a same-session throwing
  `Get-FileHash` shadow.
- `done/step916.md`: records this recovery and its bounded evidence.

## Tests run

- Focused authoritative OVMF preflight module: 40 `TEST_START`, 41
  `TEST_PASS`, zero `TEST_FAIL`; all new success/failure and handle-lifetime
  cases pass.
- Focused QEMU runner module: 155 `TEST_START`, 155 `TEST_PASS`, zero
  `TEST_FAIL`; the complete-valid-sidecar and serial-hash-mismatch verifier
  cases both pass under the throwing hash-command shadow.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`:
  exit 0 after 2,326.642 seconds with 3,452 `TEST_START`, 3,453
  `TEST_PASS`, zero `TEST_FAIL`, and exactly one terminal
  `TEST_PASS host.tests`.
- `dotnet build .\CLROS.slnx --nologo -m:1`: pass in 8.57 seconds with zero
  warnings and zero errors.
- Real installed OVMF preflight through `scripts/build-efi-shell.ps1` with
  `-PreflightOnly`: `EFI_PREFLIGHT_OK`.
- PowerShell AST parsing for both changed scripts: zero errors.
- `scripts/check-naming.ps1`: `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check`: pass.
- Independent read-only review of the exact five-file implementation diff:
  approve, with no blocking finding.

## Evidence

- Repair implementation commit:
  `2e25e3372d2d50bc280996a4c7bd017986833f58` (`Harden host firmware evidence
  collection`).
- Final complete host-test log: 442,997 bytes; SHA-256
  `C2352A10C1EDEE4D50B5F0DD3CBA733A10F4918BDB84D3F30D3BB748637CA55A`.
- Focused OVMF log SHA-256:
  `DC73E693CD5A5FDC89E96C60E5E0A49B65D5BF9CFCD217F29371FB0FAB4FDE85`.
- Focused QEMU log SHA-256:
  `AC55863E427890A06566D2A29A6010F5AF8471D8AD47CBC6BFD816CF53320BBC`.
- Final real-OVMF preflight log: 1,888 bytes; SHA-256
  `5DE8A1F4C79CBC6619D6C6C65B87777F368B652492172063EC1AE62501C584BE`.
- Installed OVMF: `C:\Program Files\qemu\share\edk2-x86_64-code.fd`;
  3,653,632 bytes; SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
- Canonical documentation hashes independently recomputed by the review:
  `docs/diagnostics.md` =
  `bc1a9ffddfe8f71eaf6b729609366efffed7693b92e8851e784624470bfce76a`;
  `docs/boot-order.md` =
  `16d88fac99cbb273a4426841cb55e541ad9a2a4ffe151cf435cc02bedd59beac`.
- Exact issue-owned implementation paths: five; no nonignored untracked file,
  forbidden first-party source change, or generated artifact was committed.

## Failures

- The initial canonical run stopped after 434 starts with 408 passes and 26
  failure records; log SHA-256
  `124060505547D388E750FCBDA192109E285C29B940E1371CA5BD7C17E8DC832B`.
  Valid temporary OVMF fixtures were being reopened after validation and
  reported as unavailable, with dependent preflight cases cascading.
- The first full run after the OVMF repair progressed to 1,340 starts and
  exposed two failure records from stale M02-008 documentation hashes; log
  SHA-256
  `FF2331119B103BD9BA4B311F61E33F5E75A5388E1310D1836D20AE7CA5B093E3`.
  Git history proved the fixture predated accepted documentation commit
  `3aa6671`; only the two exact literals were refreshed.
- The next full run progressed to 2,371 starts and exposed the complete-valid
  and serial-hash-mismatch raw-observation verifier cases plus their aggregate
  failure; log SHA-256
  `BAA885E5C516BC5574D4ABA32866DEAC048712F90C7CA8579939D6E61444C574`.
  Both reached the shared `Get-M01FileSha256` helper, whose sole implementation
  was the module-autoloaded `Get-FileHash` command.
- A final-check wrapper attempted to hash an optional console Tee path that it
  had not created. This emitted a harmless file-not-found after the production
  preflight had already succeeded. Only the canonical script-owned 1,888-byte
  preflight log is retained as evidence.
- Each newly reachable failure was diagnosed from source/history, added to
  issue #474 ownership, repaired narrowly, focused-tested, and followed by a
  fresh complete run from the beginning. Only the final zero-failure run is
  accepted.

## Deferred work

- This is host-side firmware-file and sidecar evidence reliability only. It
  does not boot the validated OVMF file, execute a guest, or prove any EFI or
  kernel behavior.
- It does not prove real `ExitBootServices`, live paging, storage, networking,
  hosted .NET, Minimal API/Kestrel, CloudReady/provider support, or production
  readiness.
- OVMF evidence is immutable only for one preflight invocation. Artifact/image
  construction and guest execution retain their separate source, artifact,
  and runtime evidence gates.
