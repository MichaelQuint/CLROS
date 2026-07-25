# Step 237 - Azure WireServer Fixture Planner

## Summary

Added host-tested Azure WireServer goal-state parsing, ready health-body
formatting, and request metadata planning fixtures.

## Why

Issue #237 tracks the optional generalized no-agent Azure readiness path. The
first Azure target remains specialized/no-agent, but the WireServer parser and
formatter can be host-tested safely now without making a live Azure networking
or provisioning claim.

## Files changed

- `src/OS.Kernel/Cloud/AzureWireServerGoalState.cs`
- `tests/OS.HostTests/AzureWireServerGoalStateTests.cs`
- `docs/cloud-import.md`
- `docs/cloud-provider-requirements.md`
- `done/step237.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence

- Solution build passed with `Build succeeded.`, `0 Warning(s)`, and
  `0 Error(s)`.
- The full host harness exited 0 and emitted:
  - `TEST_PASS cloud.azure.wireserver parses goal state`
  - `TEST_PASS cloud.azure.wireserver rejects missing ids`
  - `TEST_PASS cloud.azure.wireserver formats ready health body`
  - `TEST_PASS cloud.azure.wireserver plans goalstate and health requests`
  - `TEST_PASS host.tests`

## Failures

- No current verification failures for this fixture slice.

## Deferred work

- No live HTTP call is made.
- No DHCP, route, DNS, Azure platform IP reachability, WireServer POST, Azure
  provisioning, Azure VM boot, or Azure Compute Gallery import is claimed.
- App access to provider metadata remains a future supervisor/security policy
  concern.
