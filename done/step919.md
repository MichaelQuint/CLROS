# Step 919 - Integrate the M04-003 fixed span/memory catalog into EFI and prove it in QEMU

## Summary

Integrated the reviewed M04-003 span/overlap/memory-cast/unsafe-width pointer
catalog into one default-off NativeAOT EFI profile and preserved fresh host,
same-build artifact-verifier, and real QEMU/OVMF evidence.

The returning guest profile requires M04-002, is incompatible with the
terminal M02-009 profile, executes the fixed 16-vector catalog exactly once in
caller-owned storage before memory-map capture, emits one exact guest M04-003
ready marker, and continues to the ordinary smoke-pass terminal. The accepted
profile has exactly 16 required, four ordered, two unique, and four forbidden
markers.

This step does not advance completion Gate 4.

## Why

The merged M04-003 leaf had comprehensive .NET 10 host cross-checks and a
reviewed `NOSTDLIB_EFI` pointer core, but it remained host-only. Issue #476 was
opened as the serialized central integration needed to prove that exact core
could be linked into the explicit no-stdlib NativeAOT EFI closure and executed
under real QEMU/OVMF without weakening the default profile or claiming a
general BCL/runtime surface.

## Files changed

- `src/OS/OS.csproj`
  - Adds the explicit M04-003 source link, default-false typed property, exact
    symbol, M04-002 dependency, and M02-009 terminal-profile exclusion.
- `src/OS/Boot/EfiEntry.cs`
  - Copies the fixed catalog once, executes it once with fixed caller-owned
    buffers, audits all 16 vectors/records and raw values, and emits the exact
    success or failure record before memory-map capture.
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
  - Carry and verify the typed switch, 59-item project closure, 72-input source
    authority, exact 16/4/2/4 guest contract, dependencies, and collision guards.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`
  - Pin the project/source closure, default property state, EFI runtime shape,
    exact markers, dependency failures, hostile verifier mutations, and prior
    profile compatibility.
- `README.md`
- `docs/efi-build.md`
- `docs/qemu.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
  - Replace pre-evidence wording with the measured build/guest tuple while
    preserving Gate 4 as `Fail`.
- `done/step919.md`
  - Preserves this evidence ledger.

## Tests run

- At exact implementation/evidence head
  `dedb55c3ddb155336ef4c7f387cf14b533c37f09`:
  - `dotnet build .\CLROS.slnx --nologo -m:1`
    - Exit `0`; zero warnings and zero errors.
  - `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
    - Exit `0`; zero warnings and zero errors.
  - Temporary ignored focused verifier runner over the production
    `EfiArtifactVerificationScriptTests.cs` module:
    - 19 `TEST_START`, 20 `TEST_PASS`, zero `TEST_FAIL`.
    - Duration 851.972 seconds.
    - Log:
      `artifacts/logs/issue-476-m04-003-focused-artifact-verifier-third.log`.
    - Log SHA-256:
      `3A99F2F331DC57E2418F9B6463731081572C33F8F81DB7E6BCCD322FE3062191`.
  - `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
    - Exit `0` after 2,398.431 seconds.
    - 3,469 `TEST_START`, 3,470 `TEST_PASS`, zero `TEST_FAIL`.
    - Exactly one terminal `TEST_PASS host.tests`.
    - Exactly one complete host M04-003 marker with
      `hostConformance=true guestIntegrated=false gate4=Fail`.
  - Real QEMU/OVMF command:

    ```powershell
    .\scripts\run-qemu-smoke.ps1 `
      -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
      -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
      -TimeoutSeconds 30 `
      -WorkloadProfile SingleMinimalApi `
      -DiskFormat raw `
      -BuildEfi `
      -EnableM04IndependentEndianConformanceProbe `
      -EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe `
      -EfiArtifact .\artifacts\efi\BOOTX64-issue-476-m04-003-dedb55c3ddb155336ef4c7f387cf14b533c37f09.EFI `
      -Image .\artifacts\images\issue-476-m04-003-dedb55c3ddb155336ef4c7f387cf14b533c37f09.raw `
      -SerialLog .\artifacts\logs\issue-476-m04-003-dedb55c3ddb155336ef4c7f387cf14b533c37f09-qemu-serial.log `
      -SummaryLog .\artifacts\logs\issue-476-m04-003-dedb55c3ddb155336ef4c7f387cf14b533c37f09-qemu-summary.log
    ```

    - Outer exit `0` after 46.842 seconds.
    - Same-build NativeAOT publish, artifact verification, raw-image build, and
      QEMU smoke all emitted their terminal pass records.
    - `git diff --check` and `git status --short` were clean at the tested
      implementation head before evidence generation.
- After adding only evidence documentation and this done step:
  - `dotnet build .\CLROS.slnx --nologo -m:1`
    - Exit `0`; zero warnings and zero errors.
  - `.\scripts\check-naming.ps1`
    - `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
  - `.\scripts\run-qemu-smoke.ps1 -DryRun`
    - Exit `0`; default 12-required, zero-ordered, zero-unique profile retained.
  - `.\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM04IndependentEndianConformanceProbe -EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe`
    - Exit `0`; exact 16-required/four-ordered/two-unique/four-forbidden
      profile retained.
  - `git diff --check`
    - Exit `0`.

## Evidence

### Source identity

- Canonical base/sole parent:
  `d41a2d39b0e63e893f542a673c6b8eb6a59f18f7`.
- Tested implementation head:
  `dedb55c3ddb155336ef4c7f387cf14b533c37f09`.
- Tested tree: `3ce99d70fe4607e5ed5858e86863ed49d0e47381`.
- Stable patch ID: `e3a598e27fd00a28964dd1b3c509f1a4750426a9`.
- Exactly one implementation commit over the base and 20 implementation/
  pre-evidence-documentation paths.

The provisional identity named when #476 was first clarified was superseded by
the hardened leaf merged through PR #485 before this integration was built.
The accepted integration consumes the final canonical leaf identities:

- Leaf head: `4aba409f5f6a65e2c0a7e0ae4b0562a30791f68a`.
- Leaf merge: `453a8cb88624d2ef8b8965f995eb7e466c08574a`.
- Leaf tree: `739190c55986ff3e626439c555ed662939ef7f8a`.
- Leaf combined patch ID: `377f9923e6e06bcced213950b7f9c1d9872e4913`.
- Production blob `39e8e21ff7d00e939ee5c125fb80eeae7de2fe08`,
  SHA-256
  `0C818F89ECC761A4E5692CE3970E98D577381B98BBE540F6EC5F154130C12153`.
- Host-test blob `760d3f326f0d7189aae607834b4c6b02679aad74`,
  SHA-256
  `6EBBC0F3C1ED86C5E93377F0BD24D9FC7E707D7881D7CA8D483A52EE30867B1F`.

The post-evidence documentation and this step are intentionally layered after
the tested implementation commit. A later documentation-only branch head is
not claimed as QEMU-tested; the evidence source identity remains the exact head
and tree above.

### Complete host run

- Log:
  `artifacts/logs/issue-476-m04-003-dedb55c3ddb155336ef4c7f387cf14b533c37f09-hosttests.log`.
- 445,759 bytes; 6,995 lines.
- SHA-256:
  `66A36EB56F81013C53EF9CB88CF00913DDEED51DA169675DC5EDAEF553D97F80`.
- Exit `0`; 3,469 starts; 3,470 passes; zero failures; exactly one final
  `TEST_PASS host.tests`.

### Same-build EFI closure

The schema-three link evidence was generated at
`2026-08-25T21:22:43.3827991Z` and records:

- Configuration `Release`; RID `win-x64`; .NET SDK `10.0.302`.
- Exactly 59 compile items with compile-key SHA-256
  `9E424BEFDB74433B2B179AEFC15996E693D835099C196B2E8DC3716C5D7C4F6D`.
- Exactly 72 source-authority inputs with source-closure SHA-256
  `6CD53117395FFE7668E26A8FD9AB30F6F9154AA8A7911A5B99B92E89F493599A`.
- Zero project references and zero ordinary references.
- M04-002 and M04-003 properties `true`; terminal M02-009 property `false`.
- MSVC linker `14.51.36256.0`; Windows SDK `10.0.28000.0`;
  ILCompiler/runtime pack `10.0.8`.
- PE32+ x64, EFI subsystem 10, same-build entry/serializer bindings, and no
  ordinary, bound, IAT, or delay imports.

Artifact ledger:

| Artifact | Bytes | Lines | SHA-256 |
| --- | ---: | ---: | --- |
| EFI | 82,944 | n/a | `0F8A7E3FB5D626E7017D2839DA0E3C966D86A53E769B9D39A3D0275F974A550B` |
| Link map | 45,149 | 454 | `7656D08CBCC932CEFC35303B19DEC981D437DD6FCC70FECA92084F8C2DAED2B3` |
| Link response | 500 | 15 | `C2A083629F7DC030ECBCA2347A6B4E8AD8C505CE107B233F078814B793C50E24` |
| Link-evidence JSON | 398,199 | 5,149 | `7136A51B7199AC0B0EBB76E0F6908FB09869EBD6E1584EDDA1E8C521A770FA2B` |
| Serializer object | 336 | n/a | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| Archived EFI build log | 4,734 | 39 | `31D76FACE1A612B7CEC24519C55BC9A89937023995CD5A9B0A9ADAAA4DDFAC70` |

### Real QEMU/OVMF tuple

- Wrapper interval: `2026-08-25T21:22:18.1468401Z` through
  `2026-08-25T21:23:04.9883486Z`; exit `0`.
- QEMU `11.0.50` (`v11.0.0-12631-g54e84cdc7a`):
  - 25,272,968 bytes.
  - SHA-256
    `B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`.
- OVMF:
  - 3,653,632 bytes.
  - SHA-256
    `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
- Raw image:
  - 67,108,864 bytes.
  - SHA-256
    `C8D06743C1DC96B2A2593A93CE7F7E6F59820AFA463EF8CC287B5E977E61FEB6`.
- Serial log:
  - 8,371 bytes; 28 lines.
  - SHA-256
    `65E677830952D0C1657ECF6F038BA1441AC1CC30F556185502BDE7FE53EDDDDC`.
- Summary log:
  - 4,508 bytes; 48 lines.
  - SHA-256
    `E54F101CA318F9A1A1BD26C3785CD689380B3ADC35DF2DB6D5E71FCD258AA487`.
- Stdout log:
  - 14,091 bytes; 107 lines.
  - SHA-256
    `30E9518695F4C928CBF7591203C180E178035B581EF3591F45D2876CCF924F52`.
- Stderr log:
  - Zero bytes and zero lines.
  - SHA-256
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

The serial state machine reported:

```text
SERIAL_MARKER_STREAM status=Passed terminal=Success bytes=8371 lines=28 invalidBytes=207 invalidLines=2 orderedSeen=4 outOfOrder=0 uniqueSeen=2 duplicateUnique=0
SERIAL_MARKER_DRAIN milliseconds=750
QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1
```

The required ordered subsequence appeared exactly once and in this order:

```text
EFI_CONSOLE_READY
CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4 fingerprint=F03FF59D36F39ECD pointerWidth=8 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
UEFI_MEMORY_MAP_READY
```

Independent exact-line parsing found one M04-002 marker, one M04-003 marker,
and one `OS_SMOKE_PASS`. It found zero `OS_SMOKE_FAIL`, zero `TEST_FAIL`, zero
`M04_ENDIAN_CONFORMANCE_FAILED`, and zero
`M04_SPAN_MEMORY_CONFORMANCE_FAILED`. The summary records:

```text
QEMU_SMOKE_STATUS pass
MISSING_MARKERS none
OUT_OF_ORDER_MARKERS none
DUPLICATE_UNIQUE_MARKERS none
FORBIDDEN_MARKER_HIT none
FAIL_REASON none
```

## Failures

- Rejected full run at head
  `3b4ea97446a5be1659047716e0e25160582cd0f1` exposed stale artifact-verifier
  source-authority expectations (`71` instead of the actual `72`):
  - 662 starts, 644 passes, 18 failures, no terminal host pass.
  - Log SHA-256
    `6E4666FFD3E7A9B9B4A20D757E609C16541B122DDEBD1F3DF0727D2583CA5A6B`.
  - This run is preserved but excluded from acceptance evidence.
- Rejected full run at head
  `4349a0e74942dddd25f186819a48e4be05762897` exposed a second stale fixture
  count (`58` compile items instead of the actual `59`):
  - Stopped after 656 starts, 652 passes, and three failures; no terminal host
    pass.
  - Log SHA-256
    `DB2A545880B967D2674DC07E4580B4D6050FDA469DBAE2A30E1BF51121DE278F`.
  - This run is preserved but excluded from acceptance evidence.
- The repaired verifier added hostile cases for a missing M04-003 post-restore
  property and for that property being enabled without the switch. The focused
  verifier and complete exact-head suite both passed those cases.
- The accepted real-QEMU run had no failed attempt and was not retried.

## Deferred work

- Completion Gate 4 remains `Fail`; general `Span<T>`, `MemoryMarshal`,
  `Unsafe`, and `BinaryPrimitives` behavior still needs an explicitly supported
  first-party surface and broader guest/runtime evidence.
- Allocation, GC/root scanning, exceptions, metadata, interface dispatch,
  cctor execution, threading, and the remaining NativeAOT helper ledger are not
  implemented by this profile.
- Real successful `ExitBootServices`, owned post-firmware diagnostics,
  physical hardware/provider behavior, hosted .NET 10, Minimal API/Kestrel,
  CloudReady status, security hardening, and production readiness remain
  unproved.
- The reviewed PR/merge and Gitea evidence transactions occur after this
  immutable source/evidence record. Supplemental #476 must close manually only
  after exact-head approval and merge; parent #72 must close through a separate
  manifest-only transaction before M04-004 can become ready.
- M02-010 guest materialization issue #487 remains blocked on this central
  EFI/build/verifier/QEMU lane until #476 is merged and releases it.

## Evidence boundary

This step proves only one default-off, fixed 16-vector catalog execution in the
no-stdlib EFI guest on the recorded QEMU/OVMF platform while firmware services
remained available. It proves that the reviewed `NOSTDLIB_EFI` pointer core can
be compiled into this explicit closure, called once with fixed caller-owned
buffers, and produce the audited results before memory-map capture.

It does not provide a canonical `System.Span<T>`, `MemoryMarshal`, `Unsafe`, or
`BinaryPrimitives` implementation; arbitrary-input or general memory-safety
coverage; allocation, GC, exceptions, metadata, dispatch, or broader runtime
compatibility; real successful `ExitBootServices`; post-firmware execution;
hosted .NET 10, Minimal API, or Kestrel; physical/provider support; CloudReady
status; security hardening; production readiness; or Gate 4 advancement.
