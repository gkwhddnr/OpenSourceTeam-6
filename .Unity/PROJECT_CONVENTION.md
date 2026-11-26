# PROJECT_CONVENTION.md

Reusable engineering conventions for personal Unity projects. AI operating
behavior belongs in `AGENT.md`; this file covers source, content, Editor tools,
performance, and verification.

Official references were reviewed on 2026-09-20 using Unity 6.0 and the explicit
package documentation versions linked below. These are reference baselines,
not a claim that they are the latest releases or requirements to install them.
Match documentation to `ProjectVersion.txt` and resolved package versions.
Naming, layout, ownership, and optimization policies below are project
recommendations informed by documentation, not universal Unity requirements.

## 1. Project Profile

Fill in only from verified project files and environment information.

```text
Project name:
Unity Editor version:
Supported platforms:
Render pipeline and package version:
Scripting backend / API compatibility:
Runtime / Editor / test assemblies:
Input system:
UI system:
Physics: 2D / 3D
Networking package and authority:
Asset loading: direct / Addressables / other
Visual Scripting / Splines / ProBuilder / Terrain Tools versions:
Domain Reload / Scene Reload settings:
Build profiles or build scene list:
Build entry point and output directory:
Edit Mode / Play Mode test commands:
Source control / binary asset locking:
Performance budgets and target hardware:
```

## 2. Sources of Truth and Layout

| Path | Responsibility |
|---|---|
| `ProjectSettings/ProjectVersion.txt` | Required Editor version |
| `Packages/manifest.json` | Declared package dependencies |
| `Packages/packages-lock.json` | Resolved dependency graph |
| `ProjectSettings/` | Shared project configuration |
| `Assets/` and associated `.meta` files | Source content and stable asset identity |
| `.asmdef` / `.asmref` | Assembly boundaries and references |
| Build scripts/profiles and tests | Build and verification contracts |

Suggested layout when no established structure exists:

```text
Assets/
  Project/
    Runtime/
    Editor/
    Tests/EditMode/
    Tests/PlayMode/
    Scenes/
    Prefabs/
    Data/
    Art/
    Audio/
Packages/
ProjectSettings/
```

Organize larger projects by feature when that clarifies ownership. Preserve
required special folder semantics. Do not commit generated `Library/`, `Temp/`,
`Obj/`, logs, local `UserSettings/`, or build outputs. Generated solution/project
files are not the authority for Player compilation.

## 3. Asset Identity and Source Control

- Track `.meta` files for assets and folders. Move/rename them with the asset;
  prefer the Project window or `AssetDatabase`.
- A new GUID is a new asset identity even when its path matches an old asset.
  Preserve GUIDs and inspect referencers before deleting or replacing assets.
- Prefer readable text serialization for eligible assets when the repository
  uses it, but do not change serialization mode or force-reserialize globally
  as a side effect of an unrelated task.
- Merge scenes/prefabs using the project's Unity-aware workflow; validate the
  result in Unity. Do not text-merge binary files.
- Separate intentional asset migration from unrelated formatting/import noise.

Source: [Asset metadata](https://docs.unity3d.com/6000.0/Documentation/Manual/AssetMetadata.html).

## 4. Assemblies and Package Boundaries

- Group code into cohesive assemblies rather than one assembly per class.
- Keep dependencies explicit and acyclic; separate Runtime, Editor, and tests.
- Configure Editor assembly platform restrictions. An `Editor` folder nested
  under an assembly definition should not be assumed to isolate code by itself.
- Use `UNITY_EDITOR` for small optional Editor portions; substantial tools
  belong in an Editor-only assembly with no reverse Runtime dependency.
- Respect platform filters, version defines, and the installed package APIs.
- Avoid package upgrades and new dependencies unless the task needs them.
  Keep manifest and lockfile changes consistent and review transitive changes.

Source: [Organizing scripts into assemblies](https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definition-files.html).

## 5. C# Naming and API Design

Follow existing project style. Defaults when none is defined:

| Element | Convention | Example |
|---|---|---|
| Namespace | Product/feature scope | `Game.Inventory` |
| Type, method, property | PascalCase | `PlayerMotor`, `ApplyDamage` |
| Interface | `I` + PascalCase | `IDamageable` |
| Parameter/local variable | camelCase | `durationSeconds` |
| Private field | Consistent camelCase convention | `moveSpeed` |
| Boolean | Positive condition | `isGrounded` |
| Event | Meaningful occurrence | `HealthChanged` |

- Match script filenames to attached MonoBehaviour/ScriptableObject type names.
- Prefer private serialized fields with controlled public access to state.
- Include units in ambiguous values and expose only useful configuration.
- Keep pure logic separate from Unity callbacks when it improves testability.
- Avoid public mutable collections, global singletons, and inheritance added
  solely for convenience; use ownership and composition deliberately.

## 6. Serialization and Data Migration

- Use `[SerializeField]` for supported private fields that must persist.
  Ordinary C# properties are not automatically Inspector-serialized fields.
- Check supported field/container types. Do not assume dictionaries, nested
  containers, or arbitrary .NET objects serialize like simple lists.
- Use `[SerializeReference]` only when managed-reference semantics such as
  polymorphism are required; account for type identity and migration.
- Use `[FormerlySerializedAs("oldName")]` for supported field renames. Validate
  scenes, prefabs, and assets; the attribute does not migrate every type change.
- Inspect persistent UnityEvent bindings and graph references after API renames.
- Keep derived caches out of serialized data unless deliberately persisted.
- Version player save data separately from Unity asset serialization.

Sources: [Script serialization](https://docs.unity3d.com/6000.0/Documentation/Manual/script-serialization.html),
[FormerlySerializedAs](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Serialization.FormerlySerializedAsAttribute.html).

## 7. MonoBehaviour Lifecycle and Physics

- Use `Awake` for local initialization, `OnEnable` for activation-scoped work,
  and `Start` for appropriate initialization before the first Update.
- Do not depend on unspecified ordering between different objects' callbacks.
  Make cross-object dependencies explicit instead of adding execution-order
  overrides as a default fix.
- Use `Update` for frame-based behavior and `FixedUpdate` for work tied to the
  chosen fixed-step physics simulation. Fixed updates need not occur once per frame.
- Use the project's input update mode; retain transient input until consumed
  by the relevant simulation step when needed.
- Distinguish Rigidbody/Collider from Rigidbody2D/Collider2D and avoid competing
  transform/physics controllers on the same object.
- Pair activation-scoped subscriptions with `OnDisable`; release lifetime-owned
  resources in appropriate cleanup. Account for objects that never became active.

Source: [Event function execution order](https://docs.unity3d.com/6000.0/Documentation/Manual/execution-order.html).

## 8. Object Validity (`IsValid` Equivalent)

- Unity does not use Unreal's UObject `IsValid` contract. For a reference typed
  as `UnityEngine.Object` or a derived class, use `if (target != null)` or
  `if (target)` when checking whether its native object still exists.
- `ReferenceEquals`, `is null`, `?.`, and `??` do not perform Unity's overloaded
  destroyed-object check. Do not use them as a replacement for that check.
- A reference stored as an interface or `object` needs deliberate Unity-object
  lifetime handling; its ordinary null comparison is insufficient.
- Revalidate after an await/yield, delayed callback, or scene unload. Check
  component presence, active/enabled state, and indices separately.
- Handle missing required references with a useful diagnostic; skip optional
  behavior deliberately. Do not hide lifecycle bugs with repeated global searches.

Source: [UnityEngine.Object](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Object.html).

## 9. Play Mode Reload and Shared State

- Read the project's Enter Play Mode settings before relying on state reset.
- When Domain Reload is disabled, explicitly reset session static state and
  avoid accumulating static event handlers between Play Mode runs.
- Use appropriate initialization hooks, such as
  `RuntimeInitializeOnLoadMethod` with `SubsystemRegistration` where suitable.
- Verify repeated Play Mode entry/exit, scene reload, and enabled/disabled
  objects. Do not silently enable reload options to conceal a state bug.

Source: [Domain reloading](https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html).

## 10. Events, Coroutines, Async Work, and Resources

- Choose subscription lifetime explicitly and unsubscribe symmetrically.
- Coroutines normally run on the main thread; yielding does not offload CPU work.
- Cancel stale async operations and observe exceptions. Recheck the receiver
  before applying results and return to a supported thread for Unity API calls.
- Reuse temporary buffers where practical; dispose native containers and GPU
  buffers only after dependent work finishes.
- Destroy only runtime-owned mesh/material/object instances. Preserve shared
  project assets and release asset-loading handles through their owning system.
- Reset pooled objects' state, subscriptions, and outstanding work on reuse.

## 11. C#, Visual Scripting, Jobs, and Native Plugins

- Use C# for reusable rules, testable services, and measured hot paths. Use
  Visual Scripting for suitable event-driven composition when already adopted.
- Expose focused APIs to graphs; avoid duplicate rules in scripts and graphs.
- Keep graph variables, events, and scene/object scope explicit. Verify graphs
  after changing their referenced C# methods or types.
- Profile before introducing Jobs/Burst. They require compatible data and APIs,
  correct dependencies, allocator lifetime, and ownership; they do not make
  arbitrary GameObject code safe to run on workers.
- Use native C/C++ plugins only for a real integration requirement. IL2CPP
  translates managed code during builds; do not edit its generated C++.

Sources: [Visual Scripting](https://docs.unity3d.com/Packages/com.unity.visualscripting@1.9/manual/index.html),
[Job system](https://docs.unity3d.com/6000.0/Documentation/Manual/job-system.html),
[Burst](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/index.html),
[IL2CPP](https://docs.unity3d.com/6000.0/Documentation/Manual/scripting-backends-il2cpp.html).

## 12. Scenes, Prefabs, and ScriptableObjects

- Prefer reusable prefabs for repeated GameObject composition; keep genuinely
  scene-specific wiring in the scene.
- Identify base prefab, nested prefab, variant, and instance ownership before
  applying changes. Review overrides rather than applying/reverting everything.
- Validate missing scripts and references after moves or migrations.
- Keep shared ScriptableObject configuration distinct from session state.
  Clone or maintain separate runtime state when per-instance mutation is needed.
- Do not treat modifying a ScriptableObject asset as a Player save system.
- For scene transitions, define persistent service ownership and avoid duplicate
  bootstrap objects or references to unloaded scenes.

Sources: [Prefabs](https://docs.unity3d.com/6000.0/Documentation/Manual/Prefabs.html),
[ScriptableObject](https://docs.unity3d.com/6000.0/Documentation/Manual/class-ScriptableObject.html).

## 13. Editor Tools, Undo, and Validation

- Prefer `SerializedObject`/`SerializedProperty` for serialized Inspector edits.
- For direct changes, register Undo before mutation and record prefab instance
  modifications when applicable. Mark changed assets/scenes dirty through the
  appropriate API; a dirty flag alone is not an Undo record.
- Use Undo-aware creation/destruction for generated Editor objects.
- Keep `OnValidate` to data validation. It may run frequently and off the main
  thread; do not create objects, rebuild meshes, or call unsafe APIs there.
- Let a main-thread Editor callback consume pending rebuild requests, or expose
  an explicit rebuild command. Unregister callbacks during teardown/reload.
- Restrict edit-time execution to the intended scene/prefab stage and preserve
  user content. Make repeated generation produce no duplicate objects.

Sources: [EditorUtility.SetDirty](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUtility.SetDirty.html),
[OnValidate](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnValidate.html).

## 14. Performance and Diagnostics

- Establish a representative scene and target hardware before optimizing.
- Use the Profiler to distinguish main-thread, render-thread, GPU, allocation,
  physics, and loading costs. Compare a suitable Player capture with Editor data.
- Cache stable dependencies; avoid unnecessary Find operations, component
  searches, allocations, LINQ, and string formatting in measured hot paths.
- Prefer events or scheduled work over idle per-frame polling.
- Reduce the amount and frequency of work before adding pooling or threading.
- Log actionable context without per-frame spam or private data. Separate new
  errors from existing warnings and report the first meaningful failure.
- Record scene population, settings, timing, memory, and allocations when
  claiming a performance improvement.

Source: [Unity Profiler](https://docs.unity3d.com/6000.0/Documentation/Manual/Profiler.html).

## 15. Terrain Editing (Landscape Equivalent)

- Inspect Terrain/TerrainCollider, shared TerrainData, tile dimensions,
  heightmap/detail resolutions, Terrain Layers, and neighboring tiles first.
- Choose resolution for required detail and cost; verify supported values in
  the installed Editor. Do not copy Unreal Landscape component formulas.
- Preserve the original TerrainData when a unique terrain variant is required.
  Duplicating a GameObject does not imply independent shared asset data.
- Confirm active terrain, brush, radius, strength, and target texture layer.
  Limit edits to the intended region and preserve unrelated painted data.
- Terrain Layers describe surface texturing; do not assume they provide the
  same non-destructive sculpt stack as Unreal Landscape Edit Layers.
- For scripted interactive height edits, consider `SetHeightsDelayLOD` for
  intermediate writes and call `SyncHeightmap` after the edit completes. Check
  array orientation, bounds, and normalized height values for that API.
- Verify tile seams, texturing, collider behavior, vegetation, and saved data
  after the final synchronization.

Sources: [Creating and editing Terrains](https://docs.unity3d.com/6000.0/Documentation/Manual/terrain-UsingTerrains.html),
[SetHeightsDelayLOD](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TerrainData.SetHeightsDelayLOD.html).

## 16. Modeling and ProBuilder

- Check the installed ProBuilder version before using package-specific tools.
- Use primitives/ProBuilder for blockout or suitable mesh editing; preserve
  imported model sources and their asset ownership.
- Distinguish object transforms from vertex/edge/face edits and confirm local
  versus world coordinates, pivot, snapping, and scale.
- Check whether the mesh is shared before editing; make an independent asset
  when only one placement should change.
- Validate normals, UVs, submeshes/material slots, collision, and lighting after
  geometry edits. Check LOD and lightmap requirements for the chosen pipeline.
- Keep procedural preview meshes bounded in complexity; reuse owned output and
  avoid allocating a new mesh on every Inspector change.
- Preserve editable source when exporting/baking final geometry. Test the
  exported result instead of assuming conversion preserves all behavior.

Source: [ProBuilder](https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/index.html).

## 17. Vegetation and Foliage

- Distinguish Terrain trees, Terrain details, and ordinary prefab instances.
  Select based on interaction, collision, lighting, and rendering needs.
- Check prototypes, brush density, size variation, placement rules, and target
  TerrainData before painting or erasing.
- Consider instanced detail meshes where the platform/shader supports them.
  Inspect the selected render mode; available color/wind/lighting settings differ.
- Do not assume painted trees/details have every behavior of a live prefab
  GameObject with scripts. Use separate interaction ownership when required.
- Validate tree collider configuration, LOD/billboards, detail distance,
  density scaling, shadows, and alpha overdraw on representative hardware.
- Keep gameplay collision consistent when visual foliage density changes.
- Verify Undo/Redo and persistence after save/reopen; avoid repainting the
  entire terrain to fix a local configuration problem.

Sources: [Grass and other details](https://docs.unity3d.com/6000.0/Documentation/Manual/terrain-Grass.html),
[Trees](https://docs.unity3d.com/6000.0/Documentation/Manual/terrain-Trees.html).

## 18. Brush Editing and Blockout

- Identify whether the task uses a Terrain brush, vegetation brush, ProBuilder
  geometry tool, or another installed tool. Unity has no assumed one-to-one
  equivalent of Unreal's additive/subtractive BSP brush workflow.
- Use supported Terrain Tools brushes for terrain operations and ProBuilder or
  mesh workflows for geometric blockout. Check package support first.
- Preview radius, falloff, strength, masks, selection, and affected asset before
  a stroke. Preserve Undo as one meaningful editing operation where practical.
- For custom tools, batch affected data and defer expensive synchronization
  until the stroke/transaction finishes, while keeping preview behavior clear.
- Test scale, traversal, collision, and seams before adding detailed geometry.

Sources: [Terrain Tools](https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.1/manual/index.html),
[ProBuilder](https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/index.html).

## 19. Splines and Minimum Recalculation

- Verify the Splines package and the SplineContainer/spline being edited.
- Track dirty inputs: knots, tangent modes, closed state, container transform,
  spacing, source mesh, and generation options. Rebuild only on relevant changes.
- Filter `Spline.Changed` by the spline you own and coalesce repeated requests.
  It can fire repeatedly in one Editor frame. For Editor tools, consider
  `EditorSplineUtility.AfterSplineWasModified`, documented as once per frame
  when a spline changes. Keep that dependency Editor-only and unsubscribe.
- A per-frame callback is not an end-of-drag callback. For expensive output,
  use a cheaper preview and explicit final rebuild or a tool transaction boundary.
- Cache sampling results by revision. Where position, tangent, and up vector
  are all needed, use a combined `SplineUtility.Evaluate` call when supported.
- `SplineUtility` generally ignores Transform values. Use appropriate container
  evaluation or explicit transforms; invalidate world-space caches on transform
  changes, including non-uniform scale.
- Reuse generated meshes, objects, and buffers. Rebuild collision only when
  needed and verify final bounds/normals/colliders after geometry changes.
- Bound sample count and validate spacing, empty/short splines, closed seams,
  and degenerate tangents. Do not assume uniform parameter steps mean equal
  world-space distances; use distance-aware sampling when spacing matters.
- Update local output only when dependencies permit. Tangent changes can affect
  adjacent curves; distance-based placement may shift all downstream output.
- Rigid repeated objects can use instancing. Bent geometry requires actual mesh
  generation/deformation; transform instancing alone does not bend a mesh.
- Do not copy Unreal's `UpdateSpline`/`UpdateMesh` API into Unity code; follow the
  installed package's notifications, cache, and generated-output contracts.

Sources: [Spline](https://docs.unity3d.com/Packages/com.unity.splines@2.8/api/UnityEngine.Splines.Spline.html),
[EditorSplineUtility](https://docs.unity3d.com/Packages/com.unity.splines@2.8/api/UnityEditor.Splines.EditorSplineUtility.html),
[SplineUtility](https://docs.unity3d.com/Packages/com.unity.splines@2.8/api/UnityEngine.Splines.SplineUtility.html).

## 20. GPU Instancing (ISM-Related Workflow)

- Unity GPU instancing is a rendering technique, not an Unreal ISM/HISM
  component contract. It does not automatically provide per-instance scripts,
  colliders, selection, networking, or persistent gameplay identity.
- Group compatible mesh/material/shader state. Confirm platform and shader
  support and the actual path used by Built-in, URP, HDRP, or a custom pipeline.
- Distinguish GPU instancing from the SRP Batcher and static batching. Inspect
  the current pipeline's compatibility/priority rules; do not disable the SRP
  Batcher globally just to force an unmeasured instancing change.
- Avoid accidental material copies when shared rendering state is intended.
  Evaluate per-instance properties and their batching implications explicitly.
- For explicit `Graphics.RenderMeshInstanced`, cache instance transforms but
  submit visible draws each required frame: the call renders the current frame.
- Its instances are culled/sorted using group bounds. Partition large scattered
  populations into useful spatial groups and supply correct bounds when overriding.
- Respect that API/version's instance-data and batch-size limits. Do not treat
  a sample's maximum count as universal across payloads or rendering paths.
- Track stable game IDs separately from mutable instance array indices. Validate
  resources and indices, and release owned buffers when no longer used.
- Profile CPU submission, GPU work, memory, shadows, and culling; fewer objects
  or draw calls alone do not prove faster frame times.

Sources: [GPU instancing](https://docs.unity3d.com/6000.0/Documentation/Manual/GPUInstancing.html),
[SRP Batcher](https://docs.unity3d.com/6000.0/Documentation/Manual/SRPBatcher.html),
[Graphics.RenderMeshInstanced](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html).

## 21. Networking and Configuration

- Use the installed networking stack's versioned documentation and authority
  model; do not assume an Unreal-style replication API exists.
- Validate remote input and identify ownership of persistent versus transient state.
- Test host/client, late join, disconnect, and scene transition paths as affected.
- Keep shared tuning in an intentional configuration source rather than copies
  in scripts, graphs, and prefabs. Keep secrets out of serialized assets.
- Do not enable services or add analytics/network dependencies incidentally.

## 22. Testing and Build Verification

| Check | Appropriate scope |
|---|---|
| Edit Mode tests | Pure logic, serialization helpers, Editor tools |
| Play Mode tests | Components, coroutines, scene and runtime integration |
| Manual interaction | Input, visual behavior, collision, Editor UX |
| Player smoke test | Target/backend, AOT, stripping, shader/content loading |

- Use the project's installed Unity Test Framework and test assemblies.
- Keep tests independent and clean up objects, scenes, events, files, and native
  resources they own. Preserve user assets and the original scene setup.
- Test destroyed/missing references and repeated activation when relevant.
- For content tools, verify Undo/Redo, save/reopen, prefab overrides, and repeated
  generation; include zero instances and spline knot insertion/deletion.
- Discover build commands and the exact Editor executable. Record target,
  backend, scene/profile, defines, test filters, logs, and result artifacts.
- Use supported batchmode/test arguments or the project build entry point;
  inspect test results and build reports as well as process exit status.
- Do not assume Build Settings and Build Profiles UI or CLI are identical
  across Editor versions. Do not change target/backend solely for convenience.
- Build a Player when required to verify platform behavior; publish or upload
  only when separately authorized. Documentation changes need no engine build.

Sources: [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html),
[Command-line arguments](https://docs.unity3d.com/6000.0/Documentation/Manual/CommandLineArguments.html),
[Build configuration](https://docs.unity3d.com/6000.0/Documentation/Manual/BuildSettings.html).

## 23. Commits and Review

Suggested commit format when no repository convention exists:

```text
<type>(<optional-scope>): <summary>
```

Use `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `ci`, or `chore`
as appropriate. Keep one reviewable intent per change. Explain serialized-data,
prefab, package, and platform impact, with the checks actually performed.

## 24. Definition of Done

- Requested behavior or documentation is complete within the authorized scope.
- Relevant Unity compilation/tests and interaction checks pass when applicable.
- Asset GUIDs, scene references, serialized data, and prefab overrides are safe.
- Runtime/Editor dependencies, object lifetime, and cleanup remain correct.
- Content changes survive save/reopen and required Undo/Redo checks.
- Platform/Player verification is performed when the change requires it.
- No unrelated resaves, generated output, or temporary debug changes remain.
- Final notes distinguish verified results from unavailable checks.

## 25. Project-Specific Overrides

Add verified rules here rather than silently changing reusable defaults.

```text
Project / Editor version:
Assembly ownership and naming overrides:
Package and upgrade policy:
Scene / prefab / shared asset ownership:
Serialization and migration rules:
Input / UI / physics / networking conventions:
Render pipeline and instancing strategy:
Terrain / spline generation budgets:
Required tests and build commands:
Source control / locking / release policy:
Additional Definition of Done:
```

## 26. Environment Components, Sky, and Atmospheric Lighting

Inspect the active render pipeline/package, camera, scene lighting settings,
Volume profiles, baked data, probes, and time-of-day owner before editing.
Unity's environment setup is pipeline-dependent; do not add Unreal component
names as if they were built-in Unity components.

### Responsibilities and Engine Terminology

| Requested concept | Unity workflow |
|---|---|
| Directional Light | Light component with Directional type; typically sun/moon |
| Sky Light / environment illumination | Ambient/environment lighting plus appropriate reflection/probe configuration |
| Sky Atmosphere | Skybox workflow in Built-in/URP; HDRP Physically Based Sky for physical atmosphere |
| Sky Sphere / skydome | Skybox/panoramic background or a deliberately authored sphere/dome mesh; no assumed BP_Sky_Sphere equivalent |
| Atmospheric Light | Directional light participating in the chosen sky/atmosphere implementation |
| Volumetric Cloud | HDRP Volumetric Clouds override; other pipelines require a verified supported solution |
| Fog | Pipeline-supported distance fog, or HDRP Fog with optional volumetrics and local fog volumes |
| Environment controls | Lighting settings in Built-in/URP; also Visual Environment and sky Volume overrides in HDRP |

These are functional mappings, not interchangeable component APIs. A skybox
image alone does not provide a complete physical atmospheric scattering system.

### Directional Light

- Use rotation to set the light direction; do not move the Transform to simulate
  a nearby point source. Identify which light represents the sun or moon.
- Choose Realtime, Mixed, or Baked according to the pipeline and GI workflow.
  Moving a light does not regenerate baked indirect illumination automatically.
- Verify intensity units, temperature/color, shadows, bias, and shadow distance
  in the installed pipeline. Do not copy numerical intensities across engines
  or pipelines without checking units and exposure.
- Inspect the renderer's main/additional light and shadow support before adding
  multiple directional lights; support and cost vary by pipeline/settings.

### Built-in and URP: Skybox and Environment Lighting

- Set the intended Skybox Material and verify the camera renders it.
- Assign Sun Source explicitly where supported, rather than relying on implicit
  light selection. The skybox shader must support sun-driven rendering; a
  static cubemap does not acquire a moving sun disk just from this assignment.
- Configure Environment Lighting source (Skybox, Gradient, or Color where
  available) independently from direct sunlight and Environment Reflections.
- After changing the sky, verify ambient lighting and reflections separately.
  Check baked data and local reflection/light probes rather than assuming the
  new visible background updated every lighting contribution.
- Where the workflow uses `DynamicGI.UpdateEnvironment`, request updates at a
  measured cadence after meaningful changes. GPU readback can lag, excess
  requests can be ignored, and unsupported async readback can stall.
- Do not treat that API as an HDRP sky-update API or a rebake of all lighting.

### Sky Sphere, Skybox, and Custom Dome Meshes

- Use the pipeline's sky system for an ordinary distant background. Unity's
  Skybox workflow does not require placing a sphere GameObject in the scene.
- For supported Built-in/URP workflows, a Panoramic skybox maps a latitude-
  longitude texture around the scene. Check 180/360-degree image layout,
  rotation, exposure, and texture seams instead of assuming a cubemap layout.
- Check camera background configuration and any camera-specific Skybox override;
  a per-camera background does not imply separate scene ambient lighting.
- In HDRP, use the appropriate sky Volume override, such as HDRI Sky, rather
  than assuming legacy Skybox materials or camera overrides control its sky.
- A custom sphere/dome mesh is a project rendering choice, not a dedicated
  built-in Sky Sphere component. For that choice, validate inward visibility
  or shader culling, unlit shading, depth/render ordering, clipping, and scale.
- Disable unnecessary colliders/shadow casting on a decorative dome and verify
  that camera travel does not reveal its finite extent. Check each camera and
  capture path rather than moving one shared dome for incompatible cameras.
- Align painted sun/moon features with the actual directional illumination.
  Check interaction with clouds/fog and avoid duplicated or occluded sky layers.
- Preserve shared mesh/material assets, reuse runtime-owned instances, and
  schedule environment/probe updates separately from background changes.

Sources: [Using skyboxes](https://docs.unity3d.com/6000.0/Documentation/Manual/skyboxes-using.html),
[Panoramic skybox](https://docs.unity3d.com/6000.0/Documentation/Manual/shader-skybox-panoramic.html).
Custom mesh checks above are project recommendations; verify the chosen shader
and pipeline rather than treating them as a Unity Sky Sphere API contract.

### HDRP: Visual Environment and Physically Based Sky

- Use a Volume with Visual Environment and select the intended Sky Type; add
  the corresponding sky override, such as Physically Based Sky or HDRI Sky.
- Confirm override checkboxes, Volume influence/priority, and camera Volume
  selection before diagnosing parameters that appear to have no effect.
- Select Static or Dynamic ambient mode for the intended baked/current sky
  workflow; verify the environment lighting update mode for that HDRP version.
- Configure the Directional Light's sky/celestial-body participation using the
  installed HDRP controls. Unreal's Atmosphere Sun Light Index is not a Unity API.
- Tune air/aerosol scattering, absorption, planetary settings, and atmospheric
  perspective using the chosen sky model. Fog remains a separate override;
  inspect their combined result instead of duplicating haze unintentionally.
- Preserve shared Volume Profile assets. Decide whether changes are shared
  authoring edits or runtime-owned overrides before modifying them.
- Profile sky precomputation/update cost when changing atmospheric parameters
  or light direction; the cost depends on HDRP's rendering-space/settings.

### Built-in and URP: Fog and Cloud Scope

- Use the selected pipeline's supported fog workflow. For conventional scene
  fog, inspect the applicable Lighting/RenderSettings controls, fog color, and
  Linear versus Exponential/Exponential Squared mode.
- Tune start/end distance for Linear mode or density for exponential modes;
  verify the shaders actually support fog, including custom/transparent shaders.
- Distance fog is not a simulated volume with shadowed light shafts. Skybox
  cloud textures are likewise not ray-marched volumetric clouds.
- Do not assume HDRP's Volumetric Clouds, Fog overrides, or Local Volumetric Fog
  can be added to Built-in/URP. Verify installed packages/custom render features,
  camera support, and target hardware before choosing an implementation.
- Do not switch render pipelines or install a cloud/fog dependency as an
  incidental fix for a local environment edit.

Sources: [Lighting window](https://docs.unity3d.com/6000.0/Documentation/Manual/lighting-window.html),
[RenderSettings.fog](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderSettings-fog.html).

### HDRP Volumetric Clouds

- Check HDRP Asset and camera Frame Settings support, then configure the
  Volumetric Clouds Volume override and its enabled property overrides.
- Distinguish volumetric clouds from the separate Cloud Layer feature. Their
  appearance, cost, and cloud-map formats are not interchangeable.
- Start with an appropriate preset/control mode; tune density, shape/erosion,
  altitude/thickness, and wind for the intended camera views.
- Verify sunlight/ambient contribution, cloud shadows, and reflection behavior
  independently. Preserve shared Volume Profiles and cloud textures.
- Balance primary/light step counts and temporal accumulation using GPU
  measurements. More accumulation can reduce noise while increasing ghosting;
  test ghosting reduction for flicker as well as visual sharpness.
- Check ground views and any required flight through/above clouds. Avoid
  regenerating cloud textures or rewriting unchanged profile values every frame.

Source: [HDRP Volumetric Clouds](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/volumetric-clouds-volume-override-reference.html).

### HDRP Fog and Local Volumetric Fog

- Configure the Fog Volume override and verify HDRP Asset/camera support before
  enabling Volumetric Fog. Confirm Volume influence and property override states.
- Tune fog attenuation/density, base/maximum height, and color for scene scale;
  use the installed version's property meanings and units.
- For volumetrics, balance fog distance, screen resolution, and depth slices
  or the provided quality budget. Increasing distance alone can sacrifice detail.
- Inspect per-light volumetric contribution and shadow settings; surface light
  intensity is not the only control on fog brightness.
- Choose denoising for actual camera/light motion. Reprojection can ghost under
  dynamic lighting; higher-quality combined filtering has additional cost.
- Use Local Volumetric Fog components for bounded regional density, with
  deliberate size, blend distances, masks/materials, and overlap. These are
  spatial fog components, not merely local Volume Profile influence boxes.
- Keep global fog active/configured as required by the local volumetric workflow.
  Verify local boundaries and avoid visible seams or unintended density stacking.

Sources: [HDRP Fog](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/fog-volume-override-reference.html),
[Local Volumetric Fog](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/local-volumetric-fog-volume-reference.html).

### Verification and Update Budget

- Give sun rotation, sky settings, ambient updates, and reflection refreshes
  explicit ownership. Avoid multiple scripts overwriting the same state.
- Compare at controlled exposure and then test the intended adaptation.
- Check daytime, sunset, night, interiors, shadows, and reflective objects in
  Game view/Player, not only a Scene view with different lighting toggles.
- Confirm persistence after save/reopen and behavior after scene transitions.
  Record the pipeline, lighting mode, capture cadence, and CPU/GPU cost.
- Test moving cameras/lights, cloud transitions, fog boundaries, transparent
  materials, and camera cuts for trails, flicker, or stale temporal history.
  Measure clouds, fog, shadows, and reflections separately on target quality
  settings; verify save/reopen and runtime-owned profile cleanup.

Sources: [Light component](https://docs.unity3d.com/6000.0/Documentation/Manual/class-Light.html),
[Lighting window](https://docs.unity3d.com/6000.0/Documentation/Manual/lighting-window.html),
[Using skyboxes](https://docs.unity3d.com/6000.0/Documentation/Manual/skyboxes-using.html),
[DynamicGI.UpdateEnvironment](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/DynamicGI.UpdateEnvironment.html),
[HDRP Visual Environment](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/Override-Visual-Environment.html),
[HDRP Physically Based Sky](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/physically-based-sky-volume-override-reference.html),
[HDRP Light component](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/Light-Component.html),
[HDRP Environment Lighting](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/Environment-Lighting.html).

## 27. Official Reference Policy

Topic sections link directly to Unity Manual, Scripting API, and official
package documentation. Before implementation, select the exact Editor/package
version used by the project. Confirm compatibility and API availability rather
than assuming a research reference's version is installed. For topics such as
networking or Addressables, first discover the package, then consult its own
official versioned documentation; this template does not choose a stack.

## Maintenance and Adoption (2026-11-11)

- Discover actual versions, dependency locks, CI commands, targets, and ownership
  before replacing profile placeholders. Keep an existing project baseline pinned
  unless upgrading is authorized; examples are not facts about another repository.
- Record repeatable verification as command, working directory, prerequisites,
  and expected result. Use checked-in commands/wrappers; do not invent a build
  command or claim a client loaded a rule because its file parsed successfully.
- Match vendor sources to the installed release and separate documented behavior
  from local policy and performance recommendations. Record review date and scope;
  do not relabel every historical API link as revalidated.
- Reconcile contradictory instructions in place. Keep essential boundaries in
  native entry points and load detailed references for relevant tasks.
- Validate affected code/assets/contracts. For documentation-only work check
  links, syntax, examples, and consistency; do not require unrelated builds or
  add tests that only restate prose. Report unavailable checks and remaining work.
- Preserve user changes, byte-order/line-ending conventions, asset identity,
  credentials, and destination-specific settings during adoption.

## 28. Earth Rotation, Revolution, Seasons, and Celestial Position

Follow [CELESTIAL_TIME.md](CELESTIAL_TIME.md) for Earth rotation/revolution,
all 12 months in both hemispheres, the equator/latitude distinction, UTC and
longitude inputs, Sun/Moon horizon coordinates, lunar parallax/phase, and
real-time scheduling. This document also defines the Unity lighting adapter.
Keep astronomical position, seasonal weather/art, and rendering policy separate.
Use the installed engine/package documentation and validate the selected
accuracy mode; a directional light is not an ephemeris solver.

## 29. UI Text Linked to Actual Values

Reviewed: 2026-10-11. Discover TextMeshPro/uGUI/UI Toolkit or the existing custom
UI system first. Keep the raw model value authoritative and format only its
presentation. Match API/package versions instead of assuming Unreal nodes exist.

### Conversion Flows and Unity Types

The requested general flows are:

```text
Actual value -> To String -> text/string-compatible presentation
             -> Set Text / text-typed Set Value

Actual value -> To Text (supported formatting operation)
             -> Set Text / text-typed Set Value
```

Unity normally displays strings, not Unreal's separate FText type. The first
flow therefore usually becomes value.ToString(format, culture) -> SetText or
a text property, with no additional To Text conversion. In Visual Scripting,
use available conversion/member units for the actual component and inspect
their input/output types; a unit named To Text is not guaranteed to exist.

A direct formatting API can realize the second flow without an intermediate
ToString, for example TMP_Text.SetText with a supported numeric format overload.
This formats a display value; it does not create automatic model binding.

| Destination | Appropriate display update |
|---|---|
| TMP_Text / TextMeshProUGUI | SetText(formattedString), text property, or supported numeric SetText overload |
| Legacy uGUI Text | text = formattedString |
| UI Toolkit Label / TextElement | text = formattedString |
| UI Toolkit TextField | value = formattedString, or SetValueWithoutNotify for a deliberate silent update |
| Slider / numeric field | Set the actual numeric value; format its label separately |

Set Value is not interchangeable with Set Text: inspect whether the destination
expects a string, number, or another data type. A UI Toolkit TextField.value is
an editable string value; a numeric field's value is its numeric type.

Sources: [TextElement.text](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.TextElement-text.html),
[TextField](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.TextField.html).

### C# Examples

```csharp
// Snippets inside a valid UI update function with valid cached UI references.
// Explicit string route: choose the game's culture and display precision.
string display = currentValue.ToString("F1", displayCulture);
tmpLabel.SetText(display);
uiToolkitLabel.text = display;

// Direct TMP formatting route for a suitable float, using supported TMP syntax:
tmpLabel.SetText("{0:0.0}", floatValue);
```

The two TMP lines illustrate alternatives, not two writes required for every
update. TMP numeric formatting has its own supported syntax/overloads; do not
assume every .NET format/culture feature is equivalent. Do not convert large
integers or precise double values to float solely to use that overload.
For localized labels and units, use the installed localization workflow and
an explicit culture policy. Invariant formatting is a choice for technical
data, not a universal requirement for user-facing UI.

Source: [TMP_Text.SetText](https://docs.unity3d.com/ja/Packages/com.unity.textmeshpro@3.0/api/TMPro.TMP_Text.SetText.html).
This is a package reference baseline, not an instruction to install that version.

### Synchronization and Verification

- Cache/validate the intended component or query result; do not search or recreate
  the UI every frame. Mutate UI on its supported/main thread.
- Populate initial state, subscribe to authoritative model changes, and detach
  subscriptions for the owning lifetime. Rebind after scene/view recreation.
- Use one update owner: an explicit presenter or the project's binding system.
  A one-time conversion/assignment alone does not keep values synchronized.
- Suppress feedback loops for editable fields when deliberately reflecting model
  state; use a supported no-notify setter where appropriate. User edits need
  validated parsing and an explicit model commit, not repeated self-assignment.
- Update on visible value changes, coalesce rapid telemetry, and invalidate the
  display cache when culture, precision, or units change. Do not claim zero
  allocations merely because an API is named SetText.
- For celestial UI, use the same calculation snapshot as the lighting adapter.
  Do not parse rounded azimuth/altitude/phase text back into the solver.
- Check initial display, live changes, zero/negative/large values, decimal
  separators, precision, units, localization, missing references, recreation,
  and listener cleanup. Document-only checks do not prove an implemented UI works.

### Numeric and Asynchronous UI State (2026-11-11)

Reject or explicitly present NaN/infinity, unavailable samples, and invalid ranges
before formatting. Choose placeholder/error/stale-state behavior deliberately;
do not silently display an invalid sample as a real zero. Preserve numeric type
and units through the model-to-view boundary; display rounding is not simulation
rounding. Accept only the latest applicable async request result, keyed to the
active model/location/time/view, so a delayed response cannot overwrite new state.
Test culture changes with a stationary value and recreate/unbind the view without
leaking listeners or overwriting an active binding.
