# Step 962: Evidence-qualified milestone state synchronization

## What changed and why

Issue #529 repairs milestone synchronization that previously forced every
container open. Exact string intent now distinguishes `open` (preserve an
existing closed container), `closed` (evidence-gated closure) and `reopen`
(explicitly open). One resolver serves preview, apply and convergence.
Missing containers are created open; their closed intent is deferred.

Closure requires current canonical membership, the expected epic with exact
`type:epic`, the unchanged recursive issue-evidence check with pending closure
disallowed, and every actual non-PR member closed. The reviewed epic supplies
aggregate supplemental review attestation because historical supplements lack
one uniform marker. A closed container with inconsistent prerequisites reports
a blocker. No pending issue update is treated as already completed. Generic
issue closure policy and orchestration order remain unchanged; no due date is
fabricated. Ordinal comparisons reject cultural aliases and malformed types.

## Files changed

- `scripts/sync-gitea-backlog.ps1`
- `tests/OS.HostTests/GiteaBacklogManifestTests.cs`
- `.gitea/backlog/clros-issues.json`: only M02 milestone state, open to closed intent.
- `docs/issue-labels.md`
- This reserved `done/step962.md`.

The author used a separate worktree at
fa889cda09c17d1d52228fa031d7591e5ba8f7eb. Parent imported the exact two reviewed
files after the #58 host checkpoint f366f06222b78160e175fbbe9607bf748182d70b.
All 345 canonical issue rows and the other 15 milestone intents are unchanged
by this step. Previous steps, including step961, remain immutable.

Final script SHA256: 0F06DAD97A28783C2313B66499C402E9538474899B8A8BBE4602495F24B18F9A.
Final test SHA256: 5624CC4C85ED7F2BE35E963EF8ECC09DB4122323423DB22C39B6FBDA1DD411EA.

## Tests run and evidence

Actual author attempt2 ran 2026-10-03T18:33:15.154746Z through
18:34:39.603315Z. All six commands returned native 0 with empty outer stderr:
WinPS5.1 ParseFile, script `-SelfTest`, focused Release build, complete
GiteaBacklogManifestTests initializer, normal Release HostTests build and
`git diff --check`. Both builds report zero warnings/errors. The focused
project links the complete actual initializer and unchanged HostToolProcess;
its Program only emits the terminal marker.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/sync-gitea-backlog.ps1 -SelfTest
dotnet build artifacts/issue529/focused/Focused.csproj -c Release --nologo -m:1 -nodeReuse:false
dotnet artifacts/issue529/focused/bin/Release/net10.0/Focused.dll
dotnet build tests/OS.HostTests/OS.HostTests.csproj -c Release --nologo -m:1 -nodeReuse:false
git diff --check
```

The complete initializer returned 17 starts/18 passes/zero failures, all
original names/order and one final `TEST_PASS manifest.focused`, in
18:33:25.689565Z–18:34:21.939907Z. Four existing historical READY lines remain.
SelfTest covers 28 actual-function scenarios in both preview/apply modes:
exact method/path/payload/counters, rejection, unchanged closed metadata,
explicit reopening, deferred missing containers, canonical/supplemental
closure and convergence. The existing missing-field group also runs
17 malformed and three valid intent controls. Child process output inside the
C# fixture uses the existing combined-output helper; empty outer stderr does
not assert separate empty stderr for each of those children.

All 896 captured source/project/catalog/helper inputs were stable. Evidence
is under `artifacts/issue529/` in the milestone-state-sync worktree:

- `attempt2/result.json`: 0EB0F78F97E0947AFD3B561370A752B596774092B091E589AF30050BC8BE6E7C.
- Focused stdout: A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9.
- SelfTest stdout: 40172A2924C78B6673C8128321BF80A9B34BA1BF98ADC9C8A49D739D9B41E8E7.
- `final/report.md`: 260D72C350CDA4126743E698327B4F072F30778053684C9B78BF15080C7449E2.
- `final/evidence-identities.json`: F841DC922FDD83D476739ECB875CBF009F3BE2B41E16D97F37FDFBB433B1D562 (84 records).

Parent read the complete source delta and raw logs and rehashed six package,
84 evidence and 896 input records, then derived exact marker order from the
actual source. Independent source review
DE041884F617808D574810E68A8CE93CDEBEE4C00FB23F83FB74AD5CF11F430B and final
execution review55763D964E1942AED242A5AC93E72151FFD0F7A569CA8AA4B73EE4CE4ECA513B
found no remaining finding. Parent rehashed both review packages and inputs.
Root records are under `artifacts/orchestration-20261001/issue529-integration/`.
Parent root integration checks against M02 closed intent passed. The normal
HostTests Release build returned native 0, zero warnings/errors and empty
stderr at 2026-10-03T18:45:01.188618+00:00–2026-10-03T18:45:31.515713+00:00,
with all 864 captured inputs unchanged. Result SHA256
CDDF76D83F71BBE7E00F39385740FA8DDD7E0EA197916D565B2F3646BA038788; stdout
4249137F33C51C456B60EE8DDBEB1D8409CE06EA8E60A8612FC19B1352127514.

A fresh root focused build links the exact complete changed initializer and
unchanged HostToolProcess. It returned native 0 with zero warnings/errors;
its actual command/output is under `issue529-integration/focused-attempt1`.
The complete run at 2026-10-03T18:45:04.568943+00:00–
2026-10-03T18:46:14.103951+00:00 returned native 0, empty stderr,
17 starts/18 passes/zero failures, exact source-derived names/order and final
`TEST_PASS manifest.focused`. Ten captured inputs and the compiled outputs
remained unchanged. Result SHA256
7F19166A88DD9514EC9BB039CFC0F9E7767CA0C8CE8F7D4914239AD40E0AB392; stdout
A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9.
Only this step changed afterward to record these results. Diff/whitespace
checks pass. These focused checks are not the common-head full-suite gate;
that gate and live milestone closure remain pending.

## Failures and preserved corrections

V1 was corrected before execution after self-review found PowerShell singleton
array unrolling. V2's complete attempt1 passed, but review identified cultural
state equivalence and reliance on the epic label for recursive child checks.
V3 fixes those admissions and adds negative cases; its separate attempt2 is
the final source-bound result. All earlier sources and logs remain. A reviewer
read-only attempt used an extra path parent; the corrected reader passed
without product changes or reruns. No failing product execution is hidden.

## Deferred work and evidence boundary

M02 epic #40 closed with evidence comment14475 after reviewed PR530 merged as
fa889cda09c17d1d52228fa031d7591e5ba8f7eb. The subsequent milestone4 readback had
40 closed issues and zero open, but the container remains open. This step
records canonical closed intent; it does not assert a completed live update.

The combined merge wave must pass its complete common-head gate, be reviewed
and merged. Parent then rechecks actual M02 membership/evidence, changes only
milestone4 state, independently reads it back, and checks that a read-only
sync plan preserves closure. No broad live apply is authorized as a test.
Issue #529 stays open until that evidence is posted. This tooling provides no
new runtime, guest, real ExitBootServices, hosted .NET/Kestrel, cloud, production
or numbered completion-gate proof.
