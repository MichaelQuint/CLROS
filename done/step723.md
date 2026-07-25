# Step 723 - Shared ASCII classification migrations

## Summary

Added shared upper/lower ASCII letter predicates and migrated another set of
manifest, app-model, and HTTP parser validation paths onto the shared
`OS.Std.Ascii` helpers.

Completed Gitea slices:

- #674: `OS.Std.Ascii.IsUpperLetter` and `IsLowerLetter` for byte and char.
- #675: manifest parser listen-host and environment-key classification.
- #676: native app launch-plan listen-host classification.
- #677: HTTP request header token and decimal digit classification.
- #678: HTTP response status/content-length digit classification.

## Why

The CloudReady invariant requires shared string, numeric, memory, collection,
and BCL-like helper behavior to live in `std/no-runtime/shared` before kernel
or app code depends on it. These changes remove duplicated ASCII range checks
from kernel parsers while preserving the existing status-coded, allocation-free
parser behavior.

## Files changed

- `std/no-runtime/shared/Ascii.cs`
- `tests/OS.HostTests/StdAsciiTests.cs`
- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `src/OS.Kernel/Manifest/NativeAppLaunchPlan.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanTests.cs`
- `src/OS.Kernel/Net/HttpRequestParser.cs`
- `tests/OS.HostTests/HttpRequestParserTests.cs`
- `src/OS.Kernel/Net/HttpResponseParser.cs`
- `tests/OS.HostTests/HttpResponseParserTests.cs`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issues674-678-host-build-20260525.log`
  - Build succeeded with `0 Warning(s)` and `0 Error(s)`.
- `artifacts\logs\issues674-678-hosttests-20260525.log`
  - Includes `TEST_PASS std.ascii upper lower letter helpers`.
  - Includes `TEST_PASS manifest kernel uses shared ascii classification helpers`.
  - Includes `TEST_PASS appmodel launch plan uses shared ascii listen-host helper`.
  - Includes `TEST_PASS net.http.request uses shared ascii token helper`.
  - Includes `TEST_PASS net.http.response uses shared ascii digit helper`.
  - Ends with `TEST_PASS host.tests`.
- `artifacts\logs\issues674-678-git-diff-check-20260525.log`
  - `git diff --check` exited 0. Output contains only existing LF/CRLF
    working-copy warnings.

## Failures

- A transient local rebuild during #678 failed while `OS.HostTests` held the
  test output `OS.Kernel.dll` and while not every HTTP response parser digit
  call had been migrated. The integrated evidence run above passed after the
  remaining call sites were migrated and no stale test process remained.
- No real QEMU boot was run for this slice; this was host-tested
  std-helper/parser migration work only.

## Deferred work

- Continue migrating remaining local ASCII and byte classification helpers in
  storage, networking, manifest, and runtime formatting paths where the shared
  no-runtime contracts fit.
- Continue prioritizing live boot/runtime and driver slices after the small
  parser/helper cleanup backlog is exhausted or blocks on larger design work.
