# Step 907 - Validate the M04-002 endian catalog in the no-stdlib EFI guest

## Summary

Integrated M04-002's fixed, allocation-free endian conformance catalog into
the explicit `src/OS` no-stdlib NativeAOT closure under the separately claimed
serialized issue #446. The host span API remains unchanged. Under
`NOSTDLIB_EFI`, the catalog instead exposes caller-owned unsafe pointer
copy/validation functions with explicit capacities/counts and distinct
fail-closed null-pointer statuses.

The default-false `EnableM04IndependentEndianConformanceProbe` profile adds one
explicit compile item, raising the exact EFI compile closure from 52 to 53 and
the build/verifier authority input count from 65 to 66. After EFI console and
serial transport are available, the entry point stack-allocates exactly 25
read-vector records and seven reverse-vector records, copies and validates the
catalog, and emits the exact success marker before normal memory-map markers.
Any mismatch emits `M04_ENDIAN_CONFORMANCE_FAILED` and aborts that marker path.

The focused real QEMU/OVMF run emitted exactly once:

```text
CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
```

Gate 4 remains `Fail`.

## Why

Managed issue #71 owns only the production catalog, its host conformance test,
and the two runtime/gate documents. It could not lawfully change the serialized
EFI entry point, explicit project closure, source-authority scripts, central
README/QEMU/EFI docs, or integration tests. Issue #446 was created and claimed
for exactly those paths after M02-004's overlapping EFI work merged. This keeps
the managed issue's ownership contract intact while supplying its required
real-QEMU marker evidence.

The fixed catalog is useful only if its no-runtime form can be compiled and
validated without importing the host BCL surface. A dedicated opt-in guest
profile provides that proof while keeping the default boot profile unchanged
and preserving the distinction between host comparison
(`hostConformance=true guestIntegrated=false`) and guest catalog validation
(`hostConformance=false guestIntegrated=true`).

## Files changed

Issue #446 owns:

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `scripts/test-efi-artifact.ps1`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- exact-count consumers under `tests/OS.HostTests`:
  `BuildEfiShellPreflightTests.cs`,
  `EfiArtifactVerificationScriptTests.cs`,
  `M02SafeStubPageEfiEntryTests.cs`,
  `M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`, and
  `M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `docs/efi-build.md`
- `docs/qemu.md`
- `README.md`
- this globally reserved `done/step907.md`

The combined wave also finishes #71's owned catalog, primary host test,
`docs/nativeaot-nostdlib-limits.md`, `docs/completion-gates.md`, and
`done/step906.md`.

No `.gitea/backlog` path, synchronizer, `BootSequence.cs`, `Probes.cs`,
unrelated kernel/std/test/script/doc path, reference, app, tool, generated
artifact, existing done step other than #71's unmerged step906, `OS.*`
identity, `src/OS`/`src/OS.Kernel` identity, or `OSHost_*` ABI identity changed.

## Tests run

Focused development commands:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -PreflightOnly -EfiOutputPath .\artifacts\efi\BOOTX64-M04-002-DEV.EFI -LogPath .\artifacts\logs\m04-002-efi-preflight.log -EnableM04IndependentEndianConformanceProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -EfiOutputPath .\artifacts\efi\BOOTX64-M04-002-DEV.EFI -LogPath .\artifacts\logs\m04-002-efi-build.log -EnableM04IndependentEndianConformanceProbe
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-qemu-smoke.ps1 -DryRun -EnableM04IndependentEndianConformanceProbe
.\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -SkipImageBuild -EnableM04IndependentEndianConformanceProbe
.\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM04IndependentEndianConformanceProbe
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -BuildEfi -EnableM04IndependentEndianConformanceProbe -EfiArtifact .\artifacts\efi\BOOTX64-M04-002-QEMU.EFI -Image .\artifacts\images\m04-002-qemu.raw -SerialLog .\artifacts\logs\m04-002-qemu-serial.log -SummaryLog .\artifacts\logs\m04-002-qemu-summary.log
```

The host-test project build reported zero warnings and zero errors. The
positive preflight reported `inputs=66`, `compileItems=53`, and
`independentEndianConformance=true`. The accepted EFI build independently
verified PE32+ AMD64, EFI subsystem 10, no ordinary/delay imports, the exact
`EfiMain` association, the linked serializer, and source/project/property
closure before emitting `TEST_PASS efi.artifact.verify` and
`TEST_PASS efi.shell.publish`.

The default dry-run passed unchanged. The two invalid M04 shapes exited `1`
with their exact `requires_build_efi` and `forbids_skip_image_build` reasons.
The valid M04 dry-run exited `0`, required the three-marker console/M04/memory
map order, required the success marker uniquely, and forbade
`M04_ENDIAN_CONFORMANCE_FAILED` plus `TEST_FAIL`.

The combined wave must still run one complete, uncompromised solution,
HostTests, naming/source-firewall, default dry-run, EFI/verifier, and real-QEMU
gate from the clean pushed PR head before review/closure. Its exact-head logs
and hashes belong in the Gitea evidence-review comments so this ledger does not
claim acceptance prematurely.

## Evidence

The focused real run used QEMU `11.0.50` and the 3,653,632-byte OVMF image with
SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
The serial state machine reported:

```text
SERIAL_MARKER_STREAM status=Passed terminal=Success bytes=8140 lines=27 invalidBytes=207 invalidLines=2 orderedSeen=3 outOfOrder=0 uniqueSeen=1 duplicateUnique=0
TEST_PASS qemu.smoke
```

The summary records `MISSING_MARKERS none`, `OUT_OF_ORDER_MARKERS none`,
`DUPLICATE_UNIQUE_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
`FAIL_REASON none`. Exact marker audit: success `1`, line-leading `TEST_FAIL`
`0`, `M04_ENDIAN_CONFORMANCE_FAILED` `0`.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `BOOTX64-M04-002-QEMU.EFI` | 62,464 | `44F4A7B5E2929884869F324E998ABC5B89559707ACC4BA65412EB818B18D9CA0` |
| EFI link evidence JSON | 395,585 | `1C50CB90373C7BB4D4B123BB5ADD8B4F7971C5BE66A544F4A07AA76246AD0708` |
| `m04-002-qemu.raw` | 67,108,864 | `024E42220A5543529E4A0D5F8BD7C4052062E8A9FF9E46457D913E9AF9462CD8` |
| serial log | 8,140 | `05EE15A753899894F86CCA0318CC724DC22671C78D12E4C7CD87E3BA2F262453` |
| summary log | 3,443 | `646A1AFDF8572317F71381F11EB8E6646E2DAD1236FEF03E2483DCA9B7ADBDB6` |

All generated logs, EFI/link files, and images remain ignored under
`artifacts/` and do not enter Git.

## Failures

- The first flagged EFI preflight rejected the intentionally stale canonical
  project-shape hash after the new explicit compile item and switch were added.
  The newly disclosed evaluated shape was independently recalculated and the
  single canonical hash pin was updated; no artifact from that attempt was
  accepted.
- The first full flagged publish linked until NativeAOT required
  `__security_cookie` for the bounded stack allocations. The profile now
  supplies a deterministic linker-visible anchor only when the M01 paging
  profile is absent. This is explicitly not randomized-cookie or production
  hardening evidence. The failed artifact was rejected.
- The next full publish linked successfully but the artifact verifier exited
  `127`: its bounded bootstrap accepted only name/value pairs while the new
  switch had been passed as a bare token. The bootstrap now accepts only the
  exact M04 parameter with a lowercase `true|false` value and converts it to a
  typed Boolean; unknown, duplicate, malformed, or non-Boolean parameters
  still fail closed.
- A direct diagnostic verifier call first used guessed `.map`/`.rsp` filenames
  and failed `missing_linker_map`; the canonical `.EFI.link.*` paths were then
  used. The next direct run exposed a missing M04 post-restore property in the
  link evidence. The build and verifier now both capture and compare that exact
  property. A later direct run correctly rejected a stale artifact after the
  build script changed with `source_closure_input_hash_mismatch`. Only the
  fresh rebuild above is positive evidence.
- The first complete pre-commit HostTests stream is rejected. It reached 2,483
  seconds before eight artifact-verifier fixture cases failed with
  `post_restore_evidence_property_set_invalid`; their synthetic valid
  post-restore property dictionary still omitted the new default-false M04
  property. Stdout SHA-256 was
  `A8D8C10FAD9799A6F4E6F98C5BF8DEEEB5B5021ABD894FCCE2FE94F4698D2343`;
  it had no terminal host pass and is not acceptance evidence. The owned
  fixture now includes `EnableM04IndependentEndianConformanceProbe=false` and
  adds explicit missing/enabled-without-switch negative mutations. A fresh
  complete run is required.

## Deferred work

- Run the complete final-head gate, push the clean branch, post exact commands,
  logs, counts, and hashes to issues #71 and #446, move both to `agent:review`,
  and obtain independent `admin` approval whose commit ID equals the current PR
  head before merge or closure.
- After merge, close the supplemental issue only with the approved evidence.
  Managed issue #71 still requires its separately reviewed manifest closure,
  canonical synchronizer application from current `master`, and an idempotent
  no-write preview before it can become `agent:done`.
- M02-005 remains the smallest ready issue, but it must not be claimed while
  the serialized M04 wave is still active.
- This step proves no general `System.Buffers.Binary.BinaryPrimitives`
  compatibility, allocation/GC/exception/runtime completion, real
  `ExitBootServices`, owned post-firmware diagnostics, physical/provider
  support, hosted .NET, Minimal API, Kestrel, CloudReady status, production
  readiness, or numbered-gate advancement.
