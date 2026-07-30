# Step 890 - Revalidate cumulative M01 QEMU evidence on repaired current source

## Summary

Revalidated the complete M01-026 cumulative pre-`ExitBootServices` tuple from
clean, pushed implementation commit
`8f1edea17f2de425520e239af58d37aa3b182146`, tree
`6b5359e36293d770e3dc0d074b1baabf8d4fdbf8`, after repairing the fake-QEMU
failure-classification race found by the first post-repair full gate.

The accepted r16 wave passed the complete host gate, built a fresh all-switch
EFI artifact, booted it with local QEMU/OVMF, enforced all 42 M01 issue guest
markers as required, ordered, and unique, rejected all 25 issue failure
markers, captured and validated the stopped-QMP physical-memory observation,
reran the canonical EFI artifact verifier, generated a schema-version-1
evidence manifest, and independently rehashed all 47 manifest artifacts.

`done/step888.md` remains the immutable historical r12 record. This step
supersedes it only for current-source acceptance.

## Why

M01-034's verifier/fixture repair was merged into the M01-026 branch before
closure. A fresh current-source evidence wave was therefore required. The
first attempt, r15, found that a host fixture wrote two forbidden
classification lines in separate appends. The production runner correctly
failed closed on the first forbidden line and could terminate the fake child
before its second append, while the test incorrectly required both lines in
one classification summary.

The repaired fixture emits one forbidden classification marker per case and
retains separate exhaustive every-forbidden-marker coverage. Production
fail-fast behavior was not weakened.

## Files changed

Implementation and host-test source:

- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`

Canonical current-evidence documentation:

- `docs/qemu.md`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/diagnostics.md`
- `docs/completion-gates.md`

Evidence ledgers:

- `done/step888.md` - immutable historical r12 record added earlier on the
  branch.
- `done/step890.md` - current r16 record.

No `src/**`, `std/**`, app, boot-order, probe-toggle, OS project, reference,
README, or first-party native-source path changed in this evidence refresh.
The stable `OS.*`, `src/OS`, `src/OS.Kernel`, and `OSHost_*` identities remain
unchanged.

## Tests run

Focused repair checks before the accepted wave:

- Stage-three failure-classification stress: 20 of 20 passes, zero failures;
  stdout SHA-256
  `FF01F95B59E76BE6952DA40BBE97BF8E912F138CA20B90FCF1DA856BB764B36D`.
- Stage-two failure-classification stress: 20 of 20 passes, zero failures;
  stdout SHA-256
  `8CF0658736212DCC53E33725329F72CC4BEE93919779A1C7180AD54D50E71A33`.
- Complete isolated `QemuRunnerTests`: 149 starts, 149 passes, zero failures;
  stdout SHA-256
  `38DBA0D1C6AA2D563E8EF2DE355B153170EC947370D9EA6598554ED2BB304848`.
- Focused stderr logs were empty, SHA-256
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

Accepted r16 host gate:

- `dotnet build .\CLROS.slnx --nologo -m:1`: exit 0, zero stderr.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`: exit 0,
  zero stderr.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`:
  exit 0, 3,328 `TEST_START`, 3,329 `TEST_PASS`, zero `TEST_FAIL`, exactly
  one `TEST_PASS host.tests`, and zero stderr.
- `.\scripts\check-naming.ps1`: exit 0, zero stderr.
- Exact all-three-switch `.\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi ...`:
  exit 0, zero stderr.
- `git diff --check`: exit 0.
- `git status --short`: empty at the implementation evidence boundary.

The former r15 failure region passed in the complete stream:

```text
TEST_START qemu M01 stage-three rejects cumulative failure classifications
TEST_PASS qemu M01 stage-three rejects cumulative failure classifications
```

Accepted real-QEMU command:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -BuildEfi `
  -EnableM01LoadedImageStackProbe `
  -EnableM01LiveAllocatorProbe `
  -EnableM01OwnedPagingProbe `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30
```

The executed command additionally bound explicit image, serial, summary,
raw-observation, required, ordered, unique, and forbidden marker arguments
under the r16 evidence root.

## Evidence

Accepted evidence root:

`artifacts/logs/m01-026-final/8f1edea1-20260730T145353Z-r16/`

Source and host gate:

- Source commit:
  `8f1edea17f2de425520e239af58d37aa3b182146`.
- Source tree:
  `6b5359e36293d770e3dc0d074b1baabf8d4fdbf8`.
- Remote branch matched the source commit; source dirty was `false`.
- Host gate terminal:
  `HOST_PASS|commit=8f1edea17f2de425520e239af58d37aa3b182146|tree=6b5359e36293d770e3dc0d074b1baabf8d4fdbf8`.
- Host stream SHA-256:
  `996E343DC72C34B6F2521C1E703A58BB98362242A3E894D48647057F139F13F3`.
- Host-state SHA-256:
  `F4C3CF0C49005FE4A89C56050C47FE52BD4F88775C8A588BD17B01F5FB65AED9`.
- Source-state SHA-256:
  `0B0CDE1E4B7B1D58DFD1CB1AF2B389BF895D3357D35923077307ADD142298444`.

Real QEMU/OVMF:

- Started UTC: `2026-07-30T15:36:47.5722979Z`.
- Completed UTC: `2026-07-30T15:37:49.8210616Z`.
- Exit code: 0.
- QEMU: `11.0.50 (v11.0.0-12631-g54e84cdc7a)`, 25,272,968 bytes,
  SHA-256
  `B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`.
- OVMF: 3,653,632 bytes, SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
- Summary terminal: `QEMU_SMOKE_STATUS pass`.
- Serial classification:
  `terminal=Success`, `orderedSeen=42`, `outOfOrder=0`,
  `uniqueSeen=42`, `duplicateUnique=0`.
- All 42 issue guest markers occurred exactly once and in order.
- All 25 issue failure markers were absent.
- `M01_OWNED_CR3_ENTERED` was absent from public serial.
- `TEST_PASS qemu.smoke` and
  `TEST_PASS m01.026.raw-observations` each occurred exactly once.
- Outer acceptance terminal:
  `REAL_QEMU_ACCEPTANCE_PASS evidenceRoot=C:\Projects\CLROS\artifacts\logs\m01-026-final\8f1edea1-20260730T145353Z-r16`.
- Inner and outer stderr were empty.

Same-build EFI verifier:

- PE32+, AMD64 machine, EFI subsystem 10.
- Ordinary, bound, IAT, and delay-import directories absent.
- Exception directory: 807 runtime-function records and 621 distinct unwind
  records.
- Exactly one `CLROS_X64_SerializeInstructionPublication`.
- Serializer body: 20 bytes, expected cookie `0x53524C43`, SHA-256
  `DF76C8E39E0768D26EA27206FF102D7EADCC35D2C9BDF547B52B1171FD47BDAF`.
- Serializer object: 336 bytes, SHA-256
  `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.
- Whole EFI: 459,264 bytes, SHA-256
  `76F2B404E91EB5E62CAD80B797B936BA3D3D39D5B0577889BB8459AF1B55C47C`.
- Verifier terminal: `TEST_PASS efi.artifact.verify`; stderr empty.

Raw-observation boundary:

- Schema: `clros.m01.raw-observations.v1`.
- Capture: stopped QMP, 256 MiB guest, sixteen 16 MiB `pmemsave` chunks,
  terminal withheld until validation.
- 44 exact unique sidecar keys.
- CR0: `0x0000000080010033`.
- Firmware CR3 before/after/runtime:
  `0x000000000F801000`.
- Temporary owned CR3: `0x0000000000100000`.
- Raw CR4 before/after/runtime: `0x0000000000000668`.
- IA32_EFER: `0x0000000000000D00`.
- Physical address width: 40.
- RFLAGS before/after/runtime: `0x0000000000000246`.
- Exact equality validation: pass.
- Private serial, capture scratch, and temporary memory chunks were removed.

Primary SHA-256 values:

- Raw image:
  `D7D219AE32CDDA595C8A7685CD4F7733A932918258F1B52AA04C704F13333F82`.
- Public serial:
  `CC51100244A8A20865DB9D16CE5D73B0930F43EA98835DFA57027A00B126C661`.
- QEMU summary:
  `B8451020C2355B061E83DB090EFFD62A965A95AD73D11D7F9864477753582339`.
- Raw-observation sidecar:
  `6FFEDB0B5998A930E614E3EC0EE68E2B444439AE686A9412F3206760D257F933`.
- QMP transcript:
  `0A0A6CC6985593D798BBD158F46D5CE9B7D84A57928EC2781BFC3CDD643AC14E`.
- Exact QEMU command:
  `430FC9F805B235DAAA90720465D4A48AD532AFF7DF433DC323B5D7FBBF5575A5`.
- QEMU wrapper stdout:
  `484B4C602891C3FDC14203AA1E185BF5ABB099779583E3F6AE3CD53DE64099F6`.
- Outer acceptance stdout:
  `CB7BC5FD76D963D0381B2CC73C008921F1FEB53BC4D1F69DFA6597EE55CD8490`.
- EFI publish log:
  `23F93C985D984D2E8592E83F3F9EA2E5BAC9669B939921582B18E6D6CC55DE00`.
- Independent EFI verifier stdout:
  `98C3109A49D0A6661466CB9D3B785FB43F26438D9D0761DC1FC72843D57B80AB`.
- Final-state auxiliary ledger:
  `89A1A89B600698618E9DEDDE91FB1FABFD5B7B7993FA023AE8C5329C9B340F07`.

Evidence manifest:

- Schema version: 1.
- Source dirty: `false`.
- Classification: `pass`.
- Exit code: 0.
- Nonempty artifacts: 47.
- Independently rehashed artifacts: 47; mismatches: 0.
- Tool identities: 10.
- Required markers: 44, comprising the 42 issue markers plus `OS_BOOT_OK`
  and `TEST_PASS boot.sequence`.
- Forbidden markers: 26, comprising the 25 issue failure markers plus
  `M01_OWNED_CR3_ENTERED`.
- Observed serial markers: 61.
- Manifest SHA-256:
  `94A28FE683FC068C88F42AB806B3E9A06DB5467FD35488B6ADF397AC8F8A8188`.
- Independent validation record SHA-256:
  `B686F7C9C69CE8A9FB8731A4C8C2DF3807006A9993AB2349B763CE556E05DAA1`.

The driver's `09-post-qemu.hashes.txt` is not accepted as the final ledger:
it hashed the run-state file before that file was rewritten from
`PROCESS_EXIT_ZERO` to final `PASS`. The schema manifest excluded that
intermediate ledger. Packaging generated and independently revalidated
`10-current-artifacts.sha256` from the final run state instead.

## Failures

- r15: the full host run failed
  `qemu M01 stage-three rejects cumulative failure classifications`. No real
  QEMU run occurred, and r15 was rejected rather than promoted.
- Evidence packaging attempt 1: rejected
  `Collection was modified` because the packager rewrote ordered-dictionary
  values while enumerating its live key collection.
- Evidence packaging attempt 2: rejected because a single matching outer
  driver path was returned as a scalar under strict mode and did not expose a
  `Count` property.
- Evidence packaging attempt 3: passed after snapshotting the tool-key
  collection and preserving the outer-path candidates as an array. The two
  rejected attempts created no manifest or final ledger.

## Deferred work

- M01-026 still requires independent change/evidence review, tracker review,
  guarded merge, and reviewed issue closure.
- The durable backlog must then record M01-026 closed evidence through the
  M01-036 manifest/synchronizer repair before readiness is recomputed.
- M02 owns the next diagnostics and safe-stub work; no M02 live port-I/O or
  owned-COM1 claim is made here.

## Evidence boundary

This step proves one exact local QEMU/OVMF boot of the source-bound,
default-off cumulative M01 path before real `ExitBootServices`. It proves the
exact stage-one through stage-three marker sequence, the bounded
caller-observed reversible owned-root interval, restoration equality, one
scoped RW+NX-to-RO+X transition, targeted invalidation, constant-return probe,
and page-zero absence in that temporary owned hierarchy.

It does not prove restored firmware mapping policy, a permanent owned root,
permanent or systemwide W^X/page-zero policy, real `ExitBootServices`, owned
COM1 or post-firmware diagnostics, IDT/GDT/TSS/IST installation, controlled
fault capture, physical hardware behavior, NMI/SMI safety, SMP TLB shootdown,
cross-core or DMA-written-code publication, storage, networking, app launch,
hosted .NET, Minimal API, Kestrel, libvirt/provider support, CloudReady
status, or production readiness. No numbered completion-gate status changes.
