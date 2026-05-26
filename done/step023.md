# Step 023 - PAL Status Codes and POD Layout Tests

## What Changed

- Added `src/OS.Kernel/Pal/OSHostAbi.cs` with the first C# PAL ABI declaration layer.
- Defined stable `OSHostStatus` numeric values for success, invalid argument, unsupported, not found, access denied, buffer-too-small, out-of-memory, nonblocking, timeout, interrupted, EOF, connection errors, address errors, network down, retry, I/O error, and internal error.
- Added POD structs for common handles/byte spans, memory request/range/query data, time snapshots, log records, file info, block read requests, package paths, and config queries.
- Added host tests that verify sequential layout, stable enum values, no managed-reference fields, stable sizes, and selected field offsets.
- Updated PAL docs and `work/PAL/pal-design.md` to document the declaration layer and its evidence boundary.

## Why It Changed

PAL work needs stable status codes and unmanaged layout contracts before adding exported `OSHost_*` functions or hosted runtime adapters. This step creates the first testable ABI surface without pretending that PAL behavior exists.

## Files Changed

- `src/OS.Kernel/Pal/OSHostAbi.cs`
- `tests/OS.HostTests/PalAbiLayoutTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `done/step023.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check -- .\src\OS.Kernel\Pal\OSHostAbi.cs .\tests\OS.HostTests\PalAbiLayoutTests.cs .\docs\pal.md .\work\PAL\pal-design.md .\done\step023.md
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Full host tests emitted `TEST_PASS host.tests`.
- PAL ABI host-test markers passed:
  - `TEST_PASS pal abi status code values are stable`
  - `TEST_PASS pal abi structs use sequential layout`
  - `TEST_PASS pal abi structs contain no managed references`
  - `TEST_PASS pal abi struct sizes are stable`
  - `TEST_PASS pal abi field offsets are stable`
- Scoped `git diff --check` exited 0 with only existing LF/CRLF normalization warnings.

## Failures

The first host-test run caught that auto-properties are not marshaled ABI fields for `Marshal.OffsetOf`. The ABI structs were corrected to explicit public readonly fields, and the rerun passed.

## Deferred Work

- Add socket address, socket option, timer, wait, and thread/sync ABI structs.
- Add exported `OSHost_*` functions that return `OSHOST_STATUS_NOT_SUPPORTED` only where linkability is needed and success behavior is not implemented.
- Implement package/config, timer, thread/sync, and socket behavior.
- Integrate with hosted .NET 10 and Kestrel only after the required PAL domains have behavior and guest evidence.

## Evidence Boundary

This step proves status-code and POD layout declarations only. It does not export PAL functions, implement memory allocation, files, timers, threads, sockets, hosted .NET 10, Kestrel, or in-guest HTTP request/response behavior.
