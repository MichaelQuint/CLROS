# Step 136 - App Executable Memory Readiness Boundary

## What Changed

- Added `BootSequence.RunAppExecutableMemoryReadiness`.
- Added stable readiness markers:
  - `APP_EXEC_MEMORY_READY`
  - `APP_EXEC_MEMORY_NOT_READY status=<status> mapping=<mapping-status> descriptor=<descriptor-status> payload=<payload-status>`
- Extended `NativeAppExecutableMemoryPlanResult` to preserve nested payload mapping, launch descriptor, and payload header statuses for readiness markers.
- Added host tests for the ready marker, status-coded failure marker, and proof that the default boot marker sequence is unchanged.
- Updated boot, boot-order, app-model, and completion-gate docs with the host-test-only evidence boundary.

## Why It Changed

Gitea issue #154 needed the next ordered boot boundary after native payload range planning and executable-memory permission planning. This keeps native app launch work routed through `BootSequence` without prematurely allocating executable pages, changing page protections, copying payload bytes, relocating code, or jumping to app code.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Runtime/NativeAppExecutableMemoryPlan.cs`
- `tests/OS.HostTests/BootSequenceAppExecutableMemoryTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step136.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS boot.sequence app executable memory ready`.
- Host tests emitted `TEST_PASS boot.sequence app executable memory rejects plan failure`.
- Host tests emitted `TEST_PASS boot.sequence app executable memory preserves default markers`.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.

## Failures

- The full host-test process later failed in existing ImageBuilder/qcow2 tests after the #154 BootSequence tests passed. The observed failures were `imagebuilder rejects missing package`, `imagebuilder rejects empty package`, `imagebuilder rejects oversized package`, `imagebuilder emits dry-run qcow2 conversion without qemu-img`, `imagebuilder rejects missing qcow2 input`, and `imagebuilder rejects empty qcow2 input`.
- The `OS.HostTests.exe` process and `OS.ImageBuilder` children from that host-test run continued after the harness failure; those exact verification processes were stopped during cleanup. A follow-up process scan found no remaining OS-scoped verification processes from this run.
- QEMU dry-run was not run after the user requested stopping long-running verification and keeping verification focused.

## Deferred Work

- No executable page allocation was added.
- No live W^X/NX page permission transition was added.
- No payload byte copy, instruction-cache flush, relocation, or app entry invocation was added.
- The marker is not emitted by the no-stdlib EFI artifact or QEMU boot path yet.
- No hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.

## Evidence Boundary

This step is a managed host-tested boot-sequencing boundary only. It proves that selected native app executable-memory planning status can be converted into stable boot markers without altering the default boot order. It does not prove QEMU boot-time package loading, executable page allocation, live W^X/NX enforcement, payload copying, relocation, native-tier app execution, hosted .NET 10 startup, ASP.NET Minimal API execution, or Kestrel support.
