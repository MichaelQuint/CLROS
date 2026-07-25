# `src/OS.Kernel` Override

This directory contains host-testable kernel contracts. Root `AGENTS.md` applies, with the following local constraints.

## Purpose

`OS.Kernel` holds code that should eventually run in the kernel, but is currently compiled as a normal `net10.0` library so host tests can verify contracts before real boot integration.

## Architecture Rules

- `Boot/BootSequence.cs` is the source of truth for boot ordering.
- Do not bypass `BootSequence`.
- Extend boot behavior as explicit phases with stable preconditions and postconditions.
- Keep boot output machine-readable.
- Keep kernel contracts independent of host tools.
- Keep APIs small and status-coded.
- Avoid adding abstractions until a second real caller or a clear boot boundary requires them.

## Runtime Restrictions

Even though this project builds on normal .NET 10, write kernel-facing code as if it will move to a constrained NativeAOT/no-stdlib environment.

- Avoid exceptions in kernel-facing paths.
- Avoid `throw` for control flow.
- Avoid lazy static reference fields.
- Avoid reflection.
- Avoid culture-sensitive APIs.
- Avoid LINQ in boot, diagnostics, panic, driver, storage, packet, and scheduler-adjacent paths.
- Avoid allocation in panic-oriented APIs.
- Prefer explicit result structs such as `BootResult` and `BootCapabilityResult`.
- Prefer `StringComparison.Ordinal` or explicit byte/char comparisons in host-only validation.

Host-test code may use normal .NET features. Kernel production code should not depend on them.

## Diagnostics Rules

- `Diagnostics/Probes.cs` owns compile-time probe toggles.
- Dangerous probes default to `false`.
- Panic and diagnostic output must remain stable and parseable.
- Do not replace specific diagnostics with generic `Halt`.
- Add subsystem and reason to every panic path.
- When adding a diagnostic formatter, add tests.

## Boot Marker Rules

Use `SerialSmokeMarkers` for marker constants.

Required marker forms:

- `TEST_START <name>`
- `TEST_PASS <name>`
- `TEST_FAIL <name> <reason>`
- `OS_BOOT_OK`
- `OS_SMOKE_PASS`
- `OS_SMOKE_FAIL`

If new boot phases are added, host tests must prove marker order or presence before QEMU integration.

## Testing

Relevant commands:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Add focused host tests under `tests/OS.HostTests` for:

- Boot phase changes.
- Boot capability changes.
- Probe defaults.
- Panic formatting.
- Ring-buffer behavior.
- Status-code behavior.

## Documentation

When changing kernel contracts, update the relevant docs:

- `docs/boot.md`
- `docs/boot-order.md`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- latest `done/stepNN.md` for milestone-sized changes.
