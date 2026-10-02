# Step 941 - Default-off terminal page-fault EFI adapter

## Summary

Integrated the independently reviewed #517 EFI adapter over frozen #54/#510/#513
leaves into the runtime-next branch based on `a2a936b049d1ac475a721fa4a5683c2267104c6d`.
The 27 source/test files are byte-identical to the accepted detached author
worktree at `cf677286dd16dce00c3e191be2ead2bae3d85d92`. The final focused host
run and both NativeAOT artifact profiles pass. Full integrated host execution
and fresh real default/page-fault QEMU acceptance remain pending. #54/#517
remain open; Gates 3/4 remain Fail.

## Why

The prior page-image and terminal emitter had only host/compiler evidence.
This separate adapter must prepare actual EFI resources, bind current mode and
backing, publish the complete stream and provide a strict real-capture contract
before a page-fault guest can be accepted. The ordinary boot plan is unchanged.

## Files changed

The accepted source/test inventory is:

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
- `src/OS.Kernel/Diagnostics/NullPointerProbeAuthority.cs`
- `src/OS.Kernel/Diagnostics/Probes.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/M02PageFaultCr2ErrorCodeEfiIntegrationTests.cs`

Canonical updates: `docs/diagnostics.md`, `docs/efi-build.md`, `docs/qemu.md`,
`docs/boot-order.md`, `docs/completion-gates.md`, and the runtime status table
in `docs/nativeaot-nostdlib-limits.md`. Only M02-014's `currentEvidence` changes
in `.gitea/backlog/clros-issues.json`; all other catalog fields remain unchanged.
This step was reserved with `scripts/new-done-step.ps1 -Reserve`.

## Tests run

Final author command:

```powershell
dotnet run --project artifacts/m02-014/guest-focused/Focused.csproj -c Release
```

2026-10-02 07:23:30.0516924Z to 07:27:05.1335875Z, native exit 0, empty stderr:
eight starts, nine passes, no failures, one `TEST_PASS m02.014.guest.focused`.
The actual new EFI integration module runs all six groups and its outer pair;
the unchanged transition module runs in the same harness. There is no reflection
subset. Both actual default/enabled NativeAOT publishes and artifact checks pass.
The enabled no-stdlib project and complete HostTests project also build with
exit 0, zero warnings/errors; the latter is compilation, not full-suite execution.

The static nineteen-fixture audit covers 29 authority literals and 74 assertions;
it is not execution evidence. All nineteen complete modules, including their real
artifact cases, remain required in the settled root full-suite gate. Parent
integration verified every source hash before and after copying and checked
working-tree whitespace. No new real QEMU run occurred in this milestone.

Parent manifest-focused build/run also passed after the catalog update: native
exits 0, seventeen starts, eighteen passes, no failures and one terminal pass.
Naming and working-tree whitespace checks passed with native exit 0; all four
stderr logs are empty. Commands, timestamps and hashes are preserved in
`artifacts/orchestration-20261001/issue517-manifest-checks/`.

## Evidence

| Input/result | SHA256 |
| --- | --- |
| Accepted source inventory | `F2C52FD5BCBB2C003CF978C0FE3D15AEA416C6951B36078E0E5C6C0758B23AE1` |
| Independent final source acceptance | `C7BF9A25AAD62C64FDB6FFBFCC74A620748FE62B3651C3A2DB059663F47AE063` |
| Independent 230-file inventory | `C099535BD3894838C66D14810AD6FC2CDC152F8E767806F3EFE302BF3DEF309D` |
| Corrected handoff | `1C143035C31C21E15E8606D6894BE1BC8C38EB7D12C11B4F9449EFCFE8189FD5` |
| Final focused stdout | `2065AE8A34E4C3AA22A82A693ECEA7F554EA274ED4B8CEB1EF3CFE50F88DFF18` |
| Default EFI, 55,808 bytes | `EBAC4F464E5E9E21ECB8830D57DD15BEA4FC9705B5CD1B5AD1F8F67F1FFA77FD` |
| Default link sidecar | `81396BFAC190748D1768A1366E6B624029AFE549562035D29A100376EA9760B8` |
| Enabled EFI, 558,592 bytes | `9EC5A3C3494F5EF5E9B8405E17D8E9F3D63E1E71FCA32B4438FADC82C89D2961` |
| Enabled link sidecar | `6025C81F3A2F1D694C0486D15A0F7348C6DF11776C7F71B526A48D004478EFE8` |
| Raw 88-input aggregate | `502857EFE5C5789C31FC5B4D51726522CBBEA366C48CD90BF634C3F343098F70` |
| Evaluated 75-item Compile identity | `FAFC07E210DDE2A3A8E2D537F54032B2809CB01258A23B4D513E1D868DC74A9B` |
| Parent selected 200-file author archive inventory | `81661B655477D3491C86CB5038A1A5A51C8F8C7B7AAA6AE438F3C337DD475645` |

Raw author material is preserved in the worktree and byte-for-byte selected
parent archive `artifacts/orchestration-20261001/issue517-author-evidence/`.
Source copies and identities are in `issue517-integration/`; independent review
and repair controls are in `issue517-independent-review/` under the same parent.

Both sidecars bind 75 Compile items, 88 raw inputs and 24 evaluated switches.
Default is all false; enabled has exactly the new flag and eight prerequisites.
The tests read each actual OS.dll immediately after its build. The default DLL
was subsequently overwritten by the enabled build and cannot now be independently
rehashed: its zero-reference result is contemporaneous test/sidecar evidence.
The surviving enabled DLL independently reads as OS with zero AssemblyReferences,
SHA256 `A35303103167FE716749B5989A4269AE029077E146FCB8E84FEA46AA952FEDEC`.
The future guest wrapper must archive each lane's actual DLL before the next build.

## Failures

Review found and repaired scratch-buffer admission before the first query write,
literal-CS assumptions, cross-walk slot/parent consistency and absent-ancestor
contradictions with known mapped C/D/T ranges. Stale fixture project pins and the
new checked-port policy edge were corrected. Earlier fixture/compiler/PAT/mask,
profile-prerequisite and packaging errors are preserved in raw evidence; none
is relabeled a pass. The original handoff's invalid encoding and incorrect branch
description remain archived alongside the corrected detached-head handoff.

The first nineteen-module run was canceled on superseded source: twelve starts,
ten passes, no failure, no terminal pass and native exit -1. It is not accepted
test evidence. Parent chose one complete settled root suite to run all nineteen
modules after integration. Independent metadata inspection first used a shell
without reflection-only support; a fresh Windows PowerShell child succeeded.

The independent ignored tail auditor also needed two preserved-version repairs:
known-mapping/absent-ancestor consistency and firmware CR3 physical-frame/0x18
admission. These helper controls are synthetic and separate from production
source tests or actual guest evidence.

## Deferred work

Run the clean same-commit solution build and complete host suite, independently
accept the final guest wrapper, then capture fresh default and enabled QEMU/OVMF
tuples. Require per-lane source/tool/artifact/DLL identities, full prerequisite
stream, exact twelve-record/57-field tail, structural page-walk consistency,
actual 750 ms quiet drain, wrapper exit and native QEMU disposition. Keep #54 and
#517 open until accepted evidence is merged and posted/read back.

## Evidence boundary

The adapter owns C3 LoaderCode, D10 LoaderData and T43 page-table backing,
validates all five temporary buffers before writes, re-audits six actual derived
mappings, reads back and serializes before entry. The closed stream owns its
terminal records; no firmware callback or cleanup follows entry. Host preparation
controls never invoke privileged emitted bytes. Neither these checks nor native
compilation proves real CR3/descriptor activation, vector-14 delivery, CR2/error
capture or UART output. Persistent/default descriptor ownership, recovery,
ExitBootServices, drivers, hosted .NET/Kestrel, cloud and production readiness
remain unsupported.
