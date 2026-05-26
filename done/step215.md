# Step 215 - AWS Nitro ENA/NVMe PCI Discovery Plan

## Summary

Added the AWS Nitro ENA/NVMe PCI discovery research note for issue #239.

## Why

AWS Nitro needs a PCIe device plan that is separate from the existing virtio
and Azure VMBus tracks before ENA and NVMe driver work starts.

## Files changed

- `docs/aws-nitro-device-plan.md`
- `done/step215.md`

## Tests run

- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- `docs/aws-nitro-device-plan.md` defines x86_64 Nitro-first scope,
  ACPI/PCIe prerequisites, ENA and NVMe separation from virtio and Azure
  devices, driver initialization boundaries, host-testable first slices, and
  follow-up issues.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0.
- Added non-closing Gitea evidence comment
  `http://192.168.2.42:3000/michael.quint/OS/issues/239#issuecomment-15198`.

## Failures

- None.

## Deferred work

- Create or update follow-up implementation issues for the ENA PCI candidate
  parser and NVMe PCI candidate/register-map constants.
- Add a shared PCI MSI-X capability parser issue if no existing issue owns it.
- Leave issue #239 open for parent integration.

