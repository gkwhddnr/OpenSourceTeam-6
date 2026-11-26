---
applyTo: "Assets/**/*.cs,Packages/**/*.cs,Assets/**/*.asmdef,Assets/**/*.asmref,Packages/**/*.asmdef,Packages/**/*.asmref,Packages/manifest.json,Packages/packages-lock.json,ProjectSettings/**/*.asset"
---

# Unity Code Instructions

- Read `/AGENTS.md` and `/PROJECT_CONVENTION.md`.
- Verify the Unity, package, API, backend, target, and assembly context.
- Preserve serialized fields and public/asset/save compatibility.
- Respect component lifecycle, destroyed-object null semantics, main-thread rules,
  domain reload, cancellation, event cleanup, jobs, Burst, and native resources.
- Classify managed GC, runtime-created Unity objects, NativeContainers/disposables,
  loaded assets, GPU resources, and external handles separately. Give each an owner,
  budget, valid thread/lifetime, and symmetric release path; do not use forced GC or
  unused-asset unloading as routine leak repair.
- Never block the main thread on an async continuation that needs it, hold locks across
  `await`, or leave Task/Awaitable/job/NativeContainer ownership implicit. Bound queues,
  workers, retries, and teardown.
- Keep Core -> independent, Unity Runtime -> Core, and Editor -> approved Core/Runtime
  dependencies. Preserve DTO-Service-Controller-repository boundaries and CRUD/auth/
  version/conflict semantics.
- Apply SOLID only at real change/substitution boundaries; keep cohesive components,
  substitutable inheritance, narrow interfaces, and an explicit composition root.
- Keep Behavior Tree/FSM/Utility/GOAP state and abort ownership explicit; prevent
  races and stale publication, justify Singleton/Observer/EventBus, state algorithm
  bounds, obey Burst/HPC# restrictions, and minimize recurring Update work by profile.
- Keep runtime assemblies independent of editor-only APIs.
- Preserve asmdef/asmref identity and dependency direction. Resolve built-in/package
  types through documented namespaces and owning assemblies; do not use internals or
  package-cache copies.
- Prefer composition to deep Unity-object inheritance and verify inherited serialized
  fields, Unity messages, and `base` call order when a parent class changes.
- Distinguish layer indices from LayerMasks and keep physics, culling, rendering,
  Sprite, Avatar, stencil, and shader mask domains separate.
- Preserve Addressables runtime keys/catalog/profile/group/handle contracts; avoid
  unsafe synchronous waits and release handles, instances, and scenes correctly.
- Treat asmdefs, ScriptableObject definitions, and Addressables delivery as distinct
  dependency graphs; do not serialize live handles or mutable runtime state in assets.
- Verify XR provider/rig/tracked-pose/interaction/comfort/stereo contracts and the
  first/third-person or 2D/2.5D/3D gameplay-space/camera/physics rules in use.
- Preserve the selected network stack's manager, role, transport, session, scene,
  prefab, authority, state/message, delegate, reconnect, and shutdown lifecycles.
- Do not edit generated solution/project files or package cache content.
- Keep UPM/Asset Store/vendor updates pinned and scoped; review changelogs,
  compatibility, lock resolution, local modifications, license, migration, rollback,
  and asset/`.meta` diffs before accepting updated code or packages.
- Profile representative Players before significant optimization; distinguish CPU,
  render/worker/job, GPU, managed/native/GPU memory, upload/readback, and sync cost.
- Run appropriate compilation, EditMode, PlayMode, content, and player checks.
