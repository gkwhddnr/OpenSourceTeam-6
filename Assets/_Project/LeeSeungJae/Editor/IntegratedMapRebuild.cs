#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace EmployeeVoice.Editor
{
    // One-time migration for the saved integrated scene. Keep existing object references intact.
    public static class IntegratedMapRebuild
    {
        private const string ScenePath = "Assets/_Project/LeeSeungJae/Scenes/IntegratedConvenienceStore.unity";
        private const float ShelfCenterZ = 2.60f;
        private const float ShelfLength = 6.0f;
        private const float OriginalShelfZ = 0.65f;
        private const float ProductStretch = 1.72f;

        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform map = Root(scene, "ConvenienceStore_Map");
            if (map.Find("Layout_Rebuilt_2026") == null) throw new InvalidOperationException("Rebuilt layout marker missing.");
            Transform table = Child(map, "Table_and_Stools");
            if (table.Cast<Transform>().Count(t => t.name == "Stool_Seat") != 5 ||
                table.Cast<Transform>().Count(t => t.name == "Stool_Base") != 5 ||
                table.Cast<Transform>().Count(t => t.name.StartsWith("Stool_Collision_", StringComparison.Ordinal)) != 5)
                throw new InvalidOperationException("Expected five complete stools with collision.");
            Child(table, "Microwave_1"); Child(table, "Microwave_2"); Child(table, "HotWater_Dispenser");
            Transform bins = Child(map, "ATM_and_Bin");
            if (bins.Cast<Transform>().Count(t => t.name.StartsWith("Trash_Bin", StringComparison.Ordinal)) != 3)
                throw new InvalidOperationException("Expected three bins.");
            Bounds previous = default;
            foreach (string name in new[] { "Snack_Shelf", "Ramen_Shelf", "DailyGoods_Shelf" })
            {
                Transform shelf = Child(map, name);
                BoxCollider collision = Child(shelf, "Shelf Collision").GetComponent<BoxCollider>();
                if (collision == null || !collision.enabled || collision.isTrigger) throw new InvalidOperationException("Shelf collision missing: " + name);
                Bounds b = collision.bounds;
                if (previous.size.x > 0f && b.min.x - previous.max.x < 1.45f)
                    throw new InvalidOperationException("Shelf aisle is too narrow: " + name);
                previous = b;
                Debug.Log("[MapVerify] Shelf collision " + name + " " + b);
            }
            Transform storefront = Child(map, "Storefront");
            if (storefront.Cast<Transform>().Count(t => t.name.StartsWith("Window_Frame_", StringComparison.Ordinal)) != 11)
                throw new InvalidOperationException("Window frames missing.");
            var so = new SerializedObject(Root(scene, "OpenAI Voice Integration").GetComponent<VoiceMovementIntegration>());
            ValidateNavigation(scene, so, Child(Root(scene, "Demo_StoreStage"), "Store_CustomerNpcSystem"));
            Transform items = Child(map, "Store_RealSellableItems");
            foreach (string name in new[] { "Snack_Items", "Ramen_Items", "Daily_Items", "IceCream_Items", "WalkIn_Drink_Items", "FreshFood_FF_Items" })
                Debug.Log("[MapVerify] Merchandise " + name + " objects=" + Child(items, name).childCount);
            Debug.Log("[MapVerify] Saved layout, five stools, three bins, windows and navigation passed.");
        }

        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform map = Root(scene, "ConvenienceStore_Map");
            if (map.Find("Layout_Rebuilt_2026") != null)
                throw new InvalidOperationException("This layout has already been rebuilt.");
            Transform architecture = Child(map, "Architecture");
            Transform floor = Child(architecture, "Floor");
            Bounds bounds = floor.GetComponent<Renderer>().bounds;
            if (bounds.size.x < 15.5f || bounds.size.z < 10.5f || bounds.size.x > 20f || bounds.size.z > 15f)
                throw new InvalidOperationException("Expected the user's expanded floor before rebuilding: " + bounds);

            float left = bounds.min.x, right = bounds.max.x, back = bounds.min.z, front = bounds.max.z;
            float cx = bounds.center.x, cz = bounds.center.z;
            float frontDelta = front - 3.66f;
            float doorX = 5.45f;
            float doorLeft = doorX - 0.9f, doorRight = doorX + 0.9f;

            SetPart(Child(architecture, "Back_Wall"), new Vector3(cx, 1.5f, back - 0.08f), new Vector3(bounds.size.x + 0.2f, 3f, 0.16f));
            SetPart(Child(architecture, "Back_Baseboard"), new Vector3(cx, 0.1f, back + 0.02f), new Vector3(bounds.size.x, 0.2f, 0.05f));
            SetPart(Child(architecture, "Left_Wall"), new Vector3(left - 0.08f, 1.5f, cz), new Vector3(0.16f, 3f, bounds.size.z));
            SetPart(Child(architecture, "Right_Wall"), new Vector3(right + 0.08f, 1.5f, cz), new Vector3(0.16f, 3f, bounds.size.z));
            ExtendFloorJoints(architecture, bounds);

            Transform storefront = Child(map, "Storefront");
            foreach (Transform child in storefront) child.position += Vector3.forward * frontDelta;
            StretchX(Child(storefront, "Window_Header"), left, right);
            StretchX(Child(storefront, "Lower_Window_Wall"), left, doorLeft);
            StretchX(Child(storefront, "Window_Glass_Left"), left + 0.03f, doorLeft);
            StretchX(Child(storefront, "Lower_Wall_Right"), doorRight, right);
            StretchX(Child(storefront, "Window_Glass_Right"), doorRight, right - 0.03f);
            foreach (Transform child in storefront)
                if (child.name == "Door_Header" || child.name == "Door_Threshold" || child.name == "Entrance_Jamb")
                    child.position += Vector3.right * (doorX - 4.25f);
            ArrangeWindowFrames(storefront, left, right, doorLeft, doorRight);

            Transform items = Child(map, "Store_RealSellableItems");
            RebuildShelf(Child(map, "Snack_Shelf"), Child(items, "Snack_Items"), -4.50f, -4.05f);
            RebuildShelf(Child(map, "Ramen_Shelf"), Child(items, "Ramen_Items"), -2.00f, -1.95f);
            RebuildShelf(Child(map, "DailyGoods_Shelf"), Child(items, "Daily_Items"), 0.50f, 0.15f);
            RebuildFreezer(Child(map, "IceCream_Freezer"), Child(items, "IceCream_Items"));
            RebuildWalkIn(Child(map, "WalkIn_and_Storage"), Child(items, "WalkIn_Drink_Items"));
            RebuildHotFood(Child(map, "HotFood"), Child(items, "FreshFood_FF_Items"));
            RebuildTable(Child(map, "Table_and_Stools"), right);
            RebuildAtmAndBins(Child(map, "ATM_and_Bin"), right, front);

            Transform outdoor = Child(map, "Outdoor_View");
            foreach (Transform child in outdoor) child.position += new Vector3(cx, 0f, frontDelta);
            foreach (string name in new[] { "Sidewalk", "Sidewalk_Edge" })
            {
                Transform t = Child(outdoor, name);
                Vector3 size = t.localScale; size.x = bounds.size.x; t.localScale = size;
            }
            // The navigation surface uses physics geometry; the decorative sidewalk
            // originally had no collider and could not connect the outdoor route.
            Transform sidewalk = Child(outdoor, "Sidewalk");
            if (sidewalk.GetComponent<Collider>() == null) sidewalk.gameObject.AddComponent<BoxCollider>();

            Transform voice = Root(scene, "OpenAI Voice Integration");
            // Use the serialized references, not object names: the scene has two Snack nodes.
            var so = new SerializedObject(voice.GetComponent<VoiceMovementIntegration>());
            SetDestination(so, "restockDestination", -3.25f, 2.6f);
            SetDestination(so, "restockSnackDestination", -3.25f, 2.6f);
            SetDestination(so, "restockRamenDestination", -0.75f, 2.6f);
            SetDestination(so, "restockDailyGoodsDestination", 1.75f, 2.6f);
            SetDestination(so, "restockIceCreamDestination", 4.20f, 2.6f);
            SetDestination(so, "restockWalkInDestination", -5.55f, 2.6f);
            SetDestination(so, "restockHotFoodDestination", 2.0f, -1.65f);
            SetDestination(so, "cleanDestination", 6.35f, 0.5f);
            SetDestination(so, "paymentDestination", -6.0f, -1.4f);
            SetDestination(so, "cashierDestination", -6.0f, -1.4f);

            Transform npc = Child(Root(scene, "Demo_StoreStage"), "Store_CustomerNpcSystem");
            SetXZ(Child(npc, "Customer_Entrance"), doorX, front - 0.65f);
            SetXZ(Child(npc, "Customer_Exit"), doorX, front + 0.65f);
            SetXZ(Child(npc, "Sidewalk_Left"), left + 1.8f, front + 0.95f);
            SetXZ(Child(npc, "Sidewalk_Right"), right - 1.8f, front + 0.95f);
            SetXZ(Child(npc, "Customer_Shelf"), -0.75f, 2.6f);
            Transform stage = Root(scene, "Demo_StoreStage");
            Transform targetShelf = stage.Find("Target_Shelf");
            if (targetShelf != null) SetXZ(targetShelf, -0.75f, 2.6f);

            IntegratedShelfCollision.Apply(scene);
            Physics.SyncTransforms();
            ValidateNavigation(scene, so, npc);
            var marker = new GameObject("Layout_Rebuilt_2026");
            marker.transform.SetParent(map, false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the scene.");
            Debug.Log("[MapRebuild] Saved scene after validating employee destinations and customer routes.");
        }

        private static void RebuildShelf(Transform shelf, Transform products, float centerX, float oldX)
        {
            foreach (Transform child in shelf)
            {
                if (child.name != "Shelf_Base" && child.name != "Shelf_Center" && child.name != "Shelf_Board") continue;
                Vector3 p = child.position; p.x = centerX; p.z = ShelfCenterZ; child.position = p;
                Vector3 s = child.localScale; s.z = child.name == "Shelf_Base" ? ShelfLength : ShelfLength - 0.05f; child.localScale = s;
            }
            StretchProductPositions(products, centerX - oldX);
        }

        private static void StretchProductPositions(Transform products, float dx)
        {
            foreach (Transform child in products)
            {
                Vector3 p = child.position;
                p.x += dx; p.z = ShelfCenterZ + (p.z - OriginalShelfZ) * ProductStretch;
                child.position = p;
            }
        }

        private static void RebuildFreezer(Transform freezer, Transform products)
        {
            foreach (Transform child in freezer)
            {
                Vector3 p = child.position; p.x += 0.65f;
                if (child.name == "IceCream_Freezer_Base" || child.name == "Freezer_Lid")
                {
                    p.z = ShelfCenterZ;
                    Vector3 s = child.localScale; s.z = child.name == "Freezer_Lid" ? ShelfLength - 0.15f : ShelfLength; child.localScale = s;
                }
                else p.z = ShelfCenterZ + (p.z - OriginalShelfZ) * ProductStretch;
                child.position = p;
            }
            StretchProductPositions(products, 0.65f);
        }

        private static void RebuildWalkIn(Transform walkIn, Transform products)
        {
            Transform cooler = Child(walkIn, "WalkIn_Cooler");
            Vector3 p = cooler.position; p.z = 2.7f; cooler.position = p;
            Vector3 s = cooler.localScale; s.z = 6.2f; cooler.localScale = s;
            var doors = walkIn.Cast<Transform>().Where(t => t.name == "Fridge_Door" || t.name == "Fridge_Handle").ToArray();
            foreach (Transform t in doors)
            {
                Vector3 position = t.position; position.z += 0.35f; t.position = position;
                if (position.z < (t.name == "Fridge_Handle" ? 2.5f : 3f))
                {
                    Transform copy = UnityEngine.Object.Instantiate(t, walkIn);
                    copy.name = t.name;
                    copy.position = position + Vector3.forward * 3.4f;
                }
            }
            foreach (Transform child in products)
            {
                p = child.position;
                if (child.name.StartsWith("WalkIn_InnerShelf_", StringComparison.Ordinal))
                {
                    p.z = 2.7f;
                    s = child.localScale; s.z = 6.0f; child.localScale = s;
                }
                else p.z = 2.7f + (p.z - 1.5f) * 1.6f;
                child.position = p;
            }
        }

        private static void RebuildHotFood(Transform hotFood, Transform products)
        {
            foreach (Transform child in hotFood)
            {
                Vector3 p = child.position;
                if (child.name == "HotFood_Counter" || child.name == "HotFood_Top")
                {
                    p.x = 2.20f;
                    Vector3 s = child.localScale; s.x = child.name == "HotFood_Top" ? 6.6f : 6.5f; child.localScale = s;
                }
                else p.x = 2.20f + (p.x - 1.35f) * 1.3f;
                child.position = p;
            }
            foreach (Transform child in products)
            {
                Vector3 p = child.position; p.x = 2.20f + (p.x - 1.35f) * 1.3f; child.position = p;
            }
        }

        private static void RebuildTable(Transform table, float right)
        {
            table.position += new Vector3(right - 7.0f, 0f, 1.7f);
            Transform top = Child(table, "Wall_Table");
            Vector3 scale = top.localScale; scale.z = 5.5f; top.localScale = scale;
            var seats = table.Cast<Transform>().Where(t => t.name == "Stool_Seat").OrderBy(t => t.position.z).ToList();
            var bases = table.Cast<Transform>().Where(t => t.name == "Stool_Base").OrderBy(t => t.position.z).ToList();
            if (seats.Count != 4 || bases.Count != 4) throw new InvalidOperationException("Expected four existing stools.");
            seats.Add(UnityEngine.Object.Instantiate(seats[0], table));
            bases.Add(UnityEngine.Object.Instantiate(bases[0], table));
            for (int i = 0; i < 5; i++)
            {
                seats[i].name = "Stool_Seat"; bases[i].name = "Stool_Base";
                float z = -1.5f + i;
                Vector3 a = seats[i].position; a.z = z; seats[i].position = a;
                Vector3 b = bases[i].position; b.z = z; bases[i].position = b;
                var collision = new GameObject("Stool_Collision_" + (i + 1));
                collision.transform.SetParent(table, true);
                collision.transform.position = new Vector3(a.x, 0.30f, z);
                BoxCollider box = collision.AddComponent<BoxCollider>();
                box.size = new Vector3(0.48f, 0.6f, 0.48f);
            }
        }

        private static void RebuildAtmAndBins(Transform group, float right, float front)
        {
            float dx = right - 7.0f;
            Transform atm = Child(group, "ATM"), screen = Child(group, "ATM_Screen");
            foreach (Transform t in new[] { atm, screen }) t.position += new Vector3(dx, 0f, front - 3.66f);
            Transform bin = Child(group, "Trash_Bin"), lid = Child(group, "Trash_Lid");
            foreach (Transform t in new[] { bin, lid })
            {
                Vector3 p = t.position; p.x += dx; p.z = front - 2.3f; t.position = p;
                for (int i = 1; i < 3; i++)
                {
                    Transform copy = UnityEngine.Object.Instantiate(t, group);
                    copy.name = t.name + "_" + (i + 1);
                    copy.position = p + Vector3.back * (0.76f * i);
                }
            }
        }

        private static void ExtendFloorJoints(Transform architecture, Bounds floor)
        {
            var xJoints = architecture.Cast<Transform>().Where(t => t.name.StartsWith("Floor_Joint_X_", StringComparison.Ordinal)).ToList();
            var zJoints = architecture.Cast<Transform>().Where(t => t.name.StartsWith("Floor_Joint_Z_", StringComparison.Ordinal)).ToList();
            if (xJoints.Count == 0 || zJoints.Count == 0) throw new InvalidOperationException("Floor joints missing.");
            for (int x = Mathf.CeilToInt(floor.min.x); x <= Mathf.FloorToInt(floor.max.x); x++)
            {
                Transform t = xJoints.FirstOrDefault(a => Mathf.Abs(a.position.x - x) < 0.1f);
                if (t == null) { t = UnityEngine.Object.Instantiate(xJoints[0], architecture); t.name = "Floor_Joint_X_" + x; }
                SetPart(t, new Vector3(x, 0.004f, floor.center.z), new Vector3(0.012f, 0.008f, floor.size.z));
            }
            for (int z = Mathf.CeilToInt(floor.min.z); z <= Mathf.FloorToInt(floor.max.z); z++)
            {
                Transform t = zJoints.FirstOrDefault(a => Mathf.Abs(a.position.z - z) < 0.1f);
                if (t == null) { t = UnityEngine.Object.Instantiate(zJoints[0], architecture); t.name = "Floor_Joint_Z_" + z; }
                SetPart(t, new Vector3(floor.center.x, 0.004f, z), new Vector3(floor.size.x, 0.008f, 0.012f));
            }
        }

        private static void ArrangeWindowFrames(Transform storefront, float left, float right, float doorLeft, float doorRight)
        {
            var frames = storefront.Cast<Transform>().Where(t => t.name.StartsWith("Window_Frame_", StringComparison.Ordinal)).OrderBy(t => t.position.x).ToList();
            if (frames.Count != 9) throw new InvalidOperationException("Unexpected number of original window frames.");
            while (frames.Count < 11) frames.Add(UnityEngine.Object.Instantiate(frames[0], storefront));
            for (int i = 0; i < frames.Count; i++)
            {
                float x = i < 8 ? Mathf.Lerp(left + 0.06f, doorLeft, i / 7f) : Mathf.Lerp(doorRight, right - 0.06f, (i - 8) / 2f);
                Vector3 p = frames[i].position; p.x = x; frames[i].position = p; frames[i].name = "Window_Frame_" + (i + 1).ToString("00");
            }
        }

        private static void ValidateNavigation(Scene scene, SerializedObject voice, Transform npc)
        {
            NavMeshSurface surface = Root(scene, "Environment_Navigation").GetComponent<NavMeshSurface>();
            if (surface == null) throw new InvalidOperationException("NavMeshSurface missing.");
            NavMeshData original = surface.navMeshData;
            try
            {
                surface.BuildNavMesh();
                if (surface.navMeshData == null) throw new InvalidOperationException("Navigation mesh build failed.");
                Vector3 employee = Root(scene, "Voice Employee").position;
                if (!NavMesh.SamplePosition(employee, out NavMeshHit start, 1f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Employee is not near navigable ground.");
                foreach (string name in new[] { "restockSnackDestination", "restockRamenDestination", "restockDailyGoodsDestination", "restockIceCreamDestination", "restockWalkInDestination", "restockHotFoodDestination", "cleanDestination", "cashierDestination" })
                {
                    Transform t = (Transform)voice.FindProperty(name).objectReferenceValue;
                    CheckPath(start.position, t.position, name);
                }
                CheckPath(Child(npc, "Customer_Exit").position, Child(npc, "Customer_Entrance").position, "customer entrance");
                CheckPath(Child(npc, "Customer_Entrance").position, Child(npc, "Customer_Shelf").position, "customer shelf");
                CheckPath(Child(npc, "Customer_Shelf").position, Child(npc, "Customer_Checkout").position, "customer checkout");
            }
            finally
            {
                surface.RemoveData();
                surface.navMeshData = original;
                if (original != null) surface.AddData();
            }
        }

        private static void CheckPath(Vector3 from, Vector3 to, string label)
        {
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out NavMeshHit a, 0.55f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out NavMeshHit b, 0.55f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("No complete navigation path for " + label + " to " + to);
            Debug.Log("[MapRebuild] Reachable " + label + " " + b.position);
        }

        private static void SetDestination(SerializedObject voice, string field, float x, float z)
        {
            SerializedProperty property = voice.FindProperty(field);
            Transform destination = property != null ? property.objectReferenceValue as Transform : null;
            if (destination == null) throw new InvalidOperationException("Missing destination reference: " + field);
            SetXZ(destination, x, z);
        }

        private static void SetXZ(Transform t, float x, float z) { Vector3 p = t.position; p.x = x; p.z = z; t.position = p; }
        private static void SetPart(Transform t, Vector3 position, Vector3 scale) { t.position = position; t.localScale = scale; }
        private static void StretchX(Transform t, float left, float right)
        { Vector3 p = t.position; p.x = (left + right) * 0.5f; t.position = p; Vector3 s = t.localScale; s.x = right - left; t.localScale = s; }
        private static Transform Child(Transform parent, string name) => parent.Find(name) ?? throw new InvalidOperationException("Missing " + parent.name + "/" + name);
        private static Transform Root(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(o => o.name == name)?.transform ?? throw new InvalidOperationException("Missing root: " + name);
    }
}
#endif
