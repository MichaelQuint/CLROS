# Step 956 - Unexpected-vector adapter and controlled runner checkpoint

## Summary

The reviewed C2/D7 leaf, default-off EFI adapter and opt-in expected-panic
runner are integrated under #56/#526. This milestone records source, complete
focused-module and compiled-artifact evidence. It does not close either issue,
approve a real guest, or replace the required fresh whole-host run.

## Why

The earlier4KiB transition stack demonstrably overflowed in the provisional
NativeAOT study (step953). The accepted D7 repair keeps D0-D4 intact and supplies
an8KiB transition/callback region. A16-byte bottom sentinel leaves8176 usable
bytes, but actual complete call/access/ABI analysis must still prove that bound.
The adapter retains original resource/request/backend authority; a dedicated
strict runner mode distinguishes the intended terminal panic from other failures.

## Files changed

The exact36-path author inventory is
`artifacts/issue526/final-source-readiness/owned36-sha256.json` in
`C:/Users/Roger/.codex/worktrees/m02-016-panic-efi/CLROS`.
Its SHA-256 is
`311A319BB28169A4D784E0DD893A0818385F6418106DB5D83E86E6090F6EAFC6`.

- `src/OS.Kernel/Diagnostics/UnexpectedVectorPanicProbeAuthority.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/M02DoubleFaultIstEfiIntegrationTests.cs`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02NormalizedInterruptFramePageEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedIdtGatePopulationEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Ud2VectorStateEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ArithmeticConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04LayoutContractEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ManagedHeapPageRangeEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PageFaultCr2ErrorCodeEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04RhpNewFastEfiIntegrationTests.cs`
- `tests/OS.HostTests/M01LoadedImageStackEfiEntryTests.cs`
- `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs`
- `tests/OS.HostTests/M02UnexpectedVectorPanicEfiIntegrationTests.cs`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/M04ArrayAllocationEfiIntegrationTests.cs`
- `src/OS.Kernel/Diagnostics/UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.cs`
- `tests/OS.HostTests/UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassificationTests.cs`

Parent changes are `README.md`, `docs/diagnostics.md`, `docs/boot-order.md`,
`docs/efi-build.md`, `docs/qemu.md`, `docs/repo-audit.md`,
`docs/completion-gates.md`, this step and the M02-016 catalog row's current
evidence, boundary and two implementation notes. The original issue ownership,
labels, dependencies and344 other rows are preserved. Step955 and its four
accepted String host sources remain unchanged.

The import checked every existing destination against release base
`46e2a74fb88c37410d96b94a2caff4a69b624777` before copying36 reviewed byte-exact
files into clean parent head`33a6aceabb2182f82aa3a262008a726c66622d44`.
Import record`issue526-parent-source-review/accepted-import.json` has SHA-256
`EB03EDFCB3CAB9C173A61ED84213F6C13BDEC5553D5A73E32AC77BD1FEDCCDB1`.

## Implemented boundary

- C2/D7 retains1028 used stream bytes,184 capture bytes, capture offset28488
  and callback-entry RSP offset28440. The232-byte entry includes capture,
  ABI reservation and return; it is not the full reporting depth.
- Original image, callback, request, C/D leases and checked backend remain
  outside mutable scratch. Exact callback arguments precede reads.
- Preparation and complete pristine readback precede sentinel placement,
  final publication and one terminal INT0x20/vector32/IST0 entry. Reverse
  pre-entry cleanup uses admitted original authority; ambiguous resources
  are retained. There is no firmware cleanup/reset/return after entry.
- Two TEMT handoffs remain bounded at65536 polls each; per-byte THRE at128.
- Exactly32 evaluated flags cover the new false-default profile plus its
  eight prerequisites; all23 alternatives conflict. There are82 Compile
  items and95 raw source inputs. No source-scanner exception was introduced.
- Explicit runner`--expect-unexpected-vector-panic` requires the exact76
  owned CRLF records, fixed optional304-byte OVMF preamble,650-byte metadata,
  existing vector32 fault, exact EOF, measured750ms quiet and owned termination.
  Native19/expected-panic is distinct from ordinary smoke success. The wrapper
  accepts it only with strict summary/raw/native-exit binding and empty stderr.
- Compiled Run uses SSE before its Query/Mode call. A supported inherited
  firmware entry ABI is a prerequisite; later mode validation does not
  establish safety for earlier instructions or hostile unsupported-SSE entry.
  Fresh mode admission still governs the subsequent owned transition/callback.

## Tests run

Author worktree commands (full argument arrays and child invocations are
preserved in the named command/result records):

```powershell
dotnet build artifacts/issue526/runner-tests-authoring/focused/Focused.csproj -c Release --nologo -m:1 -nodeReuse:false
dotnet run --project artifacts/issue526/runner-tests-authoring/focused/Focused.csproj -c Release --no-build
dotnet build artifacts/issue526/complete-module-preparation/PanicFocused.csproj -c Release --nologo -m:1 -nodeReuse:false
dotnet artifacts/issue526/complete-module-preparation/bin/Release/net10.0/PanicFocused.dll
dotnet build artifacts/issue526/loaded-image-complete-preparation/LoadedFocused.csproj -c Release --nologo -m:1 -nodeReuse:false
dotnet artifacts/issue526/loaded-image-complete-preparation/bin/Release/net10.0/LoadedFocused.dll
dotnet build tests/OS.HostTests/OS.HostTests.csproj -c Release --nologo -m:1 -nodeReuse:false
```

- Complete runner module: native0,165 starts/166 passes/0 failures, one final
  host terminal, empty outer stderr,08:16:47.967358-08:20:03.122999 UTC.
  All158 original groups and byte-exact original source remain, with seven
  appended groups and41 retained controlled CLI cases.
- Combined attempt6:08:33:06.323921-08:38:11.074137 UTC, native DWORD3762504530
  (signed-532462766),41 starts/39 passes/2 failures, no final host terminal.
  Before the loaded-image selector failure, complete new EFI7, D7 host11 and
  publication10 groups each emitted every expected start/pass once.
  These28 completed pairs are bounded substream evidence; the batch failed.
- Actual D7 no-stdlib subtest:12 sources, zero AssemblyRefs. Controlled
  cleanup projection warnings (12 CS0649) are preserved and are not normal
  production zero-warning claims.
- Actual default/enabled NativeAOT child commands both returned0 with empty
  stderr; OS.dll has zero AssemblyRefs, flags32 with0/9 true, and exact82/95
  source closure. Default excludes the adapter; enabled IL checks original
  authority and publication/callback order. No guest code ran.
- Complete repaired loaded-image retry:08:43:00.013682-08:43:01.293905 UTC,
  native0,13 starts/14 passes/0 failures, one final terminal and empty stderr.
  Its focused build and final normal HostTests Release compilation returned0
  with canonical zero-warning/error summaries. Final normal compilation
  completed08:43:06.311292 UTC.
- Parent rehashed142 checkpoint records,131 source snapshot inputs,4 final
  package records and6319 focused evidence records; independent review was
  read back and its32 review/50 input records rehashed. Parent complete catalog module passed17 starts/18 passes/0 failures,
  native0, empty stderr and exact names at08:53:54.062245-08:54:20.732683 UTC;
  all21 inputs were unchanged. Naming/source-firewall checks both passed,
  native0 with empty stderr. The source diff whitespace check passed.

The unchanged actual complete initializers are used; no selected-method
substitute supplies a complete-module result. Controlled processes, generated
data-only projections, metadata checks and actual native artifacts retain
their distinct boundaries.

## Evidence

Author archive root:`artifacts/issue526/` in the author worktree.
Parent/reviewer root:`artifacts/orchestration-20261001/` in this checkout.

| Record | SHA-256 |
| --- | --- |
| Final source readiness report | 784DDA880F311E82626293334B1D4345761F203583FBE11B68F31DB5D8A57D75 |
| Final source package | 6DD8A05173DFC554346605B505274294A884208D94595217CC65762F1A32B6C3 |
| Raw95 source inventory | D6683807867F2F721255C42580AA06A060824EC6FBC2CD977C820B0F25EE8760 |
| Complete focused6319-file inventory | F96D5F70D0962501CAF6C2DAC94C63B04835450918E08AFD84689D8EDF2537C2 |
| Focused composite report | 35B4F4E98373402C435890EA2CB83BA489BF84F67643746DE49213E5496E21A8 |
| Independent final source/focused review | BBF469F108A254AB1DD7C4E4E7A9B51474F3BD9EA9C908AA1326D4746C25AD51 |
| Independent runner review | 76C6239DB31AF76102E01773505B5D821B5F486C03D02ED5EEF6EA6A5FF97DF2 |
| Runner complete result | 5E63E774A70F09BA2077827A4304DEE78A48E8B16B40DCB87E8E31224D3C1AD9 |
| Runner raw stdout | 8E26DBABC308DBCF654DCDA516322E97433E65F297465B61787B2C4206628337 |
| Failed combined attempt6 result | EAEBA45F2C88DCC9C5E6943FC6507E30197316936E4D3F4652DD28D6E687C709 |
| Loaded-image retry result | DBEEA7CBCA7D6C439DB8BA48C803E8DE46F5723E423B4C4634199C4B34E4EA36 |
| Loaded-image retry raw stdout | 97FDDEF6108713A6DE6D88DF3EE2B7F216194F3A348E07448287175082777CBD |
| Parent complete catalog result | 185DD3525EFDDDC3AC57F567909DE1D528A685F8E0E24BD9AEC1021505EA4F15 |
| Default native handoff inventory | 81AC14A0C6571B449B40C1ADE19EB559058D2754C02226A93E628A946B7CAFC9 |
| Enabled native handoff inventory | 15C9D5EFA4EC06F4F0BBB8D51098A3461959D2F9C0CE7F8FF690E0F9D66AAA99 |
| Default EFI | 387E1BA7F8CC22E9C869C795353D90F7B75A1FDFBCB789A0353D04C7050D4352 |
| Enabled EFI | D80E69024B6A71BAF5FC94529C7CC8A57FF06034F4A018E6C193F29E81525AAA |

The successful native child artifacts belong to failed-overall attempt6.
The later loaded-image-only repair does not change any of the95 production
inputs. Their fresh whole call/access/stack and ABI review is still separate.

## Failures

Pre-execution review found cleanup-authority issues, whitespace-stderr
classification and omitted deterministic arithmetic-flag checks; their original
checkpoints and narrow repairs remain preserved. The generated adapter test
also confused permitted pre-CLI IF with forbidden TF and was corrected.

Actual combined attempts1-6 remain archived: mixed numeric array inference in
the generated driver; nested PowerShell marker arrays; a DryRun assertion for
an internal nonprinted argument; two opposing-profile prerequisite setup
errors; and the old loaded-image selector failing to recognize the new known
false symbol. Only the marker-array construction required a later production
script repair. The final loaded-image retry preserves exact ordinal and
unknown-symbol behavior.

Source-inventory packaging/schema, earlier line-ending packaging and
review-only inspection failures remain separate from actual product/test
failures. A composite-evidence packaging attempt used an older fixture pin;
the corrected package binds the real attempt6 pre-run snapshot and recomputes
the unchanged marker names. No failed overall command is relabeled successful.

## Deferred work

- Fresh clean same-head solution/HostTests build and complete repository run.
- Independent full current native call/access/stack, original port target,
  ABI/SIMD and sentinel-separation acceptance before any guest.
- Fresh default, unexpected-panic, UD2, PF, DF, fixed, mixed-array,
  array-beforebind and array-exhaustion guests with independent raw/lifecycle,
  native relocation and image accounting. Allocation regressions cover the
  imported shared String allocator change; no String guest is implemented.
- PR527 review/merge and #56/#526 issue evidence and closure after those gates.
- Separate String native integration, memory-map work and the remaining backlog.

## Evidence boundary

This checkpoint is source/focused/compiled-artifact evidence only. It proves
neither a genuine guest panic nor a passed full suite nor full native stack
safety. No arbitrary interrupt/IRQ/NMI recovery, enforced guard, post-firmware
mapping lifetime, real ExitBootServices, GC, hosted .NET/Kestrel, cloud or
production readiness is established. Gates3/4 remain Fail.
