# Unity Project Engineering Rule

Before substantive work, read the project-root `AGENTS.md` and
`PROJECT_CONVENTION.md`. Inspect `ProjectVersion.txt`, package manifests, applicable
assembly definitions, relevant scripts and tests, affected assets/scenes/prefabs,
and repository status.

## Required Behavior

- Verify the exact Unity version, packages, target, backend, render pipeline, and
  systems in use; do not assume local defaults.
- Preserve unrelated and unsaved work.
- Keep every Unity asset or folder paired with its `.meta` file and preserve GUIDs.
- Use Unity-aware operations for reference-sensitive scene, prefab, and asset edits.
- Do not edit generated solution/project files, `Library`, package cache, build
  output, or user-local state.
- Preserve serialized fields, prefab overrides, asset references, save formats,
  addressable keys, and public APIs unless migration is authorized.
- Respect Unity lifecycle, main-thread restrictions, domain-reload settings, async
  cancellation, event cleanup, and native-resource ownership.
- Distinguish managed GC from Unity-object destruction, NativeContainer/disposable,
  loaded-asset, GPU, and plug-in/OS-handle release; require explicit owners and
  profiled collection/unload boundaries rather than routine forced GC.
- Treat file cleanup and Asset Store/UPM/vendor/source-asset updates as scoped
  reference migrations with version/license provenance, local-override review,
  Unity-aware operations, compatibility validation, and rollback.
- Apply explicit AI decision/abort and blackboard contracts, race-free shared-state
  ownership, Burst restrictions, bounded algorithms, scoped event patterns, and
  measured Update/PlayerLoop scheduling when those systems are affected.
- Avoid speculative performance changes; profile material hot-path work.
- Update tests, validation, migrations, and documentation with behavior changes.
- Never weaken security or valid tests to manufacture success.

Ask before destructive asset operations, broad reserialization/reimport, Editor
restart with possible unsaved work, package installation, expensive target changes,
credentialed or external actions, builds for distribution, deployment, or
publication.

Report Unity compilation, EditMode, PlayMode, content validation, player build, and
target-device checks separately. Claim only results actually observed.
