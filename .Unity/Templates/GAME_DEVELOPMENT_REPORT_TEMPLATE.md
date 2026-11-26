# Game Development Report Template — Unity

Use this for a sprint, week, milestone, release candidate, incident follow-up, or other
defined reporting period. Report observed outcomes and evidence. Do not infer status
from commit count, hours spent, or unverified Editor behavior.

## 1. Report Control

| Field | Value |
|---|---|
| Project | TBD |
| Report ID | DEV-REPORT-YYYY-MM-DD-TBD |
| Report type | Weekly / Sprint / Milestone / Release / Incident / Other |
| Period | YYYY-MM-DD through YYYY-MM-DD |
| Version / status | 1.0.0 / Draft or Approved |
| Author / owner | TBD |
| Reviewers | TBD |
| Repository / branch | TBD |
| Start and end commits | TBD ... TBD |
| Unity Editor / package baseline | TBD |
| Covered build(s) | TBD |

## 2. Executive Status

| Dimension | Status | Evidence-based summary | Owner |
|---|---|---|---|
| Scope | Green / Amber / Red / Unknown | TBD | TBD |
| Schedule | Green / Amber / Red / Unknown | TBD | TBD |
| Quality | Green / Amber / Red / Unknown | TBD | TBD |
| Technical risk | Green / Amber / Red / Unknown | TBD | TBD |
| Content production | Green / Amber / Red / Unknown | TBD | TBD |
| Performance / memory | Green / Amber / Red / Unknown | TBD | TBD |
| Release readiness | Green / Amber / Red / N/A | TBD | TBD |

### Outcome Summary

- Delivered: TBD
- Not delivered: TBD
- Player or team impact: TBD
- Decision or help required: TBD

## 3. Period Goals and Results

| Goal ID | Planned outcome | Acceptance criterion | Result | Evidence | Status |
|---|---|---|---|---|---|
| GOAL-001 | TBD | TBD | TBD | Build/test/issue link | Done / Partial / Missed / Cancelled |

Explain partial or missed outcomes with cause, impact, recovery action, owner, and new
date. Do not silently carry them forward.

## 4. Milestone and Delivery Progress

| Milestone / release | Planned date | Forecast | Confidence | Completed gates | Remaining gates |
|---|---|---|---|---|---|
| TBD | YYYY-MM-DD | YYYY-MM-DD | L/M/H | TBD | TBD |

If reporting percentage complete, define the denominator, weighting, evidence, and
scope changes. Otherwise report completed and remaining acceptance gates.

## 5. Completed Development Work

| Work / issue | Requirement | Outcome | Changed systems/files/assets | Validation | Owner |
|---|---|---|---|---|---|
| TBD | REQ-001 | TBD | Links / paths | Tests/builds | TBD |

Separate:

- runtime code and architecture;
- editor tools and automation;
- scenes, prefabs, ScriptableObjects, Addressables, and other content;
- rendering, material, animation, audio, VFX, UI, input, terrain, and XR work;
- network, platform, backend/service, security, and build pipeline work; and
- documentation, migration, and developer-experience work.

## 6. Design and Scope Changes

| Change ID | Previous contract | New contract | Reason / evidence | Impact | Approval | Migration |
|---|---|---|---|---|---|---|
| CHG-001 | TBD | TBD | TBD | Scope/schedule/API/asset/save/network | TBD | TBD |

List rejected or deferred changes and their revisit trigger.

## 7. Unity Project and Dependency Changes

| Area | Before | After | Reason | Compatibility / rollback | Validation |
|---|---|---|---|---|---|
| Unity / package / plug-in / asmdef / setting | TBD | TBD | TBD | TBD | TBD |

Report package manifest/lock changes, third-party licenses, native plug-ins, scripting
backend, rendering/input/network/XR packages, assembly boundaries, Project Settings,
and generated/reserialized content separately.

## 8. Build, Test, and Content Validation

| Check | Target / environment | Result | Count / duration | Artifact / log | Not run reason |
|---|---|---|---|---|---|
| Unity script compilation | Editor version | Pass / Fail / Not run | TBD | Link | TBD |
| EditMode | TBD | Pass / Fail / Not run | TBD | Link | TBD |
| PlayMode | TBD | Pass / Fail / Not run | TBD | Link | TBD |
| Player build | Platform/config | Pass / Fail / Not run | TBD | Link | TBD |
| Device smoke | Device/OS | Pass / Fail / Not run | TBD | Link | TBD |
| Content/reference validation | Scope | Pass / Fail / Not run | TBD | Link | TBD |
| Network roles | Roles/topology | Pass / Fail / N/A | TBD | Link | TBD |

Record failed, flaky, skipped, quarantined, and newly added tests. A compile or test
must not be reported as passing unless its result was observed for the stated target.

## 9. Performance, Memory, and Burst Evidence

### Capture Context

| Field | Value |
|---|---|
| Scenario and duration | TBD |
| Commit / build | TBD |
| Device / OS | TBD |
| Backend / graphics API / quality / resolution | TBD |
| Warmup / frame cap / VSync | TBD |
| Profiler overhead / tools | TBD |

### Results

| Metric | Baseline | Current | Budget | Delta | Interpretation / tradeoff | Evidence |
|---|---:|---:|---:|---:|---|---|
| Main-thread frame time | TBD | TBD | TBD | TBD | TBD | Link |
| GPU frame time | TBD | TBD | TBD | TBD | TBD | Link |
| GC allocation / pause | TBD | TBD | TBD | TBD | TBD | Link |
| Native / GPU memory | TBD | TBD | TBD | TBD | TBD | Link |
| Load time / peak memory | TBD | TBD | TBD | TBD | TBD | Link |

### Burst Change Record

| Entry point | Editor patch / built-in Burst settings | Burst off | Burst on | Safety variant | AOT target | Decision |
|---|---|---:|---:|---:|---|---|
| TBD / N/A | TBD | TBD | TBD | TBD | TBD | Keep / Revert / Investigate |

For Burst work, state `OptimizeFor`, float mode/precision, compile mode, data layout,
job/batch size, numerical/determinism results, Inspector/generated-code observations,
and measured tradeoffs. Disclose missing Burst-disabled, safety-check, IL2CPP/AOT, or
target-device coverage.

## 10. Memory and Resource-Lifetime Results

| Scenario / cycle | Managed retained | Unity native | NativeContainer / allocator | Loaded assets | GPU | Leak / action |
|---|---:|---:|---:|---:|---:|---|
| TBD | TBD | TBD | TBD | TBD | TBD | TBD |

Report repeated load/unload, scene transition, Play Mode/domain reload, Addressables
release, jobs/disposal, event/static roots, created object destruction, GPU-resource
release, forced collection/unload use, and any unverified lifetime path.

## 11. Defects and Incidents

| ID | Severity | Symptom / reproduction | Root cause status | User impact | Fix / workaround | Owner / due |
|---|---|---|---|---|---|---|
| BUG-001 | Critical / High / Medium / Low | TBD | Confirmed / Suspected / Unknown | TBD | TBD | TBD |

Include regressions, crashes, data loss, build failures, performance hitches, content
reference issues, network failures, and production incidents. Link evidence and avoid
claiming a root cause before it is verified.

## 12. Risks, Blockers, and Dependencies

| ID | Type | Description | Probability / impact | Blocking | Mitigation / ask | Owner | Due |
|---|---|---|---|---|---|---|---|
| TBD | Risk / Blocker / Dependency | TBD | L/M/H | TBD | TBD | TBD | YYYY-MM-DD |

Highlight expired assumptions, unsupported package/platform combinations, asset or
license blockers, missing decisions, unavailable test devices, and staffing constraints.

## 13. Decisions and Approvals

| Decision ID | Date | Decision | Alternatives | Evidence | Approver | Follow-up / revisit |
|---|---|---|---|---|---|---|
| DEC-001 | YYYY-MM-DD | TBD | TBD | TBD | TBD | TBD |

## 14. VCS, Build, and Release State

| Item | State / evidence |
|---|---|
| Branch / changelist health | TBD |
| Commits / pull requests | TBD |
| Asset `.meta`, GUID, LFS, and lock status | TBD |
| Package/engine/asset migrations | TBD |
| CI and build artifacts | TBD |
| Release candidate / deployment | TBD / N/A |
| Rollback readiness | TBD |
| Generated output / secrets audit | TBD |

## 15. Next Reporting Period

| Priority | Planned outcome | Acceptance criterion | Dependency | Owner | Target |
|---|---|---|---|---|---|
| P0 / P1 / P2 | TBD | TBD | TBD | TBD | YYYY-MM-DD |

State planned cuts or contingencies if the current forecast is not met.

## 16. Action Items

| Action ID | Action | Owner | Due | Verification / close condition | Status |
|---|---|---|---|---|---|
| ACT-001 | TBD | TBD | YYYY-MM-DD | TBD | Open |

## 17. Sign-Off

| Role | Name | Status | Date | Comment / condition |
|---|---|---|---|---|
| Report owner | TBD | Submitted | YYYY-MM-DD | TBD |
| Technical reviewer | TBD | Approved / Changes requested | YYYY-MM-DD | TBD |
| Product/design reviewer | TBD | Approved / Changes requested | YYYY-MM-DD | TBD |

## 18. Attachments

- Diff / pull requests: TBD
- Build artifacts: TBD
- Test logs: TBD
- Profiler and Memory Profiler captures: TBD
- Burst Inspector / benchmark evidence: TBD
- Screenshots / video / playtest notes: TBD
- Updated design, API, migration, or release documents: TBD
