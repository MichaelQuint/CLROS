# Step 985 - Register the current EFI image's bounded TypeManager metadata

## What changed and why

Step983's host contract validates and registers one module from supplied image
bytes. The separate default-off EFI adapter connects that same core to the
original M01 image capture and typed identity mapping. It retains a fixed,
aligned 4096-byte owner buffer and checks its complete writable/NX admission.
The emitted native TypeManager ABI and module slot remain separate from the
private owner state.

The adapter first exercises undersized storage without changing protected
bytes, then registers once and consumes the actual slot/handle/48-byte ABI.
It distinguishes present empty sections from absent sections, and verifies
repeat/foreign registration rejection. A fixed 45-record protocol describes
numeric results and bounded guards. This remains a one-module bootstrap probe;
GC-static initialization, roots and collection are later work.

The project and build/verification/smoke scripts add one mutually exclusive
default-false profile. There are 92 ordered Compile entries, 106 raw publication
inputs and 40 requested flags. Legacy fixture expectations are updated while
retaining their complete initializer registrations. The exact reviewed entropy
two-file framing correction is integrated before this checkpoint's validation.

## Files changed

- src/OS/Runtime/NativeAotModuleRegistrationProbe.cs: current-image adapter.
- src/OS.Kernel/Diagnostics/TypeManagerModuleRegistrationProbeAuthority.cs:
  explicit profile authority.
- src/OS/Boot/EfiEntry.cs and src/OS/OS.csproj: opt-in entry and source closure.
- scripts/build-efi-shell.ps1, scripts/test-efi-artifact.ps1 and
  scripts/run-qemu-smoke.ps1: bounded producer/verifier/profile/protocol support.
- tests/OS.HostTests/M04NativeAotModuleRegistrationEfiIntegrationTests.cs and
  TypeManagerModuleMetadataLiveNativeAOTImageTests.cs: complete controlled,
  mapped, framing, profile and publication regressions.
- Existing legacy EFI/preflight/verifier fixtures: additive profile/source
  expectations with their original groups retained.
- Existing canonical subsystem/evidence docs and this step.

## Tests run

The complete eleven-module managed attempt2 ran from 20:40:00.607748 to
21:46:20.424060 UTC on 2026-10-04 and passed 194 TEST_START / 195 TEST_PASS /
zero TEST_FAIL with one final host.tests terminal. Build and run returned 0
with empty stderr; the build reported zero warnings/errors. Independent
review accepted all 933 source/harness inputs and 14 launch identities and
archives. The actual 92-source no-stdlib child built successfully with zero
assembly references. All 41 preflight groups and the final artifact verifier
completed.

The original five-group EFI integration initializer ran from
21:52:45.930218 to 21:56:42.496902 UTC and passed 5 TEST_START / 6 TEST_PASS /
zero TEST_FAIL with one host.tests terminal. Its build and run returned 0,
with empty stderr and stable source/launch identities. Both default and
enabled NativeAOT publication archives retain 119 verified identity records.
The normal HostTests Release build ran from 21:58:40.969501 to
21:59:03.634593 UTC, returned 0 with empty stderr and stable inputs, and
reported zero warnings/errors.

The phases use the frozen `artifacts/issue544-type-manager/record-focused-checkpoint4.py`
recorder with `attempt2 managed`, `attempt3 native` and `attempt4 normal`.
Each phase retains the exact build/run argv and streams. `scripts/check-naming.ps1`
passes both naming.guard and source.firewall; `git diff --check` passes.

## Logs and evidence

Frozen combined source package:
artifacts/issue544-type-manager/source-checkpoint4. Its 174-member manifest is
EF3EDB8DEDCA2DB2BDBCA8AC0375BE17B00ABF9A04533389109F2D345A1C3242.
The immutable core SHA256 is
7DEE5F032CF4DCA09358DFA8A36D9772B12140ED733E02190AACC2A50D7EB006.
Raw106 aggregate is
d6754ab6ab3005d0b4293b239d4039497302d9acae060946c9023ea1c4d616ed.
All 35 initializer bodies across 42 owned files remain registered unchanged.

Managed attempt2 result SHA256:
D6F35261209B839E20BF8E50DC617E0F9181ABBAAB69C7C3BB1C7E31BD9573E0.
Independent review SHA256:
DB4DE23CBF7FCD22263C4D5C72D9AF75F4BFBECC1263C7D6601A134A5C556ED1.
Native attempt3 result SHA256:
9DC4C4A8575C68913A7972ACFAB3C59BCDA151E76BA3E71B1C3F02CB93F336E0.
Normal attempt4 result SHA256:
FADAECFD12DC6CB7F824BA9B879BD880EB3D8302EBCBDB96D2E506979D5DCDB3.
The new exact-detail negative controls execute inside the original passing
forbidden-source group. Its successful temporary fixture logs are cleaned by
the existing finally block; no separately retained child-log proof is claimed.

Step985 was reserved through scripts/new-done-step.ps1 -Reserve using a
coordination view with committed step984; the reservation records are retained
under root artifacts/orchestration-20261001/issue544-integration.

## Failures

The first managed attempt built successfully, then returned native exit1.
Its first nine complete initializers passed 132 starts/132 passes. The tenth
preflight module passed 14 of 41 groups and failed 27 because the generic source
guard rejected the new core's using directive before the intended later checks.
The original throw prevented the final verifier from running. Raw stream
counters include nested diagnostics and are not complete-module acceptance.

The repair admits only the exact canonical path and complete normalized core
hash, following the existing finite source-admission pattern. Modified content
at the allowed path and unchanged core at a different canonical source path
must fail with their exact expected diagnostics. Generic source guards remain
intact. Only the producer, two controls and three measured aggregate fixture
literals changed. The core and other 105 raw inputs were unchanged.

The corrected managed, native integration and normal build phases passed.

## Deferred work and evidence boundary

Fresh emitted-code review must establish alignment, actual metadata accesses,
failed-path write exclusion, publication-last and a conservative reachable
stack bound. The complete clean-head common suite and actual fresh default /
TypeManager QEMU captures remain separate required gates. Numeric admission
alone does not prove an actual mapping or lifetime.

Issues #544/#80 remain incomplete and #81 remains blocked. The upstream runtime
registry, GC statics, TLS, class constructors, root enumeration, collection,
concurrency and hardware-fault rollback are unimplemented. No general runtime,
real ExitBootServices, hosted .NET/Kestrel, cloud or production gate follows.
