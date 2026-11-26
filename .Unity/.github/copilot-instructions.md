# GitHub Copilot Instructions for Unity Projects

Before substantive work, read `/AGENTS.md` and `/PROJECT_CONVENTION.md`. Inspect the
exact Unity version, package manifest and lock, assembly definitions, relevant
scripts and tests, affected assets/scenes/prefabs/settings, and repository status.

Make the smallest coherent change that satisfies the request. Preserve unrelated
and unsaved work. Keep every Unity asset or folder paired with its `.meta` file and
preserve GUIDs. Use Unity-aware operations for reference-sensitive content changes.

Do not edit generated `.sln`/`.csproj` files, `Library`, package cache, build output,
or user-local settings. Preserve serialized fields, prefab overrides, asset
references, addressable keys, save data, and public contracts unless migration is
authorized.

Respect MonoBehaviour and ScriptableObject lifecycles, Unity main-thread rules,
domain-reload settings, async cancellation, event cleanup, job/native-resource
ownership, backend/AOT restrictions, and target platforms. Avoid unmeasured
performance rewrites.

Apply the root allocation/release rules separately to managed GC, Unity objects,
NativeContainers, loaded assets, GPU resources, and external handles. Treat cleanup
and Asset Store/UPM/vendor asset updates as scoped reference migrations with exact
paths, versions, licenses, local overrides, Unity-aware operations, validation, and
rollback; never use routine forced GC, cache deletion, or blind file overlays as fixes.

For AI, concurrency, or hot-path work, apply the root rules for decision-model and
blackboard/abort contracts, race-free ownership, Burst restrictions, bounded
algorithms, pattern lifetimes, and measured Update/PlayerLoop scheduling.

Ask before destructive asset changes, broad reimport/reserialization, package
installation, Editor restart with possible unsaved work, external writes,
credentialed operations, distribution builds, deployment, or publication.

Report Unity compilation, EditMode, PlayMode, content, player-build, and device
validation separately and only when actually performed.
