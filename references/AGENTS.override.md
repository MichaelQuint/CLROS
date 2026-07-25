# Reference Snapshot Rules

These rules apply only under `references/`.

## Purpose

The directories here are external, read-mostly snapshots used to study
architecture, derive independent tests, and identify failure modes. They are
not first-party CLROS source and do not provide CLROS completion evidence.

## Read-Mostly Boundary

- Do not edit, format, upgrade, regenerate, patch, or normalize files inside a
  reference snapshot.
- Do not add generated files, build output, package restores, logs, or local
  configuration below a reference directory.
- Do not run reference build, restore, test, setup, or bootstrap scripts unless
  the current task explicitly names that snapshot and authorizes the operation.
- Prefer static inspection. If an authorized build is required, copy the
  snapshot to a temporary directory outside the repository and record that the
  result is reference-only evidence.
- The only normal tracked file directly under `references/` is this override.

## Build Isolation

- Do not add a reference project to `CLROS.slnx`.
- Do not add `ProjectReference`, `Compile`, `Content`, `None`, `Import`,
  `NativeLibrary`, linker, package, props, or targets inputs that resolve into
  `references/` from first-party projects.
- Do not copy prebuilt reference binaries into CLROS artifacts or images.
- Do not let reference NuGet configuration, SDK selection, environment
  variables, build targets, source generators, or native libraries affect the
  first-party build.
- First-party kernel/std/app source remains C# 14/.NET 10 and may not acquire
  committed `.c`, `.cpp`, `.h`, `.asm`, or `.s` files from a reference.

## Adaptation and Evidence

- Read `docs/reference-map.md`, `docs/fork-diff.md`, and
  `docs/license-audit.md` before using a reference.
- Verify the exact local path. Archive suffixes and README claims are not
  immutable revision evidence.
- Before direct source reuse, record the upstream URL, commit/tag, license,
  authorship, notices, exact source path, affected CLROS files, and adaptation
  boundary.
- When provenance or license is incomplete, use only architecture concepts or
  independently authored tests and mark the work license-blocked.
- `docs/license-audit.md` records a SharpOS-specific owner grant and current
  upstream CC0-1.0 evidence. SharpOS-authored source may be selectively adapted
  without a license-blocked label; because the supplied archive has no commit
  metadata, record the exact local file SHA-256 and adaptation boundary.
- The SharpOS grant does not override file-local vendor/GPL terms, WAD/media
  rights, or the absent CoreCLR fork's separate license and provenance.
- Prefer specifications and behavior tests over implementation copying.
- Preserve known reference failures as negative CLROS tests.
- Never use reference source, logs, screenshots, binaries, media, or done-step
  files to pass a CLROS completion gate.
- ManagedDoom code remains isolated and GPL-governed; never move it into
  kernel/std or commit a WAD.

## Architecture Boundary

- CLROS/`CLROS.slnx` is the product identity. `OS.*`, `src/OS`,
  `src/OS.Kernel`, and `OSHost_*` remain its technical names.
- `src/OS.Kernel/Boot/BootSequence.cs` remains the CLROS boot-order authority.
- `src/OS.Kernel/Diagnostics/Probes.cs` remains the CLROS probe authority;
  dangerous probes default to `false`.
- Shared BCL-like behavior belongs in `std/no-runtime/shared`.
- Future hosted-runtime work crosses the flat `OSHost_*` POD/status/opaque
  handle ABI. Do not import SharpOS-specific PAL names or raw privileged
  pointers.
- Do not adopt a reference's POSIX layer, shell, user model, remote
  administration surface, bootloader, compiler, or runtime architecture
  without an explicit CLROS architecture decision.
