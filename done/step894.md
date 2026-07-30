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

The authoritative r1 gate ran from clean implementation commit
`06a38803b8bf4d5bc35e29948e626dbd8fb164bd` and tree
`09bc5e263a3ab624ea3232655957d9ae79b2d042`:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1
git diff --check
git status --short
```

All eight commands captured native exit `0`. Both builds completed with zero
warnings and zero errors. The complete HostTests process ran for 2,180.123
seconds and ended with exactly one `TEST_PASS host.tests`. QEMU execution was
limited to the explicit dry-run; no QEMU/OVMF guest boot ran. The EFI command
was the canonical repository regression/preflight build only and does not prove
that M02-002 is linked into `BOOTX64.EFI`.

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

Authoritative evidence root:

```text
C:\Projects\CLROS\artifacts\logs\m02-002-final\06a38803-20260730T220052Z-r1
```

Host marker results:

- `TEST_START`: 3,338
- `TEST_PASS`: 3,339
- `TEST_FAIL`: 0
- terminal `TEST_PASS host.tests`: exactly 1
- `TEST_START m02.002`: exactly 1, line 1,538
- exact `CLROS_M02_002_READY ...`: exactly 1, line 1,539
- `TEST_PASS m02.002`: exactly 1, line 1,540
- HostTests stderr: 0 bytes
- final `git status --short` stdout: 0 bytes

Required command durations and native results:

| Command | Seconds | Exit |
|---|---:|---:|
| solution build | 14.063 | 0 |
| HostTests build | 6.014 | 0 |
| complete HostTests | 2,180.123 | 0 |
| QEMU dry-run | 4.019 | 0 |
| naming/source firewall | 34.093 | 0 |
| canonical EFI build | 42.145 | 0 |
| `git diff --check` | 1.999 | 0 |
| `git status --short` | 2.013 | 0 |

Evidence SHA-256 anchors:

- evidence summary:
  `7AF214CCF75F8D05F6A7E1E667B56698633DD5EB57B22544A450B0FF1F584935`
- 37-artifact final hash ledger:
  `44D9E3F31EA3E3AFA9F66C9F3766B0A38BD84C32934145FCCD8809CFC0A9B8C7`
- solution build:
  `7C688F91856CBA961336088CD3EC1589C7D6A55DBBE457C7D4D014046D6B3A1E`
- HostTests build:
  `2573C14E5A377B06A8834119458868D02C057BAFC569773209D5E202D6E0E5DF`
- complete HostTests stdout:
  `2913C5F6475097B0AD83D5D03F14E477FD30E29320D5972EAA3C895583CCBB73`
- QEMU dry-run:
  `1D89BEBC2418F4BF887D21B84366BBE7AF2E555908570CCF1B87B415F5C91C95`
- naming/source firewall:
  `F859FFF4688D92C662BD161F4DEDF4C64778B133B7ABDBED70D634D8A2F4B651`
- canonical EFI build:
  `BE3183FC415BD168ADBEDCB4249B6146437003FE347B199DA6D617BCD5547522`
- generated `artifacts/efi/BOOTX64.EFI`:
  `158432A6F1BA692561A8CA047AD4E1C1362E3549E5CA8A7EF4E7288ABECD1741`
- command summary:
  `49D534F41640520602F000AF07349AC11A48EC26EC72425992FA0EAB18BF58EB`
- runner events:
  `1D2D67790828DEDD497998E45081BCAB058147263CB4827B3FEB8BD7F78A8B0B`
- empty diff/status output:
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`

Every entry in `final-file-sha256.json` was independently rehashed after the
runner exited: 37 checked, zero missing, zero byte-count mismatches, and zero
hash mismatches. PR #431 carries the frozen implementation branch. Evidence
review and merge remain separate tracker actions and are not claimed by this
ledger entry.

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
- The first independent hash-ledger verification one-liner wrapped the
  PowerShell `ConvertFrom-Json` array as one object and failed before validating
  any entry. It changed no evidence file. The corrected verifier checked all 37
  entries with zero failures and matched the ledger sidecar hash.

## Deferred work

- M02-017 exclusively owns `src/OS/OS.csproj`, `src/OS/Boot/EfiEntry.cs`,
  live EFI linkage, the reversible owned-root integration, and real
  QEMU/OVMF execution of only the safe offset-zero cookie.
- Offsets 16 and 32 remain privileged and unexecuted. M02-002 does not bind a
  function pointer, invoke `In8`/`Out8`, configure COM1, alter a PTE, invalidate
  a TLB, cross real `ExitBootServices`, or emit a guest marker.
- No completion gate advances. There is no provider, hosted .NET, Minimal API,
  Kestrel, CloudReady, or production-readiness claim.
