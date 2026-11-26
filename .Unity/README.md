# Unity AI Guidelines

Reusable English-language instructions for AI assistants and coding agents working
on Unity projects. The package is engine-specific but project-independent.

It covers:

- Unity Editor and command-line workflows;
- C# gameplay and tool development;
- assets, `.meta` files, scenes, prefabs, and serialization;
- source/asset-package import and reimport, packages, asmdef/asmref boundaries,
  base classes, and documented built-in/package API references;
- managed/native plug-in governance and platform compatibility;
- MCP security, version/capability compatibility, and failure recovery;
- Git, Unity Version Control, Perforce, LFS/locking, and UnityYAMLMerge workflows;
- Built-in/URP/HDRP/custom rendering, Render Graph, shaders/variants, materials,
  batching, GPU resources, target profiling, and visual regression;
- Animator/AnimationClip and root motion, Timeline/Playables, audio and mixers,
  Particle System/VFX Graph, sprites/atlases/2D content, and GameObject composition;
- Terrain/large-world content, 2D/3D collision and queries, Tags, Layers, Sorting
  Layers, and system-specific masks;
- lifecycle, async work, performance, managed/native/GPU memory allocation and
  release, controlled GC/unload boundaries, and repeated-cycle leak verification;
- safe file cleanup plus versioned Asset Store/UPM/vendor/source-asset refresh,
  replacement, compatibility, local-override, license, rollback, and validation rules;
- Task/Awaitable/coroutine/Job System scheduling, main/background thread ownership,
  resource budgets, cancellation, synchronization, and deadlock prevention;
- Core/Unity Runtime/Editor boundaries and DTO-interface-service-controller CRUD flow;
- Addressables handles/catalogs/content updates and Web/CORS/API security;
- Profiler/Statistics evidence and CPU/GPU allocation, residency, synchronization,
  and lifetime rules;
- AR/VR/XR providers, tracking rigs, interaction, comfort, rendering, permissions,
  device testing, and networked ownership;
- first-person, third-person, side-scroller, 2D, 2.5D, and 3D gameplay-space rules;
- offline/single-player and multiplayer manager/session/transport/delegate lifecycles;
- practical SOLID boundaries and ScriptableObject/Addressables/asmdef integration;
- Behavior Tree/Unity Behavior, FSM, Utility AI, GOAP and hybrid decision contracts,
  blackboards, aborts, authority, traces, and agent-scale budgets;
- race-condition prevention, Burst compatibility/AOT evidence, pre/post-processing
  pipelines, algorithm complexity/determinism, and Update/PlayerLoop minimization;
- justified Singleton/Observer/EventBus usage with explicit scope, delivery,
  reentrancy, diagnostics, and teardown;
- reusable initial game design, game development plan, and evidence-based development
  report templates with requirement-to-test traceability;
- EditMode, PlayMode, and player testing;
- builds for Mono, IL2CPP, and target platforms; and
- JetBrains AI Assistant, Junie, GitHub Copilot, Codex, and other
  `AGENTS.md`-compatible tools.

These files use Unity 6.6 (`6000.6.x`) as their baseline and remain independent
of WhiteOut. Verify the exact patch, render pipeline, input system, networking
library, and architecture from the target project.

<!-- AI_MANUAL_ADOPTION_START -->
## AI_Manual 적용 및 유지보수

- 대상: Unity. 진입 파일은 `AGENTS.md`, 협업·개발 규칙은 `PROJECT_CONVENTION.md`입니다.
- 폴더 자체는 배포용 묶음입니다. 실제 프로젝트에는 필요한 파일을 원래의 상대
  경로대로 복사하고, 기존 지침·설정과 병합합니다. 폴더를 보관하는 것만으로
  모든 도구가 지침을 자동으로 읽는다고 가정하지 않습니다.
- 프로젝트 정보와 실행 명령을 확인해 빈 값·`TBD`·과거 프로젝트 예시를 교체합니다.
- 공통 기준은 협업 규칙의 `Shared Game Development Collaboration Contract`입니다.
  시스템 소유자, 변경 범위, 인계 내용, 직렬화·API 호환성, 검증 증거를 합의합니다.
- Unity와 도구 지침을 함께 쓸 때는 도구 파일을 프로젝트 루트에, 엔진 지침 3개를
  `docs/ai/unity/`에 배치합니다. `.Unity`의 단독 설치는 엔진 파일을 루트에 둡니다.
  `Templates/`는 프로젝트 루트에 복사하거나 모든 참조 경로를 함께 조정합니다.
- 로드 확인: 에이전트에 적용한 지침 경로와 협업 규칙을 요약하도록 요청하고,
  파일 로드 표시가 제공되면 함께 확인합니다. 적용 실패는 경로와 도구 버전부터 점검합니다.
- 업데이트 시 기존 프로젝트 예외를 보존하고 공통 계약을 모든 패키지에 동일하게
  반영합니다. 기술 규칙은 공식 URL·적용 버전·확인 날짜·영향 범위를 기록합니다.
- 이 업데이트는 기존 엔진·IDE 전체 API의 최신성 재검증을 의미하지 않습니다.
  프로젝트의 실제 버전에 맞춰 해당 기술 규칙을 추가 확인합니다.
<!-- AI_MANUAL_ADOPTION_END -->

## Files

```text
Unity-AI-Guidelines/
|-- AGENTS.md
|-- PROJECT_CONVENTION.md
|-- CODE_REVIEW_RULES.md
|-- PLATFORM_SETUP.md
|-- .aiignore.example
|-- Templates/
|   |-- INITIAL_GAME_DESIGN_DOCUMENT_TEMPLATE.md
|   |-- GAME_DEVELOPMENT_PLAN_TEMPLATE.md
|   `-- GAME_DEVELOPMENT_REPORT_TEMPLATE.md
|-- .aiassistant/
|   `-- rules/
|       `-- unity-engineering.md
|-- .junie/
|   `-- AGENTS.md
`-- .github/
    |-- copilot-instructions.md
    `-- instructions/
        |-- unity-code.instructions.md
        `-- unity-content.instructions.md
```

## Unity 6.6 적용 기준

이 패키지의 기준은 **Unity 6.6 (`6000.6.x`)**입니다. 정확한 패치는 프로젝트에서
확인합니다. `PROJECT_CONVENTION.md`의 Unity 6.6 Baseline을 읽고 Play Mode 상태 초기화,
기본 Burst 모듈, 직렬화와 빌드 진단 설정을 검토합니다. 패키지 버전은 별도로 확인하며
문서 업데이트를 실제 프로젝트 업그레이드로 해석하지 않습니다.

Unreal Engine 지침은 `templates/unreal-engine/`에 별도로 제공하며 혼합 적용하지 않습니다.

## Installation

1. Copy the contents into the Unity project root—the directory containing
   `Assets/`, `Packages/`, and `ProjectSettings/`.
2. Complete every applicable `TBD` field in `PROJECT_CONVENTION.md`.
3. Remove sections for packages or systems the project does not use.
4. Configure the native agent files as described in `PLATFORM_SETUP.md`.
5. Review `.aiignore.example` before renaming it to `.aiignore`.
6. Copy and complete the applicable files under `Templates/`; keep the reusable
   originals unchanged and store project documents in the approved documentation path.

Do not use this template as a replacement for verified project facts. The exact
Editor version, package versions, build targets, scripting backend, render pipeline,
input stack, and test commands must be discovered from the project.

## Source-of-Truth Design

- `AGENTS.md` defines stable agent behavior and Unity-safe workflows.
- `PROJECT_CONVENTION.md` defines engineering and content conventions.
- Native integration files provide concise adapters for each AI tool.
- `CODE_REVIEW_RULES.md` contains review-only checks.

Keep detailed policy in the root files to avoid contradictory copies.

## Important Notes

- Keep each Unity asset or folder with its `.meta` file and preserve the GUID.
- Never treat `Library/`, `Temp/`, `Logs/`, generated solution files, or package
  cache content as source.
- A successful IDE compile does not replace Unity script compilation or a target
  player build.
- Scene, prefab, and binary asset changes should use the Unity Editor or another
  Unity-aware tool whenever references or serialization may be affected.
- This package is provided as original files only; no ZIP archive is created.

## Official References

- Unity asset metadata:
  https://docs.unity3d.com/6000.6/Documentation/Manual/AssetMetadata.html
- Unity script serialization:
  https://docs.unity3d.com/6000.6/Documentation/Manual/script-serialization.html
- Unity domain reload:
  https://docs.unity3d.com/6000.6/Documentation/Manual/domain-reloading.html
- Unity Test Framework:
  https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.test-framework.html
- Unity Package Manager lock files:
  https://docs.unity3d.com/6000.6/Documentation/Manual/upm-conflicts-auto.html
- Unity 6.6 garbage collector:
  https://docs.unity3d.com/6000.6/Documentation/Manual/performance-garbage-collector.html
- Unity memory overview:
  https://docs.unity3d.com/6000.6/Documentation/Manual/performance-memory-overview.html
- Unity AssetDatabase refresh and scoped reserialization:
  https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AssetDatabase.Refresh.html
  https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AssetDatabase.ForceReserializeAssets.html
- Unity Asset Store package updates:
  https://docs.unity3d.com/6000.6/Documentation/Manual/upm-ui-update2.html
- Unity Profiler and Highlights:
  https://docs.unity3d.com/6000.6/Documentation/Manual/Profiler.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/ProfilerHighlights.html
- Unity XR input and XR Interaction Toolkit:
  https://docs.unity3d.com/6000.6/Documentation/Manual/xr_input.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.xr.interaction.toolkit.html
- Netcode for GameObjects NetworkManager:
  https://docs-multiplayer.unity3d.com/netcode/current/components/networkmanager/
- Unity plug-in configuration:
  https://docs.unity3d.com/6000.6/Documentation/Manual/plug-in-inspector.html
- Unity Smart Merge:
  https://docs.unity3d.com/6000.6/Documentation/Manual/SmartMerge.html
- Unity SRP Batcher:
  https://docs.unity3d.com/6000.6/Documentation/Manual/SRPBatcher.html
- Unity shader variants:
  https://docs.unity3d.com/6000.6/Documentation/Manual/shader-variants.html
- Unity URP render graph:
  https://docs.unity3d.com/6000.6/Documentation/Manual/urp/render-graph.html
- Unity Frame Debugger:
  https://docs.unity3d.com/6000.6/Documentation/Manual/FrameDebugger.html
- Unity animation system overview:
  https://docs.unity3d.com/6000.6/Documentation/Manual/AnimationOverview.html
- Unity Timeline package:
  https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/index.html
- Unity audio:
  https://docs.unity3d.com/6000.6/Documentation/Manual/Audio.html
- Unity particle systems and Visual Effect Graph:
  https://docs.unity3d.com/6000.6/Documentation/Manual/ParticleSystems.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/VFXGraph.html
- Unity Sprite Atlas properties:
  https://docs.unity3d.com/6000.6/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html
- Unity GameObjects and components:
  https://docs.unity3d.com/6000.6/Documentation/Manual/class-GameObject.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/Components.html
- Unity asset import, assembly definitions, Tags/Layers, collision, and Terrain:
  https://docs.unity3d.com/6000.6/Documentation/Manual/ImportingAssets.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/assembly-definition-files.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/class-TagManager.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/LayerBasedCollision.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/terrain-UsingTerrains.html
- Unity Awaitable, Job System, Addressables, and Web CORS:
  https://docs.unity3d.com/6000.6/Documentation/Manual/async-awaitable-continuations.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/job-system-overview.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/job-system-native-container.html
  https://docs.unity.com/en-us/engine/6000.6/manual/whats-new/unity66
  https://docs.unity.cn/Packages/com.unity.addressables%403.0/manual/AddressableAssetsAsyncOperationHandle.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/webgl-networking.html
- Unity Behavior and PlayerLoop/update optimization:
  https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.behavior.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/player-loop-customizing.html
  https://docs.unity3d.com/6000.6/Documentation/Manual/events-per-frame-optimization.html
- MCP versioning and compatibility:
  https://modelcontextprotocol.io/specification/latest/basic/versioning
- MCP authorization:
  https://modelcontextprotocol.io/specification/latest/basic/authorization
- MCP security best practices:
  https://modelcontextprotocol.io/docs/tutorials/security/security_best_practices
