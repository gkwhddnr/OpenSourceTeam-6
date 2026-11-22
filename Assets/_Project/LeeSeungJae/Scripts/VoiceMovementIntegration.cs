using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceTeam6.Movement.Core;
using OpenSourceTeam6.Movement.Unity;
using OpenSourceTeam6.Time.Unity;
using OpenSourceTeam6.UI.Core;
using OpenSourceTeam6.UI.Unity;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Whisper;
using Whisper.Utils;

namespace EmployeeVoice
{
    // Bridges the existing public movement and UI contracts without changing their owners' assets.
    public sealed class VoiceMovementIntegration : MonoBehaviour
    {
        [SerializeField] private GameObject uiPrefab = null;
        [Tooltip("Use the scene's existing team UI instead of creating a second UI instance.")]
        [SerializeField] private StoreUiSessionController existingUi = null;
        [SerializeField] private BusinessTimeController businessTime = null;
        [SerializeField] private EmployeeMovementController employee = null;
        [SerializeField] private Transform employeeStatusAnchor = null;
        [SerializeField] private Transform restockDestination = null;
        [SerializeField] private Transform restockSnackDestination = null;
        [SerializeField] private Transform restockRamenDestination = null;
        [SerializeField] private Transform restockDailyGoodsDestination = null;
        [SerializeField] private Transform restockIceCreamDestination = null;
        [SerializeField] private Transform restockWalkInDestination = null;
        [SerializeField] private Transform restockHotFoodDestination = null;
        [SerializeField] private Transform cleanDestination = null;
        [SerializeField] private Transform paymentDestination = null;
        [SerializeField] private Transform cashierDestination = null;
        [SerializeField] private string whisperModelPath = "Assets/_Project/LeeSeungJae/Assets/ggml-small-q5_1.bin";
        [Tooltip("Optional Vulkan GPU inference trial. Unsupported devices may fall back to CPU.")]
        [SerializeField] private bool enableWhisperGpu = false;
        [SerializeField] private WhisperManager whisperGpuPrefab = null;
        [Tooltip("Optional exact microphone device name. Empty uses the system default.")]
        [SerializeField] private string microphoneDeviceName = "";
        [Tooltip("Boost quiet microphone recordings before transcription.")]
        [SerializeField] private bool boostQuietInput = true;
        [Tooltip("Enable temporary in-memory transcription comparison (F10/F11).")]
        [SerializeField] private bool enableVoiceDiagnostics = false;
        [Tooltip("Use store vocabulary as Whisper context. Compare with F11.")]
        [SerializeField] private bool useWhisperVocabulary = true;
        [Tooltip("Vocabulary supplied as context, not a command or guaranteed transcription.")]
        [SerializeField, TextArea] private string whisperVocabulary = "진열, 재고, 과자, 스낵, 라면, 생필품, 아이스크림, 냉장고, 워크인, 핫바, 청소, 계산대, 캐셔";
        [SerializeField] private EmployeePersonality personality = new EmployeePersonality { employeeName = "민수" };

        private StoreUiSessionController ui;
        private IAgentMovement movement;
        private MicrophoneRecord microphone;
        private WhisperManager whisper;
        private IEmployeeAiService aiService;
        private CancellationTokenSource decisionCancellation;
        private CancellationTokenSource dialogueCancellation;
        private readonly System.Random random = new System.Random();
        private bool processing;
        private bool recording;
        private bool subscribed;
        private bool transcriptionInFlight;
        private bool inferenceInFlight;
        private long activeMove;
        private int generation;
        private string transcript = "";
        private float recordingStartedAt;
        private const float MaxRecordingSeconds = 55f;
        private StaffMoveBadge badge = StaffMoveBadge.Idle;
        private string currentAction = "IDLE";
        private float[] diagnosticAudio;
        private int diagnosticFrequency;
        private int diagnosticChannels;
        private PlayerRigController playerRig;
        private Vector3? selectedDestination;
        private Vector3? recordedDestination;
        private bool cursorWasLocked;
        private bool activeMoveIsRestock;
        private StaffWorkRole activeWorkRole = StaffWorkRole.Idle;
        private EmployeeAction activeMoveAction = EmployeeAction.ACCEPT_NORMAL;
        private string activeRestockTarget;
        private string activeWork = null;
        private long lastArrivedMove;
        private bool recordingHoldR;
        private EmployeePersonalityProfile currentProfile = EmployeePersonalityProfile.CreatePreset1Diligent();
        private float stressFloat = 20f;
        private float fatigueFloat = 10f;

        // The hiring UI can provide a snapshot before starting a day; no UI ownership is assumed here.
        public bool TryApplyEmployeePersonality(EmployeePersonality selected, out string reason)
        {
            if (selected == null || string.IsNullOrWhiteSpace(selected.employeeName) ||
                !Regex.IsMatch(selected.employeeName.Trim(), @"^[가-힣]{2,4}$"))
            { reason = "직원 이름은 한글 2~4자로 입력해 주세요."; return false; }
            if (selected.traits != null && selected.traits.Length > 50)
            { reason = "직원 성격은 50자 이내로 입력해 주세요."; return false; }
            if (recording || processing || transcriptionInFlight || inferenceInFlight || activeMove != 0)
            { reason = "진행 중인 명령이나 이동이 끝난 뒤 직원을 변경해 주세요."; return false; }

            // Copy rather than sharing mutable UI-owned data with asynchronous inference.
            personality = new EmployeePersonality
            {
                employeeName = selected.employeeName.Trim(), traits = selected.traits ?? "", archetype = selected.archetype,
                diligence = Mathf.Clamp(selected.diligence, 0, 100),
                literalness = Mathf.Clamp(selected.literalness, 0, 100),
                laziness = Mathf.Clamp(selected.laziness, 0, 100),
                stress = Mathf.Clamp(selected.stress, 0, 100),
                fatigue = Mathf.Clamp(selected.fatigue, 0, 100),
                loyalty = Mathf.Clamp(selected.loyalty, 0, 100)
            };
            stressFloat = personality.stress;
            fatigueFloat = personality.fatigue;
            currentProfile = EmployeePersonaPresets.InferProfileFromTraits(personality.traits);
            reason = "";
            UpdateHud();
            return true;
        }

        private void OnPersonaConfirmed(EmployeePersonaSetupData setupData)
        {
            currentProfile = setupData.Profile;
            EmotionStatRates demoRates = EmotionStatRates.CreateDemoDefault();
            stressFloat = currentProfile.InitialStress;
            fatigueFloat = demoRates.InitialFatigue;

            string sanitizedName = string.IsNullOrWhiteSpace(setupData.EmployeeName)
                ? EmployeePersonaPresets.Preset1Name
                : setupData.EmployeeName.Trim();

            personality = new EmployeePersonality
            {
                employeeName = sanitizedName,
                traits = setupData.PersonalityTraits ?? "",
                archetype = MapToVoiceArchetype(currentProfile.Archetype),
                diligence = Mathf.Clamp(Mathf.RoundToInt(currentProfile.Diligence), 0, 100),
                literalness = currentProfile.Archetype == PersonaArchetype.LiteralRigid ? 85 : 20,
                laziness = Mathf.Clamp(Mathf.RoundToInt(currentProfile.Laziness), 0, 100),
                stress = Mathf.Clamp(Mathf.RoundToInt(stressFloat), 0, 100),
                fatigue = Mathf.Clamp(Mathf.RoundToInt(fatigueFloat), 0, 100),
                loyalty = Mathf.Clamp(Mathf.RoundToInt(currentProfile.Loyalty), 0, 100)
            };

            UpdateHud();
        }

        private static EmployeeArchetype MapToVoiceArchetype(PersonaArchetype archetype)
        {
            switch (archetype)
            {
                case PersonaArchetype.LazyComplainer: return EmployeeArchetype.LazyComplainer;
                case PersonaArchetype.FragileTimid: return EmployeeArchetype.FragileTimid;
                case PersonaArchetype.LiteralRigid: return EmployeeArchetype.LiteralRigid;
                case PersonaArchetype.PassionateHasty: return EmployeeArchetype.PassionateHasty;
                default: return EmployeeArchetype.DiligentStandard;
            }
        }

        private void Start()
        {
            if ((existingUi == null && uiPrefab == null) || employee == null || restockDestination == null)
            {
                Debug.LogError("[VoiceIntegration] UI, employee or test destination is not assigned.", this);
                enabled = false;
                return;
            }
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("Voice Integration EventSystem");
                eventSystem.transform.SetParent(transform, false);
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }
            if (existingUi != null)
                ui = existingUi;
            else
            {
                GameObject uiInstance = Instantiate(uiPrefab);
                uiInstance.transform.SetParent(transform, false);
                uiInstance.transform.localScale = Vector3.one;
                ui = uiInstance.GetComponent<StoreUiSessionController>();
            }
            if (ui == null)
            {
                Debug.LogError("[VoiceIntegration] The UI prefab has no StoreUiSessionController.", this);
                enabled = false;
                return;
            }
            // A scene-owned team UI may already own a clock bridge on another object.
            if (existingUi == null && businessTime != null && businessTime.isActiveAndEnabled)
            {
                var timeBridge = ui.gameObject.GetComponent<UiBusinessTimeBridge>();
                if (timeBridge == null)
                {
                    timeBridge = ui.gameObject.AddComponent<UiBusinessTimeBridge>();
                    if (!timeBridge.Configure(ui, businessTime))
                        Debug.LogError("[VoiceIntegration] 영업 UI와 시간 시스템을 연결하지 못했습니다.", this);
                }
                else Debug.LogWarning("[VoiceIntegration] UI에 기존 시간 연결이 있습니다. 중복 연결을 추가하지 않습니다.", this);
            }
            var player = FindAnyObjectByType<PlayerRigController>();
            playerRig = player;
            if (player != null && player.InputGate != null)
            {
                var adapter = gameObject.AddComponent<VoiceCursorGate>();
                adapter.Initialize(player.InputGate);
                ui.SetExternalCursorLockGate(adapter);
            }
            else Debug.LogWarning("[VoiceIntegration] Player input gate unavailable; UI fallback remains active.", this);
            movement = employee;
            Camera viewCam = playerRig != null ? playerRig.GetComponentInChildren<Camera>() : Camera.main;
            ui.BindEmployeeTarget(employee.transform, viewCam != null ? viewCam.transform : null);
            if (employeeStatusAnchor != null)
                ui.BindEmployeeTarget(employeeStatusAnchor, viewCam != null ? viewCam.transform : null);
            stressFloat = personality.stress;
            fatigueFloat = personality.fatigue;
            currentProfile = EmployeePersonaPresets.InferProfileFromTraits(personality.traits);
            Subscribe();
            SetupWhisper();
            aiService = new OpenAiEditorService();
        }

        private void OnEnable()
        {
            if (ui == null || movement == null) return;
            Subscribe();
            if (microphone != null) microphone.OnRecordStop += OnRecordingStopped;
        }

        private void Subscribe()
        {
            if (subscribed) return;
            ui.PersonaConfirmed += OnPersonaConfirmed;
            ui.TextCommandSubmitted += SubmitText;
            ui.CancelWorkRequested += CancelWork;
            ui.VoiceRecordToggleRequested += OnVoiceRecordToggleRequested;
            ui.DayStarted += OnDayStarted;
            movement.Finished += OnMovementFinished;
            subscribed = true;
        }

        private void OnDayStarted()
        {
            CancelWork();
            EmotionStatRates demoRates = EmotionStatRates.CreateDemoDefault();
            stressFloat = currentProfile.InitialStress;
            fatigueFloat = demoRates.InitialFatigue;
            personality.stress = Mathf.Clamp(Mathf.RoundToInt(stressFloat), 0, 100);
            personality.fatigue = Mathf.Clamp(Mathf.RoundToInt(fatigueFloat), 0, 100);
            UpdateHud();
            Feedback(CommandFeedbackKind.Info, "R 키를 누른 채 말씀하고 떼거나 V 키로 녹음합니다. 텍스트 명령은 Tab으로 입력합니다.");
        }

        private void OnVoiceRecordToggleRequested()
        {
            if (ui == null || ui.CurrentMode != UiScreenMode.InGame || ui.IsCommandOverlayOpen) return;
            if (recording)
            {
                recordingHoldR = false;
                if (microphone != null) microphone.StopRecord();
            }
            else
            {
                StartRecording(holdR: false);
            }
        }

        private string ModelPath(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured)) return "";
            return !Application.isEditor && !Path.IsPathRooted(configured)
                ? Path.Combine(Application.persistentDataPath, "EmployeeVoiceModels", Path.GetFileName(configured))
                : Path.GetFullPath(configured);
        }

        private void SetupWhisper()
        {
            try
            {
                string path = ModelPath(whisperModelPath);
                if (!File.Exists(path)) { Debug.LogWarning("[VoiceIntegration] Whisper model missing: " + path); return; }
                var root = new GameObject("Whisper runtime");
                root.transform.SetParent(transform);
                root.SetActive(false);
                if (enableWhisperGpu && whisperGpuPrefab != null)
                {
                    whisper = Instantiate(whisperGpuPrefab, root.transform);
                    Debug.Log("[VoiceIntegration] Whisper GPU 가속을 요청합니다. 실제 사용 장치는 Whisper 초기화 로그를 확인하세요.");
                }
                else
                {
                    if (enableWhisperGpu) Debug.LogWarning("[VoiceIntegration] GPU 설정 프리팹이 없어 CPU 설정을 사용합니다.");
                    whisper = root.AddComponent<WhisperManager>();
                    Debug.Log("[VoiceIntegration] Whisper CPU 설정으로 시작합니다.");
                }
                whisper.IsModelPathInStreamingAssets = false;
                whisper.ModelPath = path;
                whisper.language = "ko";
                whisper.translateToEnglish = false;
                microphone = root.AddComponent<MicrophoneRecord>();
                // Stop before the 60-second buffer can wrap, even if the user forgets to stop.
                microphone.maxLengthSec = 60;
                microphone.loop = true;
                microphone.echo = false;
                microphone.useVad = false;
                microphone.OnRecordStop += OnRecordingStopped;
                root.SetActive(true);
            }
            catch (Exception ex) { Debug.LogError("[VoiceIntegration] Whisper setup: " + ex.Message); }
        }

        private void Update()
        {
            if (ui == null) return;
            if (ui.CurrentMode != UiScreenMode.InGame)
            {
                if (processing || recording || activeMove != 0) CancelWork();
                return;
            }
            if (recording && !Application.isFocused)
            {
                recording = false;
                recordingHoldR = false;
                microphone.StopRecord();
                ui.PushVoiceStage(VoiceProcessStage.Idle, "");
                Feedback(CommandFeedbackKind.Info, "포커스가 해제되어 녹음이 취소됐습니다.");
                return;
            }
            if (recording && Time.realtimeSinceStartup - recordingStartedAt >= MaxRecordingSeconds)
            {
                Debug.Log("[VoiceIntegration] Maximum recording duration reached; stopping.");
                recordingHoldR = false;
                microphone.StopRecord();
                return;
            }
            if (recording && recordingHoldR && (Keyboard.current == null || !Keyboard.current.rKey.isPressed))
            {
                recordingHoldR = false;
                microphone.StopRecord();
                return;
            }
            if (!ui.IsPauseMenuOpen && !ui.IsSettingsOpen && Time.deltaTime > 0f)
            {
                TickContinuousEmotionAndRevenue(Time.deltaTime);
                if (ui.CurrentMode != UiScreenMode.InGame) return;
            }
            if (activeMove != 0)
            {
                MovementSnapshot snapshot = movement.Snapshot;
                StaffMoveBadge next = snapshot.State == MoveState.Moving || snapshot.State == MoveState.Planning
                    ? StaffMoveBadge.Moving : badge;
                if (next != badge) { badge = next; UpdateHud(); }
            }
            if (Application.isFocused && cursorWasLocked && Cursor.lockState == CursorLockMode.Locked &&
                !ui.IsCommandOverlayOpen && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SelectDestination();
            if (enableVoiceDiagnostics && !ui.IsCommandOverlayOpen && Keyboard.current != null)
            {
                if (Keyboard.current.f10Key.wasPressedThisFrame) _ = CompareDiagnosticTranscriptions();
                if (Keyboard.current.f11Key.wasPressedThisFrame) _ = CompareVocabularyTranscriptions();
            }
            if (!ui.IsCommandOverlayOpen && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                StartRecording(holdR: true);
        }

        private void TickContinuousEmotionAndRevenue(float deltaTime)
        {
            bool isResting = activeWorkRole == StaffWorkRole.Idle && activeMove == 0;
            EmotionStatRates rates = EmotionStatRates.CreateDemoDefault();
            float stressDelta = StressRules.EvaluateContinuousStressDelta(
                activeWorkRole,
                isStoreCrowdedOrNoisy: false,
                currentProfile,
                rates,
                deltaTime);
            float fatigueDelta = isResting
                ? -rates.RestFatigueRecoveryPerSecond * currentProfile.RestRecoveryMultiplier * deltaTime
                : rates.WorkFatiguePerSecond * deltaTime;

            int prevStress = personality.stress;
            int prevFatigue = personality.fatigue;

            stressFloat = StressRules.ClampStat(stressFloat + stressDelta);
            fatigueFloat = StressRules.ClampStat(fatigueFloat + fatigueDelta);
            personality.stress = Mathf.Clamp(Mathf.RoundToInt(stressFloat), 0, 100);
            personality.fatigue = Mathf.Clamp(Mathf.RoundToInt(fatigueFloat), 0, 100);

            ui.RecordWorkSample(activeWorkRole, stressFloat, deltaTime);

            if (personality.stress != prevStress || personality.fatigue != prevFatigue)
            {
                UpdateHud();
            }

            if (stressFloat >= StoreEndingRules.StressOverflowThreshold)
            {
                TriggerStressOverflowEnding();
            }
        }

        private bool ApplyCommandStressAndCheckOverflow(string commandText, StoreCommandIntent commandIntent, StaffWorkRole targetRole)
        {
            UtteranceTone tone = EmotionReactionRules.ParseKoreanTone(commandText);
            float delta = StressRules.EvaluateCommandStressDelta(
                commandIntent,
                tone,
                activeWorkRole,
                targetRole,
                currentProfile);

            if (Mathf.Abs(delta) > 0.001f)
            {
                stressFloat = StressRules.ClampStat(stressFloat + delta);
                personality.stress = Mathf.Clamp(Mathf.RoundToInt(stressFloat), 0, 100);
                UpdateHud();
            }

            if (stressFloat >= StoreEndingRules.StressOverflowThreshold)
            {
                TriggerStressOverflowEnding();
                return true;
            }

            return false;
        }

        private void TriggerStressOverflowEnding()
        {
            CancelWork();
            ui.CompleteDayAndShowSettlement(stressFloat);
            ui.ShowEndingScreen();
        }

        private void LateUpdate()
        {
            cursorWasLocked = Application.isFocused && ui != null && ui.CurrentMode == UiScreenMode.InGame &&
                !ui.IsCommandOverlayOpen && Cursor.lockState == CursorLockMode.Locked;
        }

        private void SelectDestination()
        {
            selectedDestination = null;
            Camera view = playerRig != null ? playerRig.GetComponentInChildren<Camera>() : Camera.main;
            if (view == null) { Feedback(CommandFeedbackKind.SystemOrMoveError, "플레이어 카메라를 찾지 못했습니다."); return; }
            RaycastHit[] hits = Physics.RaycastAll(view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), 100f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (playerRig != null && hit.collider.transform.IsChildOf(playerRig.transform)) continue;
                NavMeshAgent agent = employee.GetComponent<NavMeshAgent>();
                if (hit.normal.y < 0.7f || agent == null ||
                    !NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 0.5f,
                        new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask })) break;
                selectedDestination = navHit.position;
                Feedback(CommandFeedbackKind.Info, "이동 위치를 지정했습니다. R을 누르고 '저기로 가'라고 말씀해 주세요.");
                return;
            }
            Feedback(CommandFeedbackKind.Info, "이동 가능한 바닥을 조준해 다시 클릭해 주세요.");
        }

        private void StartRecording(bool holdR = true)
        {
            if (recording) return;
            if (transcriptionInFlight || (processing && !inferenceInFlight)) { Feedback(CommandFeedbackKind.Info, "이전 음성을 처리 중입니다."); return; }
            if (whisper == null || microphone == null || !whisper.IsLoaded)
            { Feedback(CommandFeedbackKind.SystemOrMoveError, "음성 모델을 사용할 수 없거나 로딩 중입니다."); return; }
            if (Microphone.devices.Length == 0)
            { Feedback(CommandFeedbackKind.SystemOrMoveError, "마이크를 찾지 못했습니다."); return; }
            try
            {
                if (!string.IsNullOrWhiteSpace(microphoneDeviceName))
                {
                    string selected = microphoneDeviceName.Trim();
                    if (Array.IndexOf(Microphone.devices, selected) < 0)
                    {
                        Feedback(CommandFeedbackKind.SystemOrMoveError, "지정한 마이크를 찾지 못했습니다. Inspector의 마이크 이름을 확인해 주세요.");
                        return;
                    }
                    microphone.SelectedMicDevice = selected;
                }
                else microphone.SelectedMicDevice = null;
                CancelDialogue();
                Debug.Log("[VoiceIntegration] 마이크: " + (microphone.SelectedMicDevice ?? "시스템 기본 장치") +
                    " | 사용 가능: " + string.Join(", ", Microphone.devices));
                transcript = "";
                recordedDestination = selectedDestination;
                recording = true;
                recordingHoldR = holdR;
                recordingStartedAt = Time.realtimeSinceStartup;
                microphone.StartRecord();
                ui.PushVoiceStage(VoiceProcessStage.Listening, "");
                Feedback(CommandFeedbackKind.Info, holdR
                    ? "녹음 중입니다. R 키를 떼면 인식합니다. (최대 55초)"
                    : "녹음 중입니다. 다시 V 키를 누르면 인식합니다. (최대 55초)");
            }
            catch (Exception ex) { recording = false; recordingHoldR = false; Feedback(CommandFeedbackKind.SystemOrMoveError, "녹음 실패: " + ex.Message); }
        }

        private async void OnRecordingStopped(AudioChunk audio)
        {
            if (!recording || !isActiveAndEnabled) return;
            recording = false;
            processing = true;
            transcriptionInFlight = true;
            bool ownsTranscription = true;
            Vector3? utteranceDestination = recordedDestination;
            int request = generation;
            ui.PushVoiceStage(VoiceProcessStage.Processing, "");
            try
            {
                if (audio.Data == null || audio.Data.Length == 0) throw new InvalidOperationException("녹음된 음성이 없습니다.");
                LogAudioSummary(audio);
                if (enableVoiceDiagnostics && audio.Frequency > 0 && audio.Channels > 0 && audio.Data.Length % audio.Channels == 0)
                {
                    diagnosticAudio = (float[])audio.Data.Clone();
                    diagnosticFrequency = audio.Frequency;
                    diagnosticChannels = audio.Channels;
                    Debug.Log("[VoiceIntegration] 진단 녹음 준비: F10 음량 비교, F11 단어 문맥 비교. 파일은 저장하지 않습니다.");
                }
                if (!TryPrepareAudio(audio.Data, out float[] prepared, out float gain))
                {
                    Fail(CommandFeedbackKind.RecognitionFailed, "마이크 입력이 너무 작거나 없습니다. 입력 장치와 음량을 확인해 주세요.");
                    return;
                }
                if (!boostQuietInput) { prepared = audio.Data; gain = 1f; }
                Debug.Log("[VoiceIntegration] 음성 입력 보정 배율: " + gain.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                whisper.initialPrompt = useWhisperVocabulary ? BuildWhisperVocabulary() : "";
                var whisperTimer = System.Diagnostics.Stopwatch.StartNew();
                WhisperResult result = await whisper.GetTextAsync(prepared, audio.Frequency, audio.Channels);
                whisperTimer.Stop();
                Debug.Log("[VoiceIntegration] timing whisper_ms=" + whisperTimer.ElapsedMilliseconds);
                transcriptionInFlight = false;
                ownsTranscription = false;
                if (!IsCurrent(request)) return;
                if (result == null || string.IsNullOrWhiteSpace(result.Result))
                { Fail(CommandFeedbackKind.RecognitionFailed, "음성을 이해하지 못했습니다. 다시 말씀해 주세요."); return; }
                transcript = result.Result.Trim();
                Debug.Log("[VoiceIntegration] Recognized: " + transcript);
                ui.PushVoiceStage(VoiceProcessStage.Processing, transcript);
                processing = false;
                await ProcessText(transcript, utteranceDestination);
            }
            catch (Exception ex) { if (IsCurrent(request)) Fail(CommandFeedbackKind.SystemOrMoveError, "음성 인식 실패: " + ex.Message); }
            finally { if (ownsTranscription) transcriptionInFlight = false; }
        }

        private void SubmitText(string text)
        {
            if (recording || transcriptionInFlight || ((processing || inferenceInFlight) && !IsCancelCommand(text, personality.employeeName))) { Feedback(CommandFeedbackKind.Info, "이전 명령을 처리 중입니다."); return; }
            CancelDialogue();
            transcript = text ?? "";
            _ = ProcessText(transcript, selectedDestination);
        }

        private async Task ProcessText(string text, Vector3? destination)
        {
            if (ui == null || ui.CurrentMode != UiScreenMode.InGame) return;
            if (string.IsNullOrWhiteSpace(text)) { Fail(CommandFeedbackKind.RecognitionFailed, "명령을 다시 말씀해 주세요."); return; }
            if (!TargetsEmployee(text, personality.employeeName)) { Fail(CommandFeedbackKind.Info, "현재 직원 " + personality.employeeName + "에게 명령해 주세요."); return; }
            if (IsCancelCommand(text, personality.employeeName))
            {
                bool hadWork = activeMove != 0 || activeWorkRole != StaffWorkRole.Idle || inferenceInFlight;
                CancelWork();
                Feedback(CommandFeedbackKind.Info, hadWork ? "직원에게 진행 중인 작업 중지를 요청했습니다." : "현재 진행 중인 작업이 없습니다.");
                return;
            }
            if (processing || inferenceInFlight) { Feedback(CommandFeedbackKind.Info, "이전 명령을 처리 중입니다."); return; }
            if (IsVagueCommand(text, personality.employeeName))
            {
                if (ApplyCommandStressAndCheckOverflow(text, StoreCommandIntent.Clarify, StaffWorkRole.Idle)) return;
                Fail(CommandFeedbackKind.Info, "어떤 업무를 할지 다시 말씀해 주세요.");
                return;
            }
            if (IsUnspecifiedRestockCommand(text, personality.employeeName))
            {
                if (ApplyCommandStressAndCheckOverflow(text, StoreCommandIntent.Clarify, StaffWorkRole.Idle)) return;
                Fail(CommandFeedbackKind.Info, "어떤 상품 구역을 진열할지 다시 말씀해 주세요.");
                return;
            }
            if (aiService == null) aiService = new OpenAiEditorService();
            if (!aiService.IsAvailable(out string unavailableReason))
            { Fail(CommandFeedbackKind.SystemOrMoveError, unavailableReason); return; }
            processing = true;
            inferenceInFlight = true;
            int request = generation;
            var cancellation = new CancellationTokenSource();
            decisionCancellation = cancellation;
            var decisionTimer = System.Diagnostics.Stopwatch.StartNew();
            ui.PushVoiceStage(VoiceProcessStage.Processing, text);
            try
            {
                string examples = "예시 (반응은 성격과 상태에 따라 달라질 수 있음):\n" +
                    "과자 좀 => RESTOCK|NONE|SNACK|ACCEPT_NORMAL\n민수야 과자 갖다놔 => RESTOCK|SELF|SNACK|ACCEPT_NORMAL\n" +
                    "컵라면 진열해 => RESTOCK|NONE|RAMEN|ACCEPT_NORMAL\n생활용품 채워줘 => RESTOCK|NONE|DAILY_GOODS|ACCEPT_NORMAL\n" +
                    "아이스크림 채워줘 => RESTOCK|NONE|ICE_CREAM|ACCEPT_NORMAL\n음료 냉장고 채워 => RESTOCK|NONE|WALK_IN|ACCEPT_NORMAL\n" +
                    "핫바 진열해 => RESTOCK|NONE|HOT_FOOD|ACCEPT_NORMAL\n" +
                    "진열해줘 => CLARIFY|NONE|NONE|NONE\n과자 치워 => CLARIFY|NONE|NONE|NONE\n과자 버려 => UNSUPPORTED|NONE|NONE|NONE\n" +
                    "민수야 청소해라 => CLEAN|SELF|DEFAULT|ACCEPT_NORMAL\n손님 계산해줘 => CASHIER|NONE|DEFAULT|ACCEPT_NORMAL\n" +
                    "스트레스 90, 피곤하고 화난 직원에게 계산해 => CASHIER|NONE|DEFAULT|REFUSE_EMOTION\n" +
                    "저기로 가 => MOVE|NONE|CLICKED|NONE\n지수야 과자 채워 => RESTOCK|OTHER|SNACK|ACCEPT_NORMAL\n" +
                    "가지 마 => CANCEL|NONE|NONE|NONE\n저것 좀 해 => CLARIFY|NONE|NONE|NONE\n";
                var classifierTimer = System.Diagnostics.Stopwatch.StartNew();
                string response = await aiService.ClassifyAsync("Example employee for examples: 민수.\n" + examples +
                    "Current employee: " + personality.employeeName + "; personality: " + personality.traits +
                    "; stress=" + personality.stress + "; fatigue=" + personality.fatigue + "; loyalty=" + personality.loyalty +
                    ".\n명령: " + text + "\n답:", cancellation.Token);
                classifierTimer.Stop();
                Debug.Log("[VoiceIntegration] timing classification_behavior_ms=" + classifierTimer.ElapsedMilliseconds);
                if (!IsCurrent(request)) return;
                while (recording || transcriptionInFlight) { await Task.Yield(); if (!IsCurrent(request)) return; }
                if (!TryParseCombinedCommand(response, out string intent, out string employeeTarget, out string taskTarget, out EmployeeAction proposal))
                { Fail(CommandFeedbackKind.RecognitionFailed, "명령 해석에 실패했습니다. 다시 말씀해 주세요."); return; }
                Debug.Log("[VoiceIntegration] 명령 해석: " + response.Trim());
                if (employeeTarget == "OTHER") { Fail(CommandFeedbackKind.Info, "현재 통합 테스트에는 " + personality.employeeName + " 직원 한 명만 있습니다."); return; }
                if (IsMixedCommand(text) || (HasNegation(text) && intent != "CANCEL"))
                { Fail(CommandFeedbackKind.Info, "중지와 새 업무를 나누어 말씀해 주세요."); return; }
                if (intent == "CANCEL")
                {
                    bool hadWork = activeMove != 0 || activeWorkRole != StaffWorkRole.Idle;
                    CancelWork();
                    Feedback(CommandFeedbackKind.Info, hadWork ? "직원에게 진행 중인 작업 중지를 요청했습니다." : "현재 진행 중인 작업이 없습니다.");
                    return;
                }
                if (intent == "CLARIFY")
                {
                    if (ApplyCommandStressAndCheckOverflow(text, StoreCommandIntent.Clarify, StaffWorkRole.Idle)) return;
                    Fail(CommandFeedbackKind.Info, "어떤 업무와 상품 구역인지 다시 말씀해 주세요.");
                    return;
                }
                if (intent == "UNSUPPORTED")
                {
                    if (ApplyCommandStressAndCheckOverflow(text, StoreCommandIntent.Unsupported, StaffWorkRole.Idle)) return;
                    Fail(CommandFeedbackKind.Info, "아직 지원하지 않는 업무입니다. 진열, 청소, 계산, 캐셔 또는 이동을 말씀해 주세요.");
                    return;
                }
                if (activeMove != 0) { Fail(CommandFeedbackKind.Info, "직원이 이미 이동 중입니다. 먼저 취소해 주세요."); return; }
                if (intent == "MOVE")
                {
                    if (taskTarget != "CLICKED" || !destination.HasValue)
                    { Fail(CommandFeedbackKind.Info, "이동할 바닥을 조준해 클릭한 뒤 다시 말씀해 주세요."); return; }
                    Vector3 point = destination.Value;
                    MoveReceipt move = movement.RequestMove(new WorldPoint(point.x, point.y, point.z));
                    if (!move.Accepted) { badge = StaffMoveBadge.Failed; UpdateHud(); Fail(CommandFeedbackKind.SystemOrMoveError, "이동 요청 거절: " + move.RejectReason); return; }
                    activeMove = move.RequestId;
                    lastArrivedMove = 0;
                    activeMoveIsRestock = false;
                    activeRestockTarget = null;
                    activeWorkRole = StaffWorkRole.Idle;
                    activeMoveAction = EmployeeAction.ACCEPT_NORMAL;
                    activeWork = null;
                    currentAction = "MOVE";
                    badge = StaffMoveBadge.Moving;
                    ui.PushVoiceStage(VoiceProcessStage.Completed, text);
                    UpdateHud();
                    Feedback(CommandFeedbackKind.Accepted, "직원이 클릭한 위치로 이동합니다.");
                    return;
                }
                if (!TryGetWorkAction(intent, out EmployeeAction workAction))
                { Fail(CommandFeedbackKind.RecognitionFailed, "업무를 이해하지 못했습니다. 다시 말씀해 주세요."); return; }
                if (workAction == EmployeeAction.RESTOCK && !IsRestockTarget(taskTarget))
                { Fail(CommandFeedbackKind.Info, "어떤 상품 구역을 진열할지 다시 말씀해 주세요."); return; }
                if (workAction != EmployeeAction.RESTOCK && taskTarget != "DEFAULT")
                { Fail(CommandFeedbackKind.Info, "해당 업무의 대상은 아직 지정할 수 없습니다. 다시 말씀해 주세요."); return; }
                Transform workDestination = GetWorkDestination(workAction, taskTarget);
                if (workDestination == null)
                { Fail(CommandFeedbackKind.SystemOrMoveError, WorkDescription(workAction, taskTarget) + " 임시 목적지가 설정되지 않았습니다."); return; }
                // The API proposal uses the earlier state snapshot; the final
                // team-weighted selection uses stress after applying this command.
                if (ApplyCommandStressAndCheckOverflow(text, ToStoreCommandIntent(workAction), MapToStaffWorkRole(workAction)))
                {
                    return;
                }
                EmployeeAction action = SelectTeamReaction(personality, proposal, random.NextDouble());
                decisionTimer.Stop();
                Debug.Log("[VoiceIntegration] timing decision_total_ms=" + decisionTimer.ElapsedMilliseconds);
                currentAction = action.ToString();
                ui.PushVoiceStage(VoiceProcessStage.Completed, text);
                Debug.Log("[VoiceIntegration] command=" + text + " | intent=" + intent + " | target=" + taskTarget + " | proposal=" + proposal + " | action=" + action);
                if (action == EmployeeAction.SLACK_OFF || action == EmployeeAction.REFUSE_EMOTION)
                {
                    activeWorkRole = StaffWorkRole.Idle;
                    badge = StaffMoveBadge.Idle;
                    UpdateHud();
                    Feedback(CommandFeedbackKind.RefusedByEmotion, "직원이 " + action + " 행동을 선택했습니다.");
                    if (action == EmployeeAction.REFUSE_EMOTION) ui.RecordAngerRefusal();
                    StartDialogue(text, workAction, taskTarget, action, request, 0);
                    return;
                }
                Vector3 position = workDestination.position;
                MoveReceipt receipt = movement.RequestMove(new WorldPoint(position.x, position.y, position.z));
                if (!receipt.Accepted)
                { badge = StaffMoveBadge.Failed; UpdateHud(); Fail(CommandFeedbackKind.SystemOrMoveError, "이동 요청 거절: " + receipt.RejectReason); return; }
                activeMove = receipt.RequestId;
                lastArrivedMove = 0;
                activeMoveIsRestock = workAction == EmployeeAction.RESTOCK;
                activeRestockTarget = activeMoveIsRestock ? taskTarget : null;
                activeWorkRole = MapToStaffWorkRole(workAction);
                activeMoveAction = action;
                activeWork = WorkDescription(workAction, taskTarget);
                badge = StaffMoveBadge.Moving;
                UpdateHud();
                Feedback(CommandFeedbackKind.Accepted, "직원이 " + activeWork + " 목적지로 이동합니다. 행동: " + action + ".");
                StartDialogue(text, workAction, taskTarget, action, request, receipt.RequestId);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (IsCurrent(request)) Fail(CommandFeedbackKind.SystemOrMoveError, "명령 처리 실패: " + ex.Message); }
            finally
            {
                if (ReferenceEquals(decisionCancellation, cancellation)) { decisionCancellation = null; inferenceInFlight = false; }
                cancellation.Dispose();
                if (IsCurrent(request)) processing = false;
            }
        }

        private void StartDialogue(string command, EmployeeAction workAction, string target, EmployeeAction action, int request, long moveId)
        {
            CancelDialogue();
            var cancellation = new CancellationTokenSource();
            dialogueCancellation = cancellation;
            _ = ShowDialogue(command, workAction, target, action, request, moveId, cancellation);
        }

        private void CancelDialogue()
        {
            var previous = dialogueCancellation;
            dialogueCancellation = null;
            previous?.Cancel();
        }

        private async Task ShowDialogue(string command, EmployeeAction workAction, string target, EmployeeAction action, int request, long moveId, CancellationTokenSource cancellation)
        {
            try
            {
                string line = FallbackDialogue(action);
                bool generated = false;
                if (aiService != null)
                {
                    try
                    {
                        string prompt = "직원 성격: " + personality.traits + "; 스트레스: " + personality.stress +
                            "; 피로: " + personality.fatigue + "; 충성도: " + personality.loyalty +
                            "\n요청 업무: " + WorkDescription(workAction, target) + "\n플레이어 발화: " + command + "\n확정된 행동: " + DialogueActionDescription(workAction, action) +
                            "\n확정된 행동과 모순되지 않는 짧은 한국어 대사만 작성하세요.";
                        var dialogueTimer = System.Diagnostics.Stopwatch.StartNew();
                        string response = await aiService.DialogueAsync(prompt, cancellation.Token);
                        dialogueTimer.Stop();
                        Debug.Log("[VoiceIntegration] timing dialogue_ms=" + dialogueTimer.ElapsedMilliseconds);
                        if (!IsCurrent(request)) return;
                        if (TryNormalizeDialogue(response, out string normalized)) { line = normalized; generated = true; }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex) { Debug.LogWarning("[VoiceIntegration] 대사 생성 실패: " + ex.Message); }
                }
                if (cancellation.IsCancellationRequested || !IsCurrent(request)) return;
                // Do not show an old line after a new utterance or a cancelled/failed move.
                if (recording || transcriptionInFlight || (moveId != 0 && activeMove != moveId && lastArrivedMove != moveId)) return;
                string speaker = string.IsNullOrWhiteSpace(personality.employeeName) ? "직원" : personality.employeeName;
                Debug.Log("[VoiceIntegration] 직원 대사 (" + (generated ? "OpenAI" : "기본") + "): " + speaker + ": " + line);
                ui.PushCommandFeedback(new VoiceCommandFeedback(action == EmployeeAction.SLACK_OFF || action == EmployeeAction.REFUSE_EMOTION ? CommandFeedbackKind.RefusedByEmotion : CommandFeedbackKind.Accepted,
                    command, action.ToString(), speaker + ": \"" + line + "\"", line));
            }
            finally
            {
                if (ReferenceEquals(dialogueCancellation, cancellation)) dialogueCancellation = null;
                cancellation.Dispose();
            }
        }

        public static bool TryNormalizeDialogue(string value, out string line)
        {
            line = "";
            if (string.IsNullOrWhiteSpace(value)) return false;
            string candidate = Regex.Replace(value, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();
            candidate = candidate.Trim('"', '\'', '“', '”', '‘', '’', ' ', '\r', '\n');
            if (candidate.Length == 0 || candidate.Length > 120 || candidate.Contains("\n") || candidate.Contains("\r") ||
                candidate.Contains("<") || candidate.Contains(">") || candidate.Contains("```") || !Regex.IsMatch(candidate, "[가-힣]")) return false;
            line = candidate;
            return true;
        }

        private static string DialogueActionDescription(EmployeeAction work, EmployeeAction action)
        {
            if (action == work) return WorkName(work) + " 업무를 맡았다";
            switch (action)
            {
                case EmployeeAction.ACCEPT_NORMAL: return WorkName(work) + " 업무를 정상적으로 맡았다";
                case EmployeeAction.ACCEPT_COMPLAIN: return "불평하지만 " + WorkName(work) + " 업무를 수행한다";
                case EmployeeAction.MISTAKE: return WorkName(work) + " 업무를 맡지만 실수할 예정이다. 아직 목적지에 도착하지 않았다";
                case EmployeeAction.SLACK_OFF: return WorkName(work) + " 업무를 미루고 농땡이를 피운다";
                case EmployeeAction.REFUSE_EMOTION: return WorkName(work) + " 업무를 제자리에서 거부한다";
                case EmployeeAction.IGNORE_COMMAND: return "명령을 무시하고 " + WorkName(work) + " 업무를 하지 않는다";
                case EmployeeAction.COMPLAIN: return "불평하며 " + WorkName(work) + " 업무를 거부한다";
                case EmployeeAction.REST: return WorkName(work) + " 업무를 하지 않고 휴식한다";
                default: return "업무를 수행하지 않는다";
            }
        }

        private static string FallbackDialogue(EmployeeAction action)
        {
            switch (action)
            {
                case EmployeeAction.RESTOCK: return "네, 진열할게요.";
                case EmployeeAction.CLEAN: return "네, 청소할게요.";
                case EmployeeAction.HANDLE_PAYMENT: return "네, 계산할게요.";
                case EmployeeAction.CASHIER: return "네, 계산대를 맡을게요.";
                case EmployeeAction.ACCEPT_NORMAL: return "네, 할게요.";
                case EmployeeAction.ACCEPT_COMPLAIN: return "하기 싫지만 할게요.";
                case EmployeeAction.MISTAKE: return "네, 해볼게요.";
                case EmployeeAction.SLACK_OFF: return "조금 쉬었다 할래요.";
                case EmployeeAction.REFUSE_EMOTION: return "지금은 못 하겠어요.";
                case EmployeeAction.IGNORE_COMMAND: return "지금은 못 들은 척할래요.";
                case EmployeeAction.COMPLAIN: return "지금은 하기 싫어요.";
                case EmployeeAction.REST: return "잠깐 쉬고 싶어요.";
                default: return "잠시만요.";
            }
        }

        private static string NormalizeCommand(string text, string employeeName = "민수")
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string normalized = Regex.Replace(text, @"[\s,.!?，。！？]", "");
            string fullName = (employeeName ?? "").Trim();
            if (fullName.Length == 0) return normalized;
            string givenName = fullName.Length == 3 ? fullName.Substring(1) : fullName;
            string names = fullName == "민수" ? "김민수|민수" : Regex.Escape(fullName) + "|" + Regex.Escape(givenName);
            return Regex.Replace(normalized, @"^(?:" + names + @")(?:야|아|님|씨)?", "");
        }

        public static bool TryParseCommand(string response, out string intent, out string employee, out string target)
        {
            intent = employee = target = "";
            if (string.IsNullOrWhiteSpace(response)) return false;
            string[] parts = response.Trim().Split('|');
            if (parts.Length != 3) return false;
            intent = parts[0].Trim(); employee = parts[1].Trim(); target = parts[2].Trim();
            return (intent == "RESTOCK" || intent == "CLEAN" || intent == "CASHIER" || intent == "MOVE" || intent == "CANCEL" || intent == "CLARIFY" || intent == "UNSUPPORTED") &&
                (employee == "SELF" || employee == "OTHER" || employee == "NONE") &&
                (IsRestockTarget(target) || target == "DEFAULT" || target == "CLICKED" || target == "NONE");
        }

        public static bool TryParseCombinedCommand(string response, out string intent, out string employee,
            out string target, out EmployeeAction proposal)
        {
            intent = employee = target = "";
            proposal = default;
            if (string.IsNullOrWhiteSpace(response)) return false;
            string[] parts = response.Trim().Split('|');
            if (parts.Length != 4 ||
                !TryParseCommand(parts[0] + "|" + parts[1] + "|" + parts[2], out intent, out employee, out target)) return false;
            string reaction = parts[3].Trim();
            if (intent == "RESTOCK" || intent == "CLEAN" || intent == "CASHIER")
            {
                if (intent == "RESTOCK" ? !IsRestockTarget(target) : target != "DEFAULT") return false;
                return EmployeeVoiceDemo.TryParseAction(reaction, out proposal) &&
                    (proposal == EmployeeAction.ACCEPT_NORMAL || proposal == EmployeeAction.ACCEPT_COMPLAIN ||
                     proposal == EmployeeAction.SLACK_OFF || proposal == EmployeeAction.REFUSE_EMOTION);
            }
            if (intent == "MOVE") return target == "CLICKED" && reaction == "NONE";
            return target == "NONE" && reaction == "NONE";
        }

        public static bool IsVagueCommand(string text)
            => IsVagueCommand(text, "민수");

        public static bool IsVagueCommand(string text, string employeeName)
        {
            string value = NormalizeCommand(text, employeeName);
            return Regex.IsMatch(value, @"^(저것|그것|이것|저거|그거|이거)(좀)?(해|해줘|해봐|부탁해)$");
        }

        public static bool IsUnspecifiedRestockCommand(string text, string employeeName)
        {
            string value = NormalizeCommand(text, employeeName);
            return Regex.IsMatch(value, @"^(?:진열(?:을|를)?(?:좀)?(?:해|해줘|해라|해요|해줘요|해주세요|하세요|부탁해|부탁해요)?|재고(?:를|좀)?(?:채워|채워줘|채워요|채워주세요|보충해|보충해줘))$");
        }

        public static bool IsMixedCommand(string text)
        {
            string value = NormalizeCommand(text);
            return Regex.IsMatch(value, @"(말고|그만하고|멈추고|취소하고|중지하고|한뒤|하고나서|그리고|그다음|끝나면|가고)");
        }

        public static bool HasNegation(string text)
        {
            string value = NormalizeCommand(text);
            return Regex.IsMatch(value, @"(하지마|하지말|하지않|하지는마|가지마|가지말|지마|지말|지않|안해|안돼|금지)");
        }

        public static bool IsCancelCommand(string text)
            => IsCancelCommand(text, "민수");

        public static bool IsCancelCommand(string text, string employeeName)
        {
            string value = NormalizeCommand(text, employeeName);
            if (value.Contains("취소하지마") || value.Contains("멈추지마") || value.Contains("중지하지마")) return false;
            return Regex.IsMatch(value, @"^(?:이제)?(?:(?:진열|재고|청소|계산|캐셔|이동|작업|업무)(?:을|를)?(?:은|는)?)?(?:그만|그만해|그만해줘|그만해주세요|그만해요|그만하세요|멈춰|멈춰줘|멈춰요|멈춰주세요|멈추세요|중지|중지해|중지해줘|중지해요|취소|취소해|취소해줘|취소해요|취소해주세요|하지마|하지마요|하지말아줘|하지마세요|가지마|가지마요|가지마세요|이동하지마|됐어)$");
        }

        public static bool IsMoveCommand(string text)
        {
            string value = NormalizeCommand(text);
            if (IsCancelCommand(text)) return false;
            return Regex.IsMatch(value, @"^(?:(?:저기|여기|거기|저쪽|이쪽|그쪽)(?:로|으로)?|절로|저리|이리|그리)?(?:가|가줘|가세요|가봐|가봐줘|이동해|이동해줘|이동하세요|움직여|움직여줘)$");
        }

        public static bool TargetsDemoEmployee(string text)
            => TargetsEmployee(text, "민수");

        public static bool TargetsEmployee(string text, string employeeName)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(employeeName)) return false;
            string fullName = employeeName.Trim();
            string givenName = fullName.Length == 3 ? fullName.Substring(1) : fullName;
            // A single employee may receive unnamed commands; any named addressee must match.
            MatchCollection names = Regex.Matches(text, @"(?<![가-힣A-Za-z0-9])([가-힣]{2,4})\s*(?:야|아|님|씨)(?=$|\s|[,，])");
            if (names.Count == 0)
            {
                // Unmarked Korean words such as "과자 진열" may be product names, not an addressee.
                // Let the classifier disambiguate names without a vocative suffix.
                return true;
            }
            return names.Count == 1 && (names[0].Groups[1].Value == fullName || names[0].Groups[1].Value == givenName ||
                (fullName == "민수" && names[0].Groups[1].Value == "김민수"));
        }

        public static string DescribeAudio(float[] samples, int frequency, int channels)
        {
            if (samples == null || samples.Length == 0 || frequency <= 0 || channels <= 0) return "invalid audio";
            double sumSquares = 0;
            float peak = 0;
            int valid = 0;
            foreach (float sample in samples)
            {
                if (float.IsNaN(sample) || float.IsInfinity(sample)) continue;
                float magnitude = Math.Abs(sample);
                peak = Math.Max(peak, magnitude);
                sumSquares += sample * (double)sample;
                valid++;
            }
            if (valid == 0) return "invalid samples";
            double seconds = samples.Length / (double)(frequency * channels);
            double rms = Math.Sqrt(sumSquares / valid);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "duration={0:F2}s rate={1}Hz channels={2} rms={3:F4} peak={4:F4}", seconds, frequency, channels, rms, peak);
        }

        // Boost a quiet recording without clipping; never modify the microphone's input buffer.
        public static bool TryPrepareAudio(float[] samples, out float[] prepared, out float gain)
        {
            prepared = null;
            gain = 1f;
            if (samples == null || samples.Length == 0) return false;
            double sum = 0;
            float peak = 0;
            foreach (float sample in samples)
            {
                if (float.IsNaN(sample) || float.IsInfinity(sample)) return false;
                sum += sample * (double)sample;
                peak = Math.Max(peak, Math.Abs(sample));
            }
            double rms = Math.Sqrt(sum / samples.Length);
            if (rms < 0.0002 || peak <= 0) return false;
            gain = (float)Math.Max(1.0, Math.Min(20.0, Math.Min(0.03 / rms, 0.95 / peak)));
            if (gain <= 1.001f) { prepared = samples; gain = 1f; return true; }
            prepared = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) prepared[i] = samples[i] * gain;
            return true;
        }

        private static void LogAudioSummary(AudioChunk audio)
        {
            Debug.Log("[VoiceIntegration] 녹음 진단: " + DescribeAudio(audio.Data, audio.Frequency, audio.Channels));
        }

        private async Task CompareDiagnosticTranscriptions()
        {
            if (recording || processing || transcriptionInFlight || inferenceInFlight)
            { Feedback(CommandFeedbackKind.Info, "현재 작업이 끝난 뒤 인식을 비교해 주세요."); return; }
            if (diagnosticAudio == null || whisper == null || !whisper.IsLoaded)
            { Feedback(CommandFeedbackKind.Info, "진단 녹음 또는 음성 모델이 준비되지 않았습니다."); return; }
            if (!TryPrepareAudio(diagnosticAudio, out float[] boosted, out float gain))
            { Feedback(CommandFeedbackKind.Info, "입력이 너무 작거나 잘못되어 보정 전후를 비교할 수 없습니다."); return; }
            int request = generation;
            processing = true;
            transcriptionInFlight = true;
            Feedback(CommandFeedbackKind.Info, "동일한 녹음의 원본과 보정본을 비교합니다. Console을 확인해 주세요.");
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                WhisperResult raw = await whisper.GetTextAsync(diagnosticAudio, diagnosticFrequency, diagnosticChannels);
                timer.Stop();
                if (!IsCurrent(request)) return;
                Debug.Log("[VoiceIntegration] 진단 인식 원본: " + (raw?.Result ?? "<인식 결과 없음>") + " | elapsed_ms=" + timer.ElapsedMilliseconds);
                timer.Restart();
                WhisperResult adjusted = await whisper.GetTextAsync(boosted, diagnosticFrequency, diagnosticChannels);
                timer.Stop();
                if (!IsCurrent(request)) return;
                Debug.Log("[VoiceIntegration] 진단 인식 보정본: " + (adjusted?.Result ?? "<인식 결과 없음>") + " | gain=" + gain.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " | elapsed_ms=" + timer.ElapsedMilliseconds);
                Feedback(CommandFeedbackKind.Info, "인식 비교 완료. Console의 원본·보정본 결과를 확인해 주세요.");
            }
            catch (Exception ex) { if (IsCurrent(request)) Feedback(CommandFeedbackKind.SystemOrMoveError, "인식 비교 실패: " + ex.Message); }
            finally { transcriptionInFlight = false; if (IsCurrent(request)) processing = false; }
        }

        private string BuildWhisperVocabulary()
        {
            string vocabulary = (whisperVocabulary ?? "").Trim();
            string employeeName = personality?.employeeName?.Trim();
            if (!string.IsNullOrEmpty(employeeName))
                vocabulary = string.IsNullOrEmpty(vocabulary) ? employeeName : vocabulary + ", " + employeeName;
            return vocabulary;
        }

        private async Task CompareVocabularyTranscriptions()
        {
            if (recording || processing || transcriptionInFlight || inferenceInFlight)
            { Feedback(CommandFeedbackKind.Info, "현재 작업이 끝난 뒤 단어 문맥을 비교해 주세요."); return; }
            if (diagnosticAudio == null || whisper == null || !whisper.IsLoaded)
            { Feedback(CommandFeedbackKind.Info, "진단 녹음 또는 음성 모델이 준비되지 않았습니다."); return; }
            if (!TryPrepareAudio(diagnosticAudio, out float[] prepared, out float gain))
            { Feedback(CommandFeedbackKind.Info, "입력이 너무 작거나 잘못되어 인식을 비교할 수 없습니다."); return; }
            if (!boostQuietInput) { prepared = diagnosticAudio; gain = 1f; }
            string vocabulary = BuildWhisperVocabulary();
            if (string.IsNullOrWhiteSpace(vocabulary))
            { Feedback(CommandFeedbackKind.Info, "비교할 단어 문맥이 비어 있습니다."); return; }
            int request = generation;
            processing = true;
            transcriptionInFlight = true;
            Feedback(CommandFeedbackKind.Info, "동일한 녹음으로 단어 문맥 적용 전후를 비교합니다. Console을 확인해 주세요.");
            try
            {
                whisper.initialPrompt = "";
                var timer = System.Diagnostics.Stopwatch.StartNew();
                WhisperResult without = await whisper.GetTextAsync(prepared, diagnosticFrequency, diagnosticChannels);
                timer.Stop();
                if (!IsCurrent(request)) return;
                Debug.Log("[VoiceIntegration] 단어 문맥 없음: " + (without?.Result ?? "<인식 결과 없음>") + " | elapsed_ms=" + timer.ElapsedMilliseconds);
                whisper.initialPrompt = vocabulary;
                timer.Restart();
                WhisperResult with = await whisper.GetTextAsync(prepared, diagnosticFrequency, diagnosticChannels);
                timer.Stop();
                if (!IsCurrent(request)) return;
                Debug.Log("[VoiceIntegration] 단어 문맥 적용: " + (with?.Result ?? "<인식 결과 없음>") + " | context=" + vocabulary + " | gain=" + gain.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " | elapsed_ms=" + timer.ElapsedMilliseconds);
                Feedback(CommandFeedbackKind.Info, "단어 문맥 비교 완료. Console에서 두 결과를 확인해 주세요.");
            }
            catch (Exception ex) { if (IsCurrent(request)) Feedback(CommandFeedbackKind.SystemOrMoveError, "단어 문맥 비교 실패: " + ex.Message); }
            finally
            {
                if (whisper != null) whisper.initialPrompt = useWhisperVocabulary ? BuildWhisperVocabulary() : "";
                transcriptionInFlight = false;
                if (IsCurrent(request)) processing = false;
            }
        }

        public static bool IsReaction(EmployeeAction action) => action == EmployeeAction.ACCEPT_NORMAL ||
            action == EmployeeAction.ACCEPT_COMPLAIN || action == EmployeeAction.MISTAKE ||
            action == EmployeeAction.SLACK_OFF || action == EmployeeAction.REFUSE_EMOTION;

        // Adapter connected with OpenSourceTeam6.UI.Core.StressRules archetype & stress band weights.
        public static EmployeeAction SelectTeamReaction(EmployeePersonality employee, EmployeeAction proposal, double sample)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));
            if (!IsReaction(proposal)) throw new ArgumentException("Invalid reaction proposal.", nameof(proposal));
            if (double.IsNaN(sample) || sample < 0 || sample >= 1) throw new ArgumentOutOfRangeException(nameof(sample));
            int stress = Math.Max(0, Math.Min(100, employee.stress));
            // Mistakes remain in the action enum but are disabled for this demo. The remaining
            // weights are sampled relative to their total, which renormalizes each stress band.
            double[] weights = stress < 40 ? new double[] { 85, 10, 0, 2, 0 } :
                stress < 70 ? new double[] { 35, 40, 0, 10, 5 } :
                stress < 90 ? new double[] { 10, 25, 0, 20, 25 } :
                              new double[] { 0, 10, 0, 20, 55 };
            if (employee.archetype == EmployeeArchetype.DiligentStandard) weights[0] *= 1.15;
            if (employee.archetype == EmployeeArchetype.LazyComplainer) weights[3] *= 1.8;
            if (employee.archetype == EmployeeArchetype.LiteralRigid) { weights[0] *= 1.2; weights[3] *= 0.5; }
            EmployeeAction[] reactions = { EmployeeAction.ACCEPT_NORMAL, EmployeeAction.ACCEPT_COMPLAIN,
                EmployeeAction.MISTAKE, EmployeeAction.SLACK_OFF, EmployeeAction.REFUSE_EMOTION };
            double total = 0;
            for (int i = 0; i < reactions.Length; i++)
            {
                if (reactions[i] == proposal) weights[i] *= 1.25;
                total += weights[i];
            }
            double cursor = sample * total;
            for (int i = 0; i < reactions.Length; i++)
            {
                cursor -= weights[i];
                if (cursor < 0) return reactions[i];
            }
            return reactions[reactions.Length - 1];
        }

        private static StoreCommandIntent ToStoreCommandIntent(EmployeeAction work)
        {
            switch (work)
            {
                case EmployeeAction.HANDLE_PAYMENT:
                case EmployeeAction.CASHIER:
                    return StoreCommandIntent.Checkout;
                case EmployeeAction.RESTOCK:
                case EmployeeAction.CLEAN:
                    return StoreCommandIntent.Restock;
                default:
                    return StoreCommandIntent.Unsupported;
            }
        }

        private static StaffWorkRole MapToStaffWorkRole(EmployeeAction work)
        {
            switch (work)
            {
                case EmployeeAction.HANDLE_PAYMENT:
                case EmployeeAction.CASHIER:
                    return StaffWorkRole.Checkout;
                case EmployeeAction.RESTOCK:
                    return StaffWorkRole.Restock;
                case EmployeeAction.CLEAN:
                    return StaffWorkRole.Other;
                default:
                    return StaffWorkRole.Idle;
            }
        }

        public static bool IsRestockAction(EmployeeAction action) => action == EmployeeAction.RESTOCK ||
            action == EmployeeAction.IGNORE_COMMAND || action == EmployeeAction.COMPLAIN || action == EmployeeAction.REST;

        public static bool TryGetWorkAction(string intent, out EmployeeAction action)
        {
            switch (intent)
            {
                case "RESTOCK": action = EmployeeAction.RESTOCK; return true;
                case "CLEAN": action = EmployeeAction.CLEAN; return true;
                case "HANDLE_PAYMENT": action = EmployeeAction.CASHIER; return true; // Compatibility with older callers.
                case "CASHIER": action = EmployeeAction.CASHIER; return true;
                default: action = default; return false;
            }
        }

        private static string WorkName(EmployeeAction work)
        {
            switch (work)
            {
                case EmployeeAction.RESTOCK: return "진열";
                case EmployeeAction.CLEAN: return "청소";
                case EmployeeAction.HANDLE_PAYMENT: return "계산";
                case EmployeeAction.CASHIER: return "캐셔";
                default: return "알 수 없는";
            }
        }

        public static bool IsRestockTarget(string target) => target == "SNACK" || target == "RAMEN" ||
            target == "DAILY_GOODS" || target == "ICE_CREAM" || target == "WALK_IN" || target == "HOT_FOOD";

        private static string WorkDescription(EmployeeAction work, string target)
        {
            if (work != EmployeeAction.RESTOCK) return WorkName(work);
            switch (target)
            {
                case "SNACK": return "스낵류 진열";
                case "RAMEN": return "라면류 진열";
                case "DAILY_GOODS": return "생필품 진열";
                case "ICE_CREAM": return "아이스크림 진열";
                case "WALK_IN": return "워크인 냉장고 진열";
                case "HOT_FOOD": return "FF·핫바 진열";
                default: return "진열";
            }
        }

        private Transform GetWorkDestination(EmployeeAction work, string target)
        {
            switch (work)
            {
                case EmployeeAction.RESTOCK:
                    switch (target)
                    {
                        case "SNACK": return restockSnackDestination != null ? restockSnackDestination : restockDestination;
                        case "RAMEN": return restockRamenDestination;
                        case "DAILY_GOODS": return restockDailyGoodsDestination;
                        case "ICE_CREAM": return restockIceCreamDestination;
                        case "WALK_IN": return restockWalkInDestination;
                        case "HOT_FOOD": return restockHotFoodDestination;
                        default: return null;
                    }
                case EmployeeAction.CLEAN: return cleanDestination;
                case EmployeeAction.HANDLE_PAYMENT: return paymentDestination;
                case EmployeeAction.CASHIER: return cashierDestination != null ? cashierDestination : paymentDestination;
                default: return null;
            }
        }

        public static bool IsWorkAction(EmployeeAction work, EmployeeAction proposal) =>
            TryGetWorkAction(work.ToString(), out _) && (proposal == work || proposal == EmployeeAction.IGNORE_COMMAND ||
                proposal == EmployeeAction.COMPLAIN || proposal == EmployeeAction.REST);

        public static EmployeeAction SelectRestockAction(EmployeePersonality employee, EmployeeAction proposal, double sample)
            => SelectWorkAction(employee, EmployeeAction.RESTOCK, proposal, sample);

        public static EmployeeAction SelectWorkAction(EmployeePersonality employee, EmployeeAction work, EmployeeAction proposal, double sample)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));
            if (!IsWorkAction(work, proposal)) throw new ArgumentException("Invalid work or behavior proposal.", nameof(proposal));
            if (double.IsNaN(sample) || sample < 0 || sample >= 1) throw new ArgumentOutOfRangeException(nameof(sample));
            double diligence = Math.Max(0, Math.Min(100, employee.diligence));
            double laziness = Math.Max(0, Math.Min(100, employee.laziness));
            double fatigue = Math.Max(0, Math.Min(100, employee.fatigue));
            double stress = Math.Max(0, Math.Min(100, employee.stress));
            double loyalty = Math.Max(0, Math.Min(100, employee.loyalty));
            EmployeeAction[] actions = { work, EmployeeAction.IGNORE_COMMAND, EmployeeAction.COMPLAIN, EmployeeAction.REST };
            double[] weights = { Math.Max(10, 90 + diligence * .2 - stress * .1 - fatigue * .1), 1 + laziness * .3 + fatigue * .06,
                1 + stress * .3 + (100 - loyalty) * .04, 1 + fatigue * .3 };
            double total = 0;
            for (int i = 0; i < actions.Length; i++) { if (actions[i] == proposal) weights[i] *= 1.25; total += weights[i]; }
            double cursor = sample * total;
            for (int i = 0; i < actions.Length; i++) { cursor -= weights[i]; if (cursor < 0) return actions[i]; }
            return actions[actions.Length - 1];
        }

        private void OnMovementFinished(MoveOutcome outcome)
        {
            if (outcome.RequestId != activeMove) return;
            activeMove = 0;
            lastArrivedMove = outcome.Status == MoveState.Arrived ? outcome.RequestId : 0;
            string finishedWork = activeWork;
            EmployeeAction finishedAction = activeMoveAction;
            bool finishedRestock = activeMoveIsRestock;
            string finishedTarget = activeRestockTarget;
            activeMoveIsRestock = false;
            activeRestockTarget = null;
            activeWork = null;

            if (outcome.Status == MoveState.Arrived)
            {
                badge = StaffMoveBadge.Arrived;
                if (finishedWork != null)
                {
                    if (finishedAction == EmployeeAction.MISTAKE)
                    {
                        activeWorkRole = StaffWorkRole.Idle;
                        currentAction = "MISTAKE";
                        stressFloat = StressRules.ClampStat(stressFloat + 5f);
                        personality.stress = Mathf.Clamp(Mathf.RoundToInt(stressFloat), 0, 100);
                        Feedback(CommandFeedbackKind.SystemOrMoveError, finishedWork + " 목적지에 도착했으나 업무 실수(MISTAKE)가 발생했습니다.");
                        UpdateHud();
                        if (stressFloat >= StoreEndingRules.StressOverflowThreshold)
                        {
                            TriggerStressOverflowEnding();
                        }
                        return;
                    }

                    currentAction = finishedAction.ToString();
                    var integratedStore = GetComponent<IntegratedNpcSession>();
                    if (finishedRestock && integratedStore != null && integratedStore.TryRestock(finishedTarget, out string item))
                        Feedback(CommandFeedbackKind.Accepted, finishedWork + " 완료: " + item + " 재고 3개 보충.");
                    else
                        Feedback(CommandFeedbackKind.Accepted, finishedWork + " 테스트 목적지에 도착했습니다. 실제 업무는 아직 연결되지 않았습니다.");
                }
                else
                {
                    activeWorkRole = StaffWorkRole.Idle;
                    currentAction = "IDLE";
                    Feedback(CommandFeedbackKind.Info, "지정한 위치에 도착했습니다.");
                }
            }
            else if (outcome.Status == MoveState.Cancelled)
            {
                activeWorkRole = StaffWorkRole.Idle;
                currentAction = "IDLE";
                badge = StaffMoveBadge.Cancelled;
                Feedback(CommandFeedbackKind.Info, "직원 이동이 취소되었습니다.");
            }
            else
            {
                activeWorkRole = StaffWorkRole.Idle;
                currentAction = "IDLE";
                badge = StaffMoveBadge.Failed;
                Feedback(CommandFeedbackKind.SystemOrMoveError, "이동 실패: " + outcome.Failure);
            }

            UpdateHud();
        }

        private void CancelWork()
        {
            generation++;
            lastArrivedMove = 0;
            processing = false;
            recordingHoldR = false;
            if (recording && microphone != null) { recording = false; microphone.StopRecord(); }
            decisionCancellation?.Cancel();
            CancelDialogue();
            if (activeMove != 0 && !movement.Cancel(activeMove)) activeMove = 0;
            if (activeMove == 0)
            {
                activeWork = null;
                activeMoveIsRestock = false;
                activeRestockTarget = null;
                activeWorkRole = StaffWorkRole.Idle;
            }
            ui.PushVoiceStage(VoiceProcessStage.Idle, "");
            if (activeMove == 0) { badge = StaffMoveBadge.Idle; currentAction = "IDLE"; UpdateHud(); }
        }

        private void UpdateHud()
        {
            if (ui == null) return;
            StaffWorkRole displayedRole = activeWorkRole != StaffWorkRole.Idle
                ? activeWorkRole
                : (activeMove == 0 ? StaffWorkRole.Idle : (activeMoveIsRestock ? StaffWorkRole.Restock : StaffWorkRole.Other));
            ui.UpdateEmployeeHud(new EmployeeHudSnapshot(personality.employeeName, personality.traits,
                displayedRole, currentAction, badge,
                personality.stress, personality.fatigue, personality.loyalty));
        }

        private void Fail(CommandFeedbackKind kind, string message)
        {
            processing = false;
            ui.PushVoiceStage(VoiceProcessStage.Idle, transcript);
            Feedback(kind, message);
        }

        private void Feedback(CommandFeedbackKind kind, string message)
        {
            Debug.Log("[VoiceIntegration] " + message);
            if (ui != null) ui.PushCommandFeedback(new VoiceCommandFeedback(kind, transcript, currentAction, message));
        }

        private bool IsCurrent(int request) => this != null && isActiveAndEnabled && request == generation && ui != null && ui.CurrentMode == UiScreenMode.InGame;

        private void OnDisable()
        {
            generation++;
            diagnosticAudio = null;
            selectedDestination = null;
            recordedDestination = null;
            cursorWasLocked = false;
            recordingHoldR = false;
            if (microphone != null)
            {
                microphone.OnRecordStop -= OnRecordingStopped;
                if (recording && microphone.IsRecording) microphone.StopRecord();
            }
            recording = processing = false;
            decisionCancellation?.Cancel();
            CancelDialogue();
            if (movement != null)
            {
                movement.Finished -= OnMovementFinished;
                if (activeMove != 0) movement.Cancel(activeMove);
            }
            activeMove = 0;
            activeRestockTarget = null;
            activeWorkRole = StaffWorkRole.Idle;
            if (ui != null)
            {
                ui.PersonaConfirmed -= OnPersonaConfirmed;
                ui.TextCommandSubmitted -= SubmitText;
                ui.CancelWorkRequested -= CancelWork;
                ui.VoiceRecordToggleRequested -= OnVoiceRecordToggleRequested;
                ui.DayStarted -= OnDayStarted;
            }
            subscribed = false;
        }
    }

    public sealed class VoiceCursorGate : MonoBehaviour, ICursorLockGate
    {
        private IPlayerInputGate gate;
        public void Initialize(IPlayerInputGate inputGate) { gate = inputGate; }
        public IDisposable AcquireLock(string reason) => gate.AcquireLock(reason);
        public bool IsLocked => gate != null && gate.IsLocked;
    }
}
