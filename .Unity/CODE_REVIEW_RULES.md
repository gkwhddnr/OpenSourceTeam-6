# Unity AI Self-Review Rules

Baseline: Unity 6.6 (`6000.6.x`). Read the `Unity 6.6 Baseline` section
in the adjacent `PROJECT_CONVENTION.md`; verify the exact project patch.

Review the actual diff and affected Unity reference graph. Inspect relevant scripts,
`.meta` files, assembly definitions, packages, project settings, scenes, prefabs,
ScriptableObjects, tests, and build configuration. Report concrete defects before
style preferences.

## Priority

1. asset loss, GUID/reference corruption, and unintended scene/prefab changes;
2. security, privacy, authorization, and secret exposure;
3. serialized-data, save-data, network, and player compatibility;
4. incorrect gameplay or editor behavior;
5. lifecycle, event, async, coroutine, job, native-resource, and memory leaks;
6. target-platform, package, assembly, AOT, stripping, and build breakage;
7. measurable performance regressions;
8. missing or misleading tests and documentation; and
9. material maintainability problems.

## Asset and Repository Checks

- Flag assets or folders added, deleted, moved, or renamed without the matching
  `.meta` file.
- Flag unexpected GUID changes and references to missing GUIDs or file IDs.
- Identify accidental scene, prefab, material, import-setting, Project Settings, or
  broad YAML reserialization changes.
- Flag generated `.sln`, `.csproj`, `Library`, `Temp`, `Logs`, `UserSettings`, build
  output, captures, and package-cache content.
- Check Git LFS and locking requirements for changed binary assets.
- Reject text conflict resolution for binary assets.
- For imports/reimports, check provenance/license, source and importer version,
  Presets/settings/platform overrides, `.unitypackage` contents, scripts/plugins,
  overwrite/GUID risk, generated subassets, complete `.meta` diff, and target output.
- For Asset Store, UPM, source-art, or vendor updates, check the pinned current and
  target version/revision, changelog and compatibility matrix, license/notices,
  dependency and lock-file resolution, incoming manifest, local modifications,
  migration/rollback plan, and whether the update was isolated and scoped.
- For cleanup, verify every exact path was classified as authored, imported,
  generated, cache, backup, or local-only; check GUID/file-ID and Addressables/build/
  package referencers, recoverability, locks, the asset/`.meta` pair, and that Unity-
  aware delete/move operations were used where serialization/import state matters.
- Flag blind vendor-file overlays, direct edits to `Library/PackageCache`, routine
  cache deletion, unrequested mass updates, broad refresh/reimport/reserialization,
  and cleanup justified only by filenames or apparent folder use.

## Serialization and Compatibility Checks

- Check renamed, removed, moved, or retyped serialized fields and components.
- Require a migration strategy for affected prefabs, scenes, ScriptableObjects,
  animation paths, Timeline, UnityEvents, save data, and addressable contracts.
- Check Inspector defaults and serialized asset values, not only C# initializers.
- Flag property-setter validation that assumes Inspector edits call the setter.
- Check `SerializeReference` type identity and migration behavior.
- Check public API, assembly, package, network, and save compatibility.

## C# and Lifecycle Checks

- Check Unity API calls from constructors or unsupported threads.
- Check assumptions about `Awake`, `OnEnable`, `Start`, `OnDisable`, `OnDestroy`,
  scene unload, and application quit ordering.
- Check subscriptions, input callbacks, timers, coroutines, tasks, Addressables
  handles, native collections, jobs, and created objects for symmetric cleanup.
- Check static fields and static events across repeated Play Mode when domain reload
  is disabled.
- Check destroyed `UnityEngine.Object` references and special null semantics.
- Check unobserved tasks, `async void`, lost cancellation, and scene-lifetime races.
- Check `Task` versus Awaitable continuation threads, repeated pooled-Awaitable use,
  main-thread Unity API access, background captures, and platform thread support.
- Flag `.Wait`, `.Result`, synchronous-over-async, main-thread waits on main-thread
  continuations, locks held across `await`, inconsistent lock order, callbacks under
  locks, unbounded concurrency, ThreadPool/worker starvation, and teardown races.
- For jobs, check JobHandle dependencies/cycles, premature `Complete`, NativeContainer
  allocator/owner/capacity/access/disposal, Burst/safety compatibility, batch size,
  oversubscription, and measured worker/main-thread synchronization.
- Check base-class suitability, inherited serialized fields, hidden Unity messages,
  required `base` calls/order, component-versus-inheritance design, and prefab/asset
  migration after reparenting.
- Check that built-in/package APIs use the documented namespace and owning assembly
  for the verified version; flag Runtime references to `UnityEditor`, package-cache
  copies, undocumented internals, reflection bypasses, and broad string lookups.

## Race, Burst, Pattern, Algorithm, and AI Checks

- Check every mutable resource for an explicit writer and phase. Flag check-then-act,
  stale generation publication, duplicate completion/disposal, subscription mutation
  during dispatch, missing JobHandle hazards, mutable job statics, and Unity object
  access from unsupported threads.
- For locks/atomics/concurrent containers, check the protected invariant, memory
  visibility, global lock order, callback/await restrictions, contention, queue bound,
  and teardown. Test synchronous and delayed completion, cancellation, reload, and
  randomized contention rather than assuming main-thread callbacks cannot race.
- For Unity 6.6 built-in Burst, verify Editor patch/target, `[BurstCompile]` boundary, HPC# and
  unmanaged data compatibility, allocation/API restrictions, float/overflow/NaN/
  aliasing policy, Editor-JIT versus Player-AOT behavior, safety-check coverage, and
  measured `OptimizeFor`, batch, and vectorization decisions.
- Check that Burst remains supplemental to Mono/IL2CPP and has a correct disabled
  path. Review asynchronous versus synchronous compile choice and benchmark warmup,
  function-pointer signature/delegate lifetime/managed-call caching, `SharedStatic`
  initialization/reset/writers, unsafe/no-alias proof, safety-check exceptions, and
  CI coverage across target backends and CPU architectures.
- Require a concrete reason for Singleton, Observer, EventBus, State, Strategy,
  Command, Factory, or Pool. Check scope/composition root, duplicate and reload
  behavior, typed delivery, ordering, reentrancy, exceptions, unsubscribe/reset,
  bounded queues, diagnostics, and absence of hidden service-locator or replication.
- For non-trivial algorithms, check input/output/invariants, worst-case time/space,
  upper bounds, chosen data structures, determinism/ties, numeric edge cases,
  recursion/work/memory bounds, cancellation, adversarial tests, and representative
  benchmarks before accepting added complexity.
- Verify the chosen AI model rather than treating every AI tree as a Behavior Tree.
  For Behavior Tree/Unity Behavior, check status and abort cleanup, immutable graph
  plus per-agent state, typed blackboard ownership, subgraph contracts, event-driven
  observation, target loss, unreachable branches, deterministic ties/seeds,
  authority, traces, and maximum-agent budgets. For FSM/Utility AI/GOAP hybrids,
  check one owner for each transition and state write.
- For pre/post pipelines, check domain and stage boundaries, schema and validation,
  idempotency, cache/version keys, source immutability, partitioned parallel output,
  deterministic merge, main-thread apply, atomic publication, rollback, and cleanup.

## Performance Checks

- Focus on code proven or highly likely to run per frame, per entity, during import,
  or in other high-frequency paths.
- Check repeated scene searches, component lookup, synchronous asset loading,
  allocations, LINQ, boxing, closures, string creation, logs, and UI rebuilds.
- Do not demand caching or pooling without correct lifetime/reset rules and a
  plausible performance need.
- Require target and scenario evidence for consequential optimization claims.
- Check whether evidence comes from a representative Player/device with recorded
  commit, backend, graphics API, quality, resolution, frame policy, warmup, duration,
  and profiler overhead. Flag Editor/Game-view Stats or average FPS as sole proof.
- Check CPU main/render/worker/job, waits, GC, GPU, memory, loading, and network tracks
  before accepting a bottleneck claim. Review percentiles/hitches and matched before/
  after captures rather than one favorable frame.
- Check managed/native/GPU allocations separately, persistent versus transient
  resource ownership, upload/readback/copy/fence stalls, per-frame texture/buffer/
  render-target creation, pool growth, peak residency, and symmetric release.
- Check that managed objects, runtime-created `UnityEngine.Object`s, NativeContainers,
  loaded assets/scenes, Addressables/AssetBundle handles, GPU resources, streams,
  cancellation registrations, and plug-in/OS handles use their distinct release APIs
  exactly once at a valid lifecycle/thread boundary.
- Flag `GC.Collect` or `Resources.UnloadUnusedAssets` used as routine gameplay or leak
  fixes, `DestroyImmediate` in runtime code, native/GPU disposal before dependent work
  completes, managed-reference clearing mistaken for native destruction, and an
  incremental-GC setting presented as permission for allocation churn.
- Require repeated load/unload and Play Mode cycles plus aligned Memory Profiler
  evidence for leak or reclamation claims. Review static/event/closure/cache roots,
  disabled-domain-reload reset, retained managed size, Unity-native objects, loaded
  and duplicated assets, allocator/pool growth, GC pauses, and GPU residency separately.
- Check whether each `Update`, `LateUpdate`, or `FixedUpdate` is phase-correct and
  active only when needed. At scale, review event/timer alternatives, feature-owned
  scheduler registration and mutation safety, cadence, staggering, batching,
  relevance tiers, catch-up cap, latency, and target-player before/after evidence.
- Flag empty Unity messages, per-agent polling of shared facts, a global update-manager
  service locator, and optimizations that trade a measured bottleneck for unbounded
  latency, memory, synchronization, or scheduling overhead.

## Core, CRUD, DTO, Service, and Controller Checks

- Check dependency direction among plain Core, Unity Runtime/package adapters,
  Editor, tests, and platform assemblies; flag UnityEngine/UnityEditor types leaking
  into the wrong layer or domain/DTO contracts.
- Check Controller-to-Service-to-domain-to-repository/gateway flow, intentional
  interfaces, explicit DTO mappings, absence of direct Controller persistence/network
  access, and engine/main-thread marshaling at adapters.
- Check Create idempotency, Read authorization/paging/not-found, Update patch/replace
  and optimistic concurrency, Delete soft/hard/retention/restore, transactions or
  compensation, audit, cancellation, retryability, and partial failure.
- Check DTO version/nullability/units/enums/time/payload limits, overposting/mass
  assignment, error mapping, correlation IDs, and secret/provider-exception leakage.
- Check practical SOLID use: cohesive reasons to change, substitutable inheritance,
  capability-focused interfaces, inward-owned abstractions, explicit composition
  roots, and no speculative interface/class explosion or hidden service locator.

## Rendering Checks

- Verify the active Built-in/URP/HDRP/custom pipeline, exact package version,
  pipeline/renderer assets, quality/platform overrides, graphics API, shader target,
  color/HDR mode, dynamic resolution, and XR/camera path affected by the change.
- Check custom pass/renderer feature injection order, declared inputs/attachments,
  clear/load/store behavior, render-graph dependencies and culling, transient versus
  persistent resource lifetime, and camera/stack/XR coverage.
- Flag render textures, RTHandles, command/compute buffers, materials, callbacks, or
  native resources allocated repeatedly or not released at Editor/runtime teardown.
- Check keyword scope, variant multiplication/stripping, prewarming, runtime keyword
  paths, SRP Batcher/instancing compatibility, and missing target-player variants.
- Check shared-material mutation, hidden material instantiation, unintended volume or
  pipeline asset edits, fallback/error shaders, and broad material/lighting reimports.
- Require target-player evidence for shader, compute, precision, texture-format,
  graphics-API, HDR/color-space, and first-use compilation behavior when affected.
- Check visual baselines for deterministic camera/scene/quality/API/device settings;
  review Frame Debugger/render-graph evidence and representative CPU/GPU measurements
  for performance claims.

## XR, Perspective, Dimension, and Camera Checks

- Check exact XR provider/OpenXR/vendor, AR Foundation, XR Interaction Toolkit, Input
  System, pipeline, runtime, device, permission, and feature compatibility.
- Check single XR Origin/rig ownership, tracked camera/hand/controller hierarchy,
  tracking origin/world scale, recenter, locomotion arbitration, capability loss,
  boundary, suspend/resume, and physical-device validation. Flag ordinary camera code
  that overwrites tracked pose or projection/FOV.
- Check stereo/multiview shaders and passes, both views, render scale, foveation,
  dynamic resolution, post effects, UI, comfort options, AR session/anchor/plane/
  occlusion lifecycle, permissions, relocalization, and privacy.
- Check first-person body/view-model/camera/aim/audio/remote separation; third-person
  follow/pivot/collision/aim/rotation rules; and local-player camera/input ownership.
- For side-scroller/2D/2.5D/3D changes, check authoritative plane/axes, projection,
  physics world, depth/lanes, sorting, slopes/platforms, ground tests, parallax,
  camera bounds, navigation, off-plane prevention, and network serialization.

## Animation, Audio, VFX, Sprite, Timeline, and GameObject Checks

- Check AnimationClip bindings, Animator parameters/layers/states, Avatar and rig
  mappings, import/compression/root-motion settings, event signatures, and gameplay
  ownership. Flag authoritative effects driven only by visual sampling or an
  unvalidated Animation Event.
- Check Timeline and PlayableDirector bindings, exposed references, signals, update
  and wrap modes, restoration, nested timelines, and play/stop/seek/skip/loop event
  semantics. Flag irreversible side effects that can repeat during scrub or seek.
- Check AudioClip load/compression/streaming and platform overrides, listener and
  mixer routing, parameter units, voice/concurrency policy, spatial settings,
  scheduled playback, pooling/reset, and teardown on scene, focus, or device change.
- Check Particle System or VFX Graph simulation space/time/seed, bounds, culling,
  stop/reset behavior, exposed properties/events, pooling, renderer/material state,
  and quality/platform budgets. Flag GPU effects used as authoritative gameplay.
- Check sprite slices and file IDs, pivots/borders/PPU, atlases and variants, sorting,
  physics, platform import overrides, and references from animation, UI, Tilemaps,
  prefabs, and Addressables after re-slicing or packing changes.
- Check GameObject activation, parenting, transform ownership, required components,
  tags/layers/static flags, Instantiate/Destroy or pooling ownership, persistent
  object uniqueness, and prefab override/Undo preservation.

## Terrain, Collision, Layer, Tag, and Mask Checks

- Check TerrainData and Terrain Layer sharing, tile coordinates/neighbors/seams,
  source height/weight import, resolutions/scale, holes/details/trees, streaming,
  lighting/navigation, TerrainCollider synchronization, target budgets, and VCS scope.
- Check static/kinematic/dynamic Rigidbody ownership, Collider geometry/convexity,
  triggers/contact prerequisites, CCD/interpolation, Physics Material, transform
  synchronization, and separation of 2D and 3D physics.
- Check project Tag/Layer/Sorting Layer names, reserved entries, both collision
  matrices, index-versus-`LayerMask` use, query trigger policy, ignored pairs,
  multi-hit ordering/buffer overflow, and migration of every referencer.
- Check that physics, camera, rendering-layer, light, Sprite, Avatar, stencil, and
  shader masks are not treated as one interchangeable bit layout.
- Check Ray/Line, Sphere/Capsule/Box cast, overlap/check, single/multiple,
  allocating/NonAlloc/batched and 2D/3D query selection, initial-overlap/backface
  semantics, NativeArray result capacity, job dependencies, and main-thread apply.

## Packages, Assemblies, and Builds

- Check package version, lock-file, registry, license, maintenance, and target
  compatibility changes.
- Check asmdef references, cycles, platform constraints, version defines, and editor
  code leaking into runtime assemblies.
- Check conditional symbols, scripting backend, AOT, managed stripping, reflection,
  native plugins, and platform APIs.
- Flag disabled analyzers, skipped tests, ignored error logs, or relaxed checks used
  only to make validation pass.
- For managed/native plug-ins, check Plugin Inspector platform and architecture
  inclusion, asmdef/Auto Reference scope, API compatibility, IL2CPP/AOT/stripping,
  native ABI and dependencies, licensing, and actual target-device loading.
- Check asmdef names/GUIDs, root namespaces, dependency direction, `.asmref` folder
  ownership, Auto/Override/No Engine References, precompiled/test references,
  defines/version defines, unsafe code, and all applicable platform filters.
- For Addressables, check package/settings/Profile/builders/groups/schemas,
  addresses/labels/runtime keys, catalog/hash/CDN/auth/cache/content-update contracts,
  handle status and single release, instance/scene release APIs, stale completion,
  hard/duplicate dependencies, build layout, and client/catalog compatibility.
- Flag `WaitForCompletion` on remote, Web, scene-chain, or unbounded paths and verify
  clean/warm/offline/interrupted/expired-auth/rollback/low-storage scenarios.
- Check that asmdef type visibility, ScriptableObject serialized definitions, and
  Addressables runtime delivery are modeled as separate graphs. Flag live handles or
  mutable runtime state serialized into definition assets, code expected to arrive in
  bundles, or missing AOT/stripping/type-move/catalog compatibility tests.

## Network Manager, Session, and Delegate Checks

- Verify the exact network/transport/lobby/relay stack and version; flag NGO,
  Entities, Mirror, Photon, or custom APIs mixed because their names are similar.
- Check one explicit manager/bootstrap owner, offline/Host/listen/dedicated/Client
  roles, approval/auth, network prefabs/scenes, spawn/despawn, tick/time, shutdown,
  reconnect, and the chosen stack's persistence/multiplicity rules.
- Check authority, ownership/write permission, version handshake, RPC/message/state
  semantics, reliability/order, prediction/reconciliation, interpolation, interest,
  bandwidth/rate limits, late join, and host/local-player assumptions.
- Check delegate/event publisher, subscriber, thread, handle/token, ordering,
  exception, symmetric unsubscription, duplicate reconnect/scene callbacks, and
  idempotent disconnect/shutdown. A local event must not be mistaken for replication.
- Require offline, single-player, host, dedicated server, multiple-client, packet-
  loss/jitter, approval failure, reconnect, late-join, scene, suspend, shutdown, and
  incompatible-version coverage as applicable.

## Security, CORS, and External API Checks

- Treat CORS as a browser response-header protocol, not authentication or a native
  client control. Check exact origin/method/header allowlists, preflight, credentials,
  `Vary: Origin`, exposed headers, CDN/range/error behavior, and hosted Web builds.
- Flag wildcard credentialed origins, disabled TLS/certificate checks, client-side
  authorization assumptions, plaintext tokens, sensitive logs, unsafe redirects,
  SSRF/path traversal/injection/replay, unbounded payloads, and CRUD authorization gaps.

## MCP and VCS Checks

- Check MCP server identity/version, protocol-revision compatibility, declared
  capabilities/extensions, allowed roots/tools, credential handling,
  timeout/cancellation, retry idempotency, external destinations, and unexplained
  dirty or partial Editor state.
- Flag unreviewed floating MCP commands, broad filesystem/network scope, secrets in
  configuration/logs, and server instructions treated as trusted policy.
- Check that asset and `.meta` changes are paired and GUIDs remain stable.
- Check LFS/lock/checkout requirements, merge-driver use, unresolved text or binary
  conflicts, case-only renames, accidental serialization-mode changes, and generated
  files added after a VCS operation.
- If a commit is requested, check repository format or the documented fallback
  type/scope/imperative summary, one-intent scope, atomic asset/`.meta` changes,
  migration notes, truthful validation, and absence of secrets or generated noise.

## Testing Checks

- Require the correct layer: plain, EditMode, PlayMode, player, content, or build.
- Check determinism, isolation, cleanup, order independence, and bounded waits.
- Ensure changed lifecycle, serialization, failure, platform, and migration paths are
  covered where material.
- Do not request irrelevant tests solely to increase test count.

## Finding Format

Each finding must include:

- severity;
- affected file or asset and precise location;
- a concrete failure scenario;
- why it matters; and
- the smallest practical remediation.

If no actionable finding exists, say so and state meaningful unverified risks such
as an unopened scene, unrun PlayMode test, or unbuilt target.
