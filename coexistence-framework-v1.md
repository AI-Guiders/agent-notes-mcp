# Coexistence Framework v1 (Agents + Humans)

Goal: enable long-horizon work where agents and humans can *coexist and act together* with predictable safety, reversibility, and auditability.

This is a governance/procedural contract layer. It complements, but does not replace, the memory contract (`KB-V2-CONTRACT.md`).

## Contract (rights, boundaries, responsibilities)

### Actors

- **Human operator**: the decision-maker for gated actions; owns “final veto” when risk is unknown or high.
- **Agent**: proposes plans, executes tools, drafts changes, and provides an audit summary for every meaningful action.
- **System**: tool + UI runtime that enforces gates, produces telemetry, and provides rollback paths.

### Core invariants (must hold)

- **No silent irreversibility**: any potentially destructive step must be either reversible or preceded by an explicit human confirmation gate.
- **Explicit intent before execution**: before changing state, the agent must state objective, intended scope, and expected verification step(s).
- **Bounded context and retrieval**: long sessions must respect `KB v2` budgets and use router-first retrieval (no accidental “full archive hydration”).
- **Audit trail is always emitted**: for each action group, the system/agent must produce trace: what happened, what changed, why it was safe (or why it required confirmation).

### Rights and responsibilities

- The **agent** may:
  - propose plans and tool calls,
  - draft edits,
  - run read-only analysis and diagnostics,
  - request confirmations for gated actions,
  - prepare rollback instructions when changes are made.
- The **agent** must:
  - stop/ask for confirmation when “risk unknown” or “risk high” triggers fire,
  - provide a clear rollback path (memory revisions rollback and/or code rollback via git).
- The **human operator** may:
  - approve/reject the proposed gated step,
  - request additional verification (tests/builds) before approving,
  - trigger rollback if the result deviates from the expected outcome.

## Coexistence Modes (L1/L2/L3)

These modes define what “joint life” means operationally. They map to the UI safety level concept already present in `CascadeIDE`.

### L1 — Assistant (read-only co-pilot)

Allowed:

- analysis, planning, information retrieval,
- generating drafts and previews.

Not allowed:

- applying edits to code/files without confirmation.

Human involvement:

- none required (unless the agent flags risk).

### L2 — Confirmed edits (human-in-the-loop)

Allowed:

- applying edits, tool actions that change state, and running verification steps.

Required:

- the system must prompt a modal confirmation gate for the change batch,
- the agent must include:
  - risk summary,
  - impacted scope (what files/sections),
  - planned verification (build/tests/debug checks),
  - rollback plan.

Human involvement:

- mandatory approval before the change batch is applied.

### L3 — Autonomous bounded execution (agent acts, but stays recoverable)

Allowed:

- executing a bounded plan without pausing for every micro-step.

Required:

- budgets and contracts must be respected (especially `KB v2` budgets + router-first retrieval),
- the system must keep audit + telemetry updated continuously,
- rollback must be ready:
  - memory rollback via `rollback_agent_notes` / revisions,
  - code rollback via git strategy (rollback/revert or controlled commit boundaries).

Human involvement:

- not required for every step, but required when:
  - risk is unknown and cannot be safely bounded,
  - verification fails or mismatches expectations,
  - scope expands beyond the approved plan.

## Operational templates (what the agent/system must output)

For every “action group” the agent/system should produce:

1. **Intent**: objective + next action.
2. **Scope**: sections and/or file paths expected to change.
3. **Safety**: which coexistence mode applies and why.
4. **Verification**: build/tests/debug checks to validate.
5. **Rollback**: exact mechanism to revert if needed.
6. **Trace**: what tools ran and what outcomes were observed.

## Mapping to our stack (concrete enforcement points)

- Memory governance:
  - `agent-notes-mcp`: `memory_health`, `route_context`, `compact_hot_context`, `rollback_agent_notes`.
- IDE/UI governance:
  - `CascadeIDE`: modal confirmations via `ide_request_confirmation`,
  - telemetry strip and git summary for “what changed” visibility.
- Code safety:
  - reversible change strategy via git boundaries and rollback/revert when appropriate.

## Non-goals

- This framework does not define model alignment or “ultimate autonomy”.
- It defines a *coexistence contract* so that practical shared action is safe, auditable, and reversible.

