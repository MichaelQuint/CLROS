# Step 010 - PAL ABI Naming ADR

## What Changed

- Added `docs/adr-pal-abi-naming.md`.
- Accepted `OSHost_*` as the canonical first-party exported PAL ABI prefix.
- Updated `docs/pal.md` with:
  - example `OSHost_*` symbols;
  - migration rules for adapted reference code;
  - compatibility alias policy for any future `SharpOSHost_*` adapter.
- Updated `work/PAL/pal-design.md` to make `OSHost_*` the design baseline.

## Why It Changed

The repository is named OS. The reference PAL design uses `SharpOSHost_*`, but first-party exported ABI symbols should match the project identity and remain easy to audit before any hosted .NET 10/CoreCLR integration begins.

## Files Changed

- `docs/adr-pal-abi-naming.md`
- `docs/pal.md`
- `work/PAL/pal-design.md`

## Tests Run

```powershell
rg -n "SharpOSHost_|OSHost_|first-party ABI should use|compatibility decision" docs work src std apps tools tests scripts
git diff --check -- docs/adr-pal-abi-naming.md docs/pal.md work/PAL/pal-design.md
```

## Logs and Evidence

- `rg` confirms `OSHost_*` examples are documented in `docs/adr-pal-abi-naming.md`, `docs/pal.md`, and `work/PAL/pal-design.md`.
- `rg` confirms no `SharpOSHost_*` usage exists in first-party code directories: `src`, `std`, `apps`, `tools`, `tests`, or `scripts`.
- `git diff --check` for the changed PAL docs exited 0. It printed line-ending normalization warnings only.

## Failures

No failures remain for this documentation slice.

## Deferred Work

- Implement `OSHost_*` status codes and POD layout tests.
- Add any compatibility alias only through a documented future issue or ADR.
- Keep hosted .NET 10/CoreCLR integration separate from this naming decision.

## Evidence Boundary

This step proves the PAL naming decision is documented. It does not implement PAL functions, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
