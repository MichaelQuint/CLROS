# Step 963 - Bounded EFI String source and artifact integration

## Summary

Integrate the reviewed #528 String allocation source and complete focused
evidence for M04-009/#78. The original retained-page authority and cursor now
serve the default-off `RhNewString(MethodTable*, nint)` export and genuine
String constructor/readback paths. The positive fixture uses200 bytes for
seven allocations, including five strings. Before-bind, exhaustion and a
full-width invalid length fail closed. General runtime gates remain incomplete.

## Why

Step955 supplied bounded host String storage; step959 accepted the shared core
with existing fixed/array guests. Returning compiled String routes and actual
guest evidence require their own integration. This step records the source and
focused compiler boundary before the combined full suite and guest matrix.

## Files changed

- `.gitea/backlog/clros-issues.json`
- `docs/completion-gates.md`
- `docs/efi-build.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/qemu.md`
- `docs/repo-audit.md`
- `done/step963.md`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `scripts/test-efi-artifact.ps1`
- `src/OS.Kernel/Diagnostics/StringAllocationProbeAuthority.cs`
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `src/OS/OS.csproj`
- `src/OS/Runtime/MinimalRuntime.cs`
- `src/OS/Runtime/RhpNewFastAllocation.cs`
- `std/no-runtime/shared/DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.cs`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02DoubleFaultIstEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02NormalizedInterruptFramePageEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedIdtGatePopulationEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PageFaultCr2ErrorCodeEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M02Ud2VectorStateEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02UnexpectedVectorPanicEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ArithmeticConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ArrayAllocationEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04LayoutContractEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04ManagedHeapPageRangeEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04RhpNewFastEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04StringAllocationEfiIntegrationTests.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `tests/OS.HostTests/NoStdLibShimBoundaryTests.cs`

The release owns40 code/test/script paths;38 change and two remain byte-identical:
`src/OS/Boot/EfiEntry.cs` and `M01LoadedImageStackEfiEntryTests.cs`. Four original
host dependencies are frozen. The source profile has84 ordered Compile items,
97 raw publication inputs and36 switches. The smoke script is bound separately
from raw97. No first-party native source or generated artifact is imported.

## Tests run

- Complete eight-module managed harness: actual build/run native0, empty
  stderr,63 `TEST_START`,64 `TEST_PASS`, zero `TEST_FAIL`, final `host.tests`.
  This is attempt2,19:34:19.959427–19:34:27.242586 UTC on2026-10-03.
- Complete six-group String EFI harness: actual build/run native0, empty
  stderr,6 starts/7 passes/zero failures and final `host.tests`. This is
  attempt4,19:49:38.901431–19:58:03.580308 UTC, a separate successful run.
- That String run includes actual shared POD-core execution, strict raw
  transcript controls, profile/firewall controls and five NativeAOT publications
  (default/positive/before-bind/exhaustion/invalid-length). All five have zero
  OS AssemblyRefs and exact36-property vectors with0/10/11/11/11 true flags.
- Independent review verifies146 archived String input snapshots,23 unchanged
  run-binary identities and93 child commands (76 intended profile rejections
  and17 successes). Actual POD checks bind observation256/storage6672.
  Independent PE/COFF checks bind all24 String/Object prefix bytes and their
  related-pointer relocations on the four enabled artifacts; the invalid-length
  native route retains4294967297 without narrowing.
- Root import rechecks all40 author source identities and exact clean baseline
  bytes before copying the38 changes. Root catalog/source checks, the fresh
  committed-head build and complete suite have not run at this checkpoint.

The host harness commands and all child commands, native exits, raw logs,
binary records and source snapshots are retained in the evidence locations
below. No privileged EFI image or QEMU guest was executed for this step.

## Evidence

Author worktree:
`C:/Users/Roger/.codex/worktrees/m04-009-string-efi/CLROS/artifacts/issue528/`.
Final report: `final-focused-handoff/report.md`, SHA256
`D26155BCD83462B414A3ADE97CD6919C0E27992F6B062B61CD6B04EF5448CE4B`.
Final owned40 manifest:
`8CE9132EE7E5259C0EB5900520CBC4A970E8E5A60CEB307801B8C29AF4A59876`.
Raw97 aggregate:
`B66FE68E19054DFC56AD00458E1E54A307734EF59E718123DCEF43FDD61A4C81`.

Managed stage result:
`84C4A96A23658E61AB51D2659BE419CAD3884A5B1FBD987F67963CAD16DDBD09`;
stdout `D341B18040A531F63683AE76236AF17FECCBBD6C3233EE568EA17C3A141CD4B6`.
String attempt4 result:
`6858210A10D4634040290F55B59F8BB6CB1957240A2E4E5013AED1A224F3C28E`;
stdout `A679265E458595E7FFEAFC2F7AE0AD0A7040F1D3FF7698993D7DF79B284F9456`.
Independent focused acceptance:
`artifacts/orchestration-20261001/issue528-complete-author-independent-review/review.md`,
SHA256 `DBF86910BCE3B2E415A0388BF3236AF15150CFE101AAAF6AE305E2F0919E122C`.
Root import:
`artifacts/orchestration-20261001/issue528-integration/imported40.json`, SHA256
`570BD6465C99A823675AC7F17BB4DEF37782C208363AED205AAD544DC1BCD403`.

## Failures

Preserve all failed attempts. Attempt1's managed run expected five EFI-only
ledger rows instead of six; only the assertion literal changed. Attempt2's
String run exposed Windows PowerShell signed parsing of three high-bit hex
constants; exact positive UInt64 decimal equivalents preserve every comparison.
Attempt3's native test selected both `String__Ctor` and its unwind record by
suffix; exact ordinal selection preserves `Single` and all native assertions.
The corrected native method separately passed over a byte-identical copy of
the saved positive artifact before the complete attempt4 retry.

Source-preparation/readback errors, the earlier transcript diagnostic literal
correction and reviewer packaging failures remain in the author/review history.
They are not represented as passing product runs. No single combined passing
managed/String attempt is claimed.

## Deferred work

Run one fresh clean committed-head solution build and complete original host
suite, including all fixed/array artifact branches and legacy initializers.
Then capture default/fixed/mixed-array/String positive and three String
negative guests and independently join native, raw, lifecycle and disk
evidence. Keep PR531 draft and #78/#528 open until reviewed acceptance and merge.
#58's separate firmware map observation supplement follows the shared-lane
handoff; #529 still requires its narrow live milestone closure after merge.

## Evidence boundary

This is source, host, metadata and compiled-artifact evidence. It proves no
String guest execution, collecting GC, interning, general String/BCL surface,
barriers, refill, real ExitBootServices, hosted .NET/Kestrel, cloud readiness or
production readiness. Gates3/4 remain **Fail**. Filesystem, networking, provider
and later runtime milestones are not completed by this integration.
