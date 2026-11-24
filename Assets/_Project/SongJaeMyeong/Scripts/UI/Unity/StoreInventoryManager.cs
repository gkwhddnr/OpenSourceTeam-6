using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
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
        [SerializeField] private float playerPosInteractDistance = 3.2f;
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
        private int saleRandomSeed = 101;
        private bool isNpcSubscribed;
        private float staffCheckoutTimer;
        private int lastWaitingCustomerId = -1;
        private Camera playerCamera;
        private Transform cashRegisterTransform;
        private Transform counterMonitorTransform;
        private Transform manualCubeTransform;
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
            saleRandomSeed = unchecked(saleRandomSeed * 1103515245 + 12345 + customerId * 31);

            if (pendingCustomerPickedItemIds.TryGetValue(customerId, out string oldReservedId))
            {
                ledger.ReturnPickedItemToShelf(oldReservedId);
                pendingCustomerPickedItemIds.Remove(customerId);
            }

            if (ledger.TryPickupFromShelfByTag(productTag, saleRandomSeed, out StoreItemRecord picked))
            {
                pendingCustomerPickedItemIds[customerId] = picked.ItemId;
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

            // 1. 알바생이 계산대(StaffWorkRole.Checkout)에 도착(Arrived)해 있으면 자동으로 결제 처리
            bool isStaffStationedAtCheckout =
                uiController.CurrentEmployeeRole == StaffWorkRole.Checkout &&
                uiController.CurrentEmployeeMoveBadge == StaffMoveBadge.Arrived;

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

            // 2. 플레이어가 포스기(Counter_CashRegister / Counter_Monitor)를 바라볼 때 [E] 키 직접 결제 처리
            if (uiController.IsCommandOverlayOpen || uiController.IsExternalModalOpen)
            {
                SetPosPromptVisible(false, string.Empty);
                return;
            }

            bool lookingAtPos = IsPlayerLookingAtPosRegister(hasWaitingCustomer);
            if (!lookingAtPos)
            {
                SetPosPromptVisible(false, string.Empty);
                return;
            }

            if (hasWaitingCustomer)
            {
                StoreItemRecord previewItem = PeekWaitingCustomerItem(customerId, productTag);
                string itemLabel = previewItem != null
                    ? $"{previewItem.DisplayName} (+{previewItem.UnitPriceWon:N0}원)"
                    : $"{productTag}";
                SetPosPromptVisible(true, $"[E]  포스기 손님 결제하기  ·  {itemLabel}");

                Keyboard kb = Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame)
                {
                    TryProcessWaitingCheckout(isPlayerAction: true);
                }
            }
            else
            {
                string waitMsg = npcPopulation != null && npcPopulation.ActiveCustomers > 0
                    ? "[포스기]  손님이 물건을 고르거나 계산대로 이동 중입니다"
                    : "[포스기]  현재 계산 대기 중인 손님이 없습니다";
                SetPosPromptVisible(true, waitMsg);
            }
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
            }
            else
            {
                saleRandomSeed = unchecked(saleRandomSeed * 1103515245 + 12345 + approvedCustomerId);
                if (ledger.TryPickupFromShelfByTag(productTag, saleRandomSeed, out StoreItemRecord fallbackPicked))
                {
                    targetItemId = fallbackPicked.ItemId;
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

            if (manualCubeTransform == null)
            {
                GameObject manObj = GameObject.Find("Counter_ManualCube");
                if (manObj != null) manualCubeTransform = manObj.transform;
            }

            if (npcPopulation != null)
            {
                Transform checkoutWp = npcPopulation.transform.Find("Customer_Checkout");
                if (checkoutWp != null && checkoutWp.position.x < -3.40f)
                {
                    checkoutWp.position = new Vector3(-2.55f, 0f, -1.55f);
                    Transform q0 = npcPopulation.transform.Find("QueueSlot_0");
                    Transform q1 = npcPopulation.transform.Find("QueueSlot_1");
                    Transform q2 = npcPopulation.transform.Find("QueueSlot_2");
                    Transform q3 = npcPopulation.transform.Find("QueueSlot_3");
                    if (q0 != null) q0.position = new Vector3(-1.55f, 0f, -1.55f);
                    if (q1 != null) q1.position = new Vector3(-0.55f, 0f, -1.55f);
                    if (q2 != null) q2.position = new Vector3(0.45f, 0f, -1.55f);
                    if (q3 != null) q3.position = new Vector3(1.45f, 0f, -1.55f);
                }
            }
        }

        private bool IsPlayerLookingAtPosRegister(bool hasWaitingCustomer)
        {
            ResolveCounterObjects();
            if (playerCamera == null) return false;
            if (cashRegisterTransform == null && counterMonitorTransform == null) return false;

            Transform camTr = playerCamera.transform;
            float maxDist = Mathf.Max(3.4f, playerPosInteractDistance);
            Ray ray = new Ray(camTr.position, camTr.forward);

            // 설명서 큐브(Counter_ManualCube)를 직접 조준 중이면 설명서 [E]가 우선하도록 양보한다.
            if (manualCubeTransform != null)
            {
                Vector3 toManual = manualCubeTransform.position - camTr.position;
                float manualDist = toManual.magnitude;
                if (manualDist <= maxDist && manualDist > 0.001f)
                {
                    if (Physics.SphereCast(ray, 0.16f, out RaycastHit manualHit, maxDist, ~0, QueryTriggerInteraction.Ignore) &&
                        (manualHit.transform == manualCubeTransform || manualHit.transform.IsChildOf(manualCubeTransform)))
                    {
                        return false;
                    }
                }
            }

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.22f, maxDist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTr = hits[i].transform;
                if (hitTr == null) continue;

                if (cashRegisterTransform != null &&
                    (hitTr == cashRegisterTransform || hitTr.IsChildOf(cashRegisterTransform)))
                {
                    return true;
                }

                if (counterMonitorTransform != null &&
                    (hitTr == counterMonitorTransform || hitTr.IsChildOf(counterMonitorTransform)))
                {
                    return true;
                }
            }

            float bestDot = -1f;
            float bestDist = float.MaxValue;

            if (cashRegisterTransform != null)
            {
                Vector3 toReg = (cashRegisterTransform.position + Vector3.up * 0.16f) - camTr.position;
                float d = toReg.magnitude;
                if (d <= maxDist && d > 0.001f)
                {
                    float dot = Vector3.Dot(camTr.forward, toReg / d);
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestDist = d;
                    }
                }
            }

            if (counterMonitorTransform != null)
            {
                Vector3 toMon = (counterMonitorTransform.position + Vector3.up * 0.22f) - camTr.position;
                float d = toMon.magnitude;
                if (d <= maxDist && d > 0.001f)
                {
                    float dot = Vector3.Dot(camTr.forward, toMon / d);
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestDist = d;
                    }
                }
            }

            if (bestDist > maxDist) return false;

            float requiredDot = (hasWaitingCustomer && bestDist <= 2.40f) ? 0.45f : 0.65f;
            if (bestDot < requiredDot) return false;

            if (manualCubeTransform != null)
            {
                Vector3 toManual = manualCubeTransform.position - camTr.position;
                if (toManual.magnitude > 0.001f && toManual.magnitude <= 2.3f)
                {
                    float manualDot = Vector3.Dot(camTr.forward, toManual.normalized);
                    if (manualDot >= 0.84f && manualDot > bestDot + 0.06f)
                    {
                        return false;
                    }
                }
            }

            return true;
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
            staffCheckoutTimer = 0f;
            lastWaitingCustomerId = -1;
            ledger = new StoreInventoryLedger(StoreInventoryCatalog.CreateDefaultKoreanStoreItems());
            ApplyInitialStockSeeds();
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

            SyncInspectorMirror();
            SaveSalesLogToDisk();
            if (ledger.TryGetItem(itemId, out StoreItemRecord rec))
            {
                ItemSold?.Invoke(rec, revenueWon);
            }
            TriggerCheckoutAudioAndRevenueRoll(startRevenue, ledger.TodayRevenueWon, ledger.TodayUnitsSold);
            return true;
        }

        public bool RecordRestock(string itemId, int quantity = 3)
        {
            EnsureInitialized();
            if (!ledger.RestockItem(itemId, quantity))
            {
                return false;
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
                SyncInspectorMirror();
            }
            return added;
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
