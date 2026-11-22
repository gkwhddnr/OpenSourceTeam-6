using System;
using System.Collections.Generic;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.UI.Core;
using UnityEngine;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 편의점 54종 물품의 단가·현재 재고·판매 개수·총 매출을 Unity Inspector와 런타임에서 관리하며,
    /// 손님 NPC 계산 완료 시 실제 재고 차감 및 매출 집계를 연동한다.
    /// </summary>
    public sealed class StoreInventoryManager : MonoBehaviour
    {
        [Serializable]
        public struct StockSeedEntry
        {
            public string itemId;
            public int initialStock;
        }

        [Header("연동 대상")]
        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private NpcPopulationController npcPopulation;

        [Header("초기 진열 재고 (맵 배치 개수 자동 기록)")]
        [SerializeField] private List<StockSeedEntry> initialStockSeeds = new List<StockSeedEntry>();

        [Header("실시간 재고 · 매출 현황 (읽기 확인용)")]
        [SerializeField] private int todayUnitsSold;
        [SerializeField] private int todayRevenueWon;
        [SerializeField] private int cumulativeRevenueWon;
        [SerializeField] private int totalRemainingStock;
        [SerializeField] private List<StoreItemRecord> liveItemsSnapshot = new List<StoreItemRecord>();

        private StoreInventoryLedger ledger;
        private int lastObservedCustomerCheckouts;
        private int saleRandomSeed = 101;

        public StoreInventoryLedger Ledger
        {
            get
            {
                EnsureInitialized();
                return ledger;
            }
        }

        public int TodayRevenueWon => Ledger.TodayRevenueWon;
        public int CumulativeRevenueWon => Ledger.CumulativeRevenueWon;
        public int TodayUnitsSold => Ledger.TodayUnitsSold;
        public int TotalRemainingStock => Ledger.TotalCurrentStock;

        public event Action<StoreItemRecord, int> ItemSold;

        private void Awake()
        {
            EnsureInitialized();
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

            lastObservedCustomerCheckouts = npcPopulation != null ? npcPopulation.SuccessfulMockCheckouts : 0;
            SyncInspectorMirror();
        }

        private void Update()
        {
            if (uiController == null || uiController.CurrentMode != UiScreenMode.InGame || uiController.IsPauseMenuOpen)
            {
                return;
            }

            if (npcPopulation != null)
            {
                int currentCheckouts = npcPopulation.SuccessfulMockCheckouts;
                if (currentCheckouts > lastObservedCustomerCheckouts)
                {
                    int delta = currentCheckouts - lastObservedCustomerCheckouts;
                    lastObservedCustomerCheckouts = currentCheckouts;
                    for (int i = 0; i < delta; i++)
                    {
                        ProcessCustomerCheckoutSale();
                    }
                }
            }
        }

        private void EnsureInitialized()
        {
            if (ledger != null) return;
            ledger = new StoreInventoryLedger(StoreInventoryCatalog.CreateDefaultKoreanStoreItems());
            ApplyInitialStockSeeds();
            SyncInspectorMirror();
        }

        public void ResetForNewGame()
        {
            ledger = new StoreInventoryLedger(StoreInventoryCatalog.CreateDefaultKoreanStoreItems());
            ApplyInitialStockSeeds();
            lastObservedCustomerCheckouts = npcPopulation != null ? npcPopulation.SuccessfulMockCheckouts : 0;
            SyncInspectorMirror();
        }

        public void BeginNextDay()
        {
            EnsureInitialized();
            ledger.BeginNewDay();
            lastObservedCustomerCheckouts = npcPopulation != null ? npcPopulation.SuccessfulMockCheckouts : 0;
            SyncInspectorMirror();
        }

        public bool RecordSale(string itemId, int quantity = 1)
        {
            EnsureInitialized();
            if (!ledger.TrySellItem(itemId, quantity, out int revenueWon))
            {
                return false;
            }

            SyncInspectorMirror();
            if (ledger.TryGetItem(itemId, out StoreItemRecord rec))
            {
                ItemSold?.Invoke(rec, revenueWon);
            }
            uiController?.NotifyInventoryRevenueChanged();
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

        public string BuildSettlementDetailText(int maxLines = 8)
        {
            EnsureInitialized();
            return ledger.BuildReceiptDetailReport(maxLines);
        }

        private void ProcessCustomerCheckoutSale()
        {
            EnsureInitialized();
            // 손님 1회 계산 시 1~2개 품목 구매 시뮬레이션
            int itemCount = (saleRandomSeed % 2) + 1;
            int totalCheckoutWon = 0;
            for (int k = 0; k < itemCount; k++)
            {
                saleRandomSeed = unchecked(saleRandomSeed * 1103515245 + 12345);
                if (ledger.TrySellAnyInStockItem(saleRandomSeed, out StoreItemRecord sold, out int rev))
                {
                    totalCheckoutWon += rev;
                    ItemSold?.Invoke(sold, rev);
                }
            }

            if (totalCheckoutWon > 0)
            {
                SyncInspectorMirror();
                uiController?.AudioPlayer?.PlaySfx(StoreSfxId.BarcodeCheckout);
                uiController?.NotifyInventoryRevenueChanged();
            }
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
