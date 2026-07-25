# Step 080 - COM1 Port-I/O Backend Contract

## What Changed

- Added `Com1PortIoBackendContract` to resolve planned COM1 `Out8` and `In8` stub offsets into absolute future backend addresses.
- Added status-coded rejection for failed backend readiness, failed stub bundle plans, missing stub slots, invalid COM1 base ports, zero payload base addresses, and address overflow.
- Added host tests proving this is metadata resolution only.

## Why It Changed

Issue #98 needed an ABI/address boundary between planned generated COM1 stubs and the future owned serial writer before any function pointer invocation or port I/O is allowed.

## Files Changed

- `src/OS.Kernel/Diagnostics/Com1PortIoBackendContract.cs`
- `tests/OS.HostTests/Com1PortIoBackendContractTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS com1 port io backend contract resolves stub addresses`
  - `TEST_PASS com1 port io backend contract rejects failed readiness`
  - `TEST_PASS com1 port io backend contract rejects missing stub slots`
  - `TEST_PASS com1 port io backend contract rejects invalid payload base or overflow`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No delegate creation from raw addresses.
- No function-pointer invocation.
- No real port I/O.
- No wiring into `Com1SerialWriter`.

## Evidence Boundary

This step proves host-side address metadata only. It is not an owned serial backend or post-UEFI diagnostics proof.
