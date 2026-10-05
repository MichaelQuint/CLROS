# Step 987 - Repair legacy raw-project pins for the TypeManager closure

## What changed and why

The first full common run at 5bc37ea failed the original M02.008 exact-source
closure case. Four older integration fixtures still expected the pre-TypeManager
raw OS.csproj hash. Each reads complete raw bytes, so its expectation must
describe the current explicit source closure. Only these four hash literals
changed, from e55c4225fd2725f6e338e0f60d5113d59b4db964552ca0c157be1be1e2ae7b99
to bc74c58deea0556a1e752c3f1b6b58e9a5f44fcc7c5901b1e5959e97d6bca953.

The remaining three stale expectations were identified from their source; they
are not described as observed failures in the aborted full run. All original
initializers and assertions remain intact. No native input changed.

## Files changed

- tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs
- tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs
- tests/OS.HostTests/M02Ud2VectorStateEfiIntegrationTests.cs
- tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs
- docs/completion-gates.md, docs/efi-build.md and this step.

## Tests run

The isolated .NET 10/C#14 harness links and explicitly invokes each complete
original initializer once. Its 23 inner cases and four groups passed exactly
27 TEST_START / 28 TEST_PASS / zero TEST_FAIL / one host.tests terminal.
The build ran 2026-10-04 23:44:20.385757-23:44:27.027136 UTC and the run ended
23:48:05.563243 UTC. Both returned native exit0 with empty stderr; the build
reported zero warnings/errors. Both original UD2 publication paths passed
their artifact audit. Their original temporary-child cleanup remains intact;
no separately retained child-image archive is claimed.

Independent review verified all 936 before/after/current/snapshot inputs,
14 launch originals and copies, the 42 owned source rows and all 106 native
inputs. Native aggregate remains
d6754ab6ab3005d0b4293b239d4039497302d9acae060946c9023ea1c4d616ed.
The four-file source diff contains exactly four replacements; git diff --check
passes. This focused run does not replace the required complete common suite.

## Logs and evidence

The TypeManager worktree retains artifacts/issue544-common-pin-repair:
source-checkpoint1 contains the frozen command plan and source inventory;
attempt1 contains exact argv, intervals, raw streams, source and launch
snapshots; final contains the independently checked report and result.

- Final report SHA256: A1D689FE99D2BAFD0496DF03169F180F7A03048C12539069ABA21D25EC6C634A.
- Final result: ACA318793FC7B1E498031EC113C92333A2B349D7B942C153FEDB3EE0C3AE604C.
- Attempt result: 4C786907BB872606A7DFFBC54AD25AECEAE5A069C9E7BBC362ADD42BDA3CC936.
- Final three-file manifest: CCA3948E74C814259D252F92527AE685366073A0272577A48EB327975425D465.

The root artifact directory orchestration-20261001 retains the diagnosis,
independent raw-project review, failed common run and its five preserved DLLs.
Step987 was reserved with scripts/new-done-step.ps1 -Reserve using the shared
allocation view; records are under issue544-common-pin-integration.

## Failures

The full common run at 5bc37ea ran 22:10:17.075826-23:28:40.613321 UTC on
2026-10-04. Its native exit was 3762504530 (signed -532462766). The first
M02.008 closure case failed, its other four cases passed, then the original
initializer threw. Sources and launch binaries were unchanged. No common
acceptance is inferred from that run. Its stdout SHA256 is
56C1542DF61A589D00A26D362DCD1480E1614562309764C2C942751187EEDF9D;
stderr is F858F1D4D751412CAD14EE0A80F055C396C3BDAE788BA0A813C16C3A25174FD5.

A preparation-only source-package comparison initially omitted the existing
New metadata field. Its record is preserved; correcting that comparison did
not require an additional tracked source change.

## Deferred work and evidence boundary

The parent must rebuild and launch a fresh complete common suite at the repair
commit, recording the new DLL identities before launch. Current focused native
proof remains applicable to the unchanged 106 native inputs, but association
with the new full run and fresh default/TypeManager guest images is required.
Issues #544/#80 remain incomplete; #81 and collecting GC work remain blocked.
No root enumeration, real ExitBootServices, hosted .NET/Kestrel, cloud support
or production readiness follows from this test-expectation repair.
