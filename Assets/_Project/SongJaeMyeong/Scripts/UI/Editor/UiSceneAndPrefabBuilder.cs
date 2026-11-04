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

            BuildWorkScene(savedPrefab, koreanFont);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OpenSourceTeam6.UI] UI 프리팹 및 검증 씬 생성 완료: {PrefabPath}, {WorkScenePath}");
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
            StoreUiSessionController sessionController = root.AddComponent<StoreUiSessionController>();

            LobbyPanelPresenter lobbyPresenter = BuildLobbyPanel(root.transform, font, whiteSprite);
            InGameHudPresenter hudPresenter = BuildInGameHud(root.transform, font, whiteSprite);
            CommandOverlayPresenter overlayPresenter = BuildCommandOverlay(root.transform, font, whiteSprite);
            SettlementPanelPresenter settlementPresenter = BuildSettlementPanel(root.transform, font, whiteSprite);

            hudPresenter.SetVisible(false);
            overlayPresenter.SetOpen(false);
            settlementPresenter.SetVisible(false);
            lobbyPresenter.SetVisible(true);

            SerializedObject so = new SerializedObject(sessionController);
            so.FindProperty("lobbyPanel").objectReferenceValue = lobbyPresenter;
            so.FindProperty("inGameHud").objectReferenceValue = hudPresenter;
            so.FindProperty("commandOverlay").objectReferenceValue = overlayPresenter;
            so.FindProperty("settlementPanel").objectReferenceValue = settlementPresenter;
            so.FindProperty("standaloneFallbackGate").objectReferenceValue = fallbackGate;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static LobbyPanelPresenter BuildLobbyPanel(Transform parent, Font font, Sprite sprite)
        {
            GameObject panelObj = CreatePanel("LobbyPanel", parent, new Color(0.06f, 0.09f, 0.14f, 0.97f), sprite);
            StretchFull(panelObj.GetComponent<RectTransform>());
            LobbyPanelPresenter presenter = panelObj.AddComponent<LobbyPanelPresenter>();

            // Storefront stripe accent at top
            GameObject topStripeMint = CreatePanel("BrandStripe_Mint", panelObj.transform, new Color(0.05f, 0.66f, 0.58f, 1f), sprite);
            SetRect(topStripeMint.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 14f));

            GameObject topStripeAmber = CreatePanel("BrandStripe_Amber", panelObj.transform, new Color(0.96f, 0.58f, 0.18f, 1f), sprite);
            SetRect(topStripeAmber.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(0f, 8f));

            // Center card
            GameObject card = CreatePanel("LobbyCard", panelObj.transform, new Color(0.11f, 0.15f, 0.22f, 0.98f), sprite);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 460f));

            Text tagText = CreateText("TagText", card.transform, font, "24H AI CONVENIENCE STORE  ·  MVP PROTOTYPE", 16, FontStyle.Bold, new Color(0.22f, 0.85f, 0.74f, 1f), TextAnchor.MiddleCenter);
            SetRect(tagText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(600f, 28f));

            Text titleText = CreateText("TitleText", card.transform, font, "편의점 AI 매니저", 46, FontStyle.Bold, new Color(0.96f, 0.98f, 1f, 1f), TextAnchor.MiddleCenter);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(600f, 62f));

            Text descText = CreateText("SubtitleText", card.transform, font, "음성 지시와 감정 관리에 따라 직원의 반응과 정산 결말이 달라지는 편의점 운영 시뮬레이션", 18, FontStyle.Normal, new Color(0.73f, 0.79f, 0.87f, 1f), TextAnchor.MiddleCenter);
            SetRect(descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(580f, 52f));

            Button startBtn = CreateButton("StartGameButton", card.transform, font, sprite, "게임 시작  (1일차 영업)", 23, new Color(0.06f, 0.65f, 0.57f, 1f), Color.white);
            SetRect(startBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 132f), new Vector2(380f, 64f));

            Button quitBtn = CreateButton("QuitGameButton", card.transform, font, sprite, "게임 종료", 20, new Color(0.20f, 0.25f, 0.33f, 1f), new Color(0.86f, 0.90f, 0.95f, 1f));
            SetRect(quitBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(380f, 54f));

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("panelRoot").objectReferenceValue = panelObj;
            so.FindProperty("startGameButton").objectReferenceValue = startBtn;
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

            // 1. Top Store Bar
            GameObject topBar = CreatePanel("TopStoreBar", hudObj.transform, new Color(0.08f, 0.11f, 0.17f, 0.90f), sprite);
            SetRect(topBar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-48f, 64f));

            GameObject dayBadge = CreatePanel("DayBadge", topBar.transform, new Color(0.06f, 0.58f, 0.51f, 1f), sprite);
            SetRect(dayBadge.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(170f, 40f));
            Text dayText = CreateText("DayLabelText", dayBadge.transform, font, "1일차 영업 중", 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(dayText.rectTransform);

            Text timerText = CreateText("TimerText", topBar.transform, font, "남은 시간  03:00", 25, FontStyle.Bold, new Color(0.95f, 0.97f, 1f, 1f), TextAnchor.MiddleCenter);
            SetRect(timerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120f, 0f), new Vector2(280f, 44f));

            Text balanceText = CreateText("BalanceText", topBar.transform, font, "예상 잔액  50,000원", 23, FontStyle.Bold, new Color(0.98f, 0.82f, 0.32f, 1f), TextAnchor.MiddleCenter);
            SetRect(balanceText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(210f, 0f), new Vector2(330f, 44f));

            Button cmdBtn = CreateButton("OpenCommandOverlayBtn", topBar.transform, font, sprite, "[Tab] 텍스트 지시창", 17, new Color(0.18f, 0.25f, 0.35f, 1f), new Color(0.88f, 0.94f, 1f, 1f));
            SetRect(cmdBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(210f, 42f));

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
            BuildStatRow(empCard.transform, font, sprite, "스트레스", -102f, new Color(0.20f, 0.78f, 0.52f, 1f), "20 (안정)", out stressFill, out stressVal);

            Image fatigueFill;
            Text fatigueVal;
            BuildStatRow(empCard.transform, font, sprite, "피로도", -148f, new Color(0.94f, 0.58f, 0.20f, 1f), "10", out fatigueFill, out fatigueVal);

            Image loyaltyFill;
            Text loyaltyVal;
            BuildStatRow(empCard.transform, font, sprite, "충성도", -194f, new Color(0.24f, 0.62f, 0.96f, 1f), "75", out loyaltyFill, out loyaltyVal);

            // 4. Bottom Voice & Command Feedback Dock
            GameObject voiceDock = CreatePanel("VoiceFeedbackDock", hudObj.transform, new Color(0.08f, 0.11f, 0.17f, 0.92f), sprite);
            SetRect(voiceDock.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(980f, 128f));

            GameObject stageBadge = CreatePanel("VoiceStageBadge", voiceDock.transform, new Color(0.24f, 0.29f, 0.36f, 0.95f), sprite);
            SetRect(stageBadge.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(260f, 38f));
            Text stageTxt = CreateText("VoiceStageText", stageBadge.transform, font, "마이크 대기 중", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(stageTxt.rectTransform);

            Text transcriptTxt = CreateText("TranscriptText", voiceDock.transform, font, "인식 문장: -", 18, FontStyle.Normal, new Color(0.90f, 0.94f, 0.98f, 1f), TextAnchor.MiddleLeft);
            SetRect(transcriptTxt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(294f, -14f), new Vector2(-312f, 38f));

            GameObject bannerObj = CreatePanel("FeedbackBanner", voiceDock.transform, new Color(0.15f, 0.20f, 0.27f, 0.92f), sprite);
            SetRect(bannerObj.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-36f, 52f));
            Text bannerTxt = CreateText("FeedbackBannerText", bannerObj.transform, font, "[안내]  마이크로 업무를 지시하거나 [Tab] 키를 눌러 텍스트 명령창을 여세요.", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
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

            // InputField
            GameObject inputBg = CreatePanel("CommandInputField", modal.transform, new Color(0.06f, 0.09f, 0.14f, 1f), sprite);
            SetRect(inputBg.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -86f), new Vector2(510f, 54f));
            InputField inputField = inputBg.AddComponent<InputField>();

            Text placeholder = CreateText("Placeholder", inputBg.transform, font, "예: 민수야 계산대 좀 봐줘 / 진열대에 물건 채워줘...", 18, FontStyle.Italic, new Color(0.50f, 0.57f, 0.66f, 1f), TextAnchor.MiddleLeft);
            SetRect(placeholder.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28f, -10f));

            Text inputText = CreateText("InputText", inputBg.transform, font, string.Empty, 19, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            inputText.supportRichText = false;
            SetRect(inputText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28f, -10f));

            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.targetGraphic = inputBg.GetComponent<Image>();

            Button submitBtn = CreateButton("SubmitCommandButton", modal.transform, font, sprite, "지시 전달", 19, new Color(0.06f, 0.65f, 0.57f, 1f), Color.white);
            SetRect(submitBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -86f), new Vector2(142f, 54f));

            Text quickLabel = CreateText("QuickTitle", modal.transform, font, "시제품 빠른 업무 지시 버튼:", 16, FontStyle.Normal, new Color(0.72f, 0.79f, 0.88f, 1f), TextAnchor.MiddleLeft);
            SetRect(quickLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(28f, 96f), new Vector2(-56f, 28f));

            Button qCheckout = CreateButton("QuickCheckoutButton", modal.transform, font, sprite, "계산 지시 (Checkout)", 17, new Color(0.16f, 0.44f, 0.74f, 1f), Color.white);
            SetRect(qCheckout.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 32f), new Vector2(208f, 50f));

            Button qRestock = CreateButton("QuickRestockButton", modal.transform, font, sprite, "진열 지시 (Restock)", 17, new Color(0.16f, 0.54f, 0.44f, 1f), Color.white);
            SetRect(qRestock.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-2f, 32f), new Vector2(208f, 50f));

            Button qCancel = CreateButton("CancelWorkButton", modal.transform, font, sprite, "작업 취소 · 기본 대기", 17, new Color(0.52f, 0.25f, 0.25f, 1f), Color.white);
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

            Text staffSummaryTxt = CreateText("StaffSummaryText", card.transform, font, "직원 상태 기록  —  종료 스트레스: 20 / 100   ·   분노 거부 누적: 0회", 17, FontStyle.Normal, new Color(0.75f, 0.82f, 0.90f, 1f), TextAnchor.MiddleCenter);
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

        private static void BuildWorkScene(GameObject savedUiPrefab, Font font)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.14f, 0.18f, 0.24f, 1f);
            camObj.transform.position = new Vector3(0f, 1.6f, -4f);
            camObj.AddComponent<AudioListener>();

            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.1f;
            lightObj.transform.eulerAngles = new Vector3(45f, -35f, 0f);

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            GameObject uiInstance = (GameObject)PrefabUtility.InstantiatePrefab(savedUiPrefab);
            StoreUiSessionController uiController = uiInstance.GetComponent<StoreUiSessionController>();

            // Demo driver + non-intrusive bottom-left shortcut guide (kept outside PF_StoreUIRoot prefab!)
            GameObject demoObj = new GameObject("UI_PrototypeDemo");
            UiPrototypeDemoDriver driver = demoObj.AddComponent<UiPrototypeDemoDriver>();
            SerializedObject driverSo = new SerializedObject(driver);
            driverSo.FindProperty("uiController").objectReferenceValue = uiController;
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            Canvas demoHintCanvas = demoObj.AddComponent<Canvas>();
            demoHintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            demoHintCanvas.sortingOrder = 60;
            CanvasScaler hintScaler = demoObj.AddComponent<CanvasScaler>();
            hintScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hintScaler.referenceResolution = new Vector2(1920f, 1080f);
            hintScaler.matchWidthOrHeight = 0.5f;

            Sprite whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            GameObject hintBox = CreatePanel("DemoShortcutGuide", demoObj.transform, new Color(0.05f, 0.08f, 0.13f, 0.78f), whiteSprite);
            hintBox.GetComponent<Image>().raycastTarget = false;
            SetRect(hintBox.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 168f), new Vector2(410f, 132f));

            Text hintTxt = CreateText(
                "ShortcutText",
                hintBox.transform,
                font,
                "[개인 씬 검증 단축키 (통합 시 비활성화)]\n" +
                "• Tab : 보조 텍스트 명령창   • F1 : 음성 수락 시뮬레이션\n" +
                "• F2 : ①단순 오인식   • F3 : ②감정 거부   • F4 : ③이동 오류\n" +
                "• F5 : 스트레스 +25   • F6 : 즉시 일차 종료 및 정산",
                14,
                FontStyle.Normal,
                new Color(0.76f, 0.86f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            hintTxt.lineSpacing = 1.25f;
            hintTxt.raycastTarget = false;
            SetRect(hintTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -14f));

            EditorSceneManager.SaveScene(scene, WorkScenePath);
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
