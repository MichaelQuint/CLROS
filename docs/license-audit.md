# License Audit

## Current Status

The project owner completed the reference license review on 2026-05-21 and cleared use of source code from the reference projects as needed.

This clears license use for implementation work, but it is not evidence that OS release or security gates are complete. Those gates still require build, boot, runtime, driver, cloud-image, security, and documentation evidence.

## Implemented Behavior

- `docs/reference-map.md` lists each local reference and intended use.
- `libvirt-csharp` is now available locally at `references/libvirt-csharp-main`.
- The modern SharpOS reference is now available locally at `references/daniilvaino-SharpOS-main`.
- `scripts/check-naming.ps1` includes a source firewall that keeps forbidden low-level first-party source files and accidental `references/` build inputs out of `src/`, `std/`, and `apps/`.

## Use Rules

- Source may be used from references when it helps the implementation.
- First-party OS core/kernel/std/app code remains C# only.
- Do not commit `.c`, `.cpp`, `.h`, `.asm`, or `.s` into first-party core/kernel/std/app source.
- Non-C# reference code can inform C# ports, generated build-time artifacts, or host tooling when appropriate.
- Host-side tools may use normal .NET dependencies and reference implementations when they are kept out of the OS image.
- `libvirt-csharp` belongs only in host tooling, such as `tools/OS.LibvirtRunner`.

## Unsupported Behavior

No full license scanner, SBOM generator, or imported-source provenance database exists yet.

## Tests

`tests/OS.HostTests/SourceFirewallTests.cs` covers the automated source/build-boundary guard. It does not perform license identification.

## Known Limitations

Some reference snapshots do not contain local license files, the project owner has reviewed all licenses and has stated you are free to use any source code from any reference project. Keep the project owner's review as the current authorization record.

## Next Steps

1. Add upstream URL, commit/date.
2. Add CI wiring for the source firewall and naming guard.
3. Record reference provenance in future done steps when source is adapted.
