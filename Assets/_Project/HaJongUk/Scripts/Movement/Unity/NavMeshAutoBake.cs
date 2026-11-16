using Unity.AI.Navigation;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OpenSourceTeam6.Movement.Unity
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(NavMeshSurface))]
    public sealed class NavMeshAutoBake : MonoBehaviour
    {
        [Tooltip("에디터에서 바닥 등 자식 콜라이더가 바뀌면 NavMesh를 다시 베이크합니다.")]
        public bool autoUpdate = true;

#if UNITY_EDITOR
        private const double DelaySeconds = 0.75;
        private NavMeshSurface surface;
        private int geometryHash;
        private bool initialized;
        private bool wasEnabled;
        private double changedAt;
        private bool pending;

        private void OnEnable()
        {
            surface = GetComponent<NavMeshSurface>();
            initialized = false;
            EditorApplication.update -= UpdateEditor;
            EditorApplication.update += UpdateEditor;
        }

        private void OnDisable() { EditorApplication.update -= UpdateEditor; pending = false; }

        private void UpdateEditor()
        {
            if (this == null || !isActiveAndEnabled || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (surface == null) surface = GetComponent<NavMeshSurface>();
            if (surface == null || surface.collectObjects != CollectObjects.Children ||
                surface.useGeometry != UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders)
            {
                pending = false;
                initialized = false;
                return;
            }

            int current = CalculateGeometryHash();
            if (!initialized)
            {
                geometryHash = current;
                initialized = true;
                wasEnabled = autoUpdate;
                return;
            }
            if (!autoUpdate)
            {
                geometryHash = current;
                wasEnabled = false;
                pending = false;
                return;
            }
            if (!wasEnabled || current != geometryHash)
            {
                geometryHash = current;
                changedAt = EditorApplication.timeSinceStartup;
                pending = true;
            }
            wasEnabled = true;
            if (!pending || EditorApplication.timeSinceStartup - changedAt < DelaySeconds) return;
            pending = false;
            Bake();
        }

        private int CalculateGeometryHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                {
                    hash = hash * 31 + collider.GetHashCode();
                    hash = hash * 31 + collider.enabled.GetHashCode();
                    hash = hash * 31 + collider.gameObject.activeInHierarchy.GetHashCode();
                    hash = hash * 31 + collider.transform.localToWorldMatrix.GetHashCode();
                    hash = hash * 31 + collider.bounds.GetHashCode();
                    hash = hash * 31 + collider.gameObject.layer;
                    if (collider is MeshCollider mesh)
                        hash = hash * 31 + (mesh.sharedMesh != null ? mesh.sharedMesh.GetHashCode() : 0);
                }
                return hash;
            }
        }

        private void Bake()
        {
            var existing = surface.navMeshData;
            string path = existing == null ? null : AssetDatabase.GetAssetPath(existing);
            if (existing == null || string.IsNullOrEmpty(path) || !AssetDatabase.IsOpenForEdit(existing))
            {
                Debug.LogWarning("자동 NavMesh 갱신에는 편집 가능한 기존 NavMesh 에셋이 필요합니다.", this);
                return;
            }
            surface.BuildNavMesh();
            var baked = surface.navMeshData;
            if (baked == null || baked == existing)
            {
                Debug.LogError("NavMesh 자동 베이크에 실패했습니다.", this);
                return;
            }
            surface.RemoveData();
            EditorUtility.CopySerialized(baked, existing);
            // CopySerialized also copies the temporary bake's name. Unity expects
            // the main asset name to match its filename when saving it.
            existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
            surface.navMeshData = existing;
            surface.AddData();
            DestroyImmediate(baked);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            Debug.Log("변경된 지형에 맞춰 NavMesh를 갱신했습니다: " + path, this);
        }
#endif
    }
}
