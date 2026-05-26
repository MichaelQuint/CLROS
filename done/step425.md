# Step 425 - Virtio-net RX classifier default-result guard

## Summary

Hardened `VirtioNetReceiveFrameClassificationResult` so a default/zero result
is not reported as successful. Classifier-created results now carry explicit
initialization provenance, and `Succeeded` requires that provenance in addition
to the existing success status.

## Why

The RX completion, slab-selection, dispatch, and network-dispatch paths depend
on receive-frame classification before interpreting caller-owned packet bytes.
Because the classifier success enum value is zero,
`default(VirtioNetReceiveFrameClassificationResult)` could previously satisfy
`Succeeded`. This step blocks that accidental promotion without changing the
existing ARP/IPv4 classification or failure-status behavior.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveFrameClassifier.cs`
  - Added `IsInitialized` to `VirtioNetReceiveFrameClassificationResult`.
  - Set the initialization bit in the private result constructor.
  - Updated `Succeeded` to require initialization plus success status.
- `tests/OS.HostTests/VirtioNetReceiveFrameClassifierTests.cs`
  - Added default-result regression coverage.
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
  - Documented the default-result guard and marker while preserving the
    host-tested caller-buffer parser/classifier evidence boundary.
- `done/step425.md`
  - Recorded this milestone.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.rx classifier default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host test project build: passed with 0 warnings and 0 errors.
- Host test run: passed with `TEST_PASS host.tests`.
- Filtered marker capture:
  - `TEST_PASS drivers.virtionet.rx classifier default result is not success`
  - `TEST_PASS host.tests`
- Full solution build: passed with 0 warnings and 0 errors.
- Naming/source guard:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- `git diff --check`: exit code 0. Git emitted CRLF normalization warnings
  for existing working-copy paths only.

## Failures

None.

## Deferred work

- Continue auditing neighboring virtio-net and storage result structs for
  default-success shapes in small issue-sized slices.
- Keep this evidence scoped to host-tested caller-buffer parsing and
  classification. It does not prove live virtio-net MMIO, DMA, interrupts,
  packet I/O, networking readiness, hosted .NET 10, ASP.NET Minimal
  API/Kestrel, cloud readiness, or production readiness.
