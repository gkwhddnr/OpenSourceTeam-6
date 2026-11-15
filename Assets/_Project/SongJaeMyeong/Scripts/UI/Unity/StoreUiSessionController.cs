using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.UI.Unity
{
    public sealed class StoreUiSessionController : MonoBehaviour, IUISystemPresenter
    {
        [Header("Panels")]
        [SerializeField] private LobbyPanelPresenter lobbyPanel;
        [SerializeField] private InGameHudPresenter inGameHud;
        [SerializeField] private CommandOverlayPresenter commandOverlay;
        [SerializeField] private SettlementPanelPresenter settlementPanel;
        [SerializeField] private PausePanelPresenter pausePanel;
        [SerializeField] private SettingsPanelPresenter settingsPanel;

        [Header("Optional World-Space NPC Status UI (UI-09)")]
        [SerializeField] private EmployeeWorldStatusPresenter worldStatusPresenter;

        [Header("Audio System (SND-00 ~ SND-03)")]
        [SerializeField] private StoreAudioManager audioManager;

        [Header("Cursor Lock Gate (External Adapter or Standalone Fallback)")]
        [SerializeField] private MonoBehaviour externalCursorLockGateComponent;
        [SerializeField] private StandaloneCursorLockGate standaloneFallbackGate;

        [Header("Settlement & Day Settings (1차 데모 120초 규격 기본값)")]
        [SerializeField] private DaySettlementSettings settlementSettings = DaySettlementSettings.CreateDemoPreset();

        private DaySettlementSession settlementSession;
        private ICursorLockGate activeLockGate;
        private IStoreAudioPlayer activeAudioPlayer;
        private IDisposable screenLockToken;
        private IDisposable overlayLockToken;
        private IDisposable pauseLockToken;
        private IDisposable settingsLockToken;

        private float remainingDaySeconds;
        private EmployeeHudSnapshot lastEmployeeSnapshot;
        private VoiceProcessStage lastVoiceStage = VoiceProcessStage.Idle;

        public UiScreenMode CurrentMode { get; private set; } = UiScreenMode.Lobby;
        public IStoreAudioPlayer AudioPlayer => activeAudioPlayer;
        public bool IsCommandOverlayOpen => commandOverlay != null && commandOverlay.IsOpen;
        public bool IsPauseMenuOpen => pausePanel != null && pausePanel.IsOpen;
        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.IsOpen;
        public float RemainingDaySeconds => remainingDaySeconds;

        public event Action<string> TextCommandSubmitted;
        public event Action CancelWorkRequested;
        public event Action VoiceRecordToggleRequested;
        public event Action DayStarted;

        private void Awake()
        {
            settlementSession = new DaySettlementSession(settlementSettings);
            ResolveCursorLockGate();
            ResolveAudioPlayer();
            EmotionStatRates demoRates = EmotionStatRates.CreateDemoDefault();
            lastEmployeeSnapshot = new EmployeeHudSnapshot(
                "김민수",
                "성실함",
                StaffWorkRole.Idle,
                "IDLE",
                StaffMoveBadge.Idle,
                demoRates.InitialStress,
                demoRates.InitialFatigue,
                demoRates.InitialLoyalty);
        }

        private void OnEnable()
        {
            ResolveCursorLockGate();
            ResolveAudioPlayer();
            if (settingsPanel != null)
            {
                settingsPanel.BindAudioPlayer(activeAudioPlayer);
            }

            if (lobbyPanel != null)
            {
                lobbyPanel.StartClicked += HandleLobbyStartClicked;
                lobbyPanel.SettingsClicked += OpenSettingsPanel;
                lobbyPanel.QuitClicked += HandleLobbyQuitClicked;
            }

            if (inGameHud != null)
            {
                inGameHud.OpenCommandOverlayClicked += ToggleCommandOverlay;
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
            }

            if (settlementPanel != null)
            {
                settlementPanel.RetryDayClicked += HandleRetryDayClicked;
                settlementPanel.ReturnLobbyClicked += ShowLobby;
            }

            ShowLobby();
        }

        private void OnDisable()
        {
            if (lobbyPanel != null)
            {
                lobbyPanel.StartClicked -= HandleLobbyStartClicked;
                lobbyPanel.SettingsClicked -= OpenSettingsPanel;
                lobbyPanel.QuitClicked -= HandleLobbyQuitClicked;
            }

            if (inGameHud != null)
            {
                inGameHud.OpenCommandOverlayClicked -= ToggleCommandOverlay;
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
            }

            if (settlementPanel != null)
            {
                settlementPanel.RetryDayClicked -= HandleRetryDayClicked;
                settlementPanel.ReturnLobbyClicked -= ShowLobby;
            }

            RestoreTimeScale();
            ReleaseSettingsLock();
            ReleasePauseLock();
            ReleaseOverlayLock();
            ReleaseScreenLock();
        }

        private void Update()
        {
            HandleGlobalEscapeHotkey();

            if (CurrentMode != UiScreenMode.InGame || IsPauseMenuOpen || IsSettingsOpen) return;

            HandleInGameHotkeys();

            if (remainingDaySeconds > 0f)
            {
                remainingDaySeconds = Mathf.Max(0f, remainingDaySeconds - Time.deltaTime);
                RefreshTopBar();

                if (remainingDaySeconds <= 0f)
                {
                    CompleteDayAndShowSettlement(lastEmployeeSnapshot.Stress);
                }
            }
        }

        public void SetWorldStatusPresenter(EmployeeWorldStatusPresenter presenter)
        {
            worldStatusPresenter = presenter;
            if (worldStatusPresenter != null)
            {
                worldStatusPresenter.RenderSnapshot(lastEmployeeSnapshot);
            }
        }

        public void SetAudioPlayer(IStoreAudioPlayer player)
        {
            activeAudioPlayer = player;
            if (settingsPanel != null)
            {
                settingsPanel.BindAudioPlayer(activeAudioPlayer);
            }

            SyncBgmForCurrentState();
        }

        public void PlaySfx(StoreSfxId sfxId, float volumeScale = 1f)
        {
            activeAudioPlayer?.PlaySfx(sfxId, volumeScale);
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

            if (lobbyPanel != null) lobbyPanel.SetVisible(true);
            if (inGameHud != null) inGameHud.SetVisible(false);
            if (settlementPanel != null) settlementPanel.SetVisible(false);

            SyncBgmForCurrentState();
            AcquireScreenLock("LobbyScreen");
        }

        public void StartDay(int dayNumber, int startingBalanceWon, float durationSeconds)
        {
            if (settlementSession == null)
            {
                settlementSession = new DaySettlementSession(settlementSettings);
            }

            settlementSession.Reset(dayNumber, startingBalanceWon);
            remainingDaySeconds = durationSeconds > 0f ? durationSeconds : settlementSettings.DayDurationSeconds;

            CloseSettingsPanel();
            ClosePauseMenu();
            CloseCommandOverlay();
            ReleaseScreenLock();
            RestoreTimeScale();

            CurrentMode = UiScreenMode.InGame;
            lastVoiceStage = VoiceProcessStage.Idle;
            if (lobbyPanel != null) lobbyPanel.SetVisible(false);
            if (settlementPanel != null) settlementPanel.SetVisible(false);
            if (inGameHud != null)
            {
                inGameHud.SetVisible(true);
                inGameHud.RenderEmployee(lastEmployeeSnapshot);
                inGameHud.RenderVoiceStage(VoiceProcessStage.Idle, string.Empty);
                inGameHud.RenderFeedback(new VoiceCommandFeedback(
                    CommandFeedbackKind.Info,
                    string.Empty,
                    string.Empty,
                    "[V] 키를 눌러 음성으로 지시하거나 [Tab] 키로 텍스트 명령창을 여세요."));
            }

            if (worldStatusPresenter != null)
            {
                worldStatusPresenter.RenderSnapshot(lastEmployeeSnapshot);
                worldStatusPresenter.ShowSpeechBubble(string.Empty, CommandFeedbackKind.Info);
            }

            RefreshTopBar();
            activeAudioPlayer?.PlaySfx(StoreSfxId.StoreDoorChime);
            SyncBgmForCurrentState();
            DayStarted?.Invoke();
        }

        public void UpdateEmployeeHud(EmployeeHudSnapshot snapshot)
        {
            StaffMoveBadge previousBadge = lastEmployeeSnapshot.MoveBadge;
            lastEmployeeSnapshot = snapshot;
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

        public DaySettlementResult CompleteDayAndShowSettlement(float endingStress)
        {
            if (settlementSession == null)
            {
                settlementSession = new DaySettlementSession(settlementSettings);
            }

            CloseSettingsPanel();
            ClosePauseMenu();
            CloseCommandOverlay();
            RestoreTimeScale();

            DaySettlementResult result = settlementSession.SettleOnce(endingStress);
            CurrentMode = UiScreenMode.Settlement;

            if (lobbyPanel != null) lobbyPanel.SetVisible(false);
            if (inGameHud != null) inGameHud.SetVisible(false);
            if (settlementPanel != null)
            {
                settlementPanel.SetVisible(true);
                settlementPanel.RenderResult(result);
            }

            activeAudioPlayer?.PlaySfx(StoreSfxId.PaperFlip);
            SyncBgmForCurrentState();
            AcquireScreenLock("SettlementScreen");
            return result;
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
            if (overlayLockToken == null && activeLockGate != null)
            {
                overlayLockToken = activeLockGate.AcquireLock("CommandOverlayUI");
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
                activeAudioPlayer?.PlaySfx(StoreSfxId.UiPopupOpenClose);
            }
            Time.timeScale = 0f;
            if (pauseLockToken == null && activeLockGate != null)
            {
                pauseLockToken = activeLockGate.AcquireLock("PauseMenuUI");
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
            if (settingsLockToken == null && activeLockGate != null)
            {
                settingsLockToken = activeLockGate.AcquireLock("SettingsPanelUI");
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
        }

        private void HandleGlobalEscapeHotkey()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            if (IsSettingsOpen)
            {
                CloseSettingsPanel();
                return;
            }

            if (CurrentMode != UiScreenMode.InGame) return;

            if (IsCommandOverlayOpen)
            {
                CloseCommandOverlay();
                return;
            }

            if (pausePanel != null)
            {
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
        }

        private void RefreshTopBar()
        {
            if (inGameHud == null || settlementSession == null) return;
            int liveEstimatedBalance = settlementSession.StartingBalanceWon + settlementSession.EstimateLiveGrossSalesWon();
            inGameHud.UpdateTopBar(
                settlementSession.DayNumber,
                remainingDaySeconds,
                liveEstimatedBalance,
                settlementSession.TargetBalanceWon);
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
            StoreBgmTrack targetTrack = StoreAudioRules.ResolveBgmForState(CurrentMode, lastEmployeeSnapshot.Stress);
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
            if (activeLockGate != null)
            {
                screenLockToken = activeLockGate.AcquireLock(reason);
            }
        }

        private void ReleaseScreenLock()
        {
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
            StartDay(1, settlementSettings.StartingBalanceWon, settlementSettings.DayDurationSeconds);
        }

        private void HandleRetryDayClicked()
        {
            StartDay(1, settlementSettings.StartingBalanceWon, settlementSettings.DayDurationSeconds);
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
