# Step 889 - Repair bounded M01 artifact verification and QEMU fixtures

## Summary

Repaired the reopened M01-034 host-evidence boundary and the two M01-026
QEMU-runner fixtures that prevented an uncompromised complete validation wave.
The accepted post-reboot wave is bound to source commit
`fa5950c11091201077648d65e021b0f15b0dfc3b`, tree
`1dbd6708ac0f9a501680dcfe7db4a407d3890a3f`, and evidence root
`artifacts/logs/m01-034-timeout-repair/20260730T125526Z-final-r8-restart/`.

The verifier now reports and separately bounds its actual 4,096-token link
response scan and 2,048-record runtime-function scan. Host process safety
ceilings allow verifier startup, source-closure validation, authority-handle
work, identity hashing, and complete diagnostic capture without weakening the
one-second parser bounds. The hostile build-wrapper timeout fixture uses the
minimum supported five-second verifier deadline and a separate 45-second outer
bound so the production Job Object remains the authority for termination and
process-tree reaping.

The canonical EFI publisher now correctly handles a genuinely clean artifact
directory. Zero stale publication leaves means there is no stale identity set
to compare; every nonzero stale set still requires distinct, single-link,
non-reparse identities before guarded removal, and every new leaf still uses
create-new publication.

M01-026's delayed fake-QEMU fixtures now keep their serial stream open while
writing and flushing terminal markers. Their five-second test timeout gives
the runner enough time to drain the deliberately delayed stream and classify
either the duplicate success terminal or the process's nonzero exit. These are
fixture-only changes; the production QEMU runner and its fail-closed
classification were not changed.

The complete accepted wave passed after a fresh machine reboot. It contains
3,304 `TEST_START` markers, 3,305 `TEST_PASS` markers including the sole
terminal `TEST_PASS host.tests`, and zero lines matching `^TEST_FAIL `. The
host log legitimately contains the literal text `TEST_FAIL` inside a passing
test name, so only the machine-marker prefix is counted. Both managed builds,
the all-three-switch canonical EFI build, the independent artifact verifier,
naming/source-firewall checks, and whitespace checks exited zero.

No production kernel or EFI C# source changed in this repair wave. This step
does not replace M01-026's required renewed source-bound real-QEMU/OVMF run
after these repairs merge.

## Why

The original M01-034 closure used a 30-second host-process limit for expensive
negative and maximum-capacity verifier fixtures. Under host contention that
outer wrapper expired while the verifier was still making bounded progress,
discarding the verifier's precise rejection reason. The response-file work was
also not timed as a distinct parser operation, so simply increasing the
process limit would have removed the intended performance evidence.

The first complete repair wave then exposed two independent test-fixture races
in M01-026. Command-shell redirection reopened the fake-QEMU serial file for
each line; the real runner could observe and stop on the first success terminal
before the delayed duplicate or process exit became visible. Keeping one
shared stream open and flushing at the intended boundaries makes the fixtures
deterministic without changing production runner behavior.

Finally, the first otherwise complete host/build wave exposed a vacuous-array
PowerShell binding defect in the canonical publisher:

```text
TEST_FAIL efi.shell.publish output_authority_failed detail="Cannot bind argument to parameter 'Records' because it is an empty array."
```

The distinct-authority assertion is meaningful only when stale leaves exist.
Skipping that one call for a count of zero repairs clean publication while
preserving all nonzero identity, hard-link, reparse-point, deletion, and
create-new guards.

## Files changed

- `scripts/test-efi-artifact.ps1`
  - Measures the complete link-response parsing and normalization work while
    excluding authority-handle and identity-hash I/O.
  - Emits the stable
    `EFI_ARTIFACT_LINK_RESPONSE_SCAN tokens=<n> elapsedMs=<n> maximumTokens=4096`
    record.
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
  - Raises only the generic verifier fixture safety ceiling from 30 to 45
    seconds.
  - Retains one-second independent response-token and runtime-record scan
    limits and requires exactly one stable marker for each.
  - Uses object-shaped response tokens at the maximum-capacity boundary so the
    measured work includes per-token path normalization and matching.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
  - Gives the hostile unkillable-verifier fixture a five-second inner deadline
    and 45-second outer safety bound.
  - Requires a clean-output source/build path and freezes the zero-stale-leaf
    conditional authority assertion.
- `scripts/build-efi-shell.ps1`
  - Calls `Assert-M01DistinctAuthorityRecords` only for a nonempty stale-output
    identity set.
- `tests/OS.HostTests/QemuRunnerTests.cs`
  - Keeps each delayed fake-QEMU serial writer open across its marker delay,
    flushes at the intended observation boundaries, preserves the requested
    process exit code, and uses a five-second fixture timeout.
- `docs/efi-build.md`
  - Records the split parser/process bounds and the clean-output publication
    authority contract.
- `done/step889.md`
  - Preserves this repair, all rejected attempts, the accepted gate, exact
    hashes, and the evidence boundary.

The effective M01-034 repair owns the build/verifier scripts, their focused
host tests, `docs/efi-build.md`, and this step. The QEMU fixture ancestry is a
separate, narrow M01-026 test repair and must merge first without squash or
rebase so its two commits remain ancestors of the M01-034 repair.

No `src/OS`, `src/OS.Kernel`, `EfiEntry.cs`, `OS.csproj`,
`BootSequence.cs`, `Probes.cs`, completion-gate document, reference snapshot,
or first-party `.c`, `.cpp`, `.h`, `.asm`, or `.s` file changed.

## Tests run

Accepted fresh post-reboot wave:

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Exit zero; zero warnings; zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Exit zero; zero warnings; zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Exit zero.
  - 3,304 exact `TEST_START` lines.
  - 3,305 exact `TEST_PASS` lines.
  - Zero lines matching `^TEST_FAIL `.
  - Exactly one terminal `TEST_PASS host.tests`.
  - Exactly one M01-034 aggregate start, ready, and pass.
  - Exactly one pass for each decisive repair check:
    `m01.034 bounds verifier execution and reaps its process tree`,
    `efi artifact verifier rejects missing, duplicate, and fallback serializer
    link bindings`,
    `efi artifact verifier bounds response and runtime parsing`,
    `qemu real mode rejects duplicate success terminals`,
    `qemu drain classifies nonzero exit before kill`, and
    `ci.smoke returns nonzero on failed command`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe
  -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe`
  - Exit zero.
  - Source authority: 57 inputs.
  - M01 source closure: 44 compile items.
  - Nested `TEST_PASS efi.artifact.verify`.
  - `TEST_PASS efi.shell.publish`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\test-efi-artifact.ps1 -EfiArtifact
  .\artifacts\efi\BOOTX64.EFI`
  - Exit zero.
  - `TEST_PASS efi.artifact.verify`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - `TEST_PASS naming.guard`.
  - `TEST_PASS source.firewall`.
- `git diff --check`
  - Exit zero.
- Final `git status --short`
  - Only the reserved `done/step889.md` was untracked during the accepted
    source-bound wave.

Before the complete wave, focused repeated fixture runs passed for the
duplicate-terminal and delayed-nonzero-exit cases after each corresponding
fixture repair. The previously slow CI-smoke case also passed in focused
reproduction without a source change, and the clean-output authority repair
passed both its focused preflight probe and an exact all-three-switch canonical
EFI build from an empty publication directory. Every such pre-r8 focused or
repeated result is diagnostic-only, superseded, and not closure evidence. The
sole accepted closure artifact is the fresh post-reboot r8-restart wave.

No QEMU guest was launched by this M01-034 repair wave. The EFI preflight's
tool-version discovery is not guest evidence.

## Evidence

### Accepted source and terminal state

```text
branch=codex/M01-034-timeout-repair
commit=fa5950c11091201077648d65e021b0f15b0dfc3b
tree=1dbd6708ac0f9a501680dcfe7db4a407d3890a3f
PASS|waveCommit=fa5950c11091201077648d65e021b0f15b0dfc3b|waveTree=1dbd6708ac0f9a501680dcfe7db4a407d3890a3f
```

The accepted executable-source tree is
`1dbd6708ac0f9a501680dcfe7db4a407d3890a3f`. `done/step889.md` was the
sole untracked path during the gate and was committed afterward as a distinct
documentation-only evidence record. Its staged content passed
`git diff --cached --check`; no executable or test evidence is attributed to
the later documentation commit.

The eight accepted steps and UTC intervals were:

| Step | Started | Completed | Exit |
|---|---|---|---:|
| Solution build | 12:56:02.884 | 12:56:25.754 | 0 |
| HostTests build | 12:56:25.764 | 12:56:31.043 | 0 |
| HostTests run | 12:56:31.043 | 13:31:02.179 | 0 |
| EFI build | 13:31:02.227 | 13:31:46.207 | 0 |
| EFI verifier | 13:31:46.207 | 13:32:00.538 | 0 |
| Naming/firewall | 13:32:00.538 | 13:32:25.563 | 0 |
| Diff check | 13:32:25.563 | 13:32:25.608 | 0 |
| Final status | 13:32:25.608 | 13:32:25.654 | 0 |

The exact M01-034 and performance records were:

```text
CLROS_M01_034_READY objectBytes=336 objectSha256=DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B successCookie=0x53524C43
EFI_ARTIFACT_PERFORMANCE responseTokens=4096 responseScanElapsedMs=191 distinctUnwindRecords=2048 processElapsedMs=18319 scanElapsedMs=148 processSafetyBudgetMs=45000 responseScanBudgetMs=1000 runtimeScanBudgetMs=1000
```

The failure watcher observed no failure and stopped normally. The auxiliary
priority monitor was ready before step one and stopped after the gate. It
recorded four one-sample `SetValueInvocationException` retries and 635 benign
process-exit races, no repeated retry for any PID, and empty stderr. It was not
an acceptance authority. The gate, failure-watcher, and priority-monitor
processes were absent after completion.

### Final EFI artifact

- `BOOTX64.EFI`
  - 459,264 bytes.
  - SHA-256
    `76F2B404E91EB5E62CAD80B797B936BA3D3D39D5B0577889BB8459AF1B55C47C`.
- Link evidence JSON
  - 393,225 bytes.
  - SHA-256
    `5A29F0AA38F12437C5A267EBF22A2F53F35DBFA0533B7ED6AEEC71F66F4C5691`.
- Link map
  - 201,098 bytes.
  - SHA-256
    `E53A92225D51717A88D406D136D48C53DC18E2BE619F38F56B80D8EF5315A49F`.
- Link response
  - 522 bytes.
  - SHA-256
    `E8ADEC521A4464E80292CB74C58F54A2E9003084304CE3345BE847CA2912892E`.
- Serializer object
  - 336 bytes.
  - SHA-256
    `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.

The independent verifier observed PE32+, AMD64 `0x8664`, EFI application
subsystem 10, `EfiMain` bound to the same-build managed symbol, and absent
import, bound-import, IAT, and delay-import directories. It scanned 15 link
response tokens. The frozen source closure contains 57 inputs with aggregate
SHA-256
`50892A8E3DACBA3D4340C9BB01AF72E8F76356666CCCB5D27909716BB1DA6E83`
and zero current mismatches. The `.pdata` exception directory is at RVA
`0x001A6000`, has size 9,684 bytes, and therefore contains exactly 807
12-byte runtime-function records; the verifier observed 621 distinct unwind
records. The same-build managed `OS.dll` is 539,136 bytes with SHA-256
`40B56DD4861C0DE4AAE144C5EA77BB988DE082C54B9DF882C394DED229430A16`.
The exact 20-byte
`CLROS_X64_SerializeInstructionPublication` body remained at `.text` RVA
`0x1000` with unwind RVA `0x6F0D8`, expected cookie `0x53524C43`, and SHA-256
`DF76C8E39E0768D26EA27206FF102D7EADCC35D2C9BDF547B52B1171FD47BDAF`.

### Accepted log hashes

- Solution build stdout:
  `8A8A242D90F11027BB537D8A476BB4F0211A0412D116CAD47175519D6EB77F6E`.
- HostTests build stdout:
  `5FF021FC744F7E7FF0D00CCA63952080B7464495A6DA42968297DC78DFA1C250`.
- HostTests run stdout:
  `2BE9108AED4A0CA7B3D9E25FCE7C9FD5EF13AF9FFD3D918810978C7B61FF41AE`.
- EFI build stdout:
  `079B00D0583E6AD88E517EBF4E3E56EA05708882C831BD40866A9357F8B27354`.
- EFI verifier stdout:
  `7792DDBF723B8098A02FF3B2DACC51FDA6F1AAD32B23DD9D27BDC640BDCC3A8D`.
- Naming/firewall stdout:
  `FB9E2F9DEB178474FD6713AA598348A053B28FC79CA27732159CDD0AC565CE18`.
- Final status stdout:
  `BC368D30020AB42CD74336213352A204F59716E800E1C704910CFA1DAB4019C5`.
- Source-state record:
  `9DDD446060632EFDA3E169DC11E2D18A2BBDF511B29A49526F07D1A0F5AE10CB`.
- Terminal state record:
  `603422C2826E1F3A4D5BBB738E5B45488F9846F14EB074A0A0A7F77ACD11D1C4`.

Every accepted stderr log and both diff-check streams were empty, with SHA-256
`E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

## Failures

Rejected and aborted runs were preserved and were not relabeled as evidence:

- r1, `20260729T174807Z-final`: the link-binding negative fixture exceeded
  its old 30-second outer limit.
- r2, `20260729T183625Z-final-r2`: the maximum-capacity verifier process
  exceeded the old 30-second outer limit even though its measured parser work
  remained under one second.
- r3, `20260729T194044Z-final-r3`: manually stopped because the timer did not
  yet cover all response-token work and its metadata was stale.
- r4, `20260729T194924Z-final-r4`: the M01-034 checks passed, then the complete
  suite exposed the delayed duplicate-terminal QEMU fixture race.
- r5, `20260729T205810Z-final-r5-wave`: the duplicate-terminal fixture passed,
  then the complete suite exposed the delayed nonzero-exit fixture race.
- r6, `20260729T220056Z-final-r6-wave`: the CI-smoke test exceeded the outer
  120-second host wrapper while still progressing. Focused reproduction and
  the direct script completed successfully, so no source change was made.
- r7, `20260729T223427Z-final-r7-wave`: all 3,305 host pass markers and zero
  lines matching `^TEST_FAIL ` completed, then the canonical clean-output EFI
  build exposed the empty stale-authority-array defect quoted above.
- Pre-reboot r8, `20260729T234550Z-final-r8-wave`: deliberately stopped at the
  user's reboot boundary after 386 starts, 384 passes, and zero failures. Its
  `abort.txt` records `ABORTED_INCOMPLETE_NOT_EVIDENCE`.

Only the fresh post-reboot r8 restart is accepted.

## Deferred work

- Merge the two M01-026 fixture commits first while keeping M01-026 open, then
  merge the M01-034 repair and close the reopened issue only after independent
  review and exact tracker evidence.
- Merge the resulting master into M01-026 and run a new complete host gate and
  fresh real QEMU/OVMF run with all three M01 switches and `-BuildEfi`. The
  earlier M01-026 tuple is stale with respect to this repair wave.
- This step proves host-process bounds, deterministic negative fixtures,
  canonical EFI publication, and static final-artifact identity only. It does
  not prove guest execution, physical hardware, a provider, real
  `ExitBootServices`, permanent paging ownership, hosted .NET 10, Minimal API,
  Kestrel, CloudReady status, or production readiness.
- No completion gate advances in this repair step.

## Evidence boundary

This step proves only host-process bounds, deterministic negative-fixture
behavior, guarded canonical EFI publication, and static same-build
final-artifact identity for the exact accepted source tree. The two M01-026
QEMU tests are synthetic host fixtures; they are not guest evidence.

It does not prove that the serializer executed or returned its cookie, that a
guest booted under real QEMU/OVMF, physical-hardware or provider execution,
empirical SCSB exclusion, cross-core instruction publication, live CR3/PTE/TLB
effects, real `ExitBootServices`, hosted .NET 10, ASP.NET Minimal API, Kestrel,
CloudReady status, production readiness, or any completion-gate advancement.
M01-026 still owes a new complete host gate and fresh real QEMU/OVMF run on the
final merged source before its earlier technical evidence can be promoted.
