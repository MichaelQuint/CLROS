# Step 957 - Legacy default-profile selector repair

## Summary

The complete host attempt for #56/#526 stopped in the M01-023 source-contract
fixture. Its default-profile selector did not recognize the newly introduced,
disabled `M02_UNEXPECTED_VECTOR_PANIC_PROBE` symbol, so it retained the callback
branch and rejected `UnmanagedCallersOnly` in the supposed default view.

The repair adds exact known-false handling to that selector and the adjacent
M01-025 paging selector. The latter was found by source audit; it was not a
second observed failure in the aborted full run. Runtime, EFI and runner
behavior are unchanged. This step does not accept the full suite or any guest.

## Why

The source-contract fixtures must model the default-off probe accurately while
preserving their existing stage selections, nesting and unknown-symbol policy.
New controls exercise all four allocator and eight paging profile combinations,
nested branches, unknown directives and case aliases inside existing registered
groups. A repository scan found four legacy selectors; the loaded-image and
instruction-publication selectors already recognized the new symbol.

## Files changed

- `tests/OS.HostTests/M01LiveAllocatorEfiEntryTests.cs`
- `tests/OS.HostTests/M01LivePagingEfiEntryTests.cs`
- `docs/repo-audit.md`
- `done/step957.md`

The audit document also records the separately completed static native review:
5,208 bytes including the 232-byte bridge fit the 8,176-byte callback budget.
Fresh same-image association, actual guest evidence and the inherited entry-ABI
prerequisite remain necessary. Steps955/956 and all failed attempts are retained.

## Tests run

The failed complete host command was:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

At source `74bb2e7a2c58aed77d93b06c8869be36bebbc189`, tree
`f1615a47cda9f9982c449dde6248b63f20fbb723`, it ran from
2026-10-03T09:19:31.4301365Z to 2026-10-03T10:07:54.9661302Z.
Actual dotnet exit was -532462766; the capture wrapper exited 1. There were
1,252 starts, 1,250 passes, three failure records and no terminal host pass.
All three failure records describe one failing source-firewall group:

```text
TEST_FAIL m01.023 detail=default-profile-token=UnmanagedCallersOnly
TEST_FAIL m01.023 source firewalls preserve the prior EFI boundary
TEST_FAIL m01.023 failures=1
```

Parent rehashed the complete raw logs and all 508 frozen source inputs; no
source changed during the failed run.

The successful repair commands ran in
`C:/Users/Roger/.codex/worktrees/m02-016-panic-efi/CLROS`:

```powershell
dotnet build artifacts/issue526-m01-selector-repair/focused/Focused.csproj -c Release --nologo -m:1 -nodeReuse:false
dotnet artifacts/issue526-m01-selector-repair/focused/bin/Release/net10.0/Focused.dll
dotnet build tests/OS.HostTests/OS.HostTests.csproj -c Release --nologo -m:1 -nodeReuse:false
```

Both complete, unmodified module initializers passed at
10:16:45.542313-10:16:47.621447 UTC: 28 starts, 29 passes, zero failures, one
terminal host pass, native exit 0 and empty stderr. Both builds returned 0 with
zero warnings/errors and empty stderr; normal compilation finished at
10:16:53.260189 UTC. The harness links the actual two files, HostToolProcess
and kernel/shared projects without selecting methods or rewriting initializers.
All 26 subgroup names and two module envelopes are unchanged.

Parent reviewed the complete diff, raw results, harness and four-selector scan,
rehashed all 168 author evidence records, then read the independent review and
rehashed its 20 final records. The independent review additionally checked all
102 current run inputs, including the unchanged 95 production inputs. Parent
imported only the two exact reviewed LF files after verifying both original
destinations against the failed run's source inventory.

Parent `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
.\scripts\check-naming.ps1` returned 0 with the two expected naming.guard and
source.firewall start/pass pairs. `git diff --check` also returned 0.

## Evidence

Evidence paths below are relative to `artifacts/orchestration-20261001/`.

| Record | SHA-256 |
| --- | --- |
| `runtime-next-wave-74bb2e7/result.json` | 36F0ECABF0622F3FC1409C2ED16FDFD835F8DBCC5EADC07A8051E4BE803B34C3 |
| Completed raw stdout | 4557630664635434561271015ECE230146510A996D37194FDDD405F66BD06675 |
| Completed raw stderr | 904C593CC36A2F33AAE9A5ACB18B587282EDF151F353813E5779F8FA00D5902E |
| `issue526-fullhost-failure-74bb2e7/parent-readback.json` | 5341D5B7E50B466720483EB55BD40E6367BF524344FFBDC2FA131727D876F11E |
| Static native review report | 3CB970E90698995474A3F6B55F11853BABB5A743D365533EF0FC93D1E9640843 |
| Author focused attempt2 result | C18E7888699B652EFEC9738DE4D807BA853618A36D9BC09A979FBFCEB6650C51 |
| Author focused raw stdout | 29AAA3EDB6AE3EA26B787F1CBA3B622ECD203CAD6025326B54BB8D39E28ED92B |
| Author final-v2 evidence manifest | C2432F2333D55584B6891EE1EDCEBED0A3A3E7AFFA85F713BA11BA662336CEE7 |
| Independent selector review | A8B9A0BB6325269DA788B8B785266F1E3EBE94EF1DC0F943CE9AF5FE7FCFD975 |
| Parent two-file import | 14183178198730162538277C341C7A7F50C4EB5F3E753FA9412C0BAEEAB31058 |
| Final M01LiveAllocatorEfiEntryTests.cs | FCF1E915E6CCA7442373131350F5C3DC0FF22B95D6624EF801DD0EEB9B42B770 |
| Final M01LivePagingEfiEntryTests.cs | 74A649E0281628642ADCFBA67FB9B97923B6FF1EAA21D035DE1C9DEAA7067925 |

The author records are under `artifacts/issue526-m01-selector-repair/` in the
author worktree; final-v2 contains the report and two-path import manifest.
Parent import is `issue526-parent-selector-repair-import/accepted-import.json`;
independent review is `issue526-legacy-selector-independent-review/review.md`.

Issue #526 comment14379 and #56 comment14380 were posted and read back with
both issues still open. PR527 remains draft and unmerged. No failed attempt is
relabeled as passing.

## Failures

The complete host failure above is preserved in full. The first ignored focused
harness build used an incorrect relative project/source path and stopped before
tests; its raw logs and native result remain retained. The harness path repair
does not change production code or the frozen fixture patch.

The first author evidence packaging guard compared literal `dotnet.EXE` with
`dotnet.exe` and failed despite identical executable bytes. A new preserved
package uses Windows path identity plus exact hashes; tests were not rerun or
rewritten. Independent review's path-separator comparison and in-process log
manifest issues were likewise preserved and corrected in its final records.

Direct invocation of the step allocator was rejected by the local execution
policy. The documented Windows PowerShell child invocation with
`-ExecutionPolicy Bypass` then reserved this file successfully; no system policy
was changed.

## Deferred work

- Fresh committed-source solution/HostTests build and complete host run.
- Nine fresh guest lanes with raw/lifecycle, native and disk acceptance.
- PR527 merge and evidence-backed issue closure.
- String native integration, memory-map work and the remaining backlog.

## Evidence boundary

This repair concerns host source-contract fixtures. It establishes no new
runtime capability, full-suite pass, genuine guest panic, real ExitBootServices,
hosted .NET/Kestrel, cloud or production readiness. Gates3/4 remain Fail.
