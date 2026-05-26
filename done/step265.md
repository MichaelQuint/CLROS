# Step 265 - AWS IMDSv2 Request And Metadata Policy Fixtures

## What Changed

- Added `AwsImdsV2RequestPlanner`, a host-tested EC2 IMDSv2 request metadata
  planner for token, metadata, and user-data request shapes.
- Added `AwsImdsAppMetadataPolicyPlanner`, a default-deny app metadata access
  policy fixture that requires supervisor mediation and keeps IAM role
  credentials disabled by default.
- Added host tests for token planning, token-backed metadata and user-data
  request planning, unsafe metadata path rejection, default-deny app behavior,
  and supervisor-mediated access.
- Updated cloud import, provider traceability, networking, security, and app
  model docs to record the fixture-only evidence boundary.

## Why It Changed

Issue #265 needed a small child slice under the blocked EC2 IMDSv2 parent. The
first AWS boot path can use a baked AMI without IMDS, but future Minimal API and
multi-app scenarios need a deterministic metadata access policy before any app
can reach instance metadata. This step captures request and policy contracts
without live networking or credentials.

## Files Changed

- `src/OS.Kernel/Cloud/AwsImdsV2RequestPlan.cs`
- `tests/OS.HostTests/AwsImdsV2RequestPlanTests.cs`
- `docs/cloud-import.md`
- `docs/cloud-provider-requirements.md`
- `docs/networking.md`
- `docs/security.md`
- `docs/app-model.md`
- `done/step265.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Logs And Evidence

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed
with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`dotnet build .\OS.slnx --nologo -m:1` passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
passed and ended with:

```text
TEST_PASS cloud.aws.imds plans token request
TEST_PASS cloud.aws.imds plans metadata get with token
TEST_PASS cloud.aws.imds rejects unsafe metadata request
TEST_PASS cloud.aws.imds denies app by default
TEST_PASS cloud.aws.imds allows supervisor mediated request
TEST_PASS host.tests
```

`.\scripts\check-naming.ps1` passed with:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

`git diff --check` exited `0`. It printed only line-ending normalization
warnings for existing dirty files.

## Failures

No current verification failures for this fixture slice.

## Evidence Boundary

This step is request metadata and policy fixture work only:

- no live HTTP calls;
- no IMDSv2 token retrieval;
- no metadata or user-data retrieval;
- no IAM credential retrieval;
- no credential cache or refresh;
- no CloudWatch, SSM, or cloud-init behavior;
- no app supervisor enforcement;
- no socket firewall or process isolation;
- no AWS provider boot or EC2 serial evidence.

## Deferred Work

- Wire metadata policy decisions into a real app supervisor once process or
  workload isolation exists.
- Wire request plans into the live HTTP/TCP path only after NIC-backed
  networking and route selection exist.
- Add explicit IAM credential handling as a separate issue with cache, refresh,
  redaction, and per-app allowlist behavior.
