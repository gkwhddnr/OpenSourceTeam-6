using System;
using System.Collections.Generic;
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
    public sealed class StoreItemRecord
    {
        public string ItemId;
        public string DisplayName;
        public StoreItemCategory Category;
        public int UnitPriceWon;
        public int CurrentStock;
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
            UnitPriceWon = Math.Max(0, unitPriceWon);
            CurrentStock = Math.Max(0, initialStock);
            TodaySoldCount = 0;
            TotalSoldCount = 0;
        }
    }

    /// <summary>
    /// 2025~2026년 한국 편의점(CU/GS25/세븐일레븐) 실제 시중 물가 기준
    /// 판매 품목 54종(Sellable_Items_Handover.md 01_CurrentlySold) 가격·카테고리 마스터 카탈로그.
    /// </summary>
    public static class StoreInventoryCatalog
    {
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
    /// 각 물품의 단가·현재 재고·당일 판매량·누적 판매량 및 당일/누적 총 매출을 관리하는 순수 C# 원장.
    /// </summary>
    public sealed class StoreInventoryLedger
    {
        private readonly List<StoreItemRecord> items = new List<StoreItemRecord>();
        private readonly Dictionary<string, StoreItemRecord> byId = new Dictionary<string, StoreItemRecord>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<StoreItemRecord> Items => items;
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
                record.CurrentStock = Math.Max(0, stock);
            }
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
            record.CurrentStock += quantity;
            return true;
        }

        public void BeginNewDay()
        {
            TodayRevenueWon = 0;
            TodayUnitsSold = 0;
            for (int i = 0; i < items.Count; i++)
            {
                items[i].TodaySoldCount = 0;
            }
        }

        public string BuildReceiptDetailReport(int maxItemLines = 8)
        {
            List<StoreItemRecord> soldToday = new List<StoreItemRecord>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].TodaySoldCount > 0)
                {
                    soldToday.Add(items[i]);
                }
            }

            soldToday.Sort((a, b) => b.TodaySalesWon.CompareTo(a.TodaySalesWon));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[ 당일 품목별 판매 내역 · 총 {TodayUnitsSold}개 / {TodayRevenueWon:N0}원 ]");

            if (soldToday.Count == 0)
            {
                sb.AppendLine("  • 당일 직접 판매 기록 없음 (업무 기여 매출만 집계)");
            }
            else
            {
                int limit = Math.Min(soldToday.Count, Math.Max(1, maxItemLines));
                for (int i = 0; i < limit; i++)
                {
                    StoreItemRecord r = soldToday[i];
                    sb.AppendLine($"  • {r.DisplayName}  ×{r.TodaySoldCount}  ({r.TodaySalesWon:N0}원 · 잔여 {r.CurrentStock}개)");
                }
                if (soldToday.Count > limit)
                {
                    sb.AppendLine($"  … 외 {soldToday.Count - limit}개 품목 판매");
                }
            }

            sb.Append($"전체 남은 재고: {TotalCurrentStock:N0}개   |   누적 총 매출: {CumulativeRevenueWon:N0}원");
            return sb.ToString();
        }
    }
}
