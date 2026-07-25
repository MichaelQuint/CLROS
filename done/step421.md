# Step 421 - Virtio-block app-selection preflight default-result guard

## Summary

Added an initialized-result guard to the virtio-block native app-selection
preflight result so a zero-initialized/default result is not reported as
successful readiness evidence.

## Why

The preflight status enum uses `Success` as its zero value, which meant
`default(VirtioBlockNativeAppSelectionPreflightResult).Succeeded` could read as
successful even though no read session, scratch plan, or selection load had run.
That was too optimistic for the boot/app-model evidence boundary and needed a
host-tested guard before later boot composition can rely on the result.

## Files changed

- `src/OS.Kernel/Manifest/VirtioBlockNativeAppSelectionPreflight.cs`
  - Added private initialization state and required it for `Succeeded`.
- `tests/OS.HostTests/VirtioBlockNativeAppSelectionPreflightTests.cs`
  - Added the default-result regression test.
- `docs/app-model.md`
  - Documented the default-result guard and marker.
- `docs/cstorage.md`
  - Documented the storage/app-model preflight boundary update.
- `docs/completion-gates.md`
  - Added the guard and marker to the relevant app-model gate evidence.
- `done/step421.md`
  - Recorded this milestone.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host test build succeeded with 0 warnings and 0 errors.
- Host test run completed with `TEST_PASS host.tests`.
- Added marker:
  - `TEST_PASS appmodel.virtio selection preflight default result is not success`
- Full solution build succeeded with 0 warnings and 0 errors.
- Naming/source firewall succeeded:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- `git diff --check` exited 0; output contained only CRLF normalization
  warnings for already-edited text files.

## Failures

None.

## Deferred work

- Compose the guarded virtio-block native app-selection preflight into a later
  boot/app-model readiness slice without claiming live storage, hosted .NET, or
  Kestrel execution.

