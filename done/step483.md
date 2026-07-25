# Step 483 - NativeAOT heap bootstrap default guard

## Summary

Implemented Gitea issue #468 by making `NativeAotHeapBootstrapPlanResult`
reject default/zero values as successful runtime heap-bootstrap evidence.

## Why

`NativeAotHeapBootstrapPlanStatus.Success` is zero. The heap-bootstrap result
previously reported success for `default(NativeAotHeapBootstrapPlanResult)`
because `Succeeded` only checked status. Runtime readiness evidence depends on
an initialized allocator-state-derived plan, requested heap-page metadata,
remaining free-page metadata, and object-alignment metadata, so uninitialized
all-zero state must not count as a successful plan.

## Files changed

- `src/OS.Kernel/Runtime/NativeAotHeapBootstrapPlan.cs`
- `tests/OS.HostTests/NativeAotHeapBootstrapPlanTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step483.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `runtime.nativeaot heap bootstrap default result is not success`
  and `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START runtime.nativeaot heap bootstrap default result is not success
TEST_PASS runtime.nativeaot heap bootstrap default result is not success
TEST_PASS host.tests

dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall

git diff --check
exit 0; existing LF-to-CRLF working-copy warnings only
```

## Failures

- None.

## Deferred work

- This is host-tested runtime prerequisite metadata only. It does not reserve
  heap pages, install a managed heap, allocate objects or arrays, implement GC
  root scanning, implement write barriers, start hosted .NET 10, support
  ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove production
  readiness.
