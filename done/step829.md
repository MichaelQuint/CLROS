# Step 829 - M00-005 Image and Runtime Test Locators

## Summary

Migrated image-builder and NativeAOT boundary tests to the shared CLROS locator.

## Why

The tests previously encoded the obsolete solution name independently.

## Files changed

- `tests/OS.HostTests/ImageBuilderTests.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `tests/OS.HostTests/NoStdLibShimBoundaryTests.cs`
- `docs/image-builder.md`
- `docs/nativeaot-nostdlib-limits.md`

## Tests run

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

The affected tests complete in the integrated host baseline in Step 845.

## Failures

No runtime behavior was changed while repairing repository discovery.

## Deferred work

Live NativeAOT runtime correctness remains M04 work.

## Evidence boundary

This proves host-tested contracts and root discovery only.
