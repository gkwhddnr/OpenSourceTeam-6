#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OpenSourceTeam6.Camera.Core;
using OpenSourceTeam6.Camera.Unity;
using OpenSourceTeam6.Movement.Core;
using OpenSourceTeam6.Movement.Unity;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSourceTeam6.Movement.Tests
{
    public sealed class MovementIntegrationTests
    {
        private const string Prefabs = "Assets/_Project/HaJongUk/Prefabs/";
        private GameObject root;
        private EmployeeMovementController employee;
        private readonly List<MoveOutcome> results = new List<MoveOutcome>();
        private NavMeshSurface surface;

        [UnityTest]
        public IEnumerator InvalidInputMapCanBeFixedAndInitializedAgain()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var reader = new PlayerInputReader(asset);
            try
            {
                Assert.Catch<Exception>(() => reader.Initialize());
                var map = new InputActionMap("Gameplay");
                map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
                map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
                asset.AddActionMap(map);
                Assert.DoesNotThrow(() => reader.Initialize());
                Assert.Throws<InvalidOperationException>(() => reader.Initialize());
                reader.Enable();
                Assert.That(reader.ReadMove(), Is.EqualTo(Vector2.zero));
                reader.Disable();
                reader.Dispose();
                Assert.DoesNotThrow(() => reader.Dispose());
            }
            finally
            {
                reader.Dispose();
                Object.Destroy(asset);
            }
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            results.Clear();
            root = new GameObject("TestWorld");
            var geometry = new GameObject("Geometry"); geometry.transform.SetParent(root.transform);
            Box(geometry.transform, new Vector3(0, -0.1f, 0), new Vector3(14, 0.2f, 14));
            Box(geometry.transform, new Vector3(15, -0.1f, 0), new Vector3(4, 0.2f, 4));
            surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "EmployeeMovement.prefab");
            Assert.That(prefab, Is.Not.Null, "Create the initial movement scenes/prefabs first.");
            var staff = Object.Instantiate(prefab, new Vector3(-3, 0, 0), Quaternion.identity, root.transform);
            employee = staff.GetComponent<EmployeeMovementController>();
            employee.Finished += results.Add;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (employee != null) employee.Finished -= results.Add;
            var data = surface == null ? null : surface.navMeshData;
            Object.Destroy(root);
            if (data != null) Object.Destroy(data);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DemoSupportsAddedDestinationsAndTracksMovedTargets()
        {
            var demoObject = new GameObject("DynamicDestinationDemo");
            demoObject.transform.SetParent(root.transform);
            demoObject.SetActive(false);
            var demo = demoObject.AddComponent<MovementDemoPanel>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(MovementDemoPanel);
            type.GetField("employee", flags).SetValue(demo, employee);
            var targets = new Transform[11];
            var target = new GameObject("AddedTarget_11");
            target.transform.SetParent(root.transform);
            targets[10] = target.transform;
            type.GetField("destinations", flags).SetValue(demo, targets);
            demoObject.SetActive(true);
            // A newly appended entry must use its current position, not a cached setup position.
            target.transform.position = new Vector3(3, 0, 0);
            type.GetMethod("Request", flags).Invoke(demo, new object[] { 10 });
            long first = employee.Snapshot.ActiveRequestId;
            Assert.That(first, Is.GreaterThan(0));
            target.transform.position = new Vector3(0, 0, 3);
            yield return WaitFor(() => employee.Snapshot.ActiveRequestId != first, 2);
            yield return WaitFor(() => employee.Snapshot.State == MoveState.Arrived, 8);
            Assert.That(Vector3.Distance(employee.transform.position, target.transform.position), Is.LessThan(0.4f));
            // Keep the selected target live even after arrival.
            target.transform.position = new Vector3(3, 0, 3);
            yield return WaitFor(() => employee.Snapshot.State == MoveState.Moving || employee.Snapshot.State == MoveState.Planning, 2);
            yield return WaitFor(() => employee.Snapshot.State == MoveState.Arrived, 8);
            Assert.That(Vector3.Distance(employee.transform.position, target.transform.position), Is.LessThan(0.4f));
            var marker = new GameObject("MarkerVisual");
            marker.transform.SetParent(target.transform, false);
            marker.transform.localPosition = new Vector3(-2, 0.03f, 0);
            yield return WaitFor(() => employee.Snapshot.ActiveRequestId > 0, 2);
            yield return WaitFor(() => employee.Snapshot.State == MoveState.Arrived, 8);
            Assert.That(Vector3.Distance(employee.transform.position, marker.transform.position), Is.LessThan(0.4f),
                "A moved child marker must be the visible destination, not its old parent position.");
            type.GetMethod("Cancel", flags).Invoke(demo, null);
            target.transform.position = Vector3.zero;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(employee.Snapshot.ActiveRequestId, Is.EqualTo(0), "Cancel must stop target following.");
            Assert.DoesNotThrow(() => type.GetMethod("Request", flags).Invoke(demo, new object[] { -1 }));
            Assert.DoesNotThrow(() => type.GetMethod("Request", flags).Invoke(demo, new object[] { 0 }));
        }

        [UnityTest]
        public IEnumerator EmployeeArrivesAndPublishesOneDeferredResult()
        {
            var request = employee.RequestMove(new WorldPoint(3, 0, 0));
            Assert.That(request.Accepted, Is.True);
            Assert.That(results, Is.Empty);
            yield return WaitFor(() => results.Count == 1, 8);
            Assert.That(results[0].Status, Is.EqualTo(MoveState.Arrived),
                $"Failure: {results[0].Failure}, position: {employee.transform.position}");
            Assert.That(results[0].RequestId, Is.EqualTo(request.RequestId));
            Assert.That(Vector3.Distance(employee.transform.position, new Vector3(3, 0, 0)), Is.LessThan(0.4f));
            yield return null;
            Assert.That(results.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ArrivalSurvivesALargeSimulationStep()
        {
            float originalStep = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 0.2f;
                employee.RequestMove(new WorldPoint(3, 0, 3));
                yield return WaitFor(() => results.Count > 0, 8);
                Assert.That(results[0].Status, Is.EqualTo(MoveState.Arrived), $"Failure: {results[0].Failure}");
            }
            finally { Time.captureDeltaTime = originalStep; }
        }

        [UnityTest]
        public IEnumerator InvalidDestinationDoesNotCancelAndReplacementRejectsStaleCancel()
        {
            var first = employee.RequestMove(new WorldPoint(3, 0, 0));
            var invalid = employee.RequestMove(new WorldPoint(100, 100, 100));
            Assert.That(invalid.Accepted, Is.False);
            Assert.That(employee.Snapshot.ActiveRequestId, Is.EqualTo(first.RequestId));
            var second = employee.RequestMove(new WorldPoint(0, 0, 3));
            Assert.That(employee.Cancel(first.RequestId), Is.False);
            Assert.That(employee.Cancel(second.RequestId), Is.True);
            Assert.That(results, Is.Empty);
            yield return WaitFor(() => results.Count == 2, 2);
            Assert.That(results[0].Cancellation, Is.EqualTo(CancelReason.Replaced));
            Assert.That(results[1].Cancellation, Is.EqualTo(CancelReason.Requested));
        }

        [UnityTest]
        public IEnumerator DisconnectedIslandFailsInsteadOfClaimingArrival()
        {
            Assert.That(employee.RequestMove(new WorldPoint(15, 0, 0)).Accepted, Is.True);
            yield return WaitFor(() => results.Count > 0, 5);
            Assert.That(results[0].Status, Is.EqualTo(MoveState.Failed));
            Assert.That(results[0].Failure, Is.EqualTo(MoveFailure.PartialPath).Or.EqualTo(MoveFailure.NoPath));
        }

        [UnityTest]
        public IEnumerator SamePointPauseResumeAndDisableAreTerminallyConsistent()
        {
            Vector3 current = employee.transform.position;
            employee.RequestMove(new WorldPoint(current.x, current.y, current.z));
            yield return WaitFor(() => results.Count == 1, 3);
            Assert.That(results[0].Status, Is.EqualTo(MoveState.Arrived));
            var next = employee.RequestMove(new WorldPoint(4, 0, 4));
            Time.timeScale = 0;
            yield return null; yield return null;
            Vector3 paused = employee.transform.position;
            yield return null;
            Assert.That(employee.Snapshot.State, Is.EqualTo(MoveState.Suspended));
            Assert.That(Vector3.Distance(employee.transform.position, paused), Is.LessThan(0.001f));
            Assert.That(employee.Snapshot.ActiveRequestId, Is.EqualTo(next.RequestId));
            Time.timeScale = 1;
            yield return null;
            employee.gameObject.SetActive(false);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[1].Cancellation, Is.EqualTo(CancelReason.Disabled));
            Assert.That(employee.RequestMove(default).Accepted, Is.False);
        }

        [UnityTest]
        public IEnumerator CallbackReplacementIsDeferredUntilNextDispatch()
        {
            long secondId = 0;
            Action<MoveOutcome> handler = result =>
            {
                if (secondId != 0) return;
                secondId = employee.RequestMove(new WorldPoint(1, 0, 1)).RequestId;
                employee.Cancel(secondId);
            };
            employee.Finished += handler;
            var first = employee.RequestMove(new WorldPoint(3, 0, 0));
            employee.Cancel(first.RequestId);
            yield return new WaitForEndOfFrame();
            Assert.That(results.Count, Is.EqualTo(1));
            yield return new WaitForEndOfFrame();
            Assert.That(results.Count, Is.EqualTo(2));
            employee.Finished -= handler;
        }

        [UnityTest]
        public IEnumerator PlayerPrefabHasInvisibleBodyEyeCameraAndWallCollision()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "PlayerRig.prefab");
            var player = Object.Instantiate(prefab, new Vector3(0, 0.02f, -3), Quaternion.identity, root.transform);
            player.GetComponent<PlayerRigController>().enabled = false;
            Assert.That(player.GetComponentsInChildren<Renderer>().Length, Is.Zero);
            Assert.That(player.GetComponentsInChildren<UnityEngine.Camera>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponentsInChildren<AudioListener>().Length, Is.EqualTo(1));
            Assert.That(player.transform.Find("EyePivot").localPosition.y, Is.EqualTo(1.6f).Within(0.001f));
            Box(root.transform, new Vector3(0, 1.5f, 0), new Vector3(3, 3, 0.3f));
            Physics.SyncTransforms();
            var motor = new CharacterControllerMotor(player.GetComponent<CharacterController>());
            for (int i = 0; i < 90; i++) motor.Move(new Vector3(0, -2, 3), 1f / 60f);
            Assert.That(player.transform.position.z, Is.LessThan(-0.35f));
            var presenter = new FirstPersonCameraPresenter(player.transform.Find("EyePivot"), player.GetComponentInChildren<UnityEngine.Camera>(), null);
            presenter.Present(new LookState(0, 70));
            presenter.Apply();
            yield return new WaitForEndOfFrame();
            Assert.That(Mathf.DeltaAngle(player.transform.Find("EyePivot").localEulerAngles.x, 70), Is.EqualTo(0).Within(0.01f));
            Vector3 before = player.transform.position;
            motor.Move(new Vector3(0, 0, 100), 0);
            Assert.That(player.transform.position, Is.EqualTo(before));
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Movement did not complete within the test deadline.");
        }
        private static void Box(Transform parent, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent); box.transform.position = position; box.transform.localScale = scale;
        }
    }
}
#endif
