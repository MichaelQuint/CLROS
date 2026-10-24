# Documentation Map

[Step1577](../done/step1577.md) records full value-only disk/static replay.
Storage owns physical inventory/data/metadata interval contracts; QEMU owns the
report-only CLI and still-pending complete original execution consumer. App and
Kestrel docs retain the unfinished real Blazor/browser boundary.

[Step1576](../done/step1576.md) records original disk HTTP diagnostics and a
value-only physical inventory. Storage owns metadata/sector joins; QEMU owns
the intentionally failed complete-consumer boundary; app/Kestrel docs retain
the unfinished framework/browser qualification.

[Step1572](../done/step1572.md) records disk static-server diagnostic integration.
Boot owns its existing disk gate; storage owns deferred file observations; QEMU
owns the still-failing complete-consumer boundary. No app/gate is qualified.

[Step1571](../done/step1571.md) records three fresh complete original mechanical
disk captures. Storage and QEMU own that positive read boundary; Kestrel/app
model own the still-pending disk HTTP/Blazor demonstration. Earlier failed
originals and the outstanding batch/native/error/concurrency limits remain.

For the October 23 accepted state, start with [README](../README.md),
[current completion status](completion-gates.md#current-acceptance-october-23-2026)
and [step1556](../done/step1556.md). The live merge/tracker readback is documented
in [step1557](../done/step1557.md). The [Kestrel ledger](kestrel.md) owns finite
service recipes; the [workload roadmap](workload-profiles.md) owns remaining
dependencies. Dated development paragraphs and immutable steps retain their
original failures and limits; they are not current ownership instructions.

[Step1564](../done/step1564.md) records isolated read-service ownership, pending
disk waits, sector observation controls, deterministic real-candidate image
results and EFI C# compilation. Storage/boot/order/diagnostics own the added
contracts; QEMU owns current combined-profile selection. Native and guest disk
qualification remain required. Step1562 owns the reusable core/adapter decision;
step1563 owns the later image-packaging roadmap.
[Step1570](../done/step1570.md) adds the exact mechanical capture value joins
and original-runner candidate. Storage owns file/window semantics, QEMU owns
the report-only commands and fresh-capture boundary, and EFI build owns the
limited native inventory. Original r08 remains a failed outer command.

The NativeAOT-first architecture and ordered issue roadmap are canonical in
`docs/workload-profiles.md`; `.gitea/backlog/clros-issues.json` owns exact
dependencies and ticket acceptance. Existing app-model, PAL, manifest,
Kestrel, security and completion-gate docs describe their boundaries.
Do not create another competing roadmap or restore CoreCLR-first ordering
from older historical evidence.

## Current Status

The shared no-runtime host project lives at
`src/OS.Std.NoRuntime.Shared/OS.Std.NoRuntime.Shared.csproj`; linked source
remains at its audited `std/no-runtime/shared` paths. Its source-local guide
still governs implementation. This layout change does not change EFI sources.

Repository documentation is the source of truth for code-coupled behavior,
build/run instructions, ABI contracts, completion gates, subsystem status, and
evidence. The Gitea wiki, if maintained, is for reader-friendly summaries that
link back to repo docs and issues.

This map defines where canonical information belongs and when agents must update
each document. It does not move any canonical build, ABI, gate, or evidence
material out of the repository.

Current automation uses `dotnet run --project tools/OS.Tooling -- <command>`.
Historical evidence paragraphs and immutable `done` files may name the removed
PowerShell scripts that produced those captures. Those names identify past
provenance; current runnable examples use the compiled commands.

## Implemented Behavior

Canonical repository docs:

| Topic | Canonical repo source | Update trigger |
| --- | --- | --- |
| Agent operating rules | `AGENTS.md` | Project-wide invariants, required reads, evidence workflow, or issue workflow changes |
| Documentation ownership and wiki split | `docs/documentation-map.md` | Any change to where repo or wiki content should live |
| Quick project status and common commands | `README.md` | Current status, first-run command set, or high-level limits change |
| Repository audit and evidence snapshot | `docs/repo-audit.md` | Top-level implementation status, evidence boundary, or audit summary changes |
| Host prerequisites | `docs/host-requirements.md` | Required SDK, Windows, Visual Studio, QEMU, OVMF, or toolchain changes |
| Host test runner and shared expectations | `docs/host-requirements.md`, `tests/AGENTS.override.md` | Test discovery, lanes, filters, result markers, build freshness, or tooling migration changes |
| Windows development-machine handoff | `docs/windows-handoff.md` | Final source/ref/evidence transfer, destination verification, or tracker endpoint instructions change |
| Architecture overview | `docs/architecture.md` | Cross-subsystem architecture, milestone ordering, or workload-profile boundaries change |
| Completion gates and claim boundaries | `docs/completion-gates.md` | Any gate status, evidence, or remaining-work boundary changes |
| Boot and boot order | `docs/boot.md`, `docs/boot-order.md`, `docs/efi-build.md` | EFI, boot marker, handoff, memory-map, `ExitBootServices`, or build path changes |
| Diagnostics and probes | `docs/diagnostics.md` | Panic, fault, serial diagnostics, probe toggle, or marker changes |
| NativeAOT/no-stdlib/runtime limits | `docs/nativeaot-nostdlib-limits.md` | Runtime helpers, no-stdlib stubs, shared BCL-like API, or freestanding limits change |
| PAL and hosted runtime boundary | `docs/pal.md`, `work/PAL/pal-design.md`, `docs/adr-pal-abi-naming.md` | `OSHost_*` ABI, platform interface, hosted-runtime, POD layout, or status contract changes |
| Platform and drivers | `docs/drivers.md`, provider/device plan docs | ACPI, PCI, virtio, ENA, VMBus, MMIO, timer, framebuffer, or device-readiness changes |
| Storage and image artifacts | `docs/cstorage.md`, `docs/image-builder.md`, `docs/cloud-import.md` | GPT/FAT/package, virtio-blk, raw/qcow2/ISO, image-builder, or cloud-import behavior changes |
| Networking | `docs/networking.md` | Ethernet, ARP, IPv4, DHCP, DNS, TCP, HTTP, virtio-net, socket, or Kestrel socket ladder changes |
| App model and manifest | `docs/app-model.md`, `docs/manifest.md`, `docs/workload-profiles.md` | Manifest schema, package/app launch, workload profile, lifecycle, or supervisor changes |
| Kestrel milestone ledger | `docs/kestrel.md` | Minimal API fixture, hosted .NET, PAL prerequisite, runner HTTP probe, or Kestrel claim-boundary changes |
| QEMU and libvirt | `docs/qemu.md`, `docs/libvirt.md` | Runner commands, required markers, dry-run behavior, serial summaries, or real boot evidence changes |
| Cloud provider requirements | `docs/cloud-provider-requirements.md`, `docs/aws-cloud-requirements.md`, `docs/azure-cloud-requirements.md` | Provider-specific device, image, metadata, networking, or Kestrel claim scope changes |
| Security and license | `docs/security.md`, `docs/license-audit.md` | Secret handling, W^X/NX, metadata policy, release diagnostics, or license/provenance changes |
| Reference repositories | `docs/reference-map.md`, `docs/fork-diff.md` | Reference inventory, allowed use, provenance, or comparison notes change |
| Issue taxonomy and synchronization | `.gitea/backlog/clros-issues.json`, `tools/OS.Tooling/BacklogSync.cs`, `docs/issue-labels.md` | Issue intent, dependencies, labels, desired state, or backlog workflow changes |
| Evidence history | `done/stepNN.md` | Every meaningful implementation, test, evidence, or documentation milestone |

Directory-local guides:

| Path | Purpose | Keep only when |
| --- | --- | --- |
| `apps/AGENTS.override.md` | App fixture and workload-example rules | App projects need local rules for host-only fixtures, manifest behavior, or Kestrel claim boundaries |
| `references/AGENTS.override.md` | Reference repository safety rules | References remain read-mostly and excluded from first-party implementation edits |
| `scripts/AGENTS.override.md` | Migration notice for historical script paths | Historical evidence refers to removed scripts |
| `src/OS/AGENTS.override.md` | Freestanding no-stdlib EFI artifact rules | The EFI project remains subject to stricter runtime/link rules while included in `CLROS.slnx` |
| `src/OS.Kernel/AGENTS.override.md` | Kernel-contract implementation rules | Kernel-facing code needs constrained NativeAOT/status-code/marker guidance |
| `src/OS.Std.NoRuntime.Shared/AGENTS.override.md` | Host project linking audited shared source | The host project must not duplicate implementations or leak framework inputs into EFI |
| `std/no-runtime/shared/AGENTS.override.md` | Shared no-runtime utility rules | Shared BCL-like work needs stricter compatibility and no-runtime guidance |
| `tests/AGENTS.override.md` | Host test marker and determinism rules | Host tests remain the first evidence layer for contract behavior |
| `tools/AGENTS.override.md` | Host tool command/output rules | Tooling remains host-only and must preserve deterministic machine-readable output |
| `work/PAL/AGENTS.override.md` | Extra PAL/hosted-runtime ABI design rules | The directory needs local rules beyond root `AGENTS.md`; otherwise fold changes into `docs/pal.md` or this map |

Optional Gitea wiki summaries:

| Wiki page idea | Allowed content | Required links back |
| --- | --- | --- |
| Product overview | Reader-friendly goal, current milestone, non-goals | `README.md`, `docs/architecture.md`, `docs/completion-gates.md` |
| Roadmap snapshot | Short phase summary and next ready issues | `docs/completion-gates.md`, Gitea issues |
| Onboarding | High-level checkout/build walkthrough | `README.md`, `docs/host-requirements.md`, `AGENTS.md` |
| Architecture diagrams | Simplified boot/PAL/storage/network diagrams | Matching subsystem docs |
| FAQ | Short explanations for common status and claim-boundary questions | `docs/completion-gates.md`, relevant subsystem docs |
| Current milestone snapshot | Human-readable summary of latest evidence | Latest `done/stepNN.md`, `docs/completion-gates.md` |

## Unsupported Behavior

The wiki must not be the only source for:

- Build or smoke-run commands.
- Host prerequisites.
- `OSHost_*` ABI contracts.
- Boot order or marker contracts.
- Completion gates or claim boundaries.
- Test evidence and `done/stepNN.md` history.
- Security rules, source rules, or secret-handling policy.

Do not delete `done/stepNN.md` history during documentation consolidation.

## Tests

Docs-only consolidation should run:

```powershell
git diff --check
```

When code, project files, scripts, or behavior change, also run the focused
build/test commands named in `AGENTS.md` and the affected subsystem doc.

Useful documentation spot checks:

```powershell
rg -n "Documentation Map|Gitea wiki|completion-gates|Kestrel support|OSHost_\\*" .\AGENTS.md .\README.md .\docs
rg -n "SharpOS" .\AGENTS.md .\README.md .\docs .\src .\std .\apps .\tools .\tests
```

The second command may find deliberate reference-repository context in docs, but
first-party project naming must remain `CLROS`. The stable `OS` assembly,
namespace, source-path and hosted-ABI prefixes retain their documented identities.

## Known Limitations

Some subsystem docs intentionally contain repeated evidence-boundary language
because those warnings need to be visible where agents work. Prefer linking to
`docs/completion-gates.md` for full gate detail, but keep local warnings when
they prevent false claims about Kestrel, hosted .NET, real firmware shutdown,
drivers, storage, networking, or production readiness.

This map does not rewrite existing subsystem docs. It gives future agents a
single place to decide where a documentation update belongs.

## Next Steps

1. Keep root `AGENTS.md` compact and move subsystem detail into the matching
   docs instead of expanding the root guide again.
2. If a wiki is created, make each page summary-first and link back to the
   canonical repo docs listed above.
3. Review directory-local override guides periodically and remove any that no
   longer add local rules beyond the root contract.
