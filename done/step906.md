# Step 906 - Add independent MOSA-derived endian conformance vectors

## Summary

Implemented M04-002's allocation-free, reference-free
`OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa`
catalog. It carries 25 explicit `UInt16` read vectors from the distinct MOSA
five-byte cross product and seven explicit reverse-endian vectors from the
distinct MOSA `ushort` series. Caller-owned catalog copies validate exact
counts, initialization, key range, duplicate keys, expected values, append-only
ordering, and frozen FNV-1a64 fingerprint `E81E4451E8D999F6` through
status-coded results.

The exact owned host test compares every read vector against both
`OS.Std.Binary.EndianPrimitives` and .NET 10
`System.Buffers.Binary.BinaryPrimitives`, compares every reverse vector and
double-reverse identity against .NET 10, exercises all fail-closed catalog
paths, and emits:

```text
CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceTestSha256=DDC01A437FB2F446B18758E4BECC8DA0370177B33B6271897E7295AE14A007BC referenceFilesRequired=false hostConformance=true guestIntegrated=false gate4=Fail
```

Completion Gate 4 remains `Fail`.

## Why

The existing endian helper had ordinary host tests, but no independent,
append-only vector catalog tied to the reconstructed M04 runtime backlog.
MOSA's snapshot supplies useful boundary-value and cross-product test ideas,
while its compiler/runtime and incomplete `BinaryPrimitives` implementation are
not CLROS architecture or evidence. This slice independently reauthors the
expected values as primitive-only CLROS PODs, preserves their integrity with a
frozen fingerprint, and makes malformed, duplicate, reordered, stale-hash, and
default-result behavior explicit without importing reference source into the
build.

## Files changed

M04-002 owns exactly:

- `std/no-runtime/shared/IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs`
- `tests/OS.HostTests/IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSATests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- this globally reserved `done/step906.md`

The production/test implementation is commit
`9b10f2d8f5e96e60fa9230df7bd37f2165c4ba2c`, tree
`0a07adf2d21ee5eb15abb78897c5f899b2b09f0f`, based on
`9585bac26b18e852ddb9a6276837d205d24d635a`, tree
`7cc2b92af4160cce15df178861622c84e2f3fccc`. It adds 1,364 lines of
first-party C# across the two owned implementation/test files.

No EFI entry point, EFI project file, kernel boot sequence, probe toggle,
README, backlog manifest, existing done ledger, reference snapshot, generated
artifact, `OS.*` identity, `src/OS` identity, `src/OS.Kernel` identity, or
`OSHost_*` identity changed.

## Tests run

Focused development checks completed at the implementation commit:

```powershell
dotnet build .\std\no-runtime\shared\OS.Std.NoRuntime.Shared.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project <temporary-exact-file-harness> --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
git status --short
```

Both project builds and the solution build reported zero warnings and zero
errors. The exact-file focused harness emitted one ordered M04-002
start/ready/pass envelope and no failure marker. Naming and source-firewall
checks passed. QEMU dry-run passed and remains host tooling preflight only.

The complete source-frozen HostTests run, EFI publish, and real QEMU run are
not yet recorded in this checkpoint; their final commands, logs, exit codes,
hashes, and rejected attempts must be added before review.

## Evidence

Reference inspection was read-only. No reference file is required by the build
or tests. The independently projected inputs are bound to these local snapshot
hashes:

- MOSA `BinaryPrimitivesTests.cs` SHA-256
  `DDC01A437FB2F446B18758E4BECC8DA0370177B33B6271897E7295AE14A007BC`.
- MOSA `Numbers/U1.cs` SHA-256
  `DF3EF82D5286D69E3ADE7E7124D9B15652C58CE80598BA02206977C00AE840CE`.
- MOSA `Numbers/U2.cs` SHA-256
  `163A6BEBD4A2F916F8ECF54F38C855F038EE9B0596241DE8F3937931A5664277`.

The supplied archive has no recoverable Git revision and its complete New BSD
notice is not locally established. Accordingly, this change copies no MOSA
implementation or test source. It independently encodes primitive expected
values under the repository's documented test-vector-only boundary.

The global done-step audit scanned 188 local/remote refs, found global maximum
step 905 on the M02-004 branch, seeded an ignored reservation view with that
audited maximum, and the repository allocator emitted:

```text
TEST_START done.step.allocate
NEXT_DONE_STEP done/step906.md
TEST_PASS done.step.allocate
```

## Failures

- The first reference search exceeded its 30-second bound after already
  identifying the exact MOSA files. It was rejected as an incomplete search;
  subsequent path-scoped reads and hashes completed successfully.
- An early inspection used the obsolete guessed path
  `std/no-runtime/shared/EndianPrimitives.cs`. The canonical existing helper is
  `std/no-runtime/shared/Binary/EndianPrimitives.cs`; no repository file was
  changed by the failed read.
- The current issue cannot yet satisfy its real-QEMU marker requirement from
  its four declared implementation/document paths. The EFI project uses an
  explicit source closure, while M04-002 forbids `src/OS/EfiEntry.cs` and
  `src/OS/OS.csproj`. Those files are also still owned by open PR #444. No
  guest marker, QEMU pass, or broader runtime claim is fabricated here.

## Deferred work

- Run and preserve one complete uncompromised HostTests gate from a clean,
  pushed M04 source head.
- Wait for independent review of exact PR #444 head
  `314d674f40923a94ba0fe251c12fbc70799e2086`, then merge/converge M02-004
  before claiming a separate serialized EFI integration issue for the two
  shared files.
- Add only the separately owned no-stdlib EFI catalog validation/marker path,
  then run a fresh EFI publish and real QEMU/OVMF command requiring
  `CLROS_M04_002_READY`.
- Update this step with final source heads, command logs, hashes, failures,
  deferred work, and independent-review boundary before moving issue #71 from
  claimed to review.
- Hosted .NET 10, Minimal API, Kestrel, provider support, CloudReady status,
  and production readiness remain unproven.

The current evidence boundary is an allocation-free, host-tested conformance
catalog over 25 `UInt16` read vectors and seven reverse vectors. It proves no
guest integration, runtime helper execution, allocation/GC, canonical BCL
surface, real `ExitBootServices`, live driver/storage/network behavior, hosted
.NET, Kestrel, provider support, CloudReady status, or production readiness.
