# Step 313 - Opt-In QEMU Marker Presets

## What Changed

- Added `tools/OS.QemuRunner --marker-preset baseline|acpi|uefi-gone`.
- Added `scripts/run-qemu-smoke.ps1 -MarkerPreset baseline|acpi|uefi-gone` and comma-delimited preset validation.
- Added dry-run and fake-QEMU real-mode tests for preset selection, marker expansion, unknown-preset rejection, script forwarding, and summary artifact output.
- Updated QEMU and completion-gate documentation to describe marker presets as future required-marker contracts only.

## Why It Changed

Issue #313 needed a way for future agents to require ACPI and UEFI-gone probe markers without changing the default QEMU smoke claim. The presets make the expected marker sets explicit while keeping current default smoke behavior unchanged.

## Files Changed

- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tools/OS.QemuRunner/Program.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step313.md`

## Evidence

Focused host-test project compile:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-dependencies
```

Result: passed with 0 warnings and 0 errors.

Managed solution build:

```powershell
dotnet build .\OS.slnx --nologo -m:1
```

Result: passed with 0 warnings and 0 errors.

Full host-test run:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Result: emitted `TEST_PASS host.tests`.

Expected focused host-test markers:

```text
TEST_PASS qemu.smoke.preset default preserves current markers
TEST_PASS qemu.smoke.preset acpi adds validation markers
TEST_PASS qemu.smoke.preset uefi gone adds denial and owned serial markers
TEST_PASS qemu.smoke.preset rejects unknown preset
TEST_PASS qemu.smoke.preset script dry-run includes selected preset
TEST_PASS qemu.smoke.preset real mode passes selected markers
```

Whitespace check:

```powershell
git diff --check
```

Result: passed; Git reported existing LF-to-CRLF conversion warnings only.

## Deferred Work

- No live ACPI table walking is implemented.
- No live RSDP physical-memory dereference is implemented by this step.
- No real `ExitBootServices` path is enabled.
- No post-firmware owned serial output is proven.
- No new default smoke markers are required.

## Evidence Boundary

This step proves host-side marker selection and runner/script wiring only. It does not prove that QEMU firmware logs currently emit the new ACPI or UEFI-gone preset markers.
