# Step 894 - M02-002 fixed safe/In8/Out8 page materialization and finalization audit

## Summary

Implemented the M02-002 production C# fixed-page contract. The new
`ExistingIn8Out8StubBytesRWPagesFinalize` pointer core materializes the exact
4,096-byte safe/`Out8`/`In8` page, validates every byte on readback, and audits
the caller-owned transition from RW+NX metadata to RO+X metadata without
performing a live protection change or executing any page entry.

The existing port-I/O byte plans now expose always-compiled pointer emitters
while retaining their host `Span<byte>` wrappers behind
`#if !NOSTDLIB_EFI`. Caller-owned transition state is initialized, value-only,
versioned, replay-safe, and rejected when it aliases the page.

## Why

M02-001 selected the existing audited `In8`/`Out8` byte plans. M02-002 turns
that policy into a deterministic, independently hashable page artifact and a
strict W^X finalization audit that a later serialized integration issue can
consume. Keeping materialization, readback, and metadata audit separate from
EFI linkage prevents host evidence from being mistaken for guest execution.

## Files changed

- `src/OS.Kernel/Diagnostics/PortIoOperationPlan.cs`
- `src/OS.Kernel/Diagnostics/PortIoStubPlan.cs`
- `src/OS.Kernel/Diagnostics/ExistingIn8Out8StubBytesRWPagesFinalize.cs`
- `tests/OS.HostTests/ExistingIn8Out8StubBytesRWPagesFinalizeTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `done/step894.md`

## Tests run

Focused implementation checks completed before this ledger was reserved:

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\CLROS.slnx --nologo -m:1
git diff --check
```

A disposable ignored-project runner linked only the new M02-002 test
initializer plus its host-process helper. It completed with:

```text
TEST_START m02.002
CLROS_M02_002_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E sha256=F8DAF2F093F6FCD3CB6CA764DCC294EC9B4DBF2B392D222946F11250BFCE353D initial=RW+NX final=RO+X liveProtection=false liveExecution=false
TEST_PASS m02.002
TEST_PASS m02.002.focused-runner
```

The authoritative clean-source complete gate is intentionally not claimed in
this implementation checkpoint. Its exact commands, evidence root, native exit
codes, marker counts, and hashes will be added before evidence review.

## Evidence

The fixed page is:

| Range | Meaning | Exact bytes |
|---|---|---|
| `0..5` | safe return-cookie stub | `B8 4B 4F 53 4F C3` |
| `6..15` | reserved | all zero |
| `16..22` | `Out8` stub | `66 89 CA 88 D0 EE C3` |
| `23..31` | reserved | all zero |
| `32..36` | `In8` stub | `66 89 CA EC C3` |
| `37..4095` | reserved | all zero |

Exact whole-page identities:

- FNV-1a64: `F85393074A792B1E`
- SHA-256:
  `F8DAF2F093F6FCD3CB6CA764DCC294EC9B4DBF2B392D222946F11250BFCE353D`

Focused tests independently compute SHA-256, flip and reject each of the 4,096
page bytes, prove failure atomicity, reject state/page aliasing, exercise the
pointer and host-span surfaces, and confirm the pointer state machine allocates
no managed memory after warmup.

No-stdlib evidence is deliberately split:

1. The exact production source compiles with `NOSTDLIB_EFI`,
   `NoStdLib=true`, framework references disabled, and zero assembly
   references.
2. A separate bounded normal host process compiles those exact production
   sources with `NOSTDLIB_EFI` and executes materialize, full-page readback,
   and finalization, requiring exactly one
   `M02_002_NOSTDLIB_POINTER_RUN_PASS`.

The first artifact has no core-library reference and is therefore compile
closure, not a CLR-runnable binary. The second supplies actual exact-source
pointer execution without representing it as EFI or guest execution.

Independent read-only production review found no remaining contract defect
after the state/page-alias and exact-source `NOSTDLIB_EFI` execution
corrections.

## Failures

- An initial foreground complete HostTests attempt used a three-minute command
  window even though the accepted harness baseline takes about 32 minutes. The
  orphaned exact test process tree was stopped after confirming it was inside
  the pre-existing EFI preflight matrix; that run produced no accepted
  evidence.
- The first focused run exposed a missing unsigned suffix on the FNV prime in
  the zero-reference shim profile and an off-by-one negative test whose
  six-byte range ended exactly at byte 4,096 instead of outside it. Both were
  corrected.
- Pre-run review found nullable `default` plan arguments that selected helper
  defaults rather than invalid plans. The tests now pass explicit invalid
  `PortIoOperationKind.None` plans.
- Independent review found state/page aliasing could corrupt a just-written
  page before returning success. Production now rejects materialize and
  readback overlap atomically with `StateStorageOverlap`.
- Independent review found the original zero-reference probe was compiled but
  not runnable. The retained zero-reference compile is now paired with a
  bounded exact-source `NOSTDLIB_EFI` host execution probe.

## Deferred work

- M02-017 exclusively owns `src/OS/OS.csproj`, `src/OS/Boot/EfiEntry.cs`,
  live EFI linkage, the reversible owned-root integration, and real
  QEMU/OVMF execution of only the safe offset-zero cookie.
- Offsets 16 and 32 remain privileged and unexecuted. M02-002 does not bind a
  function pointer, invoke `In8`/`Out8`, configure COM1, alter a PTE, invalidate
  a TLB, cross real `ExitBootServices`, or emit a guest marker.
- No completion gate advances. There is no provider, hosted .NET, Minimal API,
  Kestrel, CloudReady, or production-readiness claim.
