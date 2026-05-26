# Step 007 - Std Byte-Span Helpers and Libvirt Diagnostics

## What Changed

- Added `OS.Std.MemoryExtensions` in `std/no-runtime/shared` with a partial byte-span helper slice:
  - `Contains`
  - `EndsWith`
  - `IndexOf(byte)`
  - `IndexOf(ReadOnlySpan<byte>)`
  - `SequenceEqual`
  - `StartsWith`
- Added host tests for empty inputs, exact matches, mismatches, prefix/suffix behavior, missing values, repeated patterns, and `CompatibilityStatus.Partial`.
- Updated `docs/nativeaot-nostdlib-limits.md` to document that this is an `OS.Std` partial helper, not a canonical `System.MemoryExtensions` replacement.
- Added `OS.LibvirtRunner --diagnostics` so XML remains on stdout while stable `LIBVIRT_DIAG` readiness lines are written to stderr.
- Added `OS.LibvirtRunner --validate-paths` to fail early when the raw image, serial log directory, or configured OVMF loader is missing.
- Updated libvirt and cloud-import docs to keep dry-run diagnostics separate from real libvirt or cloud boot evidence.

## Why It Changed

The std/no-runtime work needs small shared byte-span operations before storage, manifest, and networking code start adding ad hoc helpers in OS layers.

The libvirt runner needs stronger host-side readiness diagnostics before any future real libvirt boot path starts creating domains or polling serial markers.

## Files Changed

- `std/no-runtime/shared/MemoryExtensions.cs`
- `tests/OS.HostTests/StdMemoryExtensionsTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `tools/OS.LibvirtRunner/Program.cs`
- `tests/OS.HostTests/LibvirtRunnerTests.cs`
- `docs/libvirt.md`
- `docs/cloud-import.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireMarker OS_BOOT_OK,UEFI_GONE_SIM_PASS
git diff --check
```

## Logs and Evidence

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- New std marker passed: `TEST_PASS std.memoryextensions byte span helpers`.
- New libvirt markers passed:
  - `TEST_PASS libvirt diagnostics report real-run path readiness`
  - `TEST_PASS libvirt validate-paths rejects missing image`
- Naming guard emitted `TEST_PASS naming.guard`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- Multi-marker dry-run emitted `REQUIRED_MARKERS OS_BOOT_OK;UEFI_GONE_SIM_PASS` and `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0. It printed existing line-ending normalization warnings only.

## Failures

No failures remain for this slice.

## Deferred Work

- Expand `OS.Std.MemoryExtensions` only as kernel/app consumers need more behavior, keeping partial APIs under `OS.Std` until public BCL compatibility is proven.
- Do not expose this helper as `System.MemoryExtensions` until the contract is complete and documented.
- Add real libvirt connection, domain lifecycle, serial marker polling, and cleanup after QEMU and image artifacts are stable.
- Keep provider cloud import unclaimed until raw/qcow2 artifacts boot locally and provider-specific validation is performed.

## Evidence Boundary

This step proves host-side std byte-span helper behavior and libvirt dry-run readiness diagnostics. It does not prove a full no-stdlib BCL, runtime helpers, real libvirt boot, cloud import, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
