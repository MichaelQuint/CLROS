# Step 914 - Execute M02-007 x64 GDT encoding in the EFI guest

## Summary

Integrated the independently reviewed M02-007 x64 GDT encoder into the explicit
no-stdlib EFI source closure under a default-false cumulative probe. The guest
now encodes and reads back the exact 40-byte table against a caller-owned,
zeroed 104-byte TSS image and emits one fixed UEFI-serial evidence marker only
after every result, fingerprint, selector, and storage gate passes.

## Why

The managed M02-007 implementation proved the encoder on the host, but it could
not own the serialized EFI entry/project/QEMU paths. This supplemental slice
provides guest execution evidence while deliberately stopping before `lgdt`,
`ltr`, active descriptor installation, a fault route, or firmware shutdown.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- the eight exact-count/source-bound HostTests listed in supplemental issue
  #460
- `docs/efi-build.md`
- `docs/qemu.md`
- `README.md`
- this uniquely reserved step file

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
- complete `OS.HostTests` development runs
- cumulative M01/M02 `scripts/build-efi-shell.ps1` publish with M02-007 enabled
- default and negative QEMU dry-run policy cases
- complete solution, naming, diff, ownership, exact-head, and real-QEMU gates
  recorded below when the review head is frozen

## Evidence

- EFI closure: exactly 56 compile items and 69 source-authority inputs.
- Development NativeAOT publish: `TEST_PASS efi.artifact.verify`; 506,880-byte
  `BOOTX64.EFI`; SHA-256
  `88B31B1DEB6B4C1DF3A1FCFDFDCA8BC23CC152039D4D9A767B445672D97861F1`.
- The artifact remains PE32+ AMD64, EFI subsystem 10, with ordinary, bound,
  IAT, and delay imports absent.
- Corrected development QEMU/OVMF: `SERIAL_MARKER_STREAM status=Passed
  terminal=Success bytes=3735 lines=77 invalidBytes=15 invalidLines=1
  orderedSeen=55 outOfOrder=0 uniqueSeen=55 duplicateUnique=0`;
  `QEMU_SMOKE_STATUS pass`; no missing, out-of-order, duplicate, or forbidden
  marker; `FAIL_REASON none`.
- Development EFI/raw/serial/summary SHA-256:
  `88B31B1DEB6B4C1DF3A1FCFDFDCA8BC23CC152039D4D9A767B445672D97861F1`,
  `082BE0DB6F5C5B8DF4B610B302364E444824138306F00A9AAA7125666DC11F24`,
  `3638F55E4E556A497AE9FBC3DAF6366293DD93D99D58344F8223662B0CFEE06D`,
  and `EA9BA295A9D0B6A2584F17832EEFCE947D3A8971BCF255EEC940232CB38FF137`.
- Exact final-head host, policy, artifact, image, QEMU serial/summary/stdout/
  stderr, and read-only-path hashes are preserved in Gitea issues #47 and #460.

## Failures

- The first development host run failed the established M01-025 source firewall
  because the initial integration used two `stackalloc` expressions inside
  `M01StageThreeAdapter`. The older test was not changed. M02-007 now uses one
  zero-initialized local fixed-buffer struct containing exactly 104 TSS bytes
  and 40 GDT bytes; NativeAOT publication then passed.
- The first corrected NativeAOT publication reached the verifier but failed
  because its bounded parameter decoder did not classify the new switch value
  as Boolean. The decoder allowlist and source-bound integration test were
  corrected; the next artifact verification passed.
- A direct development build-script invocation without the three M01 switches
  correctly failed the preflight because M02 safe-stub execution depends on the
  accepted loaded-image-stack, live-allocator, and owned-paging profiles. The
  documented QEMU wrapper composes those prerequisites automatically.
- The first real-QEMU attempt executed the encoder and emitted the complete
  marker, but the generic comma-separated user-input normalizer split the
  internal selector payload into bogus marker entries. The runner correctly
  timed out and cleaned up QEMU. Internal profile composition now uses a
  no-split, ordinal-deduplicating merge while CLI-supplied marker lists retain
  their compatibility parser; the next real run passed 55/55 markers.

## Deferred work

- `lgdt`, `ltr`, segment-register changes, active GDT/TSS installation, IDT/IST,
  and controlled fault/panic execution remain later M02 work.
- Real `ExitBootServices`, post-firmware serial ownership, hardware/provider
  execution, hosted .NET, Minimal API, Kestrel, cloud readiness, and production
  readiness remain unproved.

## Evidence boundary

This step proves only pre-`ExitBootServices` execution of the already reviewed
M02-007 encoder into caller-owned guest memory and observation of its fixed
marker through the existing UEFI serial channel on the recorded QEMU/OVMF
platform. It does not install the table into CPU state, execute `lgdt`/`ltr`,
trigger a fault, advance a numbered completion gate, or establish any
CloudReady or production claim.
