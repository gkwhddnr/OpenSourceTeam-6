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

            if (demoEmployeeAgent != null && demoEmployeeAgent.isActiveAndEnabled && !demoEmployeeAgent.isOnNavMesh)
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
}
