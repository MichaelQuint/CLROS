# Step 576 - Native app payload-header default-result guard

## Summary

Added explicit initialized-state gating to `NativeAppPayloadHeaderResult` so a
default native payload-header parse result no longer satisfies `Succeeded`.

## Why

The native payload header validates the small `OSAPPv0` metadata envelope before
launch-descriptor and payload-readiness planning. Because
`NativeAppPayloadHeaderStatus.Success` is zero-valued, a default result could
otherwise look like a valid parsed payload header before any package bytes had
been checked.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppPayloadHeader.cs`
- `tests/OS.HostTests/NativeAppPayloadHeaderTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step576.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel payload header`, `appmodel launch descriptor`,
  `TEST_PASS host.tests`, and `TEST_FAIL`.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START appmodel launch descriptor accepts package payload header
TEST_PASS appmodel launch descriptor accepts package payload header
TEST_START appmodel launch descriptor rejects invalid payload header
TEST_PASS appmodel launch descriptor rejects invalid payload header
TEST_START appmodel launch descriptor default result is not success
TEST_PASS appmodel launch descriptor default result is not success
TEST_START appmodel payload header accepts v0 native payload
TEST_PASS appmodel payload header accepts v0 native payload
TEST_START appmodel payload header rejects bad magic
TEST_PASS appmodel payload header rejects bad magic
TEST_START appmodel payload header rejects unsupported version
TEST_PASS appmodel payload header rejects unsupported version
TEST_START appmodel payload header rejects invalid entry offset
TEST_PASS appmodel payload header rejects invalid entry offset
TEST_START appmodel payload header default result is not success
TEST_PASS appmodel payload header default result is not success
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures

- None.

## Deferred work

- This is host-tested payload-header metadata only. It does not prove complete
  executable-format validation, payload mapping, native app execution, hosted
  .NET 10, Minimal API, Kestrel, cloud readiness, or production readiness.
