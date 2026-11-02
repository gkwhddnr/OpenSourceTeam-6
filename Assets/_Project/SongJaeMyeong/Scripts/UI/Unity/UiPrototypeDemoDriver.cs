using System.Collections;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 개인 검증 씬(SJM_UI_Work.unity)에서 외부 음성·이동 시스템 없이 모든 UI 동작과 정산을 검증하기 위한 시뮬레이션 드라이버.
    /// 공용 통합 씬(Team_Integration.unity)에 UI 프리팹을 연결할 때는 본 오브젝트를 포함하지 않거나 비활성화한다.
    /// </summary>
    public sealed class UiPrototypeDemoDriver : MonoBehaviour
    {
        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private string demoEmployeeName = "김민수";
        [SerializeField] private string demoPersonality = "성실함";
        [SerializeField] private float simulatedInferenceDelaySeconds = 1.1f;

        private StaffWorkRole currentRole = StaffWorkRole.Idle;
        private string currentActionCode = "IDLE";
        private StaffMoveBadge currentMoveBadge = StaffMoveBadge.Idle;
        private float stress = 20f;
        private float fatigue = 10f;
        private float loyalty = 75f;
        private float moveArrivalTimer;
        private Coroutine voiceSimCoroutine;

        private void OnEnable()
        {
            if (uiController == null) return;
            uiController.DayStarted += HandleDayStarted;
            uiController.TextCommandSubmitted += HandleTextCommandSubmitted;
            uiController.CancelWorkRequested += HandleCancelWorkRequested;
        }

        private void OnDisable()
        {
            if (uiController == null) return;
            uiController.DayStarted -= HandleDayStarted;
            uiController.TextCommandSubmitted -= HandleTextCommandSubmitted;
            uiController.CancelWorkRequested -= HandleCancelWorkRequested;

            if (voiceSimCoroutine != null)
            {
                StopCoroutine(voiceSimCoroutine);
                voiceSimCoroutine = null;
            }
        }

        private void Update()
        {
            if (uiController == null || uiController.CurrentMode != UiScreenMode.InGame) return;

            SimulateContinuousWorkAndStats(Time.deltaTime);
            HandleSimulationHotkeys();
        }

        private void HandleDayStarted()
        {
            currentRole = StaffWorkRole.Idle;
            currentActionCode = "IDLE";
            currentMoveBadge = StaffMoveBadge.Idle;
            stress = 20f;
            fatigue = 10f;
            loyalty = 75f;
            moveArrivalTimer = 0f;
            PushSnapshot();
        }

        private void SimulateContinuousWorkAndStats(float dt)
        {
            if (dt <= 0f) return;

            if (currentMoveBadge == StaffMoveBadge.Moving)
            {
                moveArrivalTimer -= dt;
                if (moveArrivalTimer <= 0f)
                {
                    currentMoveBadge = StaffMoveBadge.Arrived;
                }
            }

            if (currentRole == StaffWorkRole.Checkout || currentRole == StaffWorkRole.Restock)
            {
                if (currentMoveBadge == StaffMoveBadge.Arrived)
                {
                    uiController.RecordWorkSample(currentRole, stress, dt);
                }

                stress = Mathf.Clamp(stress + 0.6f * dt, 0f, 100f);
                fatigue = Mathf.Clamp(fatigue + 0.35f * dt, 0f, 100f);
            }
            else
            {
                stress = Mathf.Clamp(stress - 1.2f * dt, 0f, 100f);
                fatigue = Mathf.Clamp(fatigue - 0.25f * dt, 0f, 100f);
            }

            PushSnapshot();
        }

        private void HandleSimulationHotkeys()
        {
            if (uiController.IsCommandOverlayOpen) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame)
            {
                StartSimulatedVoiceFlow("민수야 계산대 좀 봐줘");
            }
            else if (kb.f2Key.wasPressedThisFrame)
            {
                StartSimulatedFailureFlow("어... 저기 그거 해줘", CommandFeedbackKind.RecognitionFailed);
            }
            else if (kb.f3Key.wasPressedThisFrame)
            {
                stress = Mathf.Max(stress, 85f);
                loyalty = Mathf.Max(0f, loyalty - 8f);
                PushSnapshot();
                StartSimulatedFailureFlow("진열도 당장 다시 해!", CommandFeedbackKind.RefusedByEmotion);
            }
            else if (kb.f4Key.wasPressedThisFrame)
            {
                currentRole = StaffWorkRole.Idle;
                currentActionCode = "IDLE";
                currentMoveBadge = StaffMoveBadge.Failed;
                PushSnapshot();
                uiController.PushVoiceStage(VoiceProcessStage.Completed, "창고 뒤쪽으로 이동해");
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.SystemOrMoveError,
                    "창고 뒤쪽으로 이동해",
                    "MOVE_FAILED: NoPath",
                    "이동 경로 탐색에 실패해 작업을 취소하고 기본 대기 상태로 복구했습니다."));
            }
            else if (kb.f5Key.wasPressedThisFrame)
            {
                stress = Mathf.Clamp(stress + 25f, 0f, 100f);
                PushSnapshot();
            }
            else if (kb.f6Key.wasPressedThisFrame)
            {
                uiController.CompleteDayAndShowSettlement(stress);
            }
        }

        private void HandleTextCommandSubmitted(string rawCommand)
        {
            StartSimulatedVoiceFlow(rawCommand);
        }

        private void HandleCancelWorkRequested()
        {
            currentRole = StaffWorkRole.Idle;
            currentActionCode = "IDLE";
            currentMoveBadge = StaffMoveBadge.Cancelled;
            PushSnapshot();

            uiController.PushVoiceStage(VoiceProcessStage.Idle, string.Empty);
            uiController.PushCommandFeedback(new VoiceCommandFeedback(
                CommandFeedbackKind.Info,
                "작업 취소 요청",
                "IDLE",
                "현재 진행 중인 업무를 취소하고 직원을 기본 대기 상태로 전환했습니다."));
        }

        private void StartSimulatedVoiceFlow(string utterance)
        {
            if (voiceSimCoroutine != null) StopCoroutine(voiceSimCoroutine);
            voiceSimCoroutine = StartCoroutine(CoSimulateVoiceInference(utterance, null));
        }

        private void StartSimulatedFailureFlow(string utterance, CommandFeedbackKind forcedFailure)
        {
            if (voiceSimCoroutine != null) StopCoroutine(voiceSimCoroutine);
            voiceSimCoroutine = StartCoroutine(CoSimulateVoiceInference(utterance, forcedFailure));
        }

        private IEnumerator CoSimulateVoiceInference(string utterance, CommandFeedbackKind? forcedKind)
        {
            uiController.PushVoiceStage(VoiceProcessStage.Listening, string.Empty);
            yield return new WaitForSeconds(0.35f);

            uiController.PushVoiceStage(VoiceProcessStage.Processing, utterance);
            yield return new WaitForSeconds(Mathf.Max(0.2f, simulatedInferenceDelaySeconds));

            uiController.PushVoiceStage(VoiceProcessStage.Completed, utterance);

            if (forcedKind.HasValue)
            {
                ApplyForcedFeedback(utterance, forcedKind.Value);
                voiceSimCoroutine = null;
                yield break;
            }

            InterpretDemoCommand(utterance);
            voiceSimCoroutine = null;
        }

        private void ApplyForcedFeedback(string utterance, CommandFeedbackKind kind)
        {
            if (kind == CommandFeedbackKind.RecognitionFailed)
            {
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.RecognitionFailed,
                    utterance,
                    "UNRECOGNIZED",
                    "허용된 업무(계산·진열)로 판별하지 못했습니다. 다시 말씀하거나 [Tab] 보조 입력을 사용하세요."));
            }
            else if (kind == CommandFeedbackKind.RefusedByEmotion)
            {
                uiController.RecordAngerRefusal();
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.RefusedByEmotion,
                    utterance,
                    "REFUSE_ORDER",
                    "직원이 분노 임계 상태여서 지시를 거부했습니다! 대기 상태로 두어 스트레스를 낮추세요."));
            }
        }

        private void InterpretDemoCommand(string utterance)
        {
            if (stress >= 85f)
            {
                ApplyForcedFeedback(utterance, CommandFeedbackKind.RefusedByEmotion);
                return;
            }

            bool isCheckout = utterance.Contains("계산") || utterance.Contains("결제") || utterance.Contains("카운터");
            bool isRestock = utterance.Contains("진열") || utterance.Contains("물건") || utterance.Contains("채워") || utterance.Contains("정리");
            bool isRest = utterance.Contains("대기") || utterance.Contains("쉬어") || utterance.Contains("취소");

            if (!isCheckout && !isRestock && !isRest)
            {
                ApplyForcedFeedback(utterance, CommandFeedbackKind.RecognitionFailed);
                return;
            }

            if (isRest)
            {
                HandleCancelWorkRequested();
                return;
            }

            StaffWorkRole nextRole = isCheckout ? StaffWorkRole.Checkout : StaffWorkRole.Restock;
            if (currentRole != StaffWorkRole.Idle && currentRole != nextRole)
            {
                stress = Mathf.Clamp(stress + 10f, 0f, 100f);
            }

            currentRole = nextRole;
            currentActionCode = isCheckout ? "WORK_CHECKOUT" : "WORK_RESTOCK";
            currentMoveBadge = StaffMoveBadge.Moving;
            moveArrivalTimer = 1.0f;
            PushSnapshot();

            string roleName = isCheckout ? "계산대 결제" : "진열대 보충";
            uiController.PushCommandFeedback(new VoiceCommandFeedback(
                CommandFeedbackKind.Accepted,
                utterance,
                currentActionCode,
                $"직원이 [{roleName}] 위치로 이동해 업무를 수행합니다."));
        }

        private void PushSnapshot()
        {
            uiController.UpdateEmployeeHud(new EmployeeHudSnapshot(
                demoEmployeeName,
                demoPersonality,
                currentRole,
                currentActionCode,
                currentMoveBadge,
                stress,
                fatigue,
                loyalty));
        }
    }
}
