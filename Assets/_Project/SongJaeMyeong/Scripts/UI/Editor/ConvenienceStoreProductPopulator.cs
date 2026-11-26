using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenSourceTeam6.UI.Editor
{
    /// <summary>
    /// 편의점 내부 설계도(편의점 내부 설계.pdf) 및 판매 물품 정리표(Sellable_Items_Handover.md)에 따라
    /// 기존 프로토타입 큐브를 제거하고 7개 구역(워크인/스낵/라면/생필품/아이스크림/FF·핫바/담배)에
    /// 실제 판매 물품 프리팹(01_CurrentlySold)을 배치하며, 미포장 음식에는 반투명 플라스틱 포장 큐브를 씌운다.
    /// 설계도에 없는 예비 물품(02_FutureReserve)은 제외한다.
    /// </summary>
    public static class ConvenienceStoreProductPopulator
    {
        public const string PopulatedMarkerName = "Store_RealSellableItems";
        public const string GroupedZonesMarkerName = "GroupedShelfZones_V3";

        private const string SoldRoot = "Assets/SellableItems/01_CurrentlySold";
        private const string AsianStreetFoodPalettePath = "Assets/ThirdParty/Shared/AsianStreetFoodPack/Textures/palette_1024.png";
        private const string PackageMatPath = "Assets/_Project/SongJaeMyeong/Assets/UI/Mat_FoodClearPackage.mat";
        private const string ShowcaseGlassMatPath = "Assets/_Project/SongJaeMyeong/Assets/UI/Mat_HotFoodShowcaseGlass.mat";
        private const string ShelfBoardMatPath = "Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/White.mat";
        private const string DarkDividerMatPath = "Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Dark.mat";
        private const string GlassMatPath = "Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Glass.mat";
        private const string UrpCacheDir = "Assets/_Project/SongJaeMyeong/Assets/UI/ItemUrpMaterials";

        private static readonly string[] DrinkPrefabs =
        {
            SoldRoot + "/01_Drink/P_RSG_WaterBottle.prefab",
            SoldRoot + "/01_Drink/P_RSG_SodaCan.prefab",
            SoldRoot + "/01_Drink/Milk.prefab",
            SoldRoot + "/01_Drink/Orange_Juice_1.prefab",
            SoldRoot + "/01_Drink/Green_Juice_1.prefab",
            SoldRoot + "/01_Drink/Red_Juice_1.prefab",
            SoldRoot + "/01_Drink/SM_Clutter_Bottle_Blue_01.prefab",
            SoldRoot + "/01_Drink/SM_Clutter_Bottle_Green_01.prefab"
        };

        // 단별(하단→상단) 배치: 스낵 10종 모두 단당 1회씩만 고유 구역으로 진열 (한 매대 내 중복 진열 금지)
        private static readonly string[][] SnackTierPrefabs =
        {
            new[]
            {
                SoldRoot + "/02_Snack/Cake.prefab",
                SoldRoot + "/02_Snack/Waffle.prefab",
                SoldRoot + "/02_Snack/Donuts.prefab"
            },
            new[]
            {
                SoldRoot + "/02_Snack/Chocolate_Donut_Sprinkles_1.prefab",
                SoldRoot + "/02_Snack/Strawberry_Donut_Sprinkles_1.prefab"
            },
            new[]
            {
                SoldRoot + "/02_Snack/White_Donut_Sprinkles_1.prefab",
                SoldRoot + "/02_Snack/Purple_Donut_1.prefab"
            },
            new[]
            {
                SoldRoot + "/02_Snack/Chocolate_Bar_1.prefab",
                SoldRoot + "/02_Snack/Dark_Chocolate_Bar_1.prefab",
                SoldRoot + "/02_Snack/Milk_Chocolate_Bar_1.prefab"
            }
        };

        // 단별(하단→상단) 배치: 라면·즉석면 7종 카탈로그 품목을 중복 없이 단별 고유 구역으로 배치 (용기면 5종은 포장 색상 별도 구분)
        private static readonly string[][] RamenTierPrefabs =
        {
            new[]
            {
                SoldRoot + "/03_Ramen/P_RSG_FoodCan.prefab",
                SoldRoot + "/03_Ramen/SM_Cook_BowlStack_01.prefab"
            },
            new[]
            {
                SoldRoot + "/03_Ramen/SM_Cook_PhoBowl_Full_01.prefab",
                SoldRoot + "/03_Ramen/SM_Cook_PhoBowl_Empty_01.prefab"
            },
            new[]
            {
                SoldRoot + "/03_Ramen/SM_Cook_SideBowl_Lime_01.prefab",
                SoldRoot + "/03_Ramen/SM_Cook_SideBowl_01.prefab"
            },
            new[]
            {
                SoldRoot + "/03_Ramen/SM_Cook_ChopstickBundle_01.prefab"
            }
        };

        // 단별(하단→상단) 배치: 생필품 11종 모두 단별 고유 구역으로 진열 (한 매대 내 중복 진열 금지)
        private static readonly string[][] DailyTierPrefabs =
        {
            new[]
            {
                SoldRoot + "/04_Daily/SM_Cook_Basket_Blue_01.prefab",
                SoldRoot + "/04_Daily/SM_Cook_Basket_Green_01.prefab",
                SoldRoot + "/04_Daily/SM_Cook_Basket_Yellow_01.prefab"
            },
            new[]
            {
                SoldRoot + "/04_Daily/SM_Cook_SpiceJar_01.prefab",
                SoldRoot + "/04_Daily/SM_Clutter_Bottle_Amber_01.prefab"
            },
            new[]
            {
                SoldRoot + "/04_Daily/SM_Cook_SauceBottle_Chili_01.prefab",
                SoldRoot + "/04_Daily/SM_Cook_SauceBottle_Fish_01.prefab",
                SoldRoot + "/04_Daily/SM_Cook_SauceBottle_Hoisin_01.prefab",
                SoldRoot + "/04_Daily/SM_Cook_SauceBottle_Mayo_01.prefab"
            },
            new[]
            {
                SoldRoot + "/04_Daily/SM_Cook_NapkinBox_01.prefab",
                SoldRoot + "/04_Daily/SM_Cook_PepperShaker_01.prefab"
            }
        };

        // 아이스크림 10종 (컵 4종 + 바 6종)을 5개 바스켓 × 좌/우 = 10칸에 중복 없이 1:1 배치
        private static readonly string[] IceCreamAllPrefabs =
        {
            SoldRoot + "/05_IceCream/Vanilla_Ice_Cream_1.prefab",
            SoldRoot + "/05_IceCream/Red_Popsicle.prefab",
            SoldRoot + "/05_IceCream/Chocolate_Ice_Cream_1.prefab",
            SoldRoot + "/05_IceCream/Blue_Popsicle.prefab",
            SoldRoot + "/05_IceCream/Strawberry_Ice_Cream_1.prefab",
            SoldRoot + "/05_IceCream/Green_Popsicle.prefab",
            SoldRoot + "/05_IceCream/IceCream.prefab",
            SoldRoot + "/05_IceCream/Orange_Popsicle.prefab",
            SoldRoot + "/05_IceCream/Yellow_Popsicle.prefab",
            SoldRoot + "/05_IceCream/Chocolate_Popsicle_1.prefab"
        };

        // FF/핫바 6칸 쇼케이스 매칭
        private static readonly string[] FreshFoodShowcasePrefabs =
        {
            SoldRoot + "/06_FreshFood_FF/Hambuger.prefab",
            SoldRoot + "/06_FreshFood_FF/HamEgg.prefab",
            SoldRoot + "/06_FreshFood_FF/SM_Cook_HerbTray_Green_01.prefab",
            SoldRoot + "/06_FreshFood_FF/SM_Cook_HerbTray_Red_01.prefab",
            SoldRoot + "/06_FreshFood_FF/SM_Cook_HerbTray_Yellow_01.prefab",
            SoldRoot + "/06_FreshFood_FF/Medium_Coffee_1.prefab"
        };

        private const string CigarettePrefabPath = SoldRoot + "/07_Cigarette/CigPack.prefab";

        // 포장이 되어있지 않은 맨 음식/디저트/즉석식품/아이스크림 -> 반투명 플라스틱 큐브 포장 적용
        private static readonly HashSet<string> NeedsClearPackageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Chocolate_Bar_1",
            "Dark_Chocolate_Bar_1",
            "Milk_Chocolate_Bar_1",
            "Chocolate_Donut_Sprinkles_1",
            "Strawberry_Donut_Sprinkles_1",
            "White_Donut_Sprinkles_1",
            "Purple_Donut_1",
            "Cake",
            "Waffle",
            "Donuts",
            "SM_Cook_PhoBowl_Full_01",
            "SM_Cook_PhoBowl_Empty_01",
            "SM_Cook_BowlStack_01",
            "SM_Cook_SideBowl_01",
            "SM_Cook_SideBowl_Lime_01",
            "Vanilla_Ice_Cream_1",
            "Chocolate_Ice_Cream_1",
            "Strawberry_Ice_Cream_1",
            "IceCream",
            "Red_Popsicle",
            "Blue_Popsicle",
            "Green_Popsicle",
            "Orange_Popsicle",
            "Yellow_Popsicle",
            "Chocolate_Popsicle_1",
            "Hambuger",
            "HamEgg",
            "SM_Cook_HerbTray_Green_01",
            "SM_Cook_HerbTray_Red_01",
            "SM_Cook_HerbTray_Yellow_01"
        };

        private static readonly Color[] CigaretteTintColors =
        {
            new Color(0.84f, 0.22f, 0.20f, 1f),
            new Color(0.20f, 0.44f, 0.78f, 1f),
            new Color(0.22f, 0.62f, 0.42f, 1f),
            new Color(0.86f, 0.68f, 0.22f, 1f),
            new Color(0.26f, 0.27f, 0.31f, 1f),
            new Color(0.92f, 0.90f, 0.86f, 1f)
        };

        public static bool HasSellableAssetsImported()
        {
            return Directory.Exists(SoldRoot) &&
                   File.Exists(SoldRoot + "/02_Snack/Cake.prefab") &&
                   File.Exists(SoldRoot + "/06_FreshFood_FF/Hambuger.prefab");
        }

        public static bool NeedsProductShelfRefresh(GameObject populatedMarker)
        {
            return populatedMarker == null ||
                   populatedMarker.transform.Find(GroupedZonesMarkerName) == null;
        }

        public static Dictionary<string, int> CollectExistingStockCounts(GameObject mapRoot)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (mapRoot == null) return counts;

            Transform marker = mapRoot.transform.Find(PopulatedMarkerName);
            if (marker == null) return counts;

            Dictionary<Material, Material> urpMatCache = new Dictionary<Material, Material>();
            EnsureUrpMaterialsOnRenderers(marker.gameObject, urpMatCache);

            for (int g = 0; g < marker.childCount; g++)
            {
                Transform group = marker.GetChild(g);
                if (group == null || group.name == GroupedZonesMarkerName) continue;

                for (int i = 0; i < group.childCount; i++)
                {
                    string childName = group.GetChild(i).name;
                    if (string.IsNullOrWhiteSpace(childName) ||
                        childName.StartsWith("WalkIn_InnerShelf_", StringComparison.Ordinal) ||
                        childName.StartsWith("ShelfZoneDivider", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string itemId = childName.StartsWith("Item_", StringComparison.Ordinal)
                        ? childName.Substring("Item_".Length)
                        : childName;
                    if (string.IsNullOrWhiteSpace(itemId)) continue;

                    counts.TryGetValue(itemId, out int current);
                    counts[itemId] = current + 1;
                }
            }

            return counts;
        }

        public static Dictionary<string, int> PopulateMap(GameObject mapRoot)
        {
            Dictionary<string, int> spawnedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (mapRoot == null || !HasSellableAssetsImported())
            {
                return spawnedCounts;
            }

            Material clearPackageMat = EnsureClearPackageMaterial(PackageMatPath, new Color(0.95f, 0.98f, 1f, 0.24f), smoothness: 0.90f);
            Material showcaseGlassMat = EnsureClearPackageMaterial(ShowcaseGlassMatPath, new Color(0.92f, 0.97f, 1f, 0.16f), smoothness: 0.94f);
            Material dividerMat = AssetDatabase.LoadAssetAtPath<Material>(DarkDividerMatPath);
            Dictionary<Material, Material> urpMatCache = new Dictionary<Material, Material>();

            Transform existingMarker = mapRoot.transform.Find(PopulatedMarkerName);
            List<(Vector3 floorPos, Vector3 scale)> preservedCig1 = HarvestExistingCigaretteSlots(existingMarker, "Checkout1_Cigarettes");
            List<(Vector3 floorPos, Vector3 scale)> preservedCig2 = HarvestExistingCigaretteSlots(existingMarker, "Checkout2_Cigarettes");
            if (existingMarker != null)
            {
                Object.DestroyImmediate(existingMarker.gameObject);
            }

            GameObject markerRoot = new GameObject(PopulatedMarkerName);
            markerRoot.transform.SetParent(mapRoot.transform, false);

            GameObject groupedVersionMarker = new GameObject(GroupedZonesMarkerName);
            groupedVersionMarker.transform.SetParent(markerRoot.transform, false);

            int totalPlaced = 0;

            // 1. 스낵류 곤돌라 (Snack_Shelf) - 구역별 3~4개 동일 품목 정렬 배치
            Transform snackShelf = mapRoot.transform.Find("Snack_Shelf");
            if (snackShelf != null)
            {
                totalPlaced += ReplaceGondolaShelfProducts(
                    snackShelf,
                    markerRoot.transform,
                    "Snack_Items",
                    SnackTierPrefabs,
                    maxSlotSize: new Vector3(0.22f, 0.24f, 0.22f),
                    clearPackageMat,
                    dividerMat,
                    urpMatCache,
                    spawnedCounts);
            }

            // 2. 라면류 곤돌라 (Ramen_Shelf) - 구역별 3~4개 동일 품목 정렬 배치
            Transform ramenShelf = mapRoot.transform.Find("Ramen_Shelf");
            if (ramenShelf != null)
            {
                totalPlaced += ReplaceGondolaShelfProducts(
                    ramenShelf,
                    markerRoot.transform,
                    "Ramen_Items",
                    RamenTierPrefabs,
                    maxSlotSize: new Vector3(0.22f, 0.25f, 0.22f),
                    clearPackageMat,
                    dividerMat,
                    urpMatCache,
                    spawnedCounts);
            }

            // 3. 생필품 곤돌라 (DailyGoods_Shelf) - 구역별 3~4개 동일 품목 정렬 배치
            Transform dailyShelf = mapRoot.transform.Find("DailyGoods_Shelf");
            if (dailyShelf != null)
            {
                totalPlaced += ReplaceGondolaShelfProducts(
                    dailyShelf,
                    markerRoot.transform,
                    "Daily_Items",
                    DailyTierPrefabs,
                    maxSlotSize: new Vector3(0.22f, 0.27f, 0.22f),
                    clearPackageMat,
                    dividerMat,
                    urpMatCache,
                    spawnedCounts);
            }

            // 4. 워크인 냉장고 음료류 (WalkIn_and_Storage) - 유리문 구역별 동일 음료 4개 정렬 배치
            Transform walkIn = mapRoot.transform.Find("WalkIn_and_Storage");
            if (walkIn != null)
            {
                totalPlaced += PopulateWalkInCoolerDrinks(walkIn, markerRoot.transform, clearPackageMat, dividerMat, urpMatCache, spawnedCounts);
            }

            // 5. 아이스크림 냉동고 (IceCream_Freezer) - 바스켓 구획별 2×2(4개) 동일 품목 배치
            Transform iceCreamFreezer = mapRoot.transform.Find("IceCream_Freezer");
            if (iceCreamFreezer != null)
            {
                totalPlaced += PopulateIceCreamFreezer(iceCreamFreezer, markerRoot.transform, clearPackageMat, dividerMat, urpMatCache, spawnedCounts);
            }

            // 6. FF · 핫바류 오픈 쇼케이스 (HotFood) - 각 쇼케이스 칸마다 2×2(4개) 동일 품목 배치
            Transform hotFood = mapRoot.transform.Find("HotFood");
            if (hotFood != null)
            {
                totalPlaced += PopulateHotFoodShowcases(hotFood, markerRoot.transform, clearPackageMat, showcaseGlassMat, urpMatCache, spawnedCounts);
            }

            // 7. 계산대 1·2 뒤 담배 진열대 (Checkout_1, Checkout_2) - 브랜드 색상별 3갑씩 정렬 묶음 배치
            Transform checkout1 = mapRoot.transform.Find("Checkout_1");
            if (checkout1 != null)
            {
                totalPlaced += ReplaceCigarettePacks(checkout1, markerRoot.transform, "Checkout1_Cigarettes", preservedCig1, urpMatCache, spawnedCounts);
            }

            Transform checkout2 = mapRoot.transform.Find("Checkout_2");
            if (checkout2 != null)
            {
                totalPlaced += ReplaceCigarettePacks(checkout2, markerRoot.transform, "Checkout2_Cigarettes", preservedCig2, urpMatCache, spawnedCounts);
            }

            Debug.Log($"[OpenSourceTeam6.UI] 매대 구역 분할 및 동일 품목 3~4개 정렬 진열 완료! (총 {totalPlaced}개)");
            return spawnedCounts;
        }

        private static int ReplaceGondolaShelfProducts(
            Transform shelfRoot,
            Transform outputParent,
            string groupName,
            string[][] tierPrefabPaths,
            Vector3 maxSlotSize,
            Material clearPackageMat,
            Material dividerMat,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            List<float> rawBoardTopYs = new List<float>();
            List<GameObject> toDestroy = new List<GameObject>();

            float sumBoardX = 0f;
            float sumBoardZ = 0f;
            int boardCount = 0;
            float boardWidthX = 0.88f;
            float boardLenZ = 3.40f;
            float spineHalfX = 0.04f;

            for (int i = 0; i < shelfRoot.childCount; i++)
            {
                Transform child = shelfRoot.GetChild(i);
                if (child.name == "Product")
                {
                    toDestroy.Add(child.gameObject);
                }
                else if (child.name == "Shelf_Board")
                {
                    float topY = child.position.y + Mathf.Abs(child.lossyScale.y) * 0.5f;
                    rawBoardTopYs.Add(topY);
                    sumBoardX += child.position.x;
                    sumBoardZ += child.position.z;
                    boardWidthX = Mathf.Max(boardWidthX, Mathf.Abs(child.lossyScale.x));
                    boardLenZ = Mathf.Max(boardLenZ, Mathf.Abs(child.lossyScale.z));
                    boardCount++;
                }
                else if (child.name == "Shelf_Center")
                {
                    spineHalfX = Mathf.Clamp(Mathf.Abs(child.lossyScale.x) * 0.5f, 0.02f, 0.10f);
                }
                else if (child.name == "Shelf_Base" && boardCount == 0)
                {
                    boardLenZ = Mathf.Max(boardLenZ, Mathf.Abs(child.lossyScale.z));
                }
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                Object.DestroyImmediate(toDestroy[i]);
            }

            float centerX = boardCount > 0 ? sumBoardX / boardCount : shelfRoot.position.x;
            float centerZ = boardCount > 0 ? sumBoardZ / boardCount : shelfRoot.position.z;

            List<float> boardTopYs = DeduplicateSortedFloats(rawBoardTopYs, 0.08f);
            if (boardTopYs.Count == 0)
            {
                boardTopYs.AddRange(new[] { 0.415f, 0.815f, 1.215f, 1.615f });
            }

            GameObject group = new GameObject(groupName);
            group.transform.SetParent(outputParent, false);

            // 양면 곤돌라 선반 폭 계산 (중앙 격벽과 선반 외곽 사이에 안전하게 안착)
            float halfBoardX = Mathf.Max(0.36f, boardWidthX * 0.5f);
            float ledgeDepthX = Mathf.Max(0.16f, halfBoardX - spineHalfX);
            float sideOffsetX = spineHalfX + ledgeDepthX * 0.52f;
            float maxDepthX = Mathf.Clamp(ledgeDepthX * 0.82f, 0.14f, maxSlotSize.x);

            // 선반 길이(Z)를 단별 품목 수에 맞춰 중복 없는 독립 구역으로 균등 분할
            const float endMarginZ = 0.22f;
            float zMin = centerZ - boardLenZ * 0.5f + endMarginZ;
            float zMax = centerZ + boardLenZ * 0.5f - endMarginZ;
            float usableLenZ = Mathf.Max(2.2f, zMax - zMin);

            // 단별 구역 경계마다 얇은 칸막이(ShelfZoneDivider) 설치 — 물리 충돌체 없음
            for (int t = 0; t < boardTopYs.Count; t++)
            {
                float shelfTopY = boardTopYs[t];
                string[] tierList = tierPrefabPaths[Mathf.Clamp(t, 0, tierPrefabPaths.Length - 1)];
                if (tierList == null || tierList.Length <= 1) continue;

                int zoneCount = tierList.Length;
                float zoneSpanZ = usableLenZ / zoneCount;

                for (int b = 1; b < zoneCount; b++)
                {
                    float boundaryZ = zMin + zoneSpanZ * b;
                    CreateZoneDividerStrip(
                        group.transform,
                        $"ShelfZoneDivider_T{t + 1}_B{b}",
                        new Vector3(centerX, shelfTopY + 0.022f, boundaryZ),
                        new Vector3(boardWidthX * 0.94f, 0.044f, 0.018f),
                        dividerMat);
                }
            }

            int placed = 0;
            for (int sideIdx = 0; sideIdx < 2; sideIdx++)
            {
                float sideSign = sideIdx == 0 ? -1f : 1f;
                float yaw = sideIdx == 0 ? -90f : 90f;
                float laneX = centerX + sideSign * sideOffsetX;

                for (int t = 0; t < boardTopYs.Count; t++)
                {
                    float shelfTopY = boardTopYs[t];
                    string[] tierList = tierPrefabPaths[Mathf.Clamp(t, 0, tierPrefabPaths.Length - 1)];
                    if (tierList == null || tierList.Length == 0) continue;

                    int zoneCount = tierList.Length;
                    float zoneSpanZ = usableLenZ / zoneCount;

                    for (int zIdx = 0; zIdx < zoneCount; zIdx++)
                    {
                        // 같은 면 안에서는 단/구역마다 1품목만 배정 (중복 금지), 반대면은 역순 배치
                        int itemIndex = sideIdx == 0 ? zIdx : (zoneCount - 1) - zIdx;
                        string prefabPath = tierList[itemIndex];
                        bool isWide = IsWideShelfItem(prefabPath);
                        int countInZone = isWide ? 3 : 4;

                        float maxClusterSpan = zoneSpanZ * 0.68f;
                        float desiredStep = isWide ? 0.25f : 0.21f;
                        float itemStepZ = countInZone > 1
                            ? Mathf.Min(desiredStep, maxClusterSpan / Mathf.Max(1f, countInZone - 0.2f))
                            : desiredStep;
                        float fitWidthZ = Mathf.Clamp(itemStepZ - 0.024f, 0.11f, maxSlotSize.z);
                        Vector3 fittedMaxSlot = new Vector3(maxDepthX, maxSlotSize.y, fitWidthZ);

                        float zoneCenterZ = zMin + zoneSpanZ * (zIdx + 0.5f);
                        for (int k = 0; k < countInZone; k++)
                        {
                            float localZ = (k - (countInZone - 1) * 0.5f) * itemStepZ;
                            Vector3 basePos = new Vector3(laneX, shelfTopY, zoneCenterZ + localZ);

                            if (SpawnFittedItem(
                                prefabPath,
                                group.transform,
                                basePos,
                                yaw,
                                fittedMaxSlot,
                                clearPackageMat,
                                urpMatCache,
                                spawnedCounts) != null)
                            {
                                placed++;
                            }
                        }
                    }
                }
            }

            return placed;
        }

        private static int PopulateWalkInCoolerDrinks(
            Transform walkInRoot,
            Transform outputParent,
            Material clearPackageMat,
            Material dividerMat,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            // 확장 맵(Layout_Rebuilt_2026)의 Z 위치·길이를 보존하면서 본체 두께만 뒤쪽 벽으로 얇게 밀어 내부 진열이 보이도록 구성
            Transform coolerBody = walkInRoot.Find("WalkIn_Cooler");
            float coolerCenterZ = 1.58f;
            float coolerLenZ = 3.60f;
            float coolerBackX = -6.86f;
            if (coolerBody != null)
            {
                coolerCenterZ = coolerBody.position.z;
                coolerLenZ = Mathf.Max(3.40f, Mathf.Abs(coolerBody.localScale.z));
                coolerBody.position = new Vector3(coolerBackX, 1.25f, coolerCenterZ);
                coolerBody.localScale = new Vector3(0.16f, 2.50f, coolerLenZ);
            }

            Material glassMat = AssetDatabase.LoadAssetAtPath<Material>(GlassMatPath);
            List<float> rawDoorZs = new List<float>();
            float doorX = -6.30f;
            for (int i = 0; i < walkInRoot.childCount; i++)
            {
                Transform child = walkInRoot.GetChild(i);
                if (child.name == "Fridge_Door")
                {
                    rawDoorZs.Add(child.position.z);
                    doorX = child.position.x;
                    if (glassMat != null)
                    {
                        Renderer r = child.GetComponent<Renderer>();
                        if (r != null) r.sharedMaterial = glassMat;
                    }
                }
            }

            float shelfLenZ = Mathf.Max(3.20f, coolerLenZ - 0.24f);
            float shelfZMin = coolerCenterZ - shelfLenZ * 0.5f + 0.34f;
            float shelfZMax = coolerCenterZ + shelfLenZ * 0.5f - 0.34f;

            List<float> doorZs = DeduplicateSortedFloats(rawDoorZs, 0.45f);
            doorZs.RemoveAll(z => z < shelfZMin - 0.15f || z > shelfZMax + 0.15f);
            if (doorZs.Count == 0)
            {
                int fallbackDoors = coolerLenZ >= 5.0f ? 6 : 4;
                for (int d = 0; d < fallbackDoors; d++)
                {
                    float t = fallbackDoors > 1 ? (float)d / (fallbackDoors - 1) : 0.5f;
                    doorZs.Add(Mathf.Lerp(shelfZMin, shelfZMax, t));
                }
            }

            GameObject group = new GameObject("WalkIn_Drink_Items");
            group.transform.SetParent(outputParent, false);

            Material boardMat = AssetDatabase.LoadAssetAtPath<Material>(ShelfBoardMatPath);
            float[] shelfTopYs = { 0.56f, 0.98f, 1.40f, 1.82f };
            float innerBoardX = Mathf.Min(coolerBackX + 0.32f, doorX - 0.22f);
            float drinkRowX = innerBoardX + 0.05f;

            // 냉장고 내부 선반 4단 생성 (콜라이더 제거)
            for (int s = 0; s < shelfTopYs.Length; s++)
            {
                GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = $"WalkIn_InnerShelf_{s + 1}";
                board.transform.SetParent(group.transform, false);
                board.transform.position = new Vector3(innerBoardX, shelfTopYs[s] - 0.015f, coolerCenterZ);
                board.transform.localScale = new Vector3(0.46f, 0.03f, shelfLenZ);
                Collider c = board.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
                if (boardMat != null)
                {
                    Renderer r = board.GetComponent<Renderer>();
                    if (r != null) r.sharedMaterial = boardMat;
                }

                // 유리문 구역 사이마다 음료 칸막이 배치
                for (int d = 0; d < doorZs.Count - 1; d++)
                {
                    float midZ = (doorZs[d] + doorZs[d + 1]) * 0.5f;
                    CreateZoneDividerStrip(
                        group.transform,
                        $"ShelfZoneDivider_WalkIn_S{s + 1}_D{d + 1}",
                        new Vector3(innerBoardX + 0.02f, shelfTopYs[s] + 0.020f, midZ),
                        new Vector3(0.42f, 0.040f, 0.016f),
                        dividerMat);
                }
            }

            // 각 유리문(구역) × 선반 단마다 8종 음료가 한 구역씩 연속 블록을 이루도록 정렬 배치 (분산 중복 방지)
            int placed = 0;
            const int drinksPerZone = 4;
            const float drinkStepZ = 0.145f;

            for (int d = 0; d < doorZs.Count; d++)
            {
                for (int s = 0; s < shelfTopYs.Length; s++)
                {
                    int drinkIdx;
                    if (doorZs.Count >= 7)
                    {
                        drinkIdx = d < 6 ? d : (s < 2 ? 6 : 7);
                    }
                    else if (doorZs.Count == 6)
                    {
                        if (d < 4) drinkIdx = d;
                        else if (d == 4) drinkIdx = s < 2 ? 4 : 5;
                        else drinkIdx = s < 2 ? 6 : 7;
                    }
                    else if (doorZs.Count == 5)
                    {
                        if (d < 2) drinkIdx = d;
                        else drinkIdx = 2 + (d - 2) * 2 + (s >= 2 ? 1 : 0);
                    }
                    else
                    {
                        drinkIdx = d * 2 + (s >= 2 ? 1 : 0);
                    }

                    string prefabPath = DrinkPrefabs[drinkIdx % DrinkPrefabs.Length];
                    for (int k = 0; k < drinksPerZone; k++)
                    {
                        float offsetZ = (k - (drinksPerZone - 1) * 0.5f) * drinkStepZ;
                        Vector3 basePos = new Vector3(drinkRowX, shelfTopYs[s], doorZs[d] + offsetZ);
                        if (SpawnFittedItem(
                            prefabPath,
                            group.transform,
                            basePos,
                            90f,
                            new Vector3(0.16f, 0.27f, 0.125f),
                            clearPackageMat,
                            urpMatCache,
                            spawnedCounts) != null)
                        {
                            placed++;
                        }
                    }
                }
            }

            return placed;
        }

        private static int PopulateIceCreamFreezer(
            Transform freezerRoot,
            Transform outputParent,
            Material clearPackageMat,
            Material dividerMat,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            // 냉동고 상단 내부를 오목하게 만들어 투명 유리 뚜껑 아래 아이스크림이 선명하게 보이도록 조정
            Transform baseBox = freezerRoot.Find("IceCream_Freezer_Base");
            float trayTopY = 0.88f;
            float centerX = 2.35f;
            float centerZ = 0.65f;
            float freezerWidthX = 0.90f;
            float freezerLenZ = 3.20f;
            if (baseBox != null)
            {
                centerX = baseBox.position.x;
                centerZ = baseBox.position.z;
                freezerWidthX = Mathf.Max(0.76f, Mathf.Abs(baseBox.localScale.x));
                freezerLenZ = Mathf.Max(2.80f, Mathf.Abs(baseBox.localScale.z));
                baseBox.position = new Vector3(centerX, trayTopY * 0.5f, centerZ);
                Vector3 sc = baseBox.localScale;
                baseBox.localScale = new Vector3(sc.x, trayTopY, sc.z);
            }

            Material glassMat = AssetDatabase.LoadAssetAtPath<Material>(GlassMatPath);
            Transform lid = freezerRoot.Find("Freezer_Lid");
            if (lid != null && glassMat != null)
            {
                Renderer r = lid.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = glassMat;
            }

            GameObject group = new GameObject("IceCream_Items");
            group.transform.SetParent(outputParent, false);

            float zMin = centerZ - freezerLenZ * 0.5f + 0.28f;
            float zMax = centerZ + freezerLenZ * 0.5f - 0.28f;
            float usableLenZ = Mathf.Max(2.0f, zMax - zMin);
            int compartmentCount = usableLenZ >= 4.2f ? 5 : 4;
            float compSpanZ = usableLenZ / compartmentCount;

            // 좌·우 바스켓 중앙 분리대 + 구획별 가로 칸막이 생성
            CreateZoneDividerStrip(
                group.transform,
                "ShelfZoneDivider_Freezer_CenterRail",
                new Vector3(centerX, trayTopY + 0.020f, centerZ),
                new Vector3(0.018f, 0.040f, usableLenZ),
                dividerMat);

            for (int b = 1; b < compartmentCount; b++)
            {
                CreateZoneDividerStrip(
                    group.transform,
                    $"ShelfZoneDivider_Freezer_B{b}",
                    new Vector3(centerX, trayTopY + 0.022f, zMin + compSpanZ * b),
                    new Vector3(freezerWidthX * 0.88f, 0.044f, 0.018f),
                    dividerMat);
            }

            // 바스켓 구획별 좌/우 = 10칸(0~9)에 10종 아이스크림을 중복 없이 각각 4개(2×2)씩 배치
            int placed = 0;
            float laneOffsetX = Mathf.Clamp(freezerWidthX * 0.23f, 0.17f, 0.23f);
            float[] subOffsetsX = { -0.075f, 0.075f };
            float subStepZ = Mathf.Clamp(compSpanZ * 0.22f, 0.085f, 0.125f);
            float[] subOffsetsZ = { -subStepZ, subStepZ };

            for (int c = 0; c < compartmentCount; c++)
            {
                float compCenterZ = zMin + compSpanZ * (c + 0.5f);

                for (int side = 0; side < 2; side++)
                {
                    int slotIdx = c * 2 + side;
                    string prefabPath = IceCreamAllPrefabs[slotIdx % IceCreamAllPrefabs.Length];
                    float laneCenterX = centerX + (side == 0 ? -laneOffsetX : laneOffsetX);
                    float yaw = side == 0 ? -90f : 90f;

                    for (int xi = 0; xi < subOffsetsX.Length; xi++)
                    {
                        for (int zi = 0; zi < subOffsetsZ.Length; zi++)
                        {
                            Vector3 basePos = new Vector3(
                                laneCenterX + subOffsetsX[xi],
                                trayTopY,
                                compCenterZ + subOffsetsZ[zi]);

                            if (SpawnFittedItem(
                                prefabPath,
                                group.transform,
                                basePos,
                                yaw,
                                new Vector3(0.13f, 0.19f, 0.15f),
                                clearPackageMat,
                                urpMatCache,
                                spawnedCounts) != null)
                            {
                                placed++;
                            }
                        }
                    }
                }
            }

            return placed;
        }

        private static int PopulateHotFoodShowcases(
            Transform hotFoodRoot,
            Transform outputParent,
            Material clearPackageMat,
            Material showcaseGlassMat,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            List<Transform> showcases = new List<Transform>();
            float counterTopY = 1.17f;

            for (int i = 0; i < hotFoodRoot.childCount; i++)
            {
                Transform child = hotFoodRoot.GetChild(i);
                if (child.name == "HotFood_Top")
                {
                    counterTopY = child.position.y + Mathf.Abs(child.lossyScale.y) * 0.5f;
                }
                else if (child.name == "Food_Display")
                {
                    showcases.Add(child);
                    // 쇼케이스 박스를 투명 유리로 바꿔 내부 즉석식품이 선명하게 보이도록 처리
                    if (showcaseGlassMat != null)
                    {
                        Renderer r = child.GetComponent<Renderer>();
                        if (r != null) r.sharedMaterial = showcaseGlassMat;
                    }
                }
            }

            showcases.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            GameObject group = new GameObject("FreshFood_FF_Items");
            group.transform.SetParent(outputParent, false);

            int placed = 0;
            float[] xOffsets = { -0.13f, 0.13f };
            float[] zOffsets = { -0.11f, 0.11f };

            for (int i = 0; i < showcases.Count; i++)
            {
                Transform box = showcases[i];
                string prefabPath = FreshFoodShowcasePrefabs[i % FreshFoodShowcasePrefabs.Length];

                for (int xi = 0; xi < xOffsets.Length; xi++)
                {
                    for (int zi = 0; zi < zOffsets.Length; zi++)
                    {
                        Vector3 basePos = new Vector3(
                            box.position.x + xOffsets[xi],
                            counterTopY,
                            box.position.z + zOffsets[zi]);

                        if (SpawnFittedItem(
                            prefabPath,
                            group.transform,
                            basePos,
                            0f,
                            new Vector3(0.20f, 0.22f, 0.19f),
                            clearPackageMat,
                            urpMatCache,
                            spawnedCounts) != null)
                        {
                            placed++;
                        }
                    }
                }
            }

            return placed;
        }

        private static List<(Vector3 floorPos, Vector3 scale)> HarvestExistingCigaretteSlots(Transform existingMarker, string groupName)
        {
            List<(Vector3 floorPos, Vector3 scale)> result = new List<(Vector3 floorPos, Vector3 scale)>();
            if (existingMarker == null) return result;

            Transform cigGroup = existingMarker.Find(groupName);
            if (cigGroup == null) return result;

            for (int i = 0; i < cigGroup.childCount; i++)
            {
                Transform child = cigGroup.GetChild(i);
                if (child == null) continue;
                result.Add((child.position, new Vector3(0.14f, 0.20f, 0.06f)));
            }

            return result;
        }

        private static int ReplaceCigarettePacks(
            Transform checkoutRoot,
            Transform outputParent,
            string groupName,
            List<(Vector3 floorPos, Vector3 scale)> fallbackFloorSlots,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            List<(Vector3 floorPos, Vector3 scale)> packSlots = new List<(Vector3, Vector3)>();
            List<GameObject> toDestroy = new List<GameObject>();

            for (int i = 0; i < checkoutRoot.childCount; i++)
            {
                Transform child = checkoutRoot.GetChild(i);
                if (child.name == "Tobacco_Pack")
                {
                    float bottomY = child.position.y - Mathf.Abs(child.lossyScale.y) * 0.5f;
                    packSlots.Add((new Vector3(child.position.x, bottomY, child.position.z), child.lossyScale));
                    toDestroy.Add(child.gameObject);
                }
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                Object.DestroyImmediate(toDestroy[i]);
            }

            if (packSlots.Count == 0 && fallbackFloorSlots != null && fallbackFloorSlots.Count > 0)
            {
                packSlots.AddRange(fallbackFloorSlots);
            }

            GameObject cigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CigarettePrefabPath);
            if (cigPrefab == null || packSlots.Count == 0)
            {
                return 0;
            }

            // 단(Y) → 열(X) 순서로 정렬한 뒤 동일 브랜드 색상을 3갑씩 연속 묶어 정렬 진열
            packSlots.Sort((a, b) =>
            {
                if (Mathf.Abs(a.floorPos.y - b.floorPos.y) > 0.06f)
                {
                    return a.floorPos.y.CompareTo(b.floorPos.y);
                }
                return a.floorPos.x.CompareTo(b.floorPos.x);
            });

            GameObject group = new GameObject(groupName);
            group.transform.SetParent(outputParent, false);

            Material[] cigColorMats = EnsureCigaretteColorMaterials();

            int placed = 0;
            int rowIdx = 0;
            int colInRow = 0;
            float prevRowY = packSlots[0].floorPos.y;

            for (int i = 0; i < packSlots.Count; i++)
            {
                Vector3 floorPos = packSlots[i].floorPos;
                Vector3 targetScale = packSlots[i].scale;
                if (Mathf.Abs(floorPos.y - prevRowY) > 0.06f)
                {
                    rowIdx++;
                    colInRow = 0;
                    prevRowY = floorPos.y;
                }

                GameObject item = SpawnFittedItem(
                    CigarettePrefabPath,
                    group.transform,
                    floorPos,
                    0f,
                    new Vector3(Mathf.Max(0.12f, targetScale.x * 0.9f), Mathf.Max(0.18f, targetScale.y * 0.9f), Mathf.Max(0.06f, targetScale.z * 1.4f)),
                    clearPackageMat: null,
                    urpMatCache,
                    spawnedCounts,
                    allowPackageWrap: false);

                if (item != null)
                {
                    if (cigColorMats != null && cigColorMats.Length > 0)
                    {
                        int brandIdx = ((colInRow / 3) + rowIdx * 2) % cigColorMats.Length;
                        Material mat = cigColorMats[brandIdx];
                        Renderer[] rends = item.GetComponentsInChildren<Renderer>(true);
                        for (int r = 0; r < rends.Length; r++)
                        {
                            rends[r].sharedMaterial = mat;
                        }
                    }
                    placed++;
                }

                colInRow++;
            }

            return placed;
        }

        private static bool IsWideShelfItem(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return false;
            return prefabPath.IndexOf("Cake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabPath.IndexOf("Waffle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabPath.IndexOf("Donuts.prefab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabPath.IndexOf("PhoBowl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabPath.IndexOf("SideBowl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabPath.IndexOf("Basket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabPath.IndexOf("NapkinBox", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<float> DeduplicateSortedFloats(List<float> values, float minGap)
        {
            List<float> result = new List<float>();
            if (values == null || values.Count == 0) return result;

            values.Sort();
            for (int i = 0; i < values.Count; i++)
            {
                float v = values[i];
                if (result.Count == 0 || Mathf.Abs(v - result[result.Count - 1]) >= minGap)
                {
                    result.Add(v);
                }
            }

            return result;
        }

        private static void CreateZoneDividerStrip(
            Transform parent,
            string name,
            Vector3 worldPos,
            Vector3 worldScale,
            Material dividerMat)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent, false);
            strip.transform.position = worldPos;
            strip.transform.localScale = worldScale;

            Collider col = strip.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            Renderer rend = strip.GetComponent<Renderer>();
            if (rend != null)
            {
                if (dividerMat != null)
                {
                    rend.sharedMaterial = dividerMat;
                }
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static GameObject SpawnFittedItem(
            string prefabPath,
            Transform parent,
            Vector3 floorCenterWorldPos,
            float yawDegrees,
            Vector3 maxSlotSize,
            Material clearPackageMat,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts,
            bool allowPackageWrap = true)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return null;
            }

            string itemName = Path.GetFileNameWithoutExtension(prefabPath);
            if (spawnedCounts != null)
            {
                spawnedCounts.TryGetValue(itemName, out int cur);
                spawnedCounts[itemName] = cur + 1;
            }
            GameObject holder = new GameObject(itemName);
            holder.transform.SetParent(parent, false);
            holder.transform.position = floorCenterWorldPos;
            holder.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                Object.DestroyImmediate(holder);
                return null;
            }

            instance.transform.SetParent(holder.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // 진열 상품의 물리 충돌체 제거 (NavMesh·플레이어 이동 방해 방지)
            Collider[] cols = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Object.DestroyImmediate(cols[i]);
            }

            Rigidbody[] rbs = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                Object.DestroyImmediate(rbs[i]);
            }

            EnsureUrpMaterialsOnRenderers(instance, urpMatCache);
            ApplyProductColorVariantIfNeeded(instance, itemName);

            if (!TryComputeWorldBounds(instance, out Bounds rawBounds))
            {
                return holder;
            }

            // 슬롯 안에 자연스럽게 들어가도록 균일 비율 스케일 계산
            float sx = rawBounds.size.x > 0.0001f ? maxSlotSize.x / rawBounds.size.x : 1f;
            float sy = rawBounds.size.y > 0.0001f ? maxSlotSize.y / rawBounds.size.y : 1f;
            float sz = rawBounds.size.z > 0.0001f ? maxSlotSize.z / rawBounds.size.z : 1f;
            float fitScale = Mathf.Clamp(Mathf.Min(sx, Mathf.Min(sy, sz)), 0.02f, 20f);

            // 반투명 포장 큐브를 씌울 경우 약간의 외곽 여백을 확보
            bool wrapInClearBox = allowPackageWrap && clearPackageMat != null && NeedsClearPackageNames.Contains(itemName);
            if (wrapInClearBox)
            {
                fitScale *= 0.88f;
            }

            instance.transform.localScale = instance.transform.localScale * fitScale;

            // 스케일 후 정확히 선반 바닥(floorCenterWorldPos.y)에 밀착 & X/Z 중앙 정렬
            if (TryComputeWorldBounds(instance, out Bounds scaledBounds))
            {
                Vector3 delta = new Vector3(
                    floorCenterWorldPos.x - scaledBounds.center.x,
                    floorCenterWorldPos.y - scaledBounds.min.y + (wrapInClearBox ? 0.008f : 0.001f),
                    floorCenterWorldPos.z - scaledBounds.center.z);
                instance.transform.position += delta;

                if (wrapInClearBox && TryComputeWorldBounds(instance, out Bounds finalBounds))
                {
                    const float pad = 0.016f;
                    GameObject wrapCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wrapCube.name = "FoodClearPackage";
                    wrapCube.transform.SetParent(holder.transform, true);
                    wrapCube.transform.position = finalBounds.center;
                    wrapCube.transform.rotation = holder.transform.rotation;

                    // 로컬 축 기준으로 감싸도록 크기 계산
                    Vector3 worldSize = finalBounds.size;
                    float boxW = Mathf.Max(0.06f, worldSize.x + pad);
                    float boxH = Mathf.Max(0.05f, worldSize.y + pad);
                    float boxD = Mathf.Max(0.06f, worldSize.z + pad);

                    // 90도 회전된 경우 X/Z 스왑 보정
                    bool swappedXZ = Mathf.Abs(Mathf.Sin(yawDegrees * Mathf.Deg2Rad)) > 0.707f;
                    wrapCube.transform.localScale = swappedXZ
                        ? new Vector3(boxD, boxH, boxW)
                        : new Vector3(boxW, boxH, boxD);

                    // 포장 큐브 바닥도 선반에 밀착
                    Vector3 wp = wrapCube.transform.position;
                    wp.y = floorCenterWorldPos.y + boxH * 0.5f + 0.001f;
                    wrapCube.transform.position = wp;

                    Collider wc = wrapCube.GetComponent<Collider>();
                    if (wc != null) Object.DestroyImmediate(wc);

                    Renderer wr = wrapCube.GetComponent<Renderer>();
                    if (wr != null)
                    {
                        wr.sharedMaterial = clearPackageMat;
                        wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }

            return holder;
        }

        private static bool TryComputeWorldBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return hasBounds;
        }

        private static int FindNearestTierIndex(float y, List<float> boardTopYs)
        {
            int bestIdx = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < boardTopYs.Count; i++)
            {
                // 상품 큐브 중심은 선반 상판보다 위(+0.12)에 있으므로 상판 위쪽 우선 매칭
                float dist = Mathf.Abs((y - 0.12f) - boardTopYs[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private const string HqpColorGridTexturePath =
            "Assets/ThirdParty/Shared/HQP STUDIOS/Food Pack - Low Poly Sweets/Textures/ColorGrid.png";

        private static readonly string[] AlbedoTexturePropertyCandidates =
        {
            "_BaseMap",
            "_MainTex",
            "_BaseTexture",
            "_Base_Color_Map",
            "_AlbedoMap",
            "_DiffuseMap"
        };

        public static void EnsureUrpMaterialsOnRenderers(GameObject instance, Dictionary<Material, Material> cache = null)
        {
            if (instance == null) return;
            if (cache == null) cache = new Dictionary<Material, Material>();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material src = mats[m];
                    if (src == null || src.shader == null) continue;

                    string shaderName = src.shader.name ?? string.Empty;
                    bool isUrpCompatible =
                        shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
                        shaderName.StartsWith("Shader Graphs/", StringComparison.Ordinal);

                    // HQP STUDIOS FBX 임베디드 메테리얼이 URP/Lit이지만 ColorGrid 텍스처가 비어 흰색으로 뜨는 경우 보정
                    if (isUrpCompatible && TryGetHqpFallbackMaterial(r, src, urpLit, out Material hqpFallback))
                    {
                        mats[m] = hqpFallback;
                        changed = true;
                        continue;
                    }

                    if (isUrpCompatible) continue;

                    if (!cache.TryGetValue(src, out Material upgraded) || upgraded == null)
                    {
                        upgraded = GetOrCreateUrpMaterialCopy(src, urpLit);
                        cache[src] = upgraded;
                    }

                    if (upgraded != null)
                    {
                        mats[m] = upgraded;
                        changed = true;
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
                }
            }
        }

        private static bool TryGetHqpFallbackMaterial(Renderer r, Material src, Shader urpLit, out Material fallback)
        {
            fallback = null;
            Texture existingBase = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
            if (existingBase != null) return false;

            MeshFilter mf = r.GetComponent<MeshFilter>();
            string meshPath = mf != null && mf.sharedMesh != null ? AssetDatabase.GetAssetPath(mf.sharedMesh) : string.Empty;
            string srcPath = AssetDatabase.GetAssetPath(src);
            bool isHqpAsset =
                meshPath.IndexOf("HQP STUDIOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                srcPath.IndexOf("HQP STUDIOS", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isHqpAsset) return false;

            Texture colorGrid = AssetDatabase.LoadAssetAtPath<Texture>(HqpColorGridTexturePath);
            if (colorGrid == null) return false;

            if (!AssetDatabase.IsValidFolder(UrpCacheDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong/Assets/UI", "ItemUrpMaterials");
            }

            string matPath = $"{UrpCacheDir}/URP_HQP_ColorGrid.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing == null)
            {
                existing = new Material(urpLit);
                existing.name = "URP_HQP_ColorGrid";
                existing.SetTexture("_BaseMap", colorGrid);
                existing.SetColor("_BaseColor", Color.white);
                existing.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(existing, matPath);
            }
            else if (existing.GetTexture("_BaseMap") == null)
            {
                existing.SetTexture("_BaseMap", colorGrid);
                existing.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(existing);
            }

            fallback = existing;
            return true;
        }

        private static Texture FindAlbedoTexture(Material src)
        {
            if (src == null) return null;

            for (int i = 0; i < AlbedoTexturePropertyCandidates.Length; i++)
            {
                string prop = AlbedoTexturePropertyCandidates[i];
                if (src.HasProperty(prop))
                {
                    Texture tex = src.GetTexture(prop);
                    if (tex != null) return tex;
                }
            }

            string[] texNames = src.GetTexturePropertyNames();
            for (int i = 0; i < texNames.Length; i++)
            {
                string name = texNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                if (name.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Bump", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Metallic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("MRTexture", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                Texture tex = src.GetTexture(name);
                if (tex != null) return tex;
            }

            return null;
        }

        private static void ApplyProductColorVariantIfNeeded(GameObject instance, string itemName)
        {
            if (instance == null || string.IsNullOrEmpty(itemName)) return;

            string matFileName;
            Color tint;

            switch (itemName)
            {
                case "SM_Cook_PhoBowl_Full_01":
                    matFileName = "URP_RamenCup_PhoFull_Red";
                    tint = new Color(0.95f, 0.36f, 0.34f, 1f);
                    break;
                case "SM_Cook_PhoBowl_Empty_01":
                    matFileName = "URP_RamenCup_PhoBig_Orange";
                    tint = new Color(0.98f, 0.62f, 0.26f, 1f);
                    break;
                case "SM_Cook_SideBowl_Lime_01":
                    matFileName = "URP_RamenCup_SideLime_Green";
                    tint = new Color(0.50f, 0.88f, 0.42f, 1f);
                    break;
                case "SM_Cook_SideBowl_01":
                    matFileName = "URP_RamenCup_SideUdon_Blue";
                    tint = new Color(0.42f, 0.68f, 0.96f, 1f);
                    break;
                case "SM_Cook_BowlStack_01":
                    matFileName = "URP_RamenCup_Stack_Yellow";
                    tint = new Color(0.98f, 0.84f, 0.34f, 1f);
                    break;
                default:
                    return;
            }

            Material variantMat = EnsureTintedPaletteMaterial(matFileName, tint);
            if (variantMat == null) return;

            Renderer[] rends = instance.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                Renderer rend = rends[r];
                if (rend == null || rend.gameObject.name == "FoodClearPackage") continue;

                Material[] mats = rend.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    rend.sharedMaterial = variantMat;
                    continue;
                }

                for (int m = 0; m < mats.Length; m++)
                {
                    mats[m] = variantMat;
                }
                rend.sharedMaterials = mats;
            }
        }

        private static Material EnsureTintedPaletteMaterial(string matFileName, Color tint)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return null;

            if (!AssetDatabase.IsValidFolder(UrpCacheDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong/Assets/UI", "ItemUrpMaterials");
            }

            Texture paletteTex = AssetDatabase.LoadAssetAtPath<Texture>(AsianStreetFoodPalettePath);
            string matPath = $"{UrpCacheDir}/{matFileName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                bool dirty = false;
                if (paletteTex != null && existing.HasProperty("_BaseMap") && existing.GetTexture("_BaseMap") != paletteTex)
                {
                    existing.SetTexture("_BaseMap", paletteTex);
                    dirty = true;
                }
                if (existing.HasProperty("_BaseColor") && existing.GetColor("_BaseColor") != tint)
                {
                    existing.SetColor("_BaseColor", tint);
                    dirty = true;
                }
                if (existing.HasProperty("_Color") && existing.GetColor("_Color") != tint)
                {
                    existing.SetColor("_Color", tint);
                    dirty = true;
                }
                if (dirty) EditorUtility.SetDirty(existing);
                return existing;
            }

            Material mat = new Material(urpLit);
            mat.name = matFileName;
            if (paletteTex != null)
            {
                mat.SetTexture("_BaseMap", paletteTex);
            }
            mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", tint);
            }
            mat.SetFloat("_Smoothness", 0.42f);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static Material GetOrCreateUrpMaterialCopy(Material src, Shader urpLit)
        {
            if (!AssetDatabase.IsValidFolder(UrpCacheDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong/Assets/UI", "ItemUrpMaterials");
            }

            Texture mainTex = FindAlbedoTexture(src);
            string safeName = src.name.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
            string srcAssetPath = AssetDatabase.GetAssetPath(src) ?? string.Empty;
            string suffix = string.Empty;
            if (!srcAssetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                string key = !string.IsNullOrEmpty(srcAssetPath) ? srcAssetPath : (mainTex != null ? mainTex.name : string.Empty);
                if (!string.IsNullOrEmpty(key))
                {
                    unchecked
                    {
                        uint hash = 2166136261u;
                        for (int i = 0; i < key.Length; i++)
                        {
                            hash = (hash ^ key[i]) * 16777619u;
                        }
                        suffix = "_" + (hash & 0xFFFFu).ToString("X4");
                    }
                }
            }

            string matPath = $"{UrpCacheDir}/URP_{safeName}{suffix}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                if (mainTex != null && existing.HasProperty("_BaseMap") && existing.GetTexture("_BaseMap") == null)
                {
                    existing.SetTexture("_BaseMap", mainTex);
                    existing.SetColor("_BaseColor", Color.white);
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            Material mat = new Material(urpLit);
            mat.name = "URP_" + src.name;
            if (mainTex != null)
            {
                mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", Color.white);
            }
            else if (src.HasProperty("_Color"))
            {
                mat.SetColor("_BaseColor", src.GetColor("_Color"));
            }
            else if (src.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", src.GetColor("_BaseColor"));
            }
            else
            {
                mat.SetColor("_BaseColor", Color.white);
            }
            mat.SetFloat("_Smoothness", 0.35f);

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static Material EnsureClearPackageMaterial(string assetPath, Color tint, float smoothness)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return null;

            Material mat = new Material(urpLit);
            mat.name = Path.GetFileNameWithoutExtension(assetPath);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0.04f);
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(mat, assetPath);
            return mat;
        }

        private static Material[] EnsureCigaretteColorMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return null;

            if (!AssetDatabase.IsValidFolder(UrpCacheDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong/Assets/UI", "ItemUrpMaterials");
            }

            Material[] mats = new Material[CigaretteTintColors.Length];
            for (int i = 0; i < CigaretteTintColors.Length; i++)
            {
                string path = $"{UrpCacheDir}/URP_CigPack_{i}.mat";
                Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing == null)
                {
                    existing = new Material(urpLit);
                    existing.name = $"URP_CigPack_{i}";
                    existing.SetColor("_BaseColor", CigaretteTintColors[i]);
                    existing.SetFloat("_Smoothness", 0.45f);
                    AssetDatabase.CreateAsset(existing, path);
                }
                mats[i] = existing;
            }
            return mats;
        }
    }
}
