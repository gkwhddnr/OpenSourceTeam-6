# Game Development Plan Template — Unity

Use this plan after the initial game design is approved. It converts product intent
into owned technical, content, test, production, and release contracts. Replace `TBD`
with verified project facts. Link requirements and evidence rather than duplicating
large source documents.

## 1. Document Control

| Field | Value |
|---|---|
| Project | TBD |
| Document ID | DEV-PLAN-TBD |
| Version / status | 0.1.0 / Draft |
| Owner | TBD |
| Technical approver | TBD |
| Product/design approver | TBD |
| Created / updated | YYYY-MM-DD / YYYY-MM-DD |
| Covered release / milestone | TBD |
| Repository / branch baseline | URL or path / commit |
| Unity baseline | Unity 6.6 / 6000.6.x; exact patch + revision |

### Revision History

| Version | Date | Author | Material change | Approved by |
|---|---|---|---|---|
| 0.1.0 | YYYY-MM-DD | TBD | Initial plan | Pending |

## 2. Inputs and Requirement Traceability

| Source ID | Source document / issue | Approved version | Owner | Relevant sections |
|---|---|---|---|---|
| SRC-001 | Initial GDD | TBD | TBD | TBD |

| Requirement ID | Requirement | Source ID | Implementation unit | Test / evidence | Status |
|---|---|---|---|---|---|
| REQ-001 | TBD | SRC-001 | SYS-001 / issue link | TEST-001 | Planned |

State conflict resolution and approval authority when design, executable behavior,
tests, serialized assets, and this plan disagree.

## 3. Outcome, Scope, and Acceptance

### Intended Outcome

TBD

### In Scope

- TBD

### Out of Scope

- TBD

### Release Acceptance

| ID | Player-visible or operational acceptance criterion | Verification | Owner |
|---|---|---|---|
| AC-001 | TBD | Test/build/playtest link | TBD |

### Assumptions and Constraints

| ID | Assumption / constraint | Evidence | Expiry / review date | Owner |
|---|---|---|---|---|
| CON-001 | TBD | TBD | YYYY-MM-DD | TBD |

## 4. Verified Unity Project Baseline

| Area | Verified selection | Version / asset | Evidence path |
|---|---|---|---|
| Unity Editor | TBD | TBD | `ProjectSettings/ProjectVersion.txt` |
| Render pipeline | Built-in / URP / HDRP / Custom | TBD | TBD |
| Scripting backend / API | Mono / IL2CPP / TBD | TBD | Project Settings |
| Build targets | TBD | TBD | TBD |
| Input | Legacy / Input System / Custom | TBD | TBD |
| Physics | 2D / 3D / Both / Custom | TBD | TBD |
| UI | UGUI / UI Toolkit / Mixed | TBD | TBD |
| Asset delivery | Direct / Addressables / Other | TBD | TBD |
| Multiplayer | None / package / custom | TBD | TBD |
| DOTS / Collections / Jobs / Burst | Optional versions; Burst built-in in 6.6 | TBD | Editor patch + manifest/lock |
| XR | None / provider / devices | TBD | TBD |
| CI / build automation | TBD | TBD | TBD |

List required packages, plug-ins, MCP servers, native libraries, licenses, pinned
versions, supported targets, installation owner, upgrade procedure, and rollback.

## 5. Architecture and Dependency Direction

### Context Diagram

Describe players, game clients, servers, platform services, content delivery, build
systems, analytics, and external tools. Link the maintained diagram: TBD.

### Unity Assembly / Package Matrix

| Assembly / package | Layer | Responsibility | Public API | Dependencies | Editor? | Tests |
|---|---|---|---|---|---|---|
| TBD | Core / Runtime / Adapter / Editor / Test | TBD | TBD | TBD | Yes / No | TBD |

Rules:

- Domain/Core policy does not depend on `UnityEngine` or `UnityEditor` unless explicitly
  justified.
- Runtime assemblies never depend on Editor assemblies.
- Feature ownership, composition roots, service lifetime, and teardown are explicit.
- ScriptableObject definitions, runtime state, and Addressables delivery remain
  separate dependency graphs.

### Runtime Ownership

| Lifetime | Owner / composition root | Services / state | Create | Reset / teardown |
|---|---|---|---|---|
| Application | TBD | TBD | TBD | TBD |
| Session | TBD | TBD | TBD | TBD |
| Scene / world | TBD | TBD | TBD | TBD |
| Player / entity | TBD | TBD | TBD | TBD |

## 6. System Implementation Matrix

| ID | System | Owner | State authority | Inputs | Outputs / API | Dependencies | Save | Network | Failure behavior |
|---|---|---|---|---|---|---|---|---|---|
| SYS-001 | TBD | TBD | TBD | TBD | TBD | TBD | Yes / No | Role / N/A | TBD |

For every major system, link:

- functional and non-functional requirements;
- state machine or decision model;
- public interfaces, DTOs, events, commands, and versioning;
- initialization, update cadence, pause, cancellation, reset, and teardown;
- configuration/ScriptableObject ownership and runtime-copy policy;
- failure, retry, rollback, and observability;
- tests and content validation; and
- migration impact for saves, assets, APIs, scenes, prefabs, and network contracts.

## 7. Data, API, Save, CRUD, and Security Contracts

| Contract | Version | Producer | Consumer | Schema / units | Validation | Compatibility |
|---|---|---|---|---|---|---|
| TBD | v1 | TBD | TBD | Link | TBD | TBD |

Document DTO -> interface -> service -> domain -> repository/gateway -> controller
flow. Define create idempotency, read authorization/paging, update conflict strategy,
delete/retention/restore, transactions or compensation, audit, cancellation, retry,
error mapping, rate/size/time bounds, and privacy classification where applicable.

## 8. Asset and Content Production Plan

| Asset class | Source owner | Destination | Import preset | Naming / GUID policy | LFS / lock | Validation |
|---|---|---|---|---|---|---|
| TBD | TBD | `Assets/...` | TBD | TBD | TBD | TBD |

Include:

- source-file and license provenance;
- `.meta`/GUID preservation and reference-sensitive move policy;
- prefab/scene ownership and additive-loading strategy;
- animation, Timeline, material, shader, audio, VFX, sprite, terrain, and localization
  pipelines;
- Addressables groups, labels, profiles, catalogs, handles, content-update policy,
  remote hosting, and rollback;
- third-party asset update, local override, removal, and compatibility procedures; and
- content memory, quality, build-size, import-time, and platform budgets.

## 9. Execution, Async, Jobs, and Burst Plan

### Workload Matrix

| Workload | Frequency / scale | Owning thread | Model | Cancellation | Publish point | Budget |
|---|---|---|---|---|---|---|
| TBD | TBD | Main / Worker / Job | Sync / Awaitable / Task / Coroutine / Job | TBD | TBD | TBD |

### Job System Contract

| Job / pipeline | Input owner | Output owner | NativeContainers / allocator | Dependencies | Batch size | Complete / dispose owner |
|---|---|---|---|---|---|---|
| TBD | TBD | TBD | TBD | TBD | Measured / TBD | TBD |

### Burst Candidate Record

| Entry point | Measured bottleneck | Data layout | Burst/HPC# compatible? | Target value | Decision |
|---|---|---|---|---|---|
| TBD | Capture link | SoA / AoS / Other | Yes / No / Spike | TBD | Adopt / Reject / Revisit |

For each adopted Burst boundary, record:

| Setting / contract | Value / evidence |
|---|---|
| Built-in Burst baseline | Unity 6.6; exact Editor patch and settings TBD |
| `[BurstCompile]` entry point and call path | TBD |
| Editor JIT and Player AOT targets | TBD |
| `OptimizeFor` | Balanced / Performance / Size / FastCompilation / verified enum |
| `FloatMode` / `FloatPrecision` | TBD; justify numerical effect |
| NaN, infinity, signed zero, overflow, and determinism rules | TBD |
| Aliasing / unsafe assumptions | None / proof and tests |
| Safety checks | Diagnostic policy; exceptions require approval and evidence |
| Synchronous compilation | Default async / justified exception |
| Function pointers / `SharedStatic` | N/A / ownership and initialization contract |
| Burst-disabled fallback | Test and behavior evidence |
| Inspector / generated-code review | Link / N/A with reason |
| Target-device benchmark | Scenario, warmup, distribution, before/after, tradeoff |

Burst supplements Mono/IL2CPP; it does not replace the scripting backend. Keep a
correct non-Burst path. Never disable safety checks, add no-alias assumptions, force
synchronous compilation, or choose fast floating-point behavior without a measured
need, reviewed numerical contract, and target-build tests.

## 10. Memory and Resource Lifetime Plan

| Resource | Kind | Owner | Allocate/load | Capacity / peak | Release/unload | Failure path | Evidence |
|---|---|---|---|---|---|---|---|
| TBD | Managed / Unity native / NativeContainer / Asset / GPU / OS / Plug-in | TBD | TBD | TBD | TBD | TBD | TBD |

Define steady-state GC allocation budgets, pool bounds, scene/load transition policy,
Addressables/AssetBundle release, NativeContainer/job completion, created
`UnityEngine.Object` destruction, GPU/fence lifetime, domain-reload-disabled reset,
and repeated load/unload leak tests. Forced collection or unused-asset unloading
requires a profiled transition and accepted hitch.

## 11. Performance and Rendering Plan

| Metric | Scenario | Target device | Budget | Tool / marker | Gate |
|---|---|---|---:|---|---|
| Main-thread frame time | TBD | TBD | TBD ms | Profiler | TBD |
| Render-thread / GPU | TBD | TBD | TBD ms | Profiler / platform GPU tool | TBD |
| GC allocation / pause | TBD | TBD | TBD | Profiler | TBD |
| Native / GPU memory | TBD | TBD | TBD MB | Memory Profiler / platform tool | TBD |
| Load time / hitch | TBD | TBD | TBD | TBD | TBD |

Record pipeline/renderer assets, render features/passes, shaders/variants, materials,
lighting, post-processing, cameras, XR, batching/instancing, GPU resources, visual
baselines, supported graphics APIs, and quality/scalability tiers.

## 12. Networking and Role Matrix

| Mode / role | Authority | Owned objects | State sync | Commands/messages | Scene/session behavior | Test |
|---|---|---|---|---|---|---|
| Offline / Host / Server / Client / Spectator | TBD | TBD | TBD | TBD | TBD | TBD |

Define transport/session manager lifecycle, authentication, compatibility handshake,
spawn/despawn, late join, reconnect, prediction/correction, interest/relevancy,
bandwidth/rate limits, failure/timeout, scene changes, delegate cleanup, and dedicated
server behavior. Mark this section N/A only with an explicit offline decision.

## 13. Build, Platform, and Deployment Plan

| Target | Backend | Configuration | Graphics API | Signing / store | Build command | Smoke test |
|---|---|---|---|---|---|---|
| TBD | Mono / IL2CPP | Dev / Release | TBD | TBD | TBD | TBD |

Document scripting symbols, AOT/stripping/link preservation, Burst AOT settings,
native plug-ins, permissions, package manifests/lock files, Addressables build/content
update, CI secrets, artifact retention, versioning, rollout, rollback, crash symbols,
and release owner.

## 14. Testing and Validation Strategy

| Test ID / suite | Layer | Requirement | Environment | Data / fixture | Pass condition | Owner |
|---|---|---|---|---|---|---|
| TEST-001 | EditMode / PlayMode / Player / Device / Content / Network | REQ-001 | TBD | TBD | TBD | TBD |

Include deterministic unit/domain tests, scene/prefab/content checks, asset reference
validation, save migration, network role tests, failure/cancellation/teardown, repeated
load/unload, Burst on/off and safety variants, IL2CPP/AOT/stripping, rendering/visual,
accessibility, localization, security, performance, and target-device smoke tests.

## 15. Observability and Development Diagnostics

| Signal | Purpose | Owner | Enabled where | Retention / privacy | Alert / gate |
|---|---|---|---|---|---|
| Log / metric / trace / profiler marker | TBD | TBD | Editor / Dev / Release | TBD | TBD |

Define structured logs, correlation/session IDs, profiler markers, debug UI, network
diagnostics, crash reports, telemetry consent, secret/personal-data redaction, and the
procedure for collecting reproducible evidence.

## 16. Work Breakdown and Milestones

| Milestone | Outcome | Entry | Exit evidence | Target | Owner | Confidence |
|---|---|---|---|---|---|---|
| TBD | TBD | TBD | TBD | YYYY-MM-DD | TBD | Low / Medium / High |

| Work ID | Deliverable | Requirement | Dependencies | Estimate range | Owner | Issue / branch | Status |
|---|---|---|---|---|---|---|---|
| WORK-001 | TBD | REQ-001 | TBD | TBD | TBD | Link | Planned |

Estimates include assumptions, exclusions, review date, integration/content/test time,
and confidence. Do not report percentage complete without a defined denominator and
evidence.

## 17. Ownership and Communication

| Area / decision | Responsible | Accountable | Consulted | Informed | Escalation |
|---|---|---|---|---|---|
| TBD | TBD | TBD | TBD | TBD | TBD |

Record review cadence, design/technical decision channels, binary asset ownership,
lock policy, incident escalation, handoff requirements, and documentation owners.

## 18. Risk, Dependency, and Decision Registers

| Risk ID | Risk | Probability | Impact | Trigger | Mitigation | Contingency | Owner |
|---|---|---|---|---|---|---|---|
| RISK-001 | TBD | L/M/H | L/M/H | TBD | TBD | TBD | TBD |

| Dependency | Version / owner | Needed by | Failure impact | Fallback | Status |
|---|---|---|---|---|---|
| TBD | TBD | YYYY-MM-DD | TBD | TBD | TBD |

| Decision ID | Date | Decision | Alternatives | Evidence | Owner | Revisit trigger |
|---|---|---|---|---|---|---|
| DEC-001 | YYYY-MM-DD | TBD | TBD | TBD | TBD | TBD |

## 19. VCS, Change, and Release-Control Plan

- Branch/changelist model: TBD
- Commit format and issue linkage: TBD
- Git LFS / Unity Version Control / Perforce locks: TBD
- Scene/prefab ownership and UnityYAMLMerge policy: TBD
- Package/engine upgrade isolation: TBD
- Migration and rollback approval: TBD
- Generated file and secret exclusions: TBD
- Definition of reviewed/staged/releasable: TBD

## 20. Definition of Ready and Done

### Work Item Ready

- requirement, acceptance criteria, owner, dependencies, assets, risks, and test level
  are known;
- design/API/content contracts are reviewed;
- required files are editable and no conflicting owner is active; and
- target platform and migration impact are identified.

### Work Item Done

- acceptance criteria are met and traced to evidence;
- Unity compilation and applicable EditMode, PlayMode, Player, device, network, and
  content validation pass;
- lifecycle, async, resource, GC, Burst, save, asset/GUID, and compatibility contracts
  are verified where affected;
- documentation, migration, telemetry, and rollback are updated;
- diff/review contains no unrelated changes, generated output, secrets, missing `.meta`
  files, or unsupported claims; and
- remaining risks and unverified targets are explicit.

## 21. Approval

| Role | Name | Decision | Date | Conditions / follow-up |
|---|---|---|---|---|
| Product/design | TBD | Approve / Revise / Reject | YYYY-MM-DD | TBD |
| Technical | TBD | Approve / Revise / Reject | YYYY-MM-DD | TBD |
| Art/content | TBD | Approve / Revise / Reject | YYYY-MM-DD | TBD |
| QA/release | TBD | Approve / Revise / Reject | YYYY-MM-DD | TBD |

## 22. Appendices

- Architecture and data-flow diagrams: TBD
- API/schema specifications: TBD
- Content inventory and license register: TBD
- Performance captures and memory snapshots: TBD
- Burst Inspector / benchmark evidence: TBD
- Test matrix and build artifacts: TBD
- Migration and rollback runbooks: TBD
- Glossary: TBD
