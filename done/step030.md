# Step 030 - CPU Fault Frame Panic Formatter

## What Changed

- Added `CpuFaultFrame` as a host-testable CPU fault dump contract.
- Added `PanicFormatter.FormatFault` to emit deterministic QEMU-friendly register dumps.
- Covered vector, error-code presence, error code, RIP, RSP, RBP, RFLAGS, CR2, CR3, and general-purpose registers.
- Added host tests for page-fault and invalid-opcode style register dumps.
- Updated diagnostics and boot docs with the evidence boundary.

## Why It Changed

IDT/fault handler work needs a stable output contract before real CPU handlers exist. This step makes the expected panic dump format explicit and testable without claiming actual register capture or fault handling.

## Files Changed

- `src/OS.Kernel/Diagnostics/PanicFormatter.cs`
- `tests/OS.HostTests/DiagnosticsTests.cs`
- `docs/diagnostics.md`
- `docs/boot.md`
- `done/step030.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check -- .\src\OS.Kernel\Diagnostics .\tests\OS.HostTests\DiagnosticsTests.cs .\docs\diagnostics.md .\docs\boot.md .\done\step030.md
```

## Logs And Evidence

Expected host-test markers:

- `TEST_PASS panic formatter emits page fault register dump`
- `TEST_PASS panic formatter emits invalid opcode register dump`

## Failures

None expected after verification.

## Deferred Work

- Allocation-free fault-path writer.
- IDT/GDT/TSS/IST setup.
- Real page-fault and invalid-opcode handlers.
- Real CR2/CR3/general-register capture.
- QEMU fault probes for null pointer write and invalid opcode.

## Evidence Boundary

This step proves the register-dump data contract and host formatter only. It does not install an IDT, handle CPU exceptions, prevent triple faults, capture live registers, or prove QEMU fault diagnostics.
