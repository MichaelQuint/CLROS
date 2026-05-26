# Step 683 - FetchApp HTTP completion planner

## Summary
- Added a host-tested `OS.Net.FetchAppHttpCompletionPlan` contract over an
  already-successful `HttpTcpClientGetPlanResult` and caller-owned HTTP
  response bytes.
- Added host tests for success, failed HTTP plans, missing body markers, invalid
  body ranges, empty expected markers, short response buffers, and default
  result guards.
- Updated networking, app-model, and completion-gate docs with the evidence
  boundary.

## Why
FetchApp needs a deterministic completion predicate before a future app
supervisor can treat an HTTP GET result as successful application work. This
slice keeps that boundary host-tested and status-coded: it does not perform DNS,
sockets, live NIC I/O, TLS, redirects, allocation-heavy parsing, exceptions for
normal failure, or app execution.

## Files changed
- `src/OS.Kernel/Net/FetchAppHttpCompletionPlan.cs`
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `docs/networking.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step683.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo '/p:DefaultItemExcludes=$(DefaultItemExcludes)%3BStorageVirtioBlockPackageReadPreflightTests.cs'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "fetch\.http completion|host\.tests"`
- `git diff --check -- .\src\OS.Kernel\Net .\tests\OS.HostTests .\docs\networking.md .\docs\app-model.md .\docs\completion-gates.md .\done`

## Evidence
- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo` passed with 0
  warnings and 0 errors.
- The scoped host-test build with
  `DefaultItemExcludes=...StorageVirtioBlockPackageReadPreflightTests.cs`
  passed. It is only a workaround to compile and run the new #604 markers while
  preserving the unrelated storage-test file.
- The focused no-build run emitted:

```text
TEST_PASS fetch.http completion accepts expected body marker
TEST_PASS fetch.http completion rejects failed http plan
TEST_PASS fetch.http completion rejects missing body marker
TEST_PASS fetch.http completion rejects invalid body range
TEST_PASS fetch.http completion rejects empty expected marker
TEST_PASS fetch.http completion rejects short response buffer
TEST_PASS fetch.http completion default result is not success
```

- `git diff --check -- ...` exited 0 with only CRLF normalization warnings on
  existing tracked files.

## Failures
- The required unmodified host-test build is currently blocked by unrelated
  errors in `tests/OS.HostTests/StorageVirtioBlockPackageReadPreflightTests.cs`:
  missing `CreateSuccessfulPackageFileStagingPreflight` and `AllBytesEqual`
  helper methods.
- The required filtered no-build host run did not emit `TEST_PASS host.tests`.
  With the unrelated storage test excluded to allow compilation, the host test
  process still exited `-1` later in the suite after
  `TEST_START imagebuilder.kestrel_package stages published package`, with no
  matching `TEST_FAIL` line in the captured temp log. The new
  `fetch.http completion` markers had already passed before that unrelated
  harness failure point.

## Deferred work
- Wire this planner to a real FetchApp supervisor path only after live
  DNS/socket/TCP/HTTP I/O, app launch, and app log/exit observation exist.
- Keep Gate 8 and Gate 9 failed until FetchApp runs in the guest over live
  networking and the app-model captures a real exit/status marker.

