#if UNITY_EDITOR
using System;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.Movement.Unity;
using OpenSourceTeam6.UI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EmployeeVoice.Editor
{
    public static class IntegratedStoreSceneBuilder
    {
        private const string TeamScene = "Assets/_Project/SongJaeMyeong/Scenes/SJM_UI_Work.unity";
        private const string VoiceScene = "Assets/_Project/LeeSeungJae/Scenes/KoreanConvenienceStore.unity";
        private const string Output = "Assets/_Project/LeeSeungJae/Scenes/IntegratedConvenienceStore.unity";
        private const string EmployeePrefab = "Assets/_Project/HaJongUk/Prefabs/EmployeeMovement.prefab";

        [MenuItem("Tools/LeeSeungJae/Create Integrated Store Scene")]
        public static void CreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Output) != null)
                throw new InvalidOperationException("통합 씬이 이미 있습니다. 기존 씬을 덮어쓰지 않습니다: " + Output);

            if (!AssetDatabase.CopyAsset(TeamScene, Output))
                throw new InvalidOperationException("통합 씬 복사에 실패했습니다.");
            Scene scene = EditorSceneManager.OpenScene(Output, OpenSceneMode.Additive);
            StoreUiSessionController ui = Require(FindInScene<StoreUiSessionController>(scene), "팀 UI");
            NpcPopulationController population = Require(FindInScene<NpcPopulationController>(scene), "손님 시스템");
            WorkSceneNavMeshBootstrap navigation = Require(FindInScene<WorkSceneNavMeshBootstrap>(scene), "이동 시스템");
            var demoDriver = Require(FindInScene<UiPrototypeDemoDriver>(scene), "기존 데모 판단기");
            GameObject oldEmployee = Require(FindObject(scene, "Demo_Employee_NPC"), "기존 직원");
            Vector3 employeePosition = oldEmployee.transform.position;
            Object.DestroyImmediate(demoDriver.gameObject);

            GameObject prefab = Require(AssetDatabase.LoadAssetAtPath<GameObject>(EmployeePrefab), "직원 프리팹");
            var employeeObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            employeeObject.name = "Voice Employee";
            employeeObject.transform.SetPositionAndRotation(employeePosition, oldEmployee.transform.rotation);
            EmployeeMovementController employee = Require(employeeObject.GetComponent<EmployeeMovementController>(), "직원 이동 컴포넌트");
            var anchor = new GameObject("Employee Status Anchor").transform;
            anchor.SetParent(employeeObject.transform, false);
            anchor.localPosition = new Vector3(0f, 0.4f, 0f);
            Object.DestroyImmediate(oldEmployee);

            var navSo = new SerializedObject(navigation);
            navSo.FindProperty("demoEmployeeAgent").objectReferenceValue = employee.GetComponent<NavMeshAgent>();
            navSo.ApplyModifiedPropertiesWithoutUndo();

            Scene source = SceneManager.GetSceneByPath(VoiceScene);
            bool closeSource = !source.IsValid() || !source.isLoaded;
            if (closeSource) source = EditorSceneManager.OpenScene(VoiceScene, OpenSceneMode.Additive);
            VoiceMovementIntegration sourceVoice = FindInScene<VoiceMovementIntegration>(source);
            Require(sourceVoice, "기존 음성 설정");
            var voiceObject = new GameObject("OpenAI Voice Integration");
            SceneManager.MoveGameObjectToScene(voiceObject, scene);
            var voice = voiceObject.AddComponent<VoiceMovementIntegration>();
            EditorUtility.CopySerialized(sourceVoice, voice);
            var voiceSo = new SerializedObject(voice);
            Set(voiceSo, "existingUi", ui);
            Set(voiceSo, "employee", employee);
            Set(voiceSo, "employeeStatusAnchor", anchor);
            Set(voiceSo, "businessTime", null);
            Set(voiceSo, "restockDestination", Point(voiceObject.transform, "Snack", -3f, 0.65f));
            Set(voiceSo, "restockSnackDestination", Point(voiceObject.transform, "Snack", -3f, 0.65f));
            Set(voiceSo, "restockRamenDestination", Point(voiceObject.transform, "Ramen", -0.9f, 0.65f));
            Set(voiceSo, "restockDailyGoodsDestination", Point(voiceObject.transform, "Daily goods", 1.225f, 0.65f));
            Set(voiceSo, "restockIceCreamDestination", Point(voiceObject.transform, "Ice cream", 3.45f, 0.65f));
            Set(voiceSo, "restockWalkInDestination", Point(voiceObject.transform, "Walk-in", -5.5f, 1.2f));
            Set(voiceSo, "restockHotFoodDestination", Point(voiceObject.transform, "Hot food", 1f, -1.8f));
            Set(voiceSo, "cleanDestination", Point(voiceObject.transform, "Clean", 4.25f, -1.25f));
            Transform cashier = Point(voiceObject.transform, "Cashier", -6f, -1.4f);
            Set(voiceSo, "paymentDestination", cashier);
            Set(voiceSo, "cashierDestination", cashier);
            voiceSo.ApplyModifiedPropertiesWithoutUndo();
            if (closeSource) EditorSceneManager.CloseScene(source, true);
            EditorSceneManager.SetActiveScene(scene);

            var sync = voiceObject.AddComponent<IntegratedNpcSession>();
            var syncSo = new SerializedObject(sync);
            Set(syncSo, "ui", ui);
            Set(syncSo, "population", population);
            syncSo.ApplyModifiedPropertiesWithoutUndo();

            IntegratedShelfCollision.Apply(scene);

            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("통합 씬 저장 실패");
            AssetDatabase.SaveAssets();
            Debug.Log("[IntegratedStore] Created " + Output);
        }

        private static Transform Point(Transform parent, string name, float x, float z)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = new Vector3(x, 0f, z);
            return point;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private static GameObject FindObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    if (node.name == name) return node.gameObject;
            return null;
        }

        private static void Set(SerializedObject obj, string name, Object value)
        {
            SerializedProperty property = obj.FindProperty(name);
            if (property == null) throw new InvalidOperationException("필드 없음: " + name);
            property.objectReferenceValue = value;
        }

        private static T Require<T>(T value, string name) where T : Object
        {
            if (value == null) throw new InvalidOperationException(name + "을(를) 찾지 못했습니다.");
            return value;
        }
    }
}
#endif
