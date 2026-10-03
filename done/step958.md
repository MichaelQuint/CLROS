# Step 958 - Panic profile source-contract fixture repair

## Summary

The complete host retry at `aa84d3dbb7aaebeebeb04b1ee17cbf334c45df92`
stopped in the original M02-005 entry-order/result group. Its global source
count expected three COM1 backend constructions. The new, separately gated
panic authority adds a fourth; the original three constructions are unchanged.

A bounded related-fixture audit found three further stale profile counts in
the fixed-allocation, array-allocation and managed-heap integration fixtures.
Those modules were not reached by the failed full run. Their source-derived
conflict lists now include the panic profile. These are fixture repairs;
production EFI, runtime, runner and script behavior remain unchanged.

## Why

Keep the original assertions exact while representing the newly integrated
default-off panic profile. M02-005 retains its ordering, result and transport
checks, requires the original three constructions outside the exact panic
gate, and binds the fourth construction to the retained panic binding.

The fixed/array conflict counts become 19/21. The heap list becomes 17, retaining
the original 16 diagnostic checks and selecting the exact earlier panic
diagnostic only for the new panic case. The heap authority child's diagnostic
and paired expectation also report the actual 32 flags; its existing 32-flag
validation loop is unchanged.

## Files changed

- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04RhpNewFastEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ArrayAllocationEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ManagedHeapPageRangeEfiIntegrationTests.cs`
- `docs/repo-audit.md`
- `done/step958.md`

This step was reserved with `scripts/new-done-step.ps1 -Reserve`. Parent imported
the four reviewed fixture snapshots byte-for-byte after checking every original
destination against the aa84d3d baseline. Steps955/956/957 are unchanged.

## Tests run

The complete original COM1 module ran through its actual ModuleInitializer:
5 starts, 6 passes, zero failures, native exit 0 and empty stderr. Its original
four groups plus envelope remain intact. The focused build and normal
HostTests Release build each passed with zero warnings/errors. Parent reviewed
the actual project, raw output and commands and rehashed all 141 evidence
records; all 106 run inputs, including 95 production sources, stayed unchanged.
Independent review accepted that complete module and the exact source delta;
parent rechecked its four review records and eleven input identities.

The three complete allocation modules then ran together: 20 starts, 21 passes,
zero failures, native exit 0 and empty outer stderr. Their original registered
groups are unchanged. Focused and normal HostTests Release builds passed with
zero warnings/errors. All 490 recorded inputs, including 95 production files,
stayed unchanged. All eight original fixed/array NativeAOT artifact lanes
published and verified successfully with their exact 82/95/32 source and flag
inventories. Their existing nested helper logs retain their own evidence scope;
an artifact publish is not a guest run.

Independent review accepted the completed allocation package. Parent then
imported all four fixture changes. A fresh committed-source solution build and
full host suite remain pending; neither is inferred from the focused checks.

Parent ran `scripts/check-naming.ps1`: both `naming.guard` and `source.firewall`
pairs passed, native exit 0 with empty stderr. The command ran from
`2026-10-03T12:21:49.3469196Z` to `2026-10-03T12:22:29.5145774Z`;
its stdout SHA-256 is
`F859FFF4688D92C662BD161F4DEDF4C64778B133B7ABDBED70D634D8A2F4B651`.
Actual command/result and separate stdout/stderr files are under
`artifacts/orchestration-20261001/issue526-profile-fixture-milestone-checks/`.
`git diff --check` passed after the exact imports and documentation changes.

## Evidence

Completed COM1 evidence is in the panic worktree under
`artifacts/issue526-com1-construction-repair/`:

- Final report: `81325B36EA5E12600736FD0DCADD4063C977D970A62592FB07BD645264AE8A99`.
- Actual batch result: `CE20FE2988F1F4C69E19FB0748ED468B1A6384AEB17A60FBDF49F7FD7ABF6B95`.
- Focused stdout: `EA2EE92B52C297B6DF0BE24EE74503AA749BEFE7F24E01783576A3691E5F68F2`.
- Independent COM1 review:
  `C08CD8B4B11371AB88AA435BF2459BE8C1CCB3EB9B2531A9C93136E675851117`.
- Focused run: `2026-10-03T11:42:31.937849Z` to
  `2026-10-03T11:42:32.053236Z`; normal compilation ended
  `2026-10-03T11:42:44.765506Z`.

The parent failure readback is
`artifacts/orchestration-20261001/issue526-fullhost-failure-aa84d3d/parent-readback.json`.
All 508 frozen source identities remained unchanged during that failed run.

The bounded adjacent-fixture audit is
`issue526-adjacent-fixture-audit-aa84d3d/report.md`, SHA-256
`5A1A69D1409E85DD651B53150A54F5CB77C987B43BDA20ECC28142732D83CF7C`.
Its 43 measured Compile-index relations and nine scoped token predicates are
source inspection, not additional executed tests. The independent final
allocation-fixture source review is
`3F53C41FE2128381342D2609A6B5FC47D06BC64640CDB1AE7D469347FC1DBD39`;
parent rechecked its four records and 31 inputs. The completed execution review
is `E4803077F992193305E94D65AE0C165122FD7CA470ED82BA3938E658717599A4`;
parent rechecked its four records and 13 inputs. That review also verified all
489 worktree-local snapshots and the actual 32-flag child diagnostic. Native
artifact child logs combine stdout/stderr, so no separately archived empty
child-stderr claim is made.

Completed allocation evidence is in the panic worktree under
`artifacts/issue526-allocation-conflict-repair/`:

- Final report: `0C89355DD03C1CAFE19E767947EA2F3F227A7688DE7EEA232EE3E87B81847F99`.
- Actual batch result: `7099E9FFB98DAE6F80774687A6B2C042008962B29A87F57B520BE86E8898B865`.
- Focused stdout: `F36413D89801F70FED9A1E929BF6956AA6AD7C02DA40A89DA52C9C612BF50309`.
- Eight-lane inventory: `3CC22CE9293ED6ADD506ABF4FBD8BE07655E27E104EB82231105FDBB37D4F579`.
- Focused run: `2026-10-03T11:50:17.375320Z` to
  `2026-10-03T12:06:01.218854Z`; normal compilation ended
  `2026-10-03T12:06:07.244318Z`.

Parent read the complete original-harness inputs and raw result, rehashed four
package, 546 evidence and 2,233 generated records, and checked all eight
artifact lanes' actual exits, publish/verify markers and complete 32-property
vectors. Both default artifacts retain EFI SHA-256
`387E1BA7F8CC22E9C869C795353D90F7B75A1FDFBCB789A0353D04C7050D4352`.

The accepted parent import record is
`artifacts/orchestration-20261001/issue526-parent-profile-fixture-import/accepted-import.json`,
SHA-256 `41EA031AA5ABFBE6E4E68730FF13DE58FE721C468D10F3FDC7D11F63EC1395EE`.
It binds every preserved destination baseline, immutable author snapshot,
complete-module result and independent review. All four paths already belong
to the existing 38 native/4 String host ownership set; ownership did not expand.

## Failures

The full aa84d3d attempt ran from `2026-10-03T10:39:11.4786423Z` to
`2026-10-03T11:32:58.7681498Z`. Actual dotnet exit was `-532462766`; its
capture wrapper exited 1. It emitted 1,417 starts, 1,415 passes, two failure
records for one M02-005 group, and no `host.tests` terminal pass. Its stderr is
258 bytes. The repaired M01-023 and M01-025 groups completed successfully in
this run; that narrower fact does not make the whole run pass.

- Result: `CCEB0C59A036313F8C0717B312F6BEDCF7792BCAB5997B59FA10CD82A664182C`.
- Stdout: `793BC757B26A118EB8F4248EA7D193E5F31165D99BB9FE10CE436C542844AE3E`.
- Stderr: `875A392BFD194B74B01208C8723255964DEA2EAB95D815BA095E56C5BF0A2C3B`.

The failed attempt and earlier step957 failure are preserved. Neither is an
accepted baseline or a partially resumed pass. The COM1 repair's focused
build/run batch had no failed attempt.

## Deferred work

Commit this reviewed milestone, then run a fresh same-head solution build and
complete host suite. The nine guest lanes
and their raw, lifecycle, same-image native and disk reviews remain required.
Issues #56/#526 and PR527 remain open; the PR remains a draft. Native String
construction/storage remains separate work under #78.

## Evidence boundary

This milestone supplies fixture/source-contract and focused host evidence.
It does not accept a complete host suite, guest, panic transition, live device,
post-ExitBootServices environment, hosted .NET/Kestrel or cloud readiness.
