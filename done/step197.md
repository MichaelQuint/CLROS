# Step 197 - Virtio-Net Receive Lifecycle

## Summary

Implemented issue #216 by adding a host-testable virtio-net RX lifecycle slice. The new lifecycle publishes caller-owned receive buffers as device-writable split-ring descriptors and maps one used-ring completion back to the published descriptor/buffer index before handing the completed byte range to the existing no-offload RX classifier.

## Why

The network stack already had virtio-net queue metadata and no-offload receive-buffer classification, but it did not have a deterministic boundary for publishing RX descriptors or interpreting a used-ring completion. This step creates that boundary without jumping to MMIO, device notification, interrupts, DMA ownership, or live packet I/O.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveLifecycle.cs`
- `tests/OS.HostTests/VirtioNetReceiveLifecycleTests.cs`
- `docs/networking.md`
- `done/step197.md`

## Tests Run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.rx\.lifecycle|TEST_PASS host.tests|TEST_FAIL'`
- `dotnet run --project .\artifacts\issue216-focus\issue216-focus.csproj`

## Evidence

Build evidence:

- `dotnet build .\OS.slnx --nologo -m:1` succeeded.

Focused RX lifecycle evidence:

- `TEST_PASS drivers.virtionet.rx.lifecycle focus publishes`
- `TEST_PASS drivers.virtionet.rx.lifecycle focus completes`
- `TEST_PASS drivers.virtionet.rx.lifecycle focus rejects completion`

The committed host test file adds these markers for the integrated host harness:

- `TEST_PASS drivers.virtionet.rx.lifecycle publishes receive buffers`
- `TEST_PASS drivers.virtionet.rx.lifecycle wraps available ring slots`
- `TEST_PASS drivers.virtionet.rx.lifecycle completes and classifies buffer`
- `TEST_PASS drivers.virtionet.rx.lifecycle rejects short queue memory`
- `TEST_PASS drivers.virtionet.rx.lifecycle rejects descriptor exhaustion`
- `TEST_PASS drivers.virtionet.rx.lifecycle reports completion failures`

## Failures

The integrated host-test command did not complete because an unrelated, parallel-owned GPT parser test failed first:

- `TEST_FAIL storage.gpt.parser rejects entry array out of range`
- `Unhandled exception. System.InvalidOperationException: GPT partition table parser host tests failed: 1`

The GPT parser files were untracked parallel work and were not modified by this issue.

The build also emitted transient MSB3026 copy warnings because another `.NET Host` process had `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll` open during the build. The build retried and succeeded.

## Deferred Work

- Live virtio-net queue selection and MMIO register writes.
- DMA mapping and buffer ownership handoff.
- Device notification after available-ring publication.
- Interrupt or polling integration for used-ring processing.
- Multi-completion drain loops and receive-buffer recycling.
- Mergeable receive buffers.
- Checksum offload and GSO support.
- ARP/DHCP/DNS/TCP dispatch from live NIC completions.
- Hosted .NET 10, Minimal API, and Kestrel evidence.
