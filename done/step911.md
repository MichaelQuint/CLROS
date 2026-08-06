# Step 911 - M02-006 panic-safe owned-COM1 EFI integration

## Summary

Linked the M02-006 allocation-free C# panic formatter/reporter into the explicit
no-stdlib EFI closure under a default-false probe, invoked its harmless readiness
emitter over the already checked owned-COM1 binding, and taught the build,
artifact verifier, QEMU runner, source-bound tests, and canonical documentation
to distinguish the direct COM1 marker from the UEFI evidence line. The current
closure is exactly 55 compile items and 68 source-authority inputs.

## Why

Managed issue #46 cannot own `EfiEntry.cs`, `OS.csproj`, central documentation,
or the QEMU/build/verifier lane. Supplemental issue #455 separately owns those
serialized shared paths so M02-006 can obtain the required real-QEMU evidence
without violating the managed manifest contract or colliding with another
issue.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `scripts/test-efi-artifact.ps1`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `docs/efi-build.md`
- `docs/qemu.md`
- `README.md`
- `done/step911.md`

The combined branch also consumes the exact #46 paths recorded in
`done/step910.md`. No backlog, synchronizer, `BootSequence.cs`, `Probes.cs`,
completion-gate, ABI, reference, app, tool, or unrelated path is owned by this
integration.

## Tests run

- Focused M02-006 host and source-bound integration harness:
  - Eleven formatter/reporter cases passed.
  - Four EFI integration cases passed.
  - `TEST_PASS m02.006.focused`.
- Default EFI preflight:
  `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -PreflightOnly`
  - `compileItems=55`, `panicSafeOwnedCom1=false`, `EFI_PREFLIGHT_OK`.
- Full flagged EFI preflight with the three M01 switches and all five cumulative
  M02 switches:
  - `compileItems=55`, `panicSafeOwnedCom1=true`, `EFI_PREFLIGHT_OK`.
- Flagged EFI publish/verifier:
  - `EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=68`.
  - `compileItems=55`.
  - `TEST_PASS efi.artifact.verify`.
  - `TEST_PASS efi.shell.publish`.
- Focused artifact-verifier regression rerun after the final-tree fixture repair:
  - 19 starts, 19 passes, zero failures, empty stderr.
  - stdout SHA-256
    `BD0C6E73E7EEE5421ED122F429921F4FA147C1B874EA539B6895E8C81CAC838A`.
- Default QEMU dry run passed without adding M02-006 markers.
- Full M02-006 QEMU dry run passed with 54 required/ordered/unique markers and
  38 forbidden markers.
- Negative dry runs rejected the three invalid profiles with:
  - `m02_panic_safe_owned_com1_probe_requires_owned_com1_ready`;
  - `m02_panic_safe_owned_com1_probe_requires_build_efi`;
  - `m02_panic_safe_owned_com1_probe_forbids_skip_image_build`.
- Fresh development real QEMU/OVMF command:

  ```powershell
  powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe -EnableM02PanicSafeOwnedCom1Probe
  ```

  - `QEMU_SMOKE_STATUS pass`.
  - `SERIAL_MARKER_STREAM status=Passed terminal=Success bytes=3476 lines=76 invalidBytes=15 invalidLines=1 orderedSeen=54 outOfOrder=0 uniqueSeen=54 duplicateUnique=0`.
  - `TEST_PASS qemu.smoke` and `TEST_PASS m01.026.raw-observations`.
  - `MISSING_MARKERS none`, `OUT_OF_ORDER_MARKERS none`,
    `DUPLICATE_UNIQUE_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
    `FAIL_REASON none`.

The complete solution/HostTests/policy/EFI/QEMU gate is repeated on the final
staged tree before commit/review. Its final log hashes bind the pushed review
head in Gitea; the development hashes below remain the durable step evidence.

## Evidence

- Compile-closure SHA-256:
  `5E04CF9C3F932FB0F794394CC69389F9B0EEF85766368C39D7DEE46069D36337`.
- Canonical `OS.csproj` SHA-256:
  `83719D2344EFED633C9CA133339EBE4091C1E6CCA07E75491996CA42E539CC0`.
- Default preflight log SHA-256:
  `0893A1EE7A230F1D0BD958AAE120B39CB4A1D24D7B0C302C2D0D520479D2910E`.
- Flagged preflight log SHA-256:
  `D97E7E78904403A711F8BFC5A3A70DF11C8B1823B531A7D8B5F5E9AE2F5D4F13`.
- Accepted development publish stdout SHA-256:
  `E596218F9169994A1A3E612146A4EFB1F0BED5DAB425EE83DA424A9A5F08118C`;
  publish log SHA-256:
  `1FE9FF18C6F9901A898B0C3FA44628C295C53EE8A9FBFC529D16665B94E81355`.
- EFI: 500,736 bytes, SHA-256
  `004006C004248406E7EF504F7CE5CEC0A04E8340EB6BF4168C316780008617C9`.
- Link-map SHA-256:
  `4769B88DCE30B7B4C5584AE9F83654DB342563E47AB8D6AEA33E8BE52F48EE81`.
- Link-response SHA-256:
  `B6408AAE4E74BE851D060DBE88F525C992C5D4390F63B0F8F1F7C66572F3F211`.
- Serializer-object SHA-256:
  `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.
- Development raw image SHA-256:
  `75C19BDCFD9AD3DDFE60E1C145C749FE559E191647743A3DC68C86F761F5D31F`.
- Development serial SHA-256:
  `0EFD3387E10CE92FD22A5980EC6857580730DAD73FBAC9987AEFA56BF66E6EE4`.
- Development QEMU summary SHA-256:
  `977346FBF91070993835B434B3FFEBA47DCE2771F946FB80F3C1AAACDFF0EF9D`.
- Development runner stdout: 25,254 bytes, SHA-256
  `E35A685349FA3AB0DE2A736335F177307C566BBD118EFC7D80824EB64136F5F9`.
- Development runner stderr: 0 bytes, SHA-256
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Raw-observation SHA-256:
  `44A6968B0145DAED99C9B3457E5EC111E6C42C11D6DB79A2378B018C04924CB2`;
  QMP-transcript SHA-256:
  `22AAAD84ABACD673A9A7332736A66E7FFEBADA2F4E3F04D69921D96E0168F17E`.
- The direct ready marker and the distinct evidence marker each occur exactly
  once in serial. `M02_PANIC_SAFE_OWNED_COM1_FAILED` and `TEST_FAIL` each occur
  zero times.

## Failures

- The first flagged EFI publish rejected host `Span` use in the no-stdlib
  closure. The implementation was corrected to an unsafe pointer/length core.
- The next publish rejected
  `post_restore_evaluation_item_count_mismatch compile=55 project=0 reference=0 analyzer=2`
  because the exact artifact-verifier source array still omitted the new
  reporter. The owned verifier was corrected to list it after the M02-005
  writer; the next publish passed.
- The first negative-test wrapper used a two-argument PowerShell `.Contains`
  overload unavailable on this host. The wrapper was corrected to use
  `.IndexOf`; all three product-level negative cases then passed.
- The outer development `Start-Process -Wait` capture remained attached to
  reusable MSBuild descendant nodes after the nested script and QEMU had emitted
  terminal success. It was interrupted only after confirming there was no QEMU
  or child script process. This development tuple is not substituted for the
  required final direct process-exit capture.
- The first complete staged-tree gate at tree
  `32ccd69fce5d5e1b809cbbef148be8df64ec5284` was stopped immediately after one
  real regression marker: the positive artifact-verifier fixture failed with
  `post_restore_evidence_property_set_invalid`. The production verifier had
  correctly added the M02-006 post-restore switch, but its owned valid-evidence
  fixture omitted that property. The fixture now records
  `EnableM02PanicSafeOwnedCom1Probe=false` and includes missing/enabled-without-
  switch mutations. The focused 19-case verifier module then passed completely;
  the rejected partial full-host log is not acceptance evidence.
- The next complete staged-tree gate at tree
  `2c2ec5f76a8deeda9a8578e91fea8990dc5468af` was stopped at the M02-005 EFI
  integration group after `TEST_FAIL m02.005 EFI closure links one default-off
  owned writer`. Its source-bound test counted every project error condition
  mentioning `EnableM02OwnedCom1ReadyProbe`; the legitimate new M02-006
  dependency made that stale selector count two conditions. The owned consumer
  now selects only the M02-005 switch's own default-off dependency condition,
  while the M02-006 integration test retains authority for the new downstream
  dependency. The rejected partial stdout log has SHA-256
  `A5DAF8B70F312C216FA22B7640521CBBEBBA60EE46695FE5B4A6E489077AAEC7`;
  its empty stderr has SHA-256
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
  This partial run is failure evidence, not acceptance evidence.

## Deferred work

- Record the final source tree/commit and final gate hashes, post exact
  evidence to issues #46 and #455, and obtain independent `admin` approval of
  the exact combined PR head.
- Merge the combined PR, close supplemental #455, then perform the separately
  reviewed managed-manifest closure and idempotent synchronizer apply for #46.

## Evidence boundary

This step proves only harmless pre-`ExitBootServices` execution of the linked
M02-006 readiness emitter through the checked COM1 line-status read and
byte-write path on the recorded QEMU/OVMF platform. It does not prove a live
panic or fault route, IDT/GDT/TSS/IST installation, post-firmware diagnostics,
real `ExitBootServices`, runtime unwind, physical hardware/provider behavior,
hosted .NET, Minimal API, Kestrel, CloudReady status, production readiness, or
numbered completion-gate advancement.
