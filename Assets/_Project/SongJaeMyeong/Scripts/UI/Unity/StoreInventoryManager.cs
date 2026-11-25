using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    using Camera = UnityEngine.Camera;
    using Time = UnityEngine.Time;

    /// <summary>
    /// 편의점 54종 물품의 단가·현재 재고·판매 개수·총 매출을 관리하며,
    /// 손님 NPC(NpcPopulationController)의 매대 픽업(재고 -1) → 계산대 대기 →
    /// 플레이어 포스기 직접 상호작용([E] 키) 또는 알바 계산대 배치(StaffWorkRole.Checkout + Arrived) 시
    /// 결제 확정·매출 반영·판매 로그 파일 저장·손님 퇴장을 연동한다.
    /// </summary>
    public sealed class StoreInventoryManager : MonoBehaviour
    {
        public const string DefaultSalesLogFileName = "store_sales_log.txt";

        [Serializable]
        public struct StockSeedEntry
        {
            public string itemId;
            public int initialStock;
        }

        [Header("연동 대상")]
        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private NpcPopulationController npcPopulation;

        [Header("포스기 상호작용 · 알바 자동 계산 설정")]
        [SerializeField] private float playerPosInteractDistance = 2.0f;
        [SerializeField] private float staffAutoCheckoutSeconds = 1.4f;

        [Header("초기 진열 재고 (맵 배치 개수 자동 기록)")]
        [SerializeField] private List<StockSeedEntry> initialStockSeeds = new List<StockSeedEntry>();

        [Header("실시간 재고 · 매출 현황 (읽기 확인용)")]
        [SerializeField] private int todayUnitsSold;
        [SerializeField] private int todayRevenueWon;
        [SerializeField] private int cumulativeRevenueWon;
        [SerializeField] private int totalRemainingStock;
        [SerializeField] private int waitingCheckoutCustomerId = -1;
        [SerializeField] private string waitingCheckoutProductTag = string.Empty;
        [SerializeField] private List<StoreItemRecord> liveItemsSnapshot = new List<StoreItemRecord>();

        private StoreInventoryLedger ledger;
        private readonly Dictionary<int, string> pendingCustomerPickedItemIds = new Dictionary<int, string>();
        private readonly Dictionary<int, GameObject> customerHiddenShelfVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<string, List<GameObject>> shelfVisualsByItemId = new Dictionary<string, List<GameObject>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<StoreItemCategory, List<GameObject>> shelfVisualsByCategory = new Dictionary<StoreItemCategory, List<GameObject>>();
        private readonly Dictionary<StoreItemCategory, Transform> runtimeShelfPickupPoints = new Dictionary<StoreItemCategory, Transform>();
        private bool isShelfVisualsIndexed;
        private bool isNpcProductsConfigured;
        private int saleRandomSeed = 101;
        private bool isNpcSubscribed;
        private float staffCheckoutTimer;
        private int lastWaitingCustomerId = -1;
        private Camera playerCamera;
        private Transform cashRegisterTransform;
        private Transform counterMonitorTransform;
        private Transform secondaryCashRegisterTransform;
        private Transform secondaryCounterMonitorTransform;
        private Transform manualCubeTransform;
        private Text secondaryMonitorStatusText;
        private GameObject carriedTemplatesRoot;
        private readonly Dictionary<StoreItemCategory, GameObject> carriedTemplateByCategory = new Dictionary<StoreItemCategory, GameObject>();
        private bool? lastQueueAssignedToEmployeeLane;
        private GameObject posPromptBadgeObj;
        private Text posPromptBadgeText;
        private int displayedTodayRevenueWon;
        private int displayedTodayUnitsSold;
        private Coroutine checkoutSoundAndRollCoroutine;

        public StoreInventoryLedger Ledger
        {
            get
            {
                EnsureInitialized();
                return ledger;
            }
        }

        public int TodayRevenueWon => Ledger.TodayRevenueWon;
        public int DisplayedTodayRevenueWon
        {
            get
            {
                EnsureInitialized();
                return displayedTodayRevenueWon;
            }
        }
        public int CumulativeRevenueWon => Ledger.CumulativeRevenueWon;
        public int TodayUnitsSold => Ledger.TodayUnitsSold;
        public int DisplayedTodayUnitsSold
        {
            get
            {
                EnsureInitialized();
                return displayedTodayUnitsSold;
            }
        }
        public int TotalRemainingStock => Ledger.TotalCurrentStock;
        public string SalesLogFilePath => Path.Combine(Application.persistentDataPath, DefaultSalesLogFileName);

        public event Action<StoreItemRecord, int> ItemSold;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            BindNpcPopulationEvents();
        }

        private void Start()
        {
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<StoreUiSessionController>();
            }

            if (npcPopulation == null)
            {
                npcPopulation = FindAnyObjectByType<NpcPopulationController>();
            }

            BindNpcPopulationEvents();
            ResolveCounterObjects();
            IndexSceneShelfVisuals(syncLedgerCounts: true);
            EnsureNpcShelfProductsConfigured();
            SyncInspectorMirror();
            SaveSalesLogToDisk();
        }

        private void OnDisable()
        {
            UnbindNpcPopulationEvents();
            SetPosPromptVisible(false, string.Empty);
        }

        private void Update()
        {
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<StoreUiSessionController>();
                if (uiController == null) return;
            }

            if (npcPopulation == null)
            {
                npcPopulation = FindAnyObjectByType<NpcPopulationController>();
                if (npcPopulation != null)
                {
                    BindNpcPopulationEvents();
                    EnsureNpcShelfProductsConfigured();
                }
            }

            bool isPlayableInGame = uiController.CurrentMode == UiScreenMode.InGame &&
                                    !uiController.IsPauseMenuOpen &&
                                    !uiController.IsSettingsOpen &&
                                    !uiController.IsTransitionFading;

            if (npcPopulation != null)
            {
                if (!npcPopulation.WaitForExternalCheckout)
                {
                    npcPopulation.WaitForExternalCheckout = true;
                }

                if (npcPopulation.IsPaused != !isPlayableInGame)
                {
                    npcPopulation.SetPaused(!isPlayableInGame);
                }
            }

            if (!isPlayableInGame)
            {
                SetPosPromptVisible(false, string.Empty);
                return;
            }

            UpdateCheckoutLoop(Time.deltaTime);
        }

        private void BindNpcPopulationEvents()
        {
            if (npcPopulation == null || isNpcSubscribed) return;

            npcPopulation.WaitForExternalCheckout = true;
            npcPopulation.CanTakeFromShelf = CanCustomerTakeTagFromShelf;
            npcPopulation.ProductPickedUp -= HandleCustomerProductPickedUp;
            npcPopulation.ProductPickedUp += HandleCustomerProductPickedUp;
            isNpcSubscribed = true;
        }

        private void UnbindNpcPopulationEvents()
        {
            if (npcPopulation == null || !isNpcSubscribed) return;

            if (npcPopulation.CanTakeFromShelf == CanCustomerTakeTagFromShelf)
            {
                npcPopulation.CanTakeFromShelf = null;
            }
            npcPopulation.ProductPickedUp -= HandleCustomerProductPickedUp;
            isNpcSubscribed = false;
        }

        private bool CanCustomerTakeTagFromShelf(string productTag)
        {
            EnsureInitialized();
            return ledger.HasStockForTag(productTag);
        }

        private void HandleCustomerProductPickedUp(int customerId, string productTag)
        {
            EnsureInitialized();
            if (!isShelfVisualsIndexed)
            {
                IndexSceneShelfVisuals(syncLedgerCounts: false);
            }

            saleRandomSeed = unchecked(saleRandomSeed * 1103515245 + 12345 + customerId * 31);

            if (pendingCustomerPickedItemIds.TryGetValue(customerId, out string oldReservedId))
            {
                ledger.ReturnPickedItemToShelf(oldReservedId);
                pendingCustomerPickedItemIds.Remove(customerId);
            }

            if (customerHiddenShelfVisuals.TryGetValue(customerId, out GameObject prevHiddenObj))
            {
                if (prevHiddenObj != null)
                {
                    prevHiddenObj.SetActive(true);
                }
                customerHiddenShelfVisuals.Remove(customerId);
            }

            if (ledger.TryPickupFromShelfByTag(productTag, saleRandomSeed, out StoreItemRecord picked))
            {
                pendingCustomerPickedItemIds[customerId] = picked.ItemId;
                GameObject hiddenVisual = HideOneShelfVisual(picked.ItemId, picked.Category);
                if (hiddenVisual != null)
                {
                    customerHiddenShelfVisuals[customerId] = hiddenVisual;
                }
                SyncInspectorMirror();
                uiController?.AudioPlayer?.PlaySfx(StoreSfxId.ItemPickupOrPlace);
            }
        }

        private void UpdateCheckoutLoop(float deltaTime)
        {
            if (npcPopulation == null)
            {
                SetPosPromptVisible(false, string.Empty);
                return;
            }

            bool hasWaitingCustomer = npcPopulation.TryGetCurrentCheckout(out int customerId, out string productTag);
            waitingCheckoutCustomerId = hasWaitingCustomer ? customerId : -1;
            waitingCheckoutProductTag = hasWaitingCustomer ? (productTag ?? string.Empty) : string.Empty;

            if (!hasWaitingCustomer)
            {
                lastWaitingCustomerId = -1;
                staffCheckoutTimer = 0f;
            }
            else if (customerId != lastWaitingCustomerId)
            {
                lastWaitingCustomerId = customerId;
                staffCheckoutTimer = 0f;
            }

            // 1. 알바생이 계산대(StaffWorkRole.Checkout)에 도착(Arrived)해 있으면 좌측(알바 전용) 계산대에서 자동 결제 처리
            bool isStaffStationedAtCheckout =
                uiController.CurrentEmployeeRole == StaffWorkRole.Checkout &&
                uiController.CurrentEmployeeMoveBadge == StaffMoveBadge.Arrived;

            SyncCheckoutCounterLane(isStaffStationedAtCheckout);

            if (hasWaitingCustomer && isStaffStationedAtCheckout)
            {
                float efficiency = StressRules.GetEfficiencyMultiplier(uiController.CurrentEmployeeStress);
                staffCheckoutTimer += deltaTime * Mathf.Max(0.35f, efficiency);
                if (staffCheckoutTimer >= Mathf.Max(0.4f, staffAutoCheckoutSeconds))
                {
                    staffCheckoutTimer = 0f;
                    TryProcessWaitingCheckout(isPlayerAction: false);
                    hasWaitingCustomer = npcPopulation.TryGetCurrentCheckout(out customerId, out productTag);
                }
            }

            // 2. 플레이어가 우측(점장 전용) 또는 좌측(알바 전용) 포스기를 바라볼 때 안내 · [E] 직접 결제 처리
            if (uiController.IsCommandOverlayOpen || uiController.IsExternalModalOpen)
            {
                SetPosPromptVisible(false, string.Empty);
                return;
            }

            ResolveCounterObjects();
            bool lookingAtPlayerPos = IsPlayerLookingAtCounter(cashRegisterTransform, counterMonitorTransform, hasWaitingCustomer);
            if (lookingAtPlayerPos)
            {
                if (isStaffStationedAtCheckout)
                {
                    SetPosPromptVisible(true, "[점장 전용 계산대]  현재 좌측 알바 전용 계산대에서 알바가 응대 중입니다");
                    return;
                }

                if (hasWaitingCustomer)
                {
                    StoreItemRecord previewItem = PeekWaitingCustomerItem(customerId, productTag);
                    string itemLabel = previewItem != null
                        ? $"{previewItem.DisplayName} (+{previewItem.UnitPriceWon:N0}원)"
                        : $"{productTag}";
                    SetPosPromptVisible(true, $"[E]  점장 전용 포스기 결제하기  ·  {itemLabel}");

                    Keyboard kb = Keyboard.current;
                    if (kb != null && kb.eKey.wasPressedThisFrame)
                    {
                        TryProcessWaitingCheckout(isPlayerAction: true);
                    }
                }
                else
                {
                    string waitMsg = npcPopulation != null && npcPopulation.ActiveCustomers > 0
                        ? "[점장 전용 계산대]  손님이 물건을 고르거나 이동 중입니다"
                        : "[점장 전용 계산대]  현재 계산 대기 중인 손님이 없습니다";
                    SetPosPromptVisible(true, waitMsg);
                }
                return;
            }

            bool lookingAtStaffPos = IsPlayerLookingAtCounter(secondaryCashRegisterTransform, secondaryCounterMonitorTransform, hasWaitingCustomer);
            if (lookingAtStaffPos)
            {
                string staffPosMsg = isStaffStationedAtCheckout
                    ? "[알바 전용 계산대]  알바가 손님을 자동 계산 중입니다"
                    : "[알바 전용 계산대]  알바에게 '계산대 봐줘'를 지시하세요 (직접 계산은 우측 계산대)";
                SetPosPromptVisible(true, staffPosMsg);
                return;
            }

            SetPosPromptVisible(false, string.Empty);
        }

        private StoreItemRecord PeekWaitingCustomerItem(int customerId, string productTag)
        {
            EnsureInitialized();
            if (pendingCustomerPickedItemIds.TryGetValue(customerId, out string reservedItemId) &&
                ledger.TryGetItem(reservedItemId, out StoreItemRecord rec))
            {
                return rec;
            }

            if (StoreInventoryCatalog.TryParseCategoryTag(productTag, out StoreItemCategory cat))
            {
                IReadOnlyList<StoreItemRecord> all = ledger.Items;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Category == cat) return all[i];
                }
            }

            return null;
        }

        public bool TryProcessWaitingCheckout(bool isPlayerAction)
        {
            EnsureInitialized();
            if (npcPopulation == null) return false;
            if (!npcPopulation.TryGetCurrentCheckout(out int waitingId, out string productTag))
            {
                return false;
            }

            if (!npcPopulation.TryCompleteCurrentCheckout(out int approvedCustomerId) || approvedCustomerId < 0)
            {
                return false;
            }

            string targetItemId = null;
            if (pendingCustomerPickedItemIds.TryGetValue(approvedCustomerId, out string reservedId))
            {
                targetItemId = reservedId;
                pendingCustomerPickedItemIds.Remove(approvedCustomerId);
                customerHiddenShelfVisuals.Remove(approvedCustomerId);
            }
            else
            {
                saleRandomSeed = unchecked(saleRandomSeed * 1103515245 + 12345 + approvedCustomerId);
                if (ledger.TryPickupFromShelfByTag(productTag, saleRandomSeed, out StoreItemRecord fallbackPicked))
                {
                    targetItemId = fallbackPicked.ItemId;
                    HideOneShelfVisual(fallbackPicked.ItemId, fallbackPicked.Category);
                }
            }

            if (string.IsNullOrEmpty(targetItemId))
            {
                return false;
            }

            int dayNum = uiController != null ? Mathf.Max(1, uiController.CurrentDayNumber) : 1;
            string clockText = uiController != null
                ? StoreDayTimeRules.FormatGameClockStepped(uiController.CurrentGameHour, 10)
                : "08:00";
            string actorCode = isPlayerAction ? "PLAYER" : "STAFF";

            int startDisplayedRevenueWon = displayedTodayRevenueWon;

            if (!ledger.CommitPickedItemCheckout(
                    targetItemId,
                    dayNum,
                    approvedCustomerId,
                    actorCode,
                    clockText,
                    out StoreItemRecord soldItem,
                    out int revenueWon))
            {
                return false;
            }

            SyncInspectorMirror();
            SaveSalesLogToDisk();

            ItemSold?.Invoke(soldItem, revenueWon);
            TriggerCheckoutAudioAndRevenueRoll(
                startDisplayedRevenueWon,
                ledger.TodayRevenueWon,
                ledger.TodayUnitsSold);

            string actorLabel = isPlayerAction ? "점장 직접 계산[E]" : "알바 계산 완료";
            uiController?.PushCommandFeedback(
                new VoiceCommandFeedback(
                    CommandFeedbackKind.Accepted,
                    string.Empty,
                    $"POS_CHECKOUT (+{revenueWon:N0}원)",
                    $"[{actorLabel}] 손님#{approvedCustomerId} · [{soldItem.ProductTag}] {soldItem.DisplayName} +{revenueWon:N0}원 결제 완료! (당일 매출 {ledger.TodayRevenueWon:N0}원)"),
                playFeedbackSfx: false);

            if (!isPlayerAction)
            {
                uiController?.NotifyEmployeeTaskCompleted(StaffWorkRole.Checkout);
            }

            return true;
        }

        private void TriggerCheckoutAudioAndRevenueRoll(
            int startRevenueWon,
            int targetRevenueWon,
            int targetUnitsSold)
        {
            // 1단계: 바코드 스캔 '삑!' 효과음 즉시 재생
            uiController?.AudioPlayer?.PlaySfx(StoreSfxId.BarcodeCheckout);

            if (!isActiveAndEnabled)
            {
                displayedTodayRevenueWon = targetRevenueWon;
                displayedTodayUnitsSold = targetUnitsSold;
                uiController?.NotifyInventoryRevenueChanged();
                return;
            }

            if (checkoutSoundAndRollCoroutine != null)
            {
                StopCoroutine(checkoutSoundAndRollCoroutine);
                checkoutSoundAndRollCoroutine = null;
            }

            checkoutSoundAndRollCoroutine = StartCoroutine(
                RunCheckoutSoundAndRevenueRollCoroutine(startRevenueWon, targetRevenueWon, targetUnitsSold));
        }

        private IEnumerator RunCheckoutSoundAndRevenueRollCoroutine(
            int startRevenueWon,
            int targetRevenueWon,
            int targetUnitsSold)
        {
            // 1단계('삑!' 소리) 직후 대기 -> 2단계('클릭' 소리) 재생
            const float delayBeforeClick = 0.28f;
            float elapsed = 0f;
            while (elapsed < delayBeforeClick)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            uiController?.AudioPlayer?.PlaySfx(StoreSfxId.CheckoutClick);

            // 2단계('클릭' 소리) 직후 대기 -> 3단계('코인' 소리) 재생 + 매출 숫자 카운팅 시작
            const float delayBeforeCoin = 0.22f;
            elapsed = 0f;
            while (elapsed < delayBeforeCoin)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            uiController?.AudioPlayer?.PlaySfx(StoreSfxId.CheckoutCoin);
            displayedTodayUnitsSold = targetUnitsSold;

            // 코인 소리가 울리는 동안(약 0.68초) 매출 숫자가 부드럽게 카운팅 상승
            const float coinRollDuration = 0.68f;
            elapsed = 0f;
            int lastNotifiedRevenue = displayedTodayRevenueWon;
            uiController?.NotifyInventoryRevenueChanged();

            while (elapsed < coinRollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / coinRollDuration);
                float eased = 1f - Mathf.Pow(1f - t, 2.4f);
                int interpolated = Mathf.RoundToInt(Mathf.Lerp(startRevenueWon, targetRevenueWon, eased) / 10f) * 10;
                interpolated = Mathf.Clamp(interpolated, Mathf.Min(startRevenueWon, targetRevenueWon), Mathf.Max(startRevenueWon, targetRevenueWon));

                if (interpolated != lastNotifiedRevenue)
                {
                    displayedTodayRevenueWon = interpolated;
                    lastNotifiedRevenue = interpolated;
                    uiController?.NotifyInventoryRevenueChanged();
                }

                yield return null;
            }

            displayedTodayRevenueWon = ledger != null ? ledger.TodayRevenueWon : targetRevenueWon;
            displayedTodayUnitsSold = ledger != null ? ledger.TodayUnitsSold : targetUnitsSold;
            uiController?.NotifyInventoryRevenueChanged();
            checkoutSoundAndRollCoroutine = null;
        }

        private void ResolveCounterObjects()
        {
            if (playerCamera == null ||
                !playerCamera.isActiveAndEnabled ||
                playerCamera.gameObject.name == "LobbyOverviewCamera" ||
                playerCamera.gameObject.name == "PosSettlementCamera")
            {
                Camera mainCam = Camera.main;
                if (mainCam != null &&
                    mainCam.isActiveAndEnabled &&
                    mainCam.gameObject.name != "LobbyOverviewCamera" &&
                    mainCam.gameObject.name != "PosSettlementCamera")
                {
                    playerCamera = mainCam;
                }
                else
                {
                    GameObject rigObj = GameObject.Find("PlayerRig");
                    if (rigObj != null)
                    {
                        Camera rigCam = rigObj.GetComponentInChildren<Camera>(true);
                        if (rigCam != null)
                        {
                            playerCamera = rigCam;
                        }
                    }

                    if (playerCamera == null || !playerCamera.isActiveAndEnabled)
                    {
                        Camera[] allCams = Camera.allCameras;
                        for (int i = 0; i < allCams.Length; i++)
                        {
                            Camera c = allCams[i];
                            if (c != null &&
                                c.isActiveAndEnabled &&
                                c.gameObject.name != "LobbyOverviewCamera" &&
                                c.gameObject.name != "PosSettlementCamera")
                            {
                                playerCamera = c;
                                break;
                            }
                        }
                    }
                }
            }

            if (cashRegisterTransform == null)
            {
                GameObject regObj = GameObject.Find("Counter_CashRegister");
                if (regObj != null) cashRegisterTransform = regObj.transform;
            }

            if (counterMonitorTransform == null)
            {
                GameObject monObj = GameObject.Find("Counter_Monitor");
                if (monObj != null) counterMonitorTransform = monObj.transform;
            }

            if (secondaryCashRegisterTransform == null)
            {
                GameObject subRegObj = GameObject.Find("Secondary_Counter_CashRegister");
                if (subRegObj != null) secondaryCashRegisterTransform = subRegObj.transform;
            }

            if (secondaryCounterMonitorTransform == null)
            {
                GameObject subMonObj = GameObject.Find("Secondary_Counter_Monitor");
                if (subMonObj != null)
                {
                    secondaryCounterMonitorTransform = subMonObj.transform;
                    EnsureSecondaryMonitorSignage();
                }
            }

            if (manualCubeTransform == null)
            {
                GameObject manObj = GameObject.Find("Counter_ManualCube");
                if (manObj != null) manualCubeTransform = manObj.transform;
            }
        }

        private void SyncCheckoutCounterLane(bool assignToEmployeeCounter)
        {
            if (secondaryMonitorStatusText != null)
            {
                secondaryMonitorStatusText.text = assignToEmployeeCounter
                    ? "알바 근무 중 (자동 결제)"
                    : "대기 중 ('계산대 봐줘' 지시)";
                secondaryMonitorStatusText.color = assignToEmployeeCounter
                    ? new Color(0.28f, 0.98f, 0.68f, 1f)
                    : new Color(1.0f, 0.84f, 0.34f, 1f);
            }

            if (npcPopulation == null || lastQueueAssignedToEmployeeLane == assignToEmployeeCounter)
            {
                return;
            }

            lastQueueAssignedToEmployeeLane = assignToEmployeeCounter;

            Transform checkoutWp = npcPopulation.transform.Find("Customer_Checkout");
            Transform q0 = npcPopulation.transform.Find("QueueSlot_0");
            Transform q1 = npcPopulation.transform.Find("QueueSlot_1");
            Transform q2 = npcPopulation.transform.Find("QueueSlot_2");
            Transform q3 = npcPopulation.transform.Find("QueueSlot_3");

            // 좌측(x=-6.20) = 알바 전용 계산대, 우측(x=-2.55) = 점장(플레이어) 전용 계산대
            float baseX = assignToEmployeeCounter ? -6.20f : -2.55f;
            float baseZ = -1.55f;

            SetWaypointOnNavMesh(checkoutWp, new Vector3(baseX, 0f, baseZ));
            SetWaypointOnNavMesh(q0, new Vector3(baseX + 1.00f, 0f, baseZ));
            SetWaypointOnNavMesh(q1, new Vector3(baseX + 2.00f, 0f, baseZ));
            SetWaypointOnNavMesh(q2, new Vector3(baseX + 3.00f, 0f, baseZ));
            SetWaypointOnNavMesh(q3, new Vector3(baseX + 4.00f, 0f, baseZ));
        }

        private static void SetWaypointOnNavMesh(Transform wp, Vector3 desiredPos)
        {
            if (wp == null) return;
            wp.position = NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
                ? hit.position
                : desiredPos;
        }

        private void EnsureSecondaryMonitorSignage()
        {
            if (secondaryCounterMonitorTransform == null || secondaryMonitorStatusText != null) return;

            Transform existing = secondaryCounterMonitorTransform.Find("SecondaryMonitorScreenCanvas");
            if (existing != null)
            {
                Text[] existingTexts = existing.GetComponentsInChildren<Text>(true);
                if (existingTexts.Length > 0)
                {
                    secondaryMonitorStatusText = existingTexts[existingTexts.Length - 1];
                }
                return;
            }

            GameObject canvasObj = new GameObject("SecondaryMonitorScreenCanvas", typeof(RectTransform));
            canvasObj.transform.SetParent(secondaryCounterMonitorTransform, false);

            Canvas c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 10;

            RectTransform rt = canvasObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localPosition = new Vector3(0f, 0.2245f, 0.0082f);
            rt.localRotation = Quaternion.Euler(2.07f, 180f, 0f);
            rt.sizeDelta = new Vector2(1072f, 598f);
            rt.localScale = new Vector3(0.0005f, 0.0005f, 0.0005f);

            GameObject bgObj = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgObj.GetComponent<Image>().color = new Color(0.05f, 0.10f, 0.14f, 0.96f);

            Font uiFont = ResolveSharedUiFont();

            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(bgObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.55f);
            titleRt.anchorMax = new Vector2(1f, 0.92f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            Text titleTxt = titleObj.GetComponent<Text>();
            titleTxt.font = uiFont;
            titleTxt.fontSize = 68;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.76f, 0.94f, 0.98f, 1f);
            titleTxt.text = "알바 전용 계산대";

            GameObject statusObj = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusObj.transform.SetParent(bgObj.transform, false);
            RectTransform statusRt = statusObj.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 0.15f);
            statusRt.anchorMax = new Vector2(1f, 0.52f);
            statusRt.offsetMin = Vector2.zero;
            statusRt.offsetMax = Vector2.zero;
            secondaryMonitorStatusText = statusObj.GetComponent<Text>();
            secondaryMonitorStatusText.font = uiFont;
            secondaryMonitorStatusText.fontSize = 52;
            secondaryMonitorStatusText.fontStyle = FontStyle.Bold;
            secondaryMonitorStatusText.alignment = TextAnchor.MiddleCenter;
            secondaryMonitorStatusText.color = new Color(1.0f, 0.84f, 0.34f, 1f);
            secondaryMonitorStatusText.text = "대기 중 ('계산대 봐줘' 지시)";
        }

        private Font ResolveSharedUiFont()
        {
            if (uiController != null)
            {
                Text anyText = uiController.GetComponentInChildren<Text>(true);
                if (anyText != null && anyText.font != null)
                {
                    return anyText.font;
                }
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private bool IsPlayerLookingAtCounter(Transform registerTr, Transform monitorTr, bool hasWaitingCustomer)
        {
            ResolveCounterObjects();
            if (playerCamera == null) return false;
            if (registerTr == null && monitorTr == null) return false;

            float maxDist = Mathf.Clamp(playerPosInteractDistance, 0.8f, 2.05f);
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            Transform playerRoot = playerCamera.transform.root;
            float nearestDist = float.MaxValue;
            Transform nearestTr = null;

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTr = hits[i].transform;
                if (hitTr == null) continue;
                if (playerRoot != null && (hitTr == playerRoot || hitTr.IsChildOf(playerRoot))) continue;

                if (hits[i].distance < nearestDist)
                {
                    nearestDist = hits[i].distance;
                    nearestTr = hitTr;
                }
            }

            if (nearestTr == null) return false;

            if (manualCubeTransform != null &&
                (nearestTr == manualCubeTransform || nearestTr.IsChildOf(manualCubeTransform)))
            {
                return false;
            }

            if (registerTr != null && (nearestTr == registerTr || nearestTr.IsChildOf(registerTr)))
            {
                return true;
            }

            if (monitorTr != null && (nearestTr == monitorTr || nearestTr.IsChildOf(monitorTr)))
            {
                return true;
            }

            return false;
        }

        private void EnsurePosPromptBadge()
        {
            if (posPromptBadgeObj != null && posPromptBadgeText != null) return;

            GameObject canvasObj = GameObject.Find("ManualInteractCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("PosInteractCanvas");
                Canvas c = canvasObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 64;
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            Transform existing = canvasObj.transform.Find("PosCheckoutPromptBadge");
            if (existing != null)
            {
                posPromptBadgeObj = existing.gameObject;
                posPromptBadgeText = existing.GetComponentInChildren<Text>(true);
                return;
            }

            posPromptBadgeObj = new GameObject("PosCheckoutPromptBadge", typeof(RectTransform), typeof(Image));
            posPromptBadgeObj.transform.SetParent(canvasObj.transform, false);
            Image bg = posPromptBadgeObj.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.18f, 0.22f, 0.94f);
            bg.raycastTarget = false;

            RectTransform rt = posPromptBadgeObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 178f);
            rt.sizeDelta = new Vector2(620f, 52f);

            GameObject promptInner = new GameObject("PosPromptContent", typeof(RectTransform));
            promptInner.transform.SetParent(posPromptBadgeObj.transform, false);
            RectTransform innerRt = promptInner.GetComponent<RectTransform>();
            innerRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerRt.anchorMax = new Vector2(0.5f, 0.5f);
            innerRt.pivot = new Vector2(0.5f, 0.5f);
            innerRt.anchoredPosition = Vector2.zero;
            innerRt.sizeDelta = new Vector2(1200f, 96f);
            innerRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            GameObject txtObj = new GameObject("PosPromptText", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(promptInner.transform, false);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.pivot = new Vector2(0.5f, 0.5f);
            txtRt.anchoredPosition = Vector2.zero;
            txtRt.sizeDelta = Vector2.zero;

            posPromptBadgeText = txtObj.GetComponent<Text>();
            Text manualText = canvasObj.GetComponentInChildren<Text>(true);
            posPromptBadgeText.font = manualText != null && manualText.font != null
                ? manualText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            posPromptBadgeText.fontSize = 38;
            posPromptBadgeText.fontStyle = FontStyle.Bold;
            posPromptBadgeText.alignment = TextAnchor.MiddleCenter;
            posPromptBadgeText.color = new Color(0.28f, 0.98f, 0.84f, 1f);
            posPromptBadgeText.raycastTarget = false;

            posPromptBadgeObj.SetActive(false);
        }

        private void SetPosPromptVisible(bool visible, string message)
        {
            if (!visible && posPromptBadgeObj == null) return;
            EnsurePosPromptBadge();
            if (posPromptBadgeObj == null) return;

            if (visible && posPromptBadgeText != null)
            {
                posPromptBadgeText.text = message;
            }

            if (posPromptBadgeObj.activeSelf != visible)
            {
                posPromptBadgeObj.SetActive(visible);
            }
        }

        private void EnsureInitialized()
        {
            if (ledger != null) return;
            ledger = new StoreInventoryLedger(StoreInventoryCatalog.CreateDefaultKoreanStoreItems());
            ApplyInitialStockSeeds();
            displayedTodayRevenueWon = ledger.TodayRevenueWon;
            displayedTodayUnitsSold = ledger.TodayUnitsSold;
            SyncInspectorMirror();
        }

        public void ResetForNewGame()
        {
            if (checkoutSoundAndRollCoroutine != null)
            {
                StopCoroutine(checkoutSoundAndRollCoroutine);
                checkoutSoundAndRollCoroutine = null;
            }
            pendingCustomerPickedItemIds.Clear();
            customerHiddenShelfVisuals.Clear();
            RestoreAllShelfVisuals();
            staffCheckoutTimer = 0f;
            lastWaitingCustomerId = -1;
            ledger = new StoreInventoryLedger(StoreInventoryCatalog.CreateDefaultKoreanStoreItems());
            ApplyInitialStockSeeds();
            IndexSceneShelfVisuals(syncLedgerCounts: true);
            EnsureNpcShelfProductsConfigured();
            displayedTodayRevenueWon = 0;
            displayedTodayUnitsSold = 0;
            SyncInspectorMirror();
            SaveSalesLogToDisk();
        }

        public void BeginNextDay()
        {
            EnsureInitialized();
            if (checkoutSoundAndRollCoroutine != null)
            {
                StopCoroutine(checkoutSoundAndRollCoroutine);
                checkoutSoundAndRollCoroutine = null;
            }
            pendingCustomerPickedItemIds.Clear();
            customerHiddenShelfVisuals.Clear();
            staffCheckoutTimer = 0f;
            lastWaitingCustomerId = -1;
            ledger.BeginNewDay();
            displayedTodayRevenueWon = 0;
            displayedTodayUnitsSold = 0;
            SyncInspectorMirror();
            SaveSalesLogToDisk();
        }

        public bool RecordSale(string itemId, int quantity = 1)
        {
            EnsureInitialized();
            int startRevenue = displayedTodayRevenueWon;
            if (!ledger.TrySellItem(itemId, quantity, out int revenueWon))
            {
                return false;
            }

            if (ledger.TryGetItem(itemId, out StoreItemRecord rec))
            {
                for (int i = 0; i < quantity; i++)
                {
                    HideOneShelfVisual(itemId, rec.Category);
                }
                SyncInspectorMirror();
                SaveSalesLogToDisk();
                ItemSold?.Invoke(rec, revenueWon);
            }
            else
            {
                SyncInspectorMirror();
                SaveSalesLogToDisk();
            }
            TriggerCheckoutAudioAndRevenueRoll(startRevenue, ledger.TodayRevenueWon, ledger.TodayUnitsSold);
            return true;
        }

        public bool RecordRestock(string itemId, int quantity = 3)
        {
            EnsureInitialized();
            int beforeStock = ledger.TryGetItem(itemId, out StoreItemRecord beforeRec) ? beforeRec.CurrentStock : 0;
            if (!ledger.RestockItem(itemId, quantity))
            {
                return false;
            }

            int afterStock = ledger.TryGetItem(itemId, out StoreItemRecord afterRec) ? afterRec.CurrentStock : beforeStock;
            int added = Mathf.Max(0, afterStock - beforeStock);
            if (added > 0 && afterRec != null)
            {
                RestoreShelfVisuals(itemId, afterRec.Category, added);
            }

            SyncInspectorMirror();
            return true;
        }

        public int RecordRestockByTag(string tagOrCategory, int maxAddTotal = 12)
        {
            EnsureInitialized();
            int added = ledger.RestockCategoryToCap(tagOrCategory, maxAddTotal);
            if (added > 0)
            {
                if (StoreInventoryCatalog.TryParseCategoryTag(tagOrCategory, out StoreItemCategory cat))
                {
                    RestoreShelfVisuals(null, cat, added);
                }
                SyncInspectorMirror();
            }
            return added;
        }

        private void IndexSceneShelfVisuals(bool syncLedgerCounts)
        {
            EnsureInitialized();
            shelfVisualsByItemId.Clear();
            shelfVisualsByCategory.Clear();

            GameObject shelfRoot = GameObject.Find("Store_RealSellableItems");
            if (shelfRoot == null)
            {
                isShelfVisualsIndexed = false;
                return;
            }

            Transform rootTr = shelfRoot.transform;
            Transform[] allTransforms = rootTr.GetComponentsInChildren<Transform>(true);
            HashSet<GameObject> registered = new HashSet<GameObject>();

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform tr = allTransforms[i];
                if (tr == null || tr == rootTr) continue;

                string cleanedName = CleanCatalogObjectName(tr.name);
                if (!ledger.TryGetItem(cleanedName, out StoreItemRecord rec)) continue;

                // 부모가 이미 같은 상품 루트로 등록되어 있으면 자식 메시는 중복 등록하지 않는다.
                if (tr.parent != null && registered.Contains(tr.parent.gameObject)) continue;

                GameObject go = tr.gameObject;
                if (!registered.Add(go)) continue;

                if (!shelfVisualsByItemId.TryGetValue(rec.ItemId, out List<GameObject> itemList))
                {
                    itemList = new List<GameObject>();
                    shelfVisualsByItemId[rec.ItemId] = itemList;
                }
                itemList.Add(go);

                if (!shelfVisualsByCategory.TryGetValue(rec.Category, out List<GameObject> catList))
                {
                    catList = new List<GameObject>();
                    shelfVisualsByCategory[rec.Category] = catList;
                }
                catList.Add(go);
            }

            if (syncLedgerCounts && shelfVisualsByItemId.Count > 0)
            {
                foreach (KeyValuePair<string, List<GameObject>> kv in shelfVisualsByItemId)
                {
                    int activeCount = 0;
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        if (kv.Value[i] != null && kv.Value[i].activeSelf)
                        {
                            activeCount++;
                        }
                    }

                    if (activeCount > 0)
                    {
                        ledger.SetInitialStock(kv.Key, activeCount);
                    }
                }
            }

            isShelfVisualsIndexed = true;
        }

        private GameObject HideOneShelfVisual(string itemId, StoreItemCategory category)
        {
            if (!string.IsNullOrEmpty(itemId) &&
                shelfVisualsByItemId.TryGetValue(itemId, out List<GameObject> itemList))
            {
                for (int i = itemList.Count - 1; i >= 0; i--)
                {
                    GameObject go = itemList[i];
                    if (go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                        return go;
                    }
                }
            }

            if (shelfVisualsByCategory.TryGetValue(category, out List<GameObject> catList))
            {
                for (int i = catList.Count - 1; i >= 0; i--)
                {
                    GameObject go = catList[i];
                    if (go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                        return go;
                    }
                }
            }

            return null;
        }

        private void RestoreShelfVisuals(string itemId, StoreItemCategory category, int count)
        {
            int remaining = Mathf.Max(0, count);
            if (remaining == 0) return;

            if (!string.IsNullOrEmpty(itemId) &&
                shelfVisualsByItemId.TryGetValue(itemId, out List<GameObject> itemList))
            {
                for (int i = 0; i < itemList.Count && remaining > 0; i++)
                {
                    GameObject go = itemList[i];
                    if (go != null && !go.activeSelf)
                    {
                        go.SetActive(true);
                        remaining--;
                    }
                }
            }

            if (remaining > 0 && shelfVisualsByCategory.TryGetValue(category, out List<GameObject> catList))
            {
                for (int i = 0; i < catList.Count && remaining > 0; i++)
                {
                    GameObject go = catList[i];
                    if (go != null && !go.activeSelf)
                    {
                        go.SetActive(true);
                        remaining--;
                    }
                }
            }
        }

        private void RestoreAllShelfVisuals()
        {
            foreach (KeyValuePair<StoreItemCategory, List<GameObject>> kv in shelfVisualsByCategory)
            {
                List<GameObject> list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && !list[i].activeSelf)
                    {
                        list[i].SetActive(true);
                    }
                }
            }
        }

        /// <summary>
        /// SJM_UI_Work 씬의 NpcPopulationController.products 배열이 비어 있거나 pickupLocation이 미지정일 때
        /// 실제 7개 카테고리 매대 위치로 런타임 자동 연결하여 손님이 실제 매대로 걸어가 물건을 집고,
        /// 재고가 0 이하이면 구매 없이 퇴장하도록 보장한다.
        /// </summary>
        private void EnsureNpcShelfProductsConfigured()
        {
            if (npcPopulation == null || isNpcProductsConfigured) return;

            Type npcType = npcPopulation.GetType();
            FieldInfo productsField = npcType.GetField("products", BindingFlags.Instance | BindingFlags.NonPublic);
            if (productsField == null) return;

            Type optionType = productsField.FieldType.GetElementType();
            if (optionType == null) return;

            FieldInfo catField = optionType.GetField("category", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo nameField = optionType.GetField("productName", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo pickupField = optionType.GetField("pickupLocation", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo visualPrefabField = optionType.GetField("visualPrefab", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo shelfItemField = optionType.GetField("shelfItem", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo unstockedField = optionType.GetField("unstocked", BindingFlags.Instance | BindingFlags.Public);
            if (catField == null || nameField == null || pickupField == null) return;

            (ProductCategory cat, StoreItemCategory storeCat, string repName)[] defaults =
            {
                (ProductCategory.Drink, StoreItemCategory.Drink, "P_RSG_WaterBottle"),
                (ProductCategory.Snack, StoreItemCategory.Snack, "Chocolate_Bar_1"),
                (ProductCategory.Ramen, StoreItemCategory.Ramen, "SM_Cook_PhoBowl_Full_01"),
                (ProductCategory.Daily, StoreItemCategory.Daily, "SM_Cook_NapkinBox_01"),
                (ProductCategory.IceCream, StoreItemCategory.IceCream, "Vanilla_Ice_Cream_1"),
                (ProductCategory.FreshFood, StoreItemCategory.FreshFood, "Hambuger"),
                (ProductCategory.Cigarette, StoreItemCategory.Cigarette, "CigPack")
            };

            Array existing = productsField.GetValue(npcPopulation) as Array;
            Array targetArray = Array.CreateInstance(optionType, defaults.Length);

            for (int i = 0; i < defaults.Length; i++)
            {
                object option = (existing != null && i < existing.Length)
                    ? existing.GetValue(i)
                    : Activator.CreateInstance(optionType);

                catField.SetValue(option, defaults[i].cat);
                nameField.SetValue(option, defaults[i].repName);
                Transform pickupTr = ResolveOrCreateCategoryPickupTransform(defaults[i].storeCat);
                if (pickupTr != null)
                {
                    pickupField.SetValue(option, pickupTr);
                }

                // 임시 큐브(ProductBox) 대신 실제 매대 3D 물건 복제 템플릿을 손님 휴대용 프리팹으로 연결
                if (visualPrefabField != null)
                {
                    GameObject carriedTemplate = GetOrCreateCarriedTemplate(defaults[i].storeCat, defaults[i].repName);
                    if (carriedTemplate != null)
                    {
                        visualPrefabField.SetValue(option, carriedTemplate);
                    }
                }

                // 단일 오브젝트가 아닌 실제 매대 전체 재고(CanTakeFromShelf)로 품절 판정
                if (shelfItemField != null)
                {
                    shelfItemField.SetValue(option, null);
                }

                if (unstockedField != null)
                {
                    unstockedField.SetValue(option, false);
                }

                targetArray.SetValue(option, i);
            }

            productsField.SetValue(npcPopulation, targetArray);
            isNpcProductsConfigured = true;
        }

        private GameObject GetOrCreateCarriedTemplate(StoreItemCategory category, string preferredItemId)
        {
            if (carriedTemplateByCategory.TryGetValue(category, out GameObject existing) && existing != null)
            {
                return existing;
            }

            GameObject sourceObj = null;
            if (!string.IsNullOrEmpty(preferredItemId) &&
                shelfVisualsByItemId.TryGetValue(preferredItemId, out List<GameObject> prefList) &&
                prefList.Count > 0)
            {
                sourceObj = prefList[0];
            }
            else if (shelfVisualsByCategory.TryGetValue(category, out List<GameObject> catList) && catList.Count > 0)
            {
                sourceObj = catList[0];
            }

            if (sourceObj == null)
            {
                return null;
            }

            if (carriedTemplatesRoot == null)
            {
                carriedTemplatesRoot = new GameObject("CustomerCarriedTemplates");
                carriedTemplatesRoot.transform.SetParent(transform, false);
                // 부모만 비활성화하고 자식 템플릿은 activeSelf=true를 유지해야 Instantiate 시 바로 표시됨
                carriedTemplatesRoot.SetActive(false);
            }

            GameObject template = Instantiate(sourceObj, carriedTemplatesRoot.transform);
            template.name = $"CarriedTemplate_{category}";
            template.SetActive(true);
            template.transform.localPosition = Vector3.zero;
            template.transform.localRotation = Quaternion.identity;
            template.transform.localScale = sourceObj.transform.lossyScale;

            Collider[] cols = template.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Destroy(cols[i]);
            }

            Rigidbody[] bodies = template.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Destroy(bodies[i]);
            }

            carriedTemplateByCategory[category] = template;
            return template;
        }

        private Transform ResolveOrCreateCategoryPickupTransform(StoreItemCategory category)
        {
            if (runtimeShelfPickupPoints.TryGetValue(category, out Transform cached) && cached != null)
            {
                return cached;
            }

            // 1. OpenAI Voice Integration의 실제 진열 구역 목적지가 있으면 우선 활용
            Transform voiceTarget = TryFindVoiceRestockDestination(category);
            if (voiceTarget != null)
            {
                runtimeShelfPickupPoints[category] = voiceTarget;
                return voiceTarget;
            }

            // 2. 씬에 배치된 실제 3D 상품들의 중심 좌표를 NavMesh 바닥으로 투영해 픽업 포인트 생성
            if (shelfVisualsByCategory.TryGetValue(category, out List<GameObject> items) && items.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        sum += items[i].transform.position;
                        count++;
                    }
                }

                if (count > 0)
                {
                    Vector3 center = sum / count;
                    Vector3 samplePos = new Vector3(center.x, 0.2f, center.z);
                    Vector3 finalPos = NavMesh.SamplePosition(samplePos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas)
                        ? hit.position
                        : new Vector3(center.x, 0f, center.z);

                    GameObject wpObj = new GameObject($"CustomerPickup_{category}");
                    wpObj.transform.SetParent(transform, false);
                    wpObj.transform.position = finalPos;
                    runtimeShelfPickupPoints[category] = wpObj.transform;
                    return wpObj.transform;
                }
            }

            return null;
        }

        private static Transform TryFindVoiceRestockDestination(StoreItemCategory category)
        {
            string fieldName;
            switch (category)
            {
                case StoreItemCategory.Drink: fieldName = "restockWalkInDestination"; break;
                case StoreItemCategory.Snack: fieldName = "restockSnackDestination"; break;
                case StoreItemCategory.Ramen: fieldName = "restockRamenDestination"; break;
                case StoreItemCategory.Daily: fieldName = "restockDailyGoodsDestination"; break;
                case StoreItemCategory.IceCream: fieldName = "restockIceCreamDestination"; break;
                case StoreItemCategory.FreshFood: fieldName = "restockHotFoodDestination"; break;
                case StoreItemCategory.Cigarette: fieldName = "cashierDestination"; break;
                default: return null;
            }

            Type voiceType = Type.GetType("EmployeeVoice.VoiceMovementIntegration, Assembly-CSharp");
            if (voiceType == null) return null;

            UnityEngine.Object found = FindAnyObjectByType(voiceType);
            if (!(found is MonoBehaviour mb)) return null;

            FieldInfo field = voiceType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field != null ? field.GetValue(mb) as Transform : null;
        }

        private static string CleanCatalogObjectName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;
            string trimmed = rawName.Trim();
            int cloneIdx = trimmed.IndexOf("(Clone)", StringComparison.Ordinal);
            if (cloneIdx >= 0)
            {
                trimmed = trimmed.Substring(0, cloneIdx).Trim();
            }
            return trimmed;
        }

        public void SaveSalesLogToDisk()
        {
            EnsureInitialized();
            try
            {
                int dayNum = uiController != null ? Mathf.Max(1, uiController.CurrentDayNumber) : 1;
                ledger.SaveSalesLogToFile(SalesLogFilePath, dayNum);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[StoreInventoryManager] 판매 로그 파일 저장 실패: " + ex.Message);
            }
        }

        public string BuildSettlementDetailText(int maxLines = int.MaxValue)
        {
            EnsureInitialized();
            SaveSalesLogToDisk();
            if (StoreInventoryLedger.TryLoadSalesLogSummaryFromFile(SalesLogFilePath, out string loadedReport) &&
                !string.IsNullOrWhiteSpace(loadedReport))
            {
                return loadedReport;
            }

            return ledger.BuildReceiptDetailReport(maxLines);
        }

        private void ApplyInitialStockSeeds()
        {
            if (initialStockSeeds == null || initialStockSeeds.Count == 0) return;

            for (int i = 0; i < initialStockSeeds.Count; i++)
            {
                StockSeedEntry entry = initialStockSeeds[i];
                if (!string.IsNullOrWhiteSpace(entry.itemId))
                {
                    ledger.SetInitialStock(entry.itemId, entry.initialStock);
                }
            }
        }

        private void SyncInspectorMirror()
        {
            if (ledger == null) return;
            todayUnitsSold = ledger.TodayUnitsSold;
            todayRevenueWon = ledger.TodayRevenueWon;
            cumulativeRevenueWon = ledger.CumulativeRevenueWon;
            totalRemainingStock = ledger.TotalCurrentStock;

            liveItemsSnapshot.Clear();
            IReadOnlyList<StoreItemRecord> all = ledger.Items;
            for (int i = 0; i < all.Count; i++)
            {
                liveItemsSnapshot.Add(all[i]);
            }
        }
    }
}
