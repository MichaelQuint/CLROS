# Step 637 - PAL Status Domain Contract

## Summary

Added a host-tested PAL status-domain contract helper for the `OSHost_*`
config, environment, time, and console/log surfaces.

## Why

Hosted-runtime PAL calls need deterministic status-code sets per ABI domain
before unmanaged exports exist. This slice records the allowed status sets and
keeps the result surface POD/status-code shaped so later exported functions do
not leak managed references, exceptions, or allocator-owned return buffers.

## Files changed

- `src/OS.Kernel/Pal/OSHostStatusContract.cs`
- `tests/OS.HostTests/PalStatusContractTests.cs`
- `docs/pal.md`
- `docs/completion-gates.md`
- `work/PAL/pal-design.md`
- `done/step637.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build --no-restore -- /p:OSWorkloadProfile=SingleMinimalApi | Tee-Object -FilePath .\artifacts\logs\host-tests-step637.log`
- `Select-String -Path .\artifacts\logs\host-tests-step637.log -Pattern 'pal.status_contract|TEST_FAIL qemu.smoke.preset diagnostics|TEST_FAIL qemu.runner.tests|TEST_PASS host.tests'`
- `git diff --check -- .\src\OS.Kernel\Pal\OSHostStatusContract.cs .\tests\OS.HostTests\PalStatusContractTests.cs .\docs\pal.md .\docs\completion-gates.md .\work\PAL\pal-design.md .\done\step637.md`

## Evidence

- Host-test project build with explicit workload profile passed with 0 warnings
  and 0 errors.
- `artifacts/logs/host-tests-step637.log` contains:
  - `TEST_PASS pal.status_contract validates config and environment statuses`
  - `TEST_PASS pal.status_contract validates time and console statuses`
  - `TEST_PASS pal.status_contract formats domain names`
  - `TEST_PASS pal.status_contract rejects malformed inputs without throwing`
  - `TEST_PASS pal.status_contract result layouts contain no managed references`
  - `TEST_PASS pal.status_contract default results are not success`
- The status-contract result layout test proves
  `OSHostStatusContractResult` and `OSHostStatusDomainNameFormatResult` do not
  contain managed references.
- `git diff --check` passed for the touched files with line-ending warnings
  only on existing documentation files.

## Failures

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo` failed before
  compilation completed because the existing workload-profile guard rejects an
  empty `OSWorkloadProfile` for `tools/OS.QemuRunner`:
  `Invalid OSWorkloadProfile ''. Valid values: SingleMinimalApi, MultiMinimalApi.`
  The build passed when rerun with `/p:OSWorkloadProfile=SingleMinimalApi`.
- The full host-test executable exited nonzero after the PAL markers passed
  because the unrelated QEMU diagnostics-chain preset test failed:
  `TEST_FAIL qemu.smoke.preset diagnostics adds readiness chain markers` and
  `TEST_FAIL qemu.runner.tests failures=1`. That area is outside this PAL-only
  slice and was not modified.

## Deferred work

- No exported `OSHost_*` symbols were added.
- No hosted runtime, guest PAL consumer, scheduler wake, disk, socket, or
  Kestrel behavior is implemented by this host-only contract helper.

