# Step 850 - Define and validate loaded-image range capture

## Summary

Implemented the host-only M01-003 loaded-image range contract:

- Added a 40-byte readonly unmanaged
  `EfiLoadedImageProtocolObservation` that binds firmware status, input image
  handle, transient protocol address, image base, and image size into one
  caller-owned acquisition transcript.
- Added a 144-byte readonly unmanaged
  `EfiLoadedImageRangeCapturePlan` with explicit append-only statuses, nested
  typed-address and canonical-reservation statuses, count/capacity/index
  evidence, and deterministic fail-closed defaults.
- Validated exact-one count, declared capacity, pointer presence, 8-byte
  alignment, full observation-span arithmetic, and the fixed 4 KiB page-size
  contract before the sole observation read.
- Validated image handle, every nonzero firmware status, protocol pointer,
  exact image base/size/end, page-zero exclusion, and outward-rounding
  overflow in deterministic order.
- Added an 80-byte readonly unmanaged `EfiLoadedImageRange` that preserves the
  exact typed extent and embeds one canonical `BootReservedRange` with kind
  `LoadedImage`.
- Revalidated every redundant exact-range, padding, page, byte, and canonical
  reservation relationship before a result can report success.
- Consumed the transient protocol address for validation without retaining it
  in either the range or capture result.
- Added exact-source and API firewalls plus a retained `NOSTDLIB_EFI`
  compilation closure whose emitted PE must contain zero assembly references.

## Why

M01-002 established the canonical CLROS reservation record and table validator,
but it did not define how the running EFI image's exact byte extent becomes a
page-aligned `LoadedImage` reservation. M01-003 supplies that narrow,
status-coded value contract without prematurely adding a firmware-call layer.

The compact observation prevents a caller from combining a firmware status,
handle, protocol pointer, image base, and image size from different acquisition
attempts. Its unsafe evaluator can validate caller-supplied metadata and
arithmetic, but it cannot prove that an arbitrary pointer is readable. The
future live adapter therefore remains a separate boundary.

## Files changed

- `src/OS.Kernel/Boot/EfiLoadedImageRange.cs`
- `src/OS.Kernel/Boot/EfiLoadedImageRangeCapturePlan.cs`
- `tests/OS.HostTests/EfiLoadedImageRangeCapturePlanTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step850.md`

No `src/OS/Boot/EfiEntry.cs`, `src/OS/OS.csproj`, `BootSequence.cs`,
`Probes.cs`, README, completion-gate, backlog-manifest, reference-snapshot, or
first-party native source file was changed.

## Tests run

Tool and source identity:

```text
dotnet SDK: 10.0.302
branch: codex/M01-003-loaded-image-range
base: d5d016b7c3b51129e5cb1a617945a6668f130d64
implementation: c536329289b19647a95a97aceef6ca313f589912

EfiLoadedImageRange.cs SHA-256:
  B2967924097355A7BD83DFAEEB8E460C418E24026AA14799C20D90182B50FDB6
EfiLoadedImageRangeCapturePlan.cs SHA-256:
  5307F36A9D0B04AA863C176B281CDBAE9A39E9DC444A867E5E50A821711E1721
EfiLoadedImageRangeCapturePlanTests.cs SHA-256:
  D7CD39C2DA2A27428E6FF5680993885A354FC8E914C0449EAD1DCE24267DFCF6
docs/boot.md SHA-256:
  AEF1B071A5DD3258C03FB636C19799222EDC4BB70B3480875CB8EB837E5B7694
docs/architecture.md SHA-256:
  9E53AF10655B491EA0806C4FFC3383CE7E0E5A08C13EFCA5D38CDF1492BEFACF
```

Commands:

```text
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
exit 0; 0 warnings; 0 errors

dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
exit 0; 0 warnings; 0 errors

2026-07-26T03:11:13.9057832Z
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
exit 0; completed 2026-07-26T03:14:47.2300019Z

dotnet build .\CLROS.slnx --nologo -m:1
exit 0; 0 warnings; 0 errors

powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1
exit 0; TEST_PASS naming.guard; TEST_PASS source.firewall

powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun
exit 0; TEST_PASS workload.profile.tooling default single;
TEST_PASS qemu.runner.dryrun

git diff --check
exit 0
```

The final complete host run produced:

```text
log: C:\Users\Roger\AppData\Local\Temp\
  clros-m01-003-final-acceptance.log
log bytes: 739878
log lines: 5784
log SHA-256:
  812C00347F5AF8618029539880CAF8F71D9AB58000F5511ECF35F7BB8B5017CE
stderr bytes: 0
TEST_PASS marker count: 2890
TEST_FAIL/OS_SMOKE_FAIL marker count: 0
CLROS_M01_003_READY marker count: 1
TEST_PASS m01.003 marker count: 1
TEST_PASS host.tests marker count: 1
```

## Evidence

Focused evidence markers:

```text
TEST_START m01.003
TEST_PASS m01.003 statuses and pod layouts are stable
TEST_PASS m01.003 default values fail closed
TEST_PASS m01.003 aligned image produces canonical reservation
TEST_PASS m01.003 outward rounding preserves exact extent
TEST_PASS m01.003 boundary sized image remains checked
TEST_PASS m01.003 pointer count and capacity fail before dereference
TEST_PASS m01.003 invalid page sizes fail before observation use
TEST_PASS m01.003 firmware transcript failures are classified
TEST_PASS m01.003 image range failures are classified
TEST_PASS m01.003 validation precedence is deterministic
TEST_PASS m01.003 canonical reservation is accepted by ledger validator
TEST_PASS m01.003 malformed result values cannot report success
TEST_PASS m01.003 evaluation is nonmutating deterministic and allocation free
TEST_PASS m01.003 result does not retain firmware protocol pointer
TEST_PASS m01.003 sources exclude hosted and live firmware dependencies
TEST_PASS m01.003 exact sources compile under nostdlib efi
CLROS_M01_003_READY
TEST_PASS m01.003
TEST_PASS host.tests
```

The matrix includes:

- Exact enum values 0-20, 40/80/144-byte POD sizes, field offsets, readonly
  private fields, private constructors, unmanaged/value-only surfaces, and
  exact production member allowlists.
- Default observation/range/plan values and structural corruption of capture
  status, nested statuses, all five result-reserved bytes, counts, capacity,
  invalid index, page size, firmware status, handle, exact range, padding, and
  every canonical reservation geometry category.
- Aligned, unaligned, one-byte, cross-page, aligned-end, multi-page,
  highest-useful-page, and very large exact range vectors.
- Exact-end overflow, outward-rounding-only overflow, and physical-page-zero
  exclusion.
- Zero and excessive count, insufficient capacity, null pointer, misaligned
  pointer, aligned near-maximum pointer-span wrap, invalid page sizes, extra
  poisoned capacity, and maximum declared capacity.
- Zero handle, EFI error-valued status, EFI warning-valued status, missing and
  misaligned protocol pointer, zero base, and zero size with locked failure
  precedence.
- Canonical `BootReservedRange` creation and acceptance by the existing
  one-record table validator without populating live state.
- Byte-identical results for distinct valid protocol pointers, proving the
  transient pointer is not retained.
- Byte-for-byte observation immutability and zero managed allocations over
  1,000 warmed success and failure iterations.
- Exact source compilation with `NoStdLib=true`, framework references removed,
  `NOSTDLIB_EFI` defined, bounded process/stream waits, exact guarded temporary
  cleanup, and zero emitted assembly references.

Two independent read-only reviewers found no remaining production, pointer
safety, canonical-reservation, layout, no-stdlib, test, cleanup, marker, or
documentation blocker. Their nonblocking wording and POD-privacy observations
were tightened before the final acceptance run.

Reference influence was limited and recorded precisely:

- `references/SharpOS-main/OS/src/Boot/UefiFile.cs` (local snapshot SHA-256
  `4A2FC2739DDF294142787A5ED730CA268449B4BF31F153666AB50A69C766C7CB`)
  influenced status, handle, and returned-pointer validation order.
- `references/SharpOS-main/OS/src/Boot/UefiTypes.cs` (local snapshot SHA-256
  `BE363A53F6DBDE47BAA92EFB67F14A693CDEFA9025BA52B49D1C331323194A87`)
  was used to cross-check the real 96-byte x64 loaded-image field semantics.
- `references/RoseOS-master/Loader/src/EFI.cs` (local snapshot SHA-256
  `1C9EFF3980F370FA008CF7BC50A1D79D1969E7C7D1FBDEEBCAC611B9885CCD1D`)
  was test-only input for raw handle/protocol and unchecked-bound hazards.

The implementation is independently authored. No upstream API, string,
status name, wrapper, filesystem behavior, raw-call machinery, or source was
copied. The compact 40-byte CLROS observation is deliberately not the native
96-byte x64 `EFI_LOADED_IMAGE_PROTOCOL`.

## Failures

Intermediate failures were retained and repaired rather than hidden:

- The first draft retained a firmware protocol pointer in the result and
  duplicated reservation start/end/page/byte fields. Independent design review
  caught both issues before tests were locked; the final range embeds the
  canonical M01-002 reservation and the final result never retains the
  protocol pointer.
- An intermediate enum refactor briefly declared duplicate firmware and handle
  statuses. The kernel build reported both duplicate definitions; the status
  contract was rewritten with explicit unique append-only values 0-20.
- The first host-test compile used the wrong case for a named record-constructor
  argument. The one compiler error was corrected, after which the host project
  built with zero warnings and errors.
- The first exploratory harness invocation outlived a short shell wrapper and
  lost its final redirected output/exit-code channel. It was not accepted as
  evidence. A tracked rerun completed normally, and the later post-review final
  run above is the sole closure artifact.
- Reviewers noted that the original marker could imply a live ledger insertion
  and that privacy/reserved-byte invariants could be locked more explicitly.
  The marker now says the reservation is accepted by the ledger validator;
  tests now require private POD constructors/fields and corrupt every result
  reserved byte.

All final required commands passed. The QEMU command was a dry run and is not
reported as guest boot evidence.

## Deferred work

M01-003 proves only a host-tested observation, exact loaded-image range,
canonical outward-rounded reservation, deterministic status/evidence contract,
nonmutation, allocation-free evaluation, protocol-pointer nonretention, and
isolated exact-source no-stdlib compilation.

It does **not** prove or implement:

- A readable arbitrary observation pointer beyond the caller-owned readable
  and stable buffer precondition of the unsafe API.
- A native `EFI_LOADED_IMAGE_PROTOCOL` definition, `HandleProtocol`,
  `OpenProtocol`, function-pointer invocation, or any live firmware call.
- Source linking into `src/OS/OS.csproj`, `EfiMain` execution, or QEMU guest
  execution.
- Capture of the running EFI image, proof of image handle/protocol provenance,
  EFI `LoaderCode`/`LoaderData` memory-map provenance, or insertion into a live
  reservation table.
- Loaded-image subtraction from conventional memory, owned stack allocation,
  guard-page enforcement, stack switching, live page allocation/free, paging,
  CR3, TLB invalidation, cache policy, NX, or W^X.
- Real `ExitBootServices`, owned diagnostics, live PCI/storage/networking,
  hosted .NET, Kestrel, cloud-provider evidence, CloudReady status, or
  production readiness.

M01-021 owns the future live loaded-image protocol adapter. M01-022 owns real
QEMU evidence for that integration. The next dependency-ready M01 leaf may
proceed only after tracker evidence review closes M01-003 and the backlog
synchronizer recomputes readiness.
