# OS Reference Diff

## Current Status

The modern SharpOS reference is now available at `references/daniilvaino-SharpOS-main`. It is an important reference for architecture, invariants, boot order, std/no-runtime, diagnostics probes, PAL design, and done history that inform OS. It is not a first-party authority over this repository; `AGENTS.md`, `docs/documentation-map.md`, and the focused `docs/*.md` files remain canonical for OS.

The first-party `C:\Projects\OS` tree is still Phase A cloud-ready scaffolding. It has project slots, docs, marker contracts, host tests, and host tool placeholders, but it has not yet imported or implemented the reference kernel.

## Modern SharpOS Baseline

Observed from the reference tree:

- `README.md` and `CLAUDE.md` define a C#-only NativeAOT + NoStdLib UEFI OS.
- No low-level first-party source should be committed as `.c`, `.cpp`, `.h`, `.asm`, or `.s`; low-level work is expressed through C# intrinsics, C# byte-array shellcode emitters, or ephemeral build-time generation.
- `references/daniilvaino-SharpOS-main/OS/src/Boot/BootSequence.cs` is the reference boot-order model used for comparison.
- `references/daniilvaino-SharpOS-main/OS/src/Kernel/Diagnostics/Probes.cs` centralizes reference compile-time probe toggles.
- `docs/boot-order.md` describes phases for panic/IDT, memory, runtime, platform, probes, and app launch.
- `std/no-runtime/shared/` contains substantial BCL-compatible and OS-specific shared runtime work, including strings, spans, memory helpers, collections, exceptions, class-constructor support, and GC components.
- `references/daniilvaino-SharpOS-main/OS/work/PAL/pal-design.md` defines the reference `SharpOSHost_*` boundary as a flat ABI between CoreCLR PAL work and OS providers. First-party OS PAL naming remains `OSHost_*`.
- Latest observed done writeup is `done/step099.md`, which records PAL census cleanup and a PAL/OS census of `OK=37 DEG=2 FAIL=7`.

## Historical SharpOS Baseline

The older `references/SharpOS-master` tree remains useful only as historical context:

- NAnt/MSBuild-era build orchestration.
- Custom AOT compiler producing `SharpOS.Kernel.bin`.
- x86 ADC layer and Multiboot-oriented kernel.
- Custom Korlib/runtime metadata model.

## First-Party OS Differences

The current first-party tree intentionally differs from the modern SharpOS reference in these ways:

- It is a new OS workspace focused on Nanos-like deployment: single workload by default, virtualized/cloud-first, image artifacts, QEMU/libvirt/cloud import, and no default remote admin surface.
- It currently uses plain `net10.0` scaffold projects for planning and host validation; it does not yet build a freestanding EFI application.
- It has host tool project slots for `OS.ImageBuilder`, `OS.QemuRunner`, `OS.LibvirtRunner`, and `OS.ManifestTool`.
- It has placeholder apps for `HelloSharpFs`, `FetchApp`, `TinyHttpServer`, and `DiagnosticApp`.
- It has marker-contract host tests and a first-marker QEMU/OVMF proof for the no-stdlib EFI artifact.

## Preserved Invariants

- `BootSequence.cs` remains the boot-order authority.
- `Probes.cs` remains the probe toggle surface.
- Shared utility/BCL-like work belongs in `std/no-runtime/shared` before kernel/app usage.
- Kernel/std/app source remains C#-first and avoids committed non-C# low-level files.
- Hosted CoreCLR remains a long-term tier and must not block the first native-tier OS milestone unless a milestone explicitly requires it.

## Unsupported Behavior

No code-level migration from `references/daniilvaino-SharpOS-main` into first-party source has been performed yet. No runtime, driver, storage, networking, EFI, or ExitBootServices behavior can be claimed from the first-party tree.

## Tests

Current first-party evidence remains host-only:

```powershell
dotnet build .\OS.slnx
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj
.\scripts\run-qemu-smoke.ps1 -DryRun
```

## Known Limitations

The reference SharpOS has advanced features that the first-party OS scaffold does not yet have. Do not mark gates complete by pointing at `references/`; gates require first-party implementation and local evidence.

## Next Steps

1. Decide whether to port the modern SharpOS EFI/boot/runtime substrate directly into first-party source or to adapt it in smaller first-party OS slices.
2. Replace the placeholder `std/no-runtime/shared` implementation with the modern SharpOS shared runtime surface where appropriate.
3. Build the first real EFI image and make `OS.QemuRunner` consume it.
4. Update this diff after each subsystem import/adaptation.
