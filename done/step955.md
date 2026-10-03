# Step 955 - Shared String storage host checkpoint

## Summary

The new `GuestTestStringAllocationCharacterStorage` host entry joins the
existing retained-page fixed/array allocator. It accepts a signed64 length,
admits the pinned String layout, zeroes character storage and its terminator,
and commits through the original shared cursor. This is a bounded host
milestone for issue #78, not issue completion.

## Why

String storage needs the same original page ownership and allocation sequence
as fixed objects and arrays. A thin wrapper and one private core avoid a second
allocator. Wide length validation prevents truncation before the existing
layout evaluator applies metadata/shape/size checks. The earlier pinned terminal
compiler checkpoint established `RhNewString(MethodTable*,nint)`, constructor
remapping and String field offsets, but never returned an allocation.

## Files changed

- `src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs`: finite shape selection,
  signed64 internal count, additive status19 and String admission.
- `src/OS.Kernel/Runtime/GuestTestStringAllocationCharacterStorage.cs`: thin
  public signed64 entry.
- `tests/OS.HostTests/GuestTestRhpNewFastTests.cs`: shared cursor/isolation,
  additive status and production body checks.
- `tests/OS.HostTests/GuestTestStringAllocationCharacterStorageTests.cs`:
  complete11-pair module with independent byte oracles and a no-stdlib build.
- `README.md`, `docs/repo-audit.md`, `docs/nativeaot-nostdlib-limits.md`,
  `docs/completion-gates.md`: host status and remaining native work.
- `.gitea/backlog/clros-issues.json`: only M04-009 labels, ownedPaths,
  collisionLane, currentEvidence and one exact owned-path implementation note
  against the merged catalog; other notes, dependencies and344 rows are retained.
- This reserved step records the milestone.

Release base: PR524 merge `46e2a74fb88c37410d96b94a2caff4a69b624777`.
Author worktree: `C:/Users/Roger/.codex/worktrees/m04-009-strings-host/CLROS`.
Only its four owned source paths changed. Parent imported those exact files
into the integration and #526 compatibility checkouts after checking original
destination bytes against the merge base.

## Tests run

Actual author commands from that worktree:

```powershell
dotnet build artifacts/issue78/focused/Focused.csproj --nologo -m:1 -c Release -nodeReuse:false
dotnet artifacts/issue78/focused/bin/Release/net10.0/Focused.dll
dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo -m:1 -c Release -nodeReuse:false -p:UseSharedCompilation=false
git diff --check
```

- Focused Release build: native0, zero warnings/errors, empty stderr,
  2026-10-03 07:04:32-07:04:39 UTC.
- Complete fixed/array/String modules: native0,31 `TEST_START`,32 `TEST_PASS`,
  zero `TEST_FAIL`, one final `TEST_PASS host.tests`, empty stderr,
  07:04:39.440817-07:04:43.608290 UTC. The harness links the complete actual
  ModuleInitializers; Program prints only the terminal marker.
- Actual10/11/11-source fixed/array/String no-stdlib builds: native0, zero
  AssemblyRefs, real PEReader production-body checks, unchanged merged
  MinimalRuntime. Fixed/array closures do not depend on the new wrapper.
- Normal HostTests Release compilation: native0, zero warnings/errors,
  empty stderr, 07:05:22.279043-07:05:37.441412 UTC.
- All four source files pass UTF-8/no-BOM, final-newline and whitespace checks.
- Parent's corrected complete backlog module passed17 starts/18 passes with
  zero failures, exact marker names, one final `TEST_PASS manifest.focused`,
  native0 and empty stderr at07:32:41.978258-07:33:04.540178 UTC. All21 inputs
  were unchanged. Naming/source-firewall checks and `git diff --check` passed.

Coverage includes literal byte/size oracles, UTF-16 embedded NUL/non-ASCII/
surrogates, terminators/padding, forward/reverse mixed cursor224/count7,
exact2037-character4096-byte fill versus2038-character4104-byte rejection,
signed64 truncation and metadata precedence, full declared-span aliases,
original authority/controller changes, copied owner, unrelated legal free and
retained-page-free rejection. Rejections preserve all65536 fixture bytes and
return zero success fields. Warmed real mixed calls allocate zero CLR bytes.
Existing array tests are unchanged and ran completely.

## Evidence

Frozen author package: `artifacts/issue78/final/` in the String worktree.

| Record | SHA-256 |
| --- | --- |
| report.md | F100DE58EC636AB3E40608D334218C36FAA399D6696CD8E63199A6767B4CD703 |
| owned4-sha256.json | 050A5899B89219EA3FC74288E315CDBC95E133DEDDD4EA4E8B79D577B84767FB |
| validation.json | 7264A02557E1346DF7C3D0F81764D0F6839B992E7EE8FB15182626373F17DF55 |
| evidence-sha256.json (101 records) | E890AFB0A0E05B9D7A1E31BB1E4FD8766D8A991AEEB84B69BD813001AB03B8C8 |
| Focused raw stdout | 54A3B2F0F8215256D6BAA108915173C2877E4031EA2756BF8A55727EEA6346F9 |
| Fixed no-stdlib DLL | 76994985F826E32D19B6E5A25A98CDCA652D5B86D0D34BD2428A09E4E2FB45F3 |
| Array no-stdlib DLL | C459EF47AC33F4195A39DEE68CF0155A7BAB3AC66D660C7F425F330113ADE541 |
| String no-stdlib DLL | 39CFC1A995741C7C836DB6D21056CEEF0D07A671D00F27253593E3EE7B71A03F |

Source hashes, in the order of the first four changed-file bullets:

- DA93A400753BB57A8D17EE8A07DBD6B0B0C8949D1C70C23F44C22A902D7CD14A
- 0676AB5B69838BD12575A9D3C2059F8C39872CFA67C5B9FD0C37227568822ABA
- 1D98C426348A5D522842A4BF312619F393F8AA289A669CF1ABE31C8A9953F885
- 27F774D0F1123A7BE7B14CBD4C8D41E22B8FB0B29B16D113745741894AAC88B8

Independent review under `artifacts/orchestration-20261001/`:
`issue78-final-host-review/review.md`, SHA-256
`6FA1112E5BCB2F8483970B3A302F87CF259CE0C62811B82B24A834294B52F1BC`.
Parent read source/test diffs, report and raw evidence and rehashed101 author
records plus10 review/117 input identities before integration.
`issue78-parent-readback/accepted-import.json` records both exact imports,
SHA-256 `DEDE31E1EE0E2BF040D6793B46407DA64706225D60B9ABE0CE34D95A19A93AAF`.
Issue #78 comment14343 was posted and read back; body, open/claimed state and
content_version2 were preserved. The subsequent ownership-note correction
changed exactly one live body sentence and was read back at content_version3;
title and open/claimed state were preserved.

Corrected catalog check:
`issue78-milestone-checks/attempt2/result.json`, SHA-256
`30F5EAE4D3796884B366EF4BE8C8B48CCEEE84CD630C573D36C46BA80D1E37BA`;
raw stdout SHA-256
`A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9`.
Catalog SHA-256
`5585B3792F3473EF26A9E9ED36387D36CC78D1656D30D4BED2EFDA6B1ABB4DA5`.
Issue comment14346 records the failed attempt, correction and successful retry.
Only this step's check/evidence paragraphs were completed after that run.

## Failures

No author build or test failed. Parent's first complete catalog-module run
failed its shape assertion: the expanded ownedPaths list no longer matched the
original exact ownership sentence in implementationNotes. It completed with
17 starts/16 passes/1 failure, native exit3762504530, and unchanged input bytes.
Its raw output/result remain in
`artifacts/orchestration-20261001/issue78-milestone-checks/attempt1/`;
result SHA-256
`C4396B88034FA5ADF038AC43A5E6E238E6E0C6057F9ED6B0BAB0C78E6B65B0CB`.
The parent corrected only that note to the current six ownedPaths, both in the
catalog and live #78 body. The original version remains in Git/API snapshots;
no test was weakened or implementation scope expanded by the correction.

The independent review first assumed the wrong
snapshot path; that read-only failed attempt is preserved beside its corrected
successful readback. It did not execute product code or change source.

## Deferred work

The separately claimed #526 lane must reconcile shared-source admission and
same-image fixed/array compatibility. A combined full host run and native
regression gate are not claimed here. Native String exports, genuine returning
compiled constructors, guest character storage, interning, refill/reclamation,
GC/roots/barriers and post-firmware lifetime remain pending. #79 stays blocked.

## Evidence boundary

Access104/state80/result88, thirteen original authority arguments, one-shot
binder, fresh retained validation and sole commit are unchanged. String layout
is base22/component2, reference/MT B+8, length B+16, characters B+20, terminator
B+20+2*n and size align8(22+2*n). Scalar metadata and numeric buffer addresses
are not authenticated mapped metadata or CLR references. The host READY record
preserves guestIntegrated=false, compiledString=false, runtimeExport=false,
interning=false, gc=false and gate4=Fail. This step does not close #78 or change
Gate3/4, EBS, Kestrel, cloud or production readiness.
