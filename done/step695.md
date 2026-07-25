# Step 695 - Manifest FetchApp config preflight

## Summary
Added a manifest-side, no-I/O FetchApp HTTP completion config preflight over
an already parsed `NativeManifestLoadResult`.

## Why
FetchApp completion planning needs to reject malformed manifest config before a
future app/supervisor path treats expected HTTP status and body-marker metadata
as ready. The preflight keeps that validation status-coded and caller-buffer
based without parsing JSON, reading packages, or performing network I/O.

## Files changed
- `src/OS.Kernel/Manifest/NativeManifestFetchAppConfigPreflight.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step695.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "manifest fetchapp config preflight|fetch.http completion config|host.tests|TEST_FAIL"`
- `git diff --check -- src/OS.Kernel/Manifest src/OS.Kernel/Net tests/OS.HostTests/NativeManifestParserTests.cs docs/manifest.md docs/app-model.md docs/networking.md docs/completion-gates.md done`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Filtered host-test output included `TEST_PASS host.tests`.
- Host tests were added for:
  - `TEST_PASS manifest fetchapp config preflight valid config`
  - `TEST_PASS manifest fetchapp config preflight defaults`
  - `TEST_PASS manifest fetchapp config preflight invalid status`
  - `TEST_PASS manifest fetchapp config preflight empty marker`
  - `TEST_PASS manifest fetchapp config preflight oversized marker`
  - `TEST_PASS manifest fetchapp config preflight manifest not ready`
- Existing FetchApp config expectation markers still passed:
  - `TEST_PASS fetch.http completion config uses deterministic defaults`
  - `TEST_PASS fetch.http completion config flows from manifest`
  - `TEST_PASS fetch.http completion config rejects invalid status`
  - `TEST_PASS fetch.http completion config rejects empty marker`
  - `TEST_PASS fetch.http completion config rejects oversized marker`
  - `TEST_PASS fetch.http completion config requires marker when requested`
- Scoped `git diff --check` completed with exit code 0.

## Failures
- None in the focused verification. The scoped `git diff --check` printed
  existing LF-to-CRLF working-copy warnings, but no whitespace errors.

## Deferred work
- Wire the preflight into a future boot/app-selection path only after live
  storage can provide manifest bytes and after FetchApp execution/network I/O
  has separate evidence.

## Evidence boundary
- This is host-tested manifest/config metadata validation only. It does not
  prove live manifest loading, package reads, FetchApp execution, HTTP GET,
  virtio-net, hosted .NET 10, Minimal API, Kestrel, cloud readiness, or
  production readiness.

