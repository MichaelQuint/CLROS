# Step 025 - QEMU HTTP Probe Contract

## What Changed

- Added dry-run-only HTTP probe options to `tools/OS.QemuRunner`:
  - `--http-probe-url <http-url>`
  - `--http-probe-expected-status <100-599>`
  - `--http-probe-expected-body <text>`
  - `--http-probe-timeout <seconds>`
- Added validation that accepts only absolute `http://` URLs with a host.
- Added validation that rejects HTTPS/TLS, malformed HTTP status codes, non-positive probe timeouts, and incomplete probe configuration.
- Added stable dry-run evidence:

```text
HTTP_PROBE url=<url> expected_status=<code> expected_body_bytes=<bytes> timeout_seconds=<seconds>
```

- Added stable real-mode rejection:

```text
TEST_FAIL qemu.runner.http_probe_not_implemented
```

## Why It Changed

Kestrel is a required CloudReady milestone, but the OS does not yet have hosted .NET 10, PAL sockets, TCP listen support, or in-guest Kestrel startup. This step defines the QEMU runner contract that future Kestrel smoke work can depend on without pretending that host HTTP probing or guest Kestrel support already exists.

## Files Changed

- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/kestrel.md`
- `done/step025.md`

## Tests Run

```powershell
dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project .\tools\OS.QemuRunner\OS.QemuRunner.csproj -- --dry-run --image artifacts/images/os.raw --require-marker OS_SMOKE_PASS --require-marker "TEST_PASS kestrel.smoke" --http-probe-url http://127.0.0.1:8080/healthz --http-probe-expected-status 200 --http-probe-expected-body OS_MINIMAL_API_OK --http-probe-timeout 10
git diff --check -- .\tools\OS.QemuRunner\Program.cs .\tests\OS.HostTests\QemuRunnerTests.cs .\docs\qemu.md .\docs\kestrel.md .\done\step025.md
```

## Evidence

- `dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo` passed with 0 warnings and 0 errors.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and emitted `TEST_PASS host.tests`.
- QEMU runner host tests emitted:
  - `TEST_PASS qemu dry-run emits HTTP probe contract`
  - `TEST_PASS qemu rejects HTTPS HTTP probe`
  - `TEST_PASS qemu rejects invalid HTTP probe status`
  - `TEST_PASS qemu rejects invalid HTTP probe timeout`
  - `TEST_PASS qemu rejects incomplete HTTP probe`
  - `TEST_PASS qemu real mode rejects HTTP probe execution`
- Direct dry-run emitted:

```text
TEST_START qemu.runner.dryrun
REQUIRED_MARKERS OS_SMOKE_PASS;TEST_PASS kestrel.smoke
HTTP_PROBE url=http://127.0.0.1:8080/healthz expected_status=200 expected_body_bytes=17 timeout_seconds=10
TEST_PASS qemu.runner.dryrun
```

- Direct invalid HTTPS probe emitted `TEST_FAIL qemu.runner.invalid_args HTTP probe URL must use http scheme; https/TLS is not supported.`
- Direct invalid status probe emitted `TEST_FAIL qemu.runner.invalid_args Value for --http-probe-expected-status must be an HTTP status code from 100 to 599.`
- Direct invalid timeout probe emitted `TEST_FAIL qemu.runner.invalid_args Value for --http-probe-timeout must be a positive integer.`
- Direct real-mode probe emitted `TEST_FAIL qemu.runner.http_probe_not_implemented` with process exit code 7.
- `git diff --check -- .\tools\OS.QemuRunner\Program.cs .\tests\OS.HostTests\QemuRunnerTests.cs .\docs\qemu.md .\docs\kestrel.md .\done\step025.md` passed with line-ending normalization warnings only.

## Failures

No failures remain for this slice. Real HTTP probing intentionally fails with `TEST_FAIL qemu.runner.http_probe_not_implemented`.

## Deferred Work

- Implement real host-side HTTP probing after guest networking and hosted Kestrel startup markers exist.
- Add QEMU port-forwarding or routed networking configuration needed for a host probe to reach the in-guest Kestrel endpoint.
- Add libvirt HTTP probe support after the QEMU contract is proven.

## Evidence Boundary

This step proves QEMU runner CLI parsing, validation, dry-run evidence, and real-mode rejection only. It does not prove hosted .NET 10, Kestrel, TCP, socket behavior, or in-guest HTTP request handling.
