# Step 213 - Cloud Provider Requirements Traceability

## Summary

Added `docs/cloud-provider-requirements.md` as a standalone Azure/AWS traceability matrix for issue #234.

## Why

The provider requirement docs describe Azure and AWS cloud targets, while the current OS milestone ledger still has only local QEMU/OVMF and host-test evidence. The new matrix prevents future agents from treating provider requirements, virtio work, local QEMU smoke, or host-only Kestrel fixtures as Azure/AWS support.

## Files changed

- `docs/cloud-provider-requirements.md`
- `done/step213.md`

## Tests run

- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- The traceability doc separates Azure Hyper-V/VMBus/NetVSC/StorVSC from AWS Nitro/PCIe/ENA/NVMe.
- The doc states that Azure boot, AWS boot, provider import, provider networking, provider storage, and provider metadata/readiness flows are not currently supported.
- The doc states that ASP.NET Minimal API/Kestrel is a mandatory product goal but is not currently supported in guest.
- The milestone reconciliation keeps early OS native-tier work single-workload by default and treats the multi-app ASP.NET platform as a later track requiring process isolation, scheduling, supervisor, per-app ports, logs, and metadata policy.

## Failures

- None for this documentation-only slice.

## Deferred work

- Provider validation remains open for parent integration.
- Azure fixed VHD, VMBus, NetVSC, StorVSC, and Azure serial evidence are separate implementation tracks.
- AWS HVM AMI import, Nitro PCIe discovery, ENA, NVMe, and EC2 serial evidence are separate implementation tracks.
- Hosted .NET 10/Kestrel and multi-app ASP.NET supervisor/process-isolation work remain future milestones.

