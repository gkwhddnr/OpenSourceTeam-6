using System;
using System.Collections;
using System.Reflection;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.UI.Unity
{
    using Camera = UnityEngine.Camera;
    using Random = UnityEngine.Random;
    using Time = UnityEngine.Time;
    /// <summary>
    /// 개인 검증 씬(SJM_UI_Work.unity)에서 외부 음성·이동 시스템 없이
    /// 1차 통합 데모 규격(음성 녹음 V 토글, 스트레스 구간별 반응 확률, 대사·말풍선 출력, 구역 이동, 120초 정산)을
    /// 전부 단독 검증하기 위한 시뮬레이션 드라이버.
    /// 공용 통합 씬(Team_Integration.unity)에 UI 프리팹을 연결할 때는 본 오브젝트를 포함하지 않거나 비활성화한다.
    /// </summary>
    public sealed class UiPrototypeDemoDriver : MonoBehaviour
    {
        [SerializeField] private StoreUiSessionController uiController;
        [Tooltip("외부 AI(VoiceMovementIntegration)가 없는 단독 UI 테스트일 때만 켭니다. 실제 AI 연동 씬에서는 꺼두어야 이중 행동을 방지합니다.")]
        [SerializeField] private bool enableStandaloneDemoSimulation = false;
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
        [SerializeField, Min(0.1f)] private float demoAngerReactionSeconds = 2.0f;
        [SerializeField] private Collider angerStrikeSurface;

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
        private NavMeshAgent demoEmployeeAgent;
        private NpcEmployeeAnimation demoEmployeeAnimation;
        private Transform playerRigTransform;
        private Vector3 lastDispatchedNavTarget;
        private bool hasDispatchedNavTarget;
        private float angerTimeRemaining;
        private float angerDuration;
        private int nextAngerGesture;
        private AngerGesture angerGesture;
        private Vector3 angerStrikeTarget;

        private bool isRecordingVoice;
        private int sampleUtteranceIndex;
        private Coroutine voiceSimCoroutine;
        private bool externalVoiceActive;
        private MonoBehaviour externalVoiceComponent;
        private SettingsPanelPresenter boundSettingsPanel;
        private GameObject shortcutGuideObject;

        private void OnEnable()
        {
            RefreshExternalVoiceIntegration();
            BindSettingsMicrophoneRelay();

            if (!enableStandaloneDemoSimulation || uiController == null || externalVoiceActive) return;
            uiController.PersonaConfirmed += HandlePersonaConfirmed;
            uiController.DayStarted += HandleDayStarted;
            uiController.TextCommandSubmitted += HandleTextCommandSubmitted;
            uiController.CancelWorkRequested += HandleCancelWorkRequested;
            uiController.VoiceRecordToggleRequested += HandleVoiceRecordToggleRequested;
        }

        private void Start()
        {
            RefreshExternalVoiceIntegration();
            BindSettingsMicrophoneRelay();

            if ((!enableStandaloneDemoSimulation || externalVoiceActive) && uiController != null)
            {
                uiController.PersonaConfirmed -= HandlePersonaConfirmed;
                uiController.DayStarted -= HandleDayStarted;
                uiController.TextCommandSubmitted -= HandleTextCommandSubmitted;
                uiController.CancelWorkRequested -= HandleCancelWorkRequested;
                uiController.VoiceRecordToggleRequested -= HandleVoiceRecordToggleRequested;
            }
        }

        private void OnDisable()
        {
            UnbindSettingsMicrophoneRelay();

            if (uiController != null)
            {
                uiController.PersonaConfirmed -= HandlePersonaConfirmed;
                uiController.DayStarted -= HandleDayStarted;
                uiController.TextCommandSubmitted -= HandleTextCommandSubmitted;
                uiController.CancelWorkRequested -= HandleCancelWorkRequested;
                uiController.VoiceRecordToggleRequested -= HandleVoiceRecordToggleRequested;
            }

            StopActiveVoiceCoroutine();
            EndDemoAngerReaction();
            if (demoEmployeeAnimation != null)
            {
                demoEmployeeAnimation.SetPaused(false);
            }
            isRecordingVoice = false;
        }

        private void RefreshExternalVoiceIntegration()
        {
            externalVoiceComponent = null;
            externalVoiceActive = false;

            Type voiceType = Type.GetType("EmployeeVoice.VoiceMovementIntegration, Assembly-CSharp");
            if (voiceType == null) return;

            UnityEngine.Object found = FindAnyObjectByType(voiceType);
            if (found is MonoBehaviour mb && mb.isActiveAndEnabled)
            {
                externalVoiceComponent = mb;
                externalVoiceActive = true;
            }
        }

        private void SyncShortcutGuideVisibility()
        {
            if (shortcutGuideObject == null)
            {
                Transform guideTr = transform.Find("DemoShortcutGuide");
                if (guideTr != null)
                {
                    shortcutGuideObject = guideTr.gameObject;
                }
            }

            if (shortcutGuideObject == null || uiController == null) return;

            bool shouldShow = uiController.CurrentMode != UiScreenMode.Settlement &&
                              !uiController.IsSettlementVisible &&
                              !uiController.IsEndingVisible;
            if (shortcutGuideObject.activeSelf != shouldShow)
            {
                shortcutGuideObject.SetActive(shouldShow);
            }
        }

        private void BindSettingsMicrophoneRelay()
        {
            if (!externalVoiceActive || uiController == null) return;
            boundSettingsPanel = uiController.GetComponentInChildren<SettingsPanelPresenter>(true);
            if (boundSettingsPanel != null)
            {
                boundSettingsPanel.MicrophoneDeviceChanged -= HandleSettingsMicrophoneChanged;
                boundSettingsPanel.MicrophoneDeviceChanged += HandleSettingsMicrophoneChanged;
            }
        }

        private void UnbindSettingsMicrophoneRelay()
        {
            if (boundSettingsPanel != null)
            {
                boundSettingsPanel.MicrophoneDeviceChanged -= HandleSettingsMicrophoneChanged;
                boundSettingsPanel = null;
            }
        }

        private void HandleSettingsMicrophoneChanged(string deviceName)
        {
            if (externalVoiceComponent == null) return;
            FieldInfo micField = externalVoiceComponent.GetType().GetField(
                "microphoneDeviceName",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (micField != null && micField.FieldType == typeof(string))
            {
                micField.SetValue(externalVoiceComponent, deviceName ?? string.Empty);
            }
        }

        private void Update()
        {
            SyncShortcutGuideVisibility();

            bool pausedOrNotInGame = uiController == null ||
                uiController.CurrentMode != UiScreenMode.InGame ||
                uiController.IsPauseMenuOpen ||
                uiController.IsSettingsOpen ||
                uiController.IsTransitionFading;

            if (pausedOrNotInGame)
            {
                if (!externalVoiceActive && demoEmployeeAnimation != null)
                {
                    demoEmployeeAnimation.SetPaused(true);
                }
                if (uiController == null || (uiController.CurrentMode != UiScreenMode.InGame && angerTimeRemaining > 0f))
                {
                    EndDemoAngerReaction();
                }
                return;
            }

            if (!enableStandaloneDemoSimulation || externalVoiceActive)
            {
                HandleExternalVoiceUtilityHotkeys();
                return;
            }

            if (demoEmployeeAnimation != null)
            {
                demoEmployeeAnimation.SetPaused(false);
            }

            SimulateContinuousWorkAndStats(Time.deltaTime);
            HandleSimulationHotkeys();
        }

        private void HandleExternalVoiceUtilityHotkeys()
        {
            if (uiController.IsCommandOverlayOpen) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f6Key.wasPressedThisFrame)
            {
                uiController.CompleteDayAndShowSettlement(uiController.CurrentEmployeeStress);
            }
            else if (kb.f7Key.wasPressedThisFrame)
            {
                float skipSeconds = uiController.TotalDaySeconds > 0f
                    ? uiController.TotalDaySeconds / 6f
                    : 20f;
                uiController.AdvanceRemainingDaySeconds(skipSeconds);
                string clock = StoreDayTimeRules.FormatGameClock(uiController.CurrentGameHour);
                string phase = StoreDayTimeRules.GetPhaseLabel(uiController.CurrentTimeOfDayPhase);
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.Info,
                    string.Empty,
                    $"TIME_SKIP ({clock} · {phase})",
                    $"게임 시간을 4시간 빠르게 진행했습니다. 현재 시각: {clock} ({phase})"));
            }
            else if (kb.f8Key.wasPressedThisFrame)
            {
                bool nextRain = !uiController.IsRainWeather;
                uiController.SetRainWeather(nextRain);
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.Info,
                    string.Empty,
                    nextRain ? "WEATHER: RAIN_ON" : "WEATHER: RAIN_OFF",
                    nextRain
                        ? "비 날씨 이벤트를 켰습니다. 구름이 먼저 차오른 뒤 빗줄기가 내립니다. (2·3일차 날씨 프리뷰)"
                        : "비 날씨 이벤트를 껐습니다. 빗줄기가 먼저 그친 뒤 구름이 걷힙니다."));
            }
            else if (kb.f9Key.wasPressedThisFrame)
            {
                TriggerExternalVoiceAngerPreview();
            }
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
            EndDemoAngerReaction();
            if (demoEmployeeAnimation != null)
            {
                demoEmployeeAnimation.SetPaused(false);
            }
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
                if (demoEmployeeAgent == null)
                {
                    demoEmployeeAgent = demoEmployeeTransform.GetComponent<NavMeshAgent>();
                }

                hasDispatchedNavTarget = false;
                if (demoEmployeeAgent != null && demoEmployeeAgent.isActiveAndEnabled && demoEmployeeAgent.isOnNavMesh)
                {
                    demoEmployeeAgent.ResetPath();
                    demoEmployeeAgent.Warp(restTarget.position);
                }
                else
                {
                    demoEmployeeTransform.position = restTarget.position;
                }
            }

            PushSnapshot();
        }

        private void SimulateContinuousWorkAndStats(float dt)
        {
            if (dt <= 0f) return;

            bool movedNonNavMesh = false;
            if (angerTimeRemaining > 0f)
            {
                if (demoEmployeeAgent != null && demoEmployeeAgent.isActiveAndEnabled && demoEmployeeAgent.isOnNavMesh)
                {
                    demoEmployeeAgent.isStopped = true;
                }
            }
            else if (currentMoveBadge == StaffMoveBadge.Moving)
            {
                if (demoEmployeeTransform != null && activeMoveDestination != null)
                {
                    if (demoEmployeeAgent == null)
                    {
                        demoEmployeeAgent = demoEmployeeTransform.GetComponent<NavMeshAgent>();
                    }

                    Vector3 targetPos = activeMoveDestination.position;
                    float effectiveWalkSpeed = demoWalkSpeed * _activeProfile.MoveSpeedMultiplier;

                    if (demoEmployeeAgent != null && demoEmployeeAgent.isActiveAndEnabled && demoEmployeeAgent.isOnNavMesh)
                    {
                        demoEmployeeAgent.isStopped = false;
                        demoEmployeeAgent.speed = effectiveWalkSpeed;
                        if (!hasDispatchedNavTarget || (targetPos - lastDispatchedNavTarget).sqrMagnitude > 0.01f)
                        {
                            demoEmployeeAgent.SetDestination(targetPos);
                            lastDispatchedNavTarget = targetPos;
                            hasDispatchedNavTarget = true;
                        }

                        if (!demoEmployeeAgent.pathPending &&
                            demoEmployeeAgent.remainingDistance <= Mathf.Max(0.2f, demoEmployeeAgent.stoppingDistance + 0.08f))
                        {
                            currentMoveBadge = StaffMoveBadge.Arrived;
                            hasDispatchedNavTarget = false;
                        }
                    }
                    else
                    {
                        targetPos.y = demoEmployeeTransform.position.y;
                        Vector3 nextPos = Vector3.MoveTowards(
                            demoEmployeeTransform.position,
                            targetPos,
                            effectiveWalkSpeed * dt);
                        Vector3 moveDelta = nextPos - demoEmployeeTransform.position;
                        moveDelta.y = 0f;
                        if (moveDelta.sqrMagnitude > 0.00001f)
                        {
                            movedNonNavMesh = true;
                            demoEmployeeTransform.rotation = Quaternion.RotateTowards(
                                demoEmployeeTransform.rotation,
                                Quaternion.LookRotation(moveDelta.normalized, Vector3.up),
                                480f * dt);
                        }
                        demoEmployeeTransform.position = nextPos;

                        if (Vector3.Distance(demoEmployeeTransform.position, targetPos) <= 0.08f)
                        {
                            currentMoveBadge = StaffMoveBadge.Arrived;
                        }
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
            else if (hasDispatchedNavTarget)
            {
                hasDispatchedNavTarget = false;
                if (demoEmployeeAgent != null && demoEmployeeAgent.isActiveAndEnabled && demoEmployeeAgent.isOnNavMesh)
                {
                    demoEmployeeAgent.ResetPath();
                }
            }

            float prevStress = stress;
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

                fatigue = Mathf.Clamp(fatigue + statRates.WorkFatiguePerSecond * _activeProfile.WorkFatigueMultiplier * dt, 0f, 100f);
            }
            else
            {
                fatigue = Mathf.Clamp(fatigue - statRates.RestFatigueRecoveryPerSecond * _activeProfile.RestRecoveryMultiplier * dt, 0f, 100f);
            }

            PushSnapshot();

            if (stress >= StoreEndingRules.StressOverflowThreshold)
            {
                EndDemoAngerReaction();
                uiController.CompleteDayAndShowSettlement(stress);
                uiController.ShowEndingScreen();
                return;
            }

            if (prevStress < StressRules.AngerThreshold && stress >= StressRules.AngerThreshold)
            {
                StartDemoAngerReaction();
            }

            UpdateStandaloneEmployeeAnimation(dt, movedNonNavMesh);
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
            if (uiController.IsCommandOverlayOpen || uiController.IsSettingsOpen || uiController.IsExternalModalOpen) return;

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
                float prevStress = stress;
                stress = Mathf.Clamp(stress + 25f, 0f, 100f);
                PushSnapshot();
                if (stress >= StoreEndingRules.StressOverflowThreshold)
                {
                    EndDemoAngerReaction();
                    uiController.CompleteDayAndShowSettlement(stress);
                    uiController.ShowEndingScreen();
                }
                else if (prevStress < StressRules.AngerThreshold && stress >= StressRules.AngerThreshold)
                {
                    StartDemoAngerReaction();
                }
            }
            else if (kb.f6Key.wasPressedThisFrame)
            {
                uiController.CompleteDayAndShowSettlement(stress);
            }
            else if (kb.f7Key.wasPressedThisFrame)
            {
                float skipSeconds = uiController.TotalDaySeconds > 0f
                    ? uiController.TotalDaySeconds / 6f
                    : 20f;
                uiController.AdvanceRemainingDaySeconds(skipSeconds);
                string clock = StoreDayTimeRules.FormatGameClock(uiController.CurrentGameHour);
                string phase = StoreDayTimeRules.GetPhaseLabel(uiController.CurrentTimeOfDayPhase);
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.Info,
                    string.Empty,
                    $"TIME_SKIP ({clock} · {phase})",
                    $"게임 시간을 4시간 빠르게 진행했습니다. 현재 시각: {clock} ({phase})"));
            }
            else if (kb.f8Key.wasPressedThisFrame)
            {
                bool nextRain = !uiController.IsRainWeather;
                uiController.SetRainWeather(nextRain);
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.Info,
                    string.Empty,
                    nextRain ? "WEATHER: RAIN_ON" : "WEATHER: RAIN_OFF",
                    nextRain
                        ? "비 날씨 이벤트를 켰습니다. 구름이 먼저 차오른 뒤 빗줄기가 내립니다. (2·3일차 날씨 프리뷰)"
                        : "비 날씨 이벤트를 껐습니다. 빗줄기가 먼저 그친 뒤 구름이 걷힙니다."));
            }
            else if (kb.f9Key.wasPressedThisFrame)
            {
                TriggerStandaloneAngerPreview();
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
                StartDemoAngerReaction();

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
            float prevStress = stress;
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
                StartDemoAngerReaction();

                uiController.RecordAngerRefusal();
                uiController.PushCommandFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.RefusedByEmotion,
                    utterance,
                    "REFUSE_EMOTION",
                    "직원이 스트레스 과부하로 지시를 거부했습니다! 휴식을 지시해 진정시키세요.",
                    dialogue));
                return;
            }

            if (prevStress < StressRules.AngerThreshold && stress >= StressRules.AngerThreshold)
            {
                StartDemoAngerReaction();
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

        private void EnsureDemoEmployeeAnimation()
        {
            if (demoEmployeeTransform == null)
            {
                GameObject npcObj = GameObject.Find("Demo_Employee_NPC");
                if (npcObj != null)
                {
                    demoEmployeeTransform = npcObj.transform;
                }
            }

            if (demoEmployeeAnimation == null && demoEmployeeTransform != null)
            {
                demoEmployeeAnimation = demoEmployeeTransform.GetComponentInChildren<NpcEmployeeAnimation>(true);
            }

            if (demoEmployeeAgent == null && demoEmployeeTransform != null)
            {
                demoEmployeeAgent = demoEmployeeTransform.GetComponent<NavMeshAgent>();
            }
        }

        private void UpdateStandaloneEmployeeAnimation(float dt, bool movedNonNavMesh)
        {
            EnsureDemoEmployeeAnimation();
            if (demoEmployeeAnimation == null || !demoEmployeeAnimation.isActiveAndEnabled)
            {
                return;
            }

            if (angerTimeRemaining > 0f)
            {
                angerTimeRemaining -= dt;
                if (angerTimeRemaining > 0f)
                {
                    demoEmployeeAnimation.SetAngerGesture(
                        angerGesture,
                        1f - angerTimeRemaining / Mathf.Max(0.1f, angerDuration),
                        GetDemoAngerReactionTarget());
                    return;
                }

                EndDemoAngerReaction();
            }

            bool isNavMoving = demoEmployeeAgent != null &&
                               demoEmployeeAgent.isActiveAndEnabled &&
                               demoEmployeeAgent.isOnNavMesh &&
                               demoEmployeeAgent.velocity.sqrMagnitude > 0.01f;
            if (isNavMoving || movedNonNavMesh)
            {
                if (demoEmployeeAnimation.CurrentState != NpcAnimationState.Walk)
                {
                    demoEmployeeAnimation.Play(NpcAnimationState.Walk);
                }
                return;
            }

            if (demoEmployeeAnimation.CurrentState == NpcAnimationState.Checkout)
            {
                return;
            }

            bool shouldRest = currentRole == StaffWorkRole.Idle &&
                              (currentActionCode == "REST" || currentActionCode == "SLACK_OFF") &&
                              currentMoveBadge != StaffMoveBadge.Moving;
            NpcAnimationState targetState = shouldRest ? NpcAnimationState.Rest : NpcAnimationState.Idle;
            if (demoEmployeeAnimation.CurrentState != targetState)
            {
                demoEmployeeAnimation.Play(targetState);
            }
        }

        private void TriggerExternalVoiceAngerPreview()
        {
            if (externalVoiceComponent == null || uiController == null) return;

            MethodInfo previewMethod = externalVoiceComponent.GetType().GetMethod(
                "TryTriggerNextAngerReactionPreview",
                BindingFlags.Instance | BindingFlags.Public);
            if (previewMethod == null) return;

            object[] args = new object[2];
            object rawResult = previewMethod.Invoke(externalVoiceComponent, args);
            if (!(rawResult is bool started) || !started) return;

            AngerGesture played = args[0] is AngerGesture g ? g : AngerGesture.Shout;
            bool fellBack = args[1] is bool b && b;
            PushAngerPreviewFeedback(played, fellBack);
        }

        private void TriggerStandaloneAngerPreview()
        {
            if (TryTriggerStandaloneAngerReaction(out AngerGesture played, out bool fellBack))
            {
                PushAngerPreviewFeedback(played, fellBack);
            }
        }

        private void PushAngerPreviewFeedback(AngerGesture played, bool fellBackFromCounterStrike)
        {
            if (uiController == null) return;

            string label = played switch
            {
                AngerGesture.Shout => "[분노 모션 1/4] 고함 (Shout · 머리 위 '!')",
                AngerGesture.Finger => "[분노 모션 2/4] 손가락질 (Finger)",
                AngerGesture.Frown => "[분노 모션 3/4] 찡그림 (Frown · 머리 위 '...')",
                AngerGesture.CounterStrike => "[분노 모션 4/4] 계산대 내려치기 (CounterStrike)",
                _ => played.ToString()
            };

            string msg = fellBackFromCounterStrike
                ? $"{label} — 계산대에서 멀어 고함으로 대체했습니다. 계산대 옆에서 F9를 누르면 내려치기가 재생됩니다."
                : $"{label} 모션을 재생합니다. (F9로 4종 순환 확인)";

            uiController.PushCommandFeedback(new VoiceCommandFeedback(
                CommandFeedbackKind.Info,
                string.Empty,
                $"ANGER_PREVIEW: {played}",
                msg));
        }

        private void StartDemoAngerReaction()
        {
            TryTriggerStandaloneAngerReaction(out _, out _);
        }

        private bool TryTriggerStandaloneAngerReaction(out AngerGesture playedGesture, out bool fellBackFromCounterStrike)
        {
            playedGesture = default;
            fellBackFromCounterStrike = false;

            EnsureDemoEmployeeAnimation();
            if (demoEmployeeTransform == null || demoEmployeeAnimation == null ||
                !demoEmployeeAnimation.CanPlay(NpcAnimationState.Anger))
            {
                return false;
            }

            if (angerTimeRemaining > 0f)
            {
                EndDemoAngerReaction();
            }

            if (demoEmployeeAgent != null && demoEmployeeAgent.isActiveAndEnabled && demoEmployeeAgent.isOnNavMesh)
            {
                demoEmployeeAgent.isStopped = true;
            }

            AngerGesture selected = (AngerGesture)(nextAngerGesture++ % 4);
            if (selected == AngerGesture.CounterStrike && !TryGetDemoCounterStrikeTarget())
            {
                fellBackFromCounterStrike = true;
                selected = AngerGesture.Shout;
            }

            angerGesture = selected;
            playedGesture = selected;
            if (angerGesture != AngerGesture.CounterStrike)
            {
                FacePlayerHorizontallyForDemo();
            }

            demoEmployeeAnimation.Play(angerGesture == AngerGesture.Shout ? NpcAnimationState.Anger : NpcAnimationState.Idle);
            demoEmployeeAnimation.SetAngerGesture(angerGesture, 0f, GetDemoAngerReactionTarget());
            angerDuration = Mathf.Max(0.1f, demoAngerReactionSeconds);
            angerTimeRemaining = angerDuration;
            return true;
        }

        private void EndDemoAngerReaction()
        {
            angerTimeRemaining = 0f;
            if (demoEmployeeAgent != null && demoEmployeeAgent.isActiveAndEnabled && demoEmployeeAgent.isOnNavMesh)
            {
                demoEmployeeAgent.isStopped = false;
            }
            if (demoEmployeeAnimation != null)
            {
                demoEmployeeAnimation.SetAngerGesture(AngerGesture.Shout, 0f, Vector3.zero);
                demoEmployeeAnimation.Play(NpcAnimationState.Idle);
            }
        }

        private void FacePlayerHorizontallyForDemo()
        {
            if (demoEmployeeTransform == null) return;
            Transform playerTarget = ResolvePlayerTransform();
            Vector3 lookPos = playerTarget != null
                ? playerTarget.position
                : demoEmployeeTransform.position + demoEmployeeTransform.forward;
            Vector3 toward = lookPos - demoEmployeeTransform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.001f)
            {
                demoEmployeeTransform.rotation = Quaternion.LookRotation(toward.normalized, Vector3.up);
            }
        }

        private Transform ResolvePlayerTransform()
        {
            if (playerRigTransform != null) return playerRigTransform;

            GameObject rigObj = GameObject.Find("PlayerRig");
            if (rigObj != null)
            {
                playerRigTransform = rigObj.transform;
                return playerRigTransform;
            }

            Camera mainCam = Camera.main;
            return mainCam != null ? mainCam.transform : null;
        }

        private Vector3 GetDemoAngerReactionTarget()
        {
            if (angerGesture == AngerGesture.Finger)
            {
                Transform playerTarget = ResolvePlayerTransform();
                return playerTarget != null
                    ? playerTarget.position
                    : (demoEmployeeTransform != null ? demoEmployeeTransform.position + demoEmployeeTransform.forward : Vector3.forward);
            }

            return angerStrikeTarget;
        }

        private Collider ResolveCounterStrikeSurfaceFallback()
        {
            GameObject mapRoot = GameObject.Find("ConvenienceStore_Map");
            Transform checkout = mapRoot != null ? mapRoot.transform.Find("Checkout_1") : null;
            if (checkout == null)
            {
                checkout = GameObject.Find("Checkout_1")?.transform;
            }
            if (checkout == null || demoEmployeeTransform == null) return null;

            foreach (string childName in new[] { "Counter_Top", "Counter_Base" })
            {
                Transform child = checkout.Find(childName);
                Collider col = child != null ? child.GetComponent<Collider>() : null;
                if (col != null && col.enabled && col.gameObject.activeInHierarchy)
                {
                    return col;
                }
            }

            foreach (Collider col in checkout.GetComponentsInChildren<Collider>(false))
            {
                if (col == null || !col.enabled || col.isTrigger) continue;
                float topY = col.bounds.max.y - demoEmployeeTransform.position.y;
                if (topY >= 0.65f && topY <= 1.30f)
                {
                    return col;
                }
            }

            return null;
        }

        private bool TryGetDemoCounterStrikeTarget()
        {
            if (demoEmployeeTransform == null) return false;
            if (angerStrikeSurface == null)
            {
                angerStrikeSurface = ResolveCounterStrikeSurfaceFallback();
            }
            if (angerStrikeSurface == null || !angerStrikeSurface.enabled || !angerStrikeSurface.gameObject.activeInHierarchy)
            {
                return false;
            }

            Bounds bounds = angerStrikeSurface.bounds;
            Vector3 position = demoEmployeeTransform.position;
            Vector3 edge = bounds.ClosestPoint(new Vector3(position.x, bounds.max.y, position.z));
            Vector3 toward = edge - new Vector3(position.x, edge.y, position.z);
            if (toward.sqrMagnitude < 0.001f || toward.sqrMagnitude > 0.90f ||
                edge.y - position.y < 0.65f || edge.y - position.y > 1.25f)
            {
                return false;
            }

            demoEmployeeTransform.rotation = Quaternion.LookRotation(toward);
            angerStrikeTarget = edge + toward.normalized * 0.08f;
            return true;
        }
    }
}
