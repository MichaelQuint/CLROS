# Step 541 - VMBus Offer Default Result Guards

## Summary
- Added explicit initialized-state gating to `VmbusOfferParseResult` and
  `VmbusOfferCatalogParseResult`.
- Updated their success predicates so default/zero parse and catalog results
  cannot report success.
- Added host marker coverage for default parse/catalog result rejection while
  preserving valid NetVSC/StorVSC fixture parsing.
- Updated driver, cloud-provider, and completion-gate docs.

## Why
`VmbusOfferPlanStatus.Success` is zero, so default parse/catalog result structs
could previously satisfy status-only success checks without parser or catalog
initialization. The initialized-state guards prevent empty/default caller state
from becoming VMBus offer evidence.

## Files changed
- `src/OS.Kernel/Drivers/VmbusOfferPlan.cs`
- `tests/OS.HostTests/VmbusOfferPlanTests.cs`
- `docs/drivers.md`
- `docs/cloud-provider-requirements.md`
- `docs/completion-gates.md`
- `done/step541.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.vmbus.offer default results are not success`,
  `drivers.vmbus.offer parses network offer`, `TEST_PASS host.tests`, and
  failures.
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
TEST_START drivers.vmbus.offer parses network offer
TEST_PASS drivers.vmbus.offer parses network offer
TEST_START drivers.vmbus.offer default results are not success
TEST_PASS drivers.vmbus.offer default results are not success
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
- None for this slice.

## Deferred work
- This is host-tested VMBus offer fixture/caller-buffer parsing evidence only.
- No live Hyper-V setup, SynIC, VMBus negotiation, ring buffers, GPADL, NetVSC,
  StorVSC, Azure VM boot, cloud readiness, or production readiness is claimed.
