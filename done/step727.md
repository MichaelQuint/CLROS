# Step 727 - Shared ASCII Space-Or-Tab Helper For HTTP OWS

## Summary

Implemented Gitea issue #693 as a focused Phase C/networking shared-helper
slice:

- added `OS.Std.Ascii.IsSpaceOrTab` overloads for `byte` and `char`;
- made `OS.Std.Text.AsciiString.TrimSpaceOrTab` depend on the shared helper;
- migrated `HttpRequestParser` leading header OWS scanning to the shared helper;
- added host tests and source guards for the helper, string trim, and HTTP
  parser migration.

## Why

HTTP request parsing had one remaining local space-or-tab comparison in the
header value path. Moving that classification into `std/no-runtime/shared`
keeps protocol parser behavior aligned with the repository rule that common
string/ASCII utility behavior is implemented in the shared no-runtime surface
before kernel and app usage.

## Files changed

- `std/no-runtime/shared/Ascii.cs`
- `std/no-runtime/shared/Text/AsciiString.cs`
- `src/OS.Kernel/Net/HttpRequestParser.cs`
- `tests/OS.HostTests/StdAsciiTests.cs`
- `tests/OS.HostTests/StdAsciiStringTests.cs`
- `tests/OS.HostTests/HttpRequestParserTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/networking.md`
- `done/step727.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build log: `artifacts\logs\issue693-host-build-20260525.log`
  - Result: `Build succeeded.`
  - Result: `0 Warning(s)`, `0 Error(s)`
- Host test log: `artifacts\logs\issue693-hosttests-20260525.log`
  - `TEST_PASS std.ascii space-or-tab helper`
  - `TEST_PASS std.ascii.string trim uses shared space-or-tab helper`
  - `TEST_PASS net.http.request uses shared ascii space-or-tab helper`
  - `TEST_PASS host.tests`
- Whitespace log before docs/done update:
  `artifacts\logs\issue693-git-diff-check-20260525.log`
  - Exit code: 0
  - Output contained only existing LF-to-CRLF working-copy warnings.

## Failures

- No test failures were observed for this slice.
- No QEMU boot, live NIC, live TCP, FetchApp, hosted .NET, ASP.NET Minimal API,
  or Kestrel evidence was produced.

## Deferred work

- `HttpRequestParser` remains a parser-only helper over caller-owned byte spans.
- HTTP OWS handling is host-tested only; it is not proof of live socket,
  virtio-net, TCP control-block, scheduler, hosted .NET, or Kestrel behavior.
- Broader `std/no-runtime/shared` BCL compatibility and runtime-helper work
  remains tracked by `docs/completion-gates.md` and the open Phase C/D issues.
