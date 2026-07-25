# Step 608 - PAL Mock Random Facade

## Summary

Added a host-tested mock PAL entropy slice for future `OSHost_GetRandomBytes`
status translation. The slice adds a POD request shape, a deterministic
in-process random-byte table, an exported-function-shaped facade, ABI layout
coverage, and focused host-test markers.

## Why

Hosted .NET and Kestrel-adjacent runtime work eventually needs a status-coded
random-byte boundary for runtime randomization and later cryptographic
prerequisites. The repository already had guarded platform RNG contracts, but
no PAL-facing caller-buffer request model for hosted runtime entropy.

## Files changed

- `src/OS.Kernel/Pal/OSHostAbi.cs`
  - Added `OSHostRandomBytesRequest` as a fixed-layout POD request.
- `src/OS.Kernel/Pal/OSHostRandomTable.cs`
  - Added a deterministic host-only random-byte table with status-coded
    request validation, unavailable-entropy reporting, request sequencing, and
    default-result rejection.
- `src/OS.Kernel/Pal/OSHostRandomFacade.cs`
  - Added a host-only `OSHost_GetRandomBytes`-shaped facade over the table.
- `tests/OS.HostTests/PalAbiLayoutTests.cs`
  - Added request layout, size, offset, and unmanaged-field coverage.
- `tests/OS.HostTests/PalRandomTableTests.cs`
  - Added table behavior tests and stable `pal.random table` markers.
- `tests/OS.HostTests/PalRandomFacadeTests.cs`
  - Added facade behavior tests and stable `pal.random facade` markers.
- `docs/pal.md`
  - Documented the entropy request, table, facade, tests, and evidence
    boundary.
- `work/PAL/pal-design.md`
  - Added the entropy slice to the PAL design ledger.
- `docs/completion-gates.md`
  - Recorded the host-only random-byte evidence without changing gate status.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|pal\.random|pal abi (structs contain no managed references|struct sizes are stable|field offsets are stable)|host\.tests'
```

## Evidence

- Host-test build completed successfully with 0 warnings and 0 errors after the
  missing test import was fixed.
- Focused marker scan emitted:
  - `TEST_PASS pal abi structs contain no managed references`
  - `TEST_PASS pal abi struct sizes are stable`
  - `TEST_PASS pal abi field offsets are stable`
  - `TEST_PASS pal.random facade fills caller buffer`
  - `TEST_PASS pal.random facade rejects malformed request`
  - `TEST_PASS pal.random facade reports unavailable table`
  - `TEST_PASS pal.random facade propagates entropy status`
  - `TEST_PASS pal.random table fills caller buffer`
  - `TEST_PASS pal.random table advances deterministic stream`
  - `TEST_PASS pal.random table rejects invalid requests`
  - `TEST_PASS pal.random table reports unavailable entropy`
  - `TEST_PASS pal.random table default result is not success`
- A later full host-test run captured in
  `artifacts/logs/host-tests-step606.log` emitted the same PAL random markers
  and `TEST_PASS host.tests`.

## Failures

- The first focused build failed because `PalRandomFacadeTests.cs` was missing
  `using System.Runtime.CompilerServices;`; this was fixed.
- That same first build also reported a transient existing output lock on
  `OS.ManifestTool.dll` held by dotnet processes, but the retry completed and
  the subsequent build passed cleanly.
- An earlier filtered host-test executable run exited with code 1 after
  emitting the new PAL random and ABI pass markers, with no matching
  `TEST_FAIL`, `Unhandled`, or `Exception` text in the filtered output. A later
  full host-test log emitted `TEST_PASS host.tests`.

## Deferred work

- No unmanaged `OSHost_GetRandomBytes` export exists.
- The mock table does not call firmware RNG, seed a post-firmware entropy pool,
  provide cryptographic randomness, feed hosted .NET runtime randomization,
  implement TLS/certificate prerequisites, or prove Kestrel behavior.

