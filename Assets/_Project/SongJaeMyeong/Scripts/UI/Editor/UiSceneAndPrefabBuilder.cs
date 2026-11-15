using System.IO;
using OpenSourceTeam6.UI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Editor
{
    public static class UiSceneAndPrefabBuilder
    {
        private const string MenuPath = "OpenSourceTeam6/UI/Create Initial UI Scene And Prefab";
        private const string UiAssetDir = "Assets/_Project/SongJaeMyeong/Assets/UI";
        private const string WhiteSpritePath = UiAssetDir + "/Spr_WhiteFill.png";
        private const string PrefabPath = "Assets/_Project/SongJaeMyeong/Prefabs/PF_StoreUIRoot.prefab";
        private const string WorldUiPrefabPath = "Assets/_Project/SongJaeMyeong/Prefabs/PF_EmployeeWorldStatusUI.prefab";
        private const string WorkScenePath = "Assets/_Project/SongJaeMyeong/Scenes/SJM_UI_Work.unity";

        [MenuItem(MenuPath, priority = 200)]
        public static void BuildUiPrefabAndWorkScene()
        {
            EnsureFolders();
            Sprite fillSprite = EnsureWhiteSprite();
            Font koreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject tempRoot = BuildStoreUiHierarchy(koreanFont, fillSprite);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempRoot, PrefabPath);
            Object.DestroyImmediate(tempRoot);

            GameObject tempWorldUi = BuildWorldStatusHierarchy(koreanFont, fillSprite);
            GameObject savedWorldUiPrefab = PrefabUtility.SaveAsPrefabAsset(tempWorldUi, WorldUiPrefabPath);
            Object.DestroyImmediate(tempWorldUi);

            BuildWorkScene(savedPrefab, savedWorldUiPrefab, koreanFont, fillSprite);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OpenSourceTeam6.UI] 데모 규격 반영 UI 프리팹 및 검증 씬 생성 완료: {PrefabPath}, {WorldUiPrefabPath}, {WorkScenePath}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/SongJaeMyeong/Assets"))
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong", "Assets");
            if (!AssetDatabase.IsValidFolder(UiAssetDir))
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong/Assets", "UI");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/SongJaeMyeong/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/SongJaeMyeong/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong", "Scenes");
        }

        private static Sprite EnsureWhiteSprite()
        {
            if (!File.Exists(WhiteSpritePath))
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                File.WriteAllBytes(WhiteSpritePath, png);
                AssetDatabase.ImportAsset(WhiteSpritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        }

        private static GameObject BuildStoreUiHierarchy(Font font, Sprite whiteSprite)
        {
            GameObject root = new GameObject("PF_StoreUIRoot");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            StandaloneCursorLockGate fallbackGate = root.AddComponent<StandaloneCursorLockGate>();
            StoreAudioManager audioManager = root.AddComponent<StoreAudioManager>();
            StoreUiSessionController sessionController = root.AddComponent<StoreUiSessionController>();

            LobbyPanelPresenter lobbyPresenter = BuildLobbyPanel(root.transform, font, whiteSprite);
            InGameHudPresenter hudPresenter = BuildInGameHud(root.transform, font, whiteSprite);
            CommandOverlayPresenter overlayPresenter = BuildCommandOverlay(root.transform, font, whiteSprite);
            SettlementPanelPresenter settlementPresenter = BuildSettlementPanel(root.transform, font, whiteSprite);
            PausePanelPresenter pausePresenter = BuildPausePanel(root.transform, font, whiteSprite);
            SettingsPanelPresenter settingsPresenter = BuildSettingsPanel(root.transform, font, whiteSprite);

            hudPresenter.SetVisible(false);
            overlayPresenter.SetOpen(false);
            settlementPresenter.SetVisible(false);
            pausePresenter.SetOpen(false);
            settingsPresenter.SetOpen(false);
            lobbyPresenter.SetVisible(true);

            SerializedObject so = new SerializedObject(sessionController);
            so.FindProperty("lobbyPanel").objectReferenceValue = lobbyPresenter;
            so.FindProperty("inGameHud").objectReferenceValue = hudPresenter;
            so.FindProperty("commandOverlay").objectReferenceValue = overlayPresenter;
            so.FindProperty("settlementPanel").objectReferenceValue = settlementPresenter;
            so.FindProperty("pausePanel").objectReferenceValue = pausePresenter;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPresenter;
            so.FindProperty("audioManager").objectReferenceValue = audioManager;
            so.FindProperty("standaloneFallbackGate").objectReferenceValue = fallbackGate;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static LobbyPanelPresenter BuildLobbyPanel(Transform parent, Font font, Sprite sprite)
        {
            GameObject panelObj = CreatePanel("LobbyPanel", parent, new Color(0.06f, 0.09f, 0.14f, 0.97f), sprite);
            StretchFull(panelObj.GetComponent<RectTransform>());
            LobbyPanelPresenter presenter = panelObj.AddComponent<LobbyPanelPresenter>();

            GameObject topStripeMint = CreatePanel("BrandStripe_Mint", panelObj.transform, new Color(0.05f, 0.66f, 0.58f, 1f), sprite);
            SetRect(topStripeMint.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 14f));

            GameObject topStripeAmber = CreatePanel("BrandStripe_Amber", panelObj.transform, new Color(0.96f, 0.58f, 0.18f, 1f), sprite);
            SetRect(topStripeAmber.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(0f, 8f));

            GameObject card = CreatePanel("LobbyCard", panelObj.transform, new Color(0.11f, 0.15f, 0.22f, 0.98f), sprite);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 520f));

            Text tagText = CreateText("TagText", card.transform, font, "24H AI CONVENIENCE STORE  ·  1ST INTEGRATED DEMO", 16, FontStyle.Bold, new Color(0.22f, 0.85f, 0.74f, 1f), TextAnchor.MiddleCenter);
            SetRect(tagText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(600f, 28f));

            Text titleText = CreateText("TitleText", card.transform, font, "편의점 AI 매니저", 46, FontStyle.Bold, new Color(0.96f, 0.98f, 1f, 1f), TextAnchor.MiddleCenter);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(600f, 62f));

            Text descText = CreateText("SubtitleText", card.transform, font, "음성 지시와 감정 관리에 따라 알바의 반응과 정산 결말이 달라지는 편의점 운영 시뮬레이션", 18, FontStyle.Normal, new Color(0.73f, 0.79f, 0.87f, 1f), TextAnchor.MiddleCenter);
            SetRect(descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -162f), new Vector2(580f, 52f));

            Button startBtn = CreateButton("StartGameButton", card.transform, font, sprite, "게임 시작  (1일차 영업)", 22, new Color(0.06f, 0.65f, 0.57f, 1f), Color.white);
            SetRect(startBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 184f), new Vector2(380f, 60f));

            Button settingsBtn = CreateButton("SettingsButton", card.transform, font, sprite, "환경 설정  (소리 · 마이크)", 19, new Color(0.18f, 0.28f, 0.42f, 1f), new Color(0.92f, 0.96f, 1f, 1f));
            SetRect(settingsBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(380f, 52f));

            Button quitBtn = CreateButton("QuitGameButton", card.transform, font, sprite, "게임 종료", 19, new Color(0.20f, 0.25f, 0.33f, 1f), new Color(0.86f, 0.90f, 0.95f, 1f));
            SetRect(quitBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(380f, 52f));

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("panelRoot").objectReferenceValue = panelObj;
            so.FindProperty("startGameButton").objectReferenceValue = startBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("quitGameButton").objectReferenceValue = quitBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static InGameHudPresenter BuildInGameHud(Transform parent, Font font, Sprite sprite)
        {
            GameObject hudObj = new GameObject("InGameHud", typeof(RectTransform));
            hudObj.transform.SetParent(parent, false);
            StretchFull(hudObj.GetComponent<RectTransform>());
            InGameHudPresenter presenter = hudObj.AddComponent<InGameHudPresenter>();

            // 1. Top Store Bar (기능정의서 UI-08: 좌측 일차 / 중앙 시간 / 우측 현재 및 목표 금액)
            GameObject topBar = CreatePanel("TopStoreBar", hudObj.transform, new Color(0.08f, 0.11f, 0.17f, 0.90f), sprite);
            SetRect(topBar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-48f, 64f));

            GameObject dayBadge = CreatePanel("DayBadge", topBar.transform, new Color(0.06f, 0.58f, 0.51f, 1f), sprite);
            SetRect(dayBadge.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(176f, 40f));
            Text dayText = CreateText("DayLabelText", dayBadge.transform, font, "1일차 영업 중", 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(dayText.rectTransform);

            Button cmdBtn = CreateButton("OpenCommandOverlayBtn", topBar.transform, font, sprite, "[Tab] 텍스트 지시", 16, new Color(0.18f, 0.25f, 0.35f, 1f), new Color(0.88f, 0.94f, 1f, 1f));
            SetRect(cmdBtn.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(208f, 0f), new Vector2(184f, 40f));

            Text timerText = CreateText("TimerText", topBar.transform, font, "남은 시간  02:00", 25, FontStyle.Bold, new Color(0.95f, 0.97f, 1f, 1f), TextAnchor.MiddleCenter);
            SetRect(timerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 44f));

            Text balanceText = CreateText("BalanceText", topBar.transform, font, "현재 50,000원  /  목표 80,000원", 21, FontStyle.Bold, new Color(0.98f, 0.82f, 0.32f, 1f), TextAnchor.MiddleRight);
            SetRect(balanceText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(460f, 44f));

            // 2. Center subtle crosshair dot for 1st-person store view
            GameObject crosshair = CreatePanel("CrosshairDot", hudObj.transform, new Color(1f, 1f, 1f, 0.65f), sprite);
            crosshair.GetComponent<Image>().raycastTarget = false;
            SetRect(crosshair.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));

            // 3. Top-Right Employee Status Card
            GameObject empCard = CreatePanel("EmployeeStatusCard", hudObj.transform, new Color(0.08f, 0.11f, 0.17f, 0.90f), sprite);
            SetRect(empCard.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -96f), new Vector2(440f, 250f));

            Text empHeader = CreateText("EmployeeHeaderText", empCard.transform, font, "김민수  ·  성격: 성실함", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(empHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-36f, 30f));

            Text roleText = CreateText("CurrentRoleText", empCard.transform, font, "현재 업무: 기본 대기 (IDLE)", 17, FontStyle.Normal, new Color(0.78f, 0.86f, 0.95f, 1f), TextAnchor.MiddleLeft);
            SetRect(roleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -52f), new Vector2(280f, 28f));

            GameObject moveBadgeObj = CreatePanel("MoveBadge", empCard.transform, new Color(0.30f, 0.35f, 0.42f, 1f), sprite);
            SetRect(moveBadgeObj.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -52f), new Vector2(114f, 28f));
            Text moveBadgeTxt = CreateText("MoveBadgeText", moveBadgeObj.transform, font, "위치 대기", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(moveBadgeTxt.rectTransform);

            Image stressFill;
            Text stressVal;
            BuildStatRow(empCard.transform, font, sprite, "스트레스", -102f, new Color(0.20f, 0.78f, 0.52f, 1f), "25 (안정)", out stressFill, out stressVal);

            Image fatigueFill;
            Text fatigueVal;
            BuildStatRow(empCard.transform, font, sprite, "피로도", -148f, new Color(0.94f, 0.58f, 0.20f, 1f), "15", out fatigueFill, out fatigueVal);

            Image loyaltyFill;
            Text loyaltyVal;
            BuildStatRow(empCard.transform, font, sprite, "충성도", -194f, new Color(0.24f, 0.62f, 0.96f, 1f), "70", out loyaltyFill, out loyaltyVal);

            // 4. Bottom Voice & Command Feedback Dock (UI-10)
            GameObject voiceDock = CreatePanel("VoiceFeedbackDock", hudObj.transform, new Color(0.08f, 0.11f, 0.17f, 0.92f), sprite);
            SetRect(voiceDock.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1040f, 134f));

            GameObject stageBadge = CreatePanel("VoiceStageBadge", voiceDock.transform, new Color(0.24f, 0.29f, 0.36f, 0.95f), sprite);
            SetRect(stageBadge.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(270f, 38f));
            Text stageTxt = CreateText("VoiceStageText", stageBadge.transform, font, "[V] 마이크 대기 중", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(stageTxt.rectTransform);

            Text transcriptTxt = CreateText("TranscriptText", voiceDock.transform, font, "인식 문장: -", 18, FontStyle.Normal, new Color(0.90f, 0.94f, 0.98f, 1f), TextAnchor.MiddleLeft);
            SetRect(transcriptTxt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(304f, -14f), new Vector2(-322f, 38f));

            GameObject bannerObj = CreatePanel("FeedbackBanner", voiceDock.transform, new Color(0.15f, 0.20f, 0.27f, 0.92f), sprite);
            SetRect(bannerObj.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-36f, 56f));
            Text bannerTxt = CreateText("FeedbackBannerText", bannerObj.transform, font, "[안내]  [V] 키를 눌러 음성으로 지시하거나 [Tab] 키로 텍스트 명령창을 여세요.", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(bannerTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28f, 0f));

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("hudRoot").objectReferenceValue = hudObj;
            so.FindProperty("dayLabelText").objectReferenceValue = dayText;
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.FindProperty("balanceText").objectReferenceValue = balanceText;
            so.FindProperty("openCommandOverlayButton").objectReferenceValue = cmdBtn;
            so.FindProperty("employeeHeaderText").objectReferenceValue = empHeader;
            so.FindProperty("currentRoleText").objectReferenceValue = roleText;
            so.FindProperty("moveBadgeBackground").objectReferenceValue = moveBadgeObj.GetComponent<Image>();
            so.FindProperty("moveBadgeText").objectReferenceValue = moveBadgeTxt;
            so.FindProperty("stressFillImage").objectReferenceValue = stressFill;
            so.FindProperty("stressValueText").objectReferenceValue = stressVal;
            so.FindProperty("fatigueFillImage").objectReferenceValue = fatigueFill;
            so.FindProperty("fatigueValueText").objectReferenceValue = fatigueVal;
            so.FindProperty("loyaltyFillImage").objectReferenceValue = loyaltyFill;
            so.FindProperty("loyaltyValueText").objectReferenceValue = loyaltyVal;
            so.FindProperty("voiceStageBadgeBg").objectReferenceValue = stageBadge.GetComponent<Image>();
            so.FindProperty("voiceStageText").objectReferenceValue = stageTxt;
            so.FindProperty("transcriptText").objectReferenceValue = transcriptTxt;
            so.FindProperty("feedbackBannerBg").objectReferenceValue = bannerObj.GetComponent<Image>();
            so.FindProperty("feedbackBannerText").objectReferenceValue = bannerTxt;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static void BuildStatRow(
            Transform parent,
            Font font,
            Sprite sprite,
            string label,
            float topOffset,
            Color barColor,
            string defaultVal,
            out Image fillImg,
            out Text valTxt)
        {
            Text lbl = CreateText(label + "_Label", parent, font, label, 16, FontStyle.Bold, new Color(0.82f, 0.87f, 0.93f, 1f), TextAnchor.MiddleLeft);
            SetRect(lbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, topOffset), new Vector2(82f, 26f));

            GameObject track = CreatePanel(label + "_Track", parent, new Color(0.18f, 0.22f, 0.29f, 1f), sprite);
            SetRect(track.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, topOffset - 5f), new Vector2(204f, 16f));

            GameObject fillObj = CreatePanel(label + "_Fill", track.transform, barColor, sprite);
            StretchFull(fillObj.GetComponent<RectTransform>());
            fillImg = fillObj.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0.25f;

            valTxt = CreateText(label + "_Value", parent, font, defaultVal, 15, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            SetRect(valTxt.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, topOffset), new Vector2(104f, 26f));
        }

        private static CommandOverlayPresenter BuildCommandOverlay(Transform parent, Font font, Sprite sprite)
        {
            GameObject dimBg = CreatePanel("CommandOverlay", parent, new Color(0.03f, 0.05f, 0.09f, 0.68f), sprite);
            StretchFull(dimBg.GetComponent<RectTransform>());
            CommandOverlayPresenter presenter = dimBg.AddComponent<CommandOverlayPresenter>();

            GameObject modal = CreatePanel("CommandModalCard", dimBg.transform, new Color(0.11f, 0.15f, 0.22f, 0.98f), sprite);
            SetRect(modal.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 310f));

            Text header = CreateText("ModalHeader", modal.transform, font, "보조 텍스트 업무 지시창   (마이크 대신 직접 명령 전달)", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(-160f, 36f));

            Button closeBtn = CreateButton("CloseOverlayButton", modal.transform, font, sprite, "닫기 [Esc]", 16, new Color(0.25f, 0.30f, 0.38f, 1f), Color.white);
            SetRect(closeBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(112f, 38f));

            GameObject inputBg = CreatePanel("CommandInputField", modal.transform, new Color(0.06f, 0.09f, 0.14f, 1f), sprite);
            SetRect(inputBg.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -86f), new Vector2(510f, 54f));
            InputField inputField = inputBg.AddComponent<InputField>();

            Text placeholder = CreateText("Placeholder", inputBg.transform, font, "예: 민수야 계산대 좀 봐줘 / 진열대 채워줘 / 잠깐 쉬어...", 18, FontStyle.Italic, new Color(0.50f, 0.57f, 0.66f, 1f), TextAnchor.MiddleLeft);
            SetRect(placeholder.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28f, -10f));

            Text inputText = CreateText("InputText", inputBg.transform, font, string.Empty, 19, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            inputText.supportRichText = false;
            SetRect(inputText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28f, -10f));

            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.targetGraphic = inputBg.GetComponent<Image>();

            Button submitBtn = CreateButton("SubmitCommandButton", modal.transform, font, sprite, "지시 전달", 19, new Color(0.06f, 0.65f, 0.57f, 1f), Color.white);
            SetRect(submitBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -86f), new Vector2(142f, 54f));

            Text quickLabel = CreateText("QuickTitle", modal.transform, font, "편의점 공통 업무 빠른 지시 버튼:", 16, FontStyle.Normal, new Color(0.72f, 0.79f, 0.88f, 1f), TextAnchor.MiddleLeft);
            SetRect(quickLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(28f, 96f), new Vector2(-56f, 28f));

            Button qCheckout = CreateButton("QuickCheckoutButton", modal.transform, font, sprite, "계산 지시 (Checkout)", 17, new Color(0.16f, 0.44f, 0.74f, 1f), Color.white);
            SetRect(qCheckout.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 32f), new Vector2(208f, 50f));

            Button qRestock = CreateButton("QuickRestockButton", modal.transform, font, sprite, "진열 지시 (Restock)", 17, new Color(0.16f, 0.54f, 0.44f, 1f), Color.white);
            SetRect(qRestock.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-2f, 32f), new Vector2(208f, 50f));

            Button qCancel = CreateButton("CancelWorkButton", modal.transform, font, sprite, "휴식 · 대기 (Rest)", 17, new Color(0.46f, 0.34f, 0.20f, 1f), Color.white);
            SetRect(qCancel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 32f), new Vector2(212f, 50f));

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("overlayRoot").objectReferenceValue = dimBg;
            so.FindProperty("commandInputField").objectReferenceValue = inputField;
            so.FindProperty("submitButton").objectReferenceValue = submitBtn;
            so.FindProperty("quickCheckoutButton").objectReferenceValue = qCheckout;
            so.FindProperty("quickRestockButton").objectReferenceValue = qRestock;
            so.FindProperty("cancelWorkButton").objectReferenceValue = qCancel;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static SettlementPanelPresenter BuildSettlementPanel(Transform parent, Font font, Sprite sprite)
        {
            GameObject dimBg = CreatePanel("SettlementPanel", parent, new Color(0.04f, 0.06f, 0.11f, 0.88f), sprite);
            StretchFull(dimBg.GetComponent<RectTransform>());
            SettlementPanelPresenter presenter = dimBg.AddComponent<SettlementPanelPresenter>();

            GameObject card = CreatePanel("SettlementCard", dimBg.transform, new Color(0.11f, 0.15f, 0.22f, 0.99f), sprite);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 590f));

            GameObject headerBanner = CreatePanel("GradeHeaderBanner", card.transform, new Color(0.12f, 0.56f, 0.38f, 1f), sprite);
            SetRect(headerBanner.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 108f));

            Text gradeTitle = CreateText("GradeTitleText", headerBanner.transform, font, "1일차 정산 결과  ·  안정 운영 성공", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetRect(gradeTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-40f, 42f));

            Text gradeSummary = CreateText("GradeSummaryText", headerBanner.transform, font, "목표 잔액을 달성했으며 직원의 감정 상태도 안정적으로 유지했습니다.", 17, FontStyle.Normal, new Color(0.92f, 0.97f, 0.95f, 1f), TextAnchor.MiddleCenter);
            SetRect(gradeSummary.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-40f, 32f));

            GameObject receiptBox = CreatePanel("ReceiptBox", card.transform, new Color(0.07f, 0.10f, 0.16f, 1f), sprite);
            SetRect(receiptBox.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(716f, 236f));

            Text breakdownTxt = CreateText("BreakdownBodyText", receiptBox.transform, font, "정산 내역 계산 대기 중...", 19, FontStyle.Normal, new Color(0.88f, 0.93f, 0.98f, 1f), TextAnchor.MiddleLeft);
            breakdownTxt.lineSpacing = 1.32f;
            SetRect(breakdownTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-44f, -24f));

            GameObject highlightBar = CreatePanel("FinalBalanceBar", card.transform, new Color(0.16f, 0.24f, 0.34f, 1f), sprite);
            SetRect(highlightBar.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 152f), new Vector2(716f, 58f));

            Text finalBalTxt = CreateText("FinalBalanceHighlightText", highlightBar.transform, font, "당일 최종 잔액 :  80,000원", 22, FontStyle.Bold, new Color(0.98f, 0.84f, 0.34f, 1f), TextAnchor.MiddleCenter);
            StretchFull(finalBalTxt.rectTransform);

            Text staffSummaryTxt = CreateText("StaffSummaryText", card.transform, font, "직원 상태 기록  —  종료 스트레스: 25 / 100   ·   분노 거부 누적: 0회", 17, FontStyle.Normal, new Color(0.75f, 0.82f, 0.90f, 1f), TextAnchor.MiddleCenter);
            SetRect(staffSummaryTxt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(716f, 34f));

            Button retryBtn = CreateButton("RetryDayButton", card.transform, font, sprite, "다시 영업 시작 (재도전)", 20, new Color(0.06f, 0.65f, 0.57f, 1f), Color.white);
            SetRect(retryBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-178f, 28f), new Vector2(336f, 56f));

            Button returnLobbyBtn = CreateButton("ReturnLobbyButton", card.transform, font, sprite, "로비 화면으로", 20, new Color(0.24f, 0.30f, 0.39f, 1f), Color.white);
            SetRect(returnLobbyBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(178f, 28f), new Vector2(336f, 56f));

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("panelRoot").objectReferenceValue = dimBg;
            so.FindProperty("gradeHeaderBanner").objectReferenceValue = headerBanner.GetComponent<Image>();
            so.FindProperty("gradeTitleText").objectReferenceValue = gradeTitle;
            so.FindProperty("gradeSummaryText").objectReferenceValue = gradeSummary;
            so.FindProperty("breakdownBodyText").objectReferenceValue = breakdownTxt;
            so.FindProperty("finalBalanceHighlightText").objectReferenceValue = finalBalTxt;
            so.FindProperty("staffSummaryText").objectReferenceValue = staffSummaryTxt;
            so.FindProperty("retryDayButton").objectReferenceValue = retryBtn;
            so.FindProperty("returnLobbyButton").objectReferenceValue = returnLobbyBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static PausePanelPresenter BuildPausePanel(Transform parent, Font font, Sprite sprite)
        {
            GameObject dimBg = CreatePanel("PausePanel", parent, new Color(0.03f, 0.05f, 0.09f, 0.76f), sprite);
            StretchFull(dimBg.GetComponent<RectTransform>());
            PausePanelPresenter presenter = dimBg.AddComponent<PausePanelPresenter>();

            GameObject card = CreatePanel("PauseCard", dimBg.transform, new Color(0.11f, 0.15f, 0.22f, 0.99f), sprite);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 340f));

            Text title = CreateText("PauseTitle", card.transform, font, "영업 일시정지", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(380f, 42f));

            Button resumeBtn = CreateButton("ResumeButton", card.transform, font, sprite, "영업 계속하기 [Esc]", 19, new Color(0.06f, 0.65f, 0.57f, 1f), Color.white);
            SetRect(resumeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(340f, 52f));

            Button settingsBtn = CreateButton("OpenSettingsButton", card.transform, font, sprite, "환경 설정", 19, new Color(0.18f, 0.28f, 0.42f, 1f), Color.white);
            SetRect(settingsBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(340f, 52f));

            Button lobbyBtn = CreateButton("ReturnLobbyButton", card.transform, font, sprite, "메인 로비로 이동", 19, new Color(0.26f, 0.30f, 0.38f, 1f), Color.white);
            SetRect(lobbyBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(340f, 52f));

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("panelRoot").objectReferenceValue = dimBg;
            so.FindProperty("resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("openSettingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("returnLobbyButton").objectReferenceValue = lobbyBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static SettingsPanelPresenter BuildSettingsPanel(Transform parent, Font font, Sprite sprite)
        {
            GameObject dimBg = CreatePanel("SettingsPanel", parent, new Color(0.03f, 0.05f, 0.09f, 0.82f), sprite);
            StretchFull(dimBg.GetComponent<RectTransform>());
            SettingsPanelPresenter presenter = dimBg.AddComponent<SettingsPanelPresenter>();

            GameObject card = CreatePanel("SettingsCard", dimBg.transform, new Color(0.11f, 0.15f, 0.22f, 0.99f), sprite);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 470f));

            Text title = CreateText("SettingsTitle", card.transform, font, "환경 설정   (사운드 · 감도 · 마이크 장치)", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(-160f, 38f));

            Button closeBtn = CreateButton("CloseSettingsButton", card.transform, font, sprite, "닫기 [Esc]", 16, new Color(0.25f, 0.30f, 0.38f, 1f), Color.white);
            SetRect(closeBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(112f, 38f));

            Slider volSlider;
            Text volValue;
            BuildSliderRow(card.transform, font, sprite, "마스터 볼륨", -94f, 0f, 1f, 1f, "100%", out volSlider, out volValue);

            Slider bgmSlider;
            Text bgmValue;
            BuildSliderRow(card.transform, font, sprite, "배경음 볼륨", -150f, 0f, 1f, 0.75f, "75%", out bgmSlider, out bgmValue);

            Slider sfxSlider;
            Text sfxValue;
            BuildSliderRow(card.transform, font, sprite, "효과음 볼륨", -206f, 0f, 1f, 1f, "100%", out sfxSlider, out sfxValue);

            Slider sensSlider;
            Text sensValue;
            BuildSliderRow(card.transform, font, sprite, "마우스 감도", -262f, 0.2f, 2.0f, 1.0f, "1.0x", out sensSlider, out sensValue);

            Button micBtn = CreateButton("CycleMicButton", card.transform, font, sprite, "마이크 입력 장치 확인 중...", 16, new Color(0.16f, 0.23f, 0.33f, 1f), new Color(0.90f, 0.95f, 1f, 1f));
            SetRect(micBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(584f, 56f));
            Text micBtnLabel = micBtn.transform.Find("Label").GetComponent<Text>();

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("panelRoot").objectReferenceValue = dimBg;
            so.FindProperty("masterVolumeSlider").objectReferenceValue = volSlider;
            so.FindProperty("masterVolumeValueText").objectReferenceValue = volValue;
            so.FindProperty("bgmVolumeSlider").objectReferenceValue = bgmSlider;
            so.FindProperty("bgmVolumeValueText").objectReferenceValue = bgmValue;
            so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("sfxVolumeValueText").objectReferenceValue = sfxValue;
            so.FindProperty("mouseSensitivitySlider").objectReferenceValue = sensSlider;
            so.FindProperty("mouseSensitivityValueText").objectReferenceValue = sensValue;
            so.FindProperty("cycleMicrophoneButton").objectReferenceValue = micBtn;
            so.FindProperty("microphoneDeviceText").objectReferenceValue = micBtnLabel;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static void BuildSliderRow(
            Transform parent,
            Font font,
            Sprite sprite,
            string label,
            float topOffset,
            float minVal,
            float maxVal,
            float defaultVal,
            string defaultText,
            out Slider slider,
            out Text valueText)
        {
            Text lbl = CreateText(label + "_Label", parent, font, label, 18, FontStyle.Bold, new Color(0.84f, 0.89f, 0.95f, 1f), TextAnchor.MiddleLeft);
            SetRect(lbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, topOffset), new Vector2(140f, 32f));

            GameObject sliderObj = new GameObject(label + "_Slider", typeof(RectTransform));
            sliderObj.transform.SetParent(parent, false);
            SetRect(sliderObj.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(176f, topOffset - 6f), new Vector2(340f, 22f));

            GameObject bg = CreatePanel("Background", sliderObj.transform, new Color(0.18f, 0.22f, 0.30f, 1f), sprite);
            StretchFull(bg.GetComponent<RectTransform>());

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            StretchFull(fillArea.GetComponent<RectTransform>());

            GameObject fill = CreatePanel("Fill", fillArea.transform, new Color(0.06f, 0.65f, 0.57f, 1f), sprite);
            StretchFull(fill.GetComponent<RectTransform>());

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObj.transform, false);
            StretchFull(handleArea.GetComponent<RectTransform>());

            GameObject handle = CreatePanel("Handle", handleArea.transform, Color.white, sprite);
            SetRect(handle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 28f));

            slider = sliderObj.AddComponent<Slider>();
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.value = defaultVal;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();

            valueText = CreateText(label + "_Value", parent, font, defaultText, 18, FontStyle.Bold, new Color(0.96f, 0.84f, 0.36f, 1f), TextAnchor.MiddleRight);
            SetRect(valueText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, topOffset), new Vector2(86f, 32f));
        }

        private static GameObject BuildWorldStatusHierarchy(Font font, Sprite sprite)
        {
            GameObject root = new GameObject("PF_EmployeeWorldStatusUI");
            Canvas worldCanvas = root.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(420f, 130f);
            root.transform.localScale = new Vector3(0.0045f, 0.0045f, 0.0045f);

            EmployeeWorldStatusPresenter presenter = root.AddComponent<EmployeeWorldStatusPresenter>();

            GameObject headerBg = CreatePanel("StatusHeaderBadge", root.transform, new Color(0.10f, 0.42f, 0.32f, 0.92f), sprite);
            headerBg.GetComponent<Image>().raycastTarget = false;
            SetRect(headerBg.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(310f, 44f));

            Text headerTxt = CreateText("StatusHeaderText", headerBg.transform, font, "김민수  ·  대기/휴식 [안정]", 21, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            headerTxt.raycastTarget = false;
            StretchFull(headerTxt.rectTransform);

            GameObject bubbleObj = CreatePanel("SpeechBubble", root.transform, new Color(0.12f, 0.18f, 0.26f, 0.95f), sprite);
            bubbleObj.GetComponent<Image>().raycastTarget = false;
            SetRect(bubbleObj.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(410f, 62f));

            Text bubbleTxt = CreateText("SpeechBubbleText", bubbleObj.transform, font, "\"네! 바로 계산대 보겠습니다.\"", 19, FontStyle.Bold, new Color(0.96f, 0.98f, 1f, 1f), TextAnchor.MiddleCenter);
            bubbleTxt.raycastTarget = false;
            SetRect(bubbleTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -8f));
            bubbleObj.SetActive(false);

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("statusHeaderBg").objectReferenceValue = headerBg.GetComponent<Image>();
            so.FindProperty("statusHeaderText").objectReferenceValue = headerTxt;
            so.FindProperty("speechBubbleRoot").objectReferenceValue = bubbleObj;
            so.FindProperty("speechBubbleBg").objectReferenceValue = bubbleObj.GetComponent<Image>();
            so.FindProperty("speechBubbleText").objectReferenceValue = bubbleTxt;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void BuildWorkScene(GameObject savedUiPrefab, GameObject savedWorldUiPrefab, Font font, Sprite whiteSprite)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.14f, 0.18f, 0.24f, 1f);
            camObj.transform.position = new Vector3(0f, 2.2f, -5.8f);
            camObj.transform.eulerAngles = new Vector3(12f, 0f, 0f);
            camObj.AddComponent<AudioListener>();

            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.1f;
            lightObj.transform.eulerAngles = new Vector3(45f, -35f, 0f);

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // Simple 3D Convenience Store Stage & Zone Targets for visual verification
            GameObject stageRoot = new GameObject("Demo_StoreStage");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "StoreFloor";
            floor.transform.SetParent(stageRoot.transform);
            floor.transform.localScale = new Vector3(1.2f, 1f, 0.9f);

            Transform counterTarget = CreateZoneMarker(stageRoot.transform, "Target_Counter", new Vector3(-2.6f, 0f, 0.8f), new Vector3(1.5f, 0.9f, 0.8f));
            Transform shelfTarget = CreateZoneMarker(stageRoot.transform, "Target_Shelf", new Vector3(2.6f, 0f, 1.2f), new Vector3(1.4f, 1.6f, 0.6f));
            Transform restTarget = CreateZoneMarker(stageRoot.transform, "Target_Rest", new Vector3(0f, 0f, -0.8f), new Vector3(1.1f, 0.45f, 0.8f));

            GameObject npcCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcCapsule.name = "Demo_Employee_NPC";
            npcCapsule.transform.SetParent(stageRoot.transform);
            npcCapsule.transform.position = new Vector3(0f, 1.0f, -0.8f);

            GameObject worldUiInstance = (GameObject)PrefabUtility.InstantiatePrefab(savedWorldUiPrefab);
            EmployeeWorldStatusPresenter worldPresenter = worldUiInstance.GetComponent<EmployeeWorldStatusPresenter>();
            SerializedObject worldSo = new SerializedObject(worldPresenter);
            worldSo.FindProperty("followTarget").objectReferenceValue = npcCapsule.transform;
            worldSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject uiInstance = (GameObject)PrefabUtility.InstantiatePrefab(savedUiPrefab);
            StoreUiSessionController uiController = uiInstance.GetComponent<StoreUiSessionController>();
            SerializedObject uiCtrlSo = new SerializedObject(uiController);
            uiCtrlSo.FindProperty("worldStatusPresenter").objectReferenceValue = worldPresenter;
            uiCtrlSo.ApplyModifiedPropertiesWithoutUndo();

            // Demo driver + non-intrusive bottom-left shortcut guide (UI-06, kept outside PF_StoreUIRoot prefab!)
            GameObject demoObj = new GameObject("UI_PrototypeDemo");
            UiPrototypeDemoDriver driver = demoObj.AddComponent<UiPrototypeDemoDriver>();
            SerializedObject driverSo = new SerializedObject(driver);
            driverSo.FindProperty("uiController").objectReferenceValue = uiController;
            driverSo.FindProperty("demoEmployeeTransform").objectReferenceValue = npcCapsule.transform;
            driverSo.FindProperty("counterTarget").objectReferenceValue = counterTarget;
            driverSo.FindProperty("shelfTarget").objectReferenceValue = shelfTarget;
            driverSo.FindProperty("restTarget").objectReferenceValue = restTarget;
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            Canvas demoHintCanvas = demoObj.AddComponent<Canvas>();
            demoHintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            demoHintCanvas.sortingOrder = 60;
            CanvasScaler hintScaler = demoObj.AddComponent<CanvasScaler>();
            hintScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hintScaler.referenceResolution = new Vector2(1920f, 1080f);
            hintScaler.matchWidthOrHeight = 0.5f;

            GameObject hintBox = CreatePanel("DemoShortcutGuide", demoObj.transform, new Color(0.05f, 0.08f, 0.13f, 0.78f), whiteSprite);
            hintBox.GetComponent<Image>().raycastTarget = false;
            SetRect(hintBox.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 174f), new Vector2(440f, 152f));

            Text hintTxt = CreateText(
                "ShortcutText",
                hintBox.transform,
                font,
                "[조작 가이드 · 시연 단축키 (통합 시 비활성화)]\n" +
                "• V : 음성 녹음 시작/종료   • Tab : 보조 텍스트창   • Esc : 일시정지\n" +
                "• F1 : 음성 명령 시뮬레이션 (현재 스트레스 확률 반영)\n" +
                "• F2 : ①단순 오인식   • F3 : ②분노 거부   • F4 : ③이동 오류\n" +
                "• F5 : 스트레스 +25 가산   • F6 : 즉시 영업 종료·정산",
                14,
                FontStyle.Normal,
                new Color(0.76f, 0.86f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            hintTxt.lineSpacing = 1.25f;
            hintTxt.raycastTarget = false;
            SetRect(hintTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -14f));

            EditorSceneManager.SaveScene(scene, WorkScenePath);
        }

        private static Transform CreateZoneMarker(Transform parent, string markerName, Vector3 pos, Vector3 propScale)
        {
            GameObject marker = new GameObject(markerName);
            marker.transform.SetParent(parent);
            marker.transform.position = new Vector3(pos.x, 1.0f, pos.z);

            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = markerName + "_Prop";
            prop.transform.SetParent(parent);
            prop.transform.position = new Vector3(pos.x, propScale.y * 0.5f, pos.z + 0.95f);
            prop.transform.localScale = propScale;

            return marker.transform;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return go;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Font font,
            Sprite sprite,
            string label,
            int fontSize,
            Color bgColor,
            Color textColor)
        {
            GameObject go = CreatePanel(name, parent, bgColor, sprite);
            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            Text txt = CreateText("Label", go.transform, font, label, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform);
            return btn;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static void SetRect(
            RectTransform rt,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }
    }
}
