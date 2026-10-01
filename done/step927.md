# Step 927 - Disabled page-fault host intent and synthetic observation audit

## Summary

M02-014 (#54) now has an allocation-free host value contract for the M01-015
supervisor null-read intent. It validates disabled prerequisites and audits
exactly five supplied page-fault fields. No live fault or guest evidence is
claimed; the parent issue remains open for separately scoped EFI integration.

## Why

Accepted M02-013 terminal vector-6 evidence releases this diagnostics leaf,
but cannot establish vector-14 delivery or active page-zero absence. The new
contract distinguishes supplied-image intent, caller readiness attestations,
and synthetic observations from later hardware capture. Its exact full-width
error-zero policy prevents the legacy null-write or reserved/high error bits
from passing a supervisor null-read comparison.

## Files changed

- `src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultCR2ErrorCode.cs`:
  unmanaged request/result, authoritative disabled probe default, M01-015
  intent validation, canonical/page-zero instruction metadata validation,
  and deterministic five-field comparison.
- `tests/OS.HostTests/SeparateDisabledDefaultPageFaultCR2ErrorCodeTests.cs`:
  safety, authority, corruption, boundaries, mismatch ordering, every error
  and CR2 bit, allocation, and source-boundary tests.
- `docs/diagnostics.md`, `docs/boot-order.md`: implemented behavior, exact host
  marker, primary architecture references, and deferred hardware boundaries.
- `done/step927.md`: this evidence record, reserved with the allocation script.

## Tests run

From `C:/Users/Roger/.codex/worktrees/m02-014-host/CLROS`:

```powershell
dotnet run --project .\artifacts\m02-014\focused\Focused.csproj -c Release
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
git diff --check
git diff --no-index --check -- NUL .\src\OS.Kernel\Diagnostics\SeparateDisabledDefaultPageFaultCR2ErrorCode.cs
git diff --no-index --check -- NUL .\tests\OS.HostTests\SeparateDisabledDefaultPageFaultCR2ErrorCodeTests.cs
```

Focused Release run passed, stderr empty. HostTests build passed with zero
warnings/errors. Independent reviewer verified both source hashes, reran the
focused harness successfully, and found no actionable issue. Parent reviewed
both complete files and the proposed documentation before integration.
Whitespace checks include the two initially untracked source files.

Coverage includes 128 execution/readiness combinations, missing and corrupted
M01-015 authority, all page-zero/canonical boundaries, 258 vector values, each
of 64 error/CR2 bits, unrelated-register differences, and 30,000 warmed calls
with zero managed allocation. No aggregate suite or exact no-stdlib compile
is claimed here. The settled integration wave owns the later complete gate.

## Evidence

Frozen worktree source SHA-256:

- Production: `4E9E7589DC95EE122CA410BB74B0A599BE21DACF5FB58788A0306084A4245263`.
- Test: `C93027C31070151AEA4E2DF1750C49181ED9A739E6BE001CD6A183D830ACF6C3`.

These exact bytes were copied into the parent integration checkout.
Worktree logs under `artifacts/m02-014/`:

- `focused.stdout.log`: `7C266C10A96598F7F9F8A36EA30D93CA3D8B2035F4D660BA5E4BC0D83C89AB3D`.
- `host-build.stdout.log`: `FCEAEB5AD106ED7CBF649E36EE51D9B78DCA343C159465665B9A7FEE394EE544`.
- Both stderr logs: empty, SHA-256 `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

The exact host-ready line is documented in `docs/diagnostics.md`. Every result,
including accepted synthetic observations, reports no guest evidence, no
captured hardware state, and no triggered fault. No generated artifacts or
reference-project implementation are committed.

## Failures

The focused implementation run and relevant build passed on their first run.
Read-only exploration had a PowerShell pipeline syntax error and a guessed
missing filename; neither changed tracked files. In the parent checkout,
direct invocation of the unsigned done-step script was rejected by local
execution policy. The known repository script then ran successfully in a
process-scoped `powershell.exe -NoProfile -ExecutionPolicy Bypass` invocation;
the system execution policy was not modified.

## Deferred work

- #54 remains open: active page-zero absence under the actual CR3, owned
  descriptors/handler, instruction span/mapping/lifetime validation, saved
  flags including RF, early CR2 capture, and terminal behavior or recovery.
- Separate explicit EFI source closure must resolve `Probes` authority without
  duplicating its constants or silently widening the source firewall.
- Exact no-stdlib compilation, NativeAOT linkage, fresh real QEMU/OVMF proof,
  and accepted reviewed merge are not supplied by this host leaf.
- Generic resource checks for allocation, exhaustion, stale ownership, release,
  overlap, and W+X belong to the integration that owns those resources; this
  value-only contract holds no pointer, length, mapping, or allocation.

## Evidence boundary

Host-only contract and synthetic tests. Gate states are unchanged. This is
neither a page-fault handler nor a new boot phase, and does not prove fault
delivery, recovery, persistent diagnostics, real ExitBootServices, hosted .NET,
Kestrel, or cloud readiness. The all-backlog goal remains active.
