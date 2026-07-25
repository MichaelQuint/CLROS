# Step 178 - Done Step Allocator

## Summary

Added a deterministic PowerShell helper for allocating the next `done/stepNNN.md` writeup and optional reservation template during parallel-agent work.

## Why

Parallel workers had already produced overlapping done-step ranges. A repo-local allocator gives agents a machine-readable way to discover or reserve the next writeup path before they start documenting evidence.

## Files changed

- `scripts/new-done-step.ps1`
- `tests/OS.HostTests/DoneStepAllocatorScriptTests.cs`
- `AGENTS.md`
- `docs/repo-audit.md`
- `done/step178.md`

## Tests run

- `powershell -ExecutionPolicy Bypass -File .\scripts\new-done-step.ps1`
- direct reserve test against a temporary `artifacts/logs/.../done` fixture
- direct overwrite-refusal test against a temporary `artifacts/logs/.../done` fixture
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Script dry-run emitted `TEST_START done.step.allocate`, `NEXT_DONE_STEP done/step179.md`, and `TEST_PASS done.step.allocate` after this writeup was added.
- Direct reserve test emitted `NEXT_DONE_STEP done/step002.md`, `TEST_PASS done.step.allocate`, and confirmed `RESERVED_FILE_PRESENT`.
- Direct overwrite-refusal test emitted `TEST_FAIL done.step.allocate target_exists path=done/step002.md` and exited nonzero as expected.
- `dotnet build .\OS.slnx --nologo -m:1` completed with 0 warnings and 0 errors after the concurrent networking compile blocker was resolved.
- Host tests emitted `TEST_PASS done step allocator reports next step without mutating done`, `TEST_PASS done step allocator reserves template`, `TEST_PASS done step allocator refuses existing target`, and final `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.
- Host-test coverage was added for dry-run allocation, reservation template creation, and refusal to reuse an existing target path.

## Failures

- An initial `dotnet build .\OS.slnx --nologo -m:1` attempt failed in `tests/OS.HostTests/Ipv4NeighborRoutePlannerTests.cs` with `CS8352` and `CS8350` span lifetime errors from a concurrent networking slice. A later build retry passed after that blocker was resolved.

## Deferred work

- Consider adopting `scripts/new-done-step.ps1 -Reserve` in future issue templates so workers reserve writeup paths before implementation begins.
