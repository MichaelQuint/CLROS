# Step 995 - Live owned-stack checks around GC-static materialization

## Change and purpose

The first GC-static guest passed, but its coordinator lacked the executing-stack
canaries required by #548. Add a real local-address witness and two lower-stack
canaries, authenticated against the original M01 stack-switch state before the
only sentinel writes. Require the 33-page geometry, expected initial RSP,
16 KiB clearance, and no overlap with the image, authority or original heap
controller/backing extents. The existing original-state getter is shared with
this exclusive profile; no second stack or authority is fabricated.

Read-only checks follow first materialization, authenticated repeat, actual value
allocation, object store/read and null store/read. The five checks revalidate
original identity and canaries. The transcript adds numeric stages 7 and 8,
including both actual local addresses and final sentinel readbacks; strict
admission now requires 60 CLROS lines and 49 ordered/unique anchors.

Changed files: `src/OS/Runtime/GCStaticRootMaterializationProbe.cs`,
`src/OS/Boot/EfiEntry.cs`, `scripts/run-qemu-smoke.ps1`, the existing GC EFI
integration fixture, four M01 source selectors, the double-fault conditional
XML-default assertion, `docs/efi-build.md`, `docs/completion-gates.md`.
The GC tree has 39 conditional defaults and 42 evaluated switches; those are
separate assertions. No project flags, compiled-source list or helper bytes change.

## Validation and evidence

The existing focused executable runs all four M01 initializers and all three GC
integration groups: 54 TEST_START / 55 TEST_PASS / zero TEST_FAIL, one terminal
host.tests. Build has zero warnings/errors. The strict parser rejects missing,
duplicated, reordered/early, malformed and extreme numeric stack/root records.
Logs: `artifacts/issue548-efi/stack-focused.log` and existing focused project.

Fresh ordinary producer/image/QEMU/OVMF run `guest6` completed at
2026-10-05T08:20:36Z, native exit 0, empty stderr. EFI SHA-256:
`7E133F90C5D169CDA66A9195432A7070065002242116C3F14B28E47BC5065202`.
The strict transcript passes; stack 0xDC47000..0xDC68000,
initial RSP 0xDC67FD8, coordinator witness 0xDC67990, checkpoint witness
0xDC67628. Five checks retain words 0x195A86B4C327D0EF and
0x62C79E1035AB48D6; two allocations still use 48 bytes and two scalar stores.
The runner observed 49 ordered/unique markers without duplicate/order errors,
then completed its configured 750 ms drain and terminated QEMU (exit -1).
Guest6's final informational text still printed the old 58-line count; the
actual parser used 60 and that informational text is corrected in this commit.
The complete command, serial, summary and separate streams are under
`artifacts/issue548-efi/guest6/`; generated binaries remain uncommitted.

The current-image native archive `artifacts/issue548-efi/native-stack/` binds
all emitted method bytes to the PE, all three unchanged helper thunks to the
actual callback, and the actual GC cell/MT/descriptor/publication readback.
The conservative RunGcStatics graph contains 132 methods/two bounded tables;
its first-party stack bound is 10,056 bytes below coordinator entry, within the
16 KiB reservation. Firmware OutputString/SerialWrite body depths are excluded.
The path checks cover nonvolatile GPRs and the one used nonvolatile SIMD
register (XMM6), with 235 restored returns/four restored tails. The actual PE unwind records
also match all 132 methods, including dynamic RBP frames and the XMM6 save.
These interim
artifacts do not alone constitute final native/common/guest acceptance.

## Failures and remaining work

The incomplete candidate's full run was intentionally stopped after discovering
the missing canaries; `common-run/intentional-stop.json` records the action.
Its exit 1 is not a passing common gate or an observed product failure.
Native analysis iterations that lacked terminal-Halt, firmware-call or special
reference-helper modeling are preserved, along with corrected current-byte
associations. No source was weakened to make those analyses pass.

Complete final stack-envelope/metadata acceptance and the common suite, then fresh
ordinary default/enabled acceptance captures. #81/#548 stay open and #82
blocked. Gates 3/4 remain Fail. This bounded noncollecting static-root diagnostic
does not prove root discovery, collection, general static initialization,
real ExitBootServices, hosted .NET/Kestrel or cloud readiness.
