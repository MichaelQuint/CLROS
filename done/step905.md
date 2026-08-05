# Step 905 - Bound the complete CI-smoke negative fixture

## Summary

Raised only the C# HostTests whole-process safety envelope used by the complete
negative CI-smoke fixture from 120 seconds to 300 seconds. The fixture still
requires the two injected command failures, a failed CI summary, asynchronous
stdout/stderr draining, and entire-process-tree termination on a genuine
timeout.

## Why

The prior 120-second envelope was below the measured current-host duration of
the unchanged canonical-master fixture. Canonical master completed correctly in
131482 ms, and the M02-004 candidate completed correctly in 148089 ms. The old
test killed these otherwise valid negative-path runs before it could inspect
their summaries.

## Files changed

- tests/OS.HostTests/CiSmokeScriptTests.cs
- done/step905.md

## Tests run

- dotnet build .\CLROS.slnx --nologo -m:1
  - Exit 0; 0 warnings; 0 errors.
  - Log: artifacts/logs/m02-004-443-solution-build.log
  - SHA-256: DA0DDAF611C2BE445B6824277F85E002941484BAED47D56D3ED3F6AA901EFAE6
- dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
  - Exit 0; 0 warnings; 0 errors.
  - Log: artifacts/logs/m02-004-443-hosttests-build.log
  - SHA-256: E0EC8C646B91450FF9BE9AB524D6C9145F14F3D6FD50938C9B1C392D14466F82
- Exact focused exit-42 negative fixture through scripts/run-ci-smoke.ps1
  - Natural completion in 77071 ms; process exit 1 as required.
  - Required failures: ci.smoke.build exit_code=42,
    ci.smoke.host.tests exit_code=42, and ci.smoke failures=2.
  - Summary status failed; stderr empty.
  - Log root:
    artifacts/logs/m02-004-443-focused-ci-negative-20260804-221847
  - Summary SHA-256:
    6580E6FC64F534E91B7278FD391E896720B3566F31A184703F00F5C176DD8272
  - Stdout SHA-256:
    EFE4486543CBA32EE4A60C20093BC9CAB436137B4FC668ED28F6140EC5F7AE26
  - Empty stderr SHA-256:
    E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
- git diff --check
  - Exit 0.
- scripts/check-naming.ps1
  - TEST_PASS naming.guard.
  - TEST_PASS source.firewall.
- Exact union-of-manifests ownership audit
  - 34 changed paths; zero unexpected paths, zero forbidden native-source
    extensions, and zero OS identity violations.
- Source-bound audit
  - One CiSmokeProcessTimeoutMilliseconds = 300000 declaration.
  - One WaitForExit use of the named constant.
  - One matching 300-second diagnostic and zero stale WaitForExit(120000)
    calls.

## Evidence

- Supplemental ownership issue: #443.
- Canonical-master control at
  9585bac26b18e852ddb9a6276837d205d24d635a completed in 131482 ms,
  produced the expected two failed command steps and failed summary, and had
  empty stderr.
  - Summary SHA-256:
    144FE3F8342674F64AB21B25CB4F8CA06268E733648E8ABD480E81FB7BA60534
  - Stdout SHA-256:
    03AAC03ED89CBE130DD7412DBEFC9FCBE732368159129BC112435E8F31AAB4B4
- M02-004 control at 58521e688975aba917ac944a26b79d33fe306225
  completed in 148089 ms with the same expected failure shape and empty
  stderr.
  - Summary SHA-256:
    1FB9702603FBF2513A708623C93A64DB7F473F5F70C5DDF40CF5BEA10B714B4F
  - Stdout SHA-256:
    3299C2369357EFCB916F792D19B07AEF4167255200D213947F9EA19B378C8076
- The 300-second value is a whole-process kill boundary. It does not alter a
  parser performance budget or reinterpret a failed command as a pass.

## Failures

- Rejected full HostTests attempt at
  artifacts/logs/m02-004-58521e688975-host-20260804-213017:
  447 TEST_START, 446 TEST_PASS, and one TEST_FAIL. The failure was
  ci.smoke returns nonzero on failed command with a 120-second TimeoutException;
  no terminal host.tests marker was reached.
  - Stdout SHA-256:
    C97D93CF4FB48FCDB76C1A61E971EB7A61AA9F61701381BCF7AB4A77C79FB94D
  - Stderr SHA-256:
    84EC968974893AF24FA7682ACBA39FD3D2F3535B067E2EBA0F7EB3383639F469
- The first focused timing attempt did not start because the Windows PowerShell
  ProcessStartInfo implementation lacks ArgumentList. It changed no file and
  was retried with fixed, quoted local paths.

## Deferred work

- Run the full uncompromised HostTests harness at the final pushed candidate
  head and require exit 0, zero line-leading TEST_FAIL markers, and terminal
  TEST_PASS host.tests.
- Run the complete exact-head build, EFI, dry-run, and real QEMU/OVMF merge-wave
  gates for issues #44, #441, #442, and #443.
- Post exact evidence to Gitea and request independent review before any merge,
  closure, done label, or backlog synchronization.

## Evidence boundary

This step proves only that the complete negative CI-smoke fixture has a bounded
whole-process safety envelope above the measured current-host canonical-master
duration while retaining its failure assertions and kill behavior. It does not
prove guest execution, physical hardware, provider support, hosted .NET,
Minimal API, Kestrel, CloudReady status, or production readiness.
