using System;
using System.IO;
using OpenSourceTeam6.Camera.Unity;
using OpenSourceTeam6.Movement.Unity;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace OpenSourceTeam6.Movement.Editor
{
    public static class MovementSceneBuilder
    {
        public const string Root = "Assets/_Project/HaJongUk";
        public const string WorkScene = Root + "/Scenes/HJU_CameraMovement_Work.unity";
        public const string SharedScene = "Assets/_Project/Shared/Scenes/Team_Integration.unity";
        public const string PlayerPrefab = Root + "/Prefabs/PlayerRig.prefab";
        public const string EmployeePrefab = Root + "/Prefabs/EmployeeMovement.prefab";

        [MenuItem("OpenSourceTeam6/Movement/Create Initial Scenes")]
        public static void CreateInitialScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes first.");
            if (File.Exists(WorkScene) || File.Exists(SharedScene) || File.Exists(PlayerPrefab) || File.Exists(EmployeePrefab))
                throw new InvalidOperationException("Initial assets already exist. Edit them directly; this command never overwrites scenes/prefabs.");

            EnsureFolder(Root + "/Scenes");
            EnsureFolder("Assets/_Project/Shared/Scenes");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Assets/Settings");
            EnsureFolder(Root + "/Assets/Input");
            EnsureFolder(Root + "/Assets/Materials");
            EnsureFolder(Root + "/Assets/Navigation");
            EnsureFolder("Assets/_Project/Shared/Assets/Navigation");
            var original = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scratch);
                var profile = ScriptableObject.CreateInstance<MovementProfile>();
                AssetDatabase.CreateAsset(profile, Root + "/Assets/Settings/DefaultMovement.asset");
                var input = CreateInput();
                var floor = Material("Floor", new Color(0.22f, 0.28f, 0.33f));
                var wall = Material("Wall", new Color(0.75f, 0.79f, 0.78f));
                var shelf = Material("Obstacle", new Color(0.23f, 0.40f, 0.43f));
                var staff = Material("Employee", new Color(0.95f, 0.54f, 0.15f));
                var target = Material("Target", new Color(0.21f, 0.85f, 0.59f));
                var danger = Material("Unreachable", new Color(0.85f, 0.25f, 0.28f));
                CreatePlayer(profile, input);
                CreateEmployee(profile, staff);
                EditorSceneManager.CloseScene(scratch, true);
                CreateScene(WorkScene, true, floor, wall, shelf, target, danger);
                CreateScene(SharedScene, false, floor, wall, shelf, target, danger);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(original); }
            EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
            Debug.Log("Camera/movement prefabs and independent work/integration scenes created.");
        }

        private static InputActionAsset CreateInput()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplay = new InputActionMap("Gameplay");
            var move = gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            gameplay.AddAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            actions.AddActionMap(gameplay);
            string path = Root + "/Assets/Input/HJU_Player.inputactions";
            File.WriteAllText(path, actions.ToJson());
            UnityEngine.Object.DestroyImmediate(actions);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        private static void CreatePlayer(MovementProfile profile, InputActionAsset input)
        {
            var root = new GameObject("PlayerRig");
            var collider = root.AddComponent<CharacterController>();
            collider.height = 1.8f; collider.radius = 0.3f; collider.center = new Vector3(0, 0.9f, 0);
            collider.stepOffset = 0.25f; collider.skinWidth = 0.03f; collider.minMoveDistance = 0;
            var eye = new GameObject("EyePivot");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0, 1.6f, 0);
            var camera = new GameObject("MainCamera");
            camera.tag = "MainCamera";
            camera.transform.SetParent(eye.transform, false);
            var view = camera.AddComponent<UnityEngine.Camera>();
            view.nearClipPlane = 0.05f; view.farClipPlane = 100f; view.fieldOfView = 75f;
            camera.AddComponent<AudioListener>();
            var presenter = root.AddComponent<FirstPersonCameraPresenter>();
            Assign(presenter, "eyePivot", eye.transform); Assign(presenter, "viewCamera", view);
            var reader = root.AddComponent<PlayerInputReader>(); Assign(reader, "actions", input);
            var gate = root.AddComponent<PlayerInputGate>();
            var motor = root.AddComponent<CharacterControllerMotor>();
            var rig = root.AddComponent<PlayerRigController>();
            Assign(rig, "profile", profile); Assign(rig, "input", reader); Assign(rig, "inputGate", gate);
            Assign(rig, "motor", motor); Assign(rig, "cameraPresenter", presenter);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateEmployee(MovementProfile profile, Material material)
        {
            var root = new GameObject("EmployeeMovement");
            var agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.8f; agent.radius = 0.3f; agent.baseOffset = 0f;
            agent.acceleration = 12f; agent.angularSpeed = 360f; agent.stoppingDistance = 0.3f;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "EmployeeVisual"; visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0, 0.9f, 0);
            visual.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            var driver = root.AddComponent<NavMeshAgentDriver>();
            var controller = root.AddComponent<EmployeeMovementController>();
            Assign(controller, "profile", profile); Assign(controller, "driver", driver);
            PrefabUtility.SaveAsPrefabAsset(root, EmployeePrefab);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateScene(string path, bool work, Material floor, Material wall, Material shelf, Material target, Material danger)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var environment = new GameObject("Environment_Navigation");
            Box("Floor", new Vector3(0, -0.15f, 0), new Vector3(18, 0.3f, 18), floor, environment.transform);
            Box("Wall_Back", new Vector3(0, 1.5f, 9), new Vector3(18, 3, 0.3f), wall, environment.transform);
            Box("Wall_Left", new Vector3(-9, 1.5f, 0), new Vector3(0.3f, 3, 18), wall, environment.transform);
            Box("Wall_Right", new Vector3(9, 1.5f, 0), new Vector3(0.3f, 3, 18), wall, environment.transform);
            Box("Wall_Front", new Vector3(0, 1.5f, -9), new Vector3(18, 3, 0.3f), wall, environment.transform);
            Box("Shelf_A", new Vector3(-1.5f, 0.7f, 1), new Vector3(1.2f, 1.4f, 5), shelf, environment.transform);
            Box("Shelf_B", new Vector3(2, 0.7f, 2.5f), new Vector3(1.2f, 1.4f, 3), shelf, environment.transform);
            Box("Counter", new Vector3(4, 0.5f, -5), new Vector3(3, 1, 1), shelf, environment.transform);
            if (work) Box("DisconnectedIsland", new Vector3(14, -0.15f, 0), new Vector3(4, 0.3f, 4), danger, environment.transform);

            var surface = environment.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Navigation bake failed.");
            string navPath = work ? Root + "/Assets/Navigation/WorkNavMesh.asset" : "Assets/_Project/Shared/Assets/Navigation/IntegrationNavMesh.asset";
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);

            var light = new GameObject("KeyLight").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.67f, 0.72f);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), scene);
            player.transform.position = new Vector3(0, 0.05f, -7);
            var employee = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EmployeePrefab), scene);
            employee.transform.position = new Vector3(-4, 0, -3);
            var markers = new GameObject("WorkDestinations");
            var points = work ? new[] { new Vector3(-4, 0, 5), new Vector3(5, 0, 4), new Vector3(14, 0, 0) }
                : new[] { new Vector3(-4, 0, 5), new Vector3(5, 0, 4) };
            var panel = new GameObject("MovementDemo").AddComponent<MovementDemoPanel>();
            Assign(panel, "employee", employee.GetComponent<EmployeeMovementController>());
            var serialized = new SerializedObject(panel);
            var list = serialized.FindProperty("destinations"); list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                var marker = new GameObject("Target_" + (i + 1));
                marker.transform.SetParent(markers.transform); marker.transform.position = points[i];
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "MarkerVisual"; disc.transform.SetParent(marker.transform, false);
                disc.transform.localPosition = new Vector3(0, 0.025f, 0); disc.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
                UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
                disc.GetComponent<Renderer>().sharedMaterial = i == 2 ? danger : target;
                list.GetArrayElementAtIndex(i).objectReferenceValue = marker.transform;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.SetParent(parent); cube.transform.position = position; cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, Root + "/Assets/Materials/" + name + ".mat");
            return material;
        }
        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
