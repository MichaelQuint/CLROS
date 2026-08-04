# Step 900 - Integrate checked port-I/O binding into the EFI profile

## Summary

Implemented supplemental issue #438's serialized shared-file lane for
M02-003. The explicit no-stdlib EFI closure now compiles the production checked
function-pointer binder and a value-only integration helper. After the accepted
M02-017 safe-offset execution audit completes and firmware paging state is
restored, the helper checks and binds the offset-16 `Out8` and offset-32 `In8`
addresses, but deliberately invokes neither target.

The default-off all-five-switch profile emits exactly:

```text
CLROS_M02_003_READY bindingReady=true out8AddressChecked=true in8AddressChecked=true guestInvocation=false portIoExecuted=false
```

The marker is ordered after `TEST_PASS m02.017` and before
`TEST_PASS m01.stage3`. The checked profile requires 49 ordered and unique
markers and forbids 35 failure/privileged-I/O markers.

## Why

M02-003 issue #43 did not own `EfiEntry.cs`, `OS.csproj`, or the shared build,
artifact, and QEMU scripts. A separate claimed collision lane was required to
obtain honest firmware-visible evidence without violating ownership or
misrepresenting safe host callback execution as live device I/O.

## Files changed

Supplemental issue #438 owns exactly these implementation-wave paths:

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/Boot/M02CheckedPortIoFunctionPointerIntegration.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `scripts/test-efi-artifact.ps1`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- this uniquely reserved `done/step900.md`

Integration begins in commit
`102e73351705f93a53bbfdde0d27a1e4532007b4`, with closure/profile corrections
through commits `779a89597e0187d586c7851d9b5818d624670b41`,
`effa24eb88fddd22ffb02e1c930f596d67bdda25`, and
`84032f4b78804194d3352410a1f2749112d94e5f`. The frozen implementation tree is
`a02bbe8b8ccf6ce7cd13b8957dd081d5476130b3`.

The issue consumes but does not own M02-003's
`InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs`, accepted M02-017 source,
or M02-003's two canonical docs. The README progress checkpoint remains a
separate user-requested commit. No backlog, synchronizer, `BootSequence.cs`,
`Probes.cs`, `std`, app, tool, completion-gate, or reference path changed.

## Tests run

The accepted frozen-source wave ran:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -Image "<unique-r4-strong-image>" -SerialLog "<unique-r4-strong-serial>" -SummaryLog "<unique-r4-strong-summary>" -BuildEfi -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -RequireMarker "CLROS_M02_003_READY"
git diff --check
git status --short
```

Focused development runners also passed:

- M02-003 binder/call suite SHA-256
  `1A50AA4F88FE1B3F2DC314FD515F9A1554FB25F38F1872C30001CA7E41D4189F`.
- EFI integration suite SHA-256
  `E3CDA07A5297E84DF086BD058D2FD08DFF71F75D62AB6DA9B59CDE689FBCF257`.
- QEMU-runner full-class suite SHA-256
  `1DD458BD3DEA754E364C649A83F6C35071AC26F0F6FC8C21CB00B3699EB815F4`.
- Corrected M02-017 prerequisite suite SHA-256
  `054C1347F3F1B34844A97C0EDAB791EB95603333CD78306B065D586ACC65DCF6`.

## Evidence

Accepted evidence root:

```text
C:\Projects\CLROS-M02-003\artifacts\logs\m02-003-final\84032f4-20260804T052701Z-r4
```

| Command | Exit | Seconds | stderr bytes | stdout SHA-256 |
|---|---:|---:|---:|---|
| solution build | 0 | 22.914 | 0 | `35FCA1A816C31B7168078D02C0F59382011EEECDCA3D3EA0FD202900C7469E9F` |
| HostTests build | 0 | 4.964 | 0 | `0C5336F5E63851A9468483C8A67E24D9A106D038889F83DDEE2707EFDE3A746A` |
| complete HostTests | 0 | 2516.192 | 0 | `ABDF130B9FD907F3B8E23C463DC409C95FC46B11FC0C3ED9784624F953848BD0` |
| naming/source firewall | 0 | 37.774 | 0 | `FB9E2F9DEB178474FD6713AA598348A053B28FC79CA27732159CDD0AC565CE18` |
| default QEMU dry-run | 0 | 2.166 | 0 | `884F7AC3833B695CBD983AAF4A19FC57E6E75403A3026A98D4B83A3DDDAD6A65` |
| checked QEMU dry-run | 0 | 2.303 | 0 | `18AE53E1E5851E39ED0E2EDF393A04607517DC9A6A908F3860E7E565F6D54F1B` |
| all-switch EFI build | 0 | 41.046 | 0 | `15A8182E8EF601C2B5D0F00CD11124F43ED63E6392C3FB8A20DBC0BB86075FCC` |
| strong real QEMU | 0 | 55.326 | 0 | `15004851A7CAEE92D299A88642D7E856C578764F7732CB21B08AF4883A6EB770` |
| exact issue QEMU command | 0 | 9.043 | 0 | `E170C48055CB2D9FBA75C72F5777AD8770C196ED7F615FE8837411E501DA10D0` |
| `git diff --check` | 0 | 0.041 | 0 | empty SHA-256 |
| clean status | 0 | 0.044 | 0 | empty SHA-256 |

Complete HostTests recorded 3,365 starts, 3,366 passes, zero failures, one
terminal host pass, and one each of the M02-003 production and EFI-integration
start/pass/ready markers.

The all-switch EFI build reported:

```text
EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=63
EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=31 prerequisites=3 compileItems=50 prerequisiteGap=UefiMemoryType
EFI_PREFLIGHT_OK
```

The produced PE32+ x64 EFI artifact is 479,744 bytes with SHA-256
`C7666C1580A1C362DB3165166B005478910C5F3FD53C4F451E1A8F4EB57CE794`.
The build verifier retained absent imports, the exact two new compile records,
and no framework/project/native-source regression.

The strong real-QEMU serial is 2,606 bytes with SHA-256
`CC1DDEBBD8C5355D9C318BADE9F7778349D4EF6949EFA1160E40A304263AADCC`.
The exact readiness marker occurred once, after `TEST_PASS m02.017` and before
`TEST_PASS m01.stage3`. `OS_SMOKE_PASS` occurred and all 35 forbidden markers,
including every M02 privileged-I/O marker and generic `TEST_FAIL`, were absent.
The strong image SHA-256 is
`02294F94ECB6DC4AC02B11D8DCD672CC09A163F151454E921CD59711B4036188`.
The raw-observation serial SHA-256 is
`248E2677E4B14A8CA223B16A0181CAFB764F4B94754447597E71BFDF76CC0ABF`.

The literal issue command then exited zero against the same EFI bytes. Its
image SHA-256 is
`822AD43F74C36AED9CE2E0D45517F07807F0A0FBCFE219F01170F9419AB027F1`,
and its public serial bytes match the strong serial hash.

## Failures

- Final attempt r1 stopped after inherited M01-020 still expected 48 compile
  items. Its partial results are rejected.
- Attempt r2 ran the full harness but failed the M02-017 selector guard because
  it counted the downstream M02-003 dependency as an M02-017 prerequisite.
  Commit `84032f4` narrowed that test to M02-017-owned conditions. Its stdout
  SHA-256 `467768923A3EF46EC58810B34FEC81313410E989CEF7505EDEFD178E2C7CC623`
  is rejected.
- Attempt r3 produced zero test failures and a terminal host pass, but the
  ignored outer helper recorded an empty exit code and wrote `HOST_GATE_FAIL`.
  Its stdout SHA-256
  `3AB9B8D1990EFFB846D2F2B44B1499385DA83C0E1A07E24B74C979806FF46E03`
  is diagnostic only. r4 directly captured native exit zero.
- No M02-003 host attempt was promoted from a timeout. The r1 process tree was
  deliberately stopped after its deterministic failure; r2 and r3 terminated.
- The first r4 strong-QEMU command used an unauthorized EFI output directory.
  The source authority rejected it before QEMU launch with
  `output_authority_path_invalid`; exit 1, empty stderr, stdout SHA-256
  `1C73D747F15B12B9185B374866564E9FF75D9307111D0165CBA329EC4183731C`.
  The accepted `08b` command retained canonical `artifacts/efi` authority and
  used unique image/serial/summary outputs.

Every rejected attempt remains preserved and contributes no acceptance proof.

## Deferred work

- Push, independent review of the exact documentation head and evidence
  package, PR merge, issue evidence comments/markers, tracker transitions, and
  backlog synchronization remain required.
- The guest does not call `InvokeOut8` or `InvokeIn8`; offsets 16 and 32 are not
  executed. This proves no live port I/O or COM1 ownership.
- Real `ExitBootServices`, post-firmware diagnostics, descriptor/fault
  installation, packet loss, runtime unwind, provider support, hosted .NET,
  Kestrel, CloudReady status, and production readiness remain unproven.

The evidence boundary is checked address/binder readiness in the no-stdlib
guest plus safe host callback invocation. It is not a privileged-device-I/O
result and changes no numbered completion gate.
