# Step 058 - Panic Text Buffer Contract

## What Changed

- Added `PanicTextBuffer`, a caller-owned byte-buffer writer for panic diagnostics.
- Added ASCII literal writes, CR/LF line termination, unsigned decimal formatting, fixed-width hexadecimal `ulong` formatting, and overflow/invalid-ASCII statuses.
- Added fault-summary formatting that follows the existing CPU fault field order without `StringBuilder`.
- Added host tests for exact output, numeric formatting, overflow behavior, and sample fault summary formatting.

## Why It Changed

Issue #76 needed an allocation-conscious formatting contract before real fault handlers or post-firmware panic paths can safely emit diagnostic text. This creates the formatter shape without switching any live panic path yet.

## Files Changed

- `src/OS.Kernel/Diagnostics/PanicTextBuffer.cs`
- `tests/OS.HostTests/PanicTextBufferTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `docs/drivers.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS panic text buffer writes ascii lines`
  - `TEST_PASS panic text buffer writes decimal and hex`
  - `TEST_PASS panic text buffer reports overflow`
  - `TEST_PASS panic text buffer formats fault summary`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This is not wired into the real panic path.
- This does not prove register capture from a CPU fault handler.
- This does not prove serial output after `ExitBootServices`.

## Evidence Boundary

This step proves caller-storage panic text formatting only. It is not a live panic path or post-firmware diagnostics proof.
