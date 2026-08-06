# Step 913 - Owned x64 GDT encoding and hostile host evidence

## Summary

Implements M02-007's production C# encoder for a caller-owned x64 global
descriptor table containing, in fixed order, a null descriptor, a ring-zero
64-bit code descriptor, a ring-zero data descriptor, and one available 64-bit
TSS descriptor. The always-compiled implementation is an allocation-free
pointer/length core with status-coded validation, exact 40-byte mutation,
readback verification, and FNV-1a64 fingerprinting. Normal host builds add
`Span` wrappers behind `#if !NOSTDLIB_EFI`.

Eight issue-specific host cases exercise the exact positive byte transaction,
bounded pointer ownership, full TSS base/limit encoding, failure atomicity,
readback corruption, invalid verification metadata, allocation behavior, POD
defaults, and source boundaries. The full uncompromised host suite passes. Real
QEMU/OVMF evidence remains a required separately owned integration transaction
before issue #47 can close.

## Why

The older `GdtDescriptorPlan` is a useful managed span plan, but M02-007 requires
an owned kernel-facing encoder suitable for a later no-stdlib EFI caller. The
new contract writes conventional flat long-mode code/data descriptors and a
16-byte x64 TSS descriptor into caller storage, validates every applicable
input before mutation, and immediately verifies every owned byte. It neither
loads the table nor alters the boot sequence.

## Files changed

- `src/OS.Kernel/Diagnostics/OwnedX64GDTContainingNullN64BitCode.cs`: adds the
  production pointer-first encoder, result/status PODs, canonical-address and
  TSS-limit validation, exact byte catalog, readback, and fingerprinting.
- `tests/OS.HostTests/OwnedX64GDTContainingNullN64BitCodeTests.cs`: adds eight
  deterministic hostile host cases and the `m02.007` marker group.
- `docs/diagnostics.md`: documents the owned encoder, applicable failures,
  inapplicable device-transaction cases, host marker, and exact evidence
  boundary while correcting M02-006's merged status.
- `docs/boot-order.md`: records that the encoder adds no boot phase and does not
  execute `lgdt` or `ltr`, plus the host test and pending guest-integration
  boundaries.
- `done/step913.md`: this uniquely reserved implementation/evidence record.

No EFI entry/project, `BootSequence`, `Probes`, README, completion-gate,
manifest, prior step, reference, generated artifact, first-party native source,
or other unowned path changes in this implementation lane.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1` passed on the final source tree in
  14.44 seconds with zero warnings and zero errors. The 4,206-byte captured log
  SHA-256 is
  `E08397E0CA31BEBDFF2A5A960A4070E164816DC7357CA5A59635AC2AE2732D14`.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  passed on those exact binaries with 3,425 `TEST_START` lines, 3,426
  `TEST_PASS` lines, zero `TEST_FAIL` lines, exactly one M02-007 ready marker,
  and exactly one `TEST_PASS host.tests` terminal. The 879,036-byte log SHA-256
  is `A5B12A42EBF7109C8E0EADEE39031A78DAAC2F1ADBB38516F4446FA825ACC100`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun` passed with
  `TEST_PASS qemu.runner.dryrun`. The 4,792-byte log SHA-256 is
  `7A704C2C36B90F20F2335BF56A9DC222A1DFCBB819B242BE2193934C1980E868`.
  This is preflight only and is not boot evidence.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1` passed `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`. The 210-byte log SHA-256 is
  `FB9E2F9DEB178474FD6713AA598348A053B28FC79CA27732159CDD0AC565CE18`.
- `git diff --check` passed.

## Evidence

The issue-specific terminal group in the final full host log is:

```text
TEST_START m02.007
CLROS_M02_007_READY encoding=hostValidated callerOwned=true readBack=true guestEncoding=false guestLgdt=false guestLtr=false allocationFree=true
TEST_PASS m02.007
TEST_PASS host.tests
```

The fixed representative table for TSS base `0xFFFF80000020F000` and limit
`0x67` is exactly:

```text
00 00 00 00 00 00 00 00
FF FF 00 00 00 9A AF 00
FF FF 00 00 00 92 CF 00
67 00 00 F0 20 89 00 00 00 80 FF FF 00 00 00 00
```

Its FNV-1a64 is `08350E201241EB73`; its GDTR limit metadata is 39; selectors
are null `0x00`, code `0x08`, data `0x10`, and TSS `0x18`. The positive case
mutates exactly the first 40 destination bytes, leaves the following sentinel
bytes unchanged, and verifies the full table. Failures for negative/short/null
destinations, null and noncanonical TSS bases, limits outside 103 through
`0xFFFFF`, short/null verification sources, and byte corruption are
status-coded. All preflight failures preserve caller bytes.

The read-only reference influence was
`references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs`, SHA-256
`B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500`.
Only its C#-driven deterministic byte-generation approach was adapted; no
reference code, assembly, generator dependency, or runtime exception path was
copied into first-party source.

## Failures

- The first complete host run passed, but source review afterward found an
  unnecessary `fingerprint != 0` success predicate. A mathematically valid
  encoded table must not fail solely because its FNV value is zero, so the
  redundant predicate was removed and the final solution build and complete
  host suite were rerun from the corrected exact tree. Only the final hashes
  above are acceptance evidence.
- One read-only PowerShell log-inspection command tried to assign `$host`, which
  conflicts with PowerShell's read-only `$Host` variable. It changed no file or
  tracker state; the check was rerun with `$hostLogPath`.
- The combined dry-run/naming wrapper yielded while the naming log was still
  empty. No pass was inferred from that intermediate snapshot. The named guard
  process completed, and the final nonempty hashed log contains both required
  pass terminals.

## Deferred work

- Open the exact five-path implementation PR, post the hashes and ownership
  audit to issue #47, and move it to `agent:review` only after exact-tree checks
  remain green.
- Obtain independent exact-head approval and merge this implementation before
  starting the shared EFI lane from canonical master.
- Create and claim a collision-safe supplemental issue that explicitly owns
  `src/OS/EfiEntry.cs`, `src/OS/OS.csproj`, issue-specific EFI integration
  tests, QEMU profile/script surfaces, affected canonical QEMU/EFI docs, and a
  second unique done step. Do not mutate those paths under M02-007 ownership.
- In that lane, link this exact no-stdlib pointer core behind a default-off
  switch, encode and verify a caller-owned guest table, emit one direct
  `CLROS_M02_007_READY` serial marker, and require a distinct failure marker.
- Run fresh real QEMU/OVMF with `-TimeoutSeconds 30 -RequireMarker
  "CLROS_M02_007_READY"`, preserve `guestLgdt=false` and `guestLtr=false`, then
  run the complete merge-wave gate and obtain independent exact-head approval.
- Post `<!-- clros-evidence-reviewed:M02-007 -->` only after the reviewed host
  implementation and real-QEMU integration evidence both exist. Then close
  issue #47 through `agent:review` to `agent:done`, converge the canonical
  manifest through a separately owned closure transaction if required, run the
  synchronizer twice plus preview, and claim the next smallest safe ready issue.

## Evidence boundary

This step proves only deterministic host-side encoding and verification of the
owned 40-byte GDT contract. Generic absent-device/capability, cyclic capability,
BAR, feature negotiation, queue-size, descriptor leasing, timeout, reset, and
DMA-ownership failures are inapplicable because this slice performs no device
transaction. PE import policy and hosted-runtime exception behavior are also
out of scope. The host marker explicitly records `guestEncoding=false`,
`guestLgdt=false`, and `guestLtr=false`. QEMU dry-run is not boot proof. Nothing
here proves guest execution, `lgdt`, `ltr`, TSS installation, an IDT, handlers,
controlled faults, a live panic route, post-`ExitBootServices` diagnostics,
physical hardware, provider behavior, hosted .NET, Minimal API, Kestrel,
CloudReady status, production readiness, or any numbered completion gate.
