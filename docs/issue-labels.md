# Issue Labels

## Current Status

Gitea issue labels exist for phase, component ownership, work type, and agent readiness.

## Implemented Behavior

- `phase:*` labels identify milestone sequencing.
- `component:*` labels identify subsystem ownership.
- `type:*` labels identify whether the issue is implementation, test/tooling, docs/ADR, research, or an epic container.
- `agent:ready` marks small issues that an autonomous agent can pick up now.
- `agent:blocked` marks epics or issues waiting on prerequisite implementation/evidence.

## Unsupported Behavior

Labels are not synchronized from repository metadata or CI. They are currently managed through the Gitea HTTP API.

## Tests

The label set was created and applied through the Gitea API on 2026-05-21. No local code test is required.

## Known Limitations

Issue labels can drift as dependencies change. Agents should still read the issue body and current comments before starting work.

## Next Steps

1. Prefer `agent:ready` issues for parallel implementation work.
2. Avoid assigning `type:epic` issues directly; use them as containers.
3. Update labels when closing blockers or creating follow-up issues.
