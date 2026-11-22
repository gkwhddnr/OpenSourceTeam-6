using OpenSourceTeam6.Customers.Unity;
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

        private void Awake()
        {
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

        private void Start()
        {
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
            if (npcPopulation == null || uiController == null)
            {
                return;
            }

            bool shouldPause = uiController.IsPauseMenuOpen;
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
