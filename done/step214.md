# Step 214 - Hyper-V CPUID Snapshot Identification Contract

## Summary

Added a host-testable Hyper-V platform identification contract for Azure bring-up. The new code accepts caller-provided CPUID register snapshots, models the leaf 1 ECX hypervisor-present bit, decodes the 0x40000000-style vendor signature and max hypervisor leaf, requires the Hyper-V feature identification leaf snapshot when advertised, and returns status-coded results.

## Why

Issue #236 needs a deterministic Hyper-V/Azure identification boundary before live privileged CPU work exists. This step deliberately does not execute CPUID, read or write MSRs, configure hypercall pages, initialize SynIC, negotiate VMBus, or touch NetVSC/StorVSC.

## Files changed

- `src/OS.Kernel/Drivers/HyperVPlatformIdentification.cs`
- `tests/OS.HostTests/HyperVPlatformIdentificationTests.cs`
- `done/step214.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --no-dependencies --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` built `OS.Kernel` successfully before failing in unrelated dirty ImageBuilder work.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --no-dependencies --nologo -m:1` passed with 0 warnings and 0 errors, compiling the new host test file into the test binary.
- Host tests emitted:
  - `TEST_PASS drivers.hyperv identification rejects missing hypervisor bit`
  - `TEST_PASS drivers.hyperv identification rejects unsupported vendor`
  - `TEST_PASS drivers.hyperv identification rejects insufficient max leaf`
  - `TEST_PASS drivers.hyperv identification requires feature leaf snapshot`
  - `TEST_PASS drivers.hyperv identification accepts compatible snapshot`
  - `TEST_PASS host.tests`
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed existing CRLF conversion warnings for other dirty files, but no whitespace errors.
- Posted a non-closing Gitea evidence comment to issue #236: `#issuecomment-15229`.

## Failures

- Full solution build failed in `tools/OS.ImageBuilder/Program.cs` because the current dirty tree references missing `AzureVhdConverter` and `AwsAmiImportPlanner` symbols. This is outside issue #236 and was not changed in this step.

## Deferred work

- Live CPUID execution remains deferred to a privileged CPU/platform boundary.
- Hyper-V MSR access, hypercall page setup, SynIC, VMBus negotiation, NetVSC, and StorVSC remain deferred.
- No `docs/drivers.md` update was made because other agents are actively touching driver docs.
