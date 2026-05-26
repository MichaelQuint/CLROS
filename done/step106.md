# Step 106 - PAL Socket ABI Value Contracts

## What Changed

- Added Kestrel-critical PAL socket value contracts under `OS.Pal`.
- Added socket handle, IPv4 endpoint, wait option, timeout conversion, operation-result, socket constants, and status-code name helpers.
- Added host tests for layout-relevant sizes/offsets, invalid endpoint/wait/timeout values, and deterministic socket-related status names.

## Why It Changed

Issue #124 fixes ABI-shaped socket values before any hosted runtime adapter, socket PAL export, or Kestrel work starts. This keeps the future boundary status-coded and POD-only.

## Files Changed

- `src/OS.Kernel/Pal/OSHostSocketAbi.cs`
- `tests/OS.HostTests/PalSocketAbiTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build > .\artifacts\logs\host-tests-current.log
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS pal.socket abi exposes endpoint contracts`
  - `TEST_PASS pal.socket abi rejects invalid endpoint values`
  - `TEST_PASS pal.socket abi exposes deterministic status codes`
  - `TEST_PASS host.tests`

## Failures

- No failures in the final coordinated verification pass.

## Deferred Work

- No `OSHost_*` socket exports.
- No socket behavior, TCP listener, CoreCLR PAL adapter, hosted runtime, ASP.NET, or Kestrel.

## Evidence Boundary

This step proves host-side ABI value contracts only. It is not socket, TCP, hosted .NET 10, Minimal API, or Kestrel evidence.
