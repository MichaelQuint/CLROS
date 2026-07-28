# Step 879 - M01-021 probe-scoped mutation-selector repair

## Summary

Repaired two M01-021 host-test mutation-selection defects without changing
production source:

- Generic token mutations now skip matching tokens in earlier unrelated probe
  regions.
- Method-body mutations now skip earlier same-named methods and token
  occurrences outside the stage-one probe.

Both selectors accept only the first candidate that changes the extracted
`M01_LOADED_IMAGE_STACK_PROBE` branch. Empty or missing inputs and sources with
only out-of-scope matches fail closed with the original source unchanged.

The complete host harness passed with a recorded native exit code of zero,
exactly one final `TEST_PASS host.tests`, zero `TEST_FAIL` prefixes, and zero
M01-021 diagnostic prefixes. A focused runner also passed against the parked
M01-025 production source that contains both collision families.

## Why

The parked M01-025 branch placed `AllocatePages`, `EfiLoaderCode`, and
`BootReservationTableValidator` tokens in an earlier
`M01_OWNED_PAGING_PROBE` region. The old generic representative-mutation loop
used `TryReplaceFirst` on the complete `EfiEntry.cs` source, while
`ValidateContract` evaluates only the extracted
`M01_LOADED_IMAGE_STACK_PROBE` branch. A mutation could therefore change an
unrelated probe while leaving the intended stage-one contract unchanged.

The same source shape exposed an independent method-level first-match defect.
M01-025 has earlier `TryValidateAllocation`, `RangesOverlap`, and
`WriteSuccessLine` methods. The old method helper selected only the first
same-named method globally, so it could mutate an unrelated probe or stop when
that earlier method lacked the requested stage-one token.

`TryReplaceFirstAffectingProbeBranch` now walks every ordinal token occurrence.
`TryMutateFirstMethodBodyAffectingProbeBranch` enumerates sanitized static
method declarations, finds exact raw body bounds, walks every bounded ordinal
token occurrence, and constructs candidates from absolute original-source
offsets. Each helper accepts only a candidate whose extracted stage-one branch
differs from its baseline.

This is a host-test scoping repair. It does not identify or conceal a production
M01-021 defect, and it does not reorder or reformat production `EfiEntry.cs`.
The accepted production source/build record in `done/step869.md` remains
unchanged.

## Files changed

Across the reopen intent and implementation:

- `.gitea/backlog/clros-issues.json`
  - Reopens M01-021 and its dependency descendants without rewriting their
    historical evidence.
  - Adds the token- and method-selector repair contracts.
  - Preserves prior M01-021 evidence as an exact ordinal prefix.
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
  - Adds `done/step879.md` to M01-021's exact owned-path expectation.
- `tests/OS.HostTests/M01LoadedImageStackEfiEntryTests.cs`
  - Adds exact prefix-collision and same-name method-collision fixtures.
  - Adds `TryReplaceFirstAffectingProbeBranch`.
  - Adds `TryMutateFirstMethodBodyAffectingProbeBranch`.
  - Routes every generic token mutation and every EFI method mutation through
    the appropriate probe-scoped selector.
  - Leaves the runtime-source mutation path and existing unscoped helper
    behavior unchanged.
- `done/step879.md`
  - Preserves the diagnosis, implementation, validation, failures, and evidence
    boundary.

No production, EFI, kernel, runtime, application, canonical documentation,
completion-gate, reference, or historical done-step source changed.

## Tests run

- Focused M01-021 runner in the implementation worktree:
  - Passed every M01-021 subtest.
  - Emitted exactly:
    - `TEST_PASS m01.021 probe-scoped mutation selector skips prefix collisions`
    - `TEST_PASS m01.021 probe-scoped method selector skips same-name collisions`
    - `TEST_PASS m01.021 contract validator rejects representative bypasses`
    - `CLROS_M01_021_READY`
    - `TEST_PASS m01.021`
- Focused M01-021 runner against parked M01-025 source commit
  `4b42f11dd5a8cb30d91a58f88a2f0cbb17bdbe0e`:
  - Exited zero.
  - Emitted the same exact M01-021 markers once each.
  - Emitted zero `TEST_FAIL` and zero `M01_021_DIAGNOSTIC` prefixes.
- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with zero warnings and zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
  - Passed with zero warnings and zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Passed with a recorded native exit code of zero.
  - Ended with exactly `TEST_PASS host.tests`.
  - Emitted zero `TEST_FAIL` prefixes.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe`
  - Passed `efi.artifact.verify` and `efi.shell.publish`.
  - Verified PE32+ x64, EFI application subsystem 10, a nonzero file-backed
    entrypoint, same-build `EfiMain` association, and absent ordinary, bound,
    IAT, and delay imports.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - Emitted `TEST_PASS naming.guard`.
  - Emitted `TEST_PASS source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - Emitted `TEST_PASS qemu.runner.dryrun`.
  - This validates command construction only and is not boot evidence.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -SelfTest`
  - Emitted `SYNC_SELF_TEST_MANAGED_BODY_PASS`.
  - Emitted `SYNC_SELF_TEST_STATE_POLICY_PASS`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -Offline`
  - Validated 16 milestones, 68 labels, 16 epics, 321 children, and 337 issues.
  - Reported 421 skipped offline objects and zero failures.
- Live no-write synchronization preview:
  - Reported 420 unchanged objects.
  - Reported exactly one pending update, `issue:M01-021`.
  - Reported zero creates, skips, and failures.
- `git diff --check`
  - Passed.

## Evidence

Intent and implementation provenance:

- Issue: `#328`.
- Intent branch: `codex/M01-021-regression-intent`.
- Intent commit:
  `109b149b2dad6622b3b08fea5c8a05da1517f268`.
- Intent pull request: `#406`.
- Intent merge commit:
  `db9f7c9c10c8a5ed64ba7c4978c6694ebf863865`.
- Implementation branch:
  `codex/M01-021-scoped-representative-mutations`.
- Implementation commit:
  `f7251ac27b9c29a64d7c6ae7c4687318d2555196`.
- Implementation pull request: `#407`.
- Independent reviews covered selection semantics, ordinal/overlap traversal,
  raw body bounds, newline portability, exact M01-025 collisions, runtime-path
  isolation, and manifest history preservation. No merge blocker remained.

Authoritative full-host evidence:

- Local ignored stdout:
  `artifacts/evidence/m01-021-regression-20260728-054645-326/host-final.stdout.log`
- Stdout size: 414,916 bytes.
- Stdout line count: 6,536.
- Stdout SHA-256:
  `D1A2EBB0A3419E7C116B41EED84261BE7DAE196C2EDE8FA079066A3DF347646E`.
- Local ignored stderr:
  `artifacts/evidence/m01-021-regression-20260728-054645-326/host-final.stderr.log`
- Stderr size: 0 bytes.
- Stderr SHA-256:
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Recorded native exit code: 0.
- Final non-empty line: `TEST_PASS host.tests`.
- Exact marker counts:
  - `TEST_START m01.021`: 1.
  - `TEST_PASS m01.021 probe-scoped mutation selector skips prefix collisions`:
    1.
  - `TEST_PASS m01.021 probe-scoped method selector skips same-name collisions`:
    1.
  - `TEST_PASS m01.021 contract validator rejects representative bypasses`: 1.
  - `CLROS_M01_021_READY`: 1.
  - Exact `TEST_PASS m01.021`: 1.
  - Exact `TEST_PASS host.tests`: 1.
  - Any `M01_021_DIAGNOSTIC` prefix: 0.
  - Any `TEST_FAIL` prefix: 0.
- Changed host-test source:
  - Size: 117,489 bytes.
  - SHA-256:
    `24B741CA8139A2A79EB161D316E17ADFEB96EC74CBC676FE02543309E92C9D18`.

Parked M01-025 compatibility evidence:

- Source commit:
  `4b42f11dd5a8cb30d91a58f88a2f0cbb17bdbe0e`.
- `src/OS/Boot/EfiEntry.cs`:
  - Size: 357,594 bytes.
  - SHA-256:
    `AFC0C3C61C25E95EA42254D64268E22E98253B726BFB8485DD1F3A6E8FD9567D`.
- Focused log:
  `artifacts/evidence/m01-021-regression-20260728-054645-326/m01-025-focused.log`
  - Size: 1,716 bytes.
  - SHA-256:
    `DD508179458CBC462C6175E3DDBB42D1204A02A9C801E9A632D89F9A3CBA5D8E`.
  - Exit code: 0.
  - Final line: `TEST_PASS m01.021.m01.025.focused`.

Fresh stage-one EFI evidence:

- EFI build log:
  `artifacts/evidence/m01-021-regression-20260728-054645-326/efi-stage1-build.log`
  - Size: 2,929 bytes.
  - SHA-256:
    `7C95165D90D035F35D539E9E4405EA17D534BF89B91F6E084E363DC2EE5D3841`.
- `artifacts/efi/BOOTX64.EFI`:
  - Size: 132,096 bytes.
  - SHA-256:
    `325458E450AFF3308F3DF63099E244BF5813BFAD4D2BBD7DB227091E25E6FE1C`.
- `BOOTX64.EFI.link.map`:
  - Size: 60,905 bytes.
  - SHA-256:
    `02D70F7145E597CEF4CE0ACFE439EB9A920BED67198E9F29FA107E4D017BDE81`.
- `BOOTX64.EFI.link.rsp`:
  - Size: 217 bytes.
  - SHA-256:
    `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F`.
- `BOOTX64.EFI.link.json`:
  - Size: 1,817 bytes.
  - SHA-256:
    `D8C429B10B3B351EEBB9AD1A07B350E2DFD7FA083A68E89D7AC143FBFD88FDAE`.
  - `sourceProjectSha256`:
    `F5FA32DEDD1B8E20A5DB35B7788E125816B020E4D6D6F297E15237165B6A93A7`.
  - `entrySourceSha256`:
    `F571204400E7DF00236DE680FD391FE13D808748F2C737D2BDCD68D96B286C15`.
- Current `src/OS/Boot/EfiEntry.cs`:
  - Size: 213,827 bytes.
  - SHA-256:
    `F571204400E7DF00236DE680FD391FE13D808748F2C737D2BDCD68D96B286C15`.
  - The actual source hash exactly matches the link manifest.
- Toolchain:
  - MSVC linker 14.51.36252.0, SHA-256
    `F233B8E337CEC96A69868A8CDE676808BFA81152493968D0B27B7CD0DAAC15BE`.
  - Windows SDK 10.0.28000.0 `kernel32.Lib`, SHA-256
    `8BA8DAB9ADEC752047D4F136852E8136D01836BFF39BBB56A20AC5F2D56415D8`.
  - ILCompiler package 10.0.8 / file 10.0.826.23019, SHA-256
    `DBCF62CC6C5240BC3441C9F6388CC670E2C113961BDBC8042E04080B5C296A95`.
  - NativeAOT runtime pack 10.0.8, runtime-list SHA-256
    `C1736FB134EB3EC5957DA475E59AADF816B910034F838790EDABC3C80D57F9E6`.

All named logs and generated EFI artifacts are local ignored evidence. They are
not committed; their paths, sizes, hashes, commands, and marker counts are
preserved here and in the reviewed Gitea evidence comment.

## Failures

- The parked M01-025 complete host run originally reported
  `M01_021_DIAGNOSTIC mutation-survived=allocation-removed`. This established
  that the token selector had changed an earlier unrelated probe region.
- Static independent review then found the masked method-level form before the
  full gate: earlier `TryValidateAllocation`, `RangesOverlap`, and
  `WriteSuccessLine` declarations could be selected before stage one. The
  contract and implementation were expanded generically rather than
  special-casing those names.
- The first synthetic token fixture used literal LF for its expected extracted
  branch while `StringBuilder.AppendLine` emitted the platform newline. The
  focused test correctly failed `selector-baseline-branch`; the fixture now
  uses `Environment.NewLine` while retaining mixed-line-ending coverage.
- An initial successful full harness reached `TEST_PASS host.tests` with empty
  stderr, but its background launcher had not persisted the native exit code.
  Its output was not used as the authoritative run. The same already-built
  harness was rerun through an ignored wrapper that recorded exit code zero;
  the second run is the evidence cited above.
- One polling command used PowerShell's reserved `$Host` variable and emitted a
  poll-only error. It did not affect the separately running harness or either
  redirected evidence log.
- During the tracker claim, a compact PowerShell command omitted whitespace
  after `throw`, so assignment and the comment succeeded while the claimed
  label did not. The state was detected immediately, repaired to exactly
  `agent:claimed`, and verified before implementation continued.
- No failed-run log, focused harness scaffold, or generated artifact is
  committed.

## Deferred work

This step repairs and revalidates host-test source-contract mutation selectors
only. It does not change the accepted M01-021 production adapter, invoke EFI
code in a guest, call firmware allocation services, execute either generated
byte plan, switch or inspect live RSP, make the guard page inaccessible, enforce
RW-to-RX, W^X, or NX, install or load page tables, change CR3, survive
`ExitBootServices`, or add real-QEMU/OVMF evidence.

The fresh stage-one EFI build proves current source closure and artifact shape
only. The QEMU dry run proves command construction only. No completion-gate
status changes. `done/step869.md` remains the authoritative historical M01-021
production source/build record, and the temporarily reopened dependency
descendants retain their previously accepted evidence without promotion or
reinterpretation.

M01-025 remains responsible for the serialized live paging integration and
must rerun its complete source-bound gates after consuming this repair.
