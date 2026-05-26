# Step 293 - Cloud Provider Kestrel Claim Gates

## What Changed

- Added explicit provider claim boundaries for `SingleMinimalApi` Kestrel
  evidence across local QEMU/libvirt, AWS Nitro, and Azure Hyper-V VM targets.
- Updated the Kestrel ledger so a QEMU/libvirt pass cannot be reported as AWS
  EC2 or Azure VM support.
- Added AWS claim-boundary language naming ENA, NVMe, DHCP IPv4, TCP listener,
  EC2 image/AMI evidence, and EC2 serial logs.
- Added Azure claim-boundary language naming Hyper-V, VMBus, NetVSC, StorVSC,
  DHCP IPv4, TCP listener, fixed VHD/image evidence, and Azure serial or boot
  diagnostic logs.
- Updated workload profile docs so provider scope is part of the
  `SingleMinimalApi` claim.
- Added host tests that protect the provider boundary text and emit the issue
  markers.

## Why It Changed

The OS goal requires ASP.NET Minimal API/Kestrel support, but cloud evidence is
provider-specific. Local QEMU/libvirt evidence is necessary for development, yet
it does not prove AWS Nitro's ENA/NVMe path or Azure's Hyper-V VMBus/NetVSC/
StorVSC path. This documentation/test guardrail keeps future issue closures from
collapsing those separate gates.

## Files Changed

- `docs/kestrel.md`
- `docs/workload-profiles.md`
- `docs/completion-gates.md`
- `docs/aws-cloud-requirements.md`
- `docs/azure-cloud-requirements.md`
- `tests/OS.HostTests/WorkloadProfileTests.cs`
- `done/step293.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

Evidence:

```text
Build succeeded. 0 Warning(s), 0 Error(s)
TEST_PASS workload profile single minimal api keeps provider gates distinct
TEST_PASS docs.kestrel cloud provider claim boundaries present
TEST_PASS host.tests
TEST_PASS naming.guard
TEST_PASS source.firewall
```

## Failures

No functional failures remain for this docs/test guardrail step.

`git diff --check` passed. It emitted line-ending normalization warnings for
existing LF files that Git will rewrite as CRLF when touched, but reported no
whitespace errors.

## Evidence Boundary

This is docs and host-test guardrail evidence only. It does not implement
hosted .NET 10, CoreCLR PAL integration, Kestrel in guest, ENA, NVMe, VMBus,
NetVSC, StorVSC, DHCP, TCP listener behavior, AWS AMI import, Azure VHD import,
or provider boot support.

## Deferred Work

- Add real QEMU/libvirt `SingleMinimalApi` Kestrel evidence after the hosted
  runtime, PAL, storage, networking, timers, and thread prerequisites exist.
- Add AWS Nitro Kestrel evidence only after ENA, NVMe, EC2 image, DHCP/TCP, and
  EC2 serial-log evidence exists.
- Add Azure VM Kestrel evidence only after Hyper-V/VMBus, NetVSC, StorVSC,
  DHCP/TCP, fixed VHD/image, and Azure boot diagnostic evidence exists.
- Keep `MultiMinimalApi` blocked until process/isolation/supervisor/crash
  isolation evidence exists on the selected provider.
