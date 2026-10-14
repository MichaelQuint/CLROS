# `std/no-runtime/shared` Override

This directory is the staging area for shared no-runtime-compatible utilities and compatible BCL-like contracts. Root `AGENTS.md` applies, with these stricter rules.

## Purpose

Shared string, numeric, collection, memory, and utility behavior must be implemented here before kernel/app code uses it.

This project currently builds as `net11.0` for host validation, but the code should be written so it can migrate toward freestanding NativeAOT/no-stdlib use.

## Namespace Rules

Use canonical `System.*` namespaces only for APIs that are compatible with public BCL contracts, modulo documented no-stdlib limitations.

Use OS-specific namespaces for partial or nonstandard behavior:

- `OS.Std.*`
- `OS.Std.NoRuntime.*`

Do not add fake `System.*` behavior that silently differs from .NET. If compatibility is incomplete, document it and add tests for the supported subset.

## Implementation Rules

- C# only.
- Keep APIs deterministic and allocation-aware.
- Prefer ordinal/culture-invariant behavior.
- Avoid reflection.
- Avoid dynamic code generation.
- Avoid exceptions in code that is expected to migrate into kernel/runtime paths.
- Prefer explicit result values or fail-fast helpers where no exception support exists.
- Keep unsafe code isolated to memory primitives and carefully reviewed helpers.
- Avoid LINQ in low-level algorithms.
- Prefer span-based APIs when they match BCL contracts and can be implemented without hidden dependencies.
- Keep behavior small and testable.

## Compatibility Ledger

Every new BCL-like type or method should have a documented compatibility status.

Update or add docs when a supported subset changes:

- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`

Include:

- Supported overloads.
- Unsupported overloads.
- Behavioral differences.
- Allocation behavior.
- Exception behavior, especially if exceptions are not implemented.

## Testing

Add host tests under `tests/OS.HostTests` for every new algorithm or compatibility contract.

Required coverage examples:

- Empty input.
- Null or invalid input if the API surface permits it.
- Boundary lengths.
- Overlap behavior for memory copies.
- Ordinal/case behavior for string helpers.
- Duplicate keys and missing keys for collections.
- Stable failure behavior when exceptions are unavailable.

Run:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Do Not

- Do not import code from `src/OS/Runtime/MinimalRuntime.cs` as if it were production BCL.
- Do not introduce normal framework-only APIs that cannot move toward no-stdlib use.
- Do not add broad collection/string implementations without focused tests.
- Do not claim BCL compatibility without tests and documented limits.
