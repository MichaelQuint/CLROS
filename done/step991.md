# Step 991 - Controlled typed ExitBootServices EFI integration

## What changed and why

The separately claimed #546 integration adds the default-off
`EnableM03TypedExitBootServicesCallProbe` profile for #61. It uses the existing
one-attempt adapter with a same-image unmanaged callback, the original opaque
EFI image handle, four independent retained states, and fixed controlled keys.
An independent monotonic entry counter, full-state snapshots and callback
observations verify rejection, consumption and reentry without another call.
The actual firmware ExitBootServices pointer is never acquired or called.

The producer and verifier admit the exact new source closure. The ordinary
runner validates the strict numeric protocol and its original firmware return
envelope. Legacy profile fixtures retain all original module initializers and
assertions while updating exact source pins, profile inventories and diagnostic
precedence. The host adapter and its original tests are unchanged.

## Files changed

- src/OS/Boot/EfiEntry.cs and src/OS/OS.csproj: controlled coordinator and profile.
- src/OS.Kernel/Diagnostics/TypedExitBootServicesCallProbeAuthority.cs: finite
  compile-time authority value, disabled by default.
- scripts/build-efi-shell.ps1, test-efi-artifact.ps1 and run-qemu-smoke.ps1:
  exact source/profile admission and controlled protocol validation.
- tests/OS.HostTests/M03TypedExitBootServicesCallEfiIntegrationTests.cs:
  five complete integration groups, including mapped callback/orphan children,
  stream parsing, profile isolation and fresh default/enabled publications.
- Existing preflight, verifier and EFI compatibility fixtures: narrow pin,
  inventory or expected-diagnostic updates; no original initializer removed.
- docs/boot.md, docs/efi-build.md and the M03-004 catalog evidence.
- done/step991.md, reserved once through the shared allocation view at
  2026-10-05 04:26:11 UTC.

## Tests run

The frozen source checkpoint3 remains the product basis. Its eight complete
managed modules built at 04:14:31.676661-04:14:40.726526 UTC and ran at
04:14:40.732831-04:58:03.581402 UTC on 2026-10-05: exit0, 115 TEST_START,
116 TEST_PASS, zero TEST_FAIL and one host.tests terminal. All 937 inputs and
14 launch files remain unchanged. The real four-source no-stdlib child has
zero AssemblyRefs and Prepare/Attempt IL bodies; it is compiled, not executed.

The complete new native integration module built at
05:52:34.772280-05:52:37.325482 UTC and ran at
05:52:37.332570-05:55:34.705619 UTC: exit0, five starts/six passes, zero failures
and one terminal. Both fresh native publication archives contain 122 verified
files. Their ordered closure is 95 Compile items, 109 raw inputs and 41 flags;
default enables none, enabled selects only the new controlled profile.

The normal Release HostTests build at 05:57:30.509282-05:57:43.626267 UTC
returned zero. All three builds have zero warnings/errors and empty stderr.
Exact argument arrays and before/after identities are retained by the recorder.

The parent has associated all 112 default and 123 enabled emitted method bodies
with their linked PE bytes. The 101-method enabled EFI graph has balanced
returns, aligned call sites, preserved nonvolatile registers in its unwind
records, and only volatile XMM0-5 use. Its local emitted bound is 2864 bytes
below EfiMain entry; firmware bodies, interrupts and available firmware stack
capacity are excluded. Reentry is bounded by the actual InFlight rejection
branch. This is an intermediate review result, not complete native acceptance.

## Logs and evidence

Worktree ignored artifacts: artifacts/issue546-typed-ebs/source-checkpoint3
and attempt1/attempt2/attempt3. Root ignored artifacts under
artifacts/orchestration-20261001 retain issue546-focused-integration and
issue546-native-independent-review, including all extraction inputs/outputs.

- Source checkpoint report SHA256:
  AB7AB008E8C28FB0C459E2DE97D648B4F16001892CA7E97C89CBD6889F4D046E.
- Raw input aggregate:
  98bab3768c27334d4a603186a2e80bc6cf4fa1c7a0a7c66f92fa6db64c5f7914.
- Managed stdout:
  25E51112F84570BD0B09570B5A3E1F924B139E6EF5CC57B50D3FA309ACC48B77.
- Native integration stdout:
  C446DCE88E818E411E22B23CF1F3382F4B49040DB305794F15B2623731C23C5B.
- Normal build stdout:
  BCAB27EB85E3458E2FF2263E16A1728FCDF6D5146080A9222A4AE6EAD897C24D.

## Failures

Before execution, source review repaired the independent entry counter,
Prepare enum handling, self-conflicting smoke flag and nine legacy fixture
expectations. Original checkpoints are retained. No product run failed.

The first artifact-only managed audit incorrectly assumed that only one
initializer had an outer pass marker. Its additive v2 uses the actual complete
initializer order and passes; no product rerun followed. The first stack
analysis incorrectly treated a volatile PUSH RAX scratch slot as a saved
nonvolatile register. Its additive v2 handles that slot as allocation and
passes. Original scripts and diagnostics are preserved.

## Deferred work and evidence boundary

Complete native semantic/storage review, the unfiltered common suite and fresh
default/enabled QEMU raw/lifecycle/native/disk association remain pending.
#61/#546 remain open/claimed and PR545 remains draft. #62 remains blocked.
The controlled callback is not firmware exit, fresh-map ordering or retry.
The profile uses the original EFI caller stack, not the M01 owned stack.
No global BootServicesExited, post-exit execution, GC, hosted .NET/Kestrel,
cloud readiness or production readiness is claimed.
