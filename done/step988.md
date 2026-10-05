# Step 988 - Typed one-attempt ExitBootServices adapter

## What changed and why

PR542 merged at `dd5fff2417106b619ac6bde499f003b82fd1cb87`. M03-003 (#60)
and its supplement #541 closed with evidence comments 15124 and 15116.
#61's stale blocked label was removed and its typed-adapter work claimed;
comment 15139 and body version 2 were read back. #62 remains blocked until
the complete controlled adapter evidence is accepted.

The adapter admits retained caller-owned POD state with an explicitly available
firmware mode, a nonnull typed unmanaged callback and nonnull opaque handle.
It publishes the full key and InFlight state before exactly one indirect call,
then preserves the full UInt64 return and marks Returned. Both success and
failure consume the attempt. Reuse, reentry and invalid admission preserve
state without calling. No global firmware mode, map capture or retry is changed.

## Files changed

- `src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.cs`
- `tests/OS.HostTests/TypedNoStdlibExitBootServicesCallAdapterTests.cs`
- `docs/boot.md` and `docs/efi-build.md`
- `.gitea/backlog/clros-issues.json`: M03-004 only; claimed status, actual host
  evidence, corrected EFI path and concrete adapter acceptance boundaries.
- `done/step988.md`, allocated using `scripts/new-done-step.ps1 -Reserve`
  against the shared allocation view retaining steps986/987; native exit0.

## Tests run

Worktree: `C:/Users/Roger/.codex/worktrees/m03-004-ebs-adapter/CLROS`,
branch `codex/m03-004-ebs-adapter`, based on the merge above. The exact
argument arrays are in `artifacts/issue61-typed-ebs-adapter/source-checkpoint1/command-plan.json`.
All builds used `-c Release --nologo -m:1 --disable-build-servers
-p:UseSharedCompilation=false`.

1. `dotnet build artifacts/issue61-typed-ebs-adapter/focused/Focused.csproj`:
   2026-10-05 00:34:00.712265-00:34:12.730961 UTC, exit0.
2. `dotnet artifacts/issue61-typed-ebs-adapter/focused/bin/Release/net10.0/Issue61.Focused.dll`:
   00:34:12.739590-00:34:14.869003 UTC, exit0; 53 TEST_START, 54 TEST_PASS,
   zero TEST_FAIL and one terminal TEST_PASS host.tests.
3. `dotnet build tests/OS.HostTests/OS.HostTests.csproj`:
   00:34:50.527151-00:35:14.019578 UTC, exit0.

Both builds have zero warnings/errors; all three stderr streams are empty.
The focused harness invokes all six complete original initializers once:
the new eleven-group adapter module, FirmwareServicesGuard,
PlatformMemoryMapGuard, UefiMemoryMap, UefiExitBootServicesPlan and
UefiExitBootServicesLiveWiringPlan. No method subset replaces a complete module.

The tests measure state40/result16 on the x64 host, exercise eighteen
handle/key combinations and nine statuses, success/error reuse, callback
reentry and rearm refusal, malformed state, unchanged firmware guard and
zero managed allocation after warmup. The unmanaged callback records POD
observations without throwing across the boundary. The actual four-source
NoStdLib child compiles with zero AssemblyRefs and real Prepare/Attempt IL
bodies; that assembly is not executed. Original baseline modules are unchanged.

## Logs and evidence

Production SHA256 `7227E860B5E4F97D47C9C269B889C975E30F21C506F9ECB51424F5417E2945DB`;
test SHA256 `98F5A225E855D311A54D8792C7619C03365051004A6200F34DD48107D1E3F587`.
The complete focused stdout is retained at
`artifacts/issue61-typed-ebs-adapter/attempt1/managed-run/stdout.log`, SHA256
`2A223592031B536BE3D20C91B2F57E71F379EDD92208016DA075F1F8C7AAA2C7`.
Both recorder terminal results report unchanged inputs and launch binaries.
Actual raw commands, streams, before/after identities, snapshots and the
no-stdlib child remain under `artifacts`; generated evidence is not committed.

Independent source/command review found no issue; parent read both complete
files and the whole-module plan. Its root artifact
`artifacts/orchestration-20261001/issue61-host-source-independent-review/review.md`
has SHA256 `9F63FFB6028232708B17EA30C989A95BC97162BAB9ACD6B4CA6909D3A2303FC5`.
Independent terminal-evidence review likewise found no issue. The completed
review at that same root artifact's `completed/review.md` has SHA256
`FB81F6F0E1B12167C8D22AEF31EA7FA8754EF55C3E557273317FC56CCC6033F8`;
its result is `42D2E5E677D4943C928A80EDE97E25D27F51F305B98807848C84F9C788AF430A`.
It rehashed all seven final-package and 1,930 evidence records, compared the
complete ordered six-initializer stream and validated each source interval,
launch copy and three normal-build archives without product reruns.

The author's final host result is
`artifacts/issue61-typed-ebs-adapter/final/result.json`, SHA256
`551DCF05489B2ADDF5F238966944E8E4F6BB7EAA2A76446CC0560723E57C6A9E`.
Both attempts preserve all 933 input rows and archived source bytes; the nine
focused launch records remain unchanged. The no-stdlib DLL SHA256 is
`2ED61842108CC18FA71FA7BDD7EC2445D32AF9A6F58948D74A9CA3B8B2641DC0`.

Parent integration checks passed on 2026-10-05: catalog `-Offline`
00:40:33.409941-00:40:48.416788 UTC (345 issues, 16 milestones, 68 labels),
and naming/source firewall 00:40:33.427302-00:41:42.473706 UTC. Both exit0
with empty stderr. Exact records are under root
`artifacts/orchestration-20261001/issue61-host-integration`.
`git diff --check` passed after removing one new documentation EOF blank line.

## Failures

No product compile or test failure occurred. A reviewer-only static counting
expression initially omitted three multiline Run call sites; its corrected
count and original observation are retained in the review package. This did
not change product source or replay any tests.

## Deferred work and evidence boundary

#61 remains open/claimed. Controlled EFI native ABI, fresh default/enabled QEMU
captures and the clean integrated common suite remain required before closure.
The shared EFI path needs a separately claimed integration supplement. This
host pair changes no EFI project, profile, runtime helper, BootSequence or
probe switch, and calls no original firmware ExitBootServices pointer.

The caller supplies valid callable authority and serialized retained storage.
The adapter does not defend against arbitrary pointer provenance, copied/reset
state, hostile mutation or concurrent access. M03-005/006/007 (#62/#63/#64)
retain final-map ordering, stale-key retry and first actual firmware exit.
No global BootServicesExited publication, post-exit execution, GC,
hosted .NET/Kestrel, cloud or production readiness is established.
