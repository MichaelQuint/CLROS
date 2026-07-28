# Step 883 - Repair M01-033 ownership before implementation

## Summary

- Corrected the durable M01-033 contract before claiming issue `#411`.
- Added every repository path that must legitimately change when the fifth host
  tool is introduced: the recursive tool-inventory test, the tool-local agent
  guide, and the QEMU/libvirt documentation that cites the inventory marker.
- Required the focused test to clean-build and invoke the standalone generator
  through a bounded subprocess, preserving the prohibition on solution and
  HostTests project-graph integration in this leaf.
- Updated the manifest's exact-ownership regression test.

## Why

M01-033 requires
`tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj`,
but the current host harness recursively enumerates `tools/**/*.csproj` and
asserts exactly four projects. The issue did not own that test, while its
acceptance criteria permitted edits only to owned paths. Implementing the issue
as written would therefore either violate ownership or fail the complete host
gate.

The same addition makes `tools/AGENTS.override.md` and two canonical marker
references stale. Correcting the contract first keeps collision control honest
and prevents implementation work from hiding an unrelated integration edit.

## Files changed

- `.gitea/backlog/clros-issues.json`
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
- `done/step883.md`

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Exit code 0.
  - 3,270 `TEST_START` markers.
  - 3,271 `TEST_PASS` markers.
  - 0 `TEST_FAIL` markers.
  - Exactly one `TEST_PASS host.tests`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -SelfTest`
  - Passed the 16-milestone, 68-label, 16-epic, 324-child, 340-key manifest,
    managed-body, and state-policy checks.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -Offline`
  - Passed with all 424 managed objects skipped in manifest-only mode and zero
    failed, create, or update actions.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - Passed `naming.guard` and `source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - Passed the default `SingleMinimalApi` marker contract.
- `git diff --check`
  - Passed.

## Evidence

- Base source state:
  `3fdec0f33dfa5a1be4482f697a4b1504436d6f79`.
- Live read-only tracker verification before the repair found M01-033 `#411`
  open, unassigned, and the sole `agent:ready` issue.
- Three independent read-only reviews reproduced the same deterministic
  failure: adding the required generator project makes
  `HostToolProjectBoundaryTests` fail unless that test is an authorized edit.
- The corrected owned-path contract now includes:
  - `tests/OS.HostTests/HostToolProjectBoundaryTests.cs`
  - `tools/AGENTS.override.md`
  - `docs/qemu.md`
  - `docs/libvirt.md`
- The manifest explicitly requires a clean standalone generator build from the
  focused test and forbids a HostTests project reference or transitive
  `CLROS.slnx` dependency in M01-033.
- No implementation, generator project, COFF object, native source, solution
  integration, EFI integration, or tracker claim was created by this repair.

## Failures

- The first repair draft added only
  `tests/OS.HostTests/HostToolProjectBoundaryTests.cs`. Parallel review then
  identified the stale tool-local guide and QEMU/libvirt marker references.
  They were added before commit, push, tracker synchronization, or issue claim.
- No build, host-test, manifest, naming, source-firewall, dry-run, or diff
  verification failed.

## Deferred work

- Merge this administrative contract repair and synchronize issue `#411`.
- Claim M01-033 only after the live issue contains the corrected ownership.
- Implement and independently parse the deterministic AMD64 COFF serializer in
  the corrected leaf.
- M01-034 remains responsible for solution/link integration and final-image
  relocation/unwind verification.
- M01-035 remains responsible for the live instruction-publication call.
- This step proves no serializer bytes, object hash, linker behavior, EFI
  behavior, QEMU execution, live paging behavior, or completion-gate advance.
