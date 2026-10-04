# Step 978 - Bounded host firmware-entropy retention

## What changed and why

M03-002 (#59) and its native supplement #539 closed after PR538 merged at
`34a9fd562384a627decb35c2378f86c22b7a427e`. Evidence comments 14850/14843 and
closed/done states were read back; the separate COFF writer #536 closed with
comment 14838. This satisfies #60's declared dependency. #60 was changed from
blocked to claimed (body version 2, comment 14869), with host-only ownership.
The fresh read-only audit found no additional eligible unclaimed leaf.

The new M03-003 host contract retains one 32- or 64-byte result from the existing
guarded RNG interface in caller-owned storage. It checks full declared buffer
and publication aliases, requested length, capacities and publication state
before the sole source call. Successful source status, firmware mode and exact
counts precede copying. Private byte comparison precedes clearing the requested
scratch slice and publishing the POD retained length last.

Admission/source/pre-copy failures preserve the complete retained destination
and publication. A failed source may partly modify scratch. A late readback
failure cannot publish success and does not promise rollback. The synchronous
provider and storage must remain stable and nonthrowing. Zero or repeated source
bytes are valid; no entropy-quality heuristic or fallback is introduced.

## Files changed

- `src/OS.Kernel/Boot/AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown.cs`
- `tests/OS.HostTests/AnyFirmwareDerivedEntropySeedNeededFirmwareShutdownTests.cs`
- `docs/boot.md` and `docs/efi-build.md`
- `.gitea/backlog/clros-issues.json`: three M03 evidence/status rows only
- `done/step978.md`, reserved by `scripts/new-done-step.ps1 -Reserve` on
  2026-10-04 13:41:53.446980-13:41:53.698418 UTC, native exit 0

No EFI Compile item, profile, project, boot order, probe, runner, allocator or
shared runtime behavior changes in this host slice. Parent retains #537's
separate executing-stack sentinel integration lane.

## Tests run

The author worktree is
`C:/Users/Roger/.codex/worktrees/m03-003-entropy/CLROS`, branch
`codex/m03-003-entropy`, based on merge `34a9fd562384a627decb35c2378f86c22b7a427e`.
Its `artifacts/issue60/checkpoint1/commands.json` pins exact argument arrays:

1. `dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo -m:1
   --disable-build-servers -p:UseSharedCompilation=false --artifacts-path
   <worktree>/artifacts/issue60/attempt1/normal-output`
2. The same managed build options for the isolated
   `artifacts/issue60/checkpoint1/harness/Focused.csproj`, with separate
   `attempt1/focused-output` outputs.
3. `dotnet <worktree>/artifacts/issue60/attempt1/focused-output/bin/Focused/debug/Focused.dll`

On 2026-10-04 UTC, normal build ran 13:37:29.746265-13:37:53.773283;
focused build ran 13:37:53.779277-13:38:05.115500; both returned 0 with zero
warnings/errors. The complete focused run executed
13:38:05.274358-13:38:05.345079, native exit 0, empty stderr: **27 TEST_START,
28 TEST_PASS, zero TEST_FAIL**, correct pairs and one final `TEST_PASS host.tests`.
All three stderr streams are empty. This includes all 12 new groups and the
unchanged complete PlatformRandomGuard (7) and PlatformUefiGoneSimulation (8)
initializers. No selectively invoked method stands in for a complete module.

Tests cover exact and oversized capacities; patterned whole-buffer guards and
tails; full-span and publication aliases; immediate destination/publication
preservation after each rejection; source absence/error/short/over-reported or
default result; firmware-denial modes with no source calls; malformed/repeated
publication; private retention after scratch mutation and firmware denial;
zero/repeated valid fixture bytes; POD storage and warmed zero CLR allocation.
No actual entropy is acquired or printed by these deterministic fixtures.

## Logs, identities and review

Paths below are relative to the author worktree unless stated otherwise:

- Production source SHA256
  `B76FEF791119978524F7E159F99D26087D48BD33F292ABCB5422B6FE7C4000BC`;
  test source `AA403AFA4C90C82D5AA7413B6375FEA81E246B1D4E1867A97ADAD3F0B0CADC9C`.
- `artifacts/issue60/attempt1/result.json`:
  `21DA70B1668CE3FB35E9B1EC4791A8DD1504CBF27507D2AEA9317185E0B47929`.
- Complete focused stdout:
  `79183497B4F7E1E83FC093EB27BCEABAE513472C38621AB438E15FE393F02D44`.
- `artifacts/issue60/final/report.md`:
  `A68D3A439B641FC04F238FAD660DC2D73DC790AF1C6944CD0DE5B5B72353A939`.
- Nine-entry final package manifest:
  `9163D68B27BC4BDAC0E8B721A9F09680FD8B37463C6FBC800E1B9419D296A0A5`;
  42-entry execution manifest:
  `EEEA8C9D0DD76FB42710C25F38A794B417C079DD9D59BB7230A1B99C2552E6E2`.

The recorder binds 913 source/project/harness/recorder inputs and three actual
launched DLLs before and after execution. All remained byte-identical. Parent
read both complete source files, existing provider/result contracts, all raw
streams and report, then rehashed the package, execution inputs and launch
identities. Independent review accepted the source and actual focused evidence
without findings: root `artifacts/orchestration-20261001/issue60-host-independent-review/review.md`,
SHA256 `BB418C092230C164DD30825ECB738A66A533D3969BDC052541C66C035B2AC6E3`.
Its four-output manifest is
`07F444046197884639AA7D7D4360B9456C8D0910AAAAEAC8832AB0F4495ED167`;
966 unique reviewed inputs include the complete unchanged original initializers.
Parent imported the exact two source byte sequences into the root checkout.
Final integration checks passed on 2026-10-04: naming and source firewall
(13:46:22.983156-13:47:11.567411 UTC), catalog `-Offline` validation of all
345 issues, and catalog `-SelfTest` with 28 policy cases and zero actual API
calls. All returned native exit 0 with empty stderr. Their exact records are
the three `issue60-integration-*` command results under the root's
`artifacts/orchestration-20261001/issue531-final-transaction` directory.
`git diff --check` and the staged diff check passed before the six-file commit.
Independent final documentation/catalog review found no issue; all other
catalog rows and metadata remain unchanged. No native or full-suite evidence
is inferred from this batch.

## Failures

No compiler or test failure occurred in this implementation batch. The earlier
tracker claim stopped after its successful body/label readbacks because its
comment duplicate check mishandled an empty PowerShell 5 API array. Those failed
helper attempts were preserved; only the comment stage was repaired and posted
once, then read back as comment 14869. The claim and implementation were not
replayed. PR538's earlier failures remain in step977 and its transaction logs.

## Deferred work and evidence boundary

#60 remains open and claimed; #61 remains blocked. Actual EFI_RNG_PROTOCOL
discovery, a real GetRNG request, owned native retention and positive plus genuine
missing-provider QEMU cases require a separately scoped integration supplement.
Installed OVMF driver presence is preparation only. The host code is not added
to the no-stdlib EFI closure. Logical scratch clearing is not a physical
secure-erasure guarantee; no seed bytes, digest or encoded seed are logged.

Common integrated-head validation, native/guest evidence, PR merge and issue
closure remain separate future steps. This adds no PRNG, entropy pool, TLS,
cryptographic quality guarantee, actual ExitBootServices or post-firmware RNG.
It does not prove hosted .NET 10/Kestrel, cloud readiness or production readiness.
#537/#79 still need the actual executing-stack sentinel evidence; #80 remains
blocked. No acceptance criterion was waived to advance either chain.
