# `tools` Override

This directory contains host-side tooling. Root `AGENTS.md` applies, with the following local rules.

## Purpose

Host tools build, validate, package, run, or inspect OS artifacts. They are not part of the OS image.

Current tools:

- `OS.QemuRunner`
- `OS.QemuCapture`
- `OS.ManifestTool`
- `OS.LibvirtRunner`
- `OS.ImageBuilder`
- `OS.X64TextSerializerGenerator`
- `OS.Tooling`: C# build, verification, smoke, CI, backlog, repository checks,
  shared reviewed expectations, and atomic evidence-step allocation.

New automation belongs in C#/.NET 11. Do not add PowerShell scripts or embed
PowerShell in C# fixtures. Use typed production APIs from host fixtures; preserve
independent byte vectors and rejection controls. Native operations retain bounded
processes, source/output locks, exact provenance, and confirmed child cleanup.

`OS.X64TextSerializerGenerator` is the host-build leaf that produces the
deterministic M01 instruction-publication serializer object. M01-034 includes
the tool in `CLROS.slnx` and imports its integration targets from
`src/OS/OS.csproj`, but the EFI project does not take a `ProjectReference` to
the tool or link its managed output into the image. The imported target invokes
the audited host build, generates the ephemeral COFF object, and registers only
that exact object with the native linker. Build the generator project directly
when changing its implementation; run the EFI build and artifact verifier when
changing its targets or integration contract.

## Allowed Dependencies

Host tools may use normal .NET 11 APIs and NuGet packages when useful.

Rules:

- Do not link host-only packages into kernel, std, or app image code.
- Keep third-party packages out of OS image paths.
- Document any third-party host dependency in docs and license audit.
- Prefer built-in .NET APIs when they are sufficient.

## Coding Standards

- Target `net11.0`.
- Use C# 14.0.
- Nullable should remain enabled.
- Use clear command-line parsing and deterministic output.
- Return nonzero exit codes on failure.
- Validate inputs and paths.
- Avoid printing secrets.
- Keep generated paths under `artifacts/` unless the user explicitly requests another output.
- Keep generated `.obj` files ephemeral; do not commit them or native source.

## Output Contract

Tooling used by scripts or CI should emit stable machine-readable markers:

- `TEST_START <name>`
- `TEST_PASS <name>`
- `TEST_FAIL <name> <reason>`

Human-readable details are fine, but marker lines must remain parseable.

## Image And Runner Rules

For QEMU/libvirt/image tooling:

- Do not claim boot success from dry-run command construction.
- Capture serial logs as artifacts for real runs.
- Require expected smoke markers for success.
- Keep command lines reproducible.
- Preserve dry-run modes for hosts without QEMU/libvirt.
- Do not assume OVMF paths; discover or accept explicit configuration.

`OS.QemuCapture` source-links the shared `WhpxPrelaunch` implementation from
`OS.QemuRunner`. Include new partial files in both consumers and build both
when changing that boundary. Keep the
[WHPX compatibility policy](../docs/qemu.md#windows-virtio-notifier-compatibility)
and actual launch receipts consistent with device argument changes.

## Manifest Rules

Manifest tooling should:

- Reject malformed JSON.
- Reject unknown schema versions unless explicitly supported.
- Enforce size limits.
- Reject unsafe app paths.
- Reject duplicate environment keys.
- Validate network/device config with explicit messages.

Add tests in `tests/OS.HostTests` for every manifest behavior change.

## Testing

Run the relevant host tests:

```powershell
dotnet build .\tools\OS.X64TextSerializerGenerator\OS.X64TextSerializerGenerator.csproj --nologo
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
```

For replay changes, preserve the raw runner output and original file identities.
Reconstruct the same input that the live validator received. In particular, the
WHPX launch owner verifies its two launch records before projecting the output
passed to the guest validator. Use the original request's literal paths where the
validator compares transcript text. Record replay results separately from fresh
guest executions.

For image builder changes, add deterministic output tests before relying on QEMU.
