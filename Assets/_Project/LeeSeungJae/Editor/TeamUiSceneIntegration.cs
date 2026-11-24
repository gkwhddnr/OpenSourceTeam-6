#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using OpenSourceTeam6.Movement.Unity;
using OpenSourceTeam6.Movement.Core;
using OpenSourceTeam6.UI.Core;
using OpenSourceTeam6.UI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EmployeeVoice.Editor
{
    public static class TeamUiSceneIntegration
    {
        private const string TargetPath = "Assets/_Project/LeeSeungJae/Scenes/IntegratedConvenienceStore.unity";
        private const string TeamPath = "Assets/_Project/SongJaeMyeong/Scenes/SJM_UI_Work.unity";

        public static void Apply()
        {
            Scene target = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
            Scene team = EditorSceneManager.OpenScene(TeamPath, OpenSceneMode.Additive);
            Transform targetStage = Named(target, "Demo_StoreStage");
            Transform teamStage = Named(team, "Demo_StoreStage");
            StoreUiSessionController ui = FindComponent<StoreUiSessionController>(target);
            VoiceMovementIntegration voice = FindComponent<VoiceMovementIntegration>(target);
            EmployeeMovementController employee = FindComponent<EmployeeMovementController>(target);
            InGameHudPresenter hud = FindComponent<InGameHudPresenter>(target);
            if (ui == null || voice == null || !voice.enabled || employee == null || hud == null)
                throw new InvalidOperationException("통합 씬의 UI 또는 실제 직원 음성 연결을 찾지 못했습니다.");

            Transform monitorSource = teamStage.Find("Counter_PosMonitorRoot");
            if (monitorSource == null) throw new InvalidOperationException("팀 씬의 계산대 모니터가 없습니다.");
            if (targetStage.Find("Counter_PosMonitorRoot") != null)
                throw new InvalidOperationException("이미 계산대 모니터가 있습니다. 기존 배치를 덮어쓰지 않습니다.");
            Transform monitor = UnityEngine.Object.Instantiate(monitorSource);
            monitor.name = monitorSource.name;
            if (monitor.gameObject.scene != target) SceneManager.MoveGameObjectToScene(monitor.gameObject, target);
            monitor.SetParent(targetStage, true);

            Transform targetCheckout = Named(target, "Checkout_2");
            Transform teamCheckout = Named(team, "Checkout_2");
            Vector3 mainOffset = RequiredChild(targetCheckout, "Counter_Top").position - RequiredChild(teamCheckout, "Counter_Top").position;
            Vector3 subOffset = RequiredChild(targetCheckout, "Counter_Top (1)").position - RequiredChild(teamCheckout, "Counter_Top (1)").position;
            foreach (string name in new[] { "Counter_Monitor", "Counter_CashRegister" })
                RequiredChild(monitor, name).position += mainOffset;
            foreach (string name in new[] { "Secondary_Counter_Monitor", "Secondary_Counter_CashRegister" })
                RequiredChild(monitor, name).position += subOffset;
            Transform targetManual = RequiredChild(targetStage, "Counter_ManualCube");
            Transform teamManual = RequiredChild(teamStage, "Counter_ManualCube");
            targetManual.position = teamManual.position + mainOffset;

            Text day = NamedUnder(monitor, "MonitorDayLabelText").GetComponent<Text>();
            Text timer = NamedUnder(monitor, "MonitorTimerText").GetComponent<Text>();
            Text balance = NamedUnder(monitor, "MonitorBalanceText").GetComponent<Text>();
            if (day == null || timer == null || balance == null)
                throw new InvalidOperationException("계산대 모니터의 시간·매출 텍스트가 없습니다.");
            var hudSo = new SerializedObject(hud);
            SetObject(hudSo, "counterMonitorDayText", day);
            SetObject(hudSo, "counterMonitorTimerText", timer);
            SetObject(hudSo, "counterMonitorBalanceText", balance);
            hudSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);

            if (All(target).Any(t => t.name == "UI_PrototypeDemo"))
                throw new InvalidOperationException("기존 UI 보조 드라이버가 있어 중복 생성을 중단했습니다.");
            Transform demoSource = Named(team, "UI_PrototypeDemo");
            Transform demo = UnityEngine.Object.Instantiate(demoSource);
            demo.name = demoSource.name;
            if (demo.gameObject.scene != target) SceneManager.MoveGameObjectToScene(demo.gameObject, target);
            var driver = demo.GetComponent<UiPrototypeDemoDriver>();
            if (driver == null) throw new InvalidOperationException("팀 UI 보조 드라이버가 없습니다.");
            var driverSo = new SerializedObject(driver);
            SetObject(driverSo, "uiController", ui);
            SetObject(driverSo, "demoEmployeeTransform", employee.transform);
            SetObject(driverSo, "counterTarget", RequiredChild(targetStage, "Target_Counter"));
            SetObject(driverSo, "shelfTarget", RequiredChild(targetStage, "Target_Shelf"));
            SetObject(driverSo, "restTarget", RequiredChild(targetStage, "Target_Rest"));
            driverSo.ApplyModifiedPropertiesWithoutUndo();
            CheckNoCrossSceneReferences(target, monitor.gameObject);
            CheckNoCrossSceneReferences(target, demo.gameObject);
            EditorSceneManager.CloseScene(team, true);
            VerifyScene(target);
            EditorSceneManager.MarkSceneDirty(target);
            if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("통합 씬을 저장하지 못했습니다.");
            Debug.Log("[TeamUiIntegration] Updated team monitor and utility driver saved to the integrated scene.");
        }

        public static void Verify()
        {
            Scene target = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
            VerifyScene(target);
            VerifyUpdatedUiBehaviour(target);
            Debug.Log("[TeamUiIntegration] Reopened scene bindings verified.");
        }

        public static void VerifyPersonaAndRest()
        {
            Scene scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
            var voice = FindComponent<VoiceMovementIntegration>(scene);
            var ui = FindComponent<StoreUiSessionController>(scene);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (voice == null || ui == null) throw new InvalidOperationException("음성 시스템 또는 UI가 없습니다.");
            // The HTTP client and response validation are independently tested with a
            // fake relay. Construct a result here to exercise the Unity event boundary.
            var result = (PersonaEvaluationResult)Activator.CreateInstance(typeof(PersonaEvaluationResult), true);
            SetResult(result, "RequestId", "f62a3acc-6cb8-4465-bf11-bc79d50e3858");
            SetResult(result, "EmployeeName", "김민수");
            SetResult(result, "Traits", "성실하지만 예민하다");
            SetResult(result, "HourlyWageWon", 16000);
            SetResult(result, "Diligence", 85);
            SetResult(result, "Laziness", 35);
            SetResult(result, "Loyalty", 60);
            SetResult(result, "WorkStressMultiplier", 1.25f);
            SetResult(result, "WorkFatigueMultiplier", 1.15f);
            SetResult(result, "WageSensitivityMultiplier", 1.1f);
            SetResult(result, "SummaryReason", "성실하지만 업무 스트레스에 예민합니다.");
            var baseline = new EmployeePersonaSetupData("김민수", "성실하지만 예민하다", 10030,
                EmployeePersonalityProfile.CreatePreset3Timid());
            if (!voice.TryPrepareEvaluatedPersona(baseline, result, out var prepared, out string reason))
                throw new InvalidOperationException("심사 결과 준비 실패: " + reason);
            typeof(VoiceMovementIntegration).GetMethod("OnPersonaConfirmed", flags).Invoke(voice, new object[] { prepared });
            var profile = (EmployeePersonalityProfile)GetPrivate(voice, "currentProfile");
            var personality = (EmployeePersonality)GetPrivate(voice, "personality");
            float fatigueMultiplier = (float)GetPrivate(voice, "workFatigueMultiplier");
            if (prepared.HourlyWageWon != 16000 || profile.Diligence != 85 || profile.Laziness != 35 || profile.Loyalty != 60 ||
                profile.WorkStressMultiplier != 1.25f || profile.WageSensitivityMultiplier != 1.1f ||
                fatigueMultiplier != 1.15f || personality.loyalty != 60 || profile.Archetype != PersonaArchetype.FragileTimid)
                throw new InvalidOperationException("채용 프로필 또는 피로 배율이 정확하게 반영되지 않았습니다.");
            if (voice.TryPrepareEvaluatedPersona(baseline, result, out _, out _))
                throw new InvalidOperationException("같은 심사 결과가 중복 적용될 수 있습니다.");

            // Cancellation completion is intentionally deferred by the team movement
            // component. Checkout must stop being exposed before that callback arrives.
            var movement = new DeferredCancelMovement();
            SetPrivate(voice, "ui", ui);
            SetPrivate(voice, "movement", movement);
            SetPrivate(voice, "activeMove", 42L);
            SetPrivate(voice, "activeWorkRole", StaffWorkRole.Checkout);
            SetPrivate(voice, "badge", StaffMoveBadge.Moving);
            typeof(VoiceMovementIntegration).GetMethod("CancelWork", flags).Invoke(voice, null);
            if (!movement.Cancelled || (StaffWorkRole)GetPrivate(voice, "activeWorkRole") != StaffWorkRole.Idle ||
                (long)GetPrivate(voice, "activeMove") != 42L)
                throw new InvalidOperationException("이동 취소 대기 중 계산 업무가 해제되지 않았습니다.");
            var outcome = new MoveOutcome(42L, MoveState.Cancelled, MoveFailure.None, CancelReason.Requested, default);
            typeof(VoiceMovementIntegration).GetMethod("OnMovementFinished", flags).Invoke(voice, new object[] { outcome });
            if ((long)GetPrivate(voice, "activeMove") != 0 || (StaffWorkRole)GetPrivate(voice, "activeWorkRole") != StaffWorkRole.Idle)
                throw new InvalidOperationException("이동 취소 완료 후 Idle 전환에 실패했습니다.");
            Debug.Log("[PersonaIntegration] Evaluated profile, one-time application and deferred checkout cancellation passed.");
        }

        private sealed class DeferredCancelMovement : IAgentMovement
        {
            public bool Cancelled;
            public event Action<MoveOutcome> Finished { add { } remove { } }
            public MovementSnapshot Snapshot => default;
            public MoveReceipt RequestMove(WorldPoint destination) => default;
            public bool Cancel(long requestId) { Cancelled = requestId == 42L; return Cancelled; }
        }

        private static void SetResult(PersonaEvaluationResult result, string property, object value) =>
            typeof(PersonaEvaluationResult).GetProperty(property).SetValue(result, value);
        private static object GetPrivate(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void SetPrivate(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

        public static void UpdateManualGuide()
        {
            Scene scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
            Text body = Named(scene, "ManualBodyText").GetComponent<Text>();
            if (body == null) throw new InvalidOperationException("매장 설명서 본문이 없습니다.");
            string before = body.text;
            string after = before.Replace("계산·진열·휴식 지시 가능", "계산·진열·청소 등 지시 가능")
                .Replace("부드러운 말투로 칭찬하거나 '잠깐 쉬어' 지시로 스트레스를 낮추세요.", "직원을 칭찬하거나 대기 시간을 주면 스트레스를 낮출 수 있습니다.")
                .Replace("F1~F5 : 음성·오류·스트레스 시뮬 / F6 : 즉시 마감 / F7 : +4시간 / F8 : 비 토글", "F6 : 즉시 마감 / F7 : +4시간 / F8 : 비 날씨 토글");
            if (after == before) throw new InvalidOperationException("설명서의 기존 안내를 찾지 못해 변경하지 않았습니다.");
            body.text = after;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("설명서 변경을 저장하지 못했습니다.");
            Debug.Log("[TeamUiIntegration] Updated the in-scene guide for the actual voice integration.");
        }

        private static void VerifyUpdatedUiBehaviour(Scene scene)
        {
            InGameHudPresenter hud = FindComponent<InGameHudPresenter>(scene);
            var so = new SerializedObject(hud);
            hud.UpdateTopBar(2, 60f, 12345, 20000, 120f, 10.25f);
            var day = (Text)so.FindProperty("counterMonitorDayText").objectReferenceValue;
            var clock = (Text)so.FindProperty("counterMonitorTimerText").objectReferenceValue;
            var balance = (Text)so.FindProperty("counterMonitorBalanceText").objectReferenceValue;
            if (!day.text.Contains("2") || !clock.text.Contains("10:10") || !balance.text.Contains("12,345"))
                throw new InvalidOperationException("계산대 모니터의 일차·시각·매출 표시가 올바르지 않습니다.");
            hud.RenderEmployee(new EmployeeHudSnapshot("검증", "성실", StaffWorkRole.Restock, "ACCEPT_NORMAL", StaffMoveBadge.Moving, 75f, 30f, 60f));
            var stress = (Text)so.FindProperty("stressValueText").objectReferenceValue;
            if (stress == null || !stress.text.Contains("75")) throw new InvalidOperationException("직원 스트레스 HUD 갱신 실패.");
            Transform card = hud.transform.Find("EmployeeStatusCard");
            if (card == null) throw new InvalidOperationException("직원 상태 카드가 없습니다.");
            hud.SetEmployeeCardCollapsed(true);
            if (card.gameObject.activeSelf || !hud.IsEmployeeCardCollapsed) throw new InvalidOperationException("직원 카드 접기 실패.");
            hud.SetEmployeeCardCollapsed(false);
            if (!card.gameObject.activeSelf || hud.IsEmployeeCardCollapsed) throw new InvalidOperationException("직원 카드 펼치기 실패.");

            // Exercise the team's real-voice detection and microphone change relay
            // without a microphone, a server call, or entering Play mode.
            UiPrototypeDemoDriver driver = FindComponent<UiPrototypeDemoDriver>(scene);
            VoiceMovementIntegration voice = FindComponent<VoiceMovementIntegration>(scene);
            SettingsPanelPresenter settings = FindComponent<StoreUiSessionController>(scene).GetComponentInChildren<SettingsPanelPresenter>(true);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var micField = typeof(VoiceMovementIntegration).GetField("microphoneDeviceName", flags);
            var externalField = typeof(UiPrototypeDemoDriver).GetField("externalVoiceActive", flags);
            var changeEvent = typeof(SettingsPanelPresenter).GetField("MicrophoneDeviceChanged", flags);
            var onEnable = typeof(UiPrototypeDemoDriver).GetMethod("OnEnable", flags);
            var onDisable = typeof(UiPrototypeDemoDriver).GetMethod("OnDisable", flags);
            if (settings == null || micField == null || externalField == null || changeEvent == null || onEnable == null || onDisable == null)
                throw new InvalidOperationException("팀 마이크 설정 연결을 검사할 수 없습니다.");
            object originalMic = micField.GetValue(voice);
            try
            {
                onEnable.Invoke(driver, null);
                if (!(bool)externalField.GetValue(driver)) throw new InvalidOperationException("실제 음성 시스템 자동 감지 실패.");
                var handler = changeEvent.GetValue(settings) as Action<string>;
                if (handler == null) throw new InvalidOperationException("마이크 설정 이벤트가 연결되지 않았습니다.");
                handler("integration-test-microphone");
                if ((string)micField.GetValue(voice) != "integration-test-microphone")
                    throw new InvalidOperationException("마이크 선택이 음성 시스템에 전달되지 않았습니다.");
            }
            finally
            {
                onDisable.Invoke(driver, null);
                micField.SetValue(voice, originalMic);
            }
            Debug.Log("[TeamUiIntegration] Monitor, stress HUD, fold toggling, voice detection and microphone relay passed.");
        }

        private static void VerifyScene(Scene scene)
        {
            StoreUiSessionController ui = FindComponent<StoreUiSessionController>(scene);
            VoiceMovementIntegration voice = FindComponent<VoiceMovementIntegration>(scene);
            InGameHudPresenter hud = FindComponent<InGameHudPresenter>(scene);
            Transform monitor = Named(scene, "Counter_PosMonitorRoot");
            UiPrototypeDemoDriver driver = FindComponent<UiPrototypeDemoDriver>(scene);
            if (ui == null || voice == null || !voice.enabled || hud == null || driver == null)
                throw new InvalidOperationException("실제 음성, UI 또는 팀 UI 드라이버가 없습니다.");
            if (All(scene).Count(t => t.GetComponent<VoiceMovementIntegration>() != null) != 1 ||
                All(scene).Count(t => t.GetComponent<UiPrototypeDemoDriver>() != null) != 1)
                throw new InvalidOperationException("음성 또는 UI 드라이버가 중복되었습니다.");
            var driverSo = new SerializedObject(driver);
            if (driverSo.FindProperty("uiController").objectReferenceValue != ui ||
                driverSo.FindProperty("demoEmployeeTransform").objectReferenceValue == null)
                throw new InvalidOperationException("팀 UI 드라이버의 통합 씬 참조가 올바르지 않습니다.");
            var hudSo = new SerializedObject(hud);
            foreach (string field in new[] { "counterMonitorDayText", "counterMonitorTimerText", "counterMonitorBalanceText" })
            {
                var text = hudSo.FindProperty(field)?.objectReferenceValue as Text;
                if (text == null || !text.transform.IsChildOf(monitor))
                    throw new InvalidOperationException("모니터 텍스트 연결 실패: " + field);
            }
            foreach (string name in new[] { "Counter_Monitor", "Counter_CashRegister", "Secondary_Counter_Monitor", "Secondary_Counter_CashRegister", "CounterMonitorScreenCanvas" }) NamedUnder(monitor, name);
            Transform uiRoot = ui.transform;
            if (PrefabUtility.GetCorrespondingObjectFromSource(ui.gameObject) == null ||
                uiRoot.Find("InGameHud/EmployeeStatusCard/PostItContent") == null)
                throw new InvalidOperationException("최신 팀 UI 프리팹 또는 직원 상태 UI 연결을 확인해 주세요.");
            var voiceSo = new SerializedObject(voice);
            foreach (string name in new[] { "existingUi", "employee", "restockSnackDestination", "restockRamenDestination", "restockDailyGoodsDestination", "restockIceCreamDestination", "restockWalkInDestination", "restockHotFoodDestination", "cleanDestination", "cashierDestination" })
                if (voiceSo.FindProperty(name)?.objectReferenceValue == null)
                    throw new InvalidOperationException("기존 음성 직원 참조가 없습니다: " + name);
            if (voiceSo.FindProperty("existingUi").objectReferenceValue != ui)
                throw new InvalidOperationException("음성 시스템이 다른 UI를 참조합니다.");
            CheckNoCrossSceneReferences(scene, monitor.gameObject);
            CheckNoCrossSceneReferences(scene, driver.gameObject);
            Debug.Log("[TeamUiIntegration] Real voice, team UI, monitor and utility references are valid.");
        }

        private static void CheckNoCrossSceneReferences(Scene target, GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("누락된 스크립트: " + root.name);
                var so = new SerializedObject(component);
                var iterator = so.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object value = iterator.objectReferenceValue;
                    GameObject referenced = value is Component c ? c.gameObject : value as GameObject;
                    if (referenced != null && referenced.scene.IsValid() && referenced.scene != target)
                        throw new InvalidOperationException("다른 씬 참조: " + component.name + "/" + iterator.propertyPath);
                }
            }
        }

        private static void SetObject(SerializedObject so, string name, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property == null || value == null) throw new InvalidOperationException("필수 참조 없음: " + name);
            property.objectReferenceValue = value;
        }

        private static T FindComponent<T>(Scene scene) where T : Component => All(scene).Select(t => t.GetComponent<T>()).FirstOrDefault(c => c != null);
        private static Transform Named(Scene scene, string name) => All(scene).FirstOrDefault(t => t.name == name) ?? throw new InvalidOperationException("씬 오브젝트 없음: " + name);
        private static Transform NamedUnder(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) ?? throw new InvalidOperationException("하위 오브젝트 없음: " + name);
        private static Transform RequiredChild(Transform parent, string name) => parent.Find(name) ?? throw new InvalidOperationException("하위 오브젝트 없음: " + parent.name + "/" + name);

        public static void Inspect()
        {
            Scene target = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
            Describe(target);
            Scene team = EditorSceneManager.OpenScene(TeamPath, OpenSceneMode.Additive);
            Describe(team);
        }

        private static void Describe(Scene scene)
        {
            Debug.Log("[TeamUiInspect] SCENE " + scene.path);
            foreach (GameObject root in scene.GetRootGameObjects())
                Debug.Log("[TeamUiInspect] ROOT " + root.name + " active=" + root.activeSelf + " prefab=" + PrefabUtility.GetPrefabAssetType(root));
            foreach (string name in new[] { "PF_StoreUIRoot", "InGameHud", "EmployeeStatusCard", "PostItContent", "TopStoreBar", "VoiceFeedbackDock", "Counter_PosMonitorRoot", "Counter_Monitor", "CounterMonitorScreenCanvas", "Counter_ManualCube", "UI_PrototypeDemo", "OpenAI Voice Integration", "Voice Employee" })
            {
                foreach (Transform t in All(scene).Where(x => x.name == name))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    Debug.Log("[TeamUiInspect] NODE " + name + " parent=" + (t.parent != null ? t.parent.name : "<root>") + " active=" + t.gameObject.activeSelf + " pos=" + t.position.ToString("F3") + " source=" + (source != null ? AssetDatabase.GetAssetPath(source) : "none"));
                }
            }
            foreach (StoreUiSessionController ui in All(scene).Select(t => t.GetComponent<StoreUiSessionController>()).Where(x => x != null))
            {
                var s = new SerializedObject(ui);
                foreach (string field in new[] { "inGameHud", "commandOverlay", "personaSetupPanel", "lobbyPanel", "settingsPanel", "pausePanel", "employeeWorldStatus" })
                {
                    SerializedProperty p = s.FindProperty(field);
                    Debug.Log("[TeamUiInspect] UI " + field + " = " + (p == null ? "NO_FIELD" : p.objectReferenceValue != null ? p.objectReferenceValue.name : "NULL"));
                }
            }
            foreach (InGameHudPresenter hud in All(scene).Select(t => t.GetComponent<InGameHudPresenter>()).Where(x => x != null))
            {
                var s = new SerializedObject(hud);
                foreach (string field in new[] { "hudRoot", "employeeStatusCardRoot", "stressFillImage", "stressValueText", "fatigueFillImage", "loyaltyFillImage", "counterMonitorTimerText", "counterMonitorBalanceText" })
                {
                    SerializedProperty p = s.FindProperty(field);
                    Debug.Log("[TeamUiInspect] HUD " + field + " = " + (p == null ? "NO_FIELD" : p.objectReferenceValue != null ? p.objectReferenceValue.name : "NULL"));
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<Transform> All(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true));
    }
}
#endif
