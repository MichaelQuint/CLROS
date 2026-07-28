# Step 874 - Split live paging preparation from synthetic execution transcripts

## Summary

Completed Gitea issue `M01-029` by adding a host-only, no-stdlib-compatible
preparation contract for the future owned-CR3 composite transition. The new
contract validates all caller-owned metadata, materializes and verifies the
already-audited x64 byte plans, constructs the modeled page-table and
protection workspaces, and publishes a preparation state only after the final
candidate validates.

The pre-existing `Execute(...)` model remains byte-for-byte unchanged and is
now explicitly classified as `ModelOnlyLegacy`. Neither its synthetic CR3 and
protection transcripts nor the new preparation result are execution evidence.

## Why

`M01LivePagingIntegration.Execute(...)` previously combined valid preparation
work with caller-supplied synthetic CR3/protection observations. A successful
model transcript could therefore be mistaken for readiness to execute the
future composite transition. `M01-029` creates a distinct preparation-only
boundary so `M01-030` can replace historical identity aliases with exact
live-interval bindings without promoting modeled events into machine evidence.

## Files changed

- `src/OS.Kernel/Boot/M01LivePagingIntegration.cs`
  - Added `M01LivePagingExecutionMode`, preparation-only protection/evidence/
    state/result POD contracts, and `Prepare(...)`.
  - Added explicit pointer/count/capacity validation for every caller-owned
    input and output workspace.
  - Added exact byte-plan materialization/readback, map and reservation
    validation, page-table workspace construction, modeled RW-to-RX protection
    preparation, page-zero audit, cleanup planning, and final-state
    self-validation.
  - Added `ExecuteModelOnly(...)` and explicit non-claim flags while retaining
    the legacy `Execute(...)` body byte-for-byte.
- `tests/OS.HostTests/M01LivePagingIntegrationTests.cs`
  - Preserved the existing `m01.019` suite and added the separate `m01.029`
    marker contract.
  - Added positive, default-value, null, zero, short, excess, overflow,
    alignment, alias, overlap, stale-map, unsorted-ledger, malformed-preflight,
    malformed-byte-plan, undersized-workspace, repeated-use, no-partial-
    promotion, and legacy anti-promotion cases.
  - Added exact two-path page-zero source-firewall coverage and caller-line
    diagnostics for negative-contract mismatches.
- `docs/boot.md`
  - Documented the preparation-only boundary, caller-owned storage, validation
    order, failure semantics, and deferred live effects.
- `docs/architecture.md`
  - Documented the separation between preparation, legacy model transcripts,
    and future composite execution.
- `done/step874.md`
  - Preserved this implementation and verification record.

## Tests run

All authoritative commands used the exact four implementation files from
`codex/M01-029-preparation-contract`, copied byte-for-byte into an LF-normalized
detached validation worktree. SHA-256 comparison reported `MATCH=True` for all
four files.

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
git status --short
```

- Solution build: passed with 0 warnings and 0 errors.
- Host-test project build: passed with 0 warnings and 0 errors.
- Complete host harness: exit code `0`.
- Naming guard: `TEST_PASS naming.guard`.
- Source firewall: `TEST_PASS source.firewall`.
- QEMU dry run: `TEST_PASS qemu.runner.dryrun`.
- `git diff --check`: passed.

## Evidence

Authoritative host log:

- Path:
  `artifacts/m01-029-evidence/host-tests-final-20260727-202216.stdout.log`
- Length: `824266` bytes.
- SHA-256:
  `65468ECFFAA58AAD09D3259D35E8A7F6004ADA5406153077CD1FB7A9CD6C2653`
- Error log length: `0` bytes.
- Error-log SHA-256:
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Exit-record SHA-256:
  `5FECEB66FFC86F38D952786C6D696C79C2DBC239DD4E91B46729D73A27FB57E9`

Exact terminal-marker cardinality:

```text
TEST_START m01.029       1
CLROS_M01_029_READY      1
TEST_PASS m01.029        1
TEST_PASS host.tests     1
TEST_FAIL                0
```

Independent review verified:

- The legacy `Execute(...)` body remains identical to `origin/master`
  (`40717` characters).
- Topology and alias checks precede any preparation-state scan or write.
- The modeled protection leaf is restored on every post-mutation failure path.
- Preflight failure leaves every caller-owned output buffer unchanged.
- Final preparation-state promotion occurs only after candidate, evidence, and
  result validation.
- The new production contracts contain no managed references and preserve the
  no-stdlib build closure.
- Only the issue-owned production, test, and documentation paths changed
  before this evidence file was reserved.

Reference influence was test-only. The independently authored malformed-input
categories were cross-checked against:

- `references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs`
- Snapshot SHA-256:
  `B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6`

No SharpOS source was copied or materially adapted into CLROS.

## Failures

- The first complete host run in the primary worktree stopped at the existing
  M01-023 exact-source guard because the system Git setting had checked out the
  untouched, issue-forbidden `src/OS/Boot/EfiEntry.cs` with CRLF line endings.
  The test expects its audited source lines with LF. The production file and
  guard were not edited or weakened. A detached LF-normalized worktree was used
  for authoritative verification.
  - stdout SHA-256:
    `440669541125ECF263F50F5C4C767B701434DF49AE5077FD01C71E776ED3F9D4`
  - stderr SHA-256:
    `2F16F9036A924A7E451C59A4ED8ECCA920358705C5C4B4F649EA36B34D8032C8`
- The first normalized full run exposed an exact source-firewall expectation
  that still required one page-zero audit. The new preparation path correctly
  adds a second audit alongside the retained legacy path; the owned test now
  requires exactly two.
- The next run exposed a test-fixture defect: an explicitly null preflight
  pointer was silently replaced with the canonical observation. The fixture
  now passes the canonical pointer explicitly and forwards explicit null
  unchanged, proving the production null rejection.
- Independent review found that the original draft inspected preparation-state
  bytes before rejecting an alias with a smaller preflight object. Topology and
  alias validation now occurs first, with a regression test preserving the
  smaller buffer and its trailing canary.
- Independent review also found that the draft documentation overstated
  transactionality. The canonical docs now distinguish preflight atomicity,
  deterministic intermediate caller-workspace writes after preflight, and
  atomic final-state promotion.

## Deferred work

- `M01-030` must replace the historical identity aliases with exact
  live-interval bindings for the preparation and composite contracts.
- `M01-031` must size, place, materialize, and read back the prepared hierarchy
  in the exact physical table/state arena. The current workspaces are
  host-owned models and do not prove physical backing.
- `M01-032` must add the audited composite call gate, exact return site, and
  callback-free post-return RFLAGS observer. That issue remains host-only and
  does not execute generated code.
- The later `M01-025` integration must allocate the distinct live backing,
  consume the reviewed preparation/binding/arena/gate contracts, and invoke
  the audited gate through the opt-in `EfiEntry` stage-three adapter.
- No EFI link/input changed, no generated bytes were invoked, no privileged
  instruction executed, no CR3 or PTE changed, no TLB invalidation occurred,
  no RFLAGS restoration was observed, no real QEMU/OVMF run occurred, and no
  guest or completion-gate status changed.
