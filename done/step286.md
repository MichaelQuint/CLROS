# Step 286 - No-Stdlib EFI Console Adapter Boundary

## What Changed

- Added `src/OS/Boot/EfiConsoleAdapter.cs` as a no-stdlib byte-output adapter
  over the current UEFI console and Serial I/O fallback pointers.
- Routed `EfiEntry` byte writes and CR/LF writes through the adapter.
- Added the adapter to the explicit no-stdlib EFI project compile list.
- Updated boot, EFI build, and completion-gate docs.

## Why It Changed

The live EFI artifact needs a firmware-output boundary before UEFI services are
wrapped more broadly. This step keeps the current marker flow intact while
moving direct protocol byte writes behind a small no-stdlib adapter that does
not use managed interface dispatch.

## Files Changed

- `src/OS/Boot/EfiConsoleAdapter.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `docs/boot.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- `done/step286.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,EFI_CONSOLE_READY
```

## Logs And Evidence

The solution build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The focused host-test run emitted:

```text
TEST_PASS platform.console allows firmware console before UEFI gone
TEST_PASS platform.console rejects firmware console in UEFI-gone simulation
TEST_PASS platform.console rejects firmware console after ExitBootServices
TEST_PASS platform.console rejects invalid marker input
TEST_PASS platform.console reports sink unavailable
TEST_PASS host.tests
```

EFI publish emitted:

```text
TEST_PASS efi.shell.publish
EFI_ARTIFACT artifacts/efi/BOOTX64.EFI
```

Real QEMU smoke emitted:

```text
REQUIRED_MARKERS OS_SMOKE_PASS;EFI_CONSOLE_READY
TEST_PASS qemu.smoke
```

## Failures

No failures were observed for this slice.

## Evidence Boundary

This proves the live EFI artifact can build and boot after routing byte writes
through a no-stdlib adapter. It does not implement the full managed
`IPlatformConsole`, does not prove owned COM1 output after `ExitBootServices`,
and does not change the boot marker order.

## Deferred Work

- Expand UEFI-gone simulation through platform adapters.
- Continue extracting UEFI service calls behind no-stdlib-safe helpers.
- Implement owned serial output before real `ExitBootServices`.
