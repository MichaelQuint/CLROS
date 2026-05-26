# Step 554 - Virtio-net header default-result guards

## Summary

Added explicit initialized-state gating to `VirtioNetHeaderParseResult` and
`VirtioNetReceivedFrameParseResult` so default parser results no longer satisfy
`Succeeded`.

## Why

The virtio-net header parser feeds caller-owned RX frame metadata and TX header
validation. Because the shared success enum value is zero, default result
structs could otherwise look like successful parser output before any buffer was
parsed.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetHeader.cs`
- `tests/OS.HostTests/VirtioNetHeaderTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step554.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to virtio-net header markers and `TEST_PASS host.tests`.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START drivers.virtionet.header writes zero tx header
TEST_PASS drivers.virtionet.header writes zero tx header
TEST_START drivers.virtionet.header parses rx frame metadata
TEST_PASS drivers.virtionet.header parses rx frame metadata
TEST_START drivers.virtionet.header rejects truncated header
TEST_PASS drivers.virtionet.header rejects truncated header
TEST_START drivers.virtionet.header rejects missing ethernet header
TEST_PASS drivers.virtionet.header rejects missing ethernet header
TEST_START drivers.virtionet.header rejects offload and gso
TEST_PASS drivers.virtionet.header rejects offload and gso
TEST_START drivers.virtionet.header default results are not success
TEST_PASS drivers.virtionet.header default results are not success
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures

- None.

## Deferred work

- This is host-tested caller-owned virtio-net header metadata only. It does not
  prove live virtio-net packet I/O, checksum offload, mergeable buffers,
  interrupts, DMA-safe allocation, networking readiness, cloud readiness,
  Kestrel support, or production readiness.

