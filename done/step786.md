# Step 786 - FetchApp request endpoint shared buffer copy

## Summary

Migrated the FetchApp HTTP request endpoint copier onto the shared
`OS.Std.Memory.BufferOperations.MemoryCopy` helper and added a source-guard host
test marker for that contract.

## Why

CloudReady networking code should use the `std/no-runtime/shared` memory surface
instead of ad hoc span copy calls. This keeps packet/request staging aligned with
the C# no-runtime utility boundary and makes future kernel usage easier to audit.

## Files changed

- `src/OS.Kernel/Net/FetchAppHttpRequestEndpoint.cs`
  - uses `OS.Std.Memory.BufferOperations.MemoryCopy` for host and path copies;
  - maps any impossible helper copy failure to the existing destination-too-small
    failure path.
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
  - adds `TEST_PASS fetch.http request endpoint copy uses shared buffer operations`;
  - source-guards the helper import and both host/path copy calls.
- `docs/networking.md`
  - records the #744 host-tested networking evidence boundary.
- `done/step786.md`
  - records this milestone and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:BaseIntermediateOutputPath=..\..\artifacts\issue744-fetch-endpoint-bufferops\obj\ -p:OutputPath=..\..\artifacts\issue744-fetch-endpoint-bufferops\bin\`
- `dotnet .\artifacts\issue744-fetch-endpoint-bufferops\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence

- `artifacts/logs/issue744-host-build-20260525.log`
  - host test project build completed with 0 warnings and 0 errors.
- `artifacts/logs/issue744-hosttests-20260525.log`
  - `TEST_PASS fetch.http request endpoint copy uses shared buffer operations`
  - `TEST_PASS host.tests`

## Failures

- No #744 implementation failures were observed.
- This is host-tested contract evidence only. It is not QEMU boot, live
  virtio-net, DHCP, DNS, TCP, or HTTP GET execution evidence.

## Deferred work

- Continue replacing networking packet/request direct copies with
  `std/no-runtime/shared` helpers in smaller audited slices.
- Live virtio-net execution and FetchApp network completion remain future
  CloudReady gates.

## Evidence boundary

The FetchApp endpoint host/path staging path now has host source-guard evidence
for shared buffer helper use. CloudReady networking remains incomplete until
QEMU/libvirt device, DHCP, DNS, TCP, and HTTP GET evidence exists.
