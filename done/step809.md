# Step 809 - Probe Catalog Diagnostic Formatter

## Summary

Implemented a caller-owned probe catalog diagnostic formatter for issue #769.
The formatter emits stable ASCII `PROBE_CATALOG` marker bytes for initialized
`ProbeDefinition` values and caller-provided catalogs copied with
`Probes.CopyProbeCatalog`.

## Why

Future serial/QEMU diagnostics need probe availability metadata without
allocating formatter-owned strings or duplicating individual probe getter calls.
This slice makes the metadata format explicit while preserving the boundary that
no probe is executed.

## Files changed

- `src/OS.Kernel/Diagnostics/ProbeCatalogDiagnosticFormatter.cs`
- `tests/OS.HostTests/DiagnosticsTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step809.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence

- `artifacts/logs/issue769-host-build.log`: host test project build passed with
  `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.
- `artifacts/logs/issue769-host-tests.log`: full host test suite passed with
  `TEST_PASS host.tests`.
- Required #769 markers passed:
  - `TEST_PASS probes catalog formatter formats all definitions`
  - `TEST_PASS probes catalog formatter reports destination capacity`
  - `TEST_PASS probes catalog formatter keeps dangerous probes disabled`

## Failures

None in this slice.

## Deferred work

This is host-tested probe metadata formatting only. It does not execute probes,
install IDT/GDT/TSS/IST, trigger null-pointer or invalid-opcode faults, prove
QEMU panic handling, access virtio devices, start hosted .NET, start ASP.NET
Minimal API/Kestrel, or prove cloud/production readiness.
