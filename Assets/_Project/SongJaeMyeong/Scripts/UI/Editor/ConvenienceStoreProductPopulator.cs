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

        private const string SoldRoot = "Assets/SellableItems/01_CurrentlySold";
        private const string PackageMatPath = "Assets/_Project/SongJaeMyeong/Assets/UI/Mat_FoodClearPackage.mat";
        private const string ShowcaseGlassMatPath = "Assets/_Project/SongJaeMyeong/Assets/UI/Mat_HotFoodShowcaseGlass.mat";
        private const string ShelfBoardMatPath = "Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/White.mat";
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

        // 단별(하단→상단) 배치: 케이크/와플 -> 베이커리 도넛/와플 -> 스프링클 도넛 -> 초콜릿 바
        private static readonly string[][] SnackTierPrefabs =
        {
            new[]
            {
                SoldRoot + "/02_Snack/Cake.prefab",
                SoldRoot + "/02_Snack/Waffle.prefab"
            },
            new[]
            {
                SoldRoot + "/02_Snack/Donuts.prefab",
                SoldRoot + "/02_Snack/Waffle.prefab"
            },
            new[]
            {
                SoldRoot + "/02_Snack/Chocolate_Donut_Sprinkles_1.prefab",
                SoldRoot + "/02_Snack/Strawberry_Donut_Sprinkles_1.prefab",
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

        // 단별(하단→상단) 배치: 빈 그릇·가느다란 젓가락 제외, 내용물이 가득 채워진 통조림·컵라면만 진열
        private static readonly string[][] RamenTierPrefabs =
        {
            new[]
            {
                SoldRoot + "/03_Ramen/P_RSG_FoodCan.prefab"
            },
            new[]
            {
                SoldRoot + "/03_Ramen/SM_Cook_PhoBowl_Full_01.prefab",
                SoldRoot + "/03_Ramen/P_RSG_FoodCan.prefab"
            },
            new[]
            {
                SoldRoot + "/03_Ramen/SM_Cook_SideBowl_Lime_01.prefab"
            },
            new[]
            {
                SoldRoot + "/03_Ramen/SM_Cook_PhoBowl_Full_01.prefab"
            }
        };

        // 단별(하단→상단) 배치: 다용도 바구니 -> 조미료·식용유병 -> 소스 4종 -> 곽티슈·후추통
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

        private static readonly string[] IceCreamPrefabs =
        {
            SoldRoot + "/05_IceCream/Vanilla_Ice_Cream_1.prefab",
            SoldRoot + "/05_IceCream/Chocolate_Ice_Cream_1.prefab",
            SoldRoot + "/05_IceCream/Strawberry_Ice_Cream_1.prefab",
            SoldRoot + "/05_IceCream/IceCream.prefab",
            SoldRoot + "/05_IceCream/Red_Popsicle.prefab",
            SoldRoot + "/05_IceCream/Blue_Popsicle.prefab",
            SoldRoot + "/05_IceCream/Green_Popsicle.prefab",
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
            if (populatedMarker == null) return true;

            Transform ramenGroup = populatedMarker.transform.Find("Ramen_Items");
            if (ramenGroup != null)
            {
                for (int i = 0; i < ramenGroup.childCount; i++)
                {
                    string childName = ramenGroup.GetChild(i).name;
                    if (childName.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        childName.IndexOf("BowlStack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        childName.IndexOf("Chopstick", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        childName.Equals("Item_SM_Cook_SideBowl_01", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
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
            Dictionary<Material, Material> urpMatCache = new Dictionary<Material, Material>();

            Transform existingMarker = mapRoot.transform.Find(PopulatedMarkerName);
            if (existingMarker != null)
            {
                Object.DestroyImmediate(existingMarker.gameObject);
            }

            GameObject markerRoot = new GameObject(PopulatedMarkerName);
            markerRoot.transform.SetParent(mapRoot.transform, false);

            int totalPlaced = 0;

            // 1. 스낵류 곤돌라 (Snack_Shelf)
            Transform snackShelf = mapRoot.transform.Find("Snack_Shelf");
            if (snackShelf != null)
            {
                totalPlaced += ReplaceGondolaShelfProducts(
                    snackShelf,
                    markerRoot.transform,
                    "Snack_Items",
                    SnackTierPrefabs,
                    maxSlotSize: new Vector3(0.24f, 0.24f, 0.28f),
                    clearPackageMat,
                    urpMatCache,
                    spawnedCounts);
            }

            // 2. 라면류 곤돌라 (Ramen_Shelf)
            Transform ramenShelf = mapRoot.transform.Find("Ramen_Shelf");
            if (ramenShelf != null)
            {
                totalPlaced += ReplaceGondolaShelfProducts(
                    ramenShelf,
                    markerRoot.transform,
                    "Ramen_Items",
                    RamenTierPrefabs,
                    maxSlotSize: new Vector3(0.24f, 0.26f, 0.28f),
                    clearPackageMat,
                    urpMatCache,
                    spawnedCounts);
            }

            // 3. 생필품 곤돌라 (DailyGoods_Shelf)
            Transform dailyShelf = mapRoot.transform.Find("DailyGoods_Shelf");
            if (dailyShelf != null)
            {
                totalPlaced += ReplaceGondolaShelfProducts(
                    dailyShelf,
                    markerRoot.transform,
                    "Daily_Items",
                    DailyTierPrefabs,
                    maxSlotSize: new Vector3(0.24f, 0.28f, 0.28f),
                    clearPackageMat,
                    urpMatCache,
                    spawnedCounts);
            }

            // 4. 워크인 냉장고 음료류 (WalkIn_and_Storage)
            Transform walkIn = mapRoot.transform.Find("WalkIn_and_Storage");
            if (walkIn != null)
            {
                totalPlaced += PopulateWalkInCoolerDrinks(walkIn, markerRoot.transform, clearPackageMat, urpMatCache, spawnedCounts);
            }

            // 5. 아이스크림 냉동고 (IceCream_Freezer)
            Transform iceCreamFreezer = mapRoot.transform.Find("IceCream_Freezer");
            if (iceCreamFreezer != null)
            {
                totalPlaced += PopulateIceCreamFreezer(iceCreamFreezer, markerRoot.transform, clearPackageMat, urpMatCache, spawnedCounts);
            }

            // 6. FF · 핫바류 오픈 쇼케이스 (HotFood)
            Transform hotFood = mapRoot.transform.Find("HotFood");
            if (hotFood != null)
            {
                totalPlaced += PopulateHotFoodShowcases(hotFood, markerRoot.transform, clearPackageMat, showcaseGlassMat, urpMatCache, spawnedCounts);
            }

            // 7. 계산대 1·2 뒤 담배 진열대 (Checkout_1, Checkout_2)
            Transform checkout1 = mapRoot.transform.Find("Checkout_1");
            if (checkout1 != null)
            {
                totalPlaced += ReplaceCigarettePacks(checkout1, markerRoot.transform, "Checkout1_Cigarettes", urpMatCache, spawnedCounts);
            }

            Transform checkout2 = mapRoot.transform.Find("Checkout_2");
            if (checkout2 != null)
            {
                totalPlaced += ReplaceCigarettePacks(checkout2, markerRoot.transform, "Checkout2_Cigarettes", urpMatCache, spawnedCounts);
            }

            Debug.Log($"[OpenSourceTeam6.UI] 설계도 기준 편의점 실물 상품 {totalPlaced}개 배치 및 반투명 음식 포장 완료!");
            return spawnedCounts;
        }

        private static int ReplaceGondolaShelfProducts(
            Transform shelfRoot,
            Transform outputParent,
            string groupName,
            string[][] tierPrefabPaths,
            Vector3 maxSlotSize,
            Material clearPackageMat,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            List<Vector3> slotPositions = new List<Vector3>();
            List<float> boardTopYs = new List<float>();
            List<GameObject> toDestroy = new List<GameObject>();

            for (int i = 0; i < shelfRoot.childCount; i++)
            {
                Transform child = shelfRoot.GetChild(i);
                if (child.name == "Product")
                {
                    slotPositions.Add(child.position);
                    toDestroy.Add(child.gameObject);
                }
                else if (child.name == "Shelf_Board")
                {
                    float topY = child.position.y + child.lossyScale.y * 0.5f;
                    boardTopYs.Add(topY);
                }
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                Object.DestroyImmediate(toDestroy[i]);
            }

            boardTopYs.Sort();
            if (boardTopYs.Count == 0)
            {
                boardTopYs.AddRange(new[] { 0.415f, 0.815f, 1.215f, 1.615f });
            }

            GameObject group = new GameObject(groupName);
            group.transform.SetParent(outputParent, false);

            // X 중심값 계산 (양면 진열 시 통로 쪽을 바라보도록 회전)
            float avgX = 0f;
            for (int i = 0; i < slotPositions.Count; i++) avgX += slotPositions[i].x;
            if (slotPositions.Count > 0) avgX /= slotPositions.Count;

            // Z 순서대로 정렬해 깔끔하게 열별로 품목 통일
            slotPositions.Sort((a, b) =>
            {
                int yCmp = a.y.CompareTo(b.y);
                if (Mathf.Abs(a.y - b.y) > 0.1f) return yCmp;
                int xCmp = a.x.CompareTo(b.x);
                if (Mathf.Abs(a.x - b.x) > 0.1f) return xCmp;
                return a.z.CompareTo(b.z);
            });

            int placed = 0;
            int[] tierCounters = new int[tierPrefabPaths.Length];
            for (int i = 0; i < slotPositions.Count; i++)
            {
                Vector3 slotPos = slotPositions[i];
                int tierIndex = FindNearestTierIndex(slotPos.y, boardTopYs);
                float shelfTopY = boardTopYs[Mathf.Clamp(tierIndex, 0, boardTopYs.Count - 1)];

                string[] tierList = tierPrefabPaths[Mathf.Clamp(tierIndex, 0, tierPrefabPaths.Length - 1)];
                if (tierList == null || tierList.Length == 0) continue;

                // 같은 열에서 품목이 자연스럽게 2개씩 묶여 보이도록 배치
                int itemIdx = (tierCounters[tierIndex] / 2) % tierList.Length;
                tierCounters[tierIndex]++;

                float yaw = slotPos.x < avgX ? -90f : 90f;
                Vector3 basePos = new Vector3(slotPos.x, shelfTopY, slotPos.z);

                if (SpawnFittedItem(
                    tierList[itemIdx],
                    group.transform,
                    basePos,
                    yaw,
                    maxSlotSize,
                    clearPackageMat,
                    urpMatCache,
                    spawnedCounts) != null)
                {
                    placed++;
                }
            }

            return placed;
        }

        private static int PopulateWalkInCoolerDrinks(
            Transform walkInRoot,
            Transform outputParent,
            Material clearPackageMat,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            // 불투명한 WalkIn_Cooler 본체를 뒤쪽 벽으로 얇게 밀어 유리문 안쪽 선반이 보이도록 구성
            Transform coolerBody = walkInRoot.Find("WalkIn_Cooler");
            if (coolerBody != null)
            {
                coolerBody.position = new Vector3(-6.86f, 1.25f, 1.58f);
                coolerBody.localScale = new Vector3(0.16f, 2.50f, 3.60f);
            }

            Material glassMat = AssetDatabase.LoadAssetAtPath<Material>(GlassMatPath);
            List<float> doorZs = new List<float>();
            for (int i = 0; i < walkInRoot.childCount; i++)
            {
                Transform child = walkInRoot.GetChild(i);
                if (child.name == "Fridge_Door")
                {
                    doorZs.Add(child.position.z);
                    if (glassMat != null)
                    {
                        Renderer r = child.GetComponent<Renderer>();
                        if (r != null) r.sharedMaterial = glassMat;
                    }
                }
            }

            doorZs.Sort();
            if (doorZs.Count == 0)
            {
                doorZs.AddRange(new[] { 0.20f, 1.05f, 1.90f, 2.75f });
            }

            GameObject group = new GameObject("WalkIn_Drink_Items");
            group.transform.SetParent(outputParent, false);

            Material boardMat = AssetDatabase.LoadAssetAtPath<Material>(ShelfBoardMatPath);
            float[] shelfTopYs = { 0.56f, 0.98f, 1.40f, 1.82f };

            // 냉장고 내부 선반 4단 생성 (콜라이더 제거)
            for (int s = 0; s < shelfTopYs.Length; s++)
            {
                GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = $"WalkIn_InnerShelf_{s + 1}";
                board.transform.SetParent(group.transform, false);
                board.transform.position = new Vector3(-6.54f, shelfTopYs[s] - 0.015f, 1.50f);
                board.transform.localScale = new Vector3(0.46f, 0.03f, 3.36f);
                Collider c = board.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
                if (boardMat != null)
                {
                    Renderer r = board.GetComponent<Renderer>();
                    if (r != null) r.sharedMaterial = boardMat;
                }
            }

            int placed = 0;
            float[] zOffsets = { -0.22f, 0f, 0.22f };

            for (int d = 0; d < doorZs.Count; d++)
            {
                for (int s = 0; s < shelfTopYs.Length; s++)
                {
                    string prefabPath = DrinkPrefabs[(d * 2 + s) % DrinkPrefabs.Length];
                    for (int k = 0; k < zOffsets.Length; k++)
                    {
                        Vector3 basePos = new Vector3(-6.48f, shelfTopYs[s], doorZs[d] + zOffsets[k]);
                        if (SpawnFittedItem(
                            prefabPath,
                            group.transform,
                            basePos,
                            90f,
                            new Vector3(0.18f, 0.28f, 0.18f),
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
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            // 냉동고 상단 내부를 오목하게 만들어 투명 유리 뚜껑 아래 아이스크림이 선명하게 보이도록 조정
            Transform baseBox = freezerRoot.Find("IceCream_Freezer_Base");
            float trayTopY = 0.88f;
            if (baseBox != null)
            {
                baseBox.position = new Vector3(baseBox.position.x, trayTopY * 0.5f, baseBox.position.z);
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

            int placed = 0;
            float centerX = baseBox != null ? baseBox.position.x : 2.35f;
            float[] xOffsets = { -0.22f, 0.22f };

            // Z 범위 -0.78 ~ +2.02 구간에 10열 × 2행 = 20개 아이스크림 진열 (모두 반투명 포장 큐브 적용)
            const int rows = 10;
            float zStart = -0.75f;
            float zEnd = 2.02f;
            for (int r = 0; r < rows; r++)
            {
                float t = rows > 1 ? (float)r / (rows - 1) : 0.5f;
                float z = Mathf.Lerp(zStart, zEnd, t);
                string prefabPath = IceCreamPrefabs[r % IceCreamPrefabs.Length];

                for (int c = 0; c < xOffsets.Length; c++)
                {
                    Vector3 basePos = new Vector3(centerX + xOffsets[c], trayTopY, z);
                    if (SpawnFittedItem(
                        prefabPath,
                        group.transform,
                        basePos,
                        c == 0 ? -90f : 90f,
                        new Vector3(0.24f, 0.21f, 0.24f),
                        clearPackageMat,
                        urpMatCache,
                        spawnedCounts) != null)
                    {
                        placed++;
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
                    counterTopY = child.position.y + child.lossyScale.y * 0.5f;
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
            float[] xOffsets = { -0.15f, 0.15f };
            float[] zOffsets = { -0.12f, 0.14f };

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
                            new Vector3(0.22f, 0.24f, 0.22f),
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

        private static int ReplaceCigarettePacks(
            Transform checkoutRoot,
            Transform outputParent,
            string groupName,
            Dictionary<Material, Material> urpMatCache,
            Dictionary<string, int> spawnedCounts)
        {
            List<(Vector3 pos, Vector3 scale)> packSlots = new List<(Vector3, Vector3)>();
            List<GameObject> toDestroy = new List<GameObject>();

            for (int i = 0; i < checkoutRoot.childCount; i++)
            {
                Transform child = checkoutRoot.GetChild(i);
                if (child.name == "Tobacco_Pack")
                {
                    packSlots.Add((child.position, child.lossyScale));
                    toDestroy.Add(child.gameObject);
                }
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                Object.DestroyImmediate(toDestroy[i]);
            }

            GameObject cigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CigarettePrefabPath);
            if (cigPrefab == null || packSlots.Count == 0)
            {
                return 0;
            }

            GameObject group = new GameObject(groupName);
            group.transform.SetParent(outputParent, false);

            Material[] cigColorMats = EnsureCigaretteColorMaterials();

            int placed = 0;
            for (int i = 0; i < packSlots.Count; i++)
            {
                Vector3 pos = packSlots[i].pos;
                Vector3 targetScale = packSlots[i].scale;
                float bottomY = pos.y - targetScale.y * 0.5f;

                GameObject item = SpawnFittedItem(
                    CigarettePrefabPath,
                    group.transform,
                    new Vector3(pos.x, bottomY, pos.z),
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
                        Material mat = cigColorMats[i % cigColorMats.Length];
                        Renderer[] rends = item.GetComponentsInChildren<Renderer>(true);
                        for (int r = 0; r < rends.Length; r++)
                        {
                            rends[r].sharedMaterial = mat;
                        }
                    }
                    placed++;
                }
            }

            return placed;
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
            "Assets/HQP STUDIOS/Food Pack - Low Poly Sweets/Textures/ColorGrid.png";

        private static readonly string[] AlbedoTexturePropertyCandidates =
        {
            "_BaseMap",
            "_MainTex",
            "_BaseTexture",
            "_Base_Color_Map",
            "_AlbedoMap",
            "_DiffuseMap"
        };

        private static void EnsureUrpMaterialsOnRenderers(GameObject instance, Dictionary<Material, Material> cache)
        {
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

        private static Material GetOrCreateUrpMaterialCopy(Material src, Shader urpLit)
        {
            if (!AssetDatabase.IsValidFolder(UrpCacheDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong/Assets/UI", "ItemUrpMaterials");
            }

            Texture mainTex = FindAlbedoTexture(src);
            string safeName = src.name.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
            string matPath = $"{UrpCacheDir}/URP_{safeName}.mat";
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
