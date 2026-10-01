# Step 925 - SharpOS September review and M04-004 host conformance

## Summary

Reviewed modern SharpOS through `dfad516b816d29b971d23ef116d4dbebeedcd25e`
(2026-09-30), 65 commits after the August review. Recorded the useful changes
in the canonical reference crosswalk and six existing Gitea issues, then
implemented the host-owned portion of #73 / M04-004.

The new allocation-free C# 14 runner executes 57 fixed integer vectors across
30 operations. Independent host tests check 6,300 boundary combinations with
arbitrary-precision arithmetic. Its stable fingerprint is `45FDFA24DD72EA28`.
The same production file also compiles with no standard library and zero
assembly references. No source was imported from either SharpOS repository.

## Why

Upstream diagnosed deeper-frame GC root loss, native frames misread as ILC GC
metadata, 32-bit HPET wrap/stale-sample failures, wait/lifetime races, folded
allocator counters, and divergent kernel/app std lists. These are useful
negative-test and design inputs for existing CLROS work. They do not provide
networking, Kestrel, or CLROS guest proof.

M04-004 was the only live `agent:ready` issue at task start; its dependency
#72 / M04-003 was closed. Its managed issue text mistakenly described network
packets and referenced unrelated GC source. Corrected that text and the
canonical manifest to describe integer conformance. Historical SharpOS's
`ulong` division helper is explicitly a placeholder, so it cannot be used as
an oracle. Reference hashes and the independent implementation boundary are
recorded in `docs/nativeaot-nostdlib-limits.md`.

## Files changed

- `.gitea/backlog/clros-issues.json`: correct only M04-004 scope/reference text;
  preserve its open state, dependencies, ownership, and guest-evidence gate.
- `docs/reference-map.md`, `docs/fork-diff.md`: pinned upstream review and
  source-to-backlog crosswalk, including runtime/compiler and ABI limitations.
- `docs/host-requirements.md`: tested recovery for the separately selected
  NativeAOT runtime pack missing from the host cache.
- `std/no-runtime/shared/IndependentArithmeticShiftCastN64BitDivisionRemainder.cs`:
  POD/status results, fixed vectors, runtime-supplied non-inlined evaluation,
  guarded divide/remainder, and ordered execution fingerprint.
- `tests/OS.HostTests/IndependentArithmeticShiftCastN64BitDivisionRemainderTests.cs`:
  independent oracle, boundary matrix, default/invalid cases, no-allocation
  check, exact markers, and no-stdlib metadata compile guard.
- `docs/nativeaot-nostdlib-limits.md`, `docs/completion-gates.md`: supported
  subset, rejection policy, source provenance, host/guest distinction.
- `done/step925.md`: this evidence record.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`: passed, zero warnings/errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`:
  initial build passed; the later solution build includes the corrected shim.
- `dotnet run --project artifacts/m04-004/focused/Focused.csproj -c Release`:
  passed the complete new test module, including 6,300 independent comparisons
  and the no-stdlib compile/zero-assembly-reference check.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  artifacts/review-20261001/test-m04-mutations.ps1`: three isolated Release
  builds with checked arithmetic enabled rejected deliberately wrong output
  (`UnexpectedValue`, index 4), status (`UnexpectedStatus`, index 51), and
  fingerprint (`FingerprintMismatch`, index -1). These are test-only copies
  under ignored artifacts; production source was not mutated.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`: naming and first-party source firewall passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -Offline`: passed, 429 entries checked,
  zero remote writes or failures.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun -Image
  artifacts/review-20261001/default.raw -SerialLog
  artifacts/review-20261001/default.serial.log -SummaryLog
  artifacts/review-20261001/default.summary.log`: passed planning checks only.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`:
  final run passed with exit `0`, 3,508 `TEST_START`, 3,509 `TEST_PASS`, zero
  `TEST_FAIL`, and exactly one terminal `TEST_PASS host.tests`. This includes
  the new M04-004 module and the existing EFI artifact-verifier matrix.
  Stdout is 454,199 bytes; stderr is empty.
- Restored `Microsoft.NETCore.App.Runtime.NativeAOT.win-x64` exactly at
  `10.0.8` with an ignored `PackageDownload` project and the official NuGet
  source. The isolated existing `TestValidArtifactPasses` test then emitted
  `TEST_PASS efi.restored-runtime.precheck`.
- `git diff --check`: passed; final status contains only the nine files listed
  above, all uncommitted. No forbidden first-party native source was added.

## Evidence

Branch: `codex/sharpos-20261001-m04-004`, based on clean, current master
`9791d60df1c0e8ca1f806df9233d0064ae696f3f`. SDK resolved to `10.0.302`.
No commit, push, merge, production change, or reference-archive mutation was
performed. Fetched reference research is under ignored
`artifacts/references/SharpOS-20261001`.

Logs are under `artifacts/review-20261001/`: `solution-build.log`,
`host-build.log`, `m04-focused.stdout.log`, `m04-focused.stderr.log`,
`m04-mutations.log`, `naming.log`, `naming-final.log`, `backlog-offline.log`, `qemu-dryrun.log`,
and the final `host-tests.stdout.log` / `host-tests.stderr.log`.
The no-stdlib compiler emits its separate build logs below
`artifacts/m04-004/nostdlib/<run-id>/`.

SHA-256 identities for the completed implementation/focused evidence:

These source hashes identify the working files used by the recorded full run.
During the subsequent authorized commit review, staged whitespace validation
found one extra blank line at EOF in each new C# file (the earlier unstaged
check did not inspect untracked files). Only those two blank lines were removed;
comparison with `git diff --ignore-blank-lines --exit-code` confirmed no
substantive difference. After cleanup the working-file hashes are
`B9916F8F3404CF46493AA4D4A1882DE97BF82936F6C420B9DF4D743367425F5D` for production
and `1BB854C00D09B2FFE2FFC85A8B6DF698F562EA88138C937B8645C0F63BA44794` for the
test source. The focused module was rerun after this formatting-only change;
its logs are `artifacts/orchestration-20261001/m04-focused-final.*.log`.

| Input or log | SHA-256 |
| --- | --- |
| Production conformance source | `FFCE13713CA32A8AF0907DFF961ACD236EDD8F86591A5F5D0892868D63AC2959` |
| Host test source | `A395BA6D8FE96C1680E7DD968D9FF6951E59EF39B1F753E2F04B25EA1602E8AA` |
| `solution-build.log` | `3FE1C1C9890C9D3BB00A54DB793698F19B7B2F9911223B59EADDC6853C5B8F1C` |
| `m04-focused.stdout.log` | `016B7929451EA183E0A934306A4BE0826183E61E88CF17AF8448DCEFD699833C` |
| `m04-mutations.log` | `ED616BE88966D9F98B1FF8C673561BE8FDD31F3F923668418BCBE43348DA0A0F` |
| Final `host-tests.stdout.log` | `4579E677F43FE7374E4A1F85340C276862498E50A6CA1E1D5F69A45ECBA65776` |
| Final `host-tests.stderr.log` (empty) | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |

The exact host envelope is:

```text
TEST_START m04.004
CLROS_M04_004_READY vectors=57 fingerprint=45FDFA24DD72EA28 referenceFilesRequired=false hostConformance=true guestIntegrated=false gate4=Fail
TEST_PASS m04.004
```

Gitea review addenda were posted and independently read back on existing open
issues #82 (comment 13378), #96 (13382), #191 (13383), #163 (13387),
#229 (13389), and #89 (13391). The pinned-runtime prerequisite was also
recorded on #229 (13392). Their state/dependencies were preserved. #73 was claimed by
`codex`, its scope correction was independently read back at content version
2, and it remains open. No completion-evidence marker was posted to imply
closure of M04-004. The final host evidence and log hashes were posted to #73
as comment 13393 and verified by read-back.

## Failures

- The first focused no-stdlib compile failed because the test-only shim
  omitted `System.Delegate` / `MulticastDelegate`. Added those compiler
  scaffolding types and reran successfully; production behavior did not change.
- A superseded full host run was stopped so the final run could use the
  corrected compiled test module. It is not counted as passing evidence.
- The first full-suite attempt aborted with 19 existing EFI artifact-verifier
  failures because the pinned NativeAOT runtime pack `10.0.8` was absent
  (`DirectoryNotFoundException`). The SDK-selected `10.0.10` pack was present
  but cannot substitute for the explicitly selected `10.0.8` runtime tree.
  Preserved `host-tests-missing-runtime.stdout.log` / `.stderr.log`, restored
  the exact missing package, passed the existing positive fixture separately,
  and reran the full suite successfully without changing production or test logic.
- Direct invocation of `new-done-step.ps1` was rejected by the host signing
  policy; the documented `powershell -NoProfile -ExecutionPolicy Bypass -File`
  invocation reserved this step successfully without changing machine policy.
- Gitea helper rejected a label update through its `Update` action before
  writing. Used the documented issue-label REST endpoint and independent GET,
  then the helper for assignee/body/comments. No credentials were persisted.

## Deferred work

- A separately owned M04-004 EFI integration must add the default-off profile,
  source-authority closure, adapter, and required/ordered/unique/forbidden
  guest markers before obtaining fresh real QEMU/OVMF serial evidence.
  The current issue forbids those shared integration paths for the host leaf.
- General runtime/BCL compatibility, checked overflow/exception delivery,
  floating-point, enum metadata/boxing, allocation/GC, live networking,
  hosted .NET, Kestrel, and provider readiness remain future work.

## Evidence boundary

This milestone proves only host arithmetic conformance and no-stdlib source
compilation, plus a source review of upstream changes. It adds no EFI compile
item, boot phase, or dangerous probe. The QEMU dry run is not guest proof.
M04-004 remains open and Gate 4 remains **Fail**. Upstream's source, performance
figures, hardware reports, and hosted runtime are not CLROS evidence.
