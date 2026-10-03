# Step 961: M03-001 memory-map host regressions

## What changed and why

Issue #58 now has ten bounded host regression groups against the existing
`UefiMemoryMapCapture` and `UefiMemoryMapCopyPlan` implementations. The proposed
duplicate production wrapper was unnecessary. The original helpers and EFI
path remain unchanged. Tests cover growth within literal default slack,
40/48/64-byte strides including a changed final stride, configurable slack,
exhausted slack without another retry, firmware errors/null allocation,
preflight and metadata precedence, 4096/4097 descriptors, exact and one-short
destination capacities, full-width arithmetic and warmed zero managed
allocation. Independently authored descriptor bytes and complete source and
destination guards/tails are checked. Unmanaged callbacks bound their writes
and never throw across the native boundary.

## Files changed

- `tests/OS.HostTests/EFIBUFFERTOOSMALLDescriptorGrowthSlackOverflowTests.cs`
- `.gitea/backlog/clros-issues.json`: only M03-001 currentEvidence.
- `docs/boot.md` and `docs/efi-build.md`: behavior and proof boundary.
- This reserved `done/step961.md`.

Author base: d4bd8411b2bad1dbdcc4e7687c15d8675c2abfb7 in the separate
m03-001-memory-map worktree. Parent reviewed all 465 source lines and the real
Capture/CopyPlan, then imported the exact 22,567-byte test into the clean root
at fa889cda09c17d1d52228fa031d7591e5ba8f7eb after PR530's documentation merge.
Test SHA256: E6D32950852F582ADE2F06F42E1D72C44845B93493E383C541BBD8CB8638E0F2.
Prior step files remain immutable.

## Tests run and evidence

Actual author commands in the isolated worktree:

```powershell
dotnet build artifacts/issue58/focused/Focused.csproj -c Release --nologo -m:1
dotnet artifacts/issue58/focused/bin/Release/net10.0/Focused.dll
dotnet build artifacts/issue58/nostdlib/Compile.csproj -c Release --nologo -m:1
dotnet build tests/OS.HostTests/OS.HostTests.csproj -c Release --nologo -m:1 -p:UseSharedCompilation=false
```

The complete new initializer (ten groups and outer m03.001 pair) plus all
18 unchanged UefiMemoryMapTests groups returned native exit 0: 29 starts,
30 passes, zero failures, one final `TEST_PASS host.tests`, exact names/order
and empty stderr. This is a focused two-module run, not the full suite.
Actual batch: 2026-10-03T16:05:26.101647Z through 16:05:51.857094Z.
All three builds returned 0 with zero warnings/errors and empty stderr.
The actual five-file no-stdlib closure includes the current MinimalRuntime,
MethodImplAttribute, BootHandoffRecord, Capture and CopyPlan; metadata-only
inspection found zero AssemblyRefs. No replacement shim or target execution
was used. All 19 batch inputs were unchanged.

Author evidence under `artifacts/issue58/` in that worktree:

- `attempt2/result.json`: 7BF1A4B84DD1162D938640F46AF54EC29B6B091CAB95D077313FCD40188CDEBA.
- Focused stdout: 9B11BA36CE5164549037071E925AD96CEDEE8F30F331E73C371E56E5F106C31F.
- `final/evidence-identities.json`: 1C64F02F7847C971A98945666BB8278ED945418F6BC4C6A7D0946E90266FEF10 (65 files).
- Focused DLL: 9619E3955C87AE9E20200AE128E0A5EDE84BB5E0C6FCB5F953DB11DEAB14C9F1.
- No-stdlib DLL: 1604E4B898FAD42495DBEDC0B342934E36F3F812E860B4948FBDA569295EC641.

Parent read the raw logs and source-derived registrations and rehashed the
65-file manifest, four outputs, source and 19 inputs. Parent review is
`artifacts/orchestration-20261001/issue58-parent-review/result.json`, SHA256
BA4D4ADC1B609303547C7BE4D32431FACCF3ECBE78AB19A92A3EF7E189141FF0.
The exact import is recorded in `issue58-integration/import.json`.
Root integration build used the same normal HostTests command above from
C:\Projects\CLROS, returning native 0 with zero warnings/errors and empty
stderr at 2026-10-03T16:56:14.895159+00:00 through 2026-10-03T16:56:43.712387+00:00.
All 864 captured source/project inputs remained unchanged. Build result SHA256
C385F85AD9F6F0937DEBE75138C0856F712944508B204F45860675AF2B689128; stdout
A3BB6B4E851F44361F3A56E3792FCA40912E3D3A09A60F5CA62D54493A0F9EE8.

The complete unchanged catalog initializer ran through the existing
`dotnet artifacts/orchestration-20261001/issue523-manifest-focused/bin/Release/net10.0/ManifestFocused.dll`:
17 starts/18 passes/zero failures, exact original names and final
`TEST_PASS manifest.focused`, native 0, empty stderr. Actual interval:
2026-10-03T16:56:12.985408+00:00 through 2026-10-03T16:56:53.406071+00:00.
All 16 inputs remained stable during the run. Result SHA256
8142AC5AC6615EAF7B7874ED07BBB222E8DC9BAF8657577E8EF6902BEE871394; stdout
A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9. Raw commands/logs/snapshots are under
`artifacts/orchestration-20261001/issue58-integration/`. After these runs, only step961 changed, to add results and normalize whitespace.
Diff/whitespace checks pass.
Final review is pending; no new common-head full-suite or native/guest result
is claimed. This is an intermediate host checkpoint for the next merge wave.

## Failures

Author attempt1 failed in the evidence recorder before any compiler launched:
a string was passed to a Path-only helper. The original recorder and failure
remain; attempt2 fixes that conversion and records the actual batch. Parent's
first marker selector assumed RunTest instead of Run and missed multiline
calls; source inspection corrected it without changing tests or results. The
earlier four-file planning shorthand omitted MethodImplAttribute; the actual
closure is explicitly five files. A parent documentation helper stopped at an
existence assertion because reservation had already created this step's empty
template; the three completed catalog/doc edits were retained and only the
reserved template was then filled. No product/compiler/test failure was hidden.

## Deferred work and evidence boundary

Issue #58 stays open/claimed. The separately owned native observation and
fresh QEMU/OVMF proof remain required after #528 releases the shared EFI lane.
The parent common-head gate also remains pending. No CLROS_M03_001_READY guest
marker is emitted by this host fixture and no completion gate is promoted.

Capture performs one sizing query, one allocation and one recapture. A second
BUFFER_TOO_SMALL fails; no repeated reallocation or FreePool policy was added.
MapKey is an opaque full-width copied value. Stale-key retry, final-map-to-exit
discipline and firmware denial after exit remain #61-65. Scalar size checks do
not prove arbitrary pointer mapping, lifetime or complete address spans; no
fabricated wrapping pointer was dereferenced. Controlled callbacks establish
host behavior only, not naturally occurring firmware growth, real EBS, hosted
.NET/Kestrel, cloud or production readiness.
