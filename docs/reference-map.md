# Reference Map

## Current Status

The reference snapshots requested for CloudReady OS planning are present under
`references/`. They are read-only design, implementation, and tooling references;
none of them are first-party source, none are compiled by `OS.slnx`, and none are
replacement architectures for this repository.

`references/AGENTS.override.md` is the local safety rule for the directory:
agents may inspect and compare references, but must not edit them, add them to
the solution, or replace OS architecture with a reference architecture unless a
future task explicitly authorizes that work.

License and provenance remain implementation gates. The user has authorized
reference use for this project, but source import still requires clear
provenance, license notes in `docs/license-audit.md`, and reimplementation in OS
style where practical. Several local snapshots do not expose a top-level license
file in the checked tree, so do not assume copying rights from local presence
alone.

## Implemented Behavior

This document records the allowed use for each named reference. Current behavior
is documentation-only; no code, build, or package integration is implied.

| Reference | Local path / source | How OS should use it | Boundaries and caveats |
| --- | --- | --- | --- |
| Nanos | `references/nanos-master` | Product and deployment model reference: single workload, cloud image mindset, manifest/config packaging, release layout, QEMU/KVM/cloud workflow, minimal attack surface. | Do not port C code. Do not add POSIX compatibility, users, SSH, or a remote admin surface just because Nanos has those concepts. Adapt the appliance model to a C# native-tier workload. |
| OS target repo | `http://192.168.2.42:3000/michael.quint/OS` | First-party issue/backlog and remote repository target for this project. | This is not an external implementation reference. Repo docs and current workspace evidence remain canonical for status and claims. |
| daniilvaino/SharpOS | `references/daniilvaino-SharpOS-main` | Primary modern C# NativeAOT/no-stdlib reference for boot order, runtime limits, std/no-runtime work, diagnostics probes, PAL planning, app fixtures, scripts, and done-step evidence. | Do not rename OS to SharpOS. Do not replace this repository wholesale. Use `docs/fork-diff.md` for meaningful differences and preserve `BootSequence.cs` and `Probes.cs` ownership rules. |
| zerosharp | `references/zerosharp-master` | NativeAOT systems-programming reference for EFI no-runtime entrypoints, `[RuntimeExport("EfiMain")]`, primitive stubs, and minimal build patterns. | Do not regress OS to zerosharp's minimal stub model. Do not replace OS shared no-runtime utilities with one-off stubs. Verify license/provenance before copying. |
| EfiSharp | `references/EfiSharp-main` | UEFI C# application structure, EFI application linker/MSBuild ideas, protocol struct definitions, console/input examples, and VHD/run workflow concepts. | Do not import EfiSharp libc into OS kernel/std. Avoid Visual Studio-only assumptions unless OS explicitly supports them. Verify license/provenance before copying structs or helper code. |
| nativeaot-patcher / Cosmos gen3 | `references/nativeaot-patcher-main` | Modern NativeAOT bare-metal C# OS reference for runtime stubs, GC documentation, QEMU/UART integration tests, driver layout, interrupts, UART, GOP, networking, timers, and stress-test ideas. | Do not adopt Limine or plug/patcher architecture without an explicit OS decision. Compare GC/runtime design before migrating anything. Keep OS boot and evidence contracts canonical. |
| AtomOS | `references/AtomOS-master` | Managed OS concepts for drivers, paging, threading, ELF/syscall/shared-memory ideas, timers, ACPI, FAT32, and older device support. | Cloud path should prefer virtio-blk and virtio-net before ATA/IDE or desktop devices. Do not import toolchain assumptions that conflict with NativeAOT. |
| MOOS | `references/MOOS-master` | C# x64 OS comparison for application formats, threading, APIC/IOAPIC, ACPI, SMBIOS, storage, IPv4/IPv6, TCP/UDP/DNS/DHCP, and network-driver concepts. | Treat GC/runtime behavior carefully. Do not import non-C# portions into OS core. Prefer virtio cloud devices before Realtek/Intel/e1000 alternatives unless a compatibility issue calls for them. |
| FlingOS | `references/FlingOS-develop` | Educational managed-OS architecture, driver layering, kernel abstractions, tutorials, tools, and testing ideas. | Use for concepts and docs style. Do not import build/toolchain assumptions or non-OS architecture. Verify license/provenance before copying. |
| Cosmos | `references/Cosmos-master` | Managed OS development kit reference for examples, tests, filesystem/network abstractions, hardware abstraction ideas, and developer tooling. | Do not adopt IL2CPU or Cosmos project architecture. Use concepts/tests only where they fit NativeAOT OS. Watch bundled third-party tool licenses if reusing host tooling ideas. |
| RoseOS | `references/RoseOS-master` | Older CoreRT-era UEFI loader/kernel split, graphics mode picker, QEMU automation, CoreLib/NativeLib separation concepts. | Conceptual only. Do not port C/assembly or obsolete CoreRT assumptions. Preserve OS boot-sequence model. |
| MOSA | `references/MOSA-Project-master` | Managed compiler/OS architecture, micro-kernel ideas, device-driver modularity, debugger/tooling concepts, demos and tests. | Do not replace NativeAOT with the MOSA compiler. Treat bundled tool licenses separately from source concepts. |
| CSharpLLVM | `references/CSharpLLVM-master` | CIL-to-LLVM and runtime-helper design comparison. Useful for understanding compiler/runtime boundaries and non-goals. | Not a drop-in path: no GC by design, 8-bit `System.Char`, and no generic types. Do not abandon NativeAOT or import incompatible runtime behavior. |
| libvirt-csharp | `references/libvirt-csharp-main` | Host-side libvirt orchestration reference for future `tools/OS.LibvirtRunner` or equivalent: domain XML, attach disk/network, start/stop, capture console logs. | LGPL-2.1 per project guidance. Keep it host tooling only with clear package/dynamic-link boundaries. Never link it into kernel/std/app images. |
| smolsharp | `references/smolsharp-main` | NativeAOT size optimization reference: MSBuild size flags, DirectPInvoke concepts, linker flags, CRT removal, section review. | Correctness and diagnostics come first. Do not remove unwind metadata, panic data, or diagnostic visibility for size until completion gates allow it. |
| original sharpos/SharpOS | `references/SharpOS-master` | Historical C# OS context: AOT organization, kernel layout, tools/build/distro ideas, and Managed Operating System Alliance heritage. | Historical only. Do not infer compatibility with modern NativeAOT OS or use it as the current architecture source. |

## Unsupported Behavior

- Reference projects are not part of the first-party build.
- Reference paths must not be added to `OS.slnx` or first-party project files
  without a documented integration task and updated license/provenance notes.
- No committed first-party kernel/std/app source may be converted to C, C++,
  assembly, or reference-specific architecture.
- No reference may be used to claim OS supports boot, storage, networking,
  Kestrel, cloud import, or production readiness. Claims require OS evidence in
  `docs/completion-gates.md` and `done/stepNN.md`.

## Tests

Docs-only changes should run:

```powershell
git diff --check
```

Useful reference-map spot checks:

```powershell
Test-Path .\references
Get-ChildItem .\references -Directory
rg -n "Reference repositories|Reference Map|docs/reference-map.md" .\docs\documentation-map.md
```

## Known Limitations

The local snapshots are useful for study, but their upstream commits, exact
license state, and import provenance are not fully recorded here. Before
adapting source code rather than reimplementing a concept, update
`docs/license-audit.md` and the affected subsystem doc or done step with the
reference, license, copied/adapted boundary, and rationale.

## Next Steps

1. Keep `references/` read-only and out of first-party builds.
2. Add commit/date/license provenance for each snapshot before any source import.
3. Record reference influence in the relevant subsystem doc and done step when a
   future implementation uses a reference design.
