# Cloud Provider Requirements Traceability

## Current Status

This document reconciles the Azure and AWS requirement notes with the current OS milestone ledger. It is a traceability document only. It does not claim that OS boots on Azure or AWS.

Current provider status:

| Provider | Status | Boundary |
| --- | --- | --- |
| Azure | Not supported | No fixed VHD import, no Azure Compute Gallery validation, no Azure VM boot, no live Hyper-V VMBus, no NetVSC, no StorVSC, no Azure platform readiness flow. Host tests now cover VMBus offer descriptor fixtures for NetVSC/StorVSC class recognition only. |
| AWS | Not supported | No AMI import, no EC2 boot, no Nitro validation, no live PCIe device enumeration, no ENA, no NVMe, no live IMDSv2 flow. Host tests now cover IMDSv2 token/metadata/user-data request planning and default-deny app metadata policy fixtures only. |
| Local QEMU/OVMF | Partial local evidence only | The current QEMU smoke proves an EFI marker path and selected boot handoff markers. It is not provider boot evidence. |

The provider device split is a hard architectural boundary:

| Cloud | Minimum device substrate | Network | Storage |
| --- | --- | --- | --- |
| Azure | Hyper-V, Hyper-V CPUID/MSRs, hypercall page, SynIC, VMBus | NetVSC over VMBus | StorVSC over VMBus |
| AWS | Nitro, ACPI, PCIe, MSI/MSI-X, DMA-safe buffers | ENA PCIe device | NVMe PCIe device |

The current virtio planning work is useful for local QEMU milestones, but it is not a substitute for Azure VMBus/NetVSC/StorVSC or AWS Nitro/ENA/NVMe.

## Source Inputs

Primary source docs:

- `docs/azure-cloud-requirements.md`
- `docs/aws-cloud-requirements.md`
- `docs/completion-gates.md`
- `docs/kestrel.md`
- `docs/workload-profiles.md`
- `docs/cloud-import.md`
- `docs/app-model.md`
- `docs/networking.md`

Related provider-neutral gates:

- Gate 2: Boot
- Gate 5: UEFI encapsulation
- Gate 6: Platform
- Gate 7: Storage
- Gate 8: Networking
- Gate 9: Native-tier app model
- Gate 14: Hosted .NET 10 Minimal API/Kestrel
- Gate 11: Cloud image artifacts
- Gate 13: Security

## Traceability Matrix

| Requirement area | Azure requirement | AWS requirement | Current OS evidence | Completion gate | Gap / deferred work |
| --- | --- | --- | --- | --- | --- |
| Provider boot artifact | Fixed-size VHD, 1 MiB aligned, Azure Compute Gallery, `osType=Linux`, specialized image first, agent disabled. | HVM AMI, x86_64 first, EBS-backed root volume, boot mode metadata matching disk layout, ENA support flag for Nitro. | Local raw GPT/FAT32 image exists for QEMU. Optional qcow2 conversion wrapper exists. No provider image import has been tested. | Gate 11 | Add fixed VHD builder/import validation for Azure. Add AMI import/register path for AWS. Keep provider import evidence separate from local QEMU evidence. |
| Firmware boot path | Gen1 BIOS or Gen2 UEFI, one selected first. Long-term Azure path is Hyper-V-compatible boot with serial diagnostics. | Legacy BIOS or UEFI for x86_64 Nitro, one selected first. UEFI import requires fallback `EFI/BOOT/BOOTX64.EFI`. | QEMU/OVMF reaches current EFI markers and `OS_SMOKE_PASS`. This is not Azure or EC2 boot proof. | Gates 2, 5, 11 | Prove selected provider firmware path on real Azure VM and EC2 instance with serial logs. Do not infer provider boot from local OVMF. |
| Kernel substrate | x86-64 long mode, firmware memory map, paging, interrupts, timer, panic path, serial output. | x86-64 long mode first, ACPI, PCIe, interrupts, timer, serial output, panic path. | Boot handoff, memory-map ownership, ACPI RSDP provenance, and ordered boot markers are partially proven locally. No real ExitBootServices or owned driver substrate. | Gates 2, 3, 5, 6 | Implement post-firmware ownership, real descriptor/fault setup, timers, ACPI walking, and provider-safe serial diagnostics. |
| Platform bus and discovery | Hyper-V detection, MSRs, hypercall page, SynIC, VMBus negotiation, channel offers, ring buffers, GPADL/shared memory. | ACPI table walking, PCIe ECAM/live config access, BAR sizing/mapping, MSI/MSI-X, DMA-safe memory, Nitro device discovery. | ACPI/PCI parsers, virtio discovery contracts, Hyper-V CPUID snapshot identification with default-result rejection, and VMBus offer descriptor fixtures are host-tested over caller-provided bytes. No live Hyper-V or Nitro discovery. | Gate 6 | Add live Azure Hyper-V/VMBus negotiation and AWS Nitro PCIe tracks as separate issues. Do not collapse them into one generic virtio track. |
| Network device | NetVSC synthetic NIC over VMBus. | ENA PCIe NIC on Nitro. | Virtio-net metadata, queue planning, receive classification, and VMBus NetVSC offer class recognition are host-tested only. No live packet I/O. | Gate 8 | Implement live NetVSC for Azure and ENA for AWS after provider platform discovery exists. Local virtio-net can remain a QEMU milestone path. |
| Storage device | StorVSC synthetic SCSI over VMBus. | NVMe block driver for EBS and instance-store devices. | GPT/FAT32/package parser, virtio-blk planning contracts, and VMBus StorVSC offer class recognition are host-tested only. No live disk I/O after firmware. | Gate 7 | Implement live StorVSC and NVMe separately. Multi-app ASP.NET deployment practically requires storage-backed app manifests, binaries, config, and logs. |
| IPv4 network stack | Ethernet, ARP, IPv4, UDP, DHCP, TCP, DNS, loopback; Azure platform IP `168.63.129.16` optional for specialized image and required for no-agent generalized readiness. | Ethernet, ARP, IPv4, UDP, DHCP, TCP, DNS, loopback; IMDS endpoint optional for first boot, important for metadata/user-data/IAM later. | Packet primitives, DHCP/DNS/TCP/HTTP planners, and host-only PAL socket table are host-tested. No NIC-backed networking, sockets, timers, or live TCP listener. | Gate 8 | Wire packet stack to live device RX/TX, add timers/retransmission/listener state, then expose guest socket/PAL behavior. |
| Serial diagnostics | Azure serial console must carry boot and panic diagnostics. | EC2 serial console must carry boot and panic diagnostics. | Local QEMU serial marker capture exists. No provider serial retrieval has been validated. | Gates 2, 3, 11 | Preserve stable serial markers and prove retrieval through Azure and EC2 provider tooling. |
| Cloud metadata / readiness | Specialized no-agent image first. Generalized no-agent flow later requires WireServer goal-state GET and health POST to `168.63.129.16`. | Baked AMI with no SSM/cloud-init first. IMDSv2, user data, IAM credentials, and CloudWatch are later optional tracks. | Azure WireServer goal-state parsing, ready health-body formatting, request metadata, AWS IMDSv2 token/metadata/user-data request planning, and default-deny app metadata policy fixtures are host-tested only. No live provider metadata client or readiness flow exists. | Gates 8, 11, 13 | Keep metadata optional for first specialized/baked boot. Wire policy-mediated metadata access only after live networking and app supervisor boundaries exist. |
| TLS and ingress | Terminate TLS outside the VM first through Azure ingress or a separate terminator. | Terminate TLS outside the instance first through AWS ingress or a separate terminator. | No TLS stack, certificate store, ALPN, HTTP/2, or HTTPS evidence. | Gates 8, 10, 13 | Do not claim HTTPS. Plain HTTP Kestrel proof is the first hosted workload target. |
| Native-tier default app model | Single configured workload by default remains the early OS model. | Single configured workload by default remains the early OS model. | Host-tested native manifest/package/payload planning exists. No live app execution. | Gate 9 | Finish single native workload path before treating the OS as a provider multi-app ASP.NET platform. |
| ASP.NET Minimal API/Kestrel | Mandatory product goal for the CloudReady milestone, preferably Native AOT Minimal API apps. | Mandatory product goal for the CloudReady milestone, preferably Native AOT Minimal API apps or a documented custom OS target/shim. | `MinimalApiSmoke` is host-buildable only. `kestrel.smoke.package.v0` is a future contract. No in-guest hosted .NET 10 or Kestrel support; see [`docs/kestrel.md`](kestrel.md) for the smoke contract. | Gate 14 | Implement hosted runtime/PAL, sockets, timers, threading, package/config access, and an external HTTP probe before claiming Kestrel; provider-scoped claim details live in [`docs/completion-gates.md`](completion-gates.md#gate-14-provider-split). |
| Multi-app ASP.NET platform | Provider requirement docs describe multiple isolated ASP.NET Minimal API processes with a supervisor. | Provider requirement docs describe multiple isolated ASP.NET Minimal API processes with a supervisor. | No process model, per-process address spaces, scheduler-backed threads, app supervisor, restart policy, per-app ports, or per-app logs. | Gates 6, 8, 9, 10, 13 | Treat multi-app ASP.NET as a later platform track after single-app hosted proof. It requires process isolation and supervisor work beyond the early single-workload default. |
| Security posture | No SSH, no shell, no Azure agent/extensions initially. Per-app containment required for multi-app. | No SSH, no SSM/cloud-init/CloudWatch agent initially. Per-app metadata control required for multi-app. | Manifest validation and W^X planning are host-tested only. No live process isolation or metadata policy. | Gate 13 | Add kernel/user separation, per-process address spaces, validated syscall pointers, W^X/NX, per-app resource limits, and provider metadata mediation. |
| Operational import | Azure resource creation and Compute Gallery deployment are deployment-layer responsibilities. | S3/vmimport or AMI registration, VPC/security group, and EC2 launch are deployment-layer responsibilities. | `docs/cloud-import.md` says provider import is documentation-only and unvalidated. | Gate 11 | Add provider-specific import commands only with validation date, account constraints, artifact hash, and serial evidence. |

## Milestone Reconciliation

The early OS milestone model and the provider ASP.NET platform model are related, but they are not the same target.

### Track A: Current OS Bring-Up

This remains the base dependency chain:

1. Local boot and diagnostics.
2. UEFI ownership and post-firmware boundary.
3. Platform discovery.
4. Storage.
5. Networking.
6. Native-tier single workload.
7. Cloud image artifacts.
8. Hosted .NET 10 Minimal API/Kestrel.

This track keeps the product default narrow: a minimal cloud appliance with one configured workload by default and no general-purpose multi-user surface.

### Track B: Provider Hardware Compatibility

Provider hardware compatibility begins after the platform boundary is strong enough to support provider-specific device work.

Azure requires:

```text
Hyper-V detection
Hyper-V MSRs and hypercall page
SynIC
VMBus
NetVSC
StorVSC
Azure serial logs
specialized VHD import path
```

The optional generalized no-agent WireServer readiness path now has host-tested
fixtures for goal-state parsing and health-request formatting. Those fixtures do
not perform live HTTP, configure DHCP, reach `168.63.129.16`, or prove Azure
provisioning. They are useful only after the specialized no-agent image path and
network path are working.

The VMBus offer descriptor path now has host-tested fixtures for packed
channel-offer bytes supplied by tests or future platform code. The parser records
interface type GUID, instance GUID, child relation/channel ID, monitor ID,
sub-channel index, connection ID, monitor-notification flag, and bounded fixture
metadata length. It recognizes only the documented NetVSC
`f8615163-df3e-46c5-913f-f2d2f965ed0e` and StorVSC
`ba6163d9-04a1-4d29-b605-72e2ffb1dc7f` class GUIDs, rejects malformed lengths,
zero GUIDs, unsupported class GUIDs, zero channel IDs, duplicate channel IDs in a
fixture catalog, oversized or non-ASCII fixture metadata, and default parse or
catalog results as successful evidence. This is not live VMBus negotiation,
SynIC setup, ring-buffer allocation, GPADL setup, NetVSC initialization, StorVSC
initialization, Azure boot evidence, or provider readiness evidence.

The Hyper-V platform identification path is CPUID snapshot parsing evidence
only. Host tests reject default identification results as success, but no code
executes CPUID in the guest, reads Hyper-V MSRs, installs a hypercall page,
configures SynIC, negotiates VMBus, attaches NetVSC/StorVSC, boots on Azure, or
proves Azure support.

AWS requires:

```text
ACPI table walking
PCIe ECAM/live config access
BAR mapping
MSI/MSI-X
DMA-safe memory
ENA
NVMe
EC2 serial logs
HVM AMI import path
```

These are separate tracks. Azure support cannot be inferred from AWS Nitro work, and AWS support cannot be inferred from Azure Hyper-V work.

### Track C: `SingleMinimalApi` Hosted ASP.NET Proof

Kestrel/Minimal API support is mandatory for the product goal, but it is not
currently supported. The first hosted proof is the `SingleMinimalApi` profile:
one ASP.NET Minimal API workload, not a multi-app platform claim. The canonical
minimum proof and smoke markers live in
[`docs/completion-gates.md`](completion-gates.md#gate-14-profile-split) and
[`docs/kestrel.md`](kestrel.md#milestone-definition); this document only maps
that proof to provider substrates.

Provider traceability for `SingleMinimalApi` Kestrel remains:

- Local QEMU/libvirt: local guest boot plus the Kestrel smoke over the local
  virtualization network path only.
- Azure: the same guest Kestrel proof plus Azure image, Hyper-V/VMBus, NetVSC,
  StorVSC, DHCP/TCP, and Azure serial or boot diagnostic evidence.
- AWS: the same guest Kestrel proof plus AWS image/AMI, Nitro PCIe, ENA, NVMe,
  DHCP/TCP, and EC2 serial-log evidence.

Host loopback Kestrel, manifest listen metadata, PAL mock socket tests, `OSWorkloadProfile=SingleMinimalApi`, and profile-aware manifest validation are not enough to claim hosted in-guest Kestrel.

### Track D: `MultiMinimalApi` ASP.NET Platform

The Azure and AWS requirement docs describe a multi-app ASP.NET Minimal API platform. That maps to the `MultiMinimalApi` profile. It is a later expansion of the default appliance model, not a replacement for the early single-workload milestone.

The multi-app track requires:

```text
process table
per-process virtual address spaces
kernel/user privilege separation
per-process handle tables
preemptive scheduler
threads and futex/sync primitives
per-app environment/config
per-app port ownership
app supervisor
restart policy
stdout/stderr or log routing
per-app metadata access policy
per-app resource limits
```

Acceptance must prove at least two ASP.NET Minimal API apps running in separate processes on separate ports, with one app crash not corrupting the OS or the other app. Until that evidence exists, OS remains a single-workload-by-default system with future multi-app requirements documented.

## Claim Rules

Use these rules in issues, docs, and completion-gate updates:

1. Do not claim Azure boot support until a real Azure VM boot reaches the required serial markers from an Azure-compatible VHD or image version.
2. Do not claim AWS boot support until a real EC2 instance boot reaches the required serial markers from an HVM AMI.
3. Do not claim provider networking until the provider NIC driver, DHCP, routing, TCP listener, and external connection evidence all pass on that provider.
4. Do not claim provider storage until the provider storage driver can read the OS/app artifact and write logs or the selected storage scope is explicitly read-only.
5. Do not claim Kestrel support until an ASP.NET Minimal API workload responds
   to an external HTTP probe through the OS guest network path or a documented
   hosted-tier boundary; use
   [`docs/completion-gates.md`](completion-gates.md#gate-14-provider-split)
   and [`docs/kestrel.md`](kestrel.md#cloud-provider-claim-boundaries) for
   provider-scoped marker and evidence requirements.
6. Do not claim multi-app ASP.NET support until isolated processes, supervisor lifecycle, per-app ports, and crash isolation are proven.
7. Do not treat local QEMU virtio evidence as Azure Hyper-V or AWS Nitro evidence.

## Next Steps

1. Keep `docs/cloud-import.md` as the provider import status ledger and link provider validation there once real evidence exists.
2. Add Azure issue slices for fixed VHD packaging, Hyper-V/VMBus discovery, NetVSC, StorVSC, Azure serial evidence, and optional WireServer readiness.
3. Add AWS issue slices for AMI import, Nitro ACPI/PCIe discovery, ENA, NVMe, EC2 serial evidence, and optional IMDSv2 policy.
4. Keep the first hosted workload target to one Minimal API/Kestrel smoke before opening multi-app supervisor work.
5. Add multi-app process isolation and supervisor issues only after single app hosted execution has a tested boundary.
