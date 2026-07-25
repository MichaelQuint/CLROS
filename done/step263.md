# Step 263 - Azure Hyper-V VMBus Offer Descriptor Fixtures

## What Changed

- Added `VmbusOfferPlan`, a status-coded parser for host-test VMBus
  channel-offer descriptor fixtures.
- Added host tests for StorVSC and NetVSC class recognition, malformed offer
  rejection, unsupported class GUID rejection, duplicate channel ID rejection,
  and fixture metadata bounds.
- Updated Azure/Hyper-V traceability in `docs/cloud-provider-requirements.md`
  to record the fixture-only evidence boundary.

## Why It Changed

Issue #263 needed deterministic Azure Hyper-V planning evidence before live
VMBus negotiation exists. This gives later platform code a small parser boundary
for caller-provided channel-offer bytes without claiming SynIC setup, VMBus
connection establishment, GPADL setup, ring-buffer ownership, NetVSC, StorVSC,
or Azure boot support.

## Files Changed

- `src/OS.Kernel/Drivers/VmbusOfferPlan.cs`
- `tests/OS.HostTests/VmbusOfferPlanTests.cs`
- `docs/cloud-provider-requirements.md`
- `done/step263.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\OS.slnx --nologo -m:1
```

## Logs And Evidence

`dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
passed in the integrated tree.

`dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
passed and ended with:

```text
TEST_PASS drivers.vmbus.offer parses storage offer
TEST_PASS drivers.vmbus.offer parses network offer
TEST_PASS drivers.vmbus.offer rejects malformed offer
TEST_PASS drivers.vmbus.offer rejects unsupported class
TEST_PASS drivers.vmbus.offer rejects duplicate channel ids
TEST_PASS drivers.vmbus.offer rejects metadata bounds
TEST_PASS host.tests
```

## Failures

No current verification failures for this fixture-parser slice. An earlier
unrelated parent-tree ImageBuilder test edit temporarily blocked the full
solution build, but the integrated tree now builds with `0 Warning(s)` and
`0 Error(s)`.

## Evidence Boundary

This step is fixture parsing only:

- no live Hyper-V CPUID or MSR probing;
- no hypercall page setup;
- no SynIC setup;
- no VMBus connection establishment;
- no GPADL/shared-memory setup;
- no ring-buffer allocation;
- no NetVSC protocol negotiation or packet I/O;
- no StorVSC protocol negotiation or disk I/O;
- no Azure VHD import, VM boot, serial-log retrieval, or WireServer evidence.

## Deferred Work

- Feed real VMBus channel-offer messages into this parser only after Hyper-V
  detection, SynIC, and VMBus negotiation have status-coded boundaries.
- Add live channel registry and rescind handling after offer parsing has a real
  source.
- Implement NetVSC and StorVSC as separate driver tracks after live VMBus
  channel setup, GPADL, and ring buffers exist.
