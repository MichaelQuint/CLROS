# Step 848 - Close the M01-001 typed-address evidence contract

## Summary

Closed the two evidence gaps left after the original M01-001 implementation:

- Removed generic raw-`ulong` ingress/egress (`Value`, `TryCreate`, and
  `TryCreateAligned`) from `PhysicalAddress`, `VirtualAddress`, and
  `DeviceAddress`.
- Replaced those escape points with status-coded methods whose names identify a
  UEFI or hardware-register boundary.
- Added default-failing raw extraction and status-coded same-domain
  `TryCompareTo`, including deterministic failure output.
- Added a retained compiler fixture that links the exact production source with
  `NoStdLib=true`, implicit framework references disabled, and
  `NOSTDLIB_EFI` defined.
- Verified that the fixture output has zero assembly references and that eleven
  prohibited raw/cross-domain expressions fail with stable, source-attributed
  compiler diagnostics.
- Preserved the host-only boundary. The typed wrappers are still not linked
  into `src/OS/OS.csproj` or executed by `EfiMain` or QEMU.

## Why

The original implementation in
`75c11c94eef7f4f146ed061deb06959f972a64c7` (merged by PR #322 as
`04f2bc09c07f23b9e9c2a4cbc5be2a80823d4e34`) had sound POD, default,
alignment, and overflow behavior, but its generic `Value` and factory surface
did not satisfy the reconstructed issue contract. It also lacked retained proof
that the exact production file compiles in the no-stdlib EFI source closure.

The evidence-contract repair merged at
`5dbb5d34929c063394dee7b22b0ac3759e4d5431` intentionally kept M01-001 open
until those claims were made true. This step records the implementation at
`9464b1872cb2adbf34a476a30754da4b705c3d4a`.

## Files changed

- `src/OS.Kernel/Boot/TypedPhysicalVirtualDeviceAddressPODWrappers.cs`
- `tests/OS.HostTests/TypedPhysicalVirtualDeviceAddressPODWrappersTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step848.md`

No `src/OS/OS.csproj`, EFI entry, `BootSequence`, `Probes`, README,
completion-gate, backlog-manifest, reference-snapshot, or native-source file was
changed by the M01-001 implementation branch.

## Tests run

Tool/source identity:

```text
dotnet SDK: 10.0.302
branch: codex/M01-001-evidence-closure
base: 5dbb5d34929c063394dee7b22b0ac3759e4d5431
implementation: 9464b1872cb2adbf34a476a30754da4b705c3d4a
typed-address source SHA-256: 32C9B1FD62CE0921D0FDBCF21ADFFBEA1DF4EA2AEDD22CB57AB406AF0D077FA7
```

Commands:

```text
2026-07-26T01:42:25.5683208Z
dotnet build .\CLROS.slnx --nologo -m:1
exit 0; 0 warnings; 0 errors

2026-07-26T01:42:36.5248907Z
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
exit 0; 0 warnings; 0 errors

2026-07-26T01:42:48.3589960Z
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
exit 0; completed 2026-07-26T01:46:15.5096365Z

2026-07-26T01:46:22.5511689Z
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
exit 0; TEST_PASS naming.guard; TEST_PASS source.firewall

2026-07-26T01:46:22.5566898Z
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
exit 0; TEST_PASS workload.profile.tooling default single;
TEST_PASS qemu.runner.dryrun

git diff --check
exit 0

git status --short
only this reserved evidence file remained before its evidence commit
```

## Evidence

The complete final host run produced:

```text
stdout bytes: 732000
stdout SHA-256: 2AD7FAFC84A0C8C9278600DCAED2EA4E61B12699CD79ADCB340CCF8D9E6C5BFA
stderr bytes: 0
stderr SHA-256: E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
TEST_FAIL/OS_SMOKE_FAIL marker count: 0
```

Required/focused markers:

```text
TEST_START m01.001
TEST_PASS m01.001 wrappers are stable pods
TEST_PASS m01.001 wrappers reject default and zero
TEST_PASS m01.001 named boundaries preserve raw values
TEST_PASS m01.001 default raw extraction fails closed
TEST_PASS m01.001 equality is domain local
TEST_PASS m01.001 ordering is domain local
TEST_PASS m01.001 wrappers expose only named raw boundaries
TEST_PASS m01.001 source excludes managed runtime dependencies
TEST_PASS m01.001 exact source compiles under nostdlib efi
TEST_PASS m01.001 prohibited expressions fail compilation
TEST_PASS m01.001 offsets accept nonoverflowing boundaries
TEST_PASS m01.001 offsets reject wrap
TEST_PASS m01.001 end-exclusive arithmetic succeeds
TEST_PASS m01.001 end-exclusive arithmetic rejects invalid ranges
TEST_PASS m01.001 alignment validates inputs
TEST_PASS m01.001 align-up succeeds
TEST_PASS m01.001 align-up rejects wrap
TEST_PASS m01.001 failures leave deterministic default outputs
CLROS_M01_001_READY
TEST_PASS m01.001
TEST_PASS host.tests
```

The positive compiler probe links the repository production file directly into
a temporary no-stdlib project and checks the emitted PE metadata contains zero
assembly references. The negative probe requires these filename/diagnostic
pairs before it passes:

```text
unnamed-ingress.cs       CS0117
unnamed-egress.cs        CS1061
implicit-raw.cs          CS0029
explicit-raw.cs          CS0030
cross-assignment.cs      CS0029
cross-add.cs             CS0019
cross-subtract.cs        CS0019
cross-equality.cs        CS0019
cross-order.cs           CS0019
wrong-domain-out.cs      CS1503
device-uefi-boundary.cs  CS0117
```

Two independent read-only reviewers found no remaining production, API,
no-stdlib, test, documentation, or manifest-acceptance blocker. No reference
implementation was copied; SharpOS and RoseOS remained architectural/test-only
influences already recorded in the issue.

## Failures

The evidence work did not hide intermediate failures:

- The first full closure run failed four M01-001 subtests. It exposed the
  generic raw boundary, a comment-sensitive source audit, and an incomplete
  no-stdlib shim (`RuntimeMetadataVersion`, compiler-required delegate and
  attribute metadata). The API, audit, and compiler fixture were corrected.
- A review then found an unbounded post-timeout wait and a possible
  `using`-alias bypass of the managed-reference audit. Process termination,
  stream draining, and cleanup are now bounded/guarded; all using directives,
  managed-type spellings, unexpected allocations, and the complete internal
  helper metadata surface are rejected.
- Removing `System.Delegate` from the isolated shim caused the C# compiler to
  issue `CS0518` for ordinary calls/operators. The compiler-required predefined
  type remains in the fixture only. Production use is blocked by the
  source/metadata audits.

All final required commands passed. The QEMU command was a dry run and is not
reported as boot evidence.

## Deferred work

M01-001 proves only host-tested C# POD layout, named raw-boundary intent,
same-domain checked operations, deterministic invalid/default handling, and an
isolated exact-source no-stdlib compile closure.

It does **not** prove or implement:

- Canonical EFI source linking or `EfiMain` execution (serialized to M01-020).
- Boot reservation policy/table validation (M01-002).
- Live physical/virtual translation, canonical-address enforcement, CR3,
  paging, TLB invalidation, page allocation/free, or W^X/NX.
- Runtime ownership/lifetime, DMA/IOMMU mapping, hardware-register validity, or
  authorization merely because a boundary method is named.
- Real QEMU guest execution for this source, `ExitBootServices`, owned
  diagnostics, Kestrel, cloud-provider support, CloudReady status, or production
  readiness.

M01-002 may consume `PhysicalAddress.TryCompareTo` and
`TryGetEndExclusive` without extracting raw integers after tracker evidence
review closes M01-001 and the backlog synchronizer recomputes readiness.
