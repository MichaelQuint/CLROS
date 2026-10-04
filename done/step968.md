# Step968 - Original memory-map capture observation checkpoint

## Summary

Add the false-default M03-001 EFI observation profile for #58/#532. It wraps
one original four-argument Capture operation, retains the original firmware
callbacks and records query/allocation/recapture results without replacing the
firmware table. Before output it verifies the complete owned copy, both
16-byte guards and the unused 128 KiB payload tail. The ordinary snapshot,
handoff and BootSequence remain the continuation. Profile failure stops before
OS_BOOT_OK/OS_SMOKE_PASS.

This step records source, controlled host and artifact evidence. It does not
accept native instruction-level ABI behavior or a guest result. The common
full suite and fresh default/enabled QEMU captures are still required.

## Why

Step961's bounded memory-map regressions and the historical step964 common
host gate do not show the real firmware query, allocation, recapture and owned
copy observations. The new profile makes those observations available without
introducing a second capture algorithm, additional retries or EBS behavior.

The query and recapture have separate local size variables; the observer admits
exact reuse or safely disjoint slots instead of requiring compiler stack reuse.
The observer POD is 488 bytes. Its guarded storage is 131,104 bytes, with the
131,072-byte payload at offset16. Authentic firmware allocation still supplies
mapping/lifetime authority; arithmetic checks do not prove arbitrary pointers.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
- `src/OS.Kernel/Diagnostics/MemoryMapCaptureProbeAuthority.cs`
- `tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.cs`
- `tests/OS.HostTests/M01LoadedImageStackEfiEntryTests.cs`
- `tests/OS.HostTests/M01LiveAllocatorEfiEntryTests.cs`
- `tests/OS.HostTests/M01LivePagingEfiEntryTests.cs`
- `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs`
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
- `tests/OS.HostTests/M02PageFaultCr2ErrorCodeEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02DoubleFaultIstEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02UnexpectedVectorPanicEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ArithmeticConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04LayoutContractEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ManagedHeapPageRangeEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04RhpNewFastEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ArrayAllocationEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04StringAllocationEfiIntegrationTests.cs`
- `docs/boot.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- `.gitea/backlog/clros-issues.json`
- `done/step968.md`

The 29 existing compatibility fixtures retain their complete initializers and
old profile contracts. Updates preserve ordered Compile/source inventories,
reciprocal profile exclusions, exact project/source pins and finite selectors.
The explicit closure is 85 Compile items/98 raw inputs/37 probe flags, with34
XML false defaults. Default enables zero flags; the new profile enables one.
Capture, CopyPlan, BootHandoffRecord, BootSequence, central Probes, MinimalRuntime,
allocation helpers/ledgers, ImageBuilder and the generic QemuRunner are unchanged.

## Tests run

- Complete historical six-module managed run on its recorded earlier inputs:
  four M01 selector/publication modules, UefiMemoryMapTests and
  EFIBUFFERTOOSMALLDescriptorGrowthSlackOverflowTests. Native0 at
  2026-10-04 00:54:56.701787-00:55:01.512726 UTC;80 starts/81 passes/zero failures.
- Final complete M03MemoryMapCaptureEfiIntegrationTests retry7 on sourcev8:
  native0 at01:41:13.188403-01:46:19.996246 UTC;6 starts/7 passes/zero failures,
  one terminal host.tests, empty stderr and unchanged inputs/launch binaries.
  Includes actual original Capture under controlled callbacks, copy/guard/tail
  corruption, full-width values, distinct size slots,89 strict raw controls,
  ordinary lifecycle,108 reciprocal profile conflicts and both dry runs.
- Fresh default NativeAOT artifact: native0 at01:44:15.1858883-01:45:17.3297024 UTC.
- Fresh enabled NativeAOT artifact: native0 at01:45:17.3749251-01:46:19.9578858 UTC.
  Both actual archives include OS.dll/OS.obj/map/response/serializer/EFI and
  source/tool sidecars. Managed metadata has zero AssemblyRefs; profile/source
  closure and unmanaged forwarding signatures pass.115 actual child commands
  are preserved with their expected success/rejection outcomes.
- Final normal HostTests Release build: native0 at01:48:02.584888-01:48:08.489665
  UTC, zero warnings/errors, empty stderr and unchanged source inputs.
- Final parent integration uses sourcev9: only the new authority file
  CRLF-to-LF normalization and three resulting source-pin changes differ
  from v8. The complete root
  build/full-suite and guest checks have not yet run at this checkpoint.

## Evidence

Author evidence is in the m03-memory-map-observer worktree under
`artifacts/issue532/final-focused-handoff`, preserving all earlier source
freezes and failed attempts. Parent import records are under
`artifacts/orchestration-20261001/issue532-root-integration`.

| Record | SHA-256 |
| --- | --- |
| Final focused report | D5D0201F8C85CB9865CD3D3003CC51391AF4172D67C555B12EDD310F77BE58FE |
| Final package identities (13) | E5BE0346EBB3B93D5D8707F9334732E95EB92AB2F414152577CA1F732CC8ADE8 |
| Evidence identities (5,375) | FDC5D6FD87CBE505EF29442C9572D0EFACE1877E418C5E399E4FEA956092E1C8 |
| Focused v8 exact36 source manifest | 13233AAD0288CA569B39F885AA3C49DE9DBFD7055C744E071AF6B07C110B1CE0 |
| Focused v8 raw98 aggregate | FEDBC55C842F4A09469A6A6B61E729E25F537E7AA174B41BE778C9A05217901F |
| Final v9 exact36 manifest | 22026C76C9DEC75D25651D86B5333BFB55B9A16B69C432B79E297BA3CB1721B6 |
| Final v9 raw98 aggregate | CB27A2A1BC60B3C6F654B3727F9DB4976DC67B7AD7ED0C4BF1E5DF0011849F58 |
| Final v9 authority (132 bytes) | 54A8CE640B081D59568C0931314D5578114BDEA270603E9AD64EB3D9273C4310 |
| Canonical OS project | 8BDC64A0E1E3E85C4045F41F9C0F0ADC6E05BF1AD02FCDB248AB394B301DC06D |
| Retry7 result | E78083DB5F26CA9F75EEA68F54634DA0842F53223928C4D32FE6C545CA1CEAB2 |
| Retry7 stdout (942 bytes) | 92214ABA6D15125F9A5339286C39EAA41183E9739C7DB354BD2BEC8E50135F45 |
| Default EFI (55,808 bytes) | FDECE5DD4DBB61C63442099DCB1E7AEBD9EA4A3D6903C631A74C3F7134E9A703 |
| Enabled EFI (72,192 bytes) | 9C5601226E68F4458A360EDAB02AB74E336B8953DAE671F45A372ED47BB1C92A |

Parent independently rehashed all13 package and5,375 evidence rows and read
the completed command/results, whole retry7 stdout, artifact identities and
failure lineage. Independent final v8 source/focused review accepted those results and the
canonical documentation (report SHA-256
02DFE93D210F285F017B962DDB04B6B4999F82BB42124CEBE3172A9AC8EB5D21).
The additive v9 report is
5494EAC2DFCD4CB07984DF2C6D248B15D8A7B052ABE526E5B93153D6A73A8708;
its49 package entries rehash correctly. Parent inspected the exact four-file
delta, reproduced the ordered raw aggregate, and verified all36 staged Git
blobs equal the final reviewed worktree bytes. Final native inspection remains
separate. No source/focused result is promoted into native or guest acceptance.

Reservation used the actual `scripts/new-done-step.ps1 -Reserve` at
01:46:34.173455-01:46:34.426123 UTC, native0; output names done/step968.md.
No generated artifact or reference source is included in the change.

## Failures

Preserved complete observer attempts1-6 failed, respectively, on an assignment
substring falsely matching equality; an embedded raw-string comment being
parsed as a C# directive; missing explicit child entrypoint/actual console
input; trailing-space duplication in the enabled dry-run marker lists; a
stale canonical project pin; and inherited WindowsPowerShell module resolution.
The final retry passes after localized corrections. Previous partial passes
remain failed overall; no assertion or generic source-firewall rule was relaxed.

Independent source review also caught an out-of-range query descriptor-version
parser case, culture-sensitive drain comparison and one stale Compile count.
Their UInt32 bounds, ordinal checks and exact inventory assertions were fixed.
The original distinct-size-slot finding was corrected before the accepted core
checkpoint. Canonical-pin proof's first module-resolution failure is preserved.
Retry7's recorded null inherited-module-path field is a case-sensitive lookup,
not proof that the inherited Windows path was absent; only its configured path
and actual successful outputs are claimed.

Git staging then exposed six CRLF terminators in the new authority file that
would normalize to LF in the commit. The additive v9 correction changes only
those line endings (138 to132 bytes) and three exact aggregate literals.
No compiler or runtime behavior changes; v8 focused/publication evidence is
retained without a rerun. The final common suite and fresh guests must use v9.

## Deferred work and evidence boundary

Run the common clean-head full suite, including every affected legacy artifact
initializer; independently inspect actual forwarding/capture/copy/readback
machine code and ABI; capture fresh default/enabled QEMU guests and review
whole raw/lifecycle/source/native/disk evidence. #58/#532 stay open until those
checks and the reviewed merge are accepted. MapKey is opaque and observed, not
proved fresh after reporting. No EBS, post-firmware ownership, M03 completion,
GC, hosted .NET/Kestrel, cloud or production claim follows from this step.
