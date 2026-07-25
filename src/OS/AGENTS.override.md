# `src/OS` Override

This directory contains the freestanding no-stdlib NativeAOT EFI shell. Root `AGENTS.md` applies, but this directory has stricter rules.

## Purpose

`src/OS` is the first native boot artifact boundary. It publishes a minimal x64 EFI PE and stages it as:

```text
artifacts/efi/BOOTX64.EFI
```

This project is not the normal managed kernel library and is intentionally outside `OS.slnx`.

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
- Do not add this project to `OS.slnx` unless the no-stdlib publish path is stable in CI.

## Source Rules

- C# only.
- No committed `.c`, `.cpp`, `.h`, `.asm`, or `.s`.
- No runtime dependency on normal `System.Private.CoreLib`.
- No `Console`, `File`, `Task`, reflection, LINQ, or normal BCL usage.
- Avoid managed allocation unless the allocation helper and heap behavior are explicitly implemented and tested.
- Avoid exceptions, `try/catch`, and `throw`.
- Avoid static reference fields until class constructor/runtime initialization work is implemented.
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
5. Re-run `scripts/build-efi-shell.ps1`.

Do not use permissive shell placeholders as a model for the real kernel runtime. The current type-cast helpers are publish-boundary placeholders only.

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
.\scripts\build-efi-shell.ps1
```

Expected markers:

```text
TEST_PASS efi.shell.publish
EFI_ARTIFACT artifacts/efi/BOOTX64.EFI
```

Useful artifact verification:

```powershell
dumpbin /headers .\src\OS\bin\Release\net10.0\win-x64\publish\OS.exe
```

The PE headers should report:

- `machine (x64)`
- nonzero entry point
- `subsystem (EFI Application)`

Do not claim boot proof from this directory. Boot proof requires QEMU/OVMF execution and serial markers.

## Next Local Milestones

1. Introduce a typed, allocation-free boot handoff to `OS.Kernel` only after the boundary is designed.
2. Move UEFI service access behind platform interfaces.
3. Capture the UEFI memory map before ExitBootServices.
4. Keep QEMU serial marker evidence updated after every boot-path change.
