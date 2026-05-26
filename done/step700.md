# Step 700 - FetchApp Config Preflight BootSequence Marker

## Summary

Added a host-tested BootSequence readiness boundary for manifest-derived
FetchApp config preflight metadata.

## Why

Issue #620 needed a compact boot marker slice between native manifest config
preflight and future FetchApp HTTP completion planning. The marker records
whether the already-produced manifest/config preflight is ready without
claiming live storage, FetchApp execution, networking, hosted .NET, or Kestrel.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `docs/boot.md`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step700.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "manifest fetchapp config preflight|boot.sequence fetchapp config|host.tests|TEST_FAIL"`
- `git diff --check -- src/OS.Kernel/Boot/BootSequence.cs tests/OS.HostTests/NativeManifestParserTests.cs docs/boot.md docs/manifest.md docs/app-model.md docs/completion-gates.md done`

## Evidence

- Build passed with 0 warnings and 0 errors after the initial file-lock
  contention cleared.
- Parent full host-test evidence is preserved in
  `artifacts/logs/parent-hosttests-20260525-050128.log`. The full run exited 0
  and emitted `TEST_PASS host.tests`.
- The filtered host-test markers were captured in
  `artifacts/logs/issue-620-hosttests.log`:
  - `TEST_PASS manifest fetchapp config preflight valid config`
  - `TEST_PASS manifest fetchapp config preflight defaults`
  - `TEST_PASS manifest fetchapp config preflight invalid status`
  - `TEST_PASS manifest fetchapp config preflight empty marker`
  - `TEST_PASS manifest fetchapp config preflight oversized marker`
  - `TEST_PASS manifest fetchapp config preflight manifest not ready`
  - `TEST_PASS boot.sequence fetchapp config preflight ready`
  - `TEST_PASS boot.sequence fetchapp config preflight rejects failed result`
  - `TEST_PASS boot.sequence fetchapp config preflight rejects default result`
  - `TEST_PASS boot.sequence fetchapp config preflight preserves default markers`
- `git diff --check` passed for the owned paths. Git printed only line-ending
  normalization warnings.
- New host-test marker names:
  - `TEST_PASS boot.sequence fetchapp config preflight ready`
  - `TEST_PASS boot.sequence fetchapp config preflight rejects failed result`
  - `TEST_PASS boot.sequence fetchapp config preflight rejects default result`
  - `TEST_PASS boot.sequence fetchapp config preflight preserves default markers`
- New BootSequence marker prefixes:
  - `FETCHAPP_CONFIG_PREFLIGHT_READY`
  - `FETCHAPP_CONFIG_PREFLIGHT_NOT_READY`

## Failures

- The first build attempt failed because
  `tools/OS.QemuRunner/obj/Debug/net10.0/OS.QemuRunner.dll` was locked by
  another process. A later retry succeeded.
- The full filtered host-test command did not reach `TEST_PASS host.tests` in
  this run. It reached `TEST_START source firewall accepts current tree` after
  the #620 markers and then exited nonzero from the tool session before the
  host-test process completed. Parent verification reran the full host-test
  project, exited 0, and reached `TEST_PASS host.tests`.

## Deferred work

- The marker remains host-tested only and is not emitted by the default
  firmware-visible boot path.
- Live manifest reads, FetchApp execution, live HTTP I/O, hosted .NET, and
  Kestrel remain future work.

