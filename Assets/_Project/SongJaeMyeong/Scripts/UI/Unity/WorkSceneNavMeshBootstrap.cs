using System;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.UI.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// SJM_UI_Work 편의점 검증 씬에서 손님·보행자·알바 캡슐이 바로 이동할 수 있도록
    /// 실행 직전 런타임 NavMesh를 생성하고, UI 일시정지/일차와 손님 NPC 시스템을 동기화한다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class WorkSceneNavMeshBootstrap : MonoBehaviour
    {
        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private NpcPopulationController npcPopulation;
        [SerializeField] private NavMeshAgent demoEmployeeAgent;

        private bool lastPauseState;
        private int lastDay = -1;
        private bool deferredToIntegratedNpcSession;

        private void Awake()
        {
            HideLegacyPrimitiveRegisters();
            EnsureLightingVariantStabilizer();

            NavMeshSurface surface = GetComponent<NavMeshSurface>();
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy)
            {
                return;
            }

            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                surface.AddData();
            }
        }

        private void EnsureLightingVariantStabilizer()
        {
            if (GetComponent<StoreLightingVariantStabilizer>() == null)
            {
                gameObject.AddComponent<StoreLightingVariantStabilizer>();
            }
        }

        private static void HideLegacyPrimitiveRegisters()
        {
            GameObject mapRoot = GameObject.Find("ConvenienceStore_Map");
            if (mapRoot == null) return;

            Transform[] descendants = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform tr = descendants[i];
                if (tr == null || !tr.gameObject.activeSelf) continue;
                if (tr.name.StartsWith("Register_Base", StringComparison.Ordinal) ||
                    tr.name.StartsWith("Register_Screen", StringComparison.Ordinal))
                {
                    tr.gameObject.SetActive(false);
                }
            }
        }

        private void Start()
        {
            Type integratedNpcType = Type.GetType("EmployeeVoice.IntegratedNpcSession, Assembly-CSharp");
            if (integratedNpcType != null)
            {
                UnityEngine.Object found = FindAnyObjectByType(integratedNpcType);
                deferredToIntegratedNpcSession = found is MonoBehaviour mb && mb.isActiveAndEnabled;
            }

            if (demoEmployeeAgent == null)
            {
                GameObject empObj = GameObject.Find("Demo_Employee_NPC");
                if (empObj != null)
                {
                    demoEmployeeAgent = empObj.GetComponent<NavMeshAgent>();
                }
            }

            if (demoEmployeeAgent != null && demoEmployeeAgent.isActiveAndEnabled)
            {
                // 좁은 계산대 통로에서 정지한 손님 캡슐에 막혀 [이동 실패(Stalled)]가 나지 않도록
                // 벽/매대 NavMesh 경계는 유지하되 에이전트 간 강제 밀침 충돌 회피를 해제한다.
                demoEmployeeAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                demoEmployeeAgent.avoidancePriority = 5;
                demoEmployeeAgent.radius = 0.24f;
                demoEmployeeAgent.autoRepath = true;

                if (!demoEmployeeAgent.isOnNavMesh)
                {
                    NavMeshQueryFilter filter = new NavMeshQueryFilter
                    {
                        agentTypeID = demoEmployeeAgent.agentTypeID,
                        areaMask = demoEmployeeAgent.areaMask
                    };

                    if (NavMesh.SamplePosition(demoEmployeeAgent.transform.position, out NavMeshHit hit, 2.5f, filter))
                    {
                        demoEmployeeAgent.Warp(hit.position);
                    }
                }
            }

            SyncNpcState(force: true);
        }

        private void Update()
        {
            SyncNpcState(force: false);
        }

        private void SyncNpcState(bool force)
        {
            if (deferredToIntegratedNpcSession || npcPopulation == null || uiController == null)
            {
                return;
            }

            bool shouldPause = uiController.CurrentMode != UiScreenMode.InGame ||
                               uiController.IsPauseMenuOpen ||
                               uiController.IsSettingsOpen ||
                               uiController.IsTransitionFading;
            if (force || shouldPause != lastPauseState)
            {
                lastPauseState = shouldPause;
                npcPopulation.SetPaused(shouldPause);
            }

            int day = Mathf.Max(1, uiController.CurrentDayNumber);
            if (force || day != lastDay)
            {
                lastDay = day;
                npcPopulation.SetDay(day);
            }
        }
    }

    /// <summary>
    /// URP 환경에서 낮→밤 전환 시 태양 광원이 정확히 0으로 꺼지거나 달빛 추가 광원이 처음 켜질 때
    /// 셰이더 배리언트(_MAIN_LIGHT_SHADOWS / _ADDITIONAL_LIGHTS)가 런타임 전환되며
    /// 물건이 0.1초간 파란색(시안)으로 깜빡이는 현상을 방지한다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class StoreLightingVariantStabilizer : MonoBehaviour
    {
        private const float MinMainLightIntensityFloor = 0.0005f;
        private Light interiorVariantAnchorLight;

        private void Awake()
        {
            EnsureInteriorVariantAnchorLight();
        }

        private void LateUpdate()
        {
            Light sun = RenderSettings.sun;
            if (sun != null && sun.enabled && sun.intensity < MinMainLightIntensityFloor)
            {
                sun.intensity = MinMainLightIntensityFloor;
            }
        }

        private void OnDestroy()
        {
            if (interiorVariantAnchorLight != null)
            {
                Destroy(interiorVariantAnchorLight.gameObject);
                interiorVariantAnchorLight = null;
            }
        }

        private void EnsureInteriorVariantAnchorLight()
        {
            if (interiorVariantAnchorLight != null) return;

            GameObject anchorObj = new GameObject("URP_AdditionalLightVariantAnchor");
            anchorObj.transform.SetParent(transform, false);
            anchorObj.transform.position = new Vector3(0f, 3.2f, 0f);

            interiorVariantAnchorLight = anchorObj.AddComponent<Light>();
            interiorVariantAnchorLight.type = LightType.Point;
            interiorVariantAnchorLight.color = new Color(1f, 0.98f, 0.94f, 1f);
            interiorVariantAnchorLight.intensity = 0.01f;
            interiorVariantAnchorLight.range = 45f;
            interiorVariantAnchorLight.shadows = LightShadows.None;
        }
    }
}
