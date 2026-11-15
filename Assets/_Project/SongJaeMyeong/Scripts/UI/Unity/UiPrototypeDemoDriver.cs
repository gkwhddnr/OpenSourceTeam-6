using System.Collections;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 개인 검증 씬(SJM_UI_Work.unity)에서 외부 음성·이동 시스템 없이
    /// 1차 통합 데모 규격(음성 녹음 V 토글, 스트레스 구간별 반응 확률, 대사·말풍선 출력, 구역 이동, 120초 정산)을
    /// 전부 단독 검증하기 위한 시뮬레이션 드라이버.
    /// 공용 통합 씬(Team_Integration.unity)에 UI 프리팹을 연결할 때는 본 오브젝트를 포함하지 않거나 비활성화한다.
    /// </summary>
    public sealed class UiPrototypeDemoDriver : MonoBehaviour
    {
        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private string demoEmployeeName = "김민수";
        [SerializeField] private string demoPersonality = "성실함";
        [SerializeField, Range(0f, 100f)] private float diligence = 70f;
        [SerializeField, Range(0f, 100f)] private float laziness = 20f;
        [SerializeField] private float simulatedInferenceDelaySeconds = 0.9f;
        [SerializeField] private EmotionStatRates statRates = EmotionStatRates.CreateDemoDefault();

        [Header("Optional 3D Placeholder Staff & Zone Targets (SJM_UI_Work)")]
        [SerializeField] private Transform demoEmployeeTransform;
        [SerializeField] private Transform counterTarget;
        [SerializeField] private Transform shelfTarget;
        [SerializeField] private Transform restTarget;
        [SerializeField] private float demoWalkSpeed = 3.2f;

        private static readonly string[] SampleVoiceUtterances =
        {
            "민수야 계산대 좀 봐줘",
            "진열대에 물건 좀 채워줘",
            "계산대 손님 결제 도와줘",
            "잠깐 쉬어"
        };

        private StaffWorkRole currentRole = StaffWorkRole.Idle;
        private string currentActionCode = "IDLE";
        private StaffMoveBadge currentMoveBadge = StaffMoveBadge.Idle;
        private EmployeePersonalityProfile _activeProfile = EmployeePersonalityProfile.CreatePreset1Diligent();
        private float stress = 20f;
        private float fatigue = 15f;
        private float loyalty = 75f;
        private float fallbackArrivalTimer;
        private Transform activeMoveDestination;

        private bool isRecordingVoice;
        private int sampleUtteranceIndex;
        private Coroutine voiceSimCoroutine;

        private void OnEnable()
        {
            if (uiController == null) return;
            uiController.PersonaConfirmed += HandlePersonaConfirmed;
            uiController.DayStarted += HandleDayStarted;
            uiController.TextCommandSubmitted += HandleTextCommandSubmitted;
            uiController.CancelWorkRequested += HandleCancelWorkRequested;
            uiController.VoiceRecordToggleRequested += HandleVoiceRecordToggleRequested;
        }

        private void OnDisable()
        {
            if (uiController == null) return;
            uiController.PersonaConfirmed -= HandlePersonaConfirmed;
            uiController.DayStarted -= HandleDayStarted;
            uiController.TextCommandSubmitted -= HandleTextCommandSubmitted;
            uiController.CancelWorkRequested -= HandleCancelWorkRequested;
            uiController.VoiceRecordToggleRequested -= HandleVoiceRecordToggleRequested;

            StopActiveVoiceCoroutine();
            isRecordingVoice = false;
        }

        private void Update()
        {
            if (uiController == null || uiController.CurrentMode != UiScreenMode.InGame || uiController.IsPauseMenuOpen) return;

            SimulateContinuousWorkAndStats(Time.deltaTime);
            HandleSimulationHotkeys();
        }

        private void HandlePersonaConfirmed(EmployeePersonaSetupData persona)
        {
            demoEmployeeName = string.IsNullOrWhiteSpace(persona.EmployeeName) ? EmployeePersonaPresets.Preset1Name : persona.EmployeeName;
            demoPersonality = string.IsNullOrWhiteSpace(persona.PersonalityTraits)
                ? EmployeePersonaPresets.Preset1Traits
                : persona.PersonalityTraits;
            _activeProfile = persona.Profile;
            diligence = persona.Profile.Diligence;
            laziness = persona.Profile.Laziness;
            statRates.InitialStress = persona.Profile.InitialStress;
            statRates.InitialLoyalty = persona.Profile.Loyalty;
            stress = persona.Profile.InitialStress;
            loyalty = persona.Profile.Loyalty;
            PushSnapshot();
        }

        private void HandleDayStarted()
        {
            StopActiveVoiceCoroutine();
            isRecordingVoice = false;

            currentRole = StaffWorkRole.Idle;
            currentActionCode = "IDLE";
            currentMoveBadge = StaffMoveBadge.Idle;
            stress = _activeProfile.InitialStress;
            fatigue = statRates.InitialFatigue;
            loyalty = _activeProfile.Loyalty;
            fallbackArrivalTimer = 0f;
            activeMoveDestination = restTarget;

            if (demoEmployeeTransform != null && restTarget != null)
            {
                demoEmployeeTransform.position = restTarget.position;
            }

            PushSnapshot();
        }

        private void SimulateContinuousWorkAndStats(float dt)
        {
            if (dt <= 0f) return;

            if (currentMoveBadge == StaffMoveBadge.Moving)
            {
                if (demoEmployeeTransform != null && activeMoveDestination != null)
                {
                    Vector3 targetPos = activeMoveDestination.position;
                    targetPos.y = demoEmployeeTransform.position.y;
                    float effectiveWalkSpeed = demoWalkSpeed * _activeProfile.MoveSpeedMultiplier;
                    demoEmployeeTransform.position = Vector3.MoveTowards(
                        demoEmployeeTransform.position,
                        targetPos,
                        effectiveWalkSpeed * dt);

                    if (Vector3.Distance(demoEmployeeTransform.position, targetPos) <= 0.08f)
                    {
                        currentMoveBadge = StaffMoveBadge.Arrived;
                    }
                }
                else
                {
                    fallbackArrivalTimer -= dt;
                    if (fallbackArrivalTimer <= 0f)
                    {
                        currentMoveBadge = StaffMoveBadge.Arrived;
                    }
                }
            }

            float stressDelta = StressRules.EvaluateContinuousStressDelta(
                currentRole,
                isStoreCrowdedOrNoisy: false,
                _activeProfile,
                statRates,
                dt);
            stress = Mathf.Clamp(stress + stressDelta, 0f, 100f);

            if (currentRole == StaffWorkRole.Checkout || currentRole == StaffWorkRole.Restock)
            {
                if (currentMoveBadge == StaffMoveBadge.Arrived)
                {
                    uiController.RecordWorkSample(currentRole, stress, dt);
                }

                fatigue = Mathf.Clamp(fatigue + statRates.WorkFatiguePerSecond * dt, 0f, 100f);
            }
            else
            {
                fatigue = Mathf.Clamp(fatigue - statRates.RestFatigueRecoveryPerSecond * dt, 0f, 100f);
            }

            PushSnapshot();

            if (stress >= StoreEndingRules.StressOverflowThreshold)
            {
                uiController.CompleteDayAndShowSettlement(stress);
                uiController.ShowEndingScreen();
            }
        }

        private void HandleVoiceRecordToggleRequested()
        {
            if (!isRecordingVoice)
            {
                StopActiveVoiceCoroutine();
                isRecordingVoice = true;
                uiController.PushVoiceStage(VoiceProcessStage.Listening, string.Empty);
                voiceSimCoroutine = StartCoroutine(CoAutoFinishRecordingAfterMaxDuration(4.0f));
            }
            else
            {
                FinishSimulatedRecordingAndInfer();
            }
        }

        private IEnumerator CoAutoFinishRecordingAfterMaxDuration(float maxSeconds)
        {
            yield return new WaitForSeconds(maxSeconds);
            if (isRecordingVoice)
            {
                FinishSimulatedRecordingAndInfer();
            }
        }

        private void FinishSimulatedRecordingAndInfer()
        {
            isRecordingVoice = false;
            StopActiveVoiceCoroutine();

            string sampleUtterance = SampleVoiceUtterances[sampleUtteranceIndex % SampleVoiceUtterances.Length];
            sampleUtteranceIndex++;
            voiceSimCoroutine = StartCoroutine(CoSimulateInferenceOnly(sampleUtterance, null));
        }

        private void HandleSimulationHotkeys()
        {
            if (uiController.IsCommandOverlayOpen || uiController.IsSettingsOpen) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame)
            {
                StartSimulatedVoiceFlow($"{demoEmployeeName}야 계산대 좀 봐줘");
            }
            else if (kb.f2Key.wasPressedThisFrame)
            {
                StartSimulatedFailureFlow("어... 저기 그거 해줘", CommandFeedbackKind.RecognitionFailed);
            }
            else if (kb.f3Key.wasPressedThisFrame)
            {
                stress = Mathf.Max(stress, 85f);
                PushSnapshot();
                StartSimulatedFailureFlow("진열도 당장 다시 해!", CommandFeedbackKind.RefusedByEmotion);
            }
            else if (kb.f4Key.wasPressedThisFrame)
            {
                currentRole = StaffWorkRole.Idle;
                currentActionCode = "IDLE";
                currentMoveBadge = StaffMoveBadge.Failed;
                activeMoveDestination = null;
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
                if (stress >= StoreEndingRules.StressOverflowThreshold)
                {
                    uiController.CompleteDayAndShowSettlement(stress);
                    uiController.ShowEndingScreen();
                }
            }
            else if (kb.f6Key.wasPressedThisFrame)
            {
                uiController.CompleteDayAndShowSettlement(stress);
            }
        }

        private void HandleTextCommandSubmitted(string rawCommand)
        {
            isRecordingVoice = false;
            StopActiveVoiceCoroutine();
            voiceSimCoroutine = StartCoroutine(CoSimulateInferenceOnly(rawCommand, null));
        }

        private void HandleCancelWorkRequested()
        {
            TransitionToRest("작업 취소 · 휴식 지시", EmotionReactionRules.GetReactionDialogue(StoreCommandIntent.Rest, EmployeeReaction.AcceptNormal));
        }

        private void StartSimulatedVoiceFlow(string utterance)
        {
            isRecordingVoice = false;
            StopActiveVoiceCoroutine();
            voiceSimCoroutine = StartCoroutine(CoSimulateVoiceAndInference(utterance, null));
        }

        private void StartSimulatedFailureFlow(string utterance, CommandFeedbackKind forcedFailure)
        {
            isRecordingVoice = false;
            StopActiveVoiceCoroutine();
            voiceSimCoroutine = StartCoroutine(CoSimulateVoiceAndInference(utterance, forcedFailure));
        }

        private IEnumerator CoSimulateVoiceAndInference(string utterance, CommandFeedbackKind? forcedKind)
        {
            uiController.PushVoiceStage(VoiceProcessStage.Listening, string.Empty);
            yield return new WaitForSeconds(0.35f);
            yield return CoSimulateInferenceOnly(utterance, forcedKind);
        }

        private IEnumerator CoSimulateInferenceOnly(string utterance, CommandFeedbackKind? forcedKind)
        {
            uiController.PushVoiceStage(VoiceProcessStage.Processing, utterance);
            yield return new WaitForSeconds(Mathf.Max(0.2f, simulatedInferenceDelaySeconds));

            uiController.PushVoiceStage(VoiceProcessStage.Completed, utterance);

            if (forcedKind.HasValue)
            {
                ApplyForcedFeedback(utterance, forcedKind.Value);
                voiceSimCoroutine = null;
                yield break;
            }

            InterpretAndReactWithProbability(utterance);
            voiceSimCoroutine = null;
        }

        private void ApplyForcedFeedback(string utterance, CommandFeedbackKind kind)
        {
            if (kind == CommandFeedbackKind.RecognitionFailed)
            {
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.RecognitionFailed,
                    utterance,
                    "CLARIFY / UNSUPPORTED",
                    "허용된 편의점 업무(계산·진열·휴식)로 판별하지 못했습니다. 다시 말씀하거나 [Tab]을 누르세요."));
            }
            else if (kind == CommandFeedbackKind.RefusedByEmotion)
            {
                loyalty = Mathf.Clamp(loyalty - statRates.RefusalLoyaltyPenalty, 0f, 100f);
                currentRole = StaffWorkRole.Idle;
                currentActionCode = "REFUSE_EMOTION";
                currentMoveBadge = StaffMoveBadge.Idle;
                activeMoveDestination = null;
                PushSnapshot();

                uiController.RecordAngerRefusal();
                string refuseQuote = EmotionReactionRules.GetReactionDialogue(StoreCommandIntent.Restock, EmployeeReaction.RefuseEmotion);
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.RefusedByEmotion,
                    utterance,
                    "REFUSE_EMOTION",
                    "분노 임계 상태로 지시를 정면 거부했습니다! '잠깐 쉬어'로 스트레스를 낮추세요.",
                    refuseQuote));
            }
        }

        private void InterpretAndReactWithProbability(string utterance)
        {
            StoreCommandIntent intent = EmotionReactionRules.ParseKoreanIntent(utterance);
            UtteranceTone tone = EmotionReactionRules.ParseKoreanTone(utterance);

            if (intent == StoreCommandIntent.Clarify || intent == StoreCommandIntent.Unsupported)
            {
                float clarifyPenalty = StressRules.EvaluateCommandStressDelta(
                    intent, tone, currentRole, StaffWorkRole.Idle, _activeProfile);
                stress = Mathf.Clamp(stress + clarifyPenalty, 0f, 100f);
                PushSnapshot();

                string code = intent == StoreCommandIntent.Unsupported ? "UNSUPPORTED" : "CLARIFY";
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.RecognitionFailed,
                    utterance,
                    code,
                    "구체적인 편의점 업무(계산대·진열대·휴식)로 다시 지시해 주세요."));
                return;
            }

            if (intent == StoreCommandIntent.Rest)
            {
                float restDelta = StressRules.EvaluateCommandStressDelta(
                    intent, tone, currentRole, StaffWorkRole.Idle, _activeProfile);
                stress = Mathf.Clamp(stress + restDelta, 0f, 100f);
                string restDialogue = EmotionReactionRules.GetReactionDialogue(StoreCommandIntent.Rest, EmployeeReaction.AcceptNormal);
                TransitionToRest(utterance, restDialogue);
                return;
            }

            StaffWorkRole targetRole = intent == StoreCommandIntent.Checkout ? StaffWorkRole.Checkout : StaffWorkRole.Restock;
            float commandStressDelta = StressRules.EvaluateCommandStressDelta(
                intent, tone, currentRole, targetRole, _activeProfile);
            stress = Mathf.Clamp(stress + commandStressDelta, 0f, 100f);

            if (tone == UtteranceTone.PoliteOrPraise)
            {
                loyalty = Mathf.Clamp(loyalty + 3f, 0f, 100f);
            }
            else if (tone == UtteranceTone.HarshOrNagging)
            {
                loyalty = Mathf.Clamp(loyalty - 5f, 0f, 100f);
            }

            EmployeePersonalityProfile profile = new EmployeePersonalityProfile(
                diligence,
                laziness,
                loyalty,
                _activeProfile.Archetype,
                _activeProfile.InitialStress,
                _activeProfile.WorkStressMultiplier,
                _activeProfile.RestRecoveryMultiplier,
                _activeProfile.TaskSwitchMultiplier,
                _activeProfile.HarshToneMultiplier,
                _activeProfile.PraiseRecoveryMultiplier,
                _activeProfile.AmbiguousCommandPenalty,
                _activeProfile.WageSensitivityMultiplier,
                _activeProfile.MoveSpeedMultiplier);

            EmotionReactionWeights weights = EmotionReactionRules.ComputeWeights(intent, stress, fatigue, profile);
            float sample = Mathf.Clamp(Random.value, 0f, 0.9999f);
            EmployeeReaction reaction = EmotionReactionRules.SampleReaction(weights, sample);
            string dialogue = EmotionReactionRules.GetReactionDialogue(intent, reaction);

            if (reaction == EmployeeReaction.RefuseEmotion)
            {
                loyalty = Mathf.Clamp(loyalty - statRates.RefusalLoyaltyPenalty, 0f, 100f);
                currentRole = StaffWorkRole.Idle;
                currentActionCode = "REFUSE_EMOTION";
                currentMoveBadge = StaffMoveBadge.Idle;
                activeMoveDestination = null;
                PushSnapshot();

                uiController.RecordAngerRefusal();
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.RefusedByEmotion,
                    utterance,
                    "REFUSE_EMOTION",
                    "직원이 스트레스 과부하로 지시를 거부했습니다! 휴식을 지시해 진정시키세요.",
                    dialogue));
                return;
            }

            if (reaction == EmployeeReaction.SlackOff)
            {
                loyalty = Mathf.Clamp(loyalty - statRates.RefusalLoyaltyPenalty * 0.5f, 0f, 100f);
                currentRole = StaffWorkRole.Idle;
                currentActionCode = "SLACK_OFF";
                currentMoveBadge = restTarget != null ? StaffMoveBadge.Moving : StaffMoveBadge.Idle;
                activeMoveDestination = restTarget;
                fallbackArrivalTimer = 0.8f;
                PushSnapshot();

                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.RefusedByEmotion,
                    utterance,
                    "SLACK_OFF",
                    "직원이 지시를 흘려듣고 농땡이를 피우며 쉬러 갑니다. (매출 미발생)",
                    dialogue));
                return;
            }

            currentRole = targetRole;
            if (reaction == EmployeeReaction.Mistake)
            {
                currentActionCode = "MISTAKE";
            }
            else if (reaction == EmployeeReaction.AcceptComplain)
            {
                currentActionCode = "ACCEPT_COMPLAIN";
            }
            else
            {
                currentActionCode = "ACCEPT_NORMAL";
            }

            currentMoveBadge = StaffMoveBadge.Moving;
            activeMoveDestination = targetRole == StaffWorkRole.Checkout ? counterTarget : shelfTarget;
            fallbackArrivalTimer = 1.0f;
            PushSnapshot();

            string roleName = targetRole == StaffWorkRole.Checkout ? "계산대" : "진열대";
            string detail;
            if (reaction == EmployeeReaction.Mistake)
            {
                detail = $"[{roleName}] 업무 중 긴장·피로로 인해 실수가 발생했습니다!";
            }
            else if (reaction == EmployeeReaction.AcceptComplain)
            {
                detail = $"불만을 표출하지만 [{roleName}] 업무를 수행합니다. (감정 효율 적용)";
            }
            else
            {
                detail = $"[{roleName}] 위치로 이동해 성실히 업무를 수행합니다.";
            }

            uiController.PushCommandFeedback(new VoiceCommandFeedback(
                CommandFeedbackKind.Accepted,
                utterance,
                currentActionCode,
                detail,
                dialogue));
        }

        private void TransitionToRest(string utterance, string dialogue)
        {
            currentRole = StaffWorkRole.Idle;
            currentActionCode = "REST";
            currentMoveBadge = restTarget != null ? StaffMoveBadge.Moving : StaffMoveBadge.Idle;
            activeMoveDestination = restTarget;
            fallbackArrivalTimer = 0.8f;
            PushSnapshot();

            uiController.PushVoiceStage(VoiceProcessStage.Completed, utterance);
            uiController.PushCommandFeedback(new VoiceCommandFeedback(
                CommandFeedbackKind.Accepted,
                utterance,
                "REST",
                "직원이 휴식 구역으로 이동해 스트레스와 피로도를 회복합니다.",
                dialogue));
        }

        private void StopActiveVoiceCoroutine()
        {
            if (voiceSimCoroutine != null)
            {
                StopCoroutine(voiceSimCoroutine);
                voiceSimCoroutine = null;
            }
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
