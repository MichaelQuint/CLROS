# Step 020 - Kestrel-Critical PAL Contract

## What Changed

- Expanded the PAL documentation with Kestrel-critical `OSHost_*` contract families for sockets, timers, thread/sync, and package file/config access.
- Updated `work/PAL/pal-design.md` with the same first-party ABI ground rules and Kestrel evidence boundary.
- Clarified that `OSHost_*` is the canonical first-party ABI prefix and that `SharpOSHost_*` may only exist as a documented compatibility alias.
- Added future marker and evidence requirements for PAL file/config, timer, thread/sync, socket-listen, and `kestrel.smoke` proofs.
- Updated the Kestrel ledger and completion gates to point at the PAL contract without claiming guest Kestrel support.

## Why It Changed

ASP.NET Minimal API/Kestrel support is mandatory, but Kestrel cannot be validated from a host-only fixture or native-tier HTTP demo. The hosted runtime needs explicit, status-coded socket, timer, thread/sync, and package/config contracts before implementation begins.

## Files Changed

- `docs/pal.md`
- `work/PAL/pal-design.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step020.md`

## Tests Run

```powershell
rg -n "OSHost_SocketCreate|OSHost_TimerCreate|OSHost_ThreadCreate|OSHost_OpenPackageFile|TEST_START kestrel.smoke" .\docs\pal.md .\work\PAL\pal-design.md .\docs\kestrel.md
git diff --check -- .\docs\pal.md .\work\PAL\pal-design.md .\docs\kestrel.md .\docs\completion-gates.md .\done\step020.md
```

## Logs And Evidence

- Documentation contains `OSHost_SocketCreate`, `OSHost_TimerCreate`, `OSHost_ThreadCreate`, and `OSHost_OpenPackageFile`.
- Documentation preserves the `TEST_START kestrel.smoke` and `TEST_PASS kestrel.smoke` evidence boundary.
- Scoped `git diff --check` exited 0 with only existing LF/CRLF normalization warnings.

## Failures

No failures remain for this documentation slice.

## Deferred Work

- Add C# ABI declaration types for status codes, handles, socket addresses, timers, waits, and package/config structs.
- Add host tests for ABI layout, enum values, and no managed-reference fields.
- Implement package/config reads, timers, thread/sync, and socket behavior.
- Launch hosted .NET 10 and Kestrel inside the guest.

## Evidence Boundary

This step proves only the documented PAL contract. It does not implement exported PAL functions, hosted .NET 10, sockets, timers, threading, package file access, Kestrel binding, or in-guest HTTP request/response behavior.
