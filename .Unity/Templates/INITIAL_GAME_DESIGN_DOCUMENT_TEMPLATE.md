# Initial Game Design Document Template — Unity

Use this document to decide whether a game concept is coherent, feasible, and worth
advancing into production planning. Keep it concise enough to review in one meeting.
Move implementation detail into `GAME_DEVELOPMENT_PLAN_TEMPLATE.md` after the concept
and prototype gates are approved.

Use `TBD` for unknown facts. Label statements as `Confirmed`, `Assumption`, `Open`, or
`Rejected`. Do not present aspirations, estimates, or prototype observations as
approved requirements.

## 1. Document Control

| Field | Value |
|---|---|
| Project / working title | TBD |
| Document ID | GDD-INIT-TBD |
| Version | 0.1.0 |
| Status | Draft / Review / Approved / Superseded |
| Owner | TBD |
| Reviewers | TBD |
| Created | YYYY-MM-DD |
| Last updated | YYYY-MM-DD |
| Target approval date | YYYY-MM-DD |
| Related repository / branch | TBD |
| Confidentiality | Public / Internal / Restricted |

### Revision History

| Version | Date | Author | Change | Approval |
|---|---|---|---|---|
| 0.1.0 | YYYY-MM-DD | TBD | Initial draft | Pending |

## 2. One-Page Concept

| Prompt | Answer |
|---|---|
| One-sentence pitch | TBD |
| Player fantasy | TBD |
| Genre and subgenre | TBD |
| Core verbs | TBD |
| Primary platform(s) | TBD |
| Perspective / dimension | First person / Third person / Side-scroller / 2D / 2.5D / 3D / XR / TBD |
| Session length | TBD minutes |
| Target audience | TBD |
| Distinguishing value | TBD |
| Comparable titles | TBD; state similarities and intentional differences |
| Current prototype | None / Paper / Unity scene / Vertical slice / Other |

### Elevator Pitch

> In **[world/context]**, the player **[core action]** to **[goal]**, while
> **[distinctive constraint or system]** creates **[intended emotion/experience]**.

## 3. Vision, Goals, and Boundaries

### Product Vision

TBD

### Player-Experience Goals

| ID | Intended experience | Observable signal | Validation method | Status |
|---|---|---|---|---|
| PX-001 | TBD | TBD | Playtest / Telemetry / Survey / Other | Open |

### Product Goals

| ID | Goal | Metric / acceptance threshold | Target date | Owner |
|---|---|---|---|---|
| GOAL-001 | TBD | TBD | YYYY-MM-DD | TBD |

### Non-Goals

- TBD

### Design Pillars

| Pillar | Meaning | Required behavior | Explicit anti-pattern |
|---|---|---|---|
| TBD | TBD | TBD | TBD |

## 4. Audience, Platform, and Product Context

| Topic | Decision / evidence |
|---|---|
| Audience segments and accessibility needs | TBD |
| Age/rating target | TBD |
| Supported input devices | TBD |
| Minimum and recommended hardware | TBD |
| Offline / online expectations | TBD |
| Single-player / multiplayer roles | TBD |
| Localization regions and languages | TBD |
| Distribution channels | TBD |
| Business model / price / monetization | TBD / N/A |
| Privacy, account, telemetry, or parental requirements | TBD / N/A |
| Competitive or market evidence | TBD; link dated sources |

## 5. Core Gameplay Loop

### Moment-to-Moment Loop

1. TBD
2. TBD
3. TBD
4. Return to step TBD with changed state.

### Session Loop

TBD

### Long-Term Loop

TBD

### Loop Validation

| Question | Hypothesis | Prototype / evidence | Pass condition |
|---|---|---|---|
| Is the core action understandable? | TBD | TBD | TBD |
| Is the main decision meaningful? | TBD | TBD | TBD |
| Does progression change player strategy? | TBD | TBD | TBD |

## 6. Player, Camera, Input, and Rules

| Area | Initial contract |
|---|---|
| Player avatar / controlled entity | TBD |
| Movement and traversal | TBD |
| Camera and framing | TBD |
| Input actions and rebinding | TBD |
| Interaction targeting | TBD |
| Combat / conflict | TBD / N/A |
| Failure, death, retry, and recovery | TBD |
| Pause and time behavior | TBD |
| Tutorial / onboarding | TBD |
| Accessibility options | TBD |

State any frame-rate, physics, precision, or deterministic behavior that is part of
the player-facing contract.

## 7. Major Game Systems

| System ID | System | Player purpose | Inputs | State / outputs | Dependencies | MVP? |
|---|---|---|---|---|---|---|
| SYS-001 | TBD | TBD | TBD | TBD | TBD | Yes / No |

For each critical system, answer:

- What player decision does it support?
- Who owns authoritative state?
- What starts, updates, pauses, saves, replicates, and tears it down?
- What can fail, and how does the player recover?
- Which values are designer-authored rather than hard-coded?

## 8. Progression, Economy, and Balance

| Topic | Initial design |
|---|---|
| Progression axes | TBD |
| Resources / currencies | TBD / N/A |
| Sources and sinks | TBD / N/A |
| Unlocks and gating | TBD |
| Difficulty model | TBD |
| Loss and recovery | TBD |
| Anti-exploit constraints | TBD |
| Balance-data ownership | ScriptableObject / Table / Remote config / Other / TBD |

List formulas with units, ranges, caps, rounding, and edge-case behavior. Mark every
number as placeholder, prototype-derived, or approved.

## 9. World, Content, Narrative, and Level Structure

| Topic | Initial design |
|---|---|
| Setting and premise | TBD |
| World/level structure | Linear / Hub / Open / Procedural / Session-based / TBD |
| Narrative delivery | TBD / N/A |
| Content categories | TBD |
| Mission / encounter structure | TBD |
| Save/checkpoint structure | TBD |
| Content replayability | TBD |
| Estimated content volume | TBD |

### Content Inventory

| Content type | MVP count | Target count | Reuse strategy | Production risk |
|---|---:|---:|---|---|
| Levels / maps | TBD | TBD | TBD | TBD |
| Characters / enemies | TBD | TBD | TBD | TBD |
| Items / abilities | TBD | TBD | TBD | TBD |
| Narrative units | TBD | TBD | TBD | TBD |

## 10. Art, Rendering, Animation, Audio, UI, and VFX Direction

| Discipline | Direction | Reference | Technical constraint | Approval owner |
|---|---|---|---|---|
| Visual style | TBD | Link / asset | TBD | TBD |
| Render pipeline / lighting expectation | Built-in / URP / HDRP / Custom / TBD | TBD | TBD | TBD |
| Materials / shaders | TBD | TBD | TBD | TBD |
| Animation | TBD | TBD | TBD | TBD |
| VFX | TBD | TBD | TBD | TBD |
| Audio / music | TBD | TBD | TBD | TBD |
| UI / UX | TBD | TBD | TBD | TBD |
| Accessibility presentation | TBD | TBD | TBD | TBD |

Identify source ownership, licensing, target quality tiers, and placeholder versus final
assets. Do not approve a style that cannot meet target-device memory or frame budgets.

## 11. Unity Feasibility Profile

| Field | Initial decision / unknown |
|---|---|
| Unity Editor version | Unity 6.6 / 6000.6.x; exact patch TBD |
| Rendering pipeline and version | TBD |
| Input system | TBD |
| Scripting backend / API level | Mono / IL2CPP / TBD |
| Target build platforms | TBD |
| Physics stack | 2D / 3D / Both / Custom / TBD |
| UI stack | UGUI / UI Toolkit / Mixed / TBD |
| Asset delivery | Direct / Resources / Addressables / AssetBundles / Custom / TBD |
| Multiplayer stack | None / NGO / Entities / Third party / Custom / TBD |
| DOTS / Jobs / Burst candidates | TBD / N/A |
| XR provider / devices | TBD / N/A |
| Required packages / native plug-ins | TBD |

### Feasibility Spikes

| Spike ID | Unknown / risk | Smallest prototype | Timebox | Pass / fail evidence | Owner |
|---|---|---|---|---|---|
| SPIKE-001 | TBD | TBD | TBD days | TBD | TBD |

Burst is a candidate only for measured CPU-bound, data-oriented work compatible with
HPC#. It is not a substitute for Mono or IL2CPP and must not be promised as a general
solution before target-player profiling.

## 12. Scope and Release Slices

| Capability | Prototype | MVP | Vertical slice | Release | Out of scope |
|---|---|---|---|---|---|
| TBD | Yes / No | Yes / No | Yes / No | Yes / No | Notes |

Define the smallest playable proof, the smallest releasable product, and the content
that will be cut first when time or budget changes.

## 13. Prototype and Playtest Plan

| Test ID | Hypothesis | Build / scene | Participants | Method | Pass threshold | Date |
|---|---|---|---|---|---|---|
| TEST-001 | TBD | TBD | TBD | TBD | TBD | YYYY-MM-DD |

Record observations separately from interpretation. Link raw notes, captures, and
telemetry with consent and privacy rules.

## 14. Performance, Memory, and Quality Targets

| Metric | Target platform | Prototype budget | Release budget | Measurement method |
|---|---|---:|---:|---|
| Frame time / FPS | TBD | TBD | TBD | Development Player + Profiler |
| Managed allocation per steady-state frame | TBD | TBD | TBD | Profiler |
| Total / native / GPU memory | TBD | TBD | TBD | Memory Profiler + platform tools |
| Startup / scene load | TBD | TBD | TBD | TBD |
| Build/download size | TBD | TBD | TBD | TBD |
| Network latency/bandwidth | TBD | TBD | TBD | N/A / TBD |

## 15. Risks, Dependencies, and Open Questions

### Risk Register

| ID | Risk | Probability | Impact | Trigger | Mitigation | Contingency | Owner |
|---|---|---|---|---|---|---|---|
| RISK-001 | TBD | Low / Medium / High | Low / Medium / High | TBD | TBD | TBD | TBD |

### Open Questions

| ID | Question | Decision owner | Due | Blocking what? | Status |
|---|---|---|---|---|---|
| Q-001 | TBD | TBD | YYYY-MM-DD | TBD | Open |

### External Dependencies

| Dependency | Version / provider | Needed by | License / cost | Fallback |
|---|---|---|---|---|
| TBD | TBD | YYYY-MM-DD | TBD | TBD |

## 16. Team, Milestones, and Initial Estimate

| Role / discipline | Owner | Capacity | Key responsibility | Gap |
|---|---|---:|---|---|
| TBD | TBD | TBD | TBD | TBD |

| Milestone | Outcome | Entry criteria | Exit evidence | Target date |
|---|---|---|---|---|
| Concept approval | TBD | TBD | TBD | YYYY-MM-DD |
| Prototype gate | TBD | TBD | TBD | YYYY-MM-DD |
| Vertical slice gate | TBD | TBD | TBD | YYYY-MM-DD |

Estimates are ranges with assumptions, confidence, exclusions, and review dates.

## 17. Approval Gate

The concept can advance only when:

- the player fantasy, pillars, core loop, audience, and non-goals are explicit;
- critical hypotheses have timeboxed prototype tests and pass conditions;
- platform, Unity baseline, content scale, staffing, licenses, and major dependencies
  are plausible;
- performance, memory, accessibility, privacy, network, and release constraints are
  acknowledged;
- high-impact risks have owners and contingencies; and
- decision-makers approve the MVP boundary and the next funding/timebox.

| Role | Name | Decision | Date | Conditions |
|---|---|---|---|---|
| Product / design owner | TBD | Approve / Revise / Reject | YYYY-MM-DD | TBD |
| Technical owner | TBD | Approve / Revise / Reject | YYYY-MM-DD | TBD |
| Art / content owner | TBD | Approve / Revise / Reject | YYYY-MM-DD | TBD |

## 18. Decision Log

| ID | Date | Decision | Alternatives | Reason / evidence | Owner | Revisit trigger |
|---|---|---|---|---|---|---|
| DEC-001 | YYYY-MM-DD | TBD | TBD | TBD | TBD | TBD |

## 19. References and Appendices

- Player research: TBD
- Competitive references: TBD
- Prototype repository/build: TBD
- Art/audio references and licenses: TBD
- Technical spikes: TBD
- Terms and glossary: TBD
