# Step 992 - Retain one authenticated static-base allocation on the host

## What changed and why

#81 now materializes one admitted GC-static base and retains its allocation
receipt. #548 contributes a registered-image range query and a scalar
abbreviated-MT allocation route through the existing sole allocator. The root
record binds its own full capacity, original image/Type/heap/bump authority,
metadata bytes, initial cell and receipt. An absolute published cell is never
decoded again as rel32. Valid repeats and later allocations preserve lifetime.

Pre-admission failure/exhaustion preserves full storage. After allocation,
independent no-inline readbacks precede publication; a discrepancy poisons the
record without promising rollback. Registered mutable ranges exclude full
protected capacities. External roots retain caller mapping obligations. The
private checksum is an accidental-change check, not forgery or ABA protection.

## Files changed

- src/OS.Kernel/Runtime/GCStaticRootsMalformedMetadata.cs and its host tests.
- src/OS.Kernel/Runtime/TypeManagerModuleMetadataLiveNativeAOTImage.cs and tests.
- src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs and tests.
- docs/nativeaot-nostdlib-limits.md and docs/completion-gates.md.
- .gitea/backlog/clros-issues.json: only M04-012 currentEvidence.
- done/step992.md, reserved at 2026-10-05 06:25:24.811001-06:25:25.114413 UTC
  through the shared new-done-step allocation view (exit0).

## Tests run

Actual final attempt2 on 2026-10-05: focused Release build
06:26:08.758579-06:26:17.988467 UTC; complete seven-module run
06:26:17.996932-06:26:32.390467 UTC; normal HostTests Release build
06:26:32.398996-06:26:34.387539 UTC. All exit0, empty stderr; builds have zero
warnings/errors. Builds use --nologo -c Release -m:1 --disable-build-servers
-p:UseSharedCompilation=false; the focused assembly is then executed directly.

81 TEST_START, 82 TEST_PASS, zero TEST_FAIL, one host.tests terminal, seven
readiness records. Whole modules cover GC metadata/root, TypeManager, layout,
retained heap, fixed/static allocation, array and String allocation. Original
array/String initializers are unchanged. Measured root owner600/result48;
1000 warmed retained queries allocate zero managed bytes. Byte-by-byte root
and live-metadata tampering, external/image-owned storage, unused capacities,
permission failures, later allocations, lost owner and modeled poison reject
without unreported writes. No real hardware-fault injection is claimed.

The actual no-stdlib child compiles93 sources (94 source/project identities),
zero AssemblyRefs, and real ValidateOne, MaterializeOnce, TryGetRetainedRoot,
AdmitRoot, FreshAllocation and Integrity bodies. That child is not executed.
All934 source/harness/plan inputs and nine launch files match before, after and
parent review. git diff --check passes. No EFI/profile file changed here.

## Logs and evidence

Ignored worktree artifacts/issue548-host-cores retains both source checkpoints,
both attempts, exact command arrays, all streams, source/binary snapshots and
accepted-host-v2/result.json. Its SHA256 is E153F5F0FB949C447A07D3A89975BFD66D2E0E87BF8C23B1BDF7C01977CAD7D3.
Final focused stdout is ED196777D6F088B0F600222B6C3CB134586F7C21F2EF7141A4417C99826FFA81.
The root orchestration issue548-host-integration directory retains allocation
and subsequent Git/Gitea operations. No generated artifact is committed.

## Failures

The first run also passed81/82/0, but its build warned CS0649 about the private
Reserved field. Explicitly assigning its already-zero value removed the
warning; the whole relevant run and both builds passed on final checkpoint2.
The first parent artifact audit stopped on that warning. A preparation-only
newline assumption was corrected before the second run. Direct in-session
step reservation was blocked by execution policy before execution; the normal
recorded PowerShell invocation reserved step992 once. These records are kept.

## Deferred work and evidence boundary

Only synthetic mapped host image/heap and no-stdlib compilation are accepted.
The43 shared EFI/profile paths of #548 are still held at this milestone. Exact
Type/source pins, genuine compiler GCStatics and accessor-to-cell association,
real object/null field stores through the checked special-ABI helper, native
review, full common suite and fresh QEMU are outstanding. #81/#548 and draft
PR547 remain open; #82 remains blocked. No general root/GCHandle registry,
collecting GC, concurrency/reset/unload support, real ExitBootServices, hosted
.NET/Kestrel, cloud or production readiness is claimed.
