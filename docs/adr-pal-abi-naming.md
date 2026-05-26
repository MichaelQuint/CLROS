# ADR: PAL ABI Naming

## Status

Accepted.

## Context

OS will eventually expose a flat host/guest PAL ABI for hosted .NET 10/CoreCLR work. The modern SharpOS reference uses `SharpOSHost_*` names for the same conceptual boundary, but this repository is named OS. Reusing the reference prefix in first-party exports would blur product identity and make later hosted-runtime integration harder to audit.

## Decision

First-party exported PAL ABI symbols in this repository must use the `OSHost_*` prefix.

Examples:

- `OSHost_AllocPages`
- `OSHost_FreePages`
- `OSHost_ProtectPages`
- `OSHost_QueryMemory`
- `OSHost_GetMonotonicTicks`
- `OSHost_WriteLogBytes`
- `OSHost_OpenPackageFile`
- `OSHost_ReadBlock`
- `OSHost_ThreadCreate`
- `OSHost_SocketConnect`

The `SharpOSHost_*` prefix is reserved for reference-code compatibility only. It must not be introduced as a first-party exported symbol unless a later ADR or issue explicitly adds a compatibility alias for an external fork, test fixture, or imported reference contract.

## Compatibility Impact

Adapted reference material that names `SharpOSHost_*` must be migrated at the OS boundary:

- First-party declarations, exports, docs, tests, and generated bindings use `OSHost_*`.
- Reference-code comments may mention `SharpOSHost_*` only to explain the source mapping.
- A compatibility alias may forward `SharpOSHost_Foo` to `OSHost_Foo` only when a documented external dependency cannot be renamed at the same time.
- Compatibility aliases, if added later, are temporary adapter symbols and do not change the canonical ABI name.

## Migration Rule

When adapting code or docs from a reference repository:

1. Rename PAL imports/exports from `SharpOSHost_*` to `OSHost_*` before committing first-party OS code.
2. Keep the function domain and suffix stable where practical, such as `SharpOSHost_AllocPages` becoming `OSHost_AllocPages`.
3. Document any suffix change in `docs/pal.md` or a follow-up ADR.
4. Add tests or `rg` evidence proving no unintended `SharpOSHost_*` first-party declarations remain.

## Consequences

The ABI name now matches the project identity and is easy to grep. Hosted runtime work will need a small adaptation layer when consuming reference material, but the boundary stays explicit and auditable.

This ADR does not implement any PAL function and does not claim hosted .NET 10, Minimal API, or Kestrel support.
