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

        [Header("Cursor Lock Gate (External Adapter or Standalone Fallback)")]
        [SerializeField] private MonoBehaviour externalCursorLockGateComponent;
        [SerializeField] private StandaloneCursorLockGate standaloneFallbackGate;

        [Header("Settlement & Day Settings")]
        [SerializeField] private DaySettlementSettings settlementSettings = new DaySettlementSettings
        {
            DayDurationSeconds = 180f,
            StartingBalanceWon = 50000,
            TargetBalanceWon = 80000,
            CheckoutWonPerEffectiveSecond = 800f,
            RestockWonPerEffectiveSecond = 500f,
            MinEffectiveRestockSeconds = 25f,
            RestockShortageMultiplier = 0.8f,
            CostOfGoodsRatio = 0.3f,
            FixedOperatingCostWon = 25000,
            AngerStressThreshold = 80f,
            MaxAllowedAngerRefusals = 2
        };

        private DaySettlementSession settlementSession;
        private ICursorLockGate activeLockGate;
        private IDisposable screenLockToken;
        private IDisposable overlayLockToken;

        private float remainingDaySeconds;
        private EmployeeHudSnapshot lastEmployeeSnapshot;

        public UiScreenMode CurrentMode { get; private set; } = UiScreenMode.Lobby;
        public bool IsCommandOverlayOpen => commandOverlay != null && commandOverlay.IsOpen;
        public float RemainingDaySeconds => remainingDaySeconds;

        public event Action<string> TextCommandSubmitted;
        public event Action CancelWorkRequested;
        public event Action DayStarted;

        private void Awake()
        {
            settlementSession = new DaySettlementSession(settlementSettings);
            ResolveCursorLockGate();
            lastEmployeeSnapshot = new EmployeeHudSnapshot(
                "김민수",
                "성실함",
                StaffWorkRole.Idle,
                "IDLE",
                StaffMoveBadge.Idle,
                20f,
                10f,
                75f);
        }

        private void OnEnable()
        {
            ResolveCursorLockGate();

            if (lobbyPanel != null)
            {
                lobbyPanel.StartClicked += HandleLobbyStartClicked;
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

            if (settlementPanel != null)
            {
                settlementPanel.RetryDayClicked -= HandleRetryDayClicked;
                settlementPanel.ReturnLobbyClicked -= ShowLobby;
            }

            ReleaseOverlayLock();
            ReleaseScreenLock();
        }

        private void Update()
        {
            if (CurrentMode != UiScreenMode.InGame) return;

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

        public void SetExternalCursorLockGate(ICursorLockGate externalGate)
        {
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
            CloseCommandOverlay();
            CurrentMode = UiScreenMode.Lobby;

            if (lobbyPanel != null) lobbyPanel.SetVisible(true);
            if (inGameHud != null) inGameHud.SetVisible(false);
            if (settlementPanel != null) settlementPanel.SetVisible(false);

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

            CloseCommandOverlay();
            ReleaseScreenLock();

            CurrentMode = UiScreenMode.InGame;
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
                    "마이크로 업무를 지시하거나 [Tab] 키를 눌러 텍스트 명령창을 여세요."));
            }

            RefreshTopBar();
            DayStarted?.Invoke();
        }

        public void UpdateEmployeeHud(EmployeeHudSnapshot snapshot)
        {
            lastEmployeeSnapshot = snapshot;
            if (inGameHud != null)
            {
                inGameHud.RenderEmployee(snapshot);
            }
        }

        public void PushVoiceStage(VoiceProcessStage stage, string recognizedTranscript)
        {
            if (inGameHud != null)
            {
                inGameHud.RenderVoiceStage(stage, recognizedTranscript);
            }
        }

        public void PushCommandFeedback(VoiceCommandFeedback feedback)
        {
            if (inGameHud != null)
            {
                inGameHud.RenderFeedback(feedback);
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

            CloseCommandOverlay();
            DaySettlementResult result = settlementSession.SettleOnce(endingStress);
            CurrentMode = UiScreenMode.Settlement;

            if (lobbyPanel != null) lobbyPanel.SetVisible(false);
            if (inGameHud != null) inGameHud.SetVisible(false);
            if (settlementPanel != null)
            {
                settlementPanel.SetVisible(true);
                settlementPanel.RenderResult(result);
            }

            AcquireScreenLock("SettlementScreen");
            return result;
        }

        public void ToggleCommandOverlay()
        {
            if (CurrentMode != UiScreenMode.InGame || commandOverlay == null) return;
            if (commandOverlay.IsOpen) CloseCommandOverlay();
            else OpenCommandOverlay();
        }

        public void OpenCommandOverlay()
        {
            if (CurrentMode != UiScreenMode.InGame || commandOverlay == null) return;
            commandOverlay.SetOpen(true);
            if (overlayLockToken == null && activeLockGate != null)
            {
                overlayLockToken = activeLockGate.AcquireLock("CommandOverlayUI");
            }
        }

        public void CloseCommandOverlay()
        {
            if (commandOverlay != null)
            {
                commandOverlay.SetOpen(false);
            }
            ReleaseOverlayLock();
        }

        private void HandleInGameHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame)
            {
                ToggleCommandOverlay();
            }
            else if (kb.escapeKey.wasPressedThisFrame && IsCommandOverlayOpen)
            {
                CloseCommandOverlay();
            }
        }

        private void RefreshTopBar()
        {
            if (inGameHud == null || settlementSession == null) return;
            int liveEstimatedBalance = settlementSession.StartingBalanceWon + settlementSession.EstimateLiveGrossSalesWon();
            inGameHud.UpdateTopBar(settlementSession.DayNumber, remainingDaySeconds, liveEstimatedBalance);
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
            else if (IsCommandOverlayOpen && activeLockGate != null)
            {
                overlayLockToken = activeLockGate.AcquireLock("CommandOverlayUI");
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
