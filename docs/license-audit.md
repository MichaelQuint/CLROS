# License and Provenance Audit

## Current Status

The project owner has authorized agents to inspect and use the reference
projects for CLROS implementation work. That direction does not erase
third-party copyright, license notice, attribution, source-disclosure, patent,
trademark, or asset-license obligations.

The 29 local reference directories are archive snapshots without `.git`
metadata. None has a top-level file named `LICENSE`, `LICENCE`, `COPYING`, or
`NOTICE`. Some READMEs state or link a project license; some trees contain
nested component licenses; some contain no local license evidence. An archive
name ending in `-main` or `-master` is not a verified revision.

Therefore:

- Architecture study and independently authored behavioral tests are allowed.
- Direct source reuse is blocked until the exact upstream, immutable revision,
  applicable license, source path, authorship, and required notices are
  recorded.
- A nested dependency license applies to that dependency, not automatically to
  the surrounding project.
- Missing license evidence means "unknown", not "public domain" or "cleared".
- Reference artifacts never satisfy a CLROS completion gate.

SharpOS has additional permission evidence that changes its default
disposition. On 2026-07-25 the project owner supplied a Telegram transcript in
which Daniil Vaino linked `https://github.com/daniilvaino/SharpOS`, identified
it as his repository, stated that it is CC0 and free to copy, and proposed
direct consolidation of its kernel, std, UEFI-exit, memory-management, and GC
work with CLROS. The supplied 7,775-byte transcript has SHA-256
`F244C8B21E90DA7627E2F12EA3E7F0CF7F2034F5417C20F0E6D49F0F5CC5E97D`;
the private transcript itself is not committed because it contains unrelated
personal conversation. The current upstream `main` branch was also checked at
commit `b80ee3a4175e1522d1a12ba7fe371a49da3207f5`: it contains a top-level
CC0-1.0 legal text and a README license section authorizing use,
modification, and distribution for any purpose.

This is sufficient to treat SharpOS-authored source as eligible for direct,
selective adaptation rather than `reference:license-blocked`. It does not
relicense separately identified third-party/vendor code, the GPL ManagedDoom
fixture, WAD data, screenshots or other media with separate rights, or the
absent CoreCLR fork. Because the supplied archive still lacks `.git` metadata,
an adapting issue must record the exact local source path and SHA-256; if the
archive revision is later recovered, record that commit as well.

### Sanitized SharpOS permission attestation

This subsection is the durable, privacy-minimized record. The unrelated private
conversation is intentionally excluded.

| Field | Preserved value |
| --- | --- |
| Grantor identity | Daniil Vaino, who linked and described `https://github.com/daniilvaino/SharpOS` as his repository |
| Recipient/project | Michael, for consolidation into the CLROS project discussed in the same exchange |
| Transcript time evidence | The supplied export contains no timestamps; it was supplied to this repository review on 2026-07-25 |
| Explicit permission | “my repo is cc0” and “Completely free to copy/steal, no need to write from scratch” |
| Proposed reuse scope | Direct reuse from the SharpOS reference snapshot, including its kernel, std, UEFI exit, memory management, GC, and related source, while retaining CLROS project separation and cloud/Kestrel work |
| Original attachment fingerprint | 7,775 bytes; SHA-256 `F244C8B21E90DA7627E2F12EA3E7F0CF7F2034F5417C20F0E6D49F0F5CC5E97D` |
| Independent public corroboration | Current upstream commit `b80ee3a4175e1522d1a12ba7fe371a49da3207f5` contains a top-level CC0-1.0 license and matching README license statement |
| Exclusions | File-local third-party/vendor terms, the GPL ManagedDoom subtree, WAD/media rights, and the separately versioned absent CoreCLR fork |

The attestation establishes permission and scope without requiring future
agents to access the private transcript. The attachment fingerprint exists
only to detect substitution if the owner elects to present the original again;
it is not a claim that the private attachment is stored in Git.

## Snapshot Ledger

`Revision` is `unknown` for every row because the supplied archives contain no
`.git` metadata.

| Snapshot | Local evidence | Default reuse disposition |
| --- | --- | --- |
| `abanu-master` | README badge/text identifies MIT and links an upstream license that is not present locally. | Test vectors or independently reimplemented algorithms; recover revision/license before copying. |
| `AtomOS-master` | README links a missing project `LICENSE.md`; `src/Compiler/License.txt` covers compiler-related material and README separately links Cosmos licensing. | Conceptual/test-only until file-level ownership and both license scopes are resolved. |
| `Aura-Operating-System-master` | No local top-level project license; `LICENSES/` contains third-party component licenses only. | Negative security tests only; no direct project-source reuse. |
| `Brovan-main` | README states GPL-2.0. | Host test corpus ideas only unless a deliberately GPL-compatible isolated tool is approved. |
| `CosmosELF-master` | README contains no usable local license evidence. | Malformed-input ideas only; no direct reuse. |
| `Cosmos-gen2` | README links Cosmos's upstream license, but no project license file is present locally; bundled tool licenses are separate. | Independent test vectors only unless exact upstream/revision/license is recovered. |
| `CSharpLLVM-master` | No local project license evidence. | Conceptual compiler/runtime comparison only. |
| `dotnet-aot-xc-main` | No local project license evidence. | Conceptual host-tooling reference only. |
| `EfiSharp-main` | No top-level project license; `Licenses/` contains .NET, mlibc, and RoseOS component licenses. | EFI ideas/tests only; determine authorship and applicable license per file before copying. |
| `elfsharp-master` | README says the license is in a missing `LICENSE` file and notes MIT test binaries. | Use only independently authored parser tests until the missing project and corpus licenses are restored. |
| `expressos-master` | README states UIUC BSD. | Recover immutable revision and full notice before any direct adaptation. |
| `FlingOS-develop` | README states GPLv2 and explicitly warns that copying imposes GPL obligations. | Concepts/documentation patterns only; no first-party implementation copy. |
| `GlieseOS-main` | No local project license evidence. | Ignore for implementation. |
| `Kernel-master` | No local project license evidence. | Negative examples only. |
| `KernelSharp-master` | No local project license evidence. | Independent allocator/paging regression tests only. |
| `libcs-master` | No local project license evidence. | Negative/conceptual use only. |
| `libvirt-csharp-main` | `src/libvirt/libvirt.csproj` declares `LGPL-2.1-only`; no top-level license text is present. | Host-only package/API comparison. Prefer `virsh`; recover the LGPL text and revision before distribution. |
| `MOOS-master` | README links an upstream license, but the license text is absent locally. | Concepts/negative ABI tests only until provenance is recovered. |
| `MOSA-Project-master` | README states New BSD and links a missing upstream license; `Source/Docs/license.rst` and bundled tool licenses require separate review. | Independent behavior tests are preferred; recover exact notices before copying. |
| `nativeaot-patcher-main` | README states MIT and links a missing top-level `LICENSE`; no commit metadata exists. | Strong selective reference, but direct reuse remains blocked pending the exact MIT text, revision, and per-file dependency review. |
| `Nitrocid-main` | Project files declare `GPL-3.0-or-later`; source headers repeat GPL terms. | Do not copy into first-party code; optional hosted/tool use would require explicit GPL handling. |
| `PatienceOS-main` | No local project license evidence. | Test-pattern ideas only. |
| `RoseOS-master` | No top-level license; EfiSharp carries a file called `RoseOS License`, but that copy is not provenance for this snapshot. | Negative UEFI/allocator tests only until exact RoseOS provenance is restored. |
| `Sharpen-master` | No local project license evidence. | Historical conceptual use only. |
| `SharpOS-main` | The supplied archive omits its top-level license and revision metadata, but the owner-supplied permission transcript identifies the repository as CC0 and authorizes copying/consolidation. Current upstream commit `b80ee3a4175e1522d1a12ba7fe371a49da3207f5` contains a top-level CC0-1.0 license. `vendor/Iced/LICENSE.txt`, `vendor/PeNet/LICENSE`, and `apps_native/GPL_AHEAD_WARNING_DOOM_managed/LICENSE` retain their separate scopes. | SharpOS-authored source is eligible for selective adaptation when the exact snapshot path/hash and CLROS adaptation boundary are recorded. Keep vendor notices and GPL DOOM code isolated; never commit a WAD. |
| `SharpOS-master` | No local README or license file; source text refers to GPL version 3 but is not a complete license record. | Historical test census only; independently recreate tests. |
| `smolsharp-main` | No top-level project license. Files under `src/BFlat.ZeroLib` carry explicit GNU Affero GPL version 3-or-later headers; the README also discusses external ZeroLib/bflat work whose complete provenance is not locally captured. | Size/security concepts only; no direct source import. The explicit AGPL source boundary is incompatible with copying those files into first-party CLROS kernel/std/app source. |
| `xOS-main` | `CosmosOS-License` contains BSD-3-Clause terms for CosmosOS/COSMOS-derived material; no separate top-level xOS license is established. | No implementation use; do not extend the Cosmos notice to unrelated xOS/dependency code. |
| `zerosharp-master` | No local project license file; README points to historical NativeAOT/runtimelab sources. | Selective build/EFI concepts only until source and binary provenance is recovered. |

## Upstream Candidates Recorded by Local Metadata

These URLs are present in local READMEs, badges, project metadata, or the
supplied project context. They identify a provenance investigation target; they
do not identify the commit represented by the archive.

| Snapshot | Candidate upstream |
| --- | --- |
| `abanu-master` | `https://github.com/abanu-org/abanu` |
| `AtomOS-master` | `https://github.com/amaneureka/AtomOS` |
| `Aura-Operating-System-master` | `https://github.com/aura-systems/Aura-Operating-System` |
| `Brovan-main` | `https://github.com/AdvDebug/Brovan` |
| `Cosmos-gen2` | `https://github.com/CosmosOS/Cosmos` |
| `EfiSharp-main` | `https://github.com/JoshuaWierenga/EfiSharp` |
| `FlingOS-develop` | `https://github.com/FlingOS/FlingOS` |
| `GlieseOS-main` | `https://github.com/valentinbreiz/GlieseOS` |
| `libvirt-csharp-main` | `https://github.com/falox/libvirt-csharp` |
| `MOOS-master` | `https://github.com/nifanfa/MOOS` |
| `MOSA-Project-master` | `https://github.com/mosa/MOSA-Project` |
| `nativeaot-patcher-main` | `https://github.com/valentinbreiz/nativeaot-patcher` |
| `Nitrocid-main` | `https://github.com/Aptivi/Nitrocid` |
| `PatienceOS-main` | `https://github.com/FrankRay78/PatienceOS` |
| `SharpOS-main` | `https://github.com/daniilvaino/SharpOS`; current verified `main` commit `b80ee3a4175e1522d1a12ba7fe371a49da3207f5` carries CC0-1.0. The local archive predates or omits that metadata, so this commit is not asserted to be the archive revision. The README separately links `https://github.com/daniilvaino/dotnet-runtime-sharpos/tree/sharpos/coreclr-port` for its absent CoreCLR fork. |
| `smolsharp-main` | `https://github.com/ascpixi/smolsharp` |
| `zerosharp-master` | `https://github.com/MichalStrehovsky/zerosharp` |

For all omitted snapshots, the canonical upstream remains unrecorded until it
can be recovered from trustworthy metadata. Do not infer it solely from the
directory name.

## Source-Adaptation Record

Before a direct copy or material algorithm adaptation, the implementing issue
and `done/stepNN.md` must record:

1. Reference directory and exact source path.
2. Canonical upstream URL.
3. Immutable commit/tag and retrieval date.
4. License identifier and the local or upstream license text used for review.
5. Copyright holder and required notices.
6. Whether the CLROS result is copied, translated, adapted, or independently
   reimplemented from a specification/test.
7. Exact CLROS files affected.
8. Third-party dependencies, generated code, media, firmware, or test-corpus
   files involved.
9. Distribution obligations and where notices/source will be delivered.
10. The independent tests and known-reference weaknesses carried forward.

If any item is unknown, label the backlog item `reference:license-blocked` and
use only conceptual or independently authored test influence. The exception is
SharpOS-authored source covered by the owner grant and upstream CC0 evidence:
missing archive commit metadata is still a provenance gap, but it is not a
license blocker when the exact local file hash is recorded. File-local vendor,
GPL, asset, and contributor notices still take precedence over that default.

## SharpOS-Specific Boundary

The local SharpOS snapshot mixes several distinct scopes:

- The owner-supplied permission record and current upstream CC0-1.0 license
  authorize selective reuse of SharpOS-authored kernel, std, boot, PAL, tools,
  and app SDK source. The supplied archive still has no recoverable commit, so
  adaptations must bind themselves to exact local file hashes.
- `vendor/Iced/LICENSE.txt` applies to the Iced vendor tree.
- `vendor/PeNet/LICENSE` applies to the PeNet vendor tree.
- The ManagedDoom application is isolated below
  `apps_native/GPL_AHEAD_WARNING_DOOM_managed/` and carries its own GPL
  license. Its code must not enter kernel/std, and WAD data must not be
  committed.
- The linked `dotnet-runtime-sharpos` CoreCLR fork is not present. Its future
  use requires the upstream .NET runtime license, fork commit, patch ledger,
  dependency closure, build provenance, and attribution to be preserved in a
  separately versioned dependency.

The SharpOS CC0 evidence does not supersede a file-local third-party license,
and the absence of the external CoreCLR fork prevents reproducible
hosted-runtime provenance.

## First-Party and Distribution Rules

- First-party kernel/std/app source remains C#; do not commit `.c`, `.cpp`,
  `.h`, `.asm`, or `.s` there.
- C/C++ required by the future modified CoreCLR remains in a separately
  versioned sibling repository and crosses CLROS only through `OSHost_*`.
- Host tools may consume normal .NET packages only with locked versions,
  compatible licenses, notices, and separation from the guest image.
- Generated firmware, QEMU/OVMF, WADs, fonts, screenshots, binary corpora, and
  cloud images each require their own redistribution review.
- Do not print or commit credentials, tokens, private package feeds, signing
  keys, or cloud identifiers.
- A project-owner instruction to use a reference is implementation
  authorization, not legal advice. Escalate unresolved copyleft, patent,
  trademark, asset, or distribution questions before release.

## Current Automation Boundary

The source firewall can detect forbidden first-party source and accidental
reference build inputs; it does not identify licenses, determine copyright,
recover commits, generate an SBOM, or prove notice compliance.

Until dedicated tooling exists, validate this document with:

```powershell
$references = Get-ChildItem .\references -Directory
if ($references.Count -ne 29) { throw "reference inventory changed" }

$repositoriesWithGitMetadata = $references |
  Where-Object { Test-Path (Join-Path $_.FullName ".git") }
if ($repositoriesWithGitMetadata.Count -ne 0) {
  throw "update provenance entries for snapshots with recoverable git metadata"
}

git diff --check
```

## Next Steps

1. Recover the exact SharpOS archive revision if possible; until then bind each
   SharpOS adaptation to exact local file hashes and the recorded CC0/owner
   permission evidence. Separately recover canonical upstream URLs, immutable
   revisions, retrieval dates, and complete license texts for
   `nativeaot-patcher-main`, `zerosharp-master`, and `EfiSharp-main` before
   their first direct source adaptation.
2. Add a machine-readable attribution/provenance ledger and SBOM generation
   before distributing binaries containing third-party material.
3. Treat every future reference update as a new immutable snapshot requiring a
   renewed source, license, and behavioral-diff review.
