---
applyTo: "Assets/**,ProjectSettings/**,Packages/manifest.json,Packages/packages-lock.json"
---

# Unity Content Instructions

- Keep every asset or folder paired with its `.meta` file and preserve GUIDs.
- Use Unity-aware operations for moves, renames, scenes, prefabs, and structural
  serialized changes.
- Do not text-merge binary assets or broadly replace YAML GUID/file-ID data.
- Preserve prefab relationships, overrides, scene references, serialized fields,
  import settings, addressable contracts, and platform configuration.
- Preserve Animator/clip bindings, Timeline tracks and scene bindings, mixer and VFX
  parameter contracts, sprite subasset IDs and atlas membership, and GameObject
  activation/component/Transform ownership. Validate play, seek, stop, reuse, and
  target-platform behavior for affected content systems.
- Check referencers before deleting or replacing content.
- Treat cleanup as a reference migration: classify exact authored/imported/generated/
  cache paths, preserve recovery and locks, use Unity/`AssetDatabase` operations when
  import or serialization state matters, and verify no orphaned `.meta`, missing GUID,
  Addressables/build reference, or unrelated resave remains.
- For file/package import and reimport, inspect provenance, scripts/plugins, selected
  paths, overwrite/GUID risk, importer settings, generated subassets, and the complete
  asset/`.meta` diff.
- For Asset Store, UPM, source-art, and vendor updates, record current/target version,
  source/license/changelog/compatibility, incoming manifest and local modifications;
  update one scoped family with rollback and validate compile, import, references,
  memory/visual output, and representative target Player behavior.
- Preserve TerrainData/layer/tile/collider contracts and keep Tag, Layer, Sorting
  Layer, collision matrix, and each mask domain stable across content changes.
- Preserve Addressables addresses/labels/runtime keys, groups/schemas, catalogs,
  bundle dependencies, content-update state, and handle-compatible loading behavior.
- Preserve XR Origin/rig, tracked camera/controller/hand, interaction and stereo-view
  contracts, plus first/third-person cameras, view models, local-player layers, and
  side-scroller/2D/2.5D/3D plane, sorting, physics, parallax, and camera bounds.
- Preserve network prefab registration, scene synchronization IDs, spawnable asset
  compatibility, and addressable/catalog version contracts across clients.
- Preserve AI graph/subgraph identity, typed blackboard contracts, per-agent runtime
  isolation, abort paths, import pre/post-stage idempotency, and rollback-safe
  generated outputs.
- Avoid unrequested broad reimport or reserialization.
- Do not blind-overlay vendor files, modify `Library/PackageCache`, mass-update only
  because newer content exists, or use cache deletion as project-content cleanup.
- Validate missing scripts/references, affected scenes/prefabs, content rules, and
  target builds.
