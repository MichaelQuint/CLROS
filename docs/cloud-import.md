# Cloud Import

## Current Status

Cloud import is documentation-only and unvalidated. A local raw GPT/FAT32 image artifact exists at `artifacts/images/os.raw`, `tools/OS.ImageBuilder convert-qcow2` can wrap `qemu-img` to create `artifacts/images/os.qcow2` when the host has `qemu-img`, `dotnet run --project tools/OS.Tooling -- qemu qcow2` runs local QEMU/OVMF smoke for the converted image, `convert-azure-vhd` can plan or run a fixed VHD conversion, and `plan-aws-ami` can validate first-target AWS AMI import metadata in dry-run mode. No provider import flow has been tested.

## Implemented Behavior

- `tools/OS.ImageBuilder` creates a local raw image for QEMU/libvirt-oriented smoke paths.
- `tools/OS.ImageBuilder convert-qcow2 --input artifacts/images/os.raw --output artifacts/images/os.qcow2 [--qemu-img <path>] [--dry-run]` validates raw input, discovers `qemu-img` from an explicit path or PATH, and emits deterministic dry-run markers without requiring `qemu-img`.
- `dotnet run --project tools/OS.Tooling -- qemu qcow2` coordinates raw-image creation, qcow2 conversion, and local QEMU/OVMF smoke for the converted qcow2 artifact. `--dry-run` plans the conversion and runner; only real execution with captured guest markers is local boot evidence. Neither mode proves cloud provider import.
- `tools/OS.ImageBuilder convert-azure-vhd --input artifacts/images/os.raw --output artifacts/images/os.vhd [--qemu-img <path>] [--dry-run]` validates raw input, enforces Azure's 1 MiB virtual-size alignment requirement, and emits a fixed VHD `qemu-img` command shape using `-O vpc -o subformat=fixed,force_size`.
- `OS.Cloud.AzureWireServerGoalStateParser`,
  `AzureWireServerHealthFormatter`, and `AzureWireServerRequestPlanner`
  provide host-tested fixtures for the optional generalized no-agent Azure
  WireServer flow. They parse the goal-state XML fields, format the ready
  health body, and expose request metadata for
  `GET /machine?comp=goalstate` and `POST /machine?comp=health`.
- `OS.Cloud.AwsImdsV2RequestPlanner` and
  `AwsImdsAppMetadataPolicyPlanner` provide host-tested fixtures for the
  optional EC2 IMDSv2 path. They plan token, metadata, and user-data request
  metadata for `169.254.169.254` and enforce a default-deny,
  supervisor-mediated app metadata policy.
- `tools/OS.ImageBuilder plan-aws-ami --input artifacts/images/os.raw --format raw --architecture x86_64 --virtualization hvm --root-device ebs --boot-mode uefi --ena-support true [--dry-run]` validates the first AWS Nitro metadata target and rejects non-HVM, ARM64, missing ENA support, or legacy BIOS metadata for the current UEFI raw image layout.
- `tools/OS.LibvirtRunner` can generate deterministic libvirt domain XML for raw or qcow2 images, emit optional dry-run path diagnostics, emit a plan-only future real-libvirt lifecycle contract, and validate an already-captured serial log with `check-serial`, but it does not call libvirt or a cloud provider.
- `dotnet run --project tools/OS.Tooling -- libvirt smoke` records raw/qcow2 libvirt smoke intent, writes a domain XML artifact, can write a lifecycle-plan artifact through `--plan-lifecycle`, can validate an existing serial log through `--check-serial-only`, and emits deterministic dry-run, lifecycle-plan, check-serial, or missing-`virsh` markers without claiming a boot.

## Unsupported Behavior

AWS, GCE, Azure, and generic provider imports are not validated. The current raw, local qcow2 smoke, Azure fixed VHD command plan, Azure WireServer parser/formatter fixtures, AWS IMDSv2 request/policy fixtures, AWS AMI metadata plan, and libvirt serial-log preflight parser are local smoke/import-prep artifacts only; provider-specific validation is pending.

## Tests

No provider cloud tests exist. Host tests cover local image construction, qcow2 wrapper dry-run/failure contracts, Azure fixed VHD dry-run/failure contracts, Azure WireServer goal-state/health request fixtures, AWS IMDSv2 request/policy fixtures, AWS AMI metadata dry-run/failure contracts, and libvirt XML/dry-run/lifecycle-plan/check-serial/missing-tool diagnostics only. Local qcow2 QEMU smoke on 2026-05-22 emitted `TEST_PASS qemu.qcow2.smoke`, and `artifacts/logs/qemu-smoke-qcow2.summary.log` reported `MISSING_MARKERS none`.

Azure WireServer fixture markers:

```text
TEST_PASS cloud.azure.wireserver parses goal state
TEST_PASS cloud.azure.wireserver rejects missing ids
TEST_PASS cloud.azure.wireserver formats ready health body
TEST_PASS cloud.azure.wireserver plans goalstate and health requests
```

AWS IMDSv2 fixture markers:

```text
TEST_PASS cloud.aws.imds plans token request
TEST_PASS cloud.aws.imds plans metadata get with token
TEST_PASS cloud.aws.imds rejects unsafe metadata request
TEST_PASS cloud.aws.imds denies app by default
TEST_PASS cloud.aws.imds allows supervisor mediated request
```

## Known Limitations

Do not claim provider compatibility until provider import has been tested or explicitly marked unvalidated. Local raw/qcow2 QEMU boot evidence, Azure fixed VHD command planning, Azure WireServer parser/formatter fixtures, AWS IMDSv2 request/policy fixtures, AWS AMI metadata planning, and libvirt dry-run/lifecycle-plan/check-serial/missing-tool diagnostics are not provider import evidence.

## Next Steps

1. Add provider-specific Azure and AWS import command examples once credentials, accounts, regions, and artifact hosting assumptions are documented.
2. Add libvirt real boot for raw and qcow2 artifacts where libvirt is available.
3. Add provider-specific import commands after artifact generation.
4. Record validation date and account/provider constraints.
