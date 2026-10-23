# `src/OS` Override

This directory contains the freestanding no-stdlib NativeAOT EFI shell. Root `AGENTS.md` applies, but this directory has stricter rules.

## Purpose

`src/OS` is the first native boot artifact boundary. It publishes a minimal x64 EFI PE and stages it as:

```text
artifacts/efi/BOOTX64.EFI
```

This project is not the normal managed kernel library. It is included in
`CLROS.slnx`, but its explicit no-stdlib project settings and this override keep
the freestanding publish boundary separate from normal managed projects.

## Project Rules

- Keep this project freestanding.
- Keep `NoStdLib=true`.
- Keep `NoConfig=true`.
- Keep `DisableImplicitFrameworkReferences=true`.
- Keep `EnableDefaultCompileItems=false`.
- Keep compile items explicit.
- Keep `EntryPointSymbol=EfiMain`.
- Keep `LinkerSubsystem=EFI_APPLICATION`.
- Keep `RuntimeIdentifier=win-x64` until a documented cross-target path exists.
- Keep `LangVersion=14.0`.
- Do not reference `OS.Kernel` or normal managed projects until a documented handoff boundary exists.
- Do not add normal framework assembly dependencies to make compiler errors go away.
- Keep this project in `CLROS.slnx`; do not weaken its no-stdlib settings to
  make solution builds resemble normal managed builds.

## Source Rules

- C# only.
- No committed `.c`, `.cpp`, `.h`, `.asm`, or `.s`.
- No runtime dependency on normal `System.Private.CoreLib`.
- No `Console`, `File`, `Task`, reflection, LINQ, or normal BCL usage.
- Avoid managed allocation unless the allocation helper and heap behavior are explicitly implemented and tested.
- Avoid exceptions, `try/catch`, and `throw` outside explicitly qualified,
  default-off runtime probes. Bounded EH receipts do not authorize general EH.
- Avoid static reference fields outside supported initialization/rooting
  contracts; bounded constructor/GC profiles do not grant general runtime use.
- Keep unsafe code small and localized.

## Minimal Runtime Rules

`Runtime/MinimalRuntime.cs` is a shell-only compiler contract surface.

Allowed additions:

- Primitive type stubs required by C# or ILC.
- NativeAOT helper types required by a concrete compiler/linker error.
- Fail-fast placeholders that cannot silently succeed.
- Runtime exports required by the current publish boundary.

Before adding a stub:

1. Capture the exact `dotnet publish` or linker failure.
2. Check whether the helper belongs in `std/no-runtime/shared` instead.
3. Implement the smallest C# surface needed.
4. Document if behavior is a placeholder.
5. Re-run `dotnet run --project tools/OS.Tooling -- efi build`.

Do not treat compiler stubs as general runtime implementations. Unsupported
type-cast helpers fail fast; selected EH probes have bounded retained-exception
type matching. Preserve those conditional boundaries and original native review.

## Link Rules

The project strips default NativeAOT runtime/native libraries from the linker argument set. Do not reintroduce:

- `Runtime.WorkstationGC.lib`
- `Runtime.ServerGC.lib`
- `bootstrapper.obj`
- Windows SDK imports that are not explicitly required for the EFI artifact.
- CRT/default libraries through `/DEFAULTLIB`.
- Export/import-library generation for EFI.

EFI link output must remain freestanding and use `/SUBSYSTEM:EFI_APPLICATION`.

## Diagnostics And Evidence

Required command:

```powershell
dotnet run --project tools/OS.Tooling -- efi build
```

Expected markers:

```text
TEST_PASS efi.shell.publish
EFI_ARTIFACT artifacts/efi/BOOTX64.EFI
```

Useful artifact verification:

```powershell
dumpbin /headers .\src\OS\bin\Release\net11.0\win-x64\publish\OS.exe
```

The PE headers should report:

- `machine (x64)`
- nonzero entry point
- `subsystem (EFI Application)`

Do not claim boot proof from this directory. Boot proof requires QEMU/OVMF execution and serial markers.

## Current Local Boundary

Ordinary `VerifiedExit` already captures required firmware state, leaves boot
services and performs owned checks (step1094). Explicit default-off profiles
also run pinned Linux NativeAOT applications and finite Kestrel service
(steps1307 through1556). Keep `BootSequence` authoritative, source-link only
audited no-stdlib inputs, and preserve default-off probes and exact profile
prerequisites. Complete local Gate14, general runtime support, continuing disk
service and multi-app isolation still need their own evidence.
