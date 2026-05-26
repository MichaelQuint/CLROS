# Step 768 - PAL export catalog formatter shared ASCII builder

## Summary
Migrated `OSHostExportCatalogDiagnosticFormatter` from direct span-copy
appends and `AsciiDecimalFormatter.FormatInt32(...)` write calls to the shared
`OS.Std.Text.AsciiTextBuilder` append path. The existing
`PAL_EXPORT_CATALOG_VALIDATION` marker text, required-byte query behavior,
short-destination behavior, and default-result rejection remain unchanged.

## Why
PAL readiness diagnostics are early ABI evidence for the future `OSHost_*`
boundary. They should use the common no-runtime-compatible ASCII builder
surface so integer and byte appends are centralized before the catalog becomes
part of exported PAL smoke evidence.

## Files changed
- `src/OS.Kernel/Pal/OSHostExportCatalog.cs`
- `tests/OS.HostTests/PalExportCatalogTests.cs`
- `docs/pal.md`
- `done/step768.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue729-pal-export-formatter\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue729-host-build-20260525.log`
- `dotnet .\artifacts\issue729-pal-export-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue729-hosttests-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue729-git-diff-check-20260525.log`

## Evidence
- Build log: `artifacts/logs/issue729-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue729-hosttests-20260525.log`
  - `TEST_PASS pal.export catalog formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Source guard evidence:
  - `OSHostExportCatalog.cs` still uses
    `AsciiDecimalFormatter.CountInt32Digits(...)` for required-byte counts.
  - `OSHostExportCatalog.cs` now uses
    `AsciiTextBuilder.AppendBytes(destination, offset, value)` for byte spans.
  - `OSHostExportCatalog.cs` now uses
    `AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)` for
    integer appends.
  - The guard rejects `AsciiDecimalFormatter.FormatInt32(...)`,
    `destination.Slice(offset)`, direct `value.CopyTo(destination[offset..])`,
    formatter-local int32 byte-count helpers, formatter-local int32 append
    helpers, and digit-loop patterns.
- Source check: `artifacts/logs/issue729-git-diff-check-20260525.log`
  exited 0 with only existing LF-to-CRLF working-copy warnings.

## Failures
- None in this slice.
- No QEMU smoke was run because this is host-tested PAL diagnostic formatting
  only.

## Deferred work
- Exported unmanaged `OSHost_*` symbols are still future work.
- Live hosted CoreCLR, hosted .NET 10, Kestrel, scheduler integration, and PAL
  socket/timer/thread behavior remain unimplemented.
- Keep migrating PAL diagnostics that still bypass shared no-runtime ASCII
  builders as they become implementation blockers.

## Evidence boundary
This step proves host-side formatting of already-produced PAL export catalog
validation results through the shared no-runtime ASCII text builder. It does
not create unmanaged PAL exports, cross the OSHost ABI from hosted runtime
code, run CoreCLR, start Kestrel, perform live firmware calls, or provide QEMU
or cloud-provider evidence.

