# Step 146 - QEMU Native App Marker Preset

## What Changed

- Added `-RequireNativeAppMarkers` to `scripts/run-qemu-smoke.ps1`.
- The switch appends native app readiness markers to the active required marker list without changing the default QEMU smoke marker set.
- The preset includes:
  - `APP_PACKAGE_READY`
  - `APP_LAUNCH_READY`
  - `APP_PAYLOAD_READY`
  - `APP_EXEC_MAPPING_READY`
  - `APP_EXEC_MEMORY_READY`
  - `APP_INVOCATION_READY`
- Added host-test source with the required issue #164 markers:
  - `TEST_PASS qemu smoke script native app preset includes app markers`
  - `TEST_PASS qemu dry-run emits native app marker requirements`
- Updated QEMU, image-builder, and completion-gate docs with the dry-run-only evidence boundary.

## Why It Changed

Gitea issue #164 needed a documented dry-run preset for future native app images. This lets the QEMU wrapper express the expected app readiness marker sequence without changing default smoke behavior or claiming that a live QEMU boot currently loads, maps, or invokes app code.

## Files Changed

- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/image-builder.md`
- `docs/completion-gates.md`
- `done/step146.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check -- .\scripts\run-qemu-smoke.ps1 .\tests\OS.HostTests\QemuRunnerTests.cs .\docs\qemu.md .\docs\image-builder.md .\docs\completion-gates.md .\done\step146.md
```

## Logs And Evidence

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers` emitted `TEST_PASS qemu.runner.dryrun`.
- The native app preset dry-run emitted:

```text
REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;APP_PACKAGE_READY;APP_LAUNCH_READY;APP_PAYLOAD_READY;APP_EXEC_MAPPING_READY;APP_EXEC_MEMORY_READY;APP_INVOCATION_READY
```

- `.\scripts\run-qemu-smoke.ps1 -DryRun` still emitted only the default smoke marker set:

```text
REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY
```

- Scoped `git diff --check` passed for the issue #164 files. Git emitted line-ending normalization warnings for existing tracked files, but no whitespace errors.
- Required host-test markers added in source: `TEST_PASS qemu smoke script native app preset includes app markers` and `TEST_PASS qemu dry-run emits native app marker requirements`.

## Failures

- No focused build or dry-run failures were observed.
- Full host-test execution was intentionally not run for this pass because issue #164 only needed focused verification and broad host-test verification was explicitly stopped.
- No real QEMU boot was run.

## Deferred Work

- Do not close issue #164 from this step.
- Wire the app readiness markers into a live boot path only after boot-time package loading, executable mapping, memory permission transitions, and invocation prerequisites have evidence.
- Capture real QEMU evidence only when the live firmware-visible path is expected to emit these app markers.

## Evidence Boundary

This step is a script/test/docs dry-run contract only. It proves the wrapper can require the native app marker sequence and that the default smoke marker behavior remains unchanged. It does not prove guest storage, boot-time package loading, executable mapping, page protection changes, native app invocation, hosted .NET 10, ASP.NET Minimal API, Kestrel, or real QEMU app execution.
