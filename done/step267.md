# Step 267 - NativeAOT Helper Ledger Symbol Coverage

## What Changed

- Added `HelperSymbols` to `NativeAotHelperLedgerEntry`.
- Added symbol/contract detail for every helper ledger entry.
- Recorded exact EFI shim exports for the reverse P/Invoke and fail-fast
  type-cast ledger entries.
- Added host tests that parse `src/OS/Runtime/MinimalRuntime.cs` and require
  every current `[RuntimeExport]` symbol to appear in exactly one ledger entry.
- Updated NativeAOT no-stdlib limits and completion-gate docs.

## Why It Changed

Issue #267 needed the runtime helper ledger to become implementation-ready
without pretending that helper implementations exist. Exact EFI export coverage
keeps the no-stdlib shell boundary auditable while the real kernel/runtime
helpers remain deferred.

## Files Changed

- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step267.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
dotnet build .\OS.slnx --nologo -m:1
.\scripts\check-naming.ps1
git diff --check
```

## Logs And Evidence

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll` passed and
the filtered evidence included:

```text
TEST_START runtime.nativeaot helper ledger
TEST_PASS runtime.nativeaot helper ledger
TEST_START runtime.nativeaot helper ledger symbols
TEST_PASS runtime.nativeaot helper ledger symbols
TEST_PASS host.tests
HOST_TEST_EXIT 0
```

`dotnet build .\OS.slnx --nologo -m:1` passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`.\scripts\check-naming.ps1` passed with:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

`git diff --check` exited `0`. It printed only line-ending normalization
warnings for existing dirty files.

## Failures

No current verification failures for this metadata/test slice.

## Evidence Boundary

This is metadata, tests, and documentation only:

- no allocation helpers;
- no write barriers;
- no interface dispatch;
- no class constructor runner;
- no thread-static support;
- no exception/unwind support;
- no TypeManager/RTR scanning;
- no GC root scanning;
- no Gate 4 completion claim.

## Deferred Work

- Implement each deferred helper area with real behavior and focused tests.
- Replace EFI-only fail-fast/link stubs when the kernel/runtime path needs the
  corresponding behavior outside the no-stdlib EFI shell.
