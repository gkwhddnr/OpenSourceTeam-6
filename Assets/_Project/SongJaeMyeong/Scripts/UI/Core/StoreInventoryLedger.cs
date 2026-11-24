using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenSourceTeam6.UI.Core
{
    public enum StoreItemCategory
    {
        Drink = 0,
        Snack = 1,
        Ramen = 2,
        Daily = 3,
        IceCream = 4,
        FreshFood = 5,
        Cigarette = 6
    }

    [Serializable]
    public sealed class StoreSalesLogEntry
    {
        public int DayNumber;
        public int CustomerId;
        public string ProductTag;
        public string ItemId;
        public string DisplayName;
        public int UnitPriceWon;
        public string ProcessedBy;
        public string GameClockText;
    }

    [Serializable]
    public sealed class StoreItemRecord
    {
        public string ItemId;
        public string DisplayName;
        public StoreItemCategory Category;
        public string ProductTag;
        public int UnitPriceWon;
        public int MaxStock;
        public int CurrentStock;
        public int ReservedOnFloorCount;
        public int TodaySoldCount;
        public int TotalSoldCount;

        public int TodaySalesWon => TodaySoldCount * UnitPriceWon;
        public int TotalSalesWon => TotalSoldCount * UnitPriceWon;

        public StoreItemRecord()
        {
        }

        public StoreItemRecord(
            string itemId,
            string displayName,
            StoreItemCategory category,
            int unitPriceWon,
            int initialStock = 0)
        {
            ItemId = itemId ?? string.Empty;
            DisplayName = displayName ?? itemId ?? string.Empty;
            Category = category;
            ProductTag = StoreInventoryCatalog.GetCategoryTag(category);
            UnitPriceWon = Math.Max(0, unitPriceWon);
            MaxStock = Math.Max(1, initialStock);
            CurrentStock = Math.Max(0, initialStock);
            ReservedOnFloorCount = 0;
            TodaySoldCount = 0;
            TotalSoldCount = 0;
        }
    }

    /// <summary>
    /// 2025~2026년 한국 편의점(CU/GS25/세븐일레븐) 실제 시중 물가 기준
    /// 판매 품목 54종(Sellable_Items_Handover.md 01_CurrentlySold) 가격·카테고리·태그 마스터 카탈로그.
    /// </summary>
    public static class StoreInventoryCatalog
    {
        public const string TagDrink = "Item_Drink";
        public const string TagSnack = "Item_Snack";
        public const string TagRamen = "Item_Ramen";
        public const string TagDaily = "Item_Daily";
        public const string TagIceCream = "Item_IceCream";
        public const string TagFreshFood = "Item_FreshFood";
        public const string TagCigarette = "Item_Cigarette";

        public static readonly StoreItemCategory[] AllCategories =
        {
            StoreItemCategory.Drink,
            StoreItemCategory.Snack,
            StoreItemCategory.Ramen,
            StoreItemCategory.Daily,
            StoreItemCategory.IceCream,
            StoreItemCategory.FreshFood,
            StoreItemCategory.Cigarette
        };

        public static string GetCategoryTag(StoreItemCategory category)
        {
            switch (category)
            {
                case StoreItemCategory.Drink: return TagDrink;
                case StoreItemCategory.Snack: return TagSnack;
                case StoreItemCategory.Ramen: return TagRamen;
                case StoreItemCategory.Daily: return TagDaily;
                case StoreItemCategory.IceCream: return TagIceCream;
                case StoreItemCategory.FreshFood: return TagFreshFood;
                case StoreItemCategory.Cigarette: return TagCigarette;
                default: return TagDrink;
            }
        }

        public static bool TryParseCategoryTag(string tag, out StoreItemCategory category)
        {
            category = StoreItemCategory.Drink;
            if (string.IsNullOrWhiteSpace(tag)) return false;

            string trimmed = tag.Trim();
            if (string.Equals(trimmed, TagDrink, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Drink", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "WALK_IN", StringComparison.OrdinalIgnoreCase))
            {
                category = StoreItemCategory.Drink;
                return true;
            }

            if (string.Equals(trimmed, TagSnack, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Snack", StringComparison.OrdinalIgnoreCase))
            {
                category = StoreItemCategory.Snack;
                return true;
            }

            if (string.Equals(trimmed, TagRamen, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Ramen", StringComparison.OrdinalIgnoreCase))
            {
                category = StoreItemCategory.Ramen;
                return true;
            }

            if (string.Equals(trimmed, TagDaily, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Daily", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "DAILY_GOODS", StringComparison.OrdinalIgnoreCase))
            {
                category = StoreItemCategory.Daily;
                return true;
            }

            if (string.Equals(trimmed, TagIceCream, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "IceCream", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "ICE_CREAM", StringComparison.OrdinalIgnoreCase))
            {
                category = StoreItemCategory.IceCream;
                return true;
            }

            if (string.Equals(trimmed, TagFreshFood, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "FreshFood", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "HOT_FOOD", StringComparison.OrdinalIgnoreCase))
            {
                category = StoreItemCategory.FreshFood;
                return true;
            }

            if (string.Equals(trimmed, TagCigarette, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Cigarette", StringComparison.OrdinalIgnoreCase))
            {
                category = StoreItemCategory.Cigarette;
                return true;
            }

            return false;
        }

        public static List<StoreItemRecord> CreateDefaultKoreanStoreItems()
        {
            return new List<StoreItemRecord>
            {
                // 1. 음료류 (Item_Drink) - 8종
                new StoreItemRecord("P_RSG_WaterBottle", "생수 (500ml)", StoreItemCategory.Drink, 1000, 6),
                new StoreItemRecord("P_RSG_SodaCan", "탄산캔 음료", StoreItemCategory.Drink, 1700, 6),
                new StoreItemRecord("Milk", "우유 (200ml)", StoreItemCategory.Drink, 1800, 6),
                new StoreItemRecord("Orange_Juice_1", "오렌지 주스", StoreItemCategory.Drink, 2200, 6),
                new StoreItemRecord("Green_Juice_1", "그린 사과 주스", StoreItemCategory.Drink, 2200, 6),
                new StoreItemRecord("Red_Juice_1", "베리 주스", StoreItemCategory.Drink, 2200, 6),
                new StoreItemRecord("SM_Clutter_Bottle_Blue_01", "병음료 (블루)", StoreItemCategory.Drink, 2000, 6),
                new StoreItemRecord("SM_Clutter_Bottle_Green_01", "병음료 (그린)", StoreItemCategory.Drink, 2000, 6),

                // 2. 스낵/베이커리류 (Item_Snack) - 10종
                new StoreItemRecord("Chocolate_Bar_1", "초콜릿 바", StoreItemCategory.Snack, 1800, 5),
                new StoreItemRecord("Dark_Chocolate_Bar_1", "다크 초콜릿 바", StoreItemCategory.Snack, 2000, 5),
                new StoreItemRecord("Milk_Chocolate_Bar_1", "밀크 초콜릿 바", StoreItemCategory.Snack, 1800, 4),
                new StoreItemRecord("Chocolate_Donut_Sprinkles_1", "초코 스프링클 도넛", StoreItemCategory.Snack, 2200, 4),
                new StoreItemRecord("Strawberry_Donut_Sprinkles_1", "딸기 스프링클 도넛", StoreItemCategory.Snack, 2200, 4),
                new StoreItemRecord("White_Donut_Sprinkles_1", "화이트 스프링클 도넛", StoreItemCategory.Snack, 2200, 4),
                new StoreItemRecord("Purple_Donut_1", "블루베리 도넛", StoreItemCategory.Snack, 2300, 4),
                new StoreItemRecord("Cake", "조각 케이크", StoreItemCategory.Snack, 3800, 7),
                new StoreItemRecord("Waffle", "와플", StoreItemCategory.Snack, 2500, 14),
                new StoreItemRecord("Donuts", "베이커리 도넛", StoreItemCategory.Snack, 2000, 7),

                // 3. 라면/컵면류 (Item_Ramen) - 8종
                new StoreItemRecord("SM_Cook_PhoBowl_Full_01", "큰컵 라면", StoreItemCategory.Ramen, 1700, 7),
                new StoreItemRecord("SM_Cook_PhoBowl_Empty_01", "용기면 (대)", StoreItemCategory.Ramen, 1600, 7),
                new StoreItemRecord("SM_Cook_BowlStack_01", "컵라면 4입 묶음", StoreItemCategory.Ramen, 5800, 14),
                new StoreItemRecord("SM_Cook_SideBowl_01", "소컵 라면", StoreItemCategory.Ramen, 1200, 7),
                new StoreItemRecord("SM_Cook_SideBowl_Lime_01", "라임 소컵 라면", StoreItemCategory.Ramen, 1300, 7),
                new StoreItemRecord("P_RSG_FoodCan", "통조림 캔식품", StoreItemCategory.Ramen, 2800, 7),
                new StoreItemRecord("SM_Cook_ChopstickBundle_01", "나무젓가락 묶음", StoreItemCategory.Ramen, 1500, 7),
                new StoreItemRecord("SM_Cook_Chopsticks_01", "일회용 젓가락", StoreItemCategory.Ramen, 300, 0),

                // 4. 생필품/조미료 (Item_Daily) - 11종
                new StoreItemRecord("SM_Cook_NapkinBox_01", "곽티슈 / 냅킨", StoreItemCategory.Daily, 3200, 7),
                new StoreItemRecord("SM_Cook_SauceBottle_Chili_01", "칠리 소스", StoreItemCategory.Daily, 3500, 4),
                new StoreItemRecord("SM_Cook_SauceBottle_Fish_01", "간장 / 액젓 소스", StoreItemCategory.Daily, 3200, 4),
                new StoreItemRecord("SM_Cook_SauceBottle_Hoisin_01", "양념 소스", StoreItemCategory.Daily, 3600, 4),
                new StoreItemRecord("SM_Cook_SauceBottle_Mayo_01", "마요네즈", StoreItemCategory.Daily, 3400, 4),
                new StoreItemRecord("SM_Cook_SpiceJar_01", "조미료 통", StoreItemCategory.Daily, 2800, 7),
                new StoreItemRecord("SM_Cook_PepperShaker_01", "후추통", StoreItemCategory.Daily, 2900, 7),
                new StoreItemRecord("SM_Cook_Basket_Blue_01", "다용도 바구니 (블루)", StoreItemCategory.Daily, 3000, 5),
                new StoreItemRecord("SM_Cook_Basket_Green_01", "다용도 바구니 (그린)", StoreItemCategory.Daily, 3000, 5),
                new StoreItemRecord("SM_Cook_Basket_Yellow_01", "다용도 바구니 (옐로우)", StoreItemCategory.Daily, 3000, 4),
                new StoreItemRecord("SM_Clutter_Bottle_Amber_01", "식용유 / 세제 병", StoreItemCategory.Daily, 4500, 7),

                // 5. 아이스크림류 (Item_IceCream) - 10종
                new StoreItemRecord("Vanilla_Ice_Cream_1", "바닐라 아이스크림", StoreItemCategory.IceCream, 3000, 2),
                new StoreItemRecord("Chocolate_Ice_Cream_1", "초콜릿 아이스크림", StoreItemCategory.IceCream, 3000, 2),
                new StoreItemRecord("Strawberry_Ice_Cream_1", "딸기 아이스크림", StoreItemCategory.IceCream, 3000, 2),
                new StoreItemRecord("IceCream", "콘 아이스크림", StoreItemCategory.IceCream, 2500, 2),
                new StoreItemRecord("Red_Popsicle", "딸기/수박 아이스바", StoreItemCategory.IceCream, 1500, 2),
                new StoreItemRecord("Blue_Popsicle", "소다 아이스바", StoreItemCategory.IceCream, 1500, 2),
                new StoreItemRecord("Green_Popsicle", "멜론 아이스바", StoreItemCategory.IceCream, 1500, 2),
                new StoreItemRecord("Orange_Popsicle", "오렌지 아이스바", StoreItemCategory.IceCream, 1500, 2),
                new StoreItemRecord("Yellow_Popsicle", "레몬 아이스바", StoreItemCategory.IceCream, 1500, 2),
                new StoreItemRecord("Chocolate_Popsicle_1", "초코 아이스바", StoreItemCategory.IceCream, 1500, 2),

                // 6. FF / 핫바 / 즉석식품 (Item_FreshFood) - 6종
                new StoreItemRecord("Hambuger", "편의점 햄버거", StoreItemCategory.FreshFood, 3600, 4),
                new StoreItemRecord("HamEgg", "햄에그 샌드위치", StoreItemCategory.FreshFood, 2900, 4),
                new StoreItemRecord("Medium_Coffee_1", "테이크아웃 커피 (M)", StoreItemCategory.FreshFood, 1800, 4),
                new StoreItemRecord("SM_Cook_HerbTray_Green_01", "샐러드 도시락", StoreItemCategory.FreshFood, 4900, 4),
                new StoreItemRecord("SM_Cook_HerbTray_Red_01", "김밥/덮밥 도시락", StoreItemCategory.FreshFood, 4500, 4),
                new StoreItemRecord("SM_Cook_HerbTray_Yellow_01", "핫바/치킨 도시락", StoreItemCategory.FreshFood, 4800, 4),

                // 7. 담배류 (Item_Cigarette) - 1종
                new StoreItemRecord("CigPack", "담배 (한 갑)", StoreItemCategory.Cigarette, 4500, 48)
            };
        }

        public static string GetCategoryLabelKorean(StoreItemCategory category)
        {
            switch (category)
            {
                case StoreItemCategory.Drink: return "음료";
                case StoreItemCategory.Snack: return "스낵/빵";
                case StoreItemCategory.Ramen: return "라면";
                case StoreItemCategory.Daily: return "생필품";
                case StoreItemCategory.IceCream: return "아이스크림";
                case StoreItemCategory.FreshFood: return "즉석식품(FF)";
                case StoreItemCategory.Cigarette: return "담배";
                default: return "기타";
            }
        }
    }

    /// <summary>
    /// 각 물품의 단가·현재 재고·예약(손님이 집어 든) 수량·당일/누적 판매량·태그별 판매 집계 및
    /// 판매 로그 파일 저장/불러오기를 관리하는 순수 C# 원장.
    /// </summary>
    public sealed class StoreInventoryLedger
    {
        private readonly List<StoreItemRecord> items = new List<StoreItemRecord>();
        private readonly Dictionary<string, StoreItemRecord> byId = new Dictionary<string, StoreItemRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly List<StoreSalesLogEntry> salesLog = new List<StoreSalesLogEntry>();

        public IReadOnlyList<StoreItemRecord> Items => items;
        public IReadOnlyList<StoreSalesLogEntry> SalesLog => salesLog;
        public int TodayRevenueWon { get; private set; }
        public int CumulativeRevenueWon { get; private set; }
        public int TodayUnitsSold { get; private set; }
        public int TotalUnitsSold { get; private set; }

        public int TotalCurrentStock
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < items.Count; i++) sum += items[i].CurrentStock;
                return sum;
            }
        }

        public StoreInventoryLedger(IEnumerable<StoreItemRecord> initialItems = null)
        {
            LoadItems(initialItems ?? StoreInventoryCatalog.CreateDefaultKoreanStoreItems());
        }

        public void LoadItems(IEnumerable<StoreItemRecord> source)
        {
            items.Clear();
            byId.Clear();
            salesLog.Clear();
            TodayRevenueWon = 0;
            CumulativeRevenueWon = 0;
            TodayUnitsSold = 0;
            TotalUnitsSold = 0;

            if (source == null) return;
            foreach (StoreItemRecord src in source)
            {
                if (src == null || string.IsNullOrWhiteSpace(src.ItemId) || byId.ContainsKey(src.ItemId)) continue;
                StoreItemRecord copy = new StoreItemRecord(
                    src.ItemId,
                    src.DisplayName,
                    src.Category,
                    src.UnitPriceWon,
                    src.CurrentStock)
                {
                    MaxStock = Math.Max(1, Math.Max(src.MaxStock, src.CurrentStock)),
                    ReservedOnFloorCount = Math.Max(0, src.ReservedOnFloorCount),
                    TodaySoldCount = Math.Max(0, src.TodaySoldCount),
                    TotalSoldCount = Math.Max(0, src.TotalSoldCount)
                };
                items.Add(copy);
                byId[copy.ItemId] = copy;
                TodayUnitsSold += copy.TodaySoldCount;
                TotalUnitsSold += copy.TotalSoldCount;
                TodayRevenueWon += copy.TodaySalesWon;
                CumulativeRevenueWon += copy.TotalSalesWon;
            }
        }

        public bool TryGetItem(string itemId, out StoreItemRecord record)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                record = null;
                return false;
            }
            return byId.TryGetValue(itemId, out record);
        }

        public void SetInitialStock(string itemId, int stock)
        {
            if (TryGetItem(itemId, out StoreItemRecord record))
            {
                int clamped = Math.Max(0, stock);
                record.CurrentStock = clamped;
                record.MaxStock = Math.Max(1, clamped);
            }
        }

        /// <summary>
        /// 손님 AI의 CanTakeFromShelf 콜백용 조회 전용 메서드 (부작용 없음).
        /// </summary>
        public bool HasStockForTag(string productTag)
        {
            if (!StoreInventoryCatalog.TryParseCategoryTag(productTag, out StoreItemCategory category))
            {
                return false;
            }

            return GetCategoryStock(category) > 0;
        }

        public int GetCategoryStock(StoreItemCategory category)
        {
            int sum = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Category == category)
                {
                    sum += items[i].CurrentStock;
                }
            }
            return sum;
        }

        public int GetCategoryMaxStock(StoreItemCategory category)
        {
            int sum = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Category == category)
                {
                    sum += items[i].MaxStock;
                }
            }
            return sum;
        }

        /// <summary>
        /// 손님이 매대에서 물건을 집을 때(ProductPickedUp) 해당 태그 카테고리 재고를 1개 즉시 차감하고,
        /// 결제 전까지 예약 상태로 보관한다. (매출은 결제 확정 시점에 반영)
        /// </summary>
        public bool TryPickupFromShelfByTag(string productTag, int seed, out StoreItemRecord pickedRecord)
        {
            pickedRecord = null;
            if (!StoreInventoryCatalog.TryParseCategoryTag(productTag, out StoreItemCategory category))
            {
                return false;
            }

            List<StoreItemRecord> candidates = new List<StoreItemRecord>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Category == category && items[i].CurrentStock > 0)
                {
                    candidates.Add(items[i]);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            int idx = Math.Abs(seed) % candidates.Count;
            StoreItemRecord chosen = candidates[idx];
            chosen.CurrentStock -= 1;
            chosen.ReservedOnFloorCount += 1;
            pickedRecord = chosen;
            return true;
        }

        /// <summary>
        /// 손님이 결제 전에 퇴장하거나 씬이 리셋될 때 집었던 물건 재고를 매대로 복원한다.
        /// </summary>
        public bool ReturnPickedItemToShelf(string itemId)
        {
            if (!TryGetItem(itemId, out StoreItemRecord record) || record.ReservedOnFloorCount <= 0)
            {
                return false;
            }

            record.ReservedOnFloorCount -= 1;
            record.CurrentStock = Math.Min(record.MaxStock, record.CurrentStock + 1);
            return true;
        }

        /// <summary>
        /// 손님이 이미 매대에서 집어 재고가 -1 차감된 예약 물품을 포스기/알바 계산 완료 시 매출로 확정한다.
        /// </summary>
        public bool CommitPickedItemCheckout(
            string itemId,
            int dayNumber,
            int customerId,
            string processedBy,
            string gameClockText,
            out StoreItemRecord soldRecord,
            out int revenueWon)
        {
            soldRecord = null;
            revenueWon = 0;
            if (!TryGetItem(itemId, out StoreItemRecord record))
            {
                return false;
            }

            if (record.ReservedOnFloorCount > 0)
            {
                record.ReservedOnFloorCount -= 1;
            }
            else if (record.CurrentStock > 0)
            {
                record.CurrentStock -= 1;
            }
            else
            {
                return false;
            }

            record.TodaySoldCount += 1;
            record.TotalSoldCount += 1;
            revenueWon = record.UnitPriceWon;
            TodayUnitsSold += 1;
            TotalUnitsSold += 1;
            TodayRevenueWon += revenueWon;
            CumulativeRevenueWon += revenueWon;
            soldRecord = record;

            salesLog.Add(new StoreSalesLogEntry
            {
                DayNumber = Math.Max(1, dayNumber),
                CustomerId = customerId,
                ProductTag = record.ProductTag,
                ItemId = record.ItemId,
                DisplayName = record.DisplayName,
                UnitPriceWon = record.UnitPriceWon,
                ProcessedBy = string.IsNullOrWhiteSpace(processedBy) ? "STAFF" : processedBy,
                GameClockText = string.IsNullOrWhiteSpace(gameClockText) ? "08:00" : gameClockText
            });

            return true;
        }

        public bool TrySellItem(string itemId, int quantity, out int revenueWon)
        {
            revenueWon = 0;
            if (quantity <= 0 || !TryGetItem(itemId, out StoreItemRecord record)) return false;
            if (record.CurrentStock < quantity) return false;

            record.CurrentStock -= quantity;
            record.TodaySoldCount += quantity;
            record.TotalSoldCount += quantity;

            revenueWon = record.UnitPriceWon * quantity;
            TodayUnitsSold += quantity;
            TotalUnitsSold += quantity;
            TodayRevenueWon += revenueWon;
            CumulativeRevenueWon += revenueWon;
            return true;
        }

        public bool TrySellAnyInStockItem(int seed, out StoreItemRecord soldRecord, out int revenueWon)
        {
            soldRecord = null;
            revenueWon = 0;

            List<StoreItemRecord> available = new List<StoreItemRecord>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].CurrentStock > 0) available.Add(items[i]);
            }

            if (available.Count == 0) return false;

            int index = Math.Abs(seed) % available.Count;
            StoreItemRecord chosen = available[index];
            if (!TrySellItem(chosen.ItemId, 1, out revenueWon)) return false;

            soldRecord = chosen;
            return true;
        }

        public bool RestockItem(string itemId, int quantity)
        {
            if (quantity <= 0 || !TryGetItem(itemId, out StoreItemRecord record)) return false;
            record.CurrentStock = Math.Min(record.MaxStock, record.CurrentStock + quantity);
            return true;
        }

        /// <summary>
        /// 지정한 태그/카테고리 매대에서 부족한 재고를 상한치(MaxStock)까지 보충한다.
        /// </summary>
        public int RestockCategoryToCap(string tagOrCategory, int maxAddTotal = 12)
        {
            if (!StoreInventoryCatalog.TryParseCategoryTag(tagOrCategory, out StoreItemCategory category))
            {
                return 0;
            }

            int addedTotal = 0;
            int budget = Math.Max(1, maxAddTotal);
            for (int i = 0; i < items.Count && budget > 0; i++)
            {
                StoreItemRecord r = items[i];
                if (r.Category != category || r.CurrentStock >= r.MaxStock) continue;
                int deficit = r.MaxStock - r.CurrentStock;
                int add = Math.Min(deficit, budget);
                r.CurrentStock += add;
                budget -= add;
                addedTotal += add;
            }

            return addedTotal;
        }

        public void BeginNewDay()
        {
            TodayRevenueWon = 0;
            TodayUnitsSold = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ReservedOnFloorCount > 0)
                {
                    items[i].CurrentStock = Math.Min(items[i].MaxStock, items[i].CurrentStock + items[i].ReservedOnFloorCount);
                    items[i].ReservedOnFloorCount = 0;
                }
                items[i].TodaySoldCount = 0;
            }
        }

        /// <summary>
        /// 판매 내역(태그별 집계 · 품목별 판매 개수 · 개별 결제 로그)을 파일로 저장한다.
        /// </summary>
        public void SaveSalesLogToFile(string filePath, int currentDayNumber = 1)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"# STORE_SALES_LOG_V1|Day={currentDayNumber}|TodaySold={TodayUnitsSold}|TodayRevenue={TodayRevenueWon}|CumulativeSold={TotalUnitsSold}|CumulativeRevenue={CumulativeRevenueWon}");

            for (int c = 0; c < StoreInventoryCatalog.AllCategories.Length; c++)
            {
                StoreItemCategory cat = StoreInventoryCatalog.AllCategories[c];
                string tag = StoreInventoryCatalog.GetCategoryTag(cat);
                string label = StoreInventoryCatalog.GetCategoryLabelKorean(cat);
                GetCategorySalesSummary(cat, out int todayCnt, out int todayWon, out int totalCnt, out int totalWon, out int remainStock);
                sb.AppendLine($"TAG|{tag}|{label}|TodaySold={todayCnt}|TodayWon={todayWon}|TotalSold={totalCnt}|TotalWon={totalWon}|RemainStock={remainStock}");
            }

            for (int i = 0; i < items.Count; i++)
            {
                StoreItemRecord r = items[i];
                sb.AppendLine($"ITEM|{r.ProductTag}|{r.ItemId}|{r.DisplayName}|UnitPrice={r.UnitPriceWon}|TodaySold={r.TodaySoldCount}|TodayWon={r.TodaySalesWon}|TotalSold={r.TotalSoldCount}|TotalWon={r.TotalSalesWon}|Stock={r.CurrentStock}|MaxStock={r.MaxStock}");
            }

            for (int i = 0; i < salesLog.Count; i++)
            {
                StoreSalesLogEntry e = salesLog[i];
                sb.AppendLine($"SALE|Day={e.DayNumber}|Customer={e.CustomerId}|Tag={e.ProductTag}|Item={e.ItemId}|Name={e.DisplayName}|Price={e.UnitPriceWon}|By={e.ProcessedBy}|Clock={e.GameClockText}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 저장된 판매 로그 파일에서 태그별·품목별 판매 개수와 로그를 불러와 정산용 텍스트로 반환한다.
        /// </summary>
        public static bool TryLoadSalesLogSummaryFromFile(string filePath, out string formattedReport)
        {
            formattedReport = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines == null || lines.Length == 0) return false;

                StringBuilder tagBlock = new StringBuilder();
                StringBuilder itemBlock = new StringBuilder();
                StringBuilder saleBlock = new StringBuilder();
                int soldItemTypes = 0;
                int saleCount = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length < 4) continue;

                    if (parts[0] == "TAG" && parts.Length >= 8)
                    {
                        string tag = parts[1];
                        string kor = parts[2];
                        int todayCnt = ParseKeyValInt(parts[3]);
                        int todayWon = ParseKeyValInt(parts[4]);
                        int remain = ParseKeyValInt(parts[7]);
                        tagBlock.AppendLine($"  • [{tag}] {kor} :  당일 {todayCnt}개 판매  (+{todayWon:N0}원)   [남은 재고 {remain}개]");
                    }
                    else if (parts[0] == "ITEM" && parts.Length >= 10)
                    {
                        string tag = parts[1];
                        string name = parts[3];
                        int unitPrice = ParseKeyValInt(parts[4]);
                        int todayCnt = ParseKeyValInt(parts[5]);
                        int todayWon = ParseKeyValInt(parts[6]);
                        int stock = ParseKeyValInt(parts[9]);
                        if (todayCnt > 0)
                        {
                            soldItemTypes++;
                            itemBlock.AppendLine($"  • [{tag}] {name} (단가 {unitPrice:N0}원)  ×{todayCnt}개  =  +{todayWon:N0}원   (잔여 {stock}개)");
                        }
                    }
                    else if (parts[0] == "SALE" && parts.Length >= 9)
                    {
                        saleCount++;
                        string cust = parts[2].Replace("Customer=", "#");
                        string tag = parts[3].Replace("Tag=", "");
                        string name = parts[5].Replace("Name=", "");
                        int price = ParseKeyValInt(parts[6]);
                        string by = parts[7].Replace("By=", "");
                        string byKor = string.Equals(by, "PLAYER", StringComparison.OrdinalIgnoreCase) ? "점장 직접계산[E]" : "알바 계산";
                        string clock = parts[8].Replace("Clock=", "");
                        saleBlock.AppendLine($"  [{clock}] 손님{cust} · [{tag}] {name} (+{price:N0}원) — {byKor}");
                    }
                }

                StringBuilder report = new StringBuilder();
                report.AppendLine("[ 1. 상품 분류 태그(Item_*)별 판매 집계 (로그 파일 연동) ]");
                report.Append(tagBlock);
                report.AppendLine();
                report.AppendLine($"[ 2. 당일 판매 품목 상세 ({soldItemTypes}종 판매) ]");
                if (soldItemTypes == 0)
                {
                    report.AppendLine("  • 당일 포스기 결제 품목 없음");
                }
                else
                {
                    report.Append(itemBlock);
                }

                if (saleCount > 0)
                {
                    report.AppendLine();
                    report.AppendLine($"[ 3. 포스기 결제 시간순 로그 ({saleCount}건) ]");
                    report.Append(saleBlock);
                }

                formattedReport = report.ToString().TrimEnd();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ParseKeyValInt(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment)) return 0;
            int eq = segment.IndexOf('=');
            string numStr = eq >= 0 ? segment.Substring(eq + 1) : segment;
            return int.TryParse(numStr.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        public void GetCategorySalesSummary(
            StoreItemCategory category,
            out int todaySoldCount,
            out int todaySalesWon,
            out int totalSoldCount,
            out int totalSalesWon,
            out int remainingStock)
        {
            todaySoldCount = 0;
            todaySalesWon = 0;
            totalSoldCount = 0;
            totalSalesWon = 0;
            remainingStock = 0;

            for (int i = 0; i < items.Count; i++)
            {
                StoreItemRecord r = items[i];
                if (r.Category != category) continue;
                todaySoldCount += r.TodaySoldCount;
                todaySalesWon += r.TodaySalesWon;
                totalSoldCount += r.TotalSoldCount;
                totalSalesWon += r.TotalSalesWon;
                remainingStock += r.CurrentStock;
            }
        }

        public string BuildReceiptDetailReport(int maxItemLines = int.MaxValue)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[ 1. 상품 분류 태그(Item_*)별 당일 판매 집계 · 총 {TodayUnitsSold}개 / +{TodayRevenueWon:N0}원 ]");

            for (int c = 0; c < StoreInventoryCatalog.AllCategories.Length; c++)
            {
                StoreItemCategory cat = StoreInventoryCatalog.AllCategories[c];
                string tag = StoreInventoryCatalog.GetCategoryTag(cat);
                string kor = StoreInventoryCatalog.GetCategoryLabelKorean(cat);
                GetCategorySalesSummary(cat, out int todayCnt, out int todayWon, out _, out _, out int remainStock);
                sb.AppendLine($"  • [{tag}] {kor} :  {todayCnt}개 판매 (+{todayWon:N0}원)   [남은 재고 {remainStock}개]");
            }

            List<StoreItemRecord> soldToday = new List<StoreItemRecord>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].TodaySoldCount > 0)
                {
                    soldToday.Add(items[i]);
                }
            }

            soldToday.Sort((a, b) => b.TodaySalesWon.CompareTo(a.TodaySalesWon));

            sb.AppendLine();
            sb.AppendLine($"[ 2. 당일 판매된 개별 물품 내역 ({soldToday.Count}종) ]");

            if (soldToday.Count == 0)
            {
                sb.AppendLine("  • 당일 직접 판매 기록 없음");
            }
            else
            {
                int limit = Math.Min(soldToday.Count, Math.Max(1, maxItemLines));
                for (int i = 0; i < limit; i++)
                {
                    StoreItemRecord r = soldToday[i];
                    sb.AppendLine($"  • [{r.ProductTag}] {r.DisplayName} (단가 {r.UnitPriceWon:N0}원)  ×{r.TodaySoldCount}개  =  +{r.TodaySalesWon:N0}원  (잔여 {r.CurrentStock}개)");
                }
                if (soldToday.Count > limit)
                {
                    sb.AppendLine($"  … 외 {soldToday.Count - limit}개 품목 판매");
                }
            }

            if (salesLog.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[ 3. 포스기 결제 로그 ({salesLog.Count}건) ]");
                for (int i = 0; i < salesLog.Count; i++)
                {
                    StoreSalesLogEntry e = salesLog[i];
                    string byKor = string.Equals(e.ProcessedBy, "PLAYER", StringComparison.OrdinalIgnoreCase)
                        ? "점장 직접계산[E]"
                        : "알바 계산";
                    sb.AppendLine($"  [{e.GameClockText}] 손님#{e.CustomerId} · [{e.ProductTag}] {e.DisplayName} (+{e.UnitPriceWon:N0}원) — {byKor}");
                }
            }

            sb.AppendLine();
            sb.Append($"전체 남은 재고: {TotalCurrentStock:N0}개   |   누적 물품 판매 총액: {CumulativeRevenueWon:N0}원");
            return sb.ToString();
        }
    }
}
