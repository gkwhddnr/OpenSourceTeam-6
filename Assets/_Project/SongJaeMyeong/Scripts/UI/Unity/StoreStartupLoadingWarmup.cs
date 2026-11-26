using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenSourceTeam6.Customers.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenSourceTeam6.UI.Unity
{
    using Camera = UnityEngine.Camera;
    using Time = UnityEngine.Time;

    /// <summary>
    /// 1일차 검은 화면 전환("1일차" 배너) 뒤에서 118종 손님·행인 캐릭터와 매장 머티리얼의
    /// 고유 셰이더 배리언트 조합을 낮·야간 조명 조건으로 조용히 사전 워밍업하여
    /// 별도 로딩창 없이 초기 스터터링 및 야간 전환 시 시안색(Compiling shader) 현상을 방지한다.
    /// </summary>
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class StoreStartupLoadingWarmup : MonoBehaviour
    {
        private const int MaxNonCharacterProxyCount = 48;

        public static bool IsWarmingUp { get; private set; }
        public static bool HasCompletedWarmup { get; private set; }

        [Header("셰이더 워밍업 설정")]
        [SerializeField, Min(1f)] private float maxWaitForCompileSeconds = 18f;

        private GameObject warmupProxyRoot;
        private readonly List<Material> transientMaterials = new List<Material>();
        private Coroutine warmupCoroutine;

        private StoreLightingVariantStabilizer activeStabilizer;
        private Light savedSunLight;
        private float savedSunIntensity;
        private Color savedSunColor;
        private AmbientMode savedAmbientMode;
        private Color savedAmbientLight;
        private bool hasSavedLightingState;
        private bool isNightWarmupPhaseActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsWarmingUp = false;
            HasCompletedWarmup = false;
        }

        public static bool BeginSilentWarmup(MonoBehaviour hostFallback = null)
        {
            if (HasCompletedWarmup || IsWarmingUp) return false;

            StoreStartupLoadingWarmup runner = FindAnyObjectByType<StoreStartupLoadingWarmup>(FindObjectsInactive.Include);
            if (runner == null || !runner.gameObject.activeInHierarchy || !runner.enabled)
            {
                GameObject hostObj = hostFallback != null && hostFallback.gameObject.activeInHierarchy
                    ? hostFallback.gameObject
                    : new GameObject("StoreShaderWarmupRunner");
                runner = hostObj.GetComponent<StoreStartupLoadingWarmup>();
                if (runner == null)
                {
                    runner = hostObj.AddComponent<StoreStartupLoadingWarmup>();
                }
            }

            return runner.TryStartSilentWarmup();
        }

        private bool TryStartSilentWarmup()
        {
            if (HasCompletedWarmup || IsWarmingUp || warmupCoroutine != null || !isActiveAndEnabled)
            {
                return false;
            }

            IsWarmingUp = true;
            warmupCoroutine = StartCoroutine(RunSilentWarmupRoutine());
            return true;
        }

        private void LateUpdate()
        {
            PositionProxyRootInFrontOfCamera(ResolveActiveCamera());
            if (isNightWarmupPhaseActive)
            {
                ApplyNightWarmupLightingState();
            }
        }

        private void OnDisable()
        {
            CleanupAll();
        }

        private void OnDestroy()
        {
            CleanupAll();
        }

        private IEnumerator RunSilentWarmupRoutine()
        {
            yield return null;

            Camera activeCam = ResolveActiveCamera();
            BuildWarmupProxies(activeCam);

            // 1. 낮 조명 조건에서 프록시 2프레임 렌더링
            yield return null;
            yield return null;

            // 2. 야간 조명(최소 태양광 + 달빛 + 야간 환경광) 조건에서 프록시 2프레임 렌더링
            GameObject tempMoonObj = EnterNightWarmupLighting();
            yield return null;
            yield return null;
            ExitNightWarmupLighting(tempMoonObj);

            // 3. 에디터 비동기 셰이더 컴파일이 끝날 때까지 대기 (암전 상태 유지)
#if UNITY_EDITOR
            float compileStart = Time.unscaledTime;
            while (UnityEditor.ShaderUtil.anythingCompiling &&
                   Time.unscaledTime - compileStart < maxWaitForCompileSeconds)
            {
                yield return null;
            }
#endif

            yield return null;
            yield return null;

            DestroyWarmupProxiesAndMaterials();
            HasCompletedWarmup = true;
            IsWarmingUp = false;
            warmupCoroutine = null;
        }

        private void BuildWarmupProxies(Camera activeCam)
        {
            warmupProxyRoot = new GameObject("StartupShaderWarmupProxies");
            PositionProxyRootInFrontOfCamera(activeCam);

            HashSet<string> seenVariantKeys = new HashSet<string>(StringComparer.Ordinal);
            List<Transform> arrangedProxies = new List<Transform>();

            // A. 118종 로스터의 모든 고유 캐릭터 머티리얼 키워드 조합 수집 및 SkinnedMeshRenderer 프록시 생성
            List<Material> uniqueCharacterMaterials = CollectRosterMaterials(seenVariantKeys, out GameObject baseCharPrefab);
            if (baseCharPrefab != null)
            {
                for (int i = 0; i < uniqueCharacterMaterials.Count; i++)
                {
                    Transform proxy = CreateCharacterWarmupProxy(baseCharPrefab, uniqueCharacterMaterials[i], i);
                    if (proxy != null)
                    {
                        arrangedProxies.Add(proxy);
                    }
                }
            }

            // B. 씬 내 일반 MeshRenderer 및 런타임 하늘/비 셰이더의 미워밍업 고유 배리언트 수집
            List<Material> uniqueSceneMaterials = new List<Material>();
            Renderer[] sceneRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            for (int i = 0; i < sceneRenderers.Length && uniqueSceneMaterials.Count < MaxNonCharacterProxyCount; i++)
            {
                Renderer r = sceneRenderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;

                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int m = 0; m < mats.Length && uniqueSceneMaterials.Count < MaxNonCharacterProxyCount; m++)
                {
                    TryAddUniqueMaterial(mats[m], seenVariantKeys, uniqueSceneMaterials);
                }
            }

            AddTransientRuntimeShaderMaterials(seenVariantKeys, uniqueSceneMaterials);

            for (int i = 0; i < uniqueSceneMaterials.Count; i++)
            {
                Transform proxy = CreateQuadWarmupProxy(uniqueSceneMaterials[i], i);
                if (proxy != null)
                {
                    arrangedProxies.Add(proxy);
                }
            }

            const int cols = 8;
            const float spacing = 0.14f;
            int totalRows = Mathf.Max(1, Mathf.CeilToInt(arrangedProxies.Count / (float)cols));
            for (int i = 0; i < arrangedProxies.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = (col - (cols - 1) * 0.5f) * spacing;
                float y = ((totalRows - 1) * 0.5f - row) * spacing;
                arrangedProxies[i].localPosition = new Vector3(x, y, 0f);
            }
        }

        private List<Material> CollectRosterMaterials(HashSet<string> seenVariantKeys, out GameObject baseCharPrefab)
        {
            baseCharPrefab = null;
            List<Material> result = new List<Material>();

            NpcPopulationController npcPop = FindAnyObjectByType<NpcPopulationController>(FindObjectsInactive.Include);
            if (npcPop == null) return result;

            FieldInfo rosterField = typeof(NpcPopulationController).GetField("characterRoster", BindingFlags.Instance | BindingFlags.NonPublic);
            NpcCharacterRoster roster = rosterField != null ? rosterField.GetValue(npcPop) as NpcCharacterRoster : null;
            if (roster == null) return result;

            FieldInfo prefabsField = typeof(NpcCharacterRoster).GetField("characterPrefabs", BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject[] prefabs = prefabsField != null ? prefabsField.GetValue(roster) as GameObject[] : null;
            if (prefabs == null || prefabs.Length == 0) return result;

            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null) continue;

                SkinnedMeshRenderer smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (baseCharPrefab == null && smr != null)
                {
                    baseCharPrefab = prefab;
                }

                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Material[] mats = renderers[r] != null ? renderers[r].sharedMaterials : null;
                    if (mats == null) continue;

                    for (int m = 0; m < mats.Length; m++)
                    {
                        TryAddUniqueMaterial(mats[m], seenVariantKeys, result);
                    }
                }
            }

            return result;
        }

        private void AddTransientRuntimeShaderMaterials(HashSet<string> seenVariantKeys, List<Material> targetList)
        {
            Shader procSky = Shader.Find("Skybox/Procedural");
            if (procSky != null && targetList.Count < MaxNonCharacterProxyCount)
            {
                Material mat = new Material(procSky) { name = "Warmup_ProcSky" };
                mat.EnableKeyword("_SUNDISK_HIGHQUALITY");
                mat.SetFloat("_SunDisk", 2f);
                transientMaterials.Add(mat);
                TryAddUniqueMaterial(mat, seenVariantKeys, targetList);
            }

            string[] resourceShaders =
            {
                "TimeCelestials/SkyOverlay",
                "TimeCelestials/RainStreak",
                "TimeCelestials/RainSplash"
            };
            for (int i = 0; i < resourceShaders.Length && targetList.Count < MaxNonCharacterProxyCount; i++)
            {
                Shader s = Resources.Load<Shader>(resourceShaders[i]);
                if (s == null) continue;

                Material mat = new Material(s) { name = "Warmup_" + resourceShaders[i] };
                transientMaterials.Add(mat);
                TryAddUniqueMaterial(mat, seenVariantKeys, targetList);
            }
        }

        private Transform CreateCharacterWarmupProxy(GameObject baseCharPrefab, Material targetMat, int index)
        {
            GameObject slot = new GameObject($"CharWarmupProxy_{index}");
            slot.SetActive(false);
            slot.transform.SetParent(warmupProxyRoot.transform, false);
            slot.transform.localScale = Vector3.one * 0.04f;

            GameObject instance = Instantiate(baseCharPrefab, slot.transform, false);
            instance.name = $"ProxyModel_{index}";
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            Behaviour[] behaviours = instance.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    behaviours[i].enabled = false;
                }
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                {
                    bodies[i].isKinematic = true;
                }
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                int count = Mathf.Max(1, r.sharedMaterials.Length);
                Material[] filled = new Material[count];
                for (int m = 0; m < count; m++)
                {
                    filled[m] = targetMat;
                }

                r.sharedMaterials = filled;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                }
            }

            slot.SetActive(true);
            return slot.transform;
        }

        private Transform CreateQuadWarmupProxy(Material targetMat, int index)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"SceneWarmupQuad_{index}";
            quad.transform.SetParent(warmupProxyRoot.transform, false);
            quad.transform.localScale = Vector3.one * 0.04f;

            Collider col = quad.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = targetMat;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return quad.transform;
        }

        private GameObject EnterNightWarmupLighting()
        {
            if (activeStabilizer == null)
            {
                activeStabilizer = GetComponent<StoreLightingVariantStabilizer>() ?? FindAnyObjectByType<StoreLightingVariantStabilizer>();
            }

            if (activeStabilizer != null)
            {
                activeStabilizer.SuppressDuringWarmup = true;
            }

            savedSunLight = RenderSettings.sun;
            savedAmbientMode = RenderSettings.ambientMode;
            savedAmbientLight = RenderSettings.ambientLight;
            if (savedSunLight != null)
            {
                savedSunIntensity = savedSunLight.intensity;
                savedSunColor = savedSunLight.color;
            }

            hasSavedLightingState = true;
            isNightWarmupPhaseActive = true;
            ApplyNightWarmupLightingState();

            GameObject tempMoonObj = new GameObject("Warmup_TempMoonLight");
            tempMoonObj.transform.SetParent(warmupProxyRoot != null ? warmupProxyRoot.transform : transform, false);
            tempMoonObj.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            Light moon = tempMoonObj.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            moon.color = new Color(0.65f, 0.75f, 1f, 1f);
            moon.intensity = 0.12f;
            return tempMoonObj;
        }

        private void ApplyNightWarmupLightingState()
        {
            if (savedSunLight != null)
            {
                savedSunLight.intensity = 0.01f;
                savedSunLight.color = new Color(1f, 0.55f, 0.30f, 1f);
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.10f, 0.16f, 1f);
        }

        private void ExitNightWarmupLighting(GameObject tempMoonObj)
        {
            isNightWarmupPhaseActive = false;
            if (tempMoonObj != null)
            {
                Destroy(tempMoonObj);
            }

            RestoreLightingState();
        }

        private void RestoreLightingState()
        {
            if (hasSavedLightingState)
            {
                if (savedSunLight != null)
                {
                    savedSunLight.intensity = savedSunIntensity;
                    savedSunLight.color = savedSunColor;
                }

                RenderSettings.ambientMode = savedAmbientMode;
                RenderSettings.ambientLight = savedAmbientLight;
                hasSavedLightingState = false;
            }

            if (activeStabilizer != null)
            {
                activeStabilizer.SuppressDuringWarmup = false;
            }
        }

        private void PositionProxyRootInFrontOfCamera(Camera cam)
        {
            if (warmupProxyRoot == null || cam == null) return;

            Transform camTr = cam.transform;
            warmupProxyRoot.transform.SetPositionAndRotation(
                camTr.position + camTr.forward * 2.4f,
                camTr.rotation);
        }

        private static bool TryAddUniqueMaterial(
            Material mat,
            HashSet<string> seenVariantKeys,
            List<Material> targetList)
        {
            if (mat == null || mat.shader == null) return false;

            string[] keywords = mat.shaderKeywords;
            string sortedKeywords = keywords != null && keywords.Length > 0
                ? JoinSortedKeywords(keywords)
                : string.Empty;

            string key = $"{mat.shader.GetEntityId()}|{(mat.enableInstancing ? 1 : 0)}|{sortedKeywords}";
            if (!seenVariantKeys.Add(key)) return false;

            targetList.Add(mat);
            return true;
        }

        private static string JoinSortedKeywords(string[] keywords)
        {
            string[] copy = new string[keywords.Length];
            Array.Copy(keywords, copy, keywords.Length);
            Array.Sort(copy, StringComparer.Ordinal);
            return string.Join(",", copy);
        }

        private static Camera ResolveActiveCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.isActiveAndEnabled)
            {
                return mainCam;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            Camera best = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (best == null || cam.depth > best.depth)
                {
                    best = cam;
                }
            }

            return best;
        }

        private void DestroyWarmupProxiesAndMaterials()
        {
            if (warmupProxyRoot != null)
            {
                Destroy(warmupProxyRoot);
                warmupProxyRoot = null;
            }

            for (int i = 0; i < transientMaterials.Count; i++)
            {
                if (transientMaterials[i] != null)
                {
                    Destroy(transientMaterials[i]);
                }
            }
            transientMaterials.Clear();
        }

        private void CleanupAll()
        {
            if (warmupCoroutine != null)
            {
                StopCoroutine(warmupCoroutine);
                warmupCoroutine = null;
            }

            isNightWarmupPhaseActive = false;
            RestoreLightingState();
            DestroyWarmupProxiesAndMaterials();
            IsWarmingUp = false;
        }
    }
}
