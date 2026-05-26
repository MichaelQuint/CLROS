# Step 624 - Native App Package Staging Formatter

## Summary

Added a host-testable native app package-staging not-ready marker formatter.
The formatter writes the stable `APP_PACKAGE_STAGING_NOT_READY ...` diagnostic
line into caller-owned ASCII byte buffers, reports required size for short
destinations, and rejects unknown status enum values before writing.

## Why

Package staging now preserves nested manifest, package, descriptor, staging,
memory, and invocation status metadata, but boot-facing diagnostics still need
a bounded marker formatter that can summarize failure state without live
storage behavior or exception-driven formatting. This slice keeps the formatter
small and host-tested while leaving the existing package staging planner and
live boot/storage behavior unchanged.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppPackageStagingReadinessFormatter.cs`
- `tests/OS.HostTests/NativeAppPackageStagingFormatterTests.cs`
- `docs/app-model.md`
- `done/step624.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false -o .\artifacts\codex-hosttests-issue5`
- `dotnet build .\artifacts\codex-hosttests-issue5-runner\FormatterRunner.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false -o .\artifacts\codex-formatter-runner-out`
- `.\artifacts\codex-formatter-runner-out\FormatterRunner.exe`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Isolated host-test build completed with 0 warnings and 0 errors.
- Focused formatter runner emitted:
  - `TEST_PASS appmodel package staging formatter formats not-ready marker`
  - `TEST_PASS appmodel package staging formatter reports short destination`
  - `TEST_PASS appmodel package staging formatter formats default readiness`
  - `TEST_PASS appmodel package staging formatter rejects invalid status`
  - `TEST_PASS appmodel package staging formatter default result is not success`
  - `TEST_PASS formatter.runner`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` reported only Git CRLF conversion warnings; no whitespace
  errors were reported.

## Failures

- A normal host-test build to `tests/OS.HostTests\bin\Debug\net10.0` failed
  because existing `OS.HostTests` processes with PIDs `13508` and `6288` held
  `OS.Kernel.dll` and `OS.Std.NoRuntime.Shared.dll` open. The isolated
  `artifacts` output build avoided those locked files and passed.
- Running the full isolated host-test executable stopped in existing
  `CiSmokeScriptTests` before the new formatter module initializer ran. The
  failure was caused by CI smoke dry-run subcommands using normal tool output
  paths while another process held
  `tools\OS.QemuRunner\obj\Debug\net10.0\OS.QemuRunner.dll`. The focused
  formatter runner was used for this slice's marker evidence.

## Deferred work

- Wire the caller-buffer formatter into `BootSequence` when that file is in
  scope, replacing the current inline string marker composition with the shared
  formatter while preserving the existing marker text.
- Extend the same caller-buffer marker pattern to other app readiness helpers
  only where it reduces duplicated or allocation-sensitive diagnostics.

## Evidence boundary

This is host-tested diagnostic formatting evidence only. It does not change
the package staging planner, perform live storage reads, allocate boot staging
memory, map executable pages, change permissions, relocate code, execute a
native app, start hosted .NET 10, or prove Kestrel/cloud readiness.
