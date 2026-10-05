# Step 996 - Refresh the typed EBS conditional-default compatibility assertion

The complete host regression at `8e8c3f0fd4e505e38ce26faad580edc889f2d173`
failed in the existing double-fault EFI fixture: it expected 37 conditional
XML false defaults. The project has 38 conditional defaults and 41 evaluated
switches. Change only that stale conditional-default count; retain the separate
41-switch assertion and every existing control.

Changed file: `tests/OS.HostTests/M02DoubleFaultIstEfiIntegrationTests.cs`.
No runtime, EFI producer, native artifact or profile behavior changes.

Validation: an isolated executable compiles the complete original initializer,
then runs all six groups including both actual default/enabled NativeAOT
publications. Build: zero warnings/errors. Run: exit 0, 7 TEST_START,
8 TEST_PASS (including terminal host.tests), zero TEST_FAIL, one host READY.
The artifact-only project contains no replacement test bodies.
`git diff --check` passes.

Evidence: `artifacts/issue546-default-count/Focused.csproj` and `run.log` in the
EBS worktree. The prior full failure is preserved under root
`artifacts/orchestration-20261001/issue546-common-gate-8e8c3f0/run/`:
started 2026-10-05T07:16:21Z, ended 08:13:44Z; sources and launch binaries
unchanged, native exit 3762504530. This is not a full-suite pass.

Next: rerun the required complete common gate on the corrected clean head,
then fresh default/controlled QEMU acceptance and merge review for #61/#546.
No real firmware ExitBootServices, post-exit behavior, hosted .NET, or cloud
readiness is established. The controlled same-image adapter remains default off.
