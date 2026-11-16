using Unity.AI.Navigation;
using OpenSourceTeam6.Movement.Unity;
using UnityEngine;
using UnityEngine.AI;

namespace EmployeeVoice
{
    // This scene has no pre-baked navigation asset; build the surface before agents initialize.
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class StoreNavigationBootstrap : MonoBehaviour
    {
        [SerializeField] private EmployeeMovementController employee = null;

        private void Awake()
        {
            NavMeshSurface surface = GetComponent<NavMeshSurface>();
            // Awake can run before the surface's OnEnable, so isActiveAndEnabled is
            // not a reliable readiness check at this early execution order.
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy)
            {
                Debug.LogError("[StoreNavigation] 활성화된 NavMeshSurface가 없어 직원이 이동할 수 없습니다.", this);
                return;
            }

            surface.BuildNavMesh();
            if (surface.navMeshData == null)
                Debug.LogError("[StoreNavigation] 이동 가능 영역 생성에 실패했습니다. 바닥과 내비게이션 설정을 확인해 주세요.", this);
            else
            {
                // BuildNavMesh skips registration until OnEnable when called in Awake.
                // AddData is idempotent and makes it available before agent startup.
                surface.AddData();
                Debug.Log("[StoreNavigation] 편의점 이동 가능 영역을 생성했습니다.", this);
            }
        }

        private void Start()
        {
            if (employee == null) return;
            NavMeshAgent agent = employee.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.isActiveAndEnabled || agent.isOnNavMesh) return;

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 2f, filter) || !agent.Warp(hit.position))
                Debug.LogError("[StoreNavigation] 직원 시작 위치를 이동 가능 영역에 연결하지 못했습니다.", employee);
            else
                Debug.Log("[StoreNavigation] 직원 시작 위치를 이동 가능 영역에 연결했습니다.", employee);
        }
    }
}
