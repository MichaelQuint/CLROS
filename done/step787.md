# Step 787 - HTTP request builder shared buffer copy

## Summary

Migrated `HttpRequestBuilder.TryWriteGet` dynamic host and path byte copies
onto `OS.Std.Memory.BufferOperations.MemoryCopy` and added a host source-guard
marker for the contract.

## Why

The networking layer should use the shared `std/no-runtime/shared` memory
surface for caller-owned buffer copies. This keeps HTTP request staging aligned
with the rest of the CloudReady no-runtime migration while preserving exact
request bytes.

## Files changed

- `src/OS.Kernel/Net/HttpRequestBuilder.cs`
  - uses `BufferOperations.MemoryCopy` for path and host byte copies after the
    existing validation and destination-size checks.
- `tests/OS.HostTests/HttpRequestBuilderTests.cs`
  - adds `TEST_PASS net.http.request builder uses shared buffer operations`.
- `docs/networking.md`
  - records the #748 host-tested evidence boundary.
- `done/step787.md`
  - records this milestone and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
- scoped `git diff --check` for the changed #748 files.
- scoped `rg -n "[ \t]$"` trailing-whitespace scan for the changed #748 files.

## Evidence

- `artifacts/logs/issue748-host-build-default-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- `artifacts/logs/issue748-hosttests-default-20260525.log`
  - `TEST_PASS net.http.request builder uses shared buffer operations`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue748-git-diff-check-scoped-r3-20260525.log`
  - scoped `git diff --check` exited 0 with only LF-to-CRLF warnings from this
    Windows checkout.
- `artifacts/logs/issue748-rg-trailing-whitespace-r3-20260525.log`
  - `TEST_PASS issue748.scoped_whitespace_source`

## Failures

- `artifacts/logs/issue748-host-build-20260525.log` failed before compile with
  a NuGet restore filesystem race:
  `Cannot create a file when that file already exists`.
- `artifacts/logs/issue748-host-build-r2-20260525.log` failed when using a
  custom intermediate path because stale generated sources under
  `tools/OS.QemuRunner/obj/*` were included and produced duplicate assembly
  attributes. The documented default host-test build path was used for the
  passing verification above.
- The first scoped whitespace scan found two pre-existing trailing spaces in
  comments inside `HttpRequestBuilder.cs`; those comments were cleaned and the
  second scan passed.

## Deferred work

- Live virtio-net, DNS, TCP, HTTP GET execution, FetchApp execution, hosted
  .NET, ASP.NET Minimal API, and Kestrel remain deferred.

## Evidence boundary

This step is intended to prove only the host-tested caller-owned HTTP request
builder copy contract. It is not QEMU, live NIC, TCP, FetchApp, hosted .NET, or
Kestrel evidence.
