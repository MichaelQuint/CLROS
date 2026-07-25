# Step 126 - Native App Payload Header Validation

## What Changed

- Added `NativeAppPayloadHeader` with status-coded validation for a small v0 native payload header.
- Required selected native package payloads to start with `OSAPPv0\0`, version `0`, header size `32`, flags `0`, a valid entry offset after the header, and reserved field `0`.
- Updated `NativeAppLaunchDescriptorBuilder` to reject invalid selected package payload headers with `InvalidPayloadHeader` while preserving distinct missing package-file behavior.
- Updated boot-sequence app descriptor and app-selection fixtures so valid test packages carry the payload header.
- Added host tests for valid payload headers, bad magic, unsupported versions, invalid entry offsets, and launch-descriptor payload rejection.
- Updated app-model, manifest, and completion-gate docs.

## Why It Changed

Issue #143 needed the selected native app payload to be checked before later app-launch work maps or enters the binary. This gives the app-selection chain one more deterministic metadata gate without defining the final app ABI or executing app code.

## Files Changed

- `src/OS.Kernel/Manifest/NativeAppPayloadHeader.cs`
- `src/OS.Kernel/Manifest/NativeAppLaunchDescriptor.cs`
- `tests/OS.HostTests/NativeAppPayloadHeaderTests.cs`
- `tests/OS.HostTests/NativeAppLaunchDescriptorTests.cs`
- `tests/OS.HostTests/BootSequenceAppLaunchDescriptorTests.cs`
- `tests/OS.HostTests/BootSequenceAppSelectionReadinessTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step126.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- Solution build passed with 0 warnings and 0 errors.
- Parent-owned full host tests emitted:
  - `TEST_PASS appmodel payload header accepts v0 native payload`
  - `TEST_PASS appmodel payload header rejects bad magic`
  - `TEST_PASS appmodel payload header rejects unsupported version`
  - `TEST_PASS appmodel payload header rejects invalid entry offset`
  - `TEST_PASS appmodel launch descriptor rejects invalid payload header`
  - `TEST_PASS host.tests`
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited successfully. It still printed existing line-ending normalization warnings.

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No executable mapping, relocation, stack allocation, app ABI entry, scheduler/thread startup, stdout/stderr routing, exit-code capture, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel behavior was added.
- The v0 header is a minimal metadata gate only; it is not a complete executable format.

## Evidence Boundary

This step proves selected native payload metadata validation only. It does not prove native app execution, memory permissions, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
