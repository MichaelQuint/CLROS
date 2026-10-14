# `work/PAL` Override

Root `AGENTS.md` applies here. This override only records PAL design ownership
rules that differ from the root guide.

## Purpose

`work/PAL` owns design notes for the future boundary between:

- Kernel/native-tier OS code.
- Future hosted .NET 11/CoreCLR integration.
- Host tooling and tests where applicable.

Implementation-facing PAL rules live in `AGENTS.md`, `docs/pal.md`, and
`work/PAL/pal-design.md`. Keep detailed ABI tables in those docs rather than in
this override.

## Naming

The reference project may use `SharpOSHost_*`. For this repository, `OSHost_*`
is canonical. Add `SharpOSHost_*` only as a documented compatibility alias for
adapted reference code or an external runtime fork. Do not introduce `SharpOS`
as the product name.

## Documentation Requirements

Any PAL change must update:

- `work/PAL/pal-design.md`
- `docs/pal.md`
- `docs/architecture.md` if boundaries change.
- `docs/completion-gates.md` if evidence changes.

Design docs, not this override, must state:

- Function name.
- Direction of call.
- Parameters.
- Ownership.
- Status codes.
- Failure modes.
- Threading expectations.
- Whether the function is native-tier-only or hosted-tier-facing.

## Implementation Timing

Do not implement hosted CoreCLR before prerequisite boot/runtime/platform work
exists unless a current issue explicitly asks for hosted-tier work.

PAL docs may advance ahead of implementation, but must not claim implementation
evidence.
