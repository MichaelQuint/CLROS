# Step 565 - Native app selection scratch default-result guard

## Summary

Added explicit initialized-state gating to
`NativeAppSelectionBootScratchPlanResult` so a default scratch-plan result no
longer satisfies `Succeeded`.

## Why

The scratch-plan result records future boot-owned buffer requirements for GPT,
manifest, and package selection. Because the success enum value is zero, a
default result could otherwise look like a completed scratch validation before
any caller-owned buffer sizing had run.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppSelectionBootScratchPlan.cs`
- `tests/OS.HostTests/NativeAppSelectionBootScratchPlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step565.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel.selectionscratch`, `TEST_PASS host.tests`, and
  `TEST_FAIL`.
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
TEST_START appmodel.selectionscratch accepts default image buffers
TEST_PASS appmodel.selectionscratch accepts default image buffers
TEST_START appmodel.selectionscratch rejects short manifest buffer
TEST_PASS appmodel.selectionscratch rejects short manifest buffer
TEST_START appmodel.selectionscratch rejects short package buffer
TEST_PASS appmodel.selectionscratch rejects short package buffer
TEST_START appmodel.selectionscratch rejects short gpt entry buffer
TEST_PASS appmodel.selectionscratch rejects short gpt entry buffer
TEST_START appmodel.selectionscratch rejects invalid requested sizes
TEST_PASS appmodel.selectionscratch rejects invalid requested sizes
TEST_START appmodel.selectionscratch default result is not success
TEST_PASS appmodel.selectionscratch default result is not success
TEST_START appmodel.selectionscratch preserves no-live-storage boundary
TEST_PASS appmodel.selectionscratch preserves no-live-storage boundary
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

- This is host-tested scratch-plan metadata safety only. It does not allocate
  boot-owned buffers, read storage, parse manifests or packages, execute native
  app code, host .NET 10, support Kestrel, prove cloud readiness, or prove
  production readiness.

