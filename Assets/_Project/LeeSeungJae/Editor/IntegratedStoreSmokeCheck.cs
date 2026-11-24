#if UNITY_EDITOR
using System;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.Movement.Unity;
using OpenSourceTeam6.UI.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace EmployeeVoice.Editor
{
    // Editor-only smoke check for the integrated scene; no keys or recordings are logged.
    [InitializeOnLoad]
    public static class IntegratedStoreSmokeCheck
    {
        private const string Running = "EmployeeVoice.IntegratedSmokeCheck";
        private static double started;
        private static bool dayStarted;
        private static bool commandSent;

        static IntegratedStoreSmokeCheck() { EditorApplication.update += Tick; }

        [MenuItem("Tools/LeeSeungJae/Run Integrated Store Smoke Check")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "IntegratedConvenienceStore")
                throw new InvalidOperationException("통합 씬만 연 편집 상태에서 실행하세요.");
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed < 3) return;
            var ui = UnityEngine.Object.FindAnyObjectByType<StoreUiSessionController>();
            var voice = UnityEngine.Object.FindAnyObjectByType<VoiceMovementIntegration>();
            var npc = UnityEngine.Object.FindAnyObjectByType<NpcPopulationController>();
            var inventory = UnityEngine.Object.FindAnyObjectByType<StoreInventoryManager>();
            var employee = UnityEngine.Object.FindAnyObjectByType<EmployeeMovementController>();
            var bridge = UnityEngine.Object.FindAnyObjectByType<IntegratedNpcSession>();
            if (!dayStarted)
            {
                if (ui == null || voice == null || npc == null || inventory == null || employee == null || bridge == null)
                {
                    Debug.LogError("[IntegratedStoreCheck] 필수 시스템 누락");
                    Stop();
                    return;
                }
                NavMeshAgent agent = employee.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.isOnNavMesh)
                {
                    Debug.LogError("[IntegratedStoreCheck] 직원 이동 영역 연결 실패");
                    Stop();
                    return;
                }
                ui.StartDay(1, 100000, 120f);
                int before = inventory.TotalRemainingStock;
                bool restocked = bridge.TryRestock("RAMEN", out string item);
                if (!restocked || inventory.TotalRemainingStock != before + 3)
                    Debug.LogError("[IntegratedStoreCheck] 재고 원장 보충 실패");
                else
                    Debug.Log("[IntegratedStoreCheck] 재고 보충 성공: " + item);
                dayStarted = true;
            }
            if (!commandSent && elapsed >= 6)
            {
                commandSent = true;
                voice.SendMessage("SubmitText", "라면 진열해줘");
                Debug.Log("[IntegratedStoreCheck] 중계서버에 직원 명령 분류 요청");
            }
            if (elapsed >= 25)
            {
                Debug.Log("[IntegratedStoreCheck] 손님=" + npc.ActiveCustomers + " 행인=" + npc.ActivePedestrians +
                    " 경로 실패=" + npc.RouteFailures + " 모의 계산=" + npc.SuccessfulMockCheckouts);
                Stop();
            }
        }

        private static void Stop()
        {
            SessionState.SetBool(Running, false);
            started = 0;
            dayStarted = false;
            commandSent = false;
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
