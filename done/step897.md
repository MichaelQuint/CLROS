# Step 897 - Integrate the M02 safe-stub page into EFI

## Summary

The explicit M02 probe now builds one no-stdlib-safe x64 return stub into a
caller-owned page, publishes exactly that page from RW to RX, executes only the
offset-zero stub, validates its return cookie, restores firmware paging state,
and emits stable serial evidence. The default EFI path remains unchanged and
the probe is available only through `-EnableM02SafeStubProbe`.

## Why

M02 needed a smallest live bridge from host-tested executable-page planning to
the real EFI artifact. A bounded returning stub establishes the page ownership,
publication, invocation, observation, and restoration seams before any
privileged port-I/O or descriptor-table instruction is permitted.

## Files changed

- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/Boot/M02SafeStubPageIntegration.cs`
- `src/OS/OS.csproj`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/diagnostics.md`
- `docs/efi-build.md`
- `docs/qemu.md`
- `done/step897.md`

The verifier-only changes are recorded separately in `done/step896.md`.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `.\scripts\build-efi-shell.ps1`
- `.\scripts\test-efi-artifact.ps1`
- M02 all-switch dry run with `-EnableM02SafeStubProbe`
- Real QEMU/OVMF smoke with `-BuildEfi`, all three M01 switches, and
  `-EnableM02SafeStubProbe`
- `git diff --check` and clean source-state checks before live execution

## Evidence

- Accepted implementation commit:
  `cf8ce63c37a9c62a0f8f736adda14b6bd3e19a9a`; tree
  `d18f520cee19aa0c89dfb41f326c92afd0284c02`; upstream exact; source dirty
  `false` before the evidence run.
- Evidence root:
  `artifacts/logs/m02-017-final/cf8ce63c-20260803T211323Z-r1`.
- Solution build passed with empty stderr; stdout SHA-256:
  `AF20FCDA9524FAE578818F3DC65F32D05D9BFC06D58E2B0A2089994BBC8108A2`.
- Host-test build passed with empty stderr; stdout SHA-256:
  `754BEBA242CA89886D03A91827FF2D09FABB241A39302DE06829D6296F245588`.
- The complete host run recorded 3,346 `TEST_START` lines, 3,347
  `TEST_PASS` lines including exactly one terminal `TEST_PASS host.tests`, no
  `TEST_FAIL`, and empty stderr. It includes exact `TEST_PASS m01.035` and
  `TEST_PASS m02.017` markers. Stdout SHA-256:
  `C293C89AA5A48675E78380B571935B8F3EAC3E4F6A0E4F29D019F77B8E2911BB`.
- The M02 dry run bound 48 ordered unique required markers and 34 forbidden
  markers.
- The real QEMU/OVMF command began at
  `2026-08-03T21:57:26.3487831Z`, ended at
  `2026-08-03T21:58:36.8814446Z`, and exited 0 with empty stderr. QEMU stdout
  SHA-256:
  `31890CB0D67AAE93CBE65F279977ADE2D4F2764833151F1D01B1D926F68D55D3`.
- The run recorded `TEST_PASS qemu.smoke` and
  `TEST_PASS m01.026.raw-observations`; all 48 required markers occurred once
  and in order; all 34 forbidden markers were absent; and the summary recorded
  `MISSING_MARKERS none`, `OUT_OF_ORDER_MARKERS none`,
  `DUPLICATE_UNIQUE_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
  `FAIL_REASON none`.
- The 44-key raw sidecar recorded terminal withholding, one candidate,
  retained-memory exact equality, and authorized terminal release. It bound CR0
  `0x80010033`, firmware CR3 `0xF801000`, owned CR3 `0x100000`, CR4 `0x668`,
  EFER `0xD00`, physical-address width 40, and RFLAGS `0x246`, with exact
  firmware CR3/CR4/RFLAGS before/after/runtime equality.
- Raw-image SHA-256:
  `C8E8F0DE57D7E7AB1A4A606882CB05A77DD92915FEB99B7C0D35A5D37D377823`;
  EFI SHA-256:
  `D30B508AC06F592756C36ABD2B5CFAE2469ED31DF234BFDCD37134A7DBEB4EA5`;
  serial SHA-256:
  `6F223E27E4180855DA64F4785C13B9B44649F0C8EF1590AA9B65A89CEF508AD0`;
  summary SHA-256:
  `B934A1E5C2E2EEB095BDE1EE374A0B129DE99CE203AAA7F992E1F0CAC8137B0C`;
  raw-observation SHA-256:
  `D764684446C21497EE3816FF170F46E2B73F8228B429242B11BDAB47BCA16EF5`;
  QMP-transcript SHA-256:
  `9C2C8EBC64BCA601CE6CDB2D231DA784EE9555B546FB7426A46A433B71353965`.
- The decoded QMP transcript contains 20 requests, including 16 `pmemsave`
  requests and one `stop` request.
- The same-build artifact verification passed after QEMU: PE32+ x64 EFI
  subsystem 10, no imports, source-bound entry symbol, 843 exception records,
  643 distinct unwind records, and exact serializer identity.

## Failures

The frozen pre-reboot evidence run from commit
`84c5508f` was rejected: it ended without terminal host evidence and exposed one
inherited `QemuRunnerTests` assumption that recognized only M01 raw-observation
keys. The owned C# test now recognizes M01 and M02 keys while retaining the M01
assertions and separately pinning the M02 assignment. A focused rerun passed
153/153 tests with stdout SHA-256
`FCA4F08A253698A9F6CDA5645949C4DCB550BFBC68136BC8BCBE3E39ED327DB7`;
the complete accepted run is the package above.

## Deferred work

The proof is limited to the offset-zero returning cookie stub.
`portIoExecuted=false`; offsets 16 and 32 were never targeted; and no
`OUT8`, `IN8`, `PORT_IO`, or `PRIVILEGED` execution marker occurred. M02-003
must separately implement and prove the next privileged-instruction boundary.
This step does not prove descriptor installation, fault handlers, owned
post-firmware serial, real `ExitBootServices`, permanent kernel paging, SMP or
TLB shootdown behavior, systemwide W^X, physical hardware, cloud-provider
support, hosted .NET, Kestrel, CloudReady status, or production readiness.
