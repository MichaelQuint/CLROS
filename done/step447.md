# Step 447 - Virtio-blk device config result default guards

## Summary

Implemented Gitea issue #432 by making the virtio-blk device-configuration
parse and fake register-window read results reject default/zero values as
successful config evidence.

## Why

`VirtioBlockDeviceConfigStatus.Success` and
`VirtioBlockDeviceConfigRegisterWindowReadStatus.Success` are zero. Their
result structs previously reported success for default values because
`Succeeded` only checked status. Device descriptor catalog and future virtio-blk
binding paths must not treat zero/default capacity or fake register-window
metadata as initialized device configuration evidence.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockDeviceConfig.cs`
- `tests/OS.HostTests/StorageVirtioBlockDeviceConfigTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step447.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.config default result is not success|storage\.virtioblk\.config\.windowexec default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Virtio-blk device-config default-result marker evidence:

```text
TEST_START storage.virtioblk.config default result is not success
TEST_PASS storage.virtioblk.config default result is not success
TEST_START storage.virtioblk.config.windowexec default result is not success
TEST_PASS storage.virtioblk.config.windowexec default result is not success
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming and source firewall guard emitted:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

## Failures

- None.

## Deferred work

This is host-tested virtio-blk device-configuration parsing and fake
register-window evidence only. It does not read live device registers, map BARs,
configure queues, notify hardware, perform DMA or block I/O, read disks, host
.NET, run Kestrel, or prove cloud/production readiness.

