# Step 309 - Plan-Only Libvirt Lifecycle Contract

## What Changed

- Added `tools/OS.LibvirtRunner plan-lifecycle` to emit a machine-readable, plan-only real-libvirt lifecycle contract.
- Added `scripts/run-libvirt-smoke.ps1 -PlanLifecycle` to write the normal domain XML artifact, write a lifecycle plan artifact, echo the `LIBVIRT_LIFECYCLE_*` contract, and exit without requiring `virsh`.
- Added `-AllowRealLibvirtLifecycle` as the future explicit opt-in guard in the smoke wrapper. The switch is recognized, but real lifecycle execution still stops at `TEST_FAIL libvirt.smoke.real_run_not_implemented`.
- Added host-test coverage for lifecycle plan order, marker propagation, timeout metadata, cleanup order, and script guard strings.
- Updated libvirt, cloud-import, and completion-gate documentation.

## Why It Changed

Issue #309 needed the next libvirt step to describe the future real boot lifecycle without accidentally turning default smoke paths into a real domain launch. The contract now captures the future order before implementation: generate XML, validate host tools and paths, connect to libvirt, create a transient domain from XML, start it, poll serial output for required and forbidden markers, enforce timeout, collect diagnostics, destroy, undefine, and clean transient artifacts.

## Files Changed

- `tools/OS.LibvirtRunner/Program.cs`
- `scripts/run-libvirt-smoke.ps1`
- `tests/OS.HostTests/LibvirtRunnerTests.cs`
- `docs/libvirt.md`
- `docs/cloud-import.md`
- `docs/completion-gates.md`
- `done/step309.md`

## Evidence

Focused lifecycle-plan tool command:

```powershell
dotnet run --project .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj -- plan-lifecycle --name os-plan --image artifacts/images/os.qcow2 --disk-format qcow2 --serial-log artifacts/logs/libvirt-plan.log --network default --timeout-seconds 45 --require-marker OS_SMOKE_PASS --require-marker BOOT_HANDOFF_READY --forbid-marker OS_SMOKE_FAIL
```

Observed markers:

```text
LIBVIRT_LIFECYCLE_MODE plan-only
LIBVIRT_LIFECYCLE_DEFAULT_START_DOMAIN false
LIBVIRT_LIFECYCLE_REAL_RUN_IMPLEMENTED false
LIBVIRT_LIFECYCLE_REQUIRED_OPT_IN_SWITCH --allow-real-libvirt-lifecycle
LIBVIRT_LIFECYCLE_REQUIRED_MARKERS OS_SMOKE_PASS;BOOT_HANDOFF_READY
LIBVIRT_LIFECYCLE_FORBIDDEN_MARKERS OS_SMOKE_FAIL
LIBVIRT_LIFECYCLE_STEP 04 create-transient-domain-from-xml
LIBVIRT_LIFECYCLE_STEP 05 start-domain
LIBVIRT_LIFECYCLE_STEP 06 poll-serial-log-for-required-markers
LIBVIRT_LIFECYCLE_STEP 10 destroy-domain-if-running
LIBVIRT_LIFECYCLE_STEP 11 undefine-domain-if-persistent
LIBVIRT_LIFECYCLE_PLAN_READY
```

Focused wrapper command:

```powershell
.\scripts\run-libvirt-smoke.ps1 -PlanLifecycle -Image artifacts/images/os.qcow2 -DiskFormat qcow2 -SerialLog artifacts/logs/libvirt-plan.log -DomainXml artifacts/logs/libvirt-plan.xml -LifecyclePlan artifacts/logs/libvirt-plan.lifecycle.log -TimeoutSeconds 45 -ForbidMarker OS_SMOKE_FAIL
```

Observed wrapper marker:

```text
TEST_PASS libvirt.lifecycle.plan
```

Build:

```powershell
dotnet build .\OS.slnx --nologo -m:1
```

Result after stopping a stale `OS.HostTests.exe` process that was holding the test apphost: passed with 0 warnings and 0 errors.

## Deferred Work

- No libvirt connection is opened.
- No transient domain is created, started, destroyed, or undefined.
- No serial log is polled.
- No timeout path is exercised against a live VM.
- No real libvirt, cloud import, hosted .NET 10, ASP.NET Minimal API, or Kestrel evidence is produced.

## Evidence Boundary

This step proves only the host-side lifecycle plan shape and default safety guard. It does not prove a libvirt VM boot.
