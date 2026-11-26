# Unity AI Platform Setup

Baseline: Unity 6.6 (`6000.6.x`). Read the `Unity 6.6 Baseline` section
in the adjacent `PROJECT_CONVENTION.md`; verify the exact project patch.

## 1. Structure

| Tool | Project instruction location |
|---|---|
| AGENTS-compatible agents, including Codex | `AGENTS.md` |
| JetBrains AI Assistant | `.aiassistant/rules/*.md` |
| Junie | `.junie/AGENTS.md`, then root `AGENTS.md` |
| GitHub Copilot | `.github/copilot-instructions.md` and `.github/instructions/*.instructions.md` |

All adapters use `AGENTS.md` and `PROJECT_CONVENTION.md` as the detailed sources of
truth.

## 2. JetBrains AI Assistant in Rider

1. Copy `.aiassistant/rules/unity-engineering.md` into the Unity project.
2. In Rider, open **Settings | Tools | AI Assistant | Rules**.
3. Set `unity-engineering.md` to **Always**.
4. Optionally configure `CODE_REVIEW_RULES.md` as the AI Self-Review rules path in
   the AI Assistant project settings.

Rule applicability is configured in the IDE. Do not add undocumented YAML
frontmatter to the AI Assistant rule file.

An empty root `.noai` file disables JetBrains AI Assistant for the project. Do not
add or remove it incidentally.

## 3. Junie

Current Junie project discovery checks:

1. `.junie/AGENTS.md`;
2. root `AGENTS.md`; and
3. legacy `.junie/guidelines.md` or `.junie/guidelines/`.

This package uses the first file as a short entry point to the detailed root policy.
Do not maintain a conflicting legacy guideline.

Global Junie instructions can be stored at:

- Windows: `%USERPROFILE%\.junie\AGENTS.md`
- macOS/Linux: `~/.junie/AGENTS.md`

Keep personal preferences global and Unity project facts local.

## 4. GitHub Copilot

Repository-wide instructions are located at:

```text
.github/copilot-instructions.md
```

Path-specific instructions are located at:

```text
.github/instructions/*.instructions.md
```

The package separates code/build configuration from broader Unity content. Narrow
the `applyTo` patterns if the repository contains non-Unity projects.

Copilot capabilities and preview status vary by IDE/plugin version and account.
Use the Copilot Customizations UI, when available, to verify which workspace and
personal instructions are active. Avoid contradictory UI and repository rules.

## 5. Other AI Tools

For an agent that reads `AGENTS.md`, copy the root files unchanged. For tools that
require another filename, create a concise native adapter that directs the tool to
read `AGENTS.md` and `PROJECT_CONVENTION.md` rather than duplicating the policies.

Examples:

- Claude: a short root `CLAUDE.md` loader;
- Cursor: a scoped `.cursor/rules/*.mdc` loader;
- Kiro: an always-included `.kiro/steering/*.md` loader; and
- OpenCode or other agents: configure their documented project-instruction entry
  point to load the two root files.

Verify current vendor documentation before adding native metadata because formats
and precedence can change.

## 6. Plugin, MCP, and VCS Setup

### 6.1 Unity Editor Automation and MCP

Unity-aware Editor automation is preferred for changes to GameObjects, components,
scenes, prefabs, serialized assets, import settings, and reference-sensitive asset
operations.

Before state-changing automation:

1. verify the Unity project and exact Editor instance;
2. inspect compilation and Play Mode state;
3. check the active scene, current selection, and unsaved scenes/assets;
4. inspect repository status and locked assets;
5. use the narrowest operation and verify the result in the Editor; and
6. save only authorized content.

Do not assume that a tool response means serialization, compilation, import, or disk
save completed. Verify the corresponding Unity state.

### 6.2 Setup Gates

Before enabling these integrations:

1. complete the plug-in inventory and platform/backend matrix in
   `PROJECT_CONVENTION.md` Section 26;
2. register the MCP server, pinned version, transport, capabilities, roots/tools,
   credentials, and recovery test described in Section 27;
3. configure the authoritative VCS provider, visible metadata, text serialization,
   merge driver, LFS/large-file rules, locks/checkouts, and workspace model described
   in Section 28;
4. test a read-only MCP request and a disposable transactional Editor mutation,
   including timeout/rollback behavior; and
5. test a disposable asset create, move, rename, checkout/lock, merge, and revert so
   asset/`.meta` pairing and UnityYAMLMerge behavior are known before production use.

## 7. `.aiignore` and Privacy

Review `.aiignore.example`, then rename it to `.aiignore` only when appropriate.
JetBrains documents `.aiignore` and `.noai` as AI Assistant controls; they do not
guarantee restrictions for third-party plugins or agents.

Use defense in depth:

- never store production secrets in the project;
- configure ignore/access rules for each provider;
- grant agents and MCP servers least privilege;
- avoid uploading private source and assets to unapproved services; and
- confirm external destinations before transmission.

Do not ignore all of `Assets/`, `Packages/`, or `ProjectSettings/`; doing so removes
essential Unity context. Exclude sensitive or generated subpaths precisely.

## 8. Tailoring Checklist

Complete the project profile with:

- exact Unity Editor version and upgrade policy;
- target platforms, backend, API level, and stripping;
- render-pipeline package/version, pipeline and renderer assets by quality/target,
  graphics APIs/shader targets, HDR/color space, dynamic resolution/XR, shader
  variant/prewarming policy, rendering budgets/reference scenes, and profiling tools;
- Profiler/statistics capture scenarios, target frame/CPU/GPU/memory budgets, CPU/GPU
  allocation/upload/readback/residency policy, reference devices, and evidence format;
- XR provider/OpenXR/vendor, AR Foundation/XR Interaction Toolkit, tracking origin,
  interaction, comfort, stereo, permissions, supported devices, and fallback policy;
- input, UI, physics, networking, and asset-delivery stacks;
- first/third-person, side-scroller, 2D/2.5D/3D gameplay space, camera, physics,
  local-player, and authoritative network policy;
- network manager/bootstrap, offline/Host/listen/dedicated/Client roles, transport,
  session/relay/lobby, scenes/prefabs, tick, authority, and delegate lifecycle policy;
- Task/Awaitable/coroutine/Job System continuation, cancellation, queue, worker,
  synchronization, lock-order, timeout, and shutdown policy;
- Core/Unity Runtime/Editor dependency direction and DTO/interface/service/controller/
  CRUD ownership and error/versioning policy;
- practical SOLID variation points, composition roots, and allowed dependency seams;
- AI decision model/package, blackboard and abort contracts, agent-scale budgets;
- Singleton/Observer/EventBus scopes and delivery/reentrancy policy, algorithm
  complexity/determinism/numeric limits, and preprocess/postprocess publication;
- shared-state owners, race tests, Burst target/float/safety/AOT settings, and
  Update/PlayerLoop phase, cadence, scheduler, relevance, and catch-up policy;
- Addressables version/Profile/groups/catalog/CDN/cache/content-update/handle policy
  and Web origins/CORS/authentication configuration;
- source-file/`.unitypackage`/UPM import and reimport policy;
- Terrain tiles/source data/streaming/collision policy, collision matrices and query
  policy, and Tag/Layer/Sorting Layer/mask ownership;
- asmdef/asmref names, GUID/reference mode, defines/platforms, base-class policy, and
  documented built-in/package API reference rules;
- animation rig/controller/root-motion policy and Timeline/Playable bindings;
- audio mixer/import/concurrency policy, VFX stack/budgets, sprite atlas/PPU/sorting
  policy, and GameObject/persistent-object ownership;
- package and assembly boundaries;
- bootstrap and scene-loading strategy;
- domain/scene reload policy;
- save and serialized-data migration policy;
- test and content-validation commands;
- build commands and platform modules; and
- performance budgets and target hardware.

Delete unused technology guidance so agents do not infer systems that are not
present.

## 9. Verification Checklist

1. Ask the agent to identify the Unity version, packages, render pipeline, input,
   backend, and targets without editing.
2. Confirm it read `AGENTS.md` and `PROJECT_CONVENTION.md`.
3. Confirm every Copilot path rule matches intended files.
4. Verify `.aiignore` with a harmless read-only request.
5. Run a script-only change and confirm Unity recompiles without new errors.
6. Run the applicable EditMode and PlayMode test paths.
7. Test an asset operation on disposable content and verify `.meta` handling before
   trusting automation with production assets.
8. On disposable content, import/reimport a representative source asset and package;
   verify importer settings, selected contents, `.meta`/subasset identity, compile,
   references, and target-platform output.
9. Exercise a disposable Terrain tile and collision/query fixture; verify seams,
   TerrainCollider, 2D/3D matrices, tags/layers/masks, triggers, and buffer edges.
10. Compile representative runtime/editor/test asmdefs and a base-class/component
   reference path under the applicable defines and target.
11. Exercise synchronous/async/Awaitable and Job System fixtures for main/background
    transitions, cancellation, exceptions, lock ordering, worker saturation, job
    dependencies, NativeContainer disposal, teardown, and restricted-thread targets.
12. Exercise CRUD DTO mappings and Controller-Service-repository boundaries for auth,
    conflict, paging, idempotency, partial failure, cancellation, and error redaction.
13. Exercise Addressables with clean/warm/offline/interrupted cache, remote catalog,
    handle release, scene/instance unload, content rollback, and a hosted Web CORS test.
14. On disposable content, exercise Animator transitions and root motion, Timeline
   play/seek/loop, audio routing/voice cleanup, VFX stop/reuse, sprite atlas references,
   and GameObject activation/parenting/persistence.
15. Open a representative rendering scene, inspect it with Frame Debugger or the
   pipeline's render-graph/debug tooling, and verify a target Player/API path.
16. Profile a representative Player scenario and compare CPU/main/render/worker/job,
    GPU, GC, managed/native/GPU memory, loading, and network evidence against budgets;
    verify resource uploads/readbacks, residency, and release.
17. Exercise supported XR devices or documented simulators for tracking origin,
    camera/hand/controller poses, interaction/locomotion, comfort, stereo views,
    permissions, tracking loss, recenter, suspend/resume, and target performance.
18. Exercise every supported perspective/gameplay space, including camera collision,
    local-player ownership, 2D-versus-3D physics, side-scroller plane/depth behavior,
    respawn/travel, aspect ratio, and remote proxy behavior.
19. Exercise offline, single-player, Host/listen/dedicated server and multi-client
    network flows as applicable, including delegate cleanup, approval failure,
    loss/jitter, reconnect, late join, scene change, shutdown, and version mismatch.
20. Exercise Behavior Tree/Unity Behavior or the selected FSM/Utility/GOAP model for
   node/state status, abort and target loss, blackboard ownership, event wakeups,
   deterministic ties, traceability, and maximum-agent budgets.
21. Stress Observer/EventBus subscription mutation, reload and teardown, async/job
   races, stale generations, Burst on/off and safety/AOT variants, plus custom update
   scheduling at target object counts. Compare correctness and Player profiles.
22. Exercise a representative preprocess/execute/postprocess pipeline for repeated
   input, cache invalidation, cancellation, partial failure, deterministic merge,
   atomic publication, rollback, and cleanup; benchmark algorithm edge and worst-case
   inputs against declared bounds.
23. Inspect Git status for generated files, captures, unintended resaves, lighting or
   material churn, and local settings.

## 10. Maintenance

Review these instructions whenever the Unity version, packages, plug-ins, assembly
layout, render/input/UI/network stack, target platforms, serialization, VCS/LFS/lock
policy, CI, AI provider, or MCP configuration changes. Remove stale and duplicated
rules promptly.

## 11. Official References

- Unity asset metadata:
  https://docs.unity3d.com/6000.6/Documentation/Manual/AssetMetadata.html
- Unity script serialization:
  https://docs.unity3d.com/6000.6/Documentation/Manual/script-serialization.html
- Unity domain reload:
  https://docs.unity3d.com/6000.6/Documentation/Manual/domain-reloading.html
- Unity Test Framework:
  https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.test-framework.html
- Unity 6.6 garbage collector:
  https://docs.unity3d.com/6000.6/Documentation/Manual/performance-garbage-collector.html
- Unity Profiler and Highlights module:
  https://docs.unity3d.com/6000.6/Documentation/Manual/Profiler.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/ProfilerHighlights.html
- Unity XR input and XR Interaction Toolkit:
  https://docs.unity3d.com/6000.6/Documentation/Manual/xr_input.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.xr.interaction.toolkit.html
- Netcode for GameObjects NetworkManager:
  https://docs-multiplayer.unity3d.com/netcode/current/components/networkmanager/
- Unity animation system overview:
  https://docs.unity3d.com/6000.6/Documentation/Manual/AnimationOverview.html
- Unity Timeline package:
  https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/index.html
- Unity audio:
  https://docs.unity3d.com/6000.6/Documentation/Manual/Audio.html
- Unity particle systems:
  https://docs.unity3d.com/6000.6/Documentation/Manual/ParticleSystems.html
- Unity Visual Effect Graph:
  https://docs.unity3d.com/6000.6/Documentation/Manual/VFXGraph.html
- Unity Sprite Atlas properties:
  https://docs.unity3d.com/6000.6/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html
- Unity GameObjects and components:
  https://docs.unity3d.com/6000.6/Documentation/Manual/class-GameObject.html
- Unity asset import and asset packages:
  https://docs.unity3d.com/6000.6/Documentation/Manual/ImportingAssets.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/AssetPackagesImport.html
- Unity assembly definitions:
  https://docs.unity3d.com/6000.6/Documentation/Manual/assembly-definition-files.html
- Unity Tags, Layers, and collision:
  https://docs.unity3d.com/6000.6/Documentation/Manual/class-TagManager.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/LayerBasedCollision.html
- Unity Terrain and Terrain Collider:
  https://docs.unity3d.com/6000.6/Documentation/Manual/terrain-UsingTerrains.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/class-TerrainCollider.html
- Unity Awaitable and Job System:
  https://docs.unity3d.com/6000.6/Documentation/Manual/async-awaitable-continuations.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/job-system-overview.html
- Unity NativeContainer job safety and Burst:
  https://docs.unity3d.com/6000.6/Documentation/Manual/job-system-native-container.html
  https://docs.unity.com/en-us/engine/6000.6/manual/whats-new/unity66
- Unity Behavior and PlayerLoop/update guidance:
  https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.behavior.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/player-loop-customizing.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/events-per-frame-optimization.html
- Unity Addressables async operation handles:
  https://docs.unity.cn/Packages/com.unity.addressables%403.0/manual/AddressableAssetsAsyncOperationHandle.html
- Unity Web networking and CORS:
  https://docs.unity3d.com/6000.6/Documentation/Manual/webgl-networking.html
  https://fetch.spec.whatwg.org/#http-cors-protocol
- JetBrains AI Assistant project rules:
  https://www.jetbrains.com/help/ai-assistant/configure-project-rules.html
- Junie guidelines:
  https://junie.jetbrains.com/docs/guidelines-and-memory.html
- GitHub Copilot customization:
  https://docs.github.com/en/copilot/reference/customization-cheat-sheet
