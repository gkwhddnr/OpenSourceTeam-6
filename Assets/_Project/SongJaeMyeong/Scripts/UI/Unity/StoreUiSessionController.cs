using System;
using System.Collections;
using OpenSourceTeam6.Time.Core;
using OpenSourceTeam6.Time.Unity;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    using Camera = UnityEngine.Camera;
    using Time = UnityEngine.Time;
    public sealed class StoreUiSessionController : MonoBehaviour, IUISystemPresenter
    {
        [Header("Panels")]
        [SerializeField] private LobbyPanelPresenter lobbyPanel;
        [SerializeField] private PersonaSetupPanelPresenter personaSetupPanel;
        [SerializeField] private InGameHudPresenter inGameHud;
        [SerializeField] private CommandOverlayPresenter commandOverlay;
        [SerializeField] private SettlementPanelPresenter settlementPanel;
        [SerializeField] private EndingPanelPresenter endingPanel;
        [SerializeField] private PausePanelPresenter pausePanel;
        [SerializeField] private SettingsPanelPresenter settingsPanel;

        [Header("Optional World-Space NPC Status UI (UI-09)")]
        [SerializeField] private EmployeeWorldStatusPresenter worldStatusPresenter;
        [SerializeField] private GameObject defaultWorldStatusPrefab;

        [Header("Audio System (SND-00 ~ SND-03)")]
        [SerializeField] private StoreAudioManager audioManager;

        [Header("Cursor Lock Gate (External Adapter or Standalone Fallback)")]
        [SerializeField] private MonoBehaviour externalCursorLockGateComponent;
        [SerializeField] private StandaloneCursorLockGate standaloneFallbackGate;

        [Header("Time & Day-Night Environment (TIME-01 연동)")]
        [SerializeField] private BusinessTimeController businessTimeController;
        [SerializeField] private TimeEnvironmentController timeEnvironmentController;
        [Tooltip("추후 2일차 또는 3일차 날씨 이벤트(비) 자동 적용 여부. 1일차는 항상 맑은 낮과 밤으로 진행됩니다.")]
        [SerializeField] private bool enableDay2Or3RainEvent = false;

        [Header("Inventory & POS Sales Ledger (물품 재고·판매·총매출)")]
        [SerializeField] private StoreInventoryManager inventoryManager;
        [SerializeField, Min(1)] private int maxPlayableDays = 3;

        [Header("Settlement & Day Settings (1차 데모 120초 규격 기본값)")]
        [SerializeField] private DaySettlementSettings settlementSettings = DaySettlementSettings.CreateDemoPreset();

        [Header("Screen Transition Fade (화면 전환 · 일차 타이틀 페이드 연출)")]
        [SerializeField, Min(0f)] private float transitionFadeOutSeconds = 0.45f;
        [SerializeField, Min(0f)] private float transitionFadeHoldSeconds = 0.12f;
        [SerializeField, Min(0f)] private float transitionFadeInSeconds = 0.55f;
        [SerializeField, Min(0f)] private float dayTitleFadeInSeconds = 0.55f;
        [SerializeField, Min(0f)] private float dayTitleHoldSeconds = 1.05f;
        [SerializeField, Min(0f)] private float dayTitleFadeOutSeconds = 0.50f;

        private DaySettlementSession settlementSession;
        private ICursorLockGate activeLockGate;
        private IStoreAudioPlayer activeAudioPlayer;
        private IDisposable screenLockToken;
        private IDisposable overlayLockToken;
        private IDisposable pauseLockToken;
        private IDisposable settingsLockToken;
        private string pendingScreenLockReason;
        private int externalModalCount;
        private int externalModalClosedFrame = -1;
        private int pauseOpenedFrame = -1;
        private CanvasGroup transitionFadeOverlay;
        private CanvasGroup transitionDayTitleGroup;
        private Text transitionDayTitleText;
        private Coroutine activeFadeCoroutine;
        private bool isTransitionFading;
        private FirstLaunchOnboardingController onboardingController;

        private float remainingDaySeconds;
        private float totalDaySeconds;
        private bool ownsFallbackExternalClock;
        private int fallbackActiveTimeDayId;
        private EmployeeHudSnapshot lastEmployeeSnapshot;
        private DaySettlementResult lastSettlementResult;
        private VoiceProcessStage lastVoiceStage = VoiceProcessStage.Idle;
        private bool isCrisisBgmLatched;

        public UiScreenMode CurrentMode { get; private set; } = UiScreenMode.Lobby;
        public EmployeePersonaSetupData CurrentPersona { get; private set; } = EmployeePersonaPresets.CreatePreset1();
        public IStoreAudioPlayer AudioPlayer => activeAudioPlayer;
        public bool IsPersonaSetupOpen => personaSetupPanel != null && personaSetupPanel.IsVisible;
        public bool IsCommandOverlayOpen => commandOverlay != null && commandOverlay.IsOpen;
        public bool IsPauseMenuOpen => pausePanel != null && pausePanel.IsOpen;
        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.IsOpen;
        public bool IsSettlementVisible => settlementPanel != null && settlementPanel.IsVisible;
        public bool IsEndingVisible => endingPanel != null && endingPanel.IsVisible;
        public bool IsTransitionFading => isTransitionFading;
        public bool IsOnboardingBlocking => onboardingController != null && onboardingController.IsBlockingInput;
        public bool IsExternalModalOpen => externalModalCount > 0;
        public float CurrentEmployeeStress => lastEmployeeSnapshot.Stress;
        public float CurrentMouseSensitivityMultiplier => settingsPanel != null
            ? settingsPanel.MouseSensitivityMultiplier
            : Mathf.Clamp(
                PlayerPrefs.GetFloat(SettingsPanelPresenter.MouseSensitivityPrefKey, 1.0f),
                SettingsPanelPresenter.MinMouseSensitivity,
                SettingsPanelPresenter.MaxMouseSensitivity);
        public int CurrentDayNumber => settlementSession != null ? settlementSession.DayNumber : 1;
        public float RemainingDaySeconds => remainingDaySeconds;
        public float TotalDaySeconds => totalDaySeconds;
        public float DayProgress => StoreDayTimeRules.ComputeDayProgress(remainingDaySeconds, totalDaySeconds);
        public float CurrentGameHour => businessTimeController != null
            ? businessTimeController.GameHour
            : StoreDayTimeRules.ComputeGameHour(DayProgress);
        public StoreTimeOfDayPhase CurrentTimeOfDayPhase => StoreDayTimeRules.ClassifyPhase(CurrentGameHour);
        public bool IsNightTime => StoreDayTimeRules.IsNightTime(CurrentGameHour);
        public bool IsRainWeather => timeEnvironmentController != null && timeEnvironmentController.IsRain;

        public event Action<EmployeePersonaSetupData> PersonaConfirmed;
        public event Action<string> TextCommandSubmitted;
        public event Action CancelWorkRequested;
        public event Action VoiceRecordToggleRequested;
        public event Action DayStarted;
        public event Action<float> MouseSensitivityChanged;

        private void Awake()
        {
            settlementSession = new DaySettlementSession(settlementSettings);
            ResolveCursorLockGate();
            ResolveAudioPlayer();
            ResolveTimeSystem();
            CurrentPersona = EmployeePersonaPresets.CreatePreset1();
            EmotionStatRates demoRates = EmotionStatRates.CreateDemoDefault();
            lastEmployeeSnapshot = new EmployeeHudSnapshot(
                CurrentPersona.EmployeeName,
                CurrentPersona.PersonalityTraits,
                StaffWorkRole.Idle,
                "IDLE",
                StaffMoveBadge.Idle,
                demoRates.InitialStress,
                demoRates.InitialFatigue,
                CurrentPersona.Profile.Loyalty);
        }

        private void OnEnable()
        {
            ResolveCursorLockGate();
            ResolveAudioPlayer();
            ResolveTimeSystem();
            if (settingsPanel != null)
            {
                settingsPanel.BindAudioPlayer(activeAudioPlayer);
            }

            if (personaSetupPanel != null)
            {
                personaSetupPanel.BindAudioPlayer(activeAudioPlayer);
                personaSetupPanel.ConfirmStartClicked += HandlePersonaConfirmStartClicked;
            }

            if (lobbyPanel != null)
            {
                lobbyPanel.BindAudioPlayer(activeAudioPlayer);
                lobbyPanel.StartClicked += HandleLobbyStartClicked;
                lobbyPanel.SettingsClicked += OpenSettingsPanel;
                lobbyPanel.QuitClicked += HandleLobbyQuitClicked;
            }

            if (inGameHud != null)
            {
                inGameHud.OpenCommandOverlayClicked += ToggleCommandOverlay;
                inGameHud.EmployeeCardFoldToggleClicked += ToggleEmployeeStatusCardFold;
            }

            if (commandOverlay != null)
            {
                commandOverlay.CommandSubmitted += HandleOverlayCommandSubmitted;
                commandOverlay.CancelWorkClicked += HandleOverlayCancelClicked;
                commandOverlay.CloseRequested += CloseCommandOverlay;
            }

            if (pausePanel != null)
            {
                pausePanel.ResumeClicked += ClosePauseMenu;
                pausePanel.OpenSettingsClicked += OpenSettingsPanel;
                pausePanel.ReturnLobbyClicked += ShowLobby;
            }

            if (settingsPanel != null)
            {
                settingsPanel.CloseRequested += CloseSettingsPanel;
                settingsPanel.MouseSensitivityChanged += HandleMouseSensitivityChanged;
            }

            if (settlementPanel != null)
            {
                settlementPanel.BindAudioPlayer(activeAudioPlayer);
                settlementPanel.NextDayClicked += HandleNextDayClicked;
                settlementPanel.ViewEndingClicked += ShowEndingScreen;
                settlementPanel.RetryDayClicked += HandleRetryDayClicked;
                settlementPanel.ReturnLobbyClicked += ShowLobby;
            }

            if (endingPanel != null)
            {
                endingPanel.BindAudioPlayer(activeAudioPlayer);
                endingPanel.RetryClicked += HandleRetryDayClicked;
                endingPanel.ReturnLobbyClicked += ShowLobby;
            }

            EnsureOnboardingController().TryPlayFirstLaunchBootIntro(
                ResolveTransitionTitleFont(),
                SyncBgmForCurrentState);
            ShowLobby();
        }

        private void Start()
        {
            FlushPendingScreenLock();
        }

        private void OnDisable()
        {
            if (personaSetupPanel != null)
            {
                personaSetupPanel.ConfirmStartClicked -= HandlePersonaConfirmStartClicked;
            }

            if (lobbyPanel != null)
            {
                lobbyPanel.StartClicked -= HandleLobbyStartClicked;
                lobbyPanel.SettingsClicked -= OpenSettingsPanel;
                lobbyPanel.QuitClicked -= HandleLobbyQuitClicked;
            }

            if (inGameHud != null)
            {
                inGameHud.OpenCommandOverlayClicked -= ToggleCommandOverlay;
                inGameHud.EmployeeCardFoldToggleClicked -= ToggleEmployeeStatusCardFold;
            }

            if (commandOverlay != null)
            {
                commandOverlay.CommandSubmitted -= HandleOverlayCommandSubmitted;
                commandOverlay.CancelWorkClicked -= HandleOverlayCancelClicked;
                commandOverlay.CloseRequested -= CloseCommandOverlay;
            }

            if (pausePanel != null)
            {
                pausePanel.ResumeClicked -= ClosePauseMenu;
                pausePanel.OpenSettingsClicked -= OpenSettingsPanel;
                pausePanel.ReturnLobbyClicked -= ShowLobby;
            }

            if (settingsPanel != null)
            {
                settingsPanel.CloseRequested -= CloseSettingsPanel;
                settingsPanel.MouseSensitivityChanged -= HandleMouseSensitivityChanged;
            }

            if (settlementPanel != null)
            {
                settlementPanel.NextDayClicked -= HandleNextDayClicked;
                settlementPanel.ViewEndingClicked -= ShowEndingScreen;
                settlementPanel.RetryDayClicked -= HandleRetryDayClicked;
                settlementPanel.ReturnLobbyClicked -= ShowLobby;
            }

            if (endingPanel != null)
            {
                endingPanel.RetryClicked -= HandleRetryDayClicked;
                endingPanel.ReturnLobbyClicked -= ShowLobby;
            }

            if (businessTimeController != null)
            {
                businessTimeController.Clock.IsPaused = false;
                if (ownsFallbackExternalClock)
                {
                    businessTimeController.ReleaseExternalClock();
                }
            }
            ownsFallbackExternalClock = false;
            fallbackActiveTimeDayId = 0;

            CancelActiveFadeTransition();
            RestoreTimeScale();
            ReleaseSettingsLock();
            ReleasePauseLock();
            ReleaseOverlayLock();
            ReleaseScreenLock();
        }

        private void Update()
        {
            FlushPendingScreenLock();
            if (isTransitionFading || IsOnboardingBlocking) return;

            bool externalModalBlocking = IsExternalModalOpen || externalModalClosedFrame == Time.frameCount;
            if (!externalModalBlocking)
            {
                HandleGlobalEscapeHotkey();
            }

            if (CurrentMode != UiScreenMode.InGame || IsPauseMenuOpen || IsSettingsOpen) return;

            if (!externalModalBlocking)
            {
                HandleInGameHotkeys();
            }

            if (remainingDaySeconds > 0f)
            {
                remainingDaySeconds = Mathf.Max(0f, remainingDaySeconds - Time.deltaTime);
                if (ownsFallbackExternalClock && businessTimeController != null && fallbackActiveTimeDayId > 0)
                {
                    businessTimeController.ApplyExternalProgress(fallbackActiveTimeDayId, DayProgress);
                }
                RefreshTopBar();

                if (remainingDaySeconds <= 0f)
                {
                    CompleteDayAndShowSettlement(lastEmployeeSnapshot.Stress);
                }
            }
        }

        private void LateUpdate()
        {
            if (CurrentMode == UiScreenMode.InGame && !IsPauseMenuOpen && !IsSettingsOpen && businessTimeController != null)
            {
                RefreshTopBar();
            }
        }

        public void BindTimeSystem(BusinessTimeController timeController, TimeEnvironmentController environmentController = null)
        {
            businessTimeController = timeController;
            if (environmentController != null)
            {
                timeEnvironmentController = environmentController;
            }
            else if (businessTimeController != null && timeEnvironmentController == null)
            {
                timeEnvironmentController = businessTimeController.GetComponent<TimeEnvironmentController>();
            }

            SyncTimeSystemPauseState();
            RefreshTopBar();
        }

        public void SetRainWeather(bool isRain)
        {
            ResolveTimeSystem();
            if (timeEnvironmentController != null)
            {
                timeEnvironmentController.IsRain = isRain;
            }
        }

        public void AdvanceRemainingDaySeconds(float secondsToSkip)
        {
            if (CurrentMode != UiScreenMode.InGame || remainingDaySeconds <= 0f) return;
            if (float.IsNaN(secondsToSkip) || float.IsInfinity(secondsToSkip) || secondsToSkip <= 0f) return;

            remainingDaySeconds = Mathf.Max(0.1f, remainingDaySeconds - secondsToSkip);
            if (ownsFallbackExternalClock && businessTimeController != null && fallbackActiveTimeDayId > 0)
            {
                businessTimeController.ApplyExternalProgress(fallbackActiveTimeDayId, DayProgress);
            }
            RefreshTopBar();
        }

        public void SetWorldStatusPresenter(EmployeeWorldStatusPresenter presenter)
        {
            worldStatusPresenter = presenter;
            if (worldStatusPresenter != null)
            {
                worldStatusPresenter.RenderSnapshot(lastEmployeeSnapshot);
            }
        }

        public void BindEmployeeTarget(Transform employeeTransform, Transform cameraTransform = null)
        {
            if (employeeTransform == null) return;

            if (worldStatusPresenter == null)
            {
                worldStatusPresenter = employeeTransform.GetComponentInChildren<EmployeeWorldStatusPresenter>(true);
            }

            if (worldStatusPresenter == null && defaultWorldStatusPrefab != null)
            {
                GameObject instance = Instantiate(defaultWorldStatusPrefab);
                instance.name = "EmployeeWorldStatusUI";
                worldStatusPresenter = instance.GetComponent<EmployeeWorldStatusPresenter>();
            }

            if (worldStatusPresenter != null)
            {
                worldStatusPresenter.SetFollowTarget(employeeTransform);
                worldStatusPresenter.RenderSnapshot(lastEmployeeSnapshot);
            }
        }

        public void SetAudioPlayer(IStoreAudioPlayer player)
        {
            activeAudioPlayer = player;
            if (lobbyPanel != null)
            {
                lobbyPanel.BindAudioPlayer(activeAudioPlayer);
            }

            if (settingsPanel != null)
            {
                settingsPanel.BindAudioPlayer(activeAudioPlayer);
            }

            if (personaSetupPanel != null)
            {
                personaSetupPanel.BindAudioPlayer(activeAudioPlayer);
            }

            if (endingPanel != null)
            {
                endingPanel.BindAudioPlayer(activeAudioPlayer);
            }

            SyncBgmForCurrentState();
        }

        public void PlaySfx(StoreSfxId sfxId, float volumeScale = 1f)
        {
            activeAudioPlayer?.PlaySfx(sfxId, volumeScale);
        }

        public IDisposable AcquireExternalModalLock(string reason)
        {
            ResolveCursorLockGate();
            externalModalCount++;
            IDisposable innerLock = null;
            if (activeLockGate != null)
            {
                try
                {
                    innerLock = activeLockGate.AcquireLock(string.IsNullOrWhiteSpace(reason) ? "ExternalModal" : reason);
                }
                catch (Exception)
                {
                    innerLock = null;
                }
            }

            return new ExternalModalLease(this, innerLock);
        }

        private void ReleaseExternalModalLease(IDisposable innerLock)
        {
            externalModalCount = Mathf.Max(0, externalModalCount - 1);
            externalModalClosedFrame = Time.frameCount;
            innerLock?.Dispose();
        }

        private sealed class ExternalModalLease : IDisposable
        {
            private StoreUiSessionController owner;
            private IDisposable innerLock;

            public ExternalModalLease(StoreUiSessionController owner, IDisposable innerLock)
            {
                this.owner = owner;
                this.innerLock = innerLock;
            }

            public void Dispose()
            {
                if (owner == null) return;
                StoreUiSessionController target = owner;
                IDisposable lockToRelease = innerLock;
                owner = null;
                innerLock = null;
                target.ReleaseExternalModalLease(lockToRelease);
            }
        }

        private void HandleMouseSensitivityChanged(float multiplier)
        {
            MouseSensitivityChanged?.Invoke(multiplier);
        }

        public void SetExternalCursorLockGate(ICursorLockGate externalGate)
        {
            ReleaseSettingsLock();
            ReleasePauseLock();
            ReleaseOverlayLock();
            ReleaseScreenLock();
            activeLockGate = externalGate;

            if (standaloneFallbackGate != null && externalGate != null && !ReferenceEquals(externalGate, standaloneFallbackGate))
            {
                standaloneFallbackGate.SetApplyUnityCursorState(false);
            }

            ReapplyCurrentLocks();
        }

        public void ShowLobby()
        {
            CloseSettingsPanel();
            ClosePauseMenu();
            CloseCommandOverlay();
            RestoreTimeScale();

            CurrentMode = UiScreenMode.Lobby;
            SyncTimeSystemPauseState();
            if (ownsFallbackExternalClock && businessTimeController != null && fallbackActiveTimeDayId > 0)
            {
                businessTimeController.Clock.CancelDay(fallbackActiveTimeDayId);
                fallbackActiveTimeDayId = 0;
            }

            if (lobbyPanel != null) lobbyPanel.SetVisible(true);
            if (personaSetupPanel != null) personaSetupPanel.SetVisible(false);
            if (inGameHud != null) inGameHud.SetVisible(false);
            if (settlementPanel != null) settlementPanel.SetVisible(false);
            if (endingPanel != null) endingPanel.SetVisible(false);
            onboardingController?.HideResumeGuideWithoutSaving();

            SyncBgmForCurrentState();
            AcquireScreenLock("LobbyScreen");
        }

        public void ShowPersonaSetup()
        {
            if (personaSetupPanel == null)
            {
                StartDay(1, settlementSettings.StartingBalanceWon, settlementSettings.DayDurationSeconds);
                return;
            }

            CloseSettingsPanel();
            ClosePauseMenu();
            CloseCommandOverlay();
            RestoreTimeScale();

            CurrentMode = UiScreenMode.Lobby;

            if (lobbyPanel != null) lobbyPanel.SetVisible(false);
            if (inGameHud != null) inGameHud.SetVisible(false);
            if (settlementPanel != null) settlementPanel.SetVisible(false);
            if (endingPanel != null) endingPanel.SetVisible(false);
            personaSetupPanel.SetVisible(true);
            EnsureOnboardingController().TryShowFirstPlayResumeGuide(ResolveTransitionTitleFont(), activeAudioPlayer);

            SyncBgmForCurrentState();
            AcquireScreenLock("PersonaSetupScreen");
        }

        public void StartDay(int dayNumber, int startingBalanceWon, float durationSeconds)
        {
            StartDayInternal(dayNumber, startingBalanceWon, durationSeconds, playDoorChime: true);
        }

        private void StartDayInternal(int dayNumber, int startingBalanceWon, float durationSeconds, bool playDoorChime)
        {
            if (settlementSession == null)
            {
                settlementSession = new DaySettlementSession(settlementSettings);
            }

            if (inventoryManager == null)
            {
                inventoryManager = FindAnyObjectByType<StoreInventoryManager>();
            }

            if (dayNumber <= 1)
            {
                settlementSession.SetTargetBalanceWon(settlementSettings.TargetBalanceWon);
                inventoryManager?.ResetForNewGame();
            }
            else
            {
                int dailyGoalDelta = Mathf.Max(20000, settlementSettings.TargetBalanceWon - settlementSettings.StartingBalanceWon);
                settlementSession.SetTargetBalanceWon(startingBalanceWon + dailyGoalDelta);
                inventoryManager?.BeginNextDay();
            }

            settlementSession.Reset(dayNumber, startingBalanceWon);
            remainingDaySeconds = durationSeconds > 0f ? durationSeconds : settlementSettings.DayDurationSeconds;
            totalDaySeconds = remainingDaySeconds;

            CloseSettingsPanel();
            ClosePauseMenu();
            CloseCommandOverlay();
            ReleaseScreenLock();
            RestoreTimeScale();

            CurrentMode = UiScreenMode.InGame;
            lastVoiceStage = VoiceProcessStage.Idle;
            isCrisisBgmLatched = false;
            if (lobbyPanel != null) lobbyPanel.SetVisible(false);
            if (personaSetupPanel != null) personaSetupPanel.SetVisible(false);
            if (settlementPanel != null) settlementPanel.SetVisible(false);
            if (endingPanel != null) endingPanel.SetVisible(false);
            if (inGameHud != null)
            {
                inGameHud.SetVisible(true);
                inGameHud.SetEmployeeCardCollapsed(false);
                inGameHud.RenderEmployee(lastEmployeeSnapshot);
                inGameHud.RenderVoiceStage(VoiceProcessStage.Idle, string.Empty);
                inGameHud.RenderFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.Info,
                    string.Empty,
                    string.Empty,
                    "[R] 키(누른 채 말하기) 또는 [V] 키로 음성 지시하거나 [Tab] 키로 텍스트 명령창을 여세요."));
            }

            if (worldStatusPresenter != null)
            {
                worldStatusPresenter.RenderSnapshot(lastEmployeeSnapshot);
                worldStatusPresenter.ShowSpeechBubble(string.Empty, CommandFeedbackKind.Info);
            }

            PrepareTimeSystemForDayStart(dayNumber);
            DayStarted?.Invoke();
            RefreshTopBar();
            if (playDoorChime)
            {
                activeAudioPlayer?.PlaySfx(StoreSfxId.StoreDoorChime);
            }
            SyncBgmForCurrentState();
        }

        public void UpdateEmployeeHud(EmployeeHudSnapshot snapshot)
        {
            StaffMoveBadge previousBadge = lastEmployeeSnapshot.MoveBadge;
            float previousStress = lastEmployeeSnapshot.Stress;
            lastEmployeeSnapshot = snapshot;
            if (Mathf.Abs(snapshot.Stress - previousStress) >= 1f)
            {
                Debug.Log($"[StoreUI] 직원 상태 갱신: 스트레스 {previousStress:F0} -> {snapshot.Stress:F0}, 피로 {snapshot.Fatigue:F0}, 역할={snapshot.CurrentRole}, 행동={snapshot.ActionCodeLabel}");
            }

            if (inGameHud != null)
            {
                inGameHud.RenderEmployee(snapshot);
            }

            if (worldStatusPresenter != null)
            {
                worldStatusPresenter.RenderSnapshot(snapshot);
            }

            if (CurrentMode == UiScreenMode.InGame)
            {
                if (previousBadge != StaffMoveBadge.Arrived &&
                    snapshot.MoveBadge == StaffMoveBadge.Arrived &&
                    snapshot.CurrentRole != StaffWorkRole.Idle)
                {
                    activeAudioPlayer?.PlaySfx(
                        snapshot.CurrentRole == StaffWorkRole.Checkout
                            ? StoreSfxId.BarcodeCheckout
                            : StoreSfxId.WorkTaskComplete);
                }

                SyncBgmForCurrentState();
            }
        }

        public void PushVoiceStage(VoiceProcessStage stage, string recognizedTranscript)
        {
            if (StoreAudioRules.TryResolveVoiceStageSfx(lastVoiceStage, stage, out StoreSfxId sfxId))
            {
                activeAudioPlayer?.PlaySfx(sfxId);
            }

            lastVoiceStage = stage;

            if (inGameHud != null)
            {
                inGameHud.RenderVoiceStage(stage, recognizedTranscript);
            }
        }

        public void PushCommandFeedback(VoiceCommandFeedback feedback)
        {
            if (StoreAudioRules.TryResolveFeedbackSfx(feedback.Kind, out StoreSfxId sfxId))
            {
                activeAudioPlayer?.PlaySfx(sfxId);
            }

            if (inGameHud != null)
            {
                inGameHud.RenderFeedback(feedback);
            }

            if (worldStatusPresenter != null && !string.IsNullOrWhiteSpace(feedback.EmployeeDialogue))
            {
                worldStatusPresenter.ShowSpeechBubble(feedback.EmployeeDialogue, feedback.Kind);
            }
        }

        public void RecordWorkSample(StaffWorkRole role, float stress, float deltaSeconds)
        {
            if (settlementSession == null || CurrentMode != UiScreenMode.InGame) return;
            settlementSession.RecordWorkSample(role, stress, deltaSeconds);
            RefreshTopBar();
        }

        public void RecordAngerRefusal()
        {
            if (settlementSession == null || CurrentMode != UiScreenMode.InGame) return;
            settlementSession.RecordAngerRefusal();
        }

        public void NotifyInventoryRevenueChanged()
        {
            RefreshTopBar();
        }

        public DaySettlementResult CompleteDayAndShowSettlement(float endingStress)
        {
            if (settlementSession == null)
            {
                settlementSession = new DaySettlementSession(settlementSettings);
            }

            if (inventoryManager == null)
            {
                inventoryManager = FindAnyObjectByType<StoreInventoryManager>();
            }

            CloseSettingsPanel();
            ClosePauseMenu();
            CloseCommandOverlay();
            RestoreTimeScale();

            int itemSalesWon = inventoryManager != null ? inventoryManager.TodayRevenueWon : 0;
            int cumulativeSalesWon = inventoryManager != null ? inventoryManager.CumulativeRevenueWon : itemSalesWon;
            int todayUnitsSold = inventoryManager != null ? inventoryManager.TodayUnitsSold : 0;
            string inventoryDetailReport = inventoryManager != null ? inventoryManager.BuildSettlementDetailText(8) : null;

            DaySettlementResult result = settlementSession.SettleOnce(endingStress, itemSalesWon);
            lastSettlementResult = result;
            CurrentMode = UiScreenMode.Settlement;
            SyncTimeSystemPauseState();
            if (ownsFallbackExternalClock && businessTimeController != null && fallbackActiveTimeDayId > 0)
            {
                businessTimeController.ApplyExternalProgress(fallbackActiveTimeDayId, 1.0);
                businessTimeController.Clock.ConfirmSettlement(fallbackActiveTimeDayId);
            }

            if (lobbyPanel != null) lobbyPanel.SetVisible(false);
            if (personaSetupPanel != null) personaSetupPanel.SetVisible(false);
            if (inGameHud != null) inGameHud.SetVisible(false);
            if (endingPanel != null) endingPanel.SetVisible(false);
            if (settlementPanel != null)
            {
                bool isFinalOrQuit = endingStress >= StoreEndingRules.StressOverflowThreshold ||
                                     result.DayNumber >= Mathf.Max(1, maxPlayableDays);
                settlementPanel.SetVisible(true);
                settlementPanel.RenderResult(
                    result,
                    itemSalesWon,
                    cumulativeSalesWon,
                    todayUnitsSold,
                    inventoryDetailReport,
                    isFinalOrQuit);
            }

            activeAudioPlayer?.PlaySfx(StoreSfxId.PaperFlip);
            SyncBgmForCurrentState();
            AcquireScreenLock("SettlementScreen");
            return result;
        }

        public void ShowEndingScreen()
        {
            CloseSettingsPanel();
            ClosePauseMenu();
            CloseCommandOverlay();
            RestoreTimeScale();

            CurrentMode = UiScreenMode.Settlement;

            if (lobbyPanel != null) lobbyPanel.SetVisible(false);
            if (personaSetupPanel != null) personaSetupPanel.SetVisible(false);
            if (inGameHud != null) inGameHud.SetVisible(false);
            if (settlementPanel != null) settlementPanel.SetVisible(false);

            if (endingPanel != null)
            {
                endingPanel.SetVisible(true);
                endingPanel.RenderEnding(lastSettlementResult, CurrentPersona);
            }

            activeAudioPlayer?.PlaySfx(StoreSfxId.PaperFlip);
            SyncBgmForCurrentState();
            AcquireScreenLock("EndingScreen");
        }

        public void ToggleCommandOverlay()
        {
            if (CurrentMode != UiScreenMode.InGame || IsPauseMenuOpen || IsSettingsOpen || commandOverlay == null) return;
            if (commandOverlay.IsOpen) CloseCommandOverlay();
            else OpenCommandOverlay();
        }

        public void OpenCommandOverlay()
        {
            if (CurrentMode != UiScreenMode.InGame || commandOverlay == null) return;
            bool wasOpen = commandOverlay.IsOpen;
            commandOverlay.SetOpen(true);
            if (!wasOpen)
            {
                activeAudioPlayer?.PlaySfx(StoreSfxId.UiPopupOpenClose);
            }
            if (overlayLockToken == null)
            {
                overlayLockToken = TryAcquireGateLock("CommandOverlayUI");
            }
        }

        public void CloseCommandOverlay()
        {
            bool wasOpen = commandOverlay != null && commandOverlay.IsOpen;
            if (commandOverlay != null)
            {
                commandOverlay.SetOpen(false);
            }
            if (wasOpen)
            {
                activeAudioPlayer?.PlaySfx(StoreSfxId.UiPopupOpenClose);
            }
            ReleaseOverlayLock();
        }

        public void OpenPauseMenu()
        {
            if (CurrentMode != UiScreenMode.InGame || pausePanel == null) return;
            CloseCommandOverlay();
            bool wasOpen = pausePanel.IsOpen;
            pausePanel.SetOpen(true);
            if (!wasOpen)
            {
                pauseOpenedFrame = Time.frameCount;
                activeAudioPlayer?.PlaySfx(StoreSfxId.UiPopupOpenClose);
            }
            Time.timeScale = 0f;
            SyncTimeSystemPauseState();
            if (pauseLockToken == null)
            {
                pauseLockToken = TryAcquireGateLock("PauseMenuUI");
            }
        }

        public void ClosePauseMenu()
        {
            bool wasOpen = pausePanel != null && pausePanel.IsOpen;
            if (pausePanel != null)
            {
                pausePanel.SetOpen(false);
            }
            if (wasOpen)
            {
                activeAudioPlayer?.PlaySfx(StoreSfxId.UiPopupOpenClose);
            }
            ReleasePauseLock();
            RestoreTimeScale();
            SyncTimeSystemPauseState();
        }

        public void OpenSettingsPanel()
        {
            if (settingsPanel == null) return;
            bool wasOpen = settingsPanel.IsOpen;
            settingsPanel.SetOpen(true);
            if (!wasOpen)
            {
                activeAudioPlayer?.PlaySfx(StoreSfxId.UiPopupOpenClose);
            }
            SyncTimeSystemPauseState();
            if (settingsLockToken == null)
            {
                settingsLockToken = TryAcquireGateLock("SettingsPanelUI");
            }
        }

        public void CloseSettingsPanel()
        {
            bool wasOpen = settingsPanel != null && settingsPanel.IsOpen;
            if (settingsPanel != null)
            {
                settingsPanel.SetOpen(false);
            }
            if (wasOpen)
            {
                activeAudioPlayer?.PlaySfx(StoreSfxId.UiPopupOpenClose);
            }
            ReleaseSettingsLock();
            SyncTimeSystemPauseState();
        }

        private void HandleGlobalEscapeHotkey()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            bool escapePressed = kb.escapeKey.wasPressedThisFrame;
            bool pauseKeyPressed = CurrentMode == UiScreenMode.InGame &&
                                   !IsCommandOverlayOpen &&
                                   !IsSettingsOpen &&
                                   kb.pKey.wasPressedThisFrame;

            if (!escapePressed && !pauseKeyPressed) return;

            if (escapePressed && IsSettingsOpen)
            {
                CloseSettingsPanel();
                return;
            }

            if (escapePressed && IsPersonaSetupOpen)
            {
                ShowLobby();
                return;
            }

            if (CurrentMode != UiScreenMode.InGame) return;

            if (escapePressed && IsCommandOverlayOpen)
            {
                CloseCommandOverlay();
                return;
            }

            if (pausePanel != null)
            {
                if (pauseOpenedFrame == Time.frameCount) return;
                if (IsPauseMenuOpen) ClosePauseMenu();
                else OpenPauseMenu();
            }
        }

        private void HandleInGameHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame)
            {
                ToggleCommandOverlay();
            }
            else if (kb.vKey.wasPressedThisFrame && !IsCommandOverlayOpen)
            {
                VoiceRecordToggleRequested?.Invoke();
            }
            else if (kb.gKey.wasPressedThisFrame && !IsCommandOverlayOpen)
            {
                ToggleEmployeeStatusCardFold();
            }
        }

        public void ToggleEmployeeStatusCardFold()
        {
            if (inGameHud == null || CurrentMode != UiScreenMode.InGame) return;
            inGameHud.ToggleEmployeeCardCollapsed();
            activeAudioPlayer?.PlaySfx(StoreSfxId.PaperFlip);
        }

        private void RefreshTopBar()
        {
            if (inGameHud == null || settlementSession == null) return;
            int itemSalesWon = inventoryManager != null ? inventoryManager.TodayRevenueWon : 0;
            int liveEstimatedBalance = settlementSession.StartingBalanceWon +
                                       settlementSession.EstimateLiveGrossSalesWon() +
                                       itemSalesWon;
            float? explicitGameHour = businessTimeController != null &&
                                      (businessTimeController.Clock.Phase == BusinessPhase.Open ||
                                       businessTimeController.Clock.Phase == BusinessPhase.Closing)
                ? businessTimeController.GameHour
                : (float?)null;
            inGameHud.UpdateTopBar(
                settlementSession.DayNumber,
                remainingDaySeconds,
                liveEstimatedBalance,
                settlementSession.TargetBalanceWon,
                totalDaySeconds,
                explicitGameHour);
        }

        private void ResolveTimeSystem()
        {
            if (businessTimeController == null)
            {
                businessTimeController = FindAnyObjectByType<BusinessTimeController>();
            }

            if (timeEnvironmentController == null)
            {
                if (businessTimeController != null)
                {
                    timeEnvironmentController = businessTimeController.GetComponent<TimeEnvironmentController>();
                }

                if (timeEnvironmentController == null)
                {
                    timeEnvironmentController = FindAnyObjectByType<TimeEnvironmentController>();
                }
            }

            if (Application.isPlaying && businessTimeController == null)
            {
                TryBootstrapRuntimeTimeSystem();
            }
        }

        private void TryBootstrapRuntimeTimeSystem()
        {
            Camera viewCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            Light dirLight = RenderSettings.sun;
            if (dirLight == null || dirLight.type != LightType.Directional)
            {
                Light[] lights = FindObjectsByType<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        dirLight = lights[i];
                        break;
                    }
                }
            }

            if (viewCam == null || dirLight == null) return;

            viewCam.clearFlags = CameraClearFlags.Skybox;
            if (viewCam.farClipPlane < 1000f)
            {
                viewCam.farClipPlane = 1000f;
            }

            GameObject timeRoot = new GameObject("Time System (Runtime)");
            timeRoot.SetActive(false);

            businessTimeController = timeRoot.AddComponent<BusinessTimeController>();
            timeRoot.AddComponent<DayNightPresenter>();
            timeRoot.AddComponent<CelestialBodyPresenter>();
            RainWeatherController rainCtrl = timeRoot.AddComponent<RainWeatherController>();
            rainCtrl.isRain = false;
            timeRoot.AddComponent<TimeSkyExposure>();
            timeEnvironmentController = timeRoot.AddComponent<TimeEnvironmentController>();
            timeEnvironmentController.Configure(dirLight, viewCam);

            timeRoot.SetActive(true);
            ownsFallbackExternalClock = businessTimeController.AcquireExternalClock();
        }

        private void PrepareTimeSystemForDayStart(int dayNumber)
        {
            ResolveTimeSystem();
            if (businessTimeController != null)
            {
                businessTimeController.Clock.IsPaused = false;
                if (businessTimeController.Clock.Phase == BusinessPhase.Open)
                {
                    businessTimeController.Clock.CancelDay(businessTimeController.Clock.DayId);
                }
                else if (businessTimeController.Clock.Phase == BusinessPhase.Closing)
                {
                    businessTimeController.Clock.ConfirmSettlement(businessTimeController.Clock.DayId);
                }

                if (ownsFallbackExternalClock)
                {
                    int nextDayId = businessTimeController.Clock.DayId + 1;
                    if (businessTimeController.BeginDay(nextDayId))
                    {
                        fallbackActiveTimeDayId = nextDayId;
                    }
                }
            }

            if (timeEnvironmentController != null)
            {
                timeEnvironmentController.IsRain = StoreDayTimeRules.ShouldRainForDay(dayNumber, enableDay2Or3RainEvent);
            }
        }

        private void SyncTimeSystemPauseState()
        {
            if (businessTimeController == null) return;
            bool shouldPauseClock = isTransitionFading || (CurrentMode == UiScreenMode.InGame && (IsPauseMenuOpen || IsSettingsOpen));
            businessTimeController.Clock.IsPaused = shouldPauseClock;
        }

        private void ResolveAudioPlayer()
        {
            if (activeAudioPlayer != null) return;

            if (audioManager == null)
            {
                audioManager = GetComponentInChildren<StoreAudioManager>(true);
            }

            if (audioManager == null)
            {
                GameObject audioObj = new GameObject("StoreAudioManager");
                audioObj.transform.SetParent(transform, false);
                audioManager = audioObj.AddComponent<StoreAudioManager>();
            }

            activeAudioPlayer = audioManager;
        }

        private void SyncBgmForCurrentState()
        {
            if (activeAudioPlayer == null) return;

            if (onboardingController != null && onboardingController.IsBootIntroActive)
            {
                activeAudioPlayer.PlayBgm(StoreBgmTrack.None);
                return;
            }

            StoreBgmTrack targetTrack;
            if (CurrentMode == UiScreenMode.InGame)
            {
                float stress = lastEmployeeSnapshot.Stress;
                if (!isCrisisBgmLatched && stress >= StressRules.AngerThreshold)
                {
                    isCrisisBgmLatched = true;
                }
                else if (isCrisisBgmLatched && stress < StressRules.AngerThreshold - 12f)
                {
                    isCrisisBgmLatched = false;
                }

                targetTrack = isCrisisBgmLatched ? StoreBgmTrack.DayCrisis : StoreBgmTrack.DayNormal;
            }
            else
            {
                isCrisisBgmLatched = false;
                targetTrack = StoreAudioRules.ResolveBgmForState(CurrentMode, lastEmployeeSnapshot.Stress);
            }

            activeAudioPlayer.PlayBgm(targetTrack);
        }

        private void ResolveCursorLockGate()
        {
            if (externalCursorLockGateComponent is ICursorLockGate injectedGate)
            {
                activeLockGate = injectedGate;
                if (standaloneFallbackGate != null)
                {
                    standaloneFallbackGate.SetApplyUnityCursorState(false);
                }
                return;
            }

            activeLockGate = standaloneFallbackGate;
        }

        private void AcquireScreenLock(string reason)
        {
            ReleaseScreenLock();
            screenLockToken = TryAcquireGateLock(reason);
            pendingScreenLockReason = screenLockToken == null ? reason : null;
        }

        private void FlushPendingScreenLock()
        {
            if (CurrentMode == UiScreenMode.InGame)
            {
                pendingScreenLockReason = null;
                return;
            }

            if (screenLockToken != null)
            {
                pendingScreenLockReason = null;
                return;
            }

            string reason = !string.IsNullOrEmpty(pendingScreenLockReason)
                ? pendingScreenLockReason
                : (CurrentMode == UiScreenMode.Settlement ? "SettlementScreen" : "LobbyScreen");
            screenLockToken = TryAcquireGateLock(reason);
            if (screenLockToken != null)
            {
                pendingScreenLockReason = null;
            }
        }

        private IDisposable TryAcquireGateLock(string reason)
        {
            ResolveCursorLockGate();
            if (activeLockGate == null) return null;

            try
            {
                return activeLockGate.AcquireLock(reason);
            }
            catch (InvalidOperationException)
            {
                // PlayerUiCursorLockAdapter.Awake()가 아직 실행되기 전(OnEnable 타이밍)이면
                // Start()/Update()에서 자동으로 재시도해 커서 잠금을 안전하게 획득한다.
                return null;
            }
        }

        private void ReleaseScreenLock()
        {
            pendingScreenLockReason = null;
            if (screenLockToken == null) return;
            screenLockToken.Dispose();
            screenLockToken = null;
        }

        private void ReleaseOverlayLock()
        {
            if (overlayLockToken == null) return;
            overlayLockToken.Dispose();
            overlayLockToken = null;
        }

        private void ReleasePauseLock()
        {
            if (pauseLockToken == null) return;
            pauseLockToken.Dispose();
            pauseLockToken = null;
        }

        private void ReleaseSettingsLock()
        {
            if (settingsLockToken == null) return;
            settingsLockToken.Dispose();
            settingsLockToken = null;
        }

        private static void RestoreTimeScale()
        {
            if (Time.timeScale == 0f)
            {
                Time.timeScale = 1f;
            }
        }

        private void ReapplyCurrentLocks()
        {
            if (CurrentMode == UiScreenMode.Lobby)
            {
                AcquireScreenLock("LobbyScreen");
            }
            else if (CurrentMode == UiScreenMode.Settlement)
            {
                AcquireScreenLock("SettlementScreen");
            }

            if (IsCommandOverlayOpen && activeLockGate != null)
            {
                overlayLockToken = activeLockGate.AcquireLock("CommandOverlayUI");
            }

            if (IsPauseMenuOpen && activeLockGate != null)
            {
                pauseLockToken = activeLockGate.AcquireLock("PauseMenuUI");
            }

            if (IsSettingsOpen && activeLockGate != null)
            {
                settingsLockToken = activeLockGate.AcquireLock("SettingsPanelUI");
            }
        }

        private void HandleLobbyStartClicked()
        {
            if (isTransitionFading || IsOnboardingBlocking) return;
            ShowPersonaSetup();
        }

        private void HandlePersonaConfirmStartClicked(EmployeePersonaSetupData persona)
        {
            if (isTransitionFading || IsOnboardingBlocking) return;

            StartFadeTransition(() =>
            {
                CurrentPersona = persona;
                EmotionStatRates demoRates = EmotionStatRates.CreateDemoDefault();
                lastEmployeeSnapshot = new EmployeeHudSnapshot(
                    persona.EmployeeName,
                    persona.PersonalityTraits,
                    StaffWorkRole.Idle,
                    "IDLE",
                    StaffMoveBadge.Idle,
                    demoRates.InitialStress,
                    demoRates.InitialFatigue,
                    persona.Profile.Loyalty);

                PersonaConfirmed?.Invoke(persona);
                StartDayInternal(1, settlementSettings.StartingBalanceWon, settlementSettings.DayDurationSeconds, playDoorChime: false);
            }, dayBannerText: "1일차");
        }

        private void StartFadeTransition(Action onMidpoint, string dayBannerText = null)
        {
            if (!isActiveAndEnabled)
            {
                onMidpoint?.Invoke();
                return;
            }

            CancelActiveFadeTransition();
            activeFadeCoroutine = StartCoroutine(RunFadeTransitionCoroutine(onMidpoint, dayBannerText));
        }

        private IEnumerator RunFadeTransitionCoroutine(Action onMidpoint, string dayBannerText)
        {
            CanvasGroup fadeGroup = EnsureTransitionFadeOverlay();
            if (fadeGroup == null)
            {
                onMidpoint?.Invoke();
                activeFadeCoroutine = null;
                yield break;
            }

            isTransitionFading = true;
            SyncTimeSystemPauseState();
            fadeGroup.transform.SetAsLastSibling();
            fadeGroup.gameObject.SetActive(true);
            fadeGroup.blocksRaycasts = true;

            if (transitionDayTitleGroup != null)
            {
                transitionDayTitleGroup.alpha = 0f;
                transitionDayTitleGroup.gameObject.SetActive(false);
            }

            // 1. 검은 화면 페이드 인 (화면 암전)
            float fadeOutDuration = Mathf.Max(0f, transitionFadeOutSeconds);
            if (fadeOutDuration > 0.001f)
            {
                float elapsed = 0f;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                    fadeGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                    yield return null;
                }
            }
            fadeGroup.alpha = 1f;

            // 2. 암전 상태에서 다음 일차·카메라·상태 전환 수행
            onMidpoint?.Invoke();
            SyncTimeSystemPauseState();

            // 3. 검은 화면 위 'N일차' 텍스트 페이드 인 → 유지 → 페이드 아웃
            if (!string.IsNullOrWhiteSpace(dayBannerText) && transitionDayTitleGroup != null && transitionDayTitleText != null)
            {
                transitionDayTitleText.text = dayBannerText;
                transitionDayTitleGroup.alpha = 0f;
                transitionDayTitleGroup.gameObject.SetActive(true);

                float titleIn = Mathf.Max(0f, dayTitleFadeInSeconds);
                if (titleIn > 0.001f)
                {
                    float elapsed = 0f;
                    while (elapsed < titleIn)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float t = Mathf.Clamp01(elapsed / titleIn);
                        transitionDayTitleGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                        yield return null;
                    }
                }
                transitionDayTitleGroup.alpha = 1f;

                float titleHold = Mathf.Max(0f, dayTitleHoldSeconds);
                if (titleHold > 0.001f)
                {
                    float elapsed = 0f;
                    while (elapsed < titleHold)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                float titleOut = Mathf.Max(0f, dayTitleFadeOutSeconds);
                if (titleOut > 0.001f)
                {
                    float elapsed = 0f;
                    while (elapsed < titleOut)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float t = Mathf.Clamp01(elapsed / titleOut);
                        transitionDayTitleGroup.alpha = Mathf.SmoothStep(1f, 0f, t);
                        yield return null;
                    }
                }

                transitionDayTitleGroup.alpha = 0f;
                transitionDayTitleGroup.gameObject.SetActive(false);
            }

            float holdDuration = Mathf.Max(0f, transitionFadeHoldSeconds);
            if (holdDuration > 0.001f)
            {
                float elapsed = 0f;
                while (elapsed < holdDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 4. 검은 화면 페이드 아웃 (편의점 화면 밝아짐)
            float fadeInDuration = Mathf.Max(0f, transitionFadeInSeconds);
            if (fadeInDuration > 0.001f)
            {
                float elapsed = 0f;
                while (elapsed < fadeInDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeInDuration);
                    fadeGroup.alpha = Mathf.SmoothStep(1f, 0f, t);
                    yield return null;
                }
            }

            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.gameObject.SetActive(false);
            isTransitionFading = false;
            activeFadeCoroutine = null;
            SyncTimeSystemPauseState();
        }

        private CanvasGroup EnsureTransitionFadeOverlay()
        {
            if (transitionFadeOverlay != null && transitionDayTitleGroup != null && transitionDayTitleText != null)
            {
                return transitionFadeOverlay;
            }

            Transform existing = transform.Find("TransitionFadeOverlay");
            GameObject overlayObj;
            if (existing != null)
            {
                overlayObj = existing.gameObject;
            }
            else
            {
                overlayObj = new GameObject("TransitionFadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                overlayObj.transform.SetParent(transform, false);
                RectTransform rt = overlayObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;

                Image bg = overlayObj.GetComponent<Image>();
                bg.color = Color.black;
                bg.raycastTarget = true;
            }

            transitionFadeOverlay = overlayObj.GetComponent<CanvasGroup>();
            if (transitionFadeOverlay == null)
            {
                transitionFadeOverlay = overlayObj.AddComponent<CanvasGroup>();
            }

            // 중앙 'N일차' 타이틀 텍스트 (2x 슈퍼샘플링으로 선명하게 표시)
            Transform titleTr = overlayObj.transform.Find("DayTitleContainer");
            GameObject titleObj;
            if (titleTr != null)
            {
                titleObj = titleTr.gameObject;
            }
            else
            {
                titleObj = new GameObject("DayTitleContainer", typeof(RectTransform), typeof(CanvasGroup), typeof(Text));
                titleObj.transform.SetParent(overlayObj.transform, false);
                RectTransform titleRt = titleObj.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.anchoredPosition = Vector2.zero;
                titleRt.sizeDelta = new Vector2(1800f, 360f);
                titleRt.localScale = new Vector3(0.5f, 0.5f, 1f);
            }

            transitionDayTitleGroup = titleObj.GetComponent<CanvasGroup>() ?? titleObj.AddComponent<CanvasGroup>();
            transitionDayTitleGroup.alpha = 0f;
            transitionDayTitleGroup.interactable = false;
            transitionDayTitleGroup.blocksRaycasts = false;

            transitionDayTitleText = titleObj.GetComponent<Text>() ?? titleObj.AddComponent<Text>();
            transitionDayTitleText.font = ResolveTransitionTitleFont();
            transitionDayTitleText.fontSize = 160;
            transitionDayTitleText.fontStyle = FontStyle.Normal;
            transitionDayTitleText.color = new Color(0.98f, 0.98f, 0.96f, 1f);
            transitionDayTitleText.alignment = TextAnchor.MiddleCenter;
            transitionDayTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            transitionDayTitleText.verticalOverflow = VerticalWrapMode.Overflow;
            transitionDayTitleText.raycastTarget = false;
            transitionDayTitleText.text = string.Empty;
            titleObj.SetActive(false);

            transitionFadeOverlay.alpha = 0f;
            transitionFadeOverlay.interactable = false;
            transitionFadeOverlay.blocksRaycasts = false;
            overlayObj.SetActive(false);
            return transitionFadeOverlay;
        }

        private FirstLaunchOnboardingController EnsureOnboardingController()
        {
            if (onboardingController == null)
            {
                onboardingController = GetComponent<FirstLaunchOnboardingController>();
                if (onboardingController == null)
                {
                    onboardingController = gameObject.AddComponent<FirstLaunchOnboardingController>();
                }
            }

            return onboardingController;
        }

        private Font ResolveTransitionTitleFont()
        {
            if (lobbyPanel != null)
            {
                Transform titleTr = lobbyPanel.transform.Find("TitleGroup/TitleText");
                Text lobbyTitle = titleTr != null ? titleTr.GetComponent<Text>() : null;
                if (lobbyTitle != null && lobbyTitle.font != null)
                {
                    return lobbyTitle.font;
                }
            }

            Text anyText = GetComponentInChildren<Text>(true);
            if (anyText != null && anyText.font != null)
            {
                return anyText.font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void CancelActiveFadeTransition()
        {
            if (activeFadeCoroutine != null)
            {
                StopCoroutine(activeFadeCoroutine);
                activeFadeCoroutine = null;
            }

            isTransitionFading = false;
            if (transitionDayTitleGroup != null)
            {
                transitionDayTitleGroup.alpha = 0f;
                transitionDayTitleGroup.gameObject.SetActive(false);
            }
            if (transitionFadeOverlay != null)
            {
                transitionFadeOverlay.alpha = 0f;
                transitionFadeOverlay.blocksRaycasts = false;
                transitionFadeOverlay.gameObject.SetActive(false);
            }
            SyncTimeSystemPauseState();
        }

        private void HandleNextDayClicked()
        {
            if (isTransitionFading) return;
            int nextDay = Mathf.Max(2, lastSettlementResult.DayNumber + 1);
            int carryBalance = Mathf.Max(10000, lastSettlementResult.FinalBalanceWon);

            StartFadeTransition(() =>
            {
                StartDayInternal(nextDay, carryBalance, settlementSettings.DayDurationSeconds, playDoorChime: true);
            }, dayBannerText: $"{nextDay}일차");
        }

        private void HandleRetryDayClicked()
        {
            if (isTransitionFading) return;

            StartFadeTransition(() =>
            {
                StartDayInternal(1, settlementSettings.StartingBalanceWon, settlementSettings.DayDurationSeconds, playDoorChime: true);
            }, dayBannerText: "1일차");
        }

        private void HandleLobbyQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleOverlayCommandSubmitted(string commandText)
        {
            CloseCommandOverlay();
            TextCommandSubmitted?.Invoke(commandText);
        }

        private void HandleOverlayCancelClicked()
        {
            CloseCommandOverlay();
            CancelWorkRequested?.Invoke();
        }
    }
}
