# Step 782 - FetchApp expected marker shared buffer copy

## Summary
Migrated `FetchAppHttpCompletionExpectation.CopyExpectedMarker` from direct
span copy to the shared `OS.Std.Memory.BufferOperations.MemoryCopy` helper.

## Why
The FetchApp completion path already uses shared no-runtime ASCII helpers for
its diagnostic formatters. Expected-marker staging is also caller-owned
no-runtime-facing byte movement, so it should use the shared memory helper
instead of a local direct copy path.

## Files changed
- `src/OS.Kernel/Net/FetchAppHttpCompletionExpectation.cs`
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `docs/networking.md`
- `done/step782.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue743-fetch-expectation-bufferops\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue743-host-build-20260525.log`
- `dotnet .\artifacts\issue743-fetch-expectation-bufferops\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue743-hosttests-20260525.log`
- `dotnet build-server shutdown`
- `dotnet .\artifacts\issue743-fetch-expectation-bufferops\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue743-hosttests-r2-20260525.log`

## Evidence
- `artifacts/logs/issue743-host-build-20260525.log` shows the focused host-test
  build succeeded with `0 Warning(s)` and `0 Error(s)`.
- `artifacts/logs/issue743-hosttests-r2-20260525.log` includes
  `TEST_PASS fetch.http expectation marker copy uses shared buffer operations`
  and ends with `TEST_PASS host.tests`.
- The implementation now calls
  `BufferOperations.MemoryCopy(marker, destination)` and maps any unexpected
  copy failure back to the existing destination-too-small result shape.

## Failures
- The first full host-test run in
  `artifacts/logs/issue743-hosttests-20260525.log` failed in unrelated
  `CiSmokeScriptTests` nested qemu-qcow2 dry-run because the default
  `src/OS.Kernel\obj\Debug\net10.0\OS.Kernel.dll` was locked by
  `VBCSCompiler`. `dotnet build-server shutdown` cleared the stale build server
  state, and the rerun passed.

## Deferred work
- Live virtio-net packet I/O, DHCP, DNS, TCP, HTTP GET through a NIC, FetchApp
  execution, hosted .NET, ASP.NET Minimal API, and Kestrel remain out of scope.

## Evidence boundary
This step proves only host-tested caller-owned expected-marker byte staging via
the shared buffer helper. It does not prove live network I/O, FetchApp runtime
execution, package loading from disk, hosted .NET, Kestrel, or cloud-provider
behavior.

