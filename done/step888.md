# Step 888 - M01 cumulative pre-EBS live-paging QEMU evidence

## Summary

Completed the M01-026 cumulative local-QEMU/OVMF technical-exit slice from
clean, pushed implementation commit
`9fe466fdc06cfdf43ce8102b38592db9ef1c8b29`.

The runner now has a fail-closed all-switch M01 selector, guest-written register
PODs plus QMP-mediated physical-memory capture, POD and runtime-readback validation,
ordered/unique marker enforcement, cleanup enforcement, and evidence-manifest
inputs for one reversible pre-`ExitBootServices` owned-root interval. The
source-bound run observed all 42 required M01 guest markers exactly once and in
order, observed none of the 25 issue failure markers, kept
`M01_OWNED_CR3_ENTERED` out of serial output, and passed the independent raw
observation validator.

This step records the technical evidence only. Gitea review, merge, issue
closure, epic audit, and backlog readiness synchronization remain tracker
operations and do not create or strengthen the hardware claim.

## Why

The earlier M01 leaves deliberately established source, host-model, build, and
separate stage-one/stage-two QEMU evidence without claiming that the complete
stage-three machine-code interval had executed. M01-026 is the dependency-ordered
integration owner. It had to prove the exact cumulative path from a fresh EFI
artifact and capture processor-visible facts without logging from inside the
temporary owned-CR3 interval or weakening the default-off probe policy.

## Files changed

- `scripts/run-qemu-smoke.ps1`
  - Adds the exact cumulative M01 switch preflight and fresh-build requirement.
  - Generates and builds the strict raw-observation proxy.
  - Uses guest machine code to record register observations in PODs, then uses
    QMP to pause the 256 MiB guest, scan sixteen temporary 16 MiB
    physical-memory chunks, locate and validate those PODs, and release the
    withheld terminal only after all checks pass.
  - Cross-checks the POD, public/private serial identities, runtime readbacks,
    marker topology, unique/order constraints, and cleanup.
  - Deletes temporary chunks, private serial, and scratch content on success
    and on failure.
- `tests/OS.HostTests/QemuRunnerTests.cs`
  - Adds 15 focused M01-026 groups covering selector policy, exact switch
    forwarding, proxy generation/compilation, marker parity, path separation,
    fail-closed structure, semantic POD validation, hash validation, address
    validation, corruption rejection, and cleanup.
  - Fixes the marker-parity oracle so the generated shim must use literal
    manifest-backed markers.
- `docs/qemu.md`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
  - Record the exact live tuple and keep its claim bounded to the M01 local-QEMU
    pre-EBS technical exit.
- `done/step888.md`

No `src/OS`, `src/OS.Kernel`, `EfiEntry.cs`, `OS.csproj`,
`BootSequence.cs`, `Probes.cs`, `build-efi-shell.ps1`, `README.md`,
`references/`, or other forbidden/unowned path was changed by this issue.
Generated evidence remains under ignored `artifacts/` paths.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Pass in the accepted r12 host package.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Pass in the accepted r12 host package.
- Full `OS.HostTests` executable:
  - 3,328 exact `TEST_START` lines.
  - 3,329 exact `TEST_PASS` lines, including exactly one
    `TEST_PASS host.tests`.
  - Zero `TEST_FAIL` lines.
  - Empty stderr.
  - SHA-256
    `393FB9EAF36B7CB46EB32CF3496F5F8A38651F74BF887CAC5F65458DAC45E4B1`.
- Independent EFI artifact-verifier warm-up: pass.
- `.\scripts\check-naming.ps1`: pass.
- Exact all-switch M01 QEMU dry run with `-BuildEfi`: pass.
- Owned-path diff check against the issue base: pass.
- Worktree diff check: pass at the source-bound implementation commit.
- `git diff --check`: pass at the source-bound implementation commit.
- Fresh real QEMU/OVMF run:
  - `-EnableM01LoadedImageStackProbe`
  - `-EnableM01LiveAllocatorProbe`
  - `-EnableM01OwnedPagingProbe`
  - `-BuildEfi`
  - exact 42 required/ordered/unique issue markers
  - exact 25 forbidden issue failure markers
  - 30-second guest timeout
  - `TEST_PASS qemu.smoke`
  - `TEST_PASS m01.026.raw-observations`
- Canonical-path post-run EFI artifact verifier: pass.
- Evidence-manifest writer: `TEST_PASS evidence.manifest`.
- Independent rehash/revalidation of all 53 manifest artifacts: pass.

## Evidence

Accepted evidence root:

`artifacts/logs/m01-026-final/9fe466fd-20260729T160918Z-r12/`

Host gate:

- `wave-state.txt`: terminal `PASS`.
- Source commit:
  `9fe466fdc06cfdf43ce8102b38592db9ef1c8b29`.
- `wave-state.txt` SHA-256:
  `C254089C9505005B5EABACE8DA17E272C6D380882A1C539857491457008109D0`.
- Host stream SHA-256:
  `393FB9EAF36B7CB46EB32CF3496F5F8A38651F74BF887CAC5F65458DAC45E4B1`.

Full fresh `-BuildEfi` QEMU/OVMF acceptance command:

- Started UTC: `2026-07-29T16:13:34.3355662Z`.
- Completed UTC: `2026-07-29T16:15:15.2857325Z`.
- Exit code: `0`.
- QEMU: `11.0.50 (v11.0.0-12631-g54e84cdc7a)`.
- QEMU executable SHA-256:
  `B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`.
- OVMF size: 3,653,632 bytes.
- OVMF SHA-256:
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
- Runner result: `terminal=Success`, `orderedSeen=42`, `outOfOrder=0`,
  `uniqueSeen=42`, `duplicateUnique=0`.
- All 42 issue markers occurred exactly once and in order.
- All 25 issue failure markers were absent.
- `M01_OWNED_CR3_ENTERED` was absent from public serial.
- `08-real-qemu.state.txt`: terminal `status=PASS`.

Same-build EFI verifier:

- PE32+ AMD64 EFI subsystem 10.
- No import directory.
- Exactly one `CLROS_X64_SerializeInstructionPublication`.
- Serializer body: 20 bytes; SHA-256
  `DF76C8E39E0768D26EA27206FF102D7EADCC35D2C9BDF547B52B1171FD47BDAF`.
- Serializer object: 336 bytes; SHA-256
  `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.
- Publication cookie: `0x53524C43`.
- Expected Windows-x64 unwind coverage: pass.
- Whole EFI SHA-256:
  `76F2B404E91EB5E62CAD80B797B936BA3D3D39D5B0577889BB8459AF1B55C47C`.

Raw observation sidecar:

- 44 exact keys.
- CR0: `0x0000000080010033`.
- Firmware CR3 before/after/runtime: `0x000000000F801000`.
- Temporary owned CR3: `0x0000000000100000`.
- Raw CR4 before/after/runtime: `0x0000000000000668`.
- IA32_EFER: `0x0000000000000D00`.
- Physical address width: 40.
- RFLAGS before/after/runtime: `0x0000000000000246`.
- Firmware CR3, CR4, and RFLAGS restoration equality: pass.

Primary artifact SHA-256 values:

- Raw image:
  `309BAA429365AD88311510728560F5DC078BDDE62E9989DB1503D5489B3AECA9`.
- Public serial:
  `CC51100244A8A20865DB9D16CE5D73B0930F43EA98835DFA57027A00B126C661`.
- QEMU summary:
  `028DA1117470CE9938CF2EA676D5C640DEE59B243788478125CC15F6EA4E327D`.
- Raw-observation sidecar:
  `0106CFD2DD241B42ACC7FC3FD572B9526D6EC41C28AE89CF586D9A311BF6CB8F`.
- QMP transcript:
  `75C99D828D7C4A815F7F08381EC7CBFD55EEF8773FE8CB9949A6CA1CA8F3278F`.
- Exact QEMU command:
  `FDC405D14DFB8059742C3CDEFE24F11BC1912802FE40FA4CF484820149495221`.
- Real-QEMU wrapper stdout:
  `4FEAD5438C14BB42D79F427532069968C280645040F5A78B635D1DFF5B71BB18`.
- EFI publish log:
  `AED27B2D9EF64F442E5F9E41EE8A90B8DA7A6F2D068481C55B9964C16BF9B56D`.
- Post-QEMU hash ledger:
  `89E52395D104BAF332971FCCDC14D7A0A1D13DE131E50514BE67738212809368`.

Evidence manifest:

- Schema version: 1.
- Source dirty: `false`.
- Classification: `pass`.
- Exit code: `0`.
- Nonempty artifacts: 53.
- Tool identities: 10.
- Required guest markers: 44, comprising the 42 issue markers plus
  `OS_BOOT_OK` and `TEST_PASS boot.sequence`.
- Forbidden markers: 26, comprising the 25 issue failure markers plus
  `M01_OWNED_CR3_ENTERED`.
- Observed serial markers: 61.
- Independent artifact hashes revalidated: 53.
- Manifest SHA-256:
  `964F0A0346F4A4CE7B4D4E070A19743A307EAEFD16AD750849A4BFF3B67D916C`.
- Independent validation SHA-256:
  `4CE8912BF8BE6ABAAEF4DA49D13C90B23ED138463C54646F03F20999227A60F0`.

## Failures

Rejected attempts were preserved rather than relabeled as evidence:

- r1: two EFI-verifier fixture timeouts; the standalone verifier passed, but
  the wave was rejected.
- r2: positive fixture timeout; standalone reproduction passed, but the wave
  was rejected.
- r3: reached the source-policy oracle and correctly exposed a nonliteral M01
  marker-array defect. Commit `9fe466fd` added the four-line fail-closed parity
  fix.
- r4: orchestration priority race; rejected.
- r5: host launcher typo; rejected.
- r6: positive host run timeout; rejected.
- r7: the complete host executable emitted the exact terminal success stream,
  but the PowerShell parent observed a blank child `ExitCode` property. The
  preserved stream, empty stderr, exact start/pass pairing, terminal
  `TEST_PASS host.tests`, and the harness source's immediate `return 0` were
  independently audited in r12; the wrapper anomaly was not hidden.
- r8: verifier fixture timeout; rejected.
- r9: invalid scheduling-priority assumption in the audit wrapper; rejected.
- r10: a serializer-bytes fixture timed out at 30.063 seconds and reported
  `serializer_bytes_mismatch`; descendants were stopped and the wave was
  rejected.
- r11: the composite audit was interrupted during naming validation and was
  marked failed.
- r12: accepted host package and real-QEMU tuple.

The real guest run itself passed. The first independent post-run EFI verifier
invocation failed only because the evidence driver supplied relative project
path `.\src\OS\OS.csproj`, which the verifier correctly rejected as
`project_path_not_canonical_repository_contract`. The preserved resume
validator used canonical absolute project and entry paths, did not rebuild or
rerun QEMU, revalidated the same five-file EFI bundle, and passed. The executed
real-QEMU script SHA-256 is
`E1CC8CF47201A7DABB217685B16D3A5243C083DDAFD2D4057BE0620DCD5482AF`;
the resume validator SHA-256 is
`10BFD2C3BBF17953DAB3B2D66434350B2E5A0FE363DAC45B424764454B1EF6AF`.

After the evidence writer had already emitted
`TEST_PASS evidence.manifest`, the outer packaging wrapper incorrectly assumed
that `observedMarkers` must equal the 44 required markers; the correct observed
count is 61. That wrapper failure is preserved in
`10-manifest-wrapper-oracle-note.txt`. A separate validator rehashed all 53
manifest artifacts and verified the correct 44-required/61-observed contract
without rewriting the manifest.

## Deferred work

- Real `ExitBootServices` and retry execution.
- OS-owned COM1 diagnostics, IDT/GDT/TSS/IST installation, and controlled fault
  capture.
- A permanent owned root, general physical-memory allocator, managed heap,
  systemwide W^X/page-zero enforcement, SMP/TLB shootdown, PCID/LA57, and
  NMI/SMI safety.
- Live PCI/drivers, storage, networking, app loading, hosted .NET 10, ASP.NET
  Minimal API, and Kestrel.
- Physical-hardware, libvirt, and cloud-provider execution.
- CloudReady and production-readiness gates.

## Evidence boundary

This step proves one exact, reversible, pre-`ExitBootServices` M01 interval
under the recorded local QEMU/OVMF tuple. It proves the required stage marker
chain, temporary owned CR3, restoration evidence, one scoped
instruction-publication/protection/TLB/page-zero path, and the issue-specific
host capture mechanism.

It does not prove real firmware exit, restored firmware mapping policy beyond
the captured register equality, a permanent or systemwide paging/security
policy, owned diagnostics, physical or provider hardware, a functional kernel,
CloudReady status, or production readiness. Completion-gate status remains:
only the existing scoped Gate 2 default EFI smoke is `Pass`; all other gates
remain `Fail`.
