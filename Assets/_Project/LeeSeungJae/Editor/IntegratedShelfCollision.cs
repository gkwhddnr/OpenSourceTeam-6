#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace EmployeeVoice.Editor
{
    public static class IntegratedShelfCollision
    {
        private const string ScenePath = "Assets/_Project/LeeSeungJae/Scenes/IntegratedConvenienceStore.unity";
        private static readonly string[] ShelfNames = { "Snack_Shelf", "Ramen_Shelf", "DailyGoods_Shelf" };

        [MenuItem("Tools/LeeSeungJae/Fix Integrated Shelf Collision")]
        public static void FixOpenScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("통합 씬을 편집 모드로 열고 실행해 주세요.");
            if (scene.isDirty)
                throw new InvalidOperationException("통합 씬에 저장되지 않은 변경 사항이 있어 자동 저장을 중단했습니다.");
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("통합 씬 저장에 실패했습니다.");
            Debug.Log("[ShelfCollision] 세 진열대 충돌체를 적용하고 저장했습니다.");
        }

        public static void Apply(Scene scene)
        {
            foreach (string name in ShelfNames)
            {
                Transform shelf = Find(scene, name);
                if (shelf == null) throw new InvalidOperationException("진열대를 찾지 못했습니다: " + name);
                bool hasBounds = false;
                Bounds localBounds = default;
                foreach (Transform child in shelf.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != "Shelf_Base" && child.name != "Shelf_Center" && child.name != "Shelf_Board") continue;
                    Renderer renderer = child.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    Bounds world = renderer.bounds;
                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = world.center + Vector3.Scale(world.extents, new Vector3(x, y, z));
                        Vector3 local = shelf.InverseTransformPoint(corner);
                        if (!hasBounds) { localBounds = new Bounds(local, Vector3.zero); hasBounds = true; }
                        else localBounds.Encapsulate(local);
                    }
                }
                if (!hasBounds) throw new InvalidOperationException("진열대 크기를 확인하지 못했습니다: " + name);
                // Cover the shelf as a solid unit, including the merchandise above its top board.
                Vector3 min = localBounds.min;
                Vector3 max = localBounds.max;
                max.y = Mathf.Max(max.y, min.y + 1.745f);
                min.x -= 0.01f; max.x += 0.01f;
                min.z -= 0.01f; max.z += 0.01f;
                Transform existing = shelf.Find("Shelf Collision");
                GameObject collision = existing != null ? existing.gameObject : new GameObject("Shelf Collision");
                if (existing == null)
                {
                    Undo.RegisterCreatedObjectUndo(collision, "Add shelf collision");
                    collision.transform.SetParent(shelf, false);
                }
                BoxCollider box = collision.GetComponent<BoxCollider>();
                if (box == null) box = Undo.AddComponent<BoxCollider>(collision);
                Undo.RecordObject(box, "Set shelf collision size");
                box.center = (min + max) * 0.5f;
                box.size = max - min;
                box.isTrigger = false;
                Debug.Log("[ShelfCollision] " + name + " center=" + box.center + " size=" + box.size, box);
            }
        }

        [MenuItem("Tools/LeeSeungJae/Verify Integrated Shelf Collision")]
        public static void Verify()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!EditorApplication.isPlaying || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("통합 씬을 재생한 상태에서 검증해 주세요.");
            Physics.SyncTransforms();
            foreach (string name in ShelfNames)
            {
                Transform shelf = Find(scene, name);
                Transform collision = shelf != null ? shelf.Find("Shelf Collision") : null;
                BoxCollider box = collision != null ? collision.GetComponent<BoxCollider>() : null;
                if (box == null || !box.enabled || box.isTrigger) throw new InvalidOperationException(name + " 충돌체가 없습니다.");
                Bounds bounds = box.bounds;
                var probe = new GameObject("Shelf collision verification");
                try
                {
                    probe.hideFlags = HideFlags.HideAndDontSave;
                    var controller = probe.AddComponent<CharacterController>();
                    controller.height = 1.8f;
                    controller.radius = 0.3f;
                    controller.center = new Vector3(0, 0.9f, 0);
                    probe.transform.position = new Vector3(bounds.min.x - 0.8f, bounds.min.y + 0.01f, bounds.center.z);
                    Physics.SyncTransforms();
                    CollisionFlags flags = controller.Move(Vector3.right * 1.6f);
                    if ((flags & CollisionFlags.Sides) == 0 || probe.transform.position.x >= bounds.min.x)
                        throw new InvalidOperationException(name + " 플레이어 충돌 검증 실패");

                    var path = new NavMeshPath();
                    Vector3 left = new Vector3(bounds.min.x - 0.7f, bounds.min.y, bounds.center.z);
                    Vector3 right = new Vector3(bounds.max.x + 0.7f, bounds.min.y, bounds.center.z);
                    if (!NavMesh.SamplePosition(left, out NavMeshHit a, 0.4f, NavMesh.AllAreas) ||
                        !NavMesh.SamplePosition(right, out NavMeshHit b, 0.4f, NavMesh.AllAreas) ||
                        !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                        Debug.LogWarning("[ShelfCollisionCheck] " + name + " 이동 경로는 별도 확인 필요");
                    else
                    {
                        foreach (Vector3 point in path.corners)
                            if (point.x > bounds.min.x && point.x < bounds.max.x && point.z > bounds.min.z && point.z < bounds.max.z)
                                throw new InvalidOperationException(name + " 이동 경로가 선반 내부를 지납니다.");
                        if (NavMesh.Raycast(a.position, b.position, out _, NavMesh.AllAreas) == false)
                            throw new InvalidOperationException(name + " 이동 가능 영역이 선반을 가로지릅니다.");
                        Debug.Log("[ShelfCollisionCheck] " + name + " 플레이어 충돌 및 이동 우회 확인");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(probe); }
            }
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    if (node.name == name) return node;
            return null;
        }
    }
}
#endif
