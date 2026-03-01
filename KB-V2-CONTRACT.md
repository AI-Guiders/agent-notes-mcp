# KB v2 Memory Contract

Production-oriented memory contract for stable long sessions and predictable context routing.

## Goals

- Keep response latency stable during long chats.
- Preserve decision-grade memory without dragging full archives into hot context.
- Make context loading explicit, measurable, and reversible.

## Layer Boundaries

- **L0 (hot cache):** current goal, next action, blockers, guardrails.
- **L1 (working state):** active contracts and near-term decisions.
- **L2 (archive):** historical batches, long analyses, raw transcripts, large artifacts.
- **L3 (router):** status/playbook/matrix entrypoints and retrieval order.

## Budgets and SLO

- **L0+L1 soft budget:** up to 6000 chars.
- **L0+L1 hard budget:** up to 12000 chars.
- Above soft budget -> warning, recommend `compact_hot_context`.
- Above hard budget -> critical, require compaction before continuing deep work.

## Retrieval Contract

- Default order: `status -> playbook -> matrix -> kb`.
- Router must load only top relevant sections first.
- Archive access is explicit and query-driven (no full L2 hydration by default).

## Operational Triggers

- Run `memory_health`:
  - before long implementation sessions,
  - after large context imports,
  - after context compression recovery.
- Run `route_context`:
  - before deep task start,
  - when scope changes,
  - when mixed-domain ambiguity appears.
- Run `compact_hot_context`:
  - when `memory_health` emits warning/critical,
  - before release/debug marathons.

## Minimal Governance

- Preserve additive compatibility for MCP tools.
- Keep destructive behavior behind explicit `apply=true`.
- Record only decision-grade deltas in hot context.
