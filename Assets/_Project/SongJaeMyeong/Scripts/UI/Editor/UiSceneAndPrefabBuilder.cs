using System;
using System.Collections.Generic;
using System.IO;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.Movement.Unity;
using OpenSourceTeam6.Time.Unity;
using OpenSourceTeam6.UI.Core;
using OpenSourceTeam6.UI.Unity;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenSourceTeam6.UI.Editor
{
    using Camera = UnityEngine.Camera;
    public static class UiSceneAndPrefabBuilder
    {
        private const string MenuPath = "OpenSourceTeam6/UI/Create Initial UI Scene And Prefab";
        private const string ApplyFontMenuPath = "OpenSourceTeam6/UI/Apply Custom Font And Transparent Lobby Buttons";
        private const string UiAssetDir = "Assets/_Project/SongJaeMyeong/Assets/UI";
        private const string AudioAssetDir = "Assets/_Project/SongJaeMyeong/Assets/Audio";
        private const string FontAssetDir = "Assets/_Project/SongJaeMyeong/Assets/Fonts";
        private const string PrimaryFontPath = FontAssetDir + "/Cafe24Behappy.ttf";
        private const string KoreanFallbackFontPath = FontAssetDir + "/BusanDandiMyeongjoM.ttf";
        private const string PaperHandwritingFontPath = FontAssetDir + "/KyoboHandwriting2025lyb.ttf";
        private const string WhiteSpritePath = UiAssetDir + "/Spr_WhiteFill.png";
        private const string PortraitSpritePath = UiAssetDir + "/Spr_PortraitSilhouette.png";
        private const string ResumeClipboardSpritePath = UiAssetDir + "/Spr_ResumeClipboard.png";
        private const string PostItSpritePath = UiAssetDir + "/Spr_PostItNote.png";
        private const string SmoothRectSpritePath = UiAssetDir + "/Spr_UiSmoothRect.png";
        private const string PanelCardSpritePath = UiAssetDir + "/Spr_UiPanelCard.png";
        private const string ButtonDepthSpritePath = UiAssetDir + "/Spr_UiButtonDepth.png";
        private const string InputBoxSpritePath = UiAssetDir + "/Spr_UiInputBox.png";
        private const string BarTrackSpritePath = UiAssetDir + "/Spr_UiBarTrack.png";
        private const string SliderHandleSpritePath = UiAssetDir + "/Spr_UiSliderHandle.png";
        private const string PrefabPath = "Assets/_Project/SongJaeMyeong/Prefabs/PF_StoreUIRoot.prefab";
        private const string WorldUiPrefabPath = "Assets/_Project/SongJaeMyeong/Prefabs/PF_EmployeeWorldStatusUI.prefab";
        private const string WorkScenePath = "Assets/_Project/SongJaeMyeong/Scenes/SJM_UI_Work.unity";
        private const string ConvenienceStoreMapScenePath = "Assets/_Project/LeeSeungJae/Scenes/KoreanConvenienceStore.unity";
        private const string EmployeeUniformMatPath = "Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Green.mat";
        private const string PlayerRigPrefabPath = "Assets/_Project/HaJongUk/Prefabs/PlayerRig.prefab";
        private const string PlayerVisualMatPath = "Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Orange.mat";
        private const string ManualCubeMatPath = "Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Yellow.mat";
        private const string ManualSpineMatPath = "Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Dark.mat";
        private const string CustomerCapsulePrefabPath = "Assets/_Project/HaJongUk/Scenes/HJU_CustomerNpc_Test/CustomerCapsule.prefab";
        private const string PedestrianCapsulePrefabPath = "Assets/_Project/HaJongUk/Scenes/HJU_CustomerNpc_Test/PedestrianCapsule.prefab";
        private const string CustomerProductPrefabPath = "Assets/_Project/HaJongUk/Scenes/HJU_CustomerNpc_Test/ProductBox.prefab";

        private static readonly HashSet<string> ConvenienceStoreEnvironmentRoots = new HashSet<string>(StringComparer.Ordinal)
        {
            "Architecture",
            "Storefront",
            "WalkIn_and_Storage",
            "Snack_Shelf",
            "Ramen_Shelf",
            "DailyGoods_Shelf",
            "IceCream_Freezer",
            "Checkout_1",
            "Checkout_2",
            "HotFood",
            "ATM_and_Bin",
            "Table_and_Stools",
            "Outdoor_View"
        };

        private static Sprite _panelCardSprite;
        private static Sprite _buttonDepthSprite;
        private static Sprite _inputBoxSprite;
        private static Sprite _barTrackSprite;
        private static Sprite _sliderHandleSprite;
        private static Sprite _resumeClipboardSprite;
        private static Sprite _postItNoteSprite;
        private static Sprite _smoothRectSprite;

        [InitializeOnLoadMethod]
        private static void AutoSyncUiIfEndingPanelMissing()
        {
            EditorSceneManager.sceneOpened -= OnEditorSceneOpened;
            EditorSceneManager.sceneOpened += OnEditorSceneOpened;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode ||
                    EditorApplication.isCompiling ||
                    EditorApplication.isUpdating)
                {
                    return;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                GameObject worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldUiPrefabPath);
                Font targetKoreanFont = ResolvePrimaryUiFont();
                Font targetPaperFont = ResolvePaperHandwritingFont(targetKoreanFont);
                Scene activeScene = SceneManager.GetActiveScene();
                bool isWorkSceneMissingEnvironment =
                    activeScene.IsValid() &&
                    activeScene.path == WorkScenePath &&
                    (Object.FindAnyObjectByType<TimeEnvironmentController>() == null ||
                     (File.Exists(ConvenienceStoreMapScenePath) &&
                      (GameObject.Find("ConvenienceStore_Map") == null ||
                       GameObject.Find("Counter_ManualCube") == null ||
                       GameObject.Find("LobbyOverviewCamera") == null ||
                       GameObject.Find("PosSettlementStage") == null ||
                       Object.FindAnyObjectByType<StoreInventoryManager>() == null ||
                       (ConvenienceStoreProductPopulator.HasSellableAssetsImported() &&
                        ConvenienceStoreProductPopulator.NeedsProductShelfRefresh(GameObject.Find(ConvenienceStoreProductPopulator.PopulatedMarkerName))) ||
                       Object.FindAnyObjectByType<PlayerRigController>() == null)));

                bool needsFontOrLobbyRefresh = NeedsFontOrTransparentLobbySync(prefab, worldPrefab, targetKoreanFont, targetPaperFont);

                if (prefab == null || prefab.transform.Find("EndingPanel") == null || isWorkSceneMissingEnvironment || needsFontOrLobbyRefresh)
                {
                    BuildUiPrefabAndWorkScene();
                    return;
                }

                EnsurePrefabTimerTextWidth(prefab);
                SyncAudioClipsOnPrefabAndActiveScene(prefab);
            };
        }

        private static bool NeedsFontOrTransparentLobbySync(
            GameObject uiPrefab,
            GameObject worldPrefab,
            Font targetFont,
            Font targetPaperFont)
        {
            if (uiPrefab == null || worldPrefab == null) return true;

            Transform startBtnTr = uiPrefab.transform.Find("LobbyPanel/StartGameButton");
            Image startBtnImg = startBtnTr != null ? startBtnTr.GetComponent<Image>() : null;
            if (startBtnImg != null && startBtnImg.color.a > 0.01f)
            {
                return true;
            }

            if (File.Exists(ResumeClipboardSpritePath) &&
                uiPrefab.transform.Find("PersonaSetupPanel/ResumeClipboard") == null)
            {
                return true;
            }

            Transform topPostItContent = uiPrefab.transform.Find("InGameHud/TopStoreBar/PostItContent");
            if (File.Exists(PostItSpritePath) &&
                (topPostItContent == null || topPostItContent.localScale.x > 0.75f))
            {
                return true;
            }

            if (uiPrefab.transform.Find("InGameHud/VoiceFeedbackDock/PostItContent") != null ||
                uiPrefab.transform.Find("InGameHud/VoiceFeedbackDock/VoiceDockContent") == null ||
                !File.Exists(SmoothRectSpritePath))
            {
                return true;
            }

            if (File.Exists(PostItSpritePath))
            {
                TextureImporter postItImporter = AssetImporter.GetAtPath(PostItSpritePath) as TextureImporter;
                if (postItImporter != null &&
                    (!postItImporter.mipmapEnabled ||
                     postItImporter.filterMode != FilterMode.Trilinear ||
                     postItImporter.textureCompression != TextureImporterCompression.Uncompressed))
                {
                    return true;
                }
            }

            StoreAudioManager prefabAudio = uiPrefab.GetComponent<StoreAudioManager>();
            if (prefabAudio != null)
            {
                SerializedObject audioSo = new SerializedObject(prefabAudio);
                SerializedProperty bgmVolProp = audioSo.FindProperty("_initialBgmVolume");
                if (bgmVolProp != null && bgmVolProp.floatValue > 0.35f)
                {
                    return true;
                }
            }

            Transform sensSliderTr = uiPrefab.transform.Find("SettingsPanel/SettingsCard/마우스 감도_Slider");
            Slider sensSlider = sensSliderTr != null ? sensSliderTr.GetComponent<Slider>() : null;
            if (sensSlider != null && sensSlider.maxValue < 2.5f)
            {
                return true;
            }

            Transform pausePanelTr = uiPrefab.transform.Find("PausePanel");
            if (pausePanelTr == null ||
                pausePanelTr.Find("PauseCard") != null ||
                pausePanelTr.GetComponent<PausePanelPresenter>() == null)
            {
                return true;
            }

            if (uiPrefab.transform.Find("SettlementPanel/PosClickAreaButton") == null)
            {
                return true;
            }

            if (targetFont != null)
            {
                Transform titleTr = uiPrefab.transform.Find("LobbyPanel/TitleGroup/TitleText");
                Text titleText = titleTr != null ? titleTr.GetComponent<Text>() : null;
                if (titleText != null && titleText.font != targetFont)
                {
                    return true;
                }

                Text worldText = worldPrefab.GetComponentInChildren<Text>(true);
                if (worldText != null && worldText.font != targetFont)
                {
                    return true;
                }
            }

            if (targetPaperFont != null)
            {
                Transform resumeTitleTr = uiPrefab.transform.Find("PersonaSetupPanel/ResumeClipboard/PaperContentArea/ResumeTitleText");
                Text resumeTitleText = resumeTitleTr != null ? resumeTitleTr.GetComponent<Text>() : null;
                if (resumeTitleText != null && resumeTitleText.font != targetPaperFont)
                {
                    return true;
                }
            }

            return false;
        }

        private static void OnEditorSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            if (mode == OpenSceneMode.Single &&
                scene.IsValid() &&
                scene.path == WorkScenePath &&
                (Object.FindAnyObjectByType<TimeEnvironmentController>() == null ||
                 (File.Exists(ConvenienceStoreMapScenePath) &&
                  (GameObject.Find("ConvenienceStore_Map") == null ||
                   GameObject.Find("Counter_ManualCube") == null ||
                   GameObject.Find("LobbyOverviewCamera") == null ||
                   GameObject.Find("PosSettlementStage") == null ||
                   Object.FindAnyObjectByType<StoreInventoryManager>() == null ||
                   (ConvenienceStoreProductPopulator.HasSellableAssetsImported() &&
                    ConvenienceStoreProductPopulator.NeedsProductShelfRefresh(GameObject.Find(ConvenienceStoreProductPopulator.PopulatedMarkerName))) ||
                   Object.FindAnyObjectByType<PlayerRigController>() == null))))
            {
                BuildUiPrefabAndWorkScene();
            }
        }

        private static void EnsurePrefabTimerTextWidth(GameObject prefabAsset)
        {
            if (prefabAsset == null) return;
            Transform timerTransform = prefabAsset.transform.Find("InGameHud/TopStoreBar/TimerText");
            RectTransform timerRect = timerTransform != null ? timerTransform.GetComponent<RectTransform>() : null;
            if (timerRect != null && timerRect.sizeDelta.x < 420f)
            {
                timerRect.sizeDelta = new Vector2(460f, timerRect.sizeDelta.y);
                Text timerText = timerTransform.GetComponent<Text>();
                if (timerText != null)
                {
                    timerText.fontSize = 24;
                    timerText.text = "08:00 (낮)  ·  남은 02:00";
                }
                PrefabUtility.SavePrefabAsset(prefabAsset);
                AssetDatabase.SaveAssets();
            }
        }

        private static void SyncAudioClipsOnPrefabAndActiveScene(GameObject prefabAsset)
        {
            string[] audioFiles = { "BGM_Lobby.mp3", "BGM_Normal.mp3", "BGM_Crisis.mp3", "BGM_Settlement.mp3", "SFX_PencilWrite.mp3" };
            for (int i = 0; i < audioFiles.Length; i++)
            {
                string fullPath = $"{AudioAssetDir}/{audioFiles[i]}";
                if (File.Exists(fullPath) && AssetDatabase.LoadAssetAtPath<AudioClip>(fullPath) == null)
                {
                    AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceSynchronousImport);
                }
            }

            StoreAudioManager prefabAudio = prefabAsset != null ? prefabAsset.GetComponent<StoreAudioManager>() : null;
            if (prefabAudio != null)
            {
                SerializedObject checkSo = new SerializedObject(prefabAudio);
                AudioClip lobbyClip = checkSo.FindProperty("_lobbyBgmClip")?.objectReferenceValue as AudioClip;
                AudioClip normalClip = checkSo.FindProperty("_dayNormalBgmClip")?.objectReferenceValue as AudioClip;
                AudioClip crisisClip = checkSo.FindProperty("_dayCrisisBgmClip")?.objectReferenceValue as AudioClip;
                AudioClip settleClip = checkSo.FindProperty("_settlementBgmClip")?.objectReferenceValue as AudioClip;
                AudioClip pencilClip = checkSo.FindProperty("_pencilWriteClip")?.objectReferenceValue as AudioClip;

                bool needsUpdate = lobbyClip == null || normalClip == null || crisisClip == null || settleClip == null ||
                                   pencilClip == null ||
                                   lobbyClip == normalClip || crisisClip == normalClip || settleClip == normalClip;

                if (needsUpdate)
                {
                    AssignDefaultAudioClips(prefabAudio);
                    PrefabUtility.SavePrefabAsset(prefabAsset);

                    StoreAudioManager sceneAudio = Object.FindAnyObjectByType<StoreAudioManager>();
                    if (sceneAudio != null)
                    {
                        AssignDefaultAudioClips(sceneAudio);
                        EditorUtility.SetDirty(sceneAudio);
                        if (sceneAudio.gameObject.scene.IsValid())
                        {
                            EditorSceneManager.MarkSceneDirty(sceneAudio.gameObject.scene);
                            EditorSceneManager.SaveScene(sceneAudio.gameObject.scene);
                        }
                    }

                    AssetDatabase.SaveAssets();
                    Debug.Log("[OpenSourceTeam6.UI] 4종 BGM(로비/영업/분노/정산) 오디오 클립 자동 연결 완료!");
                }
            }

            StoreUiSessionController prefabSession = prefabAsset != null ? prefabAsset.GetComponent<StoreUiSessionController>() : null;
            GameObject worldUiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldUiPrefabPath);
            if (prefabSession != null && worldUiPrefab != null)
            {
                SerializedObject sessionSo = new SerializedObject(prefabSession);
                SerializedProperty worldPrefabProp = sessionSo.FindProperty("defaultWorldStatusPrefab");
                if (worldPrefabProp != null && worldPrefabProp.objectReferenceValue == null)
                {
                    worldPrefabProp.objectReferenceValue = worldUiPrefab;
                    sessionSo.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SavePrefabAsset(prefabAsset);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        [MenuItem(ApplyFontMenuPath, priority = 205)]
        public static void ApplyCustomFontAndTransparentLobbyMenu()
        {
            BuildUiPrefabAndWorkScene();
        }

        [MenuItem(MenuPath, priority = 200)]
        public static void BuildUiPrefabAndWorkScene()
        {
            EnsureFolders();
            Sprite fillSprite = EnsureWhiteSprite();
            Sprite portraitSprite = EnsurePortraitSilhouetteSprite();
            EnsureKenneyUiSprites();
            Font koreanFont = ResolvePrimaryUiFont();
            Font englishAccentFont = ResolveEnglishAccentFont(koreanFont);
            Font paperHandwritingFont = ResolvePaperHandwritingFont(koreanFont);

            GameObject tempWorldUi = BuildWorldStatusHierarchy(koreanFont, fillSprite);
            GameObject savedWorldUiPrefab = PrefabUtility.SaveAsPrefabAsset(tempWorldUi, WorldUiPrefabPath);
            Object.DestroyImmediate(tempWorldUi);

            GameObject tempRoot = BuildStoreUiHierarchy(koreanFont, englishAccentFont, paperHandwritingFont, fillSprite, portraitSprite, savedWorldUiPrefab);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempRoot, PrefabPath);
            Object.DestroyImmediate(tempRoot);

            BuildWorkScene(savedPrefab, savedWorldUiPrefab, koreanFont, paperHandwritingFont != null ? paperHandwritingFont : koreanFont, fillSprite);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OpenSourceTeam6.UI] 데모 규격 반영 UI 프리팹 및 검증 씬 생성 완료 (기본 UI 폰트: {(koreanFont != null ? koreanFont.name : "Default")}, 영문 포인트: {(englishAccentFont != null ? englishAccentFont.name : "Default")}, 종이 손글씨: {(paperHandwritingFont != null ? paperHandwritingFont.name : "Default")}): {PrefabPath}, {WorldUiPrefabPath}, {WorkScenePath}");
        }

        /// <summary>
        /// 이력서, 메모장, 인수인계 수첩 등 종이 위에 적히는 텍스트에 공통으로 적용하는 손글씨 폰트(KyoboHandwriting2025lyb)를 로드한다.
        /// </summary>
        public static Font ResolvePaperHandwritingFont(Font fallbackFont = null)
        {
            string selectedPath = null;
            if (File.Exists(PaperHandwritingFontPath))
            {
                selectedPath = PaperHandwritingFontPath;
            }
            else if (Directory.Exists(FontAssetDir))
            {
                string[] candidates = Directory.GetFiles(FontAssetDir, "*.ttf");
                for (int i = 0; i < candidates.Length; i++)
                {
                    string normalized = candidates[i].Replace('\\', '/');
                    if (normalized.IndexOf("Kyobo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        normalized.IndexOf("Handwriting", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        selectedPath = normalized;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (AssetDatabase.LoadAssetAtPath<Font>(selectedPath) == null)
                {
                    AssetDatabase.ImportAsset(selectedPath, ImportAssetOptions.ForceSynchronousImport);
                }

                TrueTypeFontImporter paperImporter = AssetImporter.GetAtPath(selectedPath) as TrueTypeFontImporter;
                if (paperImporter != null)
                {
                    Font[] desiredReferences = fallbackFont != null
                        ? new[] { fallbackFont }
                        : Array.Empty<Font>();

                    string[] desiredNames = BuildUniqueFontNames(
                        paperImporter.fontTTFName,
                        "KyoboHandwriting2025lyb",
                        "Malgun Gothic",
                        "맑은 고딕",
                        "Arial");

                    bool needsReimport =
                        paperImporter.fontTextureCase != FontTextureCase.Dynamic ||
                        !paperImporter.includeFontData ||
                        !AreStringArraysEqual(paperImporter.fontNames, desiredNames) ||
                        !AreFontArraysEqual(paperImporter.fontReferences, desiredReferences);

                    if (needsReimport)
                    {
                        paperImporter.fontTextureCase = FontTextureCase.Dynamic;
                        paperImporter.includeFontData = true;
                        ApplyFontImporterFallbackSettings(paperImporter, desiredNames, desiredReferences);
                        paperImporter.SaveAndReimport();
                    }
                }

                Font paperFont = AssetDatabase.LoadAssetAtPath<Font>(selectedPath);
                if (paperFont != null)
                {
                    return paperFont;
                }
            }

            return fallbackFont != null
                ? fallbackFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Font ResolveKoreanFallbackFont(out string koreanFontName)
        {
            koreanFontName = "BusanDandiMyeongjo";
            string selectedPath = null;
            if (File.Exists(KoreanFallbackFontPath))
            {
                selectedPath = KoreanFallbackFontPath;
            }
            else if (Directory.Exists(FontAssetDir))
            {
                string[] candidates = Directory.GetFiles(FontAssetDir, "*.ttf");
                for (int i = 0; i < candidates.Length; i++)
                {
                    string normalized = candidates[i].Replace('\\', '/');
                    if (normalized.IndexOf("BusanDandi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        normalized.IndexOf("부산단디", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        selectedPath = normalized;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(selectedPath))
            {
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<Font>(selectedPath) == null)
            {
                AssetDatabase.ImportAsset(selectedPath, ImportAssetOptions.ForceSynchronousImport);
            }

            TrueTypeFontImporter koreanImporter = AssetImporter.GetAtPath(selectedPath) as TrueTypeFontImporter;
            if (koreanImporter != null)
            {
                if (!string.IsNullOrWhiteSpace(koreanImporter.fontTTFName))
                {
                    koreanFontName = koreanImporter.fontTTFName;
                }

                string[] desiredKoreanNames = BuildUniqueFontNames(
                    koreanImporter.fontTTFName,
                    "BusanDandiMyeongjo",
                    "부산단디명조",
                    "Malgun Gothic",
                    "맑은 고딕",
                    "Arial");

                bool needsReimport =
                    koreanImporter.fontTextureCase != FontTextureCase.Dynamic ||
                    !koreanImporter.includeFontData ||
                    !AreStringArraysEqual(koreanImporter.fontNames, desiredKoreanNames);

                if (needsReimport)
                {
                    koreanImporter.fontTextureCase = FontTextureCase.Dynamic;
                    koreanImporter.includeFontData = true;
                    ApplyFontImporterFallbackSettings(koreanImporter, desiredKoreanNames, Array.Empty<Font>());
                    koreanImporter.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Font>(selectedPath);
        }

        private static Font ResolvePrimaryUiFont()
        {
            Font koreanFont = ResolveKoreanFallbackFont(out string koreanTtfName);
            ResolveEnglishAccentFont(koreanFont, koreanTtfName);

            if (koreanFont != null)
            {
                return koreanFont;
            }

            if (File.Exists(PrimaryFontPath))
            {
                Font fallbackEnglish = AssetDatabase.LoadAssetAtPath<Font>(PrimaryFontPath);
                if (fallbackEnglish != null)
                {
                    return fallbackEnglish;
                }
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Font ResolveEnglishAccentFont(Font koreanFallbackFont, string koreanTtfName = "BusanDandiMyeongjo")
        {
            string selectedPath = File.Exists(PrimaryFontPath) ? PrimaryFontPath : null;
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (AssetDatabase.LoadAssetAtPath<Font>(selectedPath) == null)
                {
                    AssetDatabase.ImportAsset(selectedPath, ImportAssetOptions.ForceSynchronousImport);
                }

                TrueTypeFontImporter fontImporter = AssetImporter.GetAtPath(selectedPath) as TrueTypeFontImporter;
                if (fontImporter != null)
                {
                    Font[] desiredReferences = koreanFallbackFont != null
                        ? new[] { koreanFallbackFont }
                        : Array.Empty<Font>();

                    string[] desiredFontNames = BuildUniqueFontNames(
                        fontImporter.fontTTFName,
                        "Cafe24 Behappy",
                        koreanTtfName,
                        "BusanDandiMyeongjo",
                        "부산단디명조",
                        "Malgun Gothic",
                        "맑은 고딕",
                        "AppleGothic",
                        "Arial");

                    bool needsReimport =
                        fontImporter.fontTextureCase != FontTextureCase.Dynamic ||
                        !fontImporter.includeFontData ||
                        !AreStringArraysEqual(fontImporter.fontNames, desiredFontNames) ||
                        !AreFontArraysEqual(fontImporter.fontReferences, desiredReferences);

                    if (needsReimport)
                    {
                        fontImporter.fontTextureCase = FontTextureCase.Dynamic;
                        fontImporter.includeFontData = true;
                        ApplyFontImporterFallbackSettings(fontImporter, desiredFontNames, desiredReferences);
                        fontImporter.SaveAndReimport();
                    }
                }

                Font englishFont = AssetDatabase.LoadAssetAtPath<Font>(selectedPath);
                if (englishFont != null)
                {
                    return englishFont;
                }
            }

            return koreanFallbackFont != null
                ? koreanFallbackFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void ApplyFontImporterFallbackSettings(
            TrueTypeFontImporter importer,
            string[] fontNames,
            Font[] fallbackReferences)
        {
            SerializedObject so = new SerializedObject(importer);
            SerializedProperty namesProp = so.FindProperty("m_FontNames");
            if (namesProp != null && fontNames != null)
            {
                namesProp.arraySize = fontNames.Length;
                for (int i = 0; i < fontNames.Length; i++)
                {
                    namesProp.GetArrayElementAtIndex(i).stringValue = fontNames[i];
                }
            }

            SerializedProperty refsProp = so.FindProperty("m_FallbackFontReferences");
            if (refsProp != null && fallbackReferences != null)
            {
                refsProp.arraySize = fallbackReferences.Length;
                for (int i = 0; i < fallbackReferences.Length; i++)
                {
                    refsProp.GetArrayElementAtIndex(i).objectReferenceValue = fallbackReferences[i];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string[] BuildUniqueFontNames(params string[] names)
        {
            List<string> result = new List<string>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                string candidate = names[i];
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                bool exists = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if (string.Equals(result[j], candidate, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    result.Add(candidate);
                }
            }
            return result.ToArray();
        }

        private static bool AreStringArraysEqual(string[] a, string[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreFontArraysEqual(Font[] a, Font[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/SongJaeMyeong/Assets"))
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong", "Assets");
            if (!AssetDatabase.IsValidFolder(UiAssetDir))
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong/Assets", "UI");
            if (!AssetDatabase.IsValidFolder(FontAssetDir))
                AssetDatabase.CreateFolder("Assets/_Project/SongJaeMyeong/Assets", "Fonts");
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

        private static Sprite EnsurePortraitSilhouetteSprite()
        {
            if (!File.Exists(PortraitSpritePath))
            {
                const int size = 256;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color bg = new Color(0.12f, 0.16f, 0.22f, 1f);
                Color silhouette = new Color(0.56f, 0.60f, 0.66f, 1f);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float nx = (x - size * 0.5f) / (size * 0.5f);
                        float ny = (y - size * 0.5f) / (size * 0.5f);

                        // Head ellipse (upper center)
                        float headDx = nx / 0.29f;
                        float headDy = (ny - 0.18f) / 0.34f;
                        bool inHead = (headDx * headDx + headDy * headDy) <= 1.0f;

                        // Ears
                        float earDx = (Mathf.Abs(nx) - 0.27f) / 0.055f;
                        float earDy = (ny - 0.15f) / 0.09f;
                        bool inEars = (earDx * earDx + earDy * earDy) <= 1.0f;

                        // Neck
                        bool inNeck = Mathf.Abs(nx) <= 0.12f && ny >= -0.30f && ny <= -0.08f;

                        // Shoulders & upper torso bust
                        float shoulderDx = nx / 0.68f;
                        float shoulderDy = (ny + 0.82f) / 0.62f;
                        bool inShoulders = ny <= -0.20f && (shoulderDx * shoulderDx + shoulderDy * shoulderDy) <= 1.0f;

                        tex.SetPixel(x, y, (inHead || inEars || inNeck || inShoulders) ? silhouette : bg);
                    }
                }

                tex.Apply();
                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                File.WriteAllBytes(PortraitSpritePath, png);
                AssetDatabase.ImportAsset(PortraitSpritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(PortraitSpritePath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(PortraitSpritePath);
        }

        private static Sprite EnsureSmoothRectSprite()
        {
            if (!File.Exists(SmoothRectSpritePath))
            {
                const int size = 32;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    int dy = Mathf.Min(y, size - 1 - y);
                    for (int x = 0; x < size; x++)
                    {
                        int dx = Mathf.Min(x, size - 1 - x);
                        int edgeDist = Mathf.Min(dx, dy);
                        float alpha;
                        if (edgeDist == 0)
                        {
                            alpha = 0f;
                        }
                        else if (edgeDist == 1)
                        {
                            alpha = (dx == 1 && dy == 1) ? 0.32f : 0.55f;
                        }
                        else
                        {
                            alpha = 1f;
                        }

                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                tex.Apply();
                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                File.WriteAllBytes(SmoothRectSpritePath, png);
                AssetDatabase.ImportAsset(SmoothRectSpritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            return ConfigureUiSpriteAsset(
                SmoothRectSpritePath,
                new Vector4(4f, 4f, 4f, 4f),
                maxTextureSize: 64,
                enableMipmaps: true,
                uncompressed: true,
                filterMode: FilterMode.Trilinear,
                anisoLevel: 4);
        }

        private static void EnsureKenneyUiSprites()
        {
            _panelCardSprite = ConfigureUiSpriteAsset(PanelCardSpritePath, new Vector4(24f, 24f, 24f, 24f));
            _buttonDepthSprite = ConfigureUiSpriteAsset(ButtonDepthSpritePath, new Vector4(24f, 30f, 24f, 24f));
            _inputBoxSprite = ConfigureUiSpriteAsset(InputBoxSpritePath, new Vector4(24f, 24f, 24f, 24f));
            _barTrackSprite = ConfigureUiSpriteAsset(BarTrackSpritePath, new Vector4(20f, 20f, 20f, 20f));
            _sliderHandleSprite = ConfigureUiSpriteAsset(SliderHandleSpritePath, Vector4.zero);
            _resumeClipboardSprite = ConfigureUiSpriteAsset(ResumeClipboardSpritePath, Vector4.zero, maxTextureSize: 2048);
            _postItNoteSprite = ConfigureUiSpriteAsset(
                PostItSpritePath,
                Vector4.zero,
                maxTextureSize: 1024,
                enableMipmaps: true,
                uncompressed: true,
                filterMode: FilterMode.Trilinear,
                anisoLevel: 8);
            _smoothRectSprite = EnsureSmoothRectSprite();
        }

        private static Sprite ConfigureUiSpriteAsset(
            string assetPath,
            Vector4 border,
            int maxTextureSize = 0,
            bool enableMipmaps = false,
            bool uncompressed = false,
            FilterMode filterMode = FilterMode.Bilinear,
            int anisoLevel = 1)
        {
            if (!File.Exists(assetPath))
            {
                return null;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                TextureImporterCompression targetCompression = uncompressed
                    ? TextureImporterCompression.Uncompressed
                    : TextureImporterCompression.Compressed;

                bool needsReimport =
                    importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single ||
                    importer.spriteBorder != border ||
                    importer.mipmapEnabled != enableMipmaps ||
                    importer.filterMode != filterMode ||
                    importer.wrapMode != TextureWrapMode.Clamp ||
                    (uncompressed && importer.textureCompression != targetCompression) ||
                    (enableMipmaps && importer.anisoLevel != anisoLevel) ||
                    (maxTextureSize > 0 && importer.maxTextureSize < maxTextureSize);

                if (needsReimport)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spriteBorder = border;
                    importer.mipmapEnabled = enableMipmaps;
                    importer.alphaIsTransparency = true;
                    importer.filterMode = filterMode;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.anisoLevel = anisoLevel;
                    if (uncompressed)
                    {
                        importer.textureCompression = targetCompression;
                    }
                    if (maxTextureSize > 0)
                    {
                        importer.maxTextureSize = maxTextureSize;
                    }
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static GameObject BuildStoreUiHierarchy(
            Font font,
            Font englishAccentFont,
            Font paperHandwritingFont,
            Sprite whiteSprite,
            Sprite portraitSprite,
            GameObject worldUiPrefab)
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
            AssignDefaultAudioClips(audioManager);
            StoreUiSessionController sessionController = root.AddComponent<StoreUiSessionController>();

            Font resolvedPaperFont = paperHandwritingFont != null ? paperHandwritingFont : font;
            Sprite smoothRectSprite = _smoothRectSprite != null ? _smoothRectSprite : whiteSprite;
            LobbyPanelPresenter lobbyPresenter = BuildLobbyPanel(root.transform, font, englishAccentFont, whiteSprite, portraitSprite);
            PersonaSetupPanelPresenter personaSetupPresenter = BuildPersonaSetupPanel(root.transform, resolvedPaperFont, englishAccentFont, whiteSprite, portraitSprite);
            InGameHudPresenter hudPresenter = BuildInGameHud(root.transform, font, resolvedPaperFont, whiteSprite, smoothRectSprite);
            CommandOverlayPresenter overlayPresenter = BuildCommandOverlay(root.transform, font, whiteSprite);
            SettlementPanelPresenter settlementPresenter = BuildSettlementPanel(root.transform, font, resolvedPaperFont, whiteSprite);
            EndingPanelPresenter endingPresenter = BuildEndingPanel(root.transform, font, whiteSprite);
            PausePanelPresenter pausePresenter = BuildPausePanel(root.transform, font, whiteSprite);
            SettingsPanelPresenter settingsPresenter = BuildSettingsPanel(root.transform, font, whiteSprite);

            personaSetupPresenter.SetVisible(false);
            hudPresenter.SetVisible(false);
            overlayPresenter.SetOpen(false);
            settlementPresenter.SetVisible(false);
            endingPresenter.SetVisible(false);
            pausePresenter.SetOpen(false);
            settingsPresenter.SetOpen(false);
            lobbyPresenter.SetVisible(true);

            SerializedObject so = new SerializedObject(sessionController);
            so.FindProperty("lobbyPanel").objectReferenceValue = lobbyPresenter;
            so.FindProperty("personaSetupPanel").objectReferenceValue = personaSetupPresenter;
            so.FindProperty("inGameHud").objectReferenceValue = hudPresenter;
            so.FindProperty("commandOverlay").objectReferenceValue = overlayPresenter;
            so.FindProperty("settlementPanel").objectReferenceValue = settlementPresenter;
            so.FindProperty("endingPanel").objectReferenceValue = endingPresenter;
            so.FindProperty("pausePanel").objectReferenceValue = pausePresenter;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPresenter;
            so.FindProperty("defaultWorldStatusPrefab").objectReferenceValue = worldUiPrefab;
            so.FindProperty("audioManager").objectReferenceValue = audioManager;
            so.FindProperty("standaloneFallbackGate").objectReferenceValue = fallbackGate;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void AssignDefaultAudioClips(StoreAudioManager audioManager)
        {
            if (audioManager == null) return;

            SerializedObject audioSo = new SerializedObject(audioManager);
            SerializedProperty bgmVolProp = audioSo.FindProperty("_initialBgmVolume");
            if (bgmVolProp != null) bgmVolProp.floatValue = 0.30f;
            SerializedProperty inGameScaleProp = audioSo.FindProperty("_inGameBgmVolumeScale");
            if (inGameScaleProp != null) inGameScaleProp.floatValue = 0.55f;
            if (!TryBindClip(audioSo, "_lobbyBgmClip", "BGM_Lobby"))
            {
                TryBindClip(audioSo, "_lobbyBgmClip", "BGM_Normal");
            }

            if (!TryBindClip(audioSo, "_dayNormalBgmClip", "BGM_Normal"))
            {
                TryBindClip(audioSo, "_dayNormalBgmClip", "BGM_Lobby");
            }

            if (!TryBindClip(audioSo, "_dayCrisisBgmClip", "BGM_Crisis") &&
                !TryBindClip(audioSo, "_dayCrisisBgmClip", "BGM_Normal"))
            {
                TryBindClip(audioSo, "_dayCrisisBgmClip", "BGM_Lobby");
            }

            if (!TryBindClip(audioSo, "_settlementBgmClip", "BGM_Settlement") &&
                !TryBindClip(audioSo, "_settlementBgmClip", "BGM_Lobby"))
            {
                TryBindClip(audioSo, "_settlementBgmClip", "BGM_Normal");
            }

            TryBindClip(audioSo, "_uiClickClip", "SFX_UiClick");
            TryBindClip(audioSo, "_uiPopupOpenCloseClip", "SFX_UiPopupOpenClose");
            TryBindClip(audioSo, "_storeDoorChimeClip", "SFX_StoreDoorChime");
            TryBindClip(audioSo, "_barcodeCheckoutClip", "SFX_BarcodeCheckout");
            TryBindClip(audioSo, "_workTaskCompleteClip", "SFX_WorkTaskComplete");
            TryBindClip(audioSo, "_paperFlipClip", "SFX_PaperFlip");
            TryBindClip(audioSo, "_pencilWriteClip", "SFX_PencilWrite");

            TryBindClip(audioSo, "_micRecordStartClip", "SFX_MicRecordStart");
            TryBindClip(audioSo, "_micRecordStopClip", "SFX_MicRecordStop");
            TryBindClip(audioSo, "_reactionAcceptClip", "SFX_ReactionAccept");
            TryBindClip(audioSo, "_reactionComplainClip", "SFX_ReactionComplain");
            TryBindClip(audioSo, "_reactionRefuseOrCrisisClip", "SFX_ReactionRefuseOrCrisis");

            TryBindClip(audioSo, "_employeeFootstepClip", "SFX_EmployeeFootstep");
            TryBindClip(audioSo, "_itemPickupOrPlaceClip", "SFX_ItemPickupOrPlace");
            audioSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool TryBindClip(SerializedObject audioSo, string propertyName, string baseFileName)
        {
            SerializedProperty prop = audioSo.FindProperty(propertyName);
            if (prop == null) return false;

            string[] exts = { ".mp3", ".ogg", ".wav" };
            for (int i = 0; i < exts.Length; i++)
            {
                string path = $"{AudioAssetDir}/{baseFileName}{exts[i]}";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    prop.objectReferenceValue = clip;
                    return true;
                }
            }

            return false;
        }

        private static LobbyPanelPresenter BuildLobbyPanel(
            Transform parent,
            Font font,
            Font englishAccentFont,
            Sprite sprite,
            Sprite portraitSprite)
        {
            GameObject panelObj = CreatePanel("LobbyPanel", parent, new Color(0f, 0f, 0f, 0f), sprite);
            StretchFull(panelObj.GetComponent<RectTransform>());
            LobbyPanelPresenter presenter = panelObj.AddComponent<LobbyPanelPresenter>();

            // 1. 좌측 상단: 게임 제목 텍스트만 깔끔하게 배치 (배경/건물/캐릭터 박스는 비워둠)
            GameObject titleGroup = new GameObject("TitleGroup", typeof(RectTransform));
            titleGroup.transform.SetParent(panelObj.transform, false);
            SetRect(titleGroup.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(88f, -92f), new Vector2(960f, 280f));

            Text tagText = CreateText(
                "TagText",
                titleGroup.transform,
                englishAccentFont != null ? englishAccentFont : font,
                "24H  AI  CONVENIENCE  STORE",
                32,
                FontStyle.Bold,
                new Color(0.18f, 0.94f, 0.82f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(tagText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(4f, -10f), new Vector2(0f, 40f));
            Outline tagOutline = tagText.gameObject.AddComponent<Outline>();
            tagOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            tagOutline.effectDistance = new Vector2(1.2f, -1.2f);

            Text titleText = CreateText(
                "TitleText",
                titleGroup.transform,
                font,
                "편의점 AI 매니저",
                84,
                FontStyle.Bold,
                new Color(0.98f, 0.99f, 1f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -56f), new Vector2(0f, 114f));
            Outline titleOutline = titleText.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.72f);
            titleOutline.effectDistance = new Vector2(1.8f, -1.8f);

            Text descText = CreateText(
                "SubtitleText",
                titleGroup.transform,
                font,
                "음성 지시와 감정 관리로 운영하는 편의점 시뮬레이션",
                24,
                FontStyle.Normal,
                new Color(0.94f, 0.97f, 1f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(descText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(4f, -178f), new Vector2(0f, 40f));
            Outline descOutline = descText.gameObject.AddComponent<Outline>();
            descOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            descOutline.effectDistance = new Vector2(1.1f, -1.1f);

            // 2. 좌측 하단: 배경이 투명하고 글자만 보이는 게임 시작 / 게임 설정 / 게임 종료 버튼 3종
            Button startBtn = CreateTransparentTextButton(
                "StartGameButton",
                panelObj.transform,
                font,
                "게임 시작",
                38,
                Color.white,
                new Color(0.24f, 0.96f, 0.84f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(startBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 264f), new Vector2(320f, 72f));

            Button settingsBtn = CreateTransparentTextButton(
                "SettingsButton",
                panelObj.transform,
                font,
                "게임 설정",
                36,
                new Color(0.92f, 0.96f, 1f, 1f),
                new Color(0.24f, 0.96f, 0.84f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(settingsBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 180f), new Vector2(320f, 72f));

            Button quitBtn = CreateTransparentTextButton(
                "QuitGameButton",
                panelObj.transform,
                font,
                "게임 종료",
                36,
                new Color(0.86f, 0.90f, 0.96f, 1f),
                new Color(1f, 0.62f, 0.62f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(quitBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 96f), new Vector2(320f, 72f));

            // 3. 우측 상단 모서리: 투명 배경의 ♪ BGM 온/오프 버튼
            Button bgmBtn = CreateTransparentTextButton(
                "BgmQuickButton",
                panelObj.transform,
                font,
                "♪",
                44,
                Color.white,
                new Color(0.24f, 0.96f, 0.84f, 1f),
                TextAnchor.MiddleCenter);
            SetRect(bgmBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(84f, 84f));
            Text bgmBtnLabel = bgmBtn.transform.Find("Label").GetComponent<Text>();

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("panelRoot").objectReferenceValue = panelObj;
            so.FindProperty("startGameButton").objectReferenceValue = startBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("quitGameButton").objectReferenceValue = quitBtn;
            so.FindProperty("bgmQuickButton").objectReferenceValue = bgmBtn;
            so.FindProperty("bgmQuickButtonLabel").objectReferenceValue = bgmBtnLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static PersonaSetupPanelPresenter BuildPersonaSetupPanel(
            Transform parent,
            Font font,
            Font englishAccentFont,
            Sprite sprite,
            Sprite portraitSprite)
        {
            // 게임 화면이 은은하게 비치는 반투명 어두운 배경 위에 클립보드 종이 이력서를 띄워 종이에 집중할 수 있게 한다.
            GameObject panelObj = CreatePanel("PersonaSetupPanel", parent, new Color(0.02f, 0.03f, 0.05f, 0.62f), sprite);
            StretchFull(panelObj.GetComponent<RectTransform>());
            PersonaSetupPanelPresenter presenter = panelObj.AddComponent<PersonaSetupPanelPresenter>();

            Color inkPrimary = new Color(0.14f, 0.13f, 0.12f, 1f);
            Color inkSecondary = new Color(0.36f, 0.34f, 0.30f, 1f);
            Color inkRule = new Color(0.24f, 0.22f, 0.20f, 0.48f);
            Color inkAccent = new Color(0.56f, 0.20f, 0.14f, 1f);
            Color paperFieldBg = new Color(0.99f, 0.98f, 0.94f, 0.62f);
            Color paperBtnBg = new Color(0.93f, 0.90f, 0.84f, 0.92f);

            // 클립보드 실사 이미지 (906x1326 비율 유지 -> 697x1020)
            GameObject clipboardObj = new GameObject("ResumeClipboard", typeof(RectTransform), typeof(Image));
            clipboardObj.transform.SetParent(panelObj.transform, false);
            SetRect(clipboardObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(697f, 1020f));
            Image clipboardImg = clipboardObj.GetComponent<Image>();
            if (_resumeClipboardSprite != null)
            {
                clipboardImg.sprite = _resumeClipboardSprite;
                clipboardImg.type = Image.Type.Simple;
                clipboardImg.preserveAspect = true;
                clipboardImg.color = Color.white;
            }
            else
            {
                clipboardImg.sprite = sprite;
                clipboardImg.color = new Color(0.94f, 0.91f, 0.85f, 1f);
            }

            // 클립보드 내부 실제 종이 영역(좌우 여백 84px, 상단 금속 클립 아래 172px, 하단 여백 92px -> 529 x 756)
            GameObject paperArea = new GameObject("PaperContentArea", typeof(RectTransform));
            paperArea.transform.SetParent(clipboardObj.transform, false);
            RectTransform paperRt = paperArea.GetComponent<RectTransform>();
            paperRt.anchorMin = Vector2.zero;
            paperRt.anchorMax = Vector2.one;
            paperRt.pivot = new Vector2(0.5f, 0.5f);
            paperRt.offsetMin = new Vector2(84f, 92f);
            paperRt.offsetMax = new Vector2(-84f, -172f);

            // 1. 문서 상단 헤더: "알바 채용 이력서" + 이중 인쇄선
            Text subHeader = CreatePaperText("ResumeDaySubHeader", paperArea.transform, font, "0일차  ·  신규 스태프 채용 심사표", 15, FontStyle.Normal, inkSecondary, TextAnchor.MiddleCenter);
            SetRect(subHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, 24f));

            Text titleText = CreatePaperText("ResumeTitleText", paperArea.transform, font, "이   력   서", 36, FontStyle.Bold, inkPrimary, TextAnchor.MiddleCenter);
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(0f, 48f));

            CreatePaperRule("HeaderRuleBold", paperArea.transform, sprite, -82f, 2.5f, new Color(0.18f, 0.16f, 0.15f, 0.78f));
            CreatePaperRule("HeaderRuleThin", paperArea.transform, sprite, -87f, 1f, inkRule);

            // 2. 프리셋 1·2·3 및 Random 선택 줄
            Text presetLabel = CreatePaperText("PresetRowLabel", paperArea.transform, font, "지원자 프리셋", 16, FontStyle.Bold, inkSecondary, TextAnchor.MiddleLeft);
            SetRect(presetLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -104f), new Vector2(114f, 38f));

            Button preset1Btn = CreatePaperButton("Preset1Button", paperArea.transform, font, sprite, "1", 19, paperBtnBg, inkPrimary, inkRule);
            SetRect(preset1Btn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(118f, -104f), new Vector2(54f, 38f));

            Button preset2Btn = CreatePaperButton("Preset2Button", paperArea.transform, font, sprite, "2", 19, paperBtnBg, inkPrimary, inkRule);
            SetRect(preset2Btn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, -104f), new Vector2(54f, 38f));

            Button preset3Btn = CreatePaperButton("Preset3Button", paperArea.transform, font, sprite, "3", 19, paperBtnBg, inkPrimary, inkRule);
            SetRect(preset3Btn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(242f, -104f), new Vector2(54f, 38f));

            Button randomBtn = CreatePaperButton("RandomButton", paperArea.transform, font, sprite, "Random", 19, new Color(0.89f, 0.85f, 0.78f, 0.95f), inkPrimary, inkRule);
            SetRect(randomBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -104f), new Vector2(130f, 38f));

            CreatePaperRule("PresetBottomRule", paperArea.transform, sprite, -154f, 1f, inkRule);

            // 3. 상단 프로필 영역: 좌측 증명사진(3:4) + 우측 성명/시급 기입란
            GameObject portraitBorder = CreateFlatRect("PortraitFrameBorder", paperArea.transform, new Color(0.28f, 0.26f, 0.24f, 0.72f), sprite);
            SetRect(portraitBorder.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -172f), new Vector2(162f, 204f));

            GameObject portraitMat = CreateFlatRect("PortraitPhotoMat", portraitBorder.transform, new Color(0.95f, 0.94f, 0.91f, 1f), sprite);
            SetRect(portraitMat.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, -4f));

            GameObject portraitInner = CreateFlatRect("PortraitImage", portraitMat.transform, Color.white, portraitSprite != null ? portraitSprite : sprite);
            SetRect(portraitInner.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -8f));
            Image portraitImg = portraitInner.GetComponent<Image>();
            portraitImg.preserveAspect = true;

            // 우측 인적사항 (성명 + 시급)
            Text nameLabel = CreatePaperText("NameLabel", paperArea.transform, font, "성  명", 19, FontStyle.Bold, inkSecondary, TextAnchor.MiddleLeft);
            SetRect(nameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(182f, -176f), new Vector2(347f, 28f));

            InputField nameInput = CreatePaperInputField(
                "NameInputField",
                paperArea.transform,
                font,
                sprite,
                new Vector2(182f, -208f),
                new Vector2(347f, 48f),
                EmployeePersonaPresets.Preset1Name,
                "이름을 입력하세요 (최대 12자)",
                EmployeePersonaPresets.MaxNameLength,
                22,
                paperFieldBg,
                inkRule,
                inkPrimary,
                inkSecondary,
                multiLine: false);

            Text wageLabel = CreatePaperText("WageLabel", paperArea.transform, font, "희망 시급 및 기본급", 18, FontStyle.Bold, inkSecondary, TextAnchor.MiddleLeft);
            SetRect(wageLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(182f, -278f), new Vector2(347f, 28f));

            GameObject wageBox = CreateFlatRect("WagePaperBox", paperArea.transform, new Color(0.96f, 0.93f, 0.87f, 0.72f), sprite);
            SetRect(wageBox.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(182f, -310f), new Vector2(347f, 66f));
            CreatePaperRule("WageUnderline", wageBox.transform, sprite, -64f, 1.5f, inkRule);

            Text wageValueText = CreatePaperText(
                "HourlyWageValueText",
                wageBox.transform,
                font,
                $"{EmployeePersonaPresets.DefaultHourlyWageWon:N0}원  (일일 기본급 20,000원)",
                18,
                FontStyle.Bold,
                inkAccent,
                TextAnchor.MiddleLeft);
            SetRect(wageValueText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -8f));

            CreatePaperRule("MidSectionRule", paperArea.transform, sprite, -396f, 1.5f, inkRule);

            // 4. 중단 영역: 성격 및 특징 (자유 서술 0/50자)
            Text traitLabel = CreatePaperText("TraitLabel", paperArea.transform, font, "성격 및 특기 사항 (자유 기입)", 20, FontStyle.Bold, inkPrimary, TextAnchor.MiddleLeft);
            SetRect(traitLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -412f), new Vector2(360f, 30f));

            string initialCounter = EmployeePersonaPresets.FormatTraitLengthCounter(EmployeePersonaPresets.Preset1Traits);
            Text traitCounterText = CreatePaperText("TraitLengthCounterText", paperArea.transform, font, initialCounter, 16, FontStyle.Normal, inkSecondary, TextAnchor.MiddleRight);
            SetRect(traitCounterText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -414f), new Vector2(140f, 28f));

            InputField traitInput = CreatePaperInputField(
                "TraitInputField",
                paperArea.transform,
                font,
                sprite,
                new Vector2(0f, -448f),
                new Vector2(529f, 164f),
                EmployeePersonaPresets.Preset1Traits,
                "성격 및 특징을 입력하세요 (최대 50자)",
                EmployeePersonaPresets.MaxTraitLength,
                20,
                paperFieldBg,
                inkRule,
                inkPrimary,
                inkSecondary,
                multiLine: true);

            // 5. 하단 서약 문구 및 채용확정 버튼
            Text pledgeText = CreatePaperText(
                "ResumePledgeText",
                paperArea.transform,
                font,
                "위 지원자의 이력 사항을 확인하였으며, 1일차 편의점 근무 채용을 확정합니다.",
                14,
                FontStyle.Normal,
                inkSecondary,
                TextAnchor.MiddleCenter);
            SetRect(pledgeText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(0f, 26f));

            Button confirmStartBtn = CreatePaperButton(
                "ConfirmStartButton",
                paperArea.transform,
                font,
                sprite,
                "채용확정  ·  1일차 시작하기 →",
                23,
                new Color(0.14f, 0.38f, 0.34f, 0.96f),
                new Color(0.98f, 0.97f, 0.94f, 1f),
                new Color(0.08f, 0.24f, 0.21f, 1f));
            SetRect(confirmStartBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(0f, 62f));

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("_panelRoot").objectReferenceValue = panelObj;
            so.FindProperty("_preset1Button").objectReferenceValue = preset1Btn;
            so.FindProperty("_preset2Button").objectReferenceValue = preset2Btn;
            so.FindProperty("_preset3Button").objectReferenceValue = preset3Btn;
            so.FindProperty("_randomButton").objectReferenceValue = randomBtn;
            so.FindProperty("_portraitImage").objectReferenceValue = portraitImg;
            so.FindProperty("_nameInputField").objectReferenceValue = nameInput;
            so.FindProperty("_traitInputField").objectReferenceValue = traitInput;
            so.FindProperty("_traitLengthCounterText").objectReferenceValue = traitCounterText;
            so.FindProperty("_hourlyWageValueText").objectReferenceValue = wageValueText;
            so.FindProperty("_confirmStartButton").objectReferenceValue = confirmStartBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static GameObject CreateFlatRect(string name, Transform parent, Color color, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
            }
            else
            {
                img.type = Image.Type.Simple;
            }
            img.color = color;
            return go;
        }

        private static void CreatePaperRule(string name, Transform parent, Sprite sprite, float topOffsetY, float thickness, Color color)
        {
            GameObject line = CreateFlatRect(name, parent, color, sprite);
            line.GetComponent<Image>().raycastTarget = false;
            SetRect(line.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, topOffsetY), new Vector2(0f, thickness));
        }

        private static Text CreatePaperText(
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
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreatePaperButton(
            string name,
            Transform parent,
            Font font,
            Sprite sprite,
            string label,
            int fontSize,
            Color bgColor,
            Color textColor,
            Color borderColor,
            FontStyle labelStyle = FontStyle.Normal,
            float borderInset = 3f)
        {
            GameObject borderGo = CreateFlatRect(name, parent, borderColor, sprite);
            GameObject fillGo = CreateFlatRect("Fill", borderGo.transform, bgColor, sprite);
            SetRect(fillGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-borderInset, -borderInset));
            Image fillImg = fillGo.GetComponent<Image>();

            Button btn = borderGo.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.94f, 0.92f, 0.88f, 1f);
            cb.pressedColor = new Color(0.84f, 0.81f, 0.76f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            Text txt = CreatePaperText("Label", fillGo.transform, font, label, fontSize, labelStyle, textColor, TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform);
            return btn;
        }

        private static InputField CreatePaperInputField(
            string name,
            Transform parent,
            Font font,
            Sprite sprite,
            Vector2 topLeftAnchoredPos,
            Vector2 size,
            string defaultText,
            string placeholderContent,
            int maxChars,
            int fontSize,
            Color bgColor,
            Color borderColor,
            Color textColor,
            Color placeholderColor,
            bool multiLine)
        {
            GameObject borderObj = CreateFlatRect(name + "_Border", parent, borderColor, sprite);
            SetRect(borderObj.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeftAnchoredPos, size);

            GameObject inputBg = CreateFlatRect(name, borderObj.transform, bgColor, sprite);
            SetRect(inputBg.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-3f, -3f));

            InputField inputField = inputBg.AddComponent<InputField>();
            inputField.characterLimit = maxChars;
            inputField.lineType = multiLine ? InputField.LineType.MultiLineSubmit : InputField.LineType.SingleLine;
            inputField.caretColor = textColor;
            inputField.selectionColor = new Color(0.14f, 0.45f, 0.40f, 0.28f);

            TextAnchor anchor = multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            Vector2 pad = multiLine ? new Vector2(-24f, -24f) : new Vector2(-20f, -8f);

            Text placeholder = CreatePaperText("Placeholder", inputBg.transform, font, placeholderContent, fontSize - 2, FontStyle.Italic, placeholderColor, anchor);
            SetRect(placeholder.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, pad);

            Text inputText = CreatePaperText("InputText", inputBg.transform, font, defaultText, fontSize, FontStyle.Normal, textColor, anchor);
            inputText.supportRichText = false;
            inputText.lineSpacing = multiLine ? 1.32f : 1.0f;
            SetRect(inputText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, pad);

            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.targetGraphic = inputBg.GetComponent<Image>();
            inputField.text = defaultText;
            return inputField;
        }

        private static InputField CreateStyledInputField(
            string name,
            Transform parent,
            Font font,
            Sprite sprite,
            Vector2 topLeftAnchoredPos,
            Vector2 size,
            string defaultText,
            string placeholderContent,
            int maxChars)
        {
            GameObject borderObj = CreatePanel(name + "_Border", parent, new Color(0.32f, 0.40f, 0.52f, 1f), sprite);
            SetRect(borderObj.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeftAnchoredPos, size);

            GameObject inputBg = CreatePanel(name, borderObj.transform, new Color(0.06f, 0.09f, 0.14f, 1f), sprite);
            SetRect(inputBg.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, -4f));

            InputField inputField = inputBg.AddComponent<InputField>();
            inputField.characterLimit = maxChars;

            Text placeholder = CreateText("Placeholder", inputBg.transform, font, placeholderContent, 18, FontStyle.Italic, new Color(0.48f, 0.55f, 0.64f, 1f), TextAnchor.MiddleLeft);
            SetRect(placeholder.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -8f));

            Text inputText = CreateText("InputText", inputBg.transform, font, defaultText, 19, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            inputText.supportRichText = false;
            SetRect(inputText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -8f));

            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.targetGraphic = inputBg.GetComponent<Image>();
            inputField.text = defaultText;
            return inputField;
        }

        private static GameObject CreatePostItNoteCard(
            string name,
            Transform parent,
            Sprite fallbackSprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            float tiltDegrees,
            Color tintColor,
            out Transform contentArea,
            float topPad = 54f,
            float bottomPad = 34f,
            float sidePad = 44f,
            float contentSupersample = 2f)
        {
            GameObject noteObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            noteObj.transform.SetParent(parent, false);
            RectTransform rt = noteObj.GetComponent<RectTransform>();
            SetRect(rt, anchorMin, anchorMax, pivot, anchoredPos, size);
            rt.localEulerAngles = new Vector3(0f, 0f, tiltDegrees);

            Image img = noteObj.GetComponent<Image>();
            if (_postItNoteSprite != null)
            {
                img.sprite = _postItNoteSprite;
                img.type = Image.Type.Simple;
                img.color = tintColor;
            }
            else
            {
                img.sprite = fallbackSprite;
                img.type = Image.Type.Simple;
                img.color = new Color(0.99f, 0.96f, 0.80f, 0.96f);
            }

            float scale = Mathf.Max(1f, contentSupersample);
            GameObject inner = new GameObject("PostItContent", typeof(RectTransform));
            inner.transform.SetParent(noteObj.transform, false);
            RectTransform innerRt = inner.GetComponent<RectTransform>();
            innerRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerRt.anchorMax = new Vector2(0.5f, 0.5f);
            innerRt.pivot = new Vector2(0.5f, 0.5f);
            innerRt.anchoredPosition = new Vector2(0f, (bottomPad - topPad) * 0.5f);
            innerRt.sizeDelta = new Vector2(
                Mathf.Max(40f, size.x - sidePad * 2f) * scale,
                Mathf.Max(40f, size.y - topPad - bottomPad) * scale);
            innerRt.localScale = new Vector3(1f / scale, 1f / scale, 1f);
            contentArea = inner.transform;
            return noteObj;
        }

        private static InGameHudPresenter BuildInGameHud(
            Transform parent,
            Font uiFont,
            Font paperFont,
            Sprite whiteSprite,
            Sprite smoothRectSprite)
        {
            GameObject hudObj = new GameObject("InGameHud", typeof(RectTransform));
            hudObj.transform.SetParent(parent, false);
            StretchFull(hudObj.GetComponent<RectTransform>());
            InGameHudPresenter presenter = hudObj.AddComponent<InGameHudPresenter>();

            Color inkPrimary = new Color(0.14f, 0.13f, 0.11f, 1f);
            Color inkSecondary = new Color(0.30f, 0.27f, 0.23f, 1f);
            Color inkAccent = new Color(0.58f, 0.19f, 0.13f, 1f);
            Color inkRule = new Color(0.24f, 0.21f, 0.17f, 0.34f);
            Color paperBtnBg = new Color(0.96f, 0.92f, 0.81f, 0.95f);

            // 1. 좌측 상단 포스트잇 (2x 슈퍼샘플링으로 기울임/작은 창에서도 또렷하게 렌더)
            CreatePostItNoteCard(
                "TopStoreBar",
                hudObj.transform,
                smoothRectSprite,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -14f),
                new Vector2(428f, 282f),
                tiltDegrees: 1.8f,
                tintColor: Color.white,
                out Transform topContent,
                topPad: 56f,
                bottomPad: 34f,
                sidePad: 44f,
                contentSupersample: 2f);

            GameObject dayBadge = CreateFlatRect("DayBadge", topContent, new Color(0.12f, 0.52f, 0.45f, 0.94f), smoothRectSprite);
            SetRect(dayBadge.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(304f, 70f));
            Text dayText = CreatePaperText("DayLabelText", dayBadge.transform, paperFont, "1일차 영업 중", 40, FontStyle.Normal, new Color(0.99f, 0.98f, 0.94f, 1f), TextAnchor.MiddleCenter);
            StretchFull(dayText.rectTransform);

            Button cmdBtn = CreatePaperButton("OpenCommandOverlayBtn", topContent, paperFont, smoothRectSprite, "[Tab] 텍스트 지시", 36, paperBtnBg, inkPrimary, inkRule, FontStyle.Normal, borderInset: 5f);
            SetRect(cmdBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(328f, 70f));

            CreatePaperRule("TopNoteRule", topContent, smoothRectSprite, -90f, 6f, inkRule);

            Text timerText = CreatePaperText("TimerText", topContent, paperFont, "08:00 (낮)  ·  남은 02:00", 50, FontStyle.Normal, inkPrimary, TextAnchor.MiddleCenter);
            SetRect(timerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(0f, 84f));

            Text balanceText = CreatePaperText("BalanceText", topContent, paperFont, "현재 50,000원  /  목표 80,000원", 44, FontStyle.Normal, inkAccent, TextAnchor.MiddleCenter);
            SetRect(balanceText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -228f), new Vector2(0f, 78f));

            // 2. Center subtle crosshair dot for 1st-person store view
            GameObject crosshair = CreatePanel("CrosshairDot", hudObj.transform, new Color(1f, 1f, 1f, 0.65f), whiteSprite);
            crosshair.GetComponent<Image>().raycastTarget = false;
            SetRect(crosshair.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));

            // 3. 우측 상단 포스트잇 (2x 슈퍼샘플링 + 여유 있는 좌우 여백으로 우측 잘림 방지)
            CreatePostItNoteCard(
                "EmployeeStatusCard",
                hudObj.transform,
                smoothRectSprite,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -14f),
                new Vector2(496f, 380f),
                tiltDegrees: -1.8f,
                tintColor: new Color(1f, 0.99f, 0.96f, 1f),
                out Transform empContent,
                topPad: 58f,
                bottomPad: 38f,
                sidePad: 48f,
                contentSupersample: 2f);

            Text empHeader = CreatePaperText("EmployeeHeaderText", empContent, paperFont, "김민수  ·  성격: 성실함", 40, FontStyle.Normal, inkPrimary, TextAnchor.UpperLeft);
            empHeader.lineSpacing = 1.15f;
            SetRect(empHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 102f));

            Text roleText = CreatePaperText("CurrentRoleText", empContent, paperFont, "현재 업무: 기본 대기 (IDLE)", 36, FontStyle.Normal, inkSecondary, TextAnchor.MiddleLeft);
            SetRect(roleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -110f), new Vector2(548f, 60f));

            GameObject moveBadgeObj = CreateFlatRect("MoveBadge", empContent, new Color(0.26f, 0.33f, 0.42f, 0.94f), smoothRectSprite);
            SetRect(moveBadgeObj.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(216f, 60f));
            Text moveBadgeTxt = CreatePaperText("MoveBadgeText", moveBadgeObj.transform, paperFont, "위치 대기", 34, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            StretchFull(moveBadgeTxt.rectTransform);

            CreatePaperRule("EmpNoteRule", empContent, smoothRectSprite, -184f, 6f, inkRule);

            Image stressFill;
            Text stressVal;
            BuildStatRow(empContent, paperFont, smoothRectSprite, "스트레스", -216f, new Color(0.18f, 0.68f, 0.44f, 1f), "25 (안정)", out stressFill, out stressVal);

            Image fatigueFill;
            Text fatigueVal;
            BuildStatRow(empContent, paperFont, smoothRectSprite, "피로도", -312f, new Color(0.88f, 0.50f, 0.16f, 1f), "15", out fatigueFill, out fatigueVal);

            Image loyaltyFill;
            Text loyaltyVal;
            BuildStatRow(empContent, paperFont, smoothRectSprite, "충성도", -408f, new Color(0.20f, 0.52f, 0.86f, 1f), "70", out loyaltyFill, out loyaltyVal);

            // 4. 하단 중앙 음성 인식 · 안내 도크 (수평 다크 HUD + 2x 텍스트 슈퍼샘플링으로 작은 창에서도 선명하게)
            GameObject voiceDock = CreatePanel("VoiceFeedbackDock", hudObj.transform, new Color(0.07f, 0.10f, 0.16f, 0.90f), whiteSprite);
            SetRect(voiceDock.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(980f, 136f));

            GameObject voiceDockContent = new GameObject("VoiceDockContent", typeof(RectTransform));
            voiceDockContent.transform.SetParent(voiceDock.transform, false);
            RectTransform voiceContentRt = voiceDockContent.GetComponent<RectTransform>();
            voiceContentRt.anchorMin = new Vector2(0.5f, 0.5f);
            voiceContentRt.anchorMax = new Vector2(0.5f, 0.5f);
            voiceContentRt.pivot = new Vector2(0.5f, 0.5f);
            voiceContentRt.anchoredPosition = Vector2.zero;
            voiceContentRt.sizeDelta = new Vector2(1960f, 272f);
            voiceContentRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            GameObject stageBadge = CreatePanel("VoiceStageBadge", voiceDockContent.transform, new Color(0.24f, 0.29f, 0.36f, 1f), whiteSprite);
            stageBadge.GetComponent<Image>().pixelsPerUnitMultiplier = 1.1f;
            SetRect(stageBadge.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -28f), new Vector2(464f, 76f));
            Text stageTxt = CreatePaperText("VoiceStageText", stageBadge.transform, uiFont, "[R/V] 마이크 대기 중", 36, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            StretchFull(stageTxt.rectTransform);

            Text transcriptTxt = CreatePaperText("TranscriptText", voiceDockContent.transform, uiFont, "인식 문장: -", 38, FontStyle.Normal, new Color(0.92f, 0.95f, 0.99f, 1f), TextAnchor.MiddleLeft);
            SetRect(transcriptTxt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(528f, -28f), new Vector2(-564f, 76f));

            GameObject bannerObj = CreatePanel("FeedbackBanner", voiceDockContent.transform, new Color(0.15f, 0.21f, 0.30f, 0.96f), whiteSprite);
            bannerObj.GetComponent<Image>().pixelsPerUnitMultiplier = 1.1f;
            SetRect(bannerObj.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(-72f, 124f));
            Text bannerTxt = CreatePaperText("FeedbackBannerText", bannerObj.transform, uiFont, "[안내]  [R] 키(누른 채 말하기) 또는 [V] 키로 음성 지시하거나 [Tab] 키로 텍스트 명령창을 여세요.", 38, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            SetRect(bannerTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-56f, -16f));

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
            Color inkPrimary = new Color(0.14f, 0.13f, 0.11f, 1f);
            Text lbl = CreatePaperText(label + "_Label", parent, font, label, 40, FontStyle.Normal, inkPrimary, TextAnchor.MiddleLeft);
            SetRect(lbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, topOffset), new Vector2(158f, 58f));

            GameObject track = CreateFlatRect(label + "_Track", parent, new Color(0.24f, 0.20f, 0.16f, 0.22f), sprite);
            SetRect(track.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(168f, topOffset - 14f), new Vector2(380f, 30f));

            GameObject fillObj = CreateFlatRect(label + "_Fill", track.transform, barColor, sprite);
            StretchFull(fillObj.GetComponent<RectTransform>());
            fillImg = fillObj.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0.25f;

            valTxt = CreatePaperText(label + "_Value", parent, font, defaultVal, 38, FontStyle.Normal, inkPrimary, TextAnchor.MiddleRight);
            SetRect(valTxt.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, topOffset), new Vector2(236f, 58f));
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

        private static SettlementPanelPresenter BuildSettlementPanel(
            Transform parent,
            Font uiFont,
            Font paperFont,
            Sprite sprite)
        {
            // 배경은 투명(alpha 0)으로 두고 전용 검은 3D 포스기 카메라(PosSettlementCamera)가 뒤에서 보이도록 구성
            GameObject rootBg = CreateFlatRect("SettlementPanel", parent, new Color(0.02f, 0.025f, 0.04f, 0f), sprite);
            rootBg.GetComponent<Image>().raycastTarget = false;
            StretchFull(rootBg.GetComponent<RectTransform>());
            SettlementPanelPresenter presenter = rootBg.AddComponent<SettlementPanelPresenter>();

            Sprite smoothSprite = _smoothRectSprite != null ? _smoothRectSprite : sprite;

            // 1. 상단 중앙 타이틀: [ 1일차 결과 및 정산 ]
            GameObject titleHolder = new GameObject("SettlementTitleHolder", typeof(RectTransform));
            titleHolder.transform.SetParent(rootBg.transform, false);
            RectTransform titleHolderRt = titleHolder.GetComponent<RectTransform>();
            SetRect(titleHolderRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(1800f, 140f));
            titleHolderRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            Text screenTitle = CreatePaperText("SettlementScreenTitle", titleHolder.transform, uiFont, "[ 1일차 결과 및 정산 ]", 76, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            StretchFull(screenTitle.rectTransform);

            // 2. 1단계: 중앙 3D 포스기 클릭 영역 + 화살표 안내 텍스트 (클릭 시 영수증 출력)
            GameObject posHitObj = new GameObject("PosClickAreaButton", typeof(RectTransform), typeof(Image));
            posHitObj.transform.SetParent(rootBg.transform, false);
            SetRect(posHitObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(760f, 660f));
            Image hitImg = posHitObj.GetComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0f);
            hitImg.raycastTarget = true;
            Button posClickBtn = posHitObj.AddComponent<Button>();
            posClickBtn.transition = Selectable.Transition.None;

            GameObject promptRoot = new GameObject("PosClickPrompt", typeof(RectTransform));
            promptRoot.transform.SetParent(rootBg.transform, false);
            RectTransform promptRt = promptRoot.GetComponent<RectTransform>();
            SetRect(promptRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(235f, 40f), new Vector2(960f, 220f));
            promptRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            Text promptTxt = CreatePaperText(
                "PosClickPromptText",
                promptRoot.transform,
                paperFont,
                "←  [ 포스기 클릭 ]\n      영수증 출력하기",
                64,
                FontStyle.Normal,
                new Color(1f, 0.94f, 0.66f, 1f),
                TextAnchor.MiddleLeft);
            promptTxt.lineSpacing = 1.25f;
            StretchFull(promptTxt.rectTransform);

            // 3. 2단계: 포스기 → 정산 내역 점선 화살표
            GameObject dashedArrow = new GameObject("DashedArrowIndicator", typeof(RectTransform));
            dashedArrow.transform.SetParent(rootBg.transform, false);
            RectTransform arrowRt = dashedArrow.GetComponent<RectTransform>();
            SetRect(arrowRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-58f, 48f), new Vector2(500f, 120f));
            arrowRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            Text arrowTxt = CreatePaperText("DashedArrowText", dashedArrow.transform, uiFont, "- - - - - ▸", 62, FontStyle.Normal, new Color(0.92f, 0.95f, 1f, 0.88f), TextAnchor.MiddleCenter);
            StretchFull(arrowTxt.rectTransform);
            dashedArrow.SetActive(false);

            // 4. 2단계: 우측 [ 정산 내역 ] 영수증 시트 + [ 다음 → ] 버튼 (와이어프레임 1:1 반영)
            GameObject receiptRoot = new GameObject("ReceiptSheetContainer", typeof(RectTransform), typeof(CanvasGroup));
            receiptRoot.transform.SetParent(rootBg.transform, false);
            SetRect(receiptRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400f, -26f), new Vector2(740f, 820f));
            CanvasGroup receiptCg = receiptRoot.GetComponent<CanvasGroup>();
            receiptCg.alpha = 0f;

            // 2x 슈퍼샘플링 콘텐츠 컨테이너
            GameObject receiptInner = new GameObject("ReceiptInner2x", typeof(RectTransform));
            receiptInner.transform.SetParent(receiptRoot.transform, false);
            RectTransform innerRt = receiptInner.GetComponent<RectTransform>();
            innerRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerRt.anchorMax = new Vector2(0.5f, 0.5f);
            innerRt.pivot = new Vector2(0.5f, 0.5f);
            innerRt.sizeDelta = new Vector2(1480f, 1640f);
            innerRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            Text receiptHeader = CreatePaperText("ReceiptHeaderTitle", receiptInner.transform, uiFont, "[ 정산 내역 ]", 54, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            SetRect(receiptHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, 0f), new Vector2(-40f, 80f));

            // 영수증 종이 카드
            GameObject paperBorder = CreateFlatRect("ReceiptPaperBorder", receiptInner.transform, new Color(0.86f, 0.84f, 0.78f, 0.96f), smoothSprite);
            SetRect(paperBorder.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(-20f, 1280f));

            GameObject paperCard = CreateFlatRect("ReceiptPaperCard", paperBorder.transform, new Color(0.98f, 0.97f, 0.93f, 0.98f), smoothSprite);
            SetRect(paperCard.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));

            Color inkPrimary = new Color(0.12f, 0.12f, 0.11f, 1f);
            Color inkAccent = new Color(0.10f, 0.45f, 0.36f, 1f);
            Color inkRule = new Color(0.20f, 0.20f, 0.18f, 0.32f);

            Text goalTxt = CreatePaperText("GoalValueText", paperCard.transform, paperFont, "목표 :  80,000원", 62, FontStyle.Normal, inkPrimary, TextAnchor.MiddleLeft);
            SetRect(goalTxt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(-96f, 96f));

            CreatePaperRule("GoalRule", paperCard.transform, smoothSprite, -156f, 5f, inkRule);

            Text currentTxt = CreatePaperText("CurrentValueText", paperCard.transform, paperFont, "현재 :  50,000원  (순이익 +0원)", 58, FontStyle.Normal, inkAccent, TextAnchor.MiddleLeft);
            SetRect(currentTxt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(-96f, 96f));

            CreatePaperRule("CurrentRule", paperCard.transform, smoothSprite, -294f, 5f, inkRule);

            // 클릭 가능한 [ 추가정보 ] 토글 버튼 (클릭 시 품목별 판매·재고 상세 내역 출력)
            GameObject extraBtnObj = CreateFlatRect("ExtraInfoToggleButton", paperCard.transform, new Color(0.93f, 0.91f, 0.84f, 0.95f), smoothSprite);
            SetRect(extraBtnObj.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -324f), new Vector2(-84f, 146f));
            Button extraToggleBtn = extraBtnObj.AddComponent<Button>();
            ColorBlock extraCb = extraToggleBtn.colors;
            extraCb.normalColor = Color.white;
            extraCb.highlightedColor = new Color(0.95f, 0.93f, 0.86f, 1f);
            extraCb.pressedColor = new Color(0.88f, 0.85f, 0.78f, 1f);
            extraToggleBtn.colors = extraCb;

            Text extraSummaryTxt = CreatePaperText(
                "ExtraInfoSummaryText",
                extraBtnObj.transform,
                uiFont,
                "추가정보 :  당일 매출 +0원\n↳  클릭 시 품목별 판매·재고 정산내역 출력  ▾",
                38,
                FontStyle.Normal,
                inkPrimary,
                TextAnchor.MiddleLeft);
            extraSummaryTxt.lineSpacing = 1.24f;
            SetRect(extraSummaryTxt.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-48f, -16f));

            // 클릭 시 펼쳐지는 상세 품목별 판매·재고 영수증 내역 박스
            GameObject detailBox = CreateFlatRect("ExtraInfoDetailBox", paperCard.transform, new Color(0.94f, 0.93f, 0.88f, 0.96f), smoothSprite);
            SetRect(detailBox.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -492f), new Vector2(-84f, 736f));

            Text detailBodyTxt = CreatePaperText("ExtraInfoDetailBodyText", detailBox.transform, uiFont, "상세 정산 내역 준비 중...", 32, FontStyle.Normal, inkPrimary, TextAnchor.UpperLeft);
            detailBodyTxt.lineSpacing = 1.28f;
            SetRect(detailBodyTxt.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-48f, -40f));
            detailBox.SetActive(false);

            // 하단 [ 다음 → ] 메인 버튼 (1일차 종료 시 2일차로 진행!)
            Button nextDayBtn = CreatePaperButton(
                "NextDayButton",
                receiptInner.transform,
                uiFont,
                smoothSprite,
                "다음 (2일차 영업)  →",
                46,
                new Color(0.08f, 0.58f, 0.49f, 1f),
                Color.white,
                new Color(0.75f, 0.95f, 0.90f, 0.45f),
                FontStyle.Normal,
                borderInset: 6f);
            SetRect(nextDayBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(-20f, 134f));
            Text nextDayLabel = nextDayBtn.transform.Find("Fill/Label").GetComponent<Text>();

            // 보조 링크 2종 (엔딩 미리보기 / 메인 로비)
            Button viewEndingBtn = CreateTransparentTextButton("ViewEndingButton", receiptInner.transform, uiFont, "엔딩 결과 바로보기", 34, new Color(0.76f, 0.82f, 0.90f, 0.88f), new Color(1f, 0.82f, 0.42f, 1f), TextAnchor.MiddleLeft);
            SetRect(viewEndingBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 10f), new Vector2(460f, 66f));

            Button returnLobbyBtn = CreateTransparentTextButton("ReturnLobbyButton", receiptInner.transform, uiFont, "메인 로비로 이동", 34, new Color(0.76f, 0.82f, 0.90f, 0.88f), new Color(1f, 0.62f, 0.62f, 1f), TextAnchor.MiddleRight);
            SetRect(returnLobbyBtn.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 10f), new Vector2(460f, 66f));

            receiptRoot.SetActive(false);

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("panelRoot").objectReferenceValue = rootBg;
            so.FindProperty("screenTitleText").objectReferenceValue = screenTitle;
            so.FindProperty("posClickButton").objectReferenceValue = posClickBtn;
            so.FindProperty("posPromptRoot").objectReferenceValue = promptRoot;
            so.FindProperty("posPromptArrowRect").objectReferenceValue = promptRt;
            so.FindProperty("dashedArrowRoot").objectReferenceValue = dashedArrow;
            so.FindProperty("receiptSheetRoot").objectReferenceValue = receiptRoot;
            so.FindProperty("receiptCanvasGroup").objectReferenceValue = receiptCg;
            so.FindProperty("goalValueText").objectReferenceValue = goalTxt;
            so.FindProperty("currentValueText").objectReferenceValue = currentTxt;
            so.FindProperty("extraInfoToggleButton").objectReferenceValue = extraToggleBtn;
            so.FindProperty("extraInfoSummaryText").objectReferenceValue = extraSummaryTxt;
            so.FindProperty("extraInfoDetailBox").objectReferenceValue = detailBox;
            so.FindProperty("extraInfoDetailBodyText").objectReferenceValue = detailBodyTxt;
            so.FindProperty("nextDayButton").objectReferenceValue = nextDayBtn;
            so.FindProperty("nextDayButtonLabel").objectReferenceValue = nextDayLabel;
            so.FindProperty("viewEndingButton").objectReferenceValue = viewEndingBtn;
            so.FindProperty("returnLobbyButton").objectReferenceValue = returnLobbyBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static EndingPanelPresenter BuildEndingPanel(Transform parent, Font font, Sprite sprite)
        {
            GameObject dimBg = CreatePanel("EndingPanel", parent, new Color(0.03f, 0.05f, 0.10f, 0.96f), sprite);
            StretchFull(dimBg.GetComponent<RectTransform>());
            EndingPanelPresenter presenter = dimBg.AddComponent<EndingPanelPresenter>();

            GameObject card = CreatePanel("EndingCard", dimBg.transform, new Color(0.10f, 0.14f, 0.21f, 0.99f), sprite);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 680f));

            // 1. 상단 엔딩 구분 배너
            GameObject headerBanner = CreatePanel("EndingHeaderBanner", card.transform, new Color(0.10f, 0.56f, 0.40f, 1f), sprite);
            SetRect(headerBanner.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 102f));

            Text headerTitle = CreateText(
                "EndingHeaderTitleText",
                headerBanner.transform,
                font,
                "노말엔딩 1  ·  성공 엔딩 (7일차 완수 + 알바 스트레스 완화)",
                27,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            SetRect(headerTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-40f, 40f));

            Text conditionSummary = CreateText(
                "EndingConditionSummaryText",
                headerBanner.transform,
                font,
                "달성 조건 : 7일차 목표 금액 완수  +  알바 스트레스 지수 완화 (60 미만 유지)",
                16,
                FontStyle.Normal,
                new Color(0.92f, 0.97f, 0.95f, 1f),
                TextAnchor.MiddleCenter);
            SetRect(conditionSummary.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-40f, 30f));

            // 2. 중앙 상단 컷신 플레이스홀더 영역 (추후 2D 애니메이션 / 3D 녹화 영상 삽입 영역)
            GameObject cutsceneBox = CreatePanel("CutscenePlaceholderBox", card.transform, new Color(0.06f, 0.08f, 0.13f, 1f), sprite);
            SetRect(cutsceneBox.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(888f, 152f));

            Text cutsceneText = CreateText(
                "CutscenePlaceholderText",
                cutsceneBox.transform,
                font,
                "[ 컷신 슬롯 : 노말엔딩 1 (성공 · 스트레스 완화) ]\n추후 2D 애니메이션 또는 3D 동작 녹화 영상이 재생될 영역입니다.",
                19,
                FontStyle.Bold,
                new Color(0.68f, 0.78f, 0.90f, 1f),
                TextAnchor.MiddleCenter);
            cutsceneText.lineSpacing = 1.35f;
            SetRect(cutsceneText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-36f, -20f));

            // 3. 중앙 하단 텍스트 출력 박스 (알바 성격 + 스트레스 지수 + 결말 텍스트)
            GameObject narrativeBox = CreatePanel("EndingNarrativeBox", card.transform, new Color(0.07f, 0.10f, 0.16f, 1f), sprite);
            SetRect(narrativeBox.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -284f), new Vector2(888f, 224f));

            Text personaSummaryTxt = CreateText(
                "PersonaSummaryText",
                narrativeBox.transform,
                font,
                "알바: 김민수 (성실·모범형)  |  성격: \"성실하고 예의 바르지만 반복 재촉에는 스트레스를 받는다.\"\n최종 스트레스 지수: 28 / 100  |  최종 잔액: 92,000원 (목표: 80,000원)",
                17,
                FontStyle.Bold,
                new Color(0.98f, 0.84f, 0.34f, 1f),
                TextAnchor.MiddleLeft);
            personaSummaryTxt.lineSpacing = 1.25f;
            SetRect(personaSummaryTxt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-44f, 56f));

            Text narrativeBodyTxt = CreateText(
                "EndingNarrativeBodyText",
                narrativeBox.transform,
                font,
                "엔딩 텍스트 로딩 중...",
                19,
                FontStyle.Normal,
                new Color(0.92f, 0.96f, 1f, 1f),
                TextAnchor.MiddleLeft);
            narrativeBodyTxt.lineSpacing = 1.38f;
            SetRect(narrativeBodyTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(-44f, -72f));

            // 4. 데모용 4대 엔딩 즉시 전환 버튼 줄 (현재 알바 성격 그대로 4가지 엔딩 텍스트 확인)
            Button prevNormal1Btn = CreateButton("PreviewNormal1Button", card.transform, font, sprite, "노말1: 완수+완화", 16, new Color(0.10f, 0.50f, 0.36f, 1f), Color.white);
            SetRect(prevNormal1Btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-336f, 98f), new Vector2(212f, 44f));

            Button prevNormal2Btn = CreateButton("PreviewNormal2Button", card.transform, font, sprite, "노말2: 완수+악화", 16, new Color(0.68f, 0.40f, 0.12f, 1f), Color.white);
            SetRect(prevNormal2Btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-112f, 98f), new Vector2(212f, 44f));

            Button prevBad1Btn = CreateButton("PreviewBad1Button", card.transform, font, sprite, "실패1: 금액 미달", 16, new Color(0.52f, 0.22f, 0.28f, 1f), Color.white);
            SetRect(prevBad1Btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(112f, 98f), new Vector2(212f, 44f));

            Button prevBad2Btn = CreateButton("PreviewBad2Button", card.transform, font, sprite, "실패2: 스트레스100", 16, new Color(0.68f, 0.14f, 0.16f, 1f), Color.white);
            SetRect(prevBad2Btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(336f, 98f), new Vector2(212f, 44f));

            // 5. 하단 다시 시작 / 메인 로비 복귀 버튼
            Button retryGameBtn = CreateButton("RetryGameButton", card.transform, font, sprite, "다시 시작", 20, new Color(0.06f, 0.65f, 0.57f, 1f), Color.white);
            SetRect(retryGameBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-224f, 24f), new Vector2(420f, 54f));

            Button lobbyBtn = CreateButton("ReturnLobbyButton", card.transform, font, sprite, "메인 로비로 복귀", 20, new Color(0.24f, 0.30f, 0.39f, 1f), Color.white);
            SetRect(lobbyBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(224f, 24f), new Vector2(420f, 54f));

            SerializedObject endingSo = new SerializedObject(presenter);
            endingSo.FindProperty("_panelRoot").objectReferenceValue = dimBg;
            endingSo.FindProperty("_headerBanner").objectReferenceValue = headerBanner.GetComponent<Image>();
            endingSo.FindProperty("_headerTitleText").objectReferenceValue = headerTitle;
            endingSo.FindProperty("_conditionSummaryText").objectReferenceValue = conditionSummary;
            endingSo.FindProperty("_cutscenePlaceholderText").objectReferenceValue = cutsceneText;
            endingSo.FindProperty("_personaSummaryText").objectReferenceValue = personaSummaryTxt;
            endingSo.FindProperty("_narrativeBodyText").objectReferenceValue = narrativeBodyTxt;
            endingSo.FindProperty("_previewNormal1Button").objectReferenceValue = prevNormal1Btn;
            endingSo.FindProperty("_previewNormal2Button").objectReferenceValue = prevNormal2Btn;
            endingSo.FindProperty("_previewBad1Button").objectReferenceValue = prevBad1Btn;
            endingSo.FindProperty("_previewBad2Button").objectReferenceValue = prevBad2Btn;
            endingSo.FindProperty("_retryGameButton").objectReferenceValue = retryGameBtn;
            endingSo.FindProperty("_returnLobbyButton").objectReferenceValue = lobbyBtn;
            endingSo.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static PausePanelPresenter BuildPausePanel(Transform parent, Font font, Sprite sprite)
        {
            // 게임 진행 화면을 그대로 두고 어둡게 덮는 전체 화면 딤 오버레이 (중앙 카드 없음)
            GameObject dimBg = CreatePanel("PausePanel", parent, new Color(0.02f, 0.03f, 0.06f, 0.66f), sprite);
            StretchFull(dimBg.GetComponent<RectTransform>());
            PausePanelPresenter presenter = dimBg.AddComponent<PausePanelPresenter>();

            // 1. 좌측 상단: 첫 화면(로비) 제목과 완전히 동일한 위치·크기·스타일의 '일시정지' 타이틀
            GameObject titleGroup = new GameObject("TitleGroup", typeof(RectTransform));
            titleGroup.transform.SetParent(dimBg.transform, false);
            SetRect(titleGroup.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(88f, -92f), new Vector2(960f, 280f));

            Text title = CreateText(
                "PauseTitle",
                titleGroup.transform,
                font,
                "일시정지",
                84,
                FontStyle.Bold,
                new Color(0.98f, 0.99f, 1f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -56f), new Vector2(0f, 114f));
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.72f);
            titleOutline.effectDistance = new Vector2(1.8f, -1.8f);

            // 2. 좌측 하단: 첫 화면(로비) 버튼 3종과 완전히 동일한 위치·크기·투명 호버 스타일
            Button resumeBtn = CreateTransparentTextButton(
                "ResumeButton",
                dimBg.transform,
                font,
                "계속하기",
                38,
                Color.white,
                new Color(0.24f, 0.96f, 0.84f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(resumeBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 264f), new Vector2(320f, 72f));

            Button settingsBtn = CreateTransparentTextButton(
                "OpenSettingsButton",
                dimBg.transform,
                font,
                "설정",
                36,
                new Color(0.92f, 0.96f, 1f, 1f),
                new Color(0.24f, 0.96f, 0.84f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(settingsBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 180f), new Vector2(320f, 72f));

            Button lobbyBtn = CreateTransparentTextButton(
                "ReturnLobbyButton",
                dimBg.transform,
                font,
                "종료",
                36,
                new Color(0.86f, 0.90f, 0.96f, 1f),
                new Color(1f, 0.62f, 0.62f, 1f),
                TextAnchor.MiddleLeft);
            SetRect(lobbyBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 96f), new Vector2(320f, 72f));

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
            BuildSliderRow(
                card.transform,
                font,
                sprite,
                "마우스 감도",
                -262f,
                SettingsPanelPresenter.MinMouseSensitivity,
                SettingsPanelPresenter.MaxMouseSensitivity,
                1.0f,
                "1.00x",
                out sensSlider,
                out sensValue);

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
            SetRect(handle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 32f));

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

        private static void BuildWorkScene(
            GameObject savedUiPrefab,
            GameObject savedWorldUiPrefab,
            Font uiFont,
            Font paperFont,
            Sprite whiteSprite)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.0f;
            dirLight.shadows = LightShadows.Soft;
            lightObj.transform.eulerAngles = new Vector3(45f, -35f, 0f);
            RenderSettings.sun = dirLight;

            const string skyPath = "Assets/_Project/HaJongUk/Assets/TimeTestSky.mat";
            Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (skyMat != null)
            {
                RenderSettings.skybox = skyMat;
            }
            else
            {
                Shader procSkyShader = Shader.Find("Skybox/Procedural");
                if (procSkyShader != null)
                {
                    RenderSettings.skybox = new Material(procSkyShader);
                }
            }

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // 1. 한국형 편의점 3D 맵 복사 (없으면 기본 바닥으로 폴백)
            bool hasStoreMap = TryImportConvenienceStoreMap(scene, out GameObject storeMapRoot, out Dictionary<string, int> spawnedItemCounts);
            GameObject stageRoot = new GameObject("Demo_StoreStage");

            Transform counterTarget;
            Transform shelfTarget;
            Transform restTarget;
            Camera cam = null;
            PlayerRigController rigCtrl = null;
            Renderer playerVisRenderer = null;
            Component cursorLockAdapter = null;

            if (hasStoreMap)
            {
                // 1-A. 플레이어 1인칭 캡슐 (계산대 안쪽 스폰 — WASD 이동 + 마우스 시점 + 왼쪽 통로로 매장 자유 이동)
                GameObject playerRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPrefabPath);
                if (playerRigPrefab != null)
                {
                    GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerRigPrefab);
                    playerInstance.name = "PlayerRig";
                    playerInstance.transform.position = new Vector3(-3.55f, 0f, -3.08f);
                    playerInstance.transform.rotation = Quaternion.Euler(0f, 18f, 0f);

                    GameObject playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    playerVisual.name = "PlayerVisualCapsule";
                    playerVisual.transform.SetParent(playerInstance.transform, false);
                    playerVisual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    playerVisual.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                    Collider playerVisCol = playerVisual.GetComponent<Collider>();
                    if (playerVisCol != null) Object.DestroyImmediate(playerVisCol);

                    Material playerMat = AssetDatabase.LoadAssetAtPath<Material>(PlayerVisualMatPath);
                    playerVisRenderer = playerVisual.GetComponent<Renderer>();
                    if (playerMat != null && playerVisRenderer != null)
                    {
                        playerVisRenderer.sharedMaterial = playerMat;
                    }

                    rigCtrl = playerInstance.GetComponent<PlayerRigController>();
                    if (rigCtrl != null && playerVisRenderer != null)
                    {
                        SerializedObject rigSo = new SerializedObject(rigCtrl);
                        SerializedProperty visualsProp = rigSo.FindProperty("localVisuals");
                        if (visualsProp != null)
                        {
                            visualsProp.arraySize = 1;
                            visualsProp.GetArrayElementAtIndex(0).objectReferenceValue = playerVisRenderer;
                            rigSo.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }

                    cam = playerInstance.GetComponentInChildren<Camera>(true);
                    if (cam != null)
                    {
                        cam.clearFlags = CameraClearFlags.Skybox;
                        cam.backgroundColor = new Color(0.14f, 0.18f, 0.24f, 1f);
                        cam.fieldOfView = 70f;
                        cam.farClipPlane = 1000f;
                    }

                    Type adapterType = Type.GetType("PlayerUiCursorLockAdapter, Assembly-CSharp");
                    if (adapterType != null)
                    {
                        cursorLockAdapter = playerInstance.AddComponent(adapterType);
                    }
                }

                counterTarget = CreateEmptyWaypoint(stageRoot.transform, "Target_Counter", new Vector3(-3.80f, 0f, -1.55f));
                shelfTarget = CreateEmptyWaypoint(stageRoot.transform, "Target_Shelf", new Vector3(-0.90f, 0f, 0.65f));
                restTarget = CreateEmptyWaypoint(stageRoot.transform, "Target_Rest", new Vector3(-0.90f, 0f, -1.55f));
            }
            else
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "StoreFloor";
                floor.transform.SetParent(stageRoot.transform);
                floor.transform.localScale = new Vector3(1.2f, 1f, 0.9f);

                counterTarget = CreateZoneMarker(stageRoot.transform, "Target_Counter", new Vector3(-2.6f, 0f, 0.8f), new Vector3(1.5f, 0.9f, 0.8f));
                shelfTarget = CreateZoneMarker(stageRoot.transform, "Target_Shelf", new Vector3(2.6f, 0f, 1.2f), new Vector3(1.4f, 1.6f, 0.6f));
                restTarget = CreateZoneMarker(stageRoot.transform, "Target_Rest", new Vector3(0f, 0f, -0.8f), new Vector3(1.1f, 0.45f, 0.8f));
            }

            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = new Color(0.14f, 0.18f, 0.24f, 1f);
                cam.fieldOfView = 60f;
                cam.farClipPlane = 1000f;
                camObj.transform.position = new Vector3(0f, 2.0f, -6.0f);
                camObj.transform.eulerAngles = new Vector3(5f, 0f, 0f);
                camObj.AddComponent<AudioListener>();
            }

            // 2. 알바 직원 캡슐 (초록 유니폼, NavMeshAgent 탑재 — 손님(빨강)/행인(파랑)/플레이어(주황)와 시각적으로 구분)
            GameObject npcRoot = new GameObject("Demo_Employee_NPC");
            npcRoot.transform.SetParent(stageRoot.transform);
            npcRoot.transform.position = restTarget.position;

            NavMeshAgent employeeAgent = npcRoot.AddComponent<NavMeshAgent>();
            employeeAgent.radius = 0.34f;
            employeeAgent.height = 1.44f;
            employeeAgent.speed = 3.2f;
            employeeAgent.acceleration = 14f;
            employeeAgent.angularSpeed = 480f;
            employeeAgent.stoppingDistance = 0.12f;

            GameObject npcVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcVisual.name = "EmployeeVisualCapsule";
            npcVisual.transform.SetParent(npcRoot.transform, false);
            npcVisual.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            npcVisual.transform.localScale = new Vector3(0.76f, 0.72f, 0.76f);
            Collider npcCol = npcVisual.GetComponent<Collider>();
            if (npcCol != null) Object.DestroyImmediate(npcCol);

            Material employeeMat = AssetDatabase.LoadAssetAtPath<Material>(EmployeeUniformMatPath);
            if (employeeMat != null)
            {
                Renderer empRenderer = npcVisual.GetComponent<Renderer>();
                if (empRenderer != null) empRenderer.sharedMaterial = employeeMat;
            }

            GameObject worldUiInstance = (GameObject)PrefabUtility.InstantiatePrefab(savedWorldUiPrefab);
            EmployeeWorldStatusPresenter worldPresenter = worldUiInstance.GetComponent<EmployeeWorldStatusPresenter>();
            SerializedObject worldSo = new SerializedObject(worldPresenter);
            worldSo.FindProperty("followTarget").objectReferenceValue = npcRoot.transform;
            worldSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject uiInstance = (GameObject)PrefabUtility.InstantiatePrefab(savedUiPrefab);
            StoreUiSessionController uiController = uiInstance.GetComponent<StoreUiSessionController>();

            // 3. 손님(빨강) · 보행자(파랑) 캡슐 NPC 시스템 + 런타임 NavMesh 생성
            NpcPopulationController npcPopulation = hasStoreMap
                ? BuildConvenienceStoreCustomerNpcSystem(stageRoot.transform)
                : null;

            GameObject navObj = new GameObject("Environment_Navigation");
            NavMeshSurface navSurface = navObj.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.layerMask = 1;
            navSurface.overrideVoxelSize = true;
            navSurface.voxelSize = 0.05f;
            navSurface.minRegionArea = 0.2f;
            navSurface.ignoreNavMeshAgent = true;
            navSurface.ignoreNavMeshObstacle = true;

            WorkSceneNavMeshBootstrap navBootstrap = navObj.AddComponent<WorkSceneNavMeshBootstrap>();
            SerializedObject navBootSo = new SerializedObject(navBootstrap);
            navBootSo.FindProperty("uiController").objectReferenceValue = uiController;
            navBootSo.FindProperty("npcPopulation").objectReferenceValue = npcPopulation;
            navBootSo.FindProperty("demoEmployeeAgent").objectReferenceValue = employeeAgent;
            navBootSo.ApplyModifiedPropertiesWithoutUndo();

            if (hasStoreMap)
            {
                navSurface.BuildNavMesh();
            }

            // Time System & Day-Night Environment (TIME-01 연동: 1일차 낮→밤 변화, 2·3일차 비 날씨 확장 대응)
            GameObject timeRoot = new GameObject("Time System");
            timeRoot.SetActive(false);

            BusinessTimeController timeController = timeRoot.AddComponent<BusinessTimeController>();
            SerializedObject timeSo = new SerializedObject(timeController);
            timeSo.FindProperty("standaloneClock").boolValue = false;
            timeSo.FindProperty("dayDurationSeconds").floatValue = 120f;
            timeSo.FindProperty("openingHour").floatValue = 8f;
            timeSo.FindProperty("closingHour").floatValue = 32f;
            timeSo.ApplyModifiedPropertiesWithoutUndo();

            timeRoot.AddComponent<DayNightPresenter>();
            timeRoot.AddComponent<CelestialBodyPresenter>();

            RainWeatherController rainController = timeRoot.AddComponent<RainWeatherController>();
            rainController.isRain = false;

            TimeSkyExposure skyExposure = timeRoot.AddComponent<TimeSkyExposure>();
            SerializedObject skySo = new SerializedObject(skyExposure);
            if (skySo.FindProperty("cloudDegreesPerGameHour") != null)
            {
                skySo.FindProperty("cloudDegreesPerGameHour").floatValue = 3f;
                skySo.ApplyModifiedPropertiesWithoutUndo();
            }

            TimeEnvironmentController timeEnvironment = timeRoot.AddComponent<TimeEnvironmentController>();
            timeEnvironment.Configure(dirLight, cam);

            Type bridgeType = Type.GetType("UiBusinessTimeBridge, Assembly-CSharp");
            if (bridgeType != null)
            {
                Component bridgeComp = timeRoot.AddComponent(bridgeType);
                SerializedObject bridgeSo = new SerializedObject(bridgeComp);
                SerializedProperty uiProp = bridgeSo.FindProperty("ui");
                SerializedProperty timeProp = bridgeSo.FindProperty("timeSystem");
                if (uiProp != null) uiProp.objectReferenceValue = uiController;
                if (timeProp != null) timeProp.objectReferenceValue = timeController;
                bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            }

            timeRoot.SetActive(true);

            // 4. 물품별 단가·재고·판매량·총매출 원장 매니저 생성 (실물 배치 개수 자동 반영)
            GameObject invObj = new GameObject("Store_InventoryManager");
            invObj.transform.SetParent(stageRoot.transform, false);
            StoreInventoryManager inventoryManager = invObj.AddComponent<StoreInventoryManager>();
            SerializedObject invSo = new SerializedObject(inventoryManager);
            invSo.FindProperty("uiController").objectReferenceValue = uiController;
            invSo.FindProperty("npcPopulation").objectReferenceValue = npcPopulation;
            if (spawnedItemCounts != null && spawnedItemCounts.Count > 0)
            {
                SerializedProperty seedsProp = invSo.FindProperty("initialStockSeeds");
                seedsProp.arraySize = spawnedItemCounts.Count;
                int idx = 0;
                foreach (KeyValuePair<string, int> kv in spawnedItemCounts)
                {
                    SerializedProperty elem = seedsProp.GetArrayElementAtIndex(idx++);
                    elem.FindPropertyRelative("itemId").stringValue = kv.Key;
                    elem.FindPropertyRelative("initialStock").intValue = kv.Value;
                }
            }
            invSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject uiCtrlSo = new SerializedObject(uiController);
            uiCtrlSo.FindProperty("worldStatusPresenter").objectReferenceValue = worldPresenter;
            if (uiCtrlSo.FindProperty("inventoryManager") != null)
            {
                uiCtrlSo.FindProperty("inventoryManager").objectReferenceValue = inventoryManager;
            }
            if (cursorLockAdapter != null && uiCtrlSo.FindProperty("externalCursorLockGateComponent") != null)
            {
                uiCtrlSo.FindProperty("externalCursorLockGateComponent").objectReferenceValue = cursorLockAdapter;
            }
            if (uiCtrlSo.FindProperty("businessTimeController") != null)
            {
                uiCtrlSo.FindProperty("businessTimeController").objectReferenceValue = timeController;
            }
            if (uiCtrlSo.FindProperty("timeEnvironmentController") != null)
            {
                uiCtrlSo.FindProperty("timeEnvironmentController").objectReferenceValue = timeEnvironment;
            }
            uiCtrlSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. 게임 시작(로비·채용) 제3자 조망 카메라 + 설정 마우스 감도 + 일차 정산 3D 포스기 스튜디오
            if (hasStoreMap)
            {
                GameObject lobbyCamObj = new GameObject("LobbyOverviewCamera");
                Vector3 lobbyCamPos = new Vector3(3.95f, 2.35f, -1.95f);
                Vector3 lobbyLookTarget = new Vector3(-1.85f, 1.05f, 0.75f);
                lobbyCamObj.transform.position = lobbyCamPos;
                lobbyCamObj.transform.rotation = Quaternion.LookRotation(lobbyLookTarget - lobbyCamPos, Vector3.up);

                Camera lobbyCam = lobbyCamObj.AddComponent<Camera>();
                lobbyCam.clearFlags = CameraClearFlags.Skybox;
                lobbyCam.backgroundColor = new Color(0.14f, 0.18f, 0.24f, 1f);
                lobbyCam.fieldOfView = 58f;
                lobbyCam.farClipPlane = 1000f;
                lobbyCam.depth = 1f;

                WorkSceneCameraAndSensitivityController camSensCtrl = lobbyCamObj.AddComponent<WorkSceneCameraAndSensitivityController>();
                SerializedObject camSensSo = new SerializedObject(camSensCtrl);
                camSensSo.FindProperty("uiController").objectReferenceValue = uiController;
                camSensSo.FindProperty("playerRig").objectReferenceValue = rigCtrl;
                camSensSo.FindProperty("playerFirstPersonCamera").objectReferenceValue = cam;
                camSensSo.FindProperty("lobbyOverviewCamera").objectReferenceValue = lobbyCam;
                camSensSo.FindProperty("playerVisualRenderer").objectReferenceValue = playerVisRenderer;
                camSensSo.FindProperty("timeEnvironment").objectReferenceValue = timeEnvironment;
                camSensSo.FindProperty("sunLight").objectReferenceValue = dirLight;
                camSensSo.ApplyModifiedPropertiesWithoutUndo();

                BuildCounterManualInteractable(stageRoot.transform, uiController, cam, uiFont, paperFont, whiteSprite);
                BuildPosSettlementStage(storeMapRoot, uiController);
            }

            // Demo driver + non-intrusive bottom-right shortcut guide (UI-06, kept outside PF_StoreUIRoot prefab!)
            GameObject demoObj = new GameObject("UI_PrototypeDemo");
            UiPrototypeDemoDriver driver = demoObj.AddComponent<UiPrototypeDemoDriver>();
            SerializedObject driverSo = new SerializedObject(driver);
            driverSo.FindProperty("uiController").objectReferenceValue = uiController;
            driverSo.FindProperty("demoEmployeeTransform").objectReferenceValue = npcRoot.transform;
            driverSo.FindProperty("counterTarget").objectReferenceValue = counterTarget;
            driverSo.FindProperty("shelfTarget").objectReferenceValue = shelfTarget;
            driverSo.FindProperty("restTarget").objectReferenceValue = restTarget;
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            Canvas demoHintCanvas = demoObj.AddComponent<Canvas>();
            demoHintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            demoHintCanvas.sortingOrder = 49;
            CanvasScaler hintScaler = demoObj.AddComponent<CanvasScaler>();
            hintScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hintScaler.referenceResolution = new Vector2(1920f, 1080f);
            hintScaler.matchWidthOrHeight = 0.5f;

            GameObject hintBox = CreatePostItNoteCard(
                "DemoShortcutGuide",
                demoObj.transform,
                _smoothRectSprite != null ? _smoothRectSprite : whiteSprite,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-18f, 18f),
                new Vector2(426f, 286f),
                tiltDegrees: -1.8f,
                tintColor: new Color(1f, 0.99f, 0.96f, 1f),
                out Transform hintContent,
                topPad: 56f,
                bottomPad: 38f,
                sidePad: 46f,
                contentSupersample: 2f);
            hintBox.GetComponent<Image>().raycastTarget = false;

            Text hintTxt = CreatePaperText(
                "ShortcutText",
                hintContent,
                paperFont,
                "[조작 가이드 · 시연 단축키]\n" +
                "• WASD : 이동 · E : 계산대 설명서\n" +
                "• R(홀드)/V : 음성 지시 · Tab : 텍스트창\n" +
                "• P / Esc : 일시정지 · F1 : 음성 명령 시뮬\n" +
                "• F2~F4 : 오인식/분노/이동오류 · F5 : 스트레스\n" +
                "• F6 : 정산 · F7 : +4시간 · F8 : 비 날씨",
                32,
                FontStyle.Normal,
                new Color(0.16f, 0.14f, 0.12f, 1f),
                TextAnchor.UpperLeft);
            hintTxt.lineSpacing = 1.28f;
            hintTxt.raycastTarget = false;
            StretchFull(hintTxt.rectTransform);

            EditorSceneManager.SaveScene(scene, WorkScenePath);
        }

        private static void BuildCounterManualInteractable(
            Transform stageRoot,
            StoreUiSessionController uiController,
            Camera playerCamera,
            Font uiFont,
            Font paperFont,
            Sprite whiteSprite)
        {
            // 1. 계산대 상판(y = 1.04) 위에 올려둔 작은 설명서 큐브
            GameObject manualCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            manualCube.name = "Counter_ManualCube";
            manualCube.transform.SetParent(stageRoot, false);
            manualCube.transform.position = new Vector3(-3.36f, 1.11f, -2.42f);
            manualCube.transform.localScale = new Vector3(0.28f, 0.14f, 0.22f);
            manualCube.transform.localEulerAngles = new Vector3(0f, -14f, 0f);

            Material coverMat = AssetDatabase.LoadAssetAtPath<Material>(ManualCubeMatPath);
            if (coverMat != null)
            {
                Renderer r = manualCube.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = coverMat;
            }

            // 책등(바인더) 디테일 장식 (충돌체 제거)
            GameObject spine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spine.name = "ManualSpineAccent";
            spine.transform.SetParent(manualCube.transform, false);
            spine.transform.localPosition = new Vector3(-0.46f, 0.04f, 0f);
            spine.transform.localScale = new Vector3(0.12f, 1.04f, 1.02f);
            Collider spineCol = spine.GetComponent<Collider>();
            if (spineCol != null) Object.DestroyImmediate(spineCol);

            Material spineMat = AssetDatabase.LoadAssetAtPath<Material>(ManualSpineMatPath);
            if (spineMat != null)
            {
                Renderer sr = spine.GetComponent<Renderer>();
                if (sr != null) sr.sharedMaterial = spineMat;
            }

            // 2. 상호작용 안내 프롬프트 + 설명서 모달 UI (sortingOrder 65)
            GameObject canvasObj = new GameObject("ManualInteractCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 65;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 2-A. 조준 시 하단 중앙에 뜨는 [E] 안내 배지
            GameObject promptObj = CreatePanel("ManualPromptBadge", canvasObj.transform, new Color(0.08f, 0.12f, 0.19f, 0.92f), whiteSprite);
            promptObj.GetComponent<Image>().raycastTarget = false;
            SetRect(promptObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 178f), new Vector2(410f, 50f));

            GameObject promptInner = new GameObject("PromptContent", typeof(RectTransform));
            promptInner.transform.SetParent(promptObj.transform, false);
            RectTransform promptInnerRt = promptInner.GetComponent<RectTransform>();
            promptInnerRt.anchorMin = new Vector2(0.5f, 0.5f);
            promptInnerRt.anchorMax = new Vector2(0.5f, 0.5f);
            promptInnerRt.pivot = new Vector2(0.5f, 0.5f);
            promptInnerRt.sizeDelta = new Vector2(820f, 100f);
            promptInnerRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            Text promptTxt = CreatePaperText("PromptText", promptInner.transform, uiFont, "[E]  편의점 점장 설명서 보기", 38, FontStyle.Normal, new Color(1f, 0.95f, 0.72f, 1f), TextAnchor.MiddleCenter);
            StretchFull(promptTxt.rectTransform);
            promptObj.SetActive(false);

            // 2-B. E 키 상호작용 시 열리는 점장 운영 설명서 모달
            GameObject dimBg = CreateFlatRect("StoreManualOverlay", canvasObj.transform, new Color(0.02f, 0.04f, 0.08f, 0.68f), whiteSprite);
            StretchFull(dimBg.GetComponent<RectTransform>());

            Sprite smoothSprite = _smoothRectSprite != null ? _smoothRectSprite : whiteSprite;
            GameObject manualBorder = CreateFlatRect("ManualPaperBorder", dimBg.transform, new Color(0.32f, 0.26f, 0.20f, 0.95f), smoothSprite);
            SetRect(manualBorder.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(844f, 656f));

            GameObject manualCard = CreateFlatRect("ManualPaperCard", manualBorder.transform, new Color(0.98f, 0.96f, 0.89f, 1f), smoothSprite);
            SetRect(manualCard.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));

            // 2x 슈퍼샘플링 콘텐츠 영역
            GameObject manualContent = new GameObject("ManualContent", typeof(RectTransform));
            manualContent.transform.SetParent(manualCard.transform, false);
            RectTransform mcRt = manualContent.GetComponent<RectTransform>();
            mcRt.anchorMin = new Vector2(0.5f, 0.5f);
            mcRt.anchorMax = new Vector2(0.5f, 0.5f);
            mcRt.pivot = new Vector2(0.5f, 0.5f);
            mcRt.sizeDelta = new Vector2(1540f, 1180f);
            mcRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            Color inkPrimary = new Color(0.14f, 0.13f, 0.11f, 1f);
            Color inkAccent = new Color(0.11f, 0.44f, 0.38f, 1f);
            Color inkRule = new Color(0.24f, 0.21f, 0.17f, 0.35f);

            Text titleTxt = CreatePaperText("ManualTitle", manualContent.transform, paperFont, "편의점 점장 운영 설명서", 72, FontStyle.Normal, inkPrimary, TextAnchor.MiddleLeft);
            SetRect(titleTxt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(-340f, 92f));

            Button closeBtn = CreatePaperButton(
                "CloseManualButton",
                manualContent.transform,
                uiFont,
                smoothSprite,
                "닫기  [E / Esc]",
                36,
                new Color(0.16f, 0.44f, 0.39f, 1f),
                Color.white,
                inkRule,
                FontStyle.Normal,
                borderInset: 4f);
            SetRect(closeBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -14f), new Vector2(290f, 76f));

            CreatePaperRule("ManualHeaderRule", manualContent.transform, smoothSprite, -112f, 6f, inkRule);

            string manualBody =
                "1. 매장 이동 및 시점 조작\n" +
                "   • WASD : 편의점 내부 자유 이동 (계산대 왼쪽 통로로 출입 가능)\n" +
                "   • 마우스 이동 : 1인칭 시점 회전  /  E 키 : 계산대 설명서 열기·닫기\n\n" +
                "2. 알바생 업무 지시 방법\n" +
                "   • R 키(누른 채 말하기) 또는 V 키 : 마이크로 알바생에게 직접 음성 지시\n" +
                "   • Tab 키 : 보조 텍스트 명령창 열기 (계산·진열·휴식 지시 가능)\n\n" +
                "3. 알바생 감정 · 스트레스 관리\n" +
                "   • 스트레스 0~39 (안정) / 40~69 (불만) / 70~99 (분노·업무 거부 위험)\n" +
                "   • 스트레스 100 도달 시 알바생이 즉시 퇴사하며 게임이 종료됩니다.\n" +
                "   • 부드러운 말투로 칭찬하거나 '잠깐 쉬어' 지시로 스트레스를 낮추세요.\n\n" +
                "4. 영업 목표 및 시연 단축키\n" +
                "   • 영업 시간(08:00 ~ 익일 08:00) 동안 목표 매출을 달성해 정산에 통과하세요.\n" +
                "   • F1~F5 : 음성·오류·스트레스 시뮬 / F6 : 즉시 마감 / F7 : +4시간 / F8 : 비 토글";

            Text bodyTxt = CreatePaperText("ManualBodyText", manualContent.transform, uiFont, manualBody, 36, FontStyle.Normal, inkPrimary, TextAnchor.UpperLeft);
            bodyTxt.lineSpacing = 1.32f;
            SetRect(bodyTxt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(0f, 920f));

            Text footerTxt = CreatePaperText("ManualFooterText", manualContent.transform, paperFont, "※ 카운터 앞 노란 바인더를 바라보고 [E]를 누르면 언제든 다시 확인할 수 있습니다.", 36, FontStyle.Normal, inkAccent, TextAnchor.MiddleCenter);
            SetRect(footerTxt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(0f, 64f));

            dimBg.SetActive(false);

            // 3. 큐브에 상호작용 컴포넌트 부착 및 연결
            StoreCounterManualInteractable interactable = manualCube.AddComponent<StoreCounterManualInteractable>();
            SerializedObject interSo = new SerializedObject(interactable);
            interSo.FindProperty("uiController").objectReferenceValue = uiController;
            interSo.FindProperty("playerCamera").objectReferenceValue = playerCamera;
            interSo.FindProperty("promptRoot").objectReferenceValue = promptObj;
            interSo.FindProperty("manualPanelRoot").objectReferenceValue = dimBg;
            interSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            interSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildPosSettlementStage(GameObject storeMapRoot, StoreUiSessionController uiController)
        {
            // 매장 아래 검은 공간(y = -60)에 일차 정산 전용 3D 포스기 스튜디오 구성
            GameObject controllerObj = new GameObject("PosSettlementStage");
            controllerObj.transform.position = new Vector3(0f, -60f, 0f);

            GameObject posCamObj = new GameObject("PosSettlementCamera");
            posCamObj.transform.SetParent(controllerObj.transform, false);
            posCamObj.transform.localPosition = Vector3.zero;
            posCamObj.transform.localRotation = Quaternion.identity;

            Camera posCam = posCamObj.AddComponent<Camera>();
            posCam.clearFlags = CameraClearFlags.SolidColor;
            posCam.backgroundColor = new Color(0.02f, 0.025f, 0.04f, 1f);
            posCam.fieldOfView = 36f;
            posCam.nearClipPlane = 0.05f;
            posCam.farClipPlane = 20f;
            posCam.depth = 5f;
            posCam.enabled = false;

            GameObject stageVisualRoot = new GameObject("PosStageVisualRoot");
            stageVisualRoot.transform.SetParent(controllerObj.transform, false);

            // 스튜디오 전용 조명 (범위를 좁혀 위쪽 편의점 맵에는 영향을 주지 않음)
            GameObject keyLightObj = new GameObject("PosStudioKeyLight");
            keyLightObj.transform.SetParent(stageVisualRoot.transform, false);
            keyLightObj.transform.localPosition = new Vector3(1.1f, 1.4f, 0.9f);
            Light keyLight = keyLightObj.AddComponent<Light>();
            keyLight.type = LightType.Point;
            keyLight.range = 8f;
            keyLight.intensity = 6.5f;
            keyLight.color = new Color(1f, 0.97f, 0.92f, 1f);

            GameObject rimLightObj = new GameObject("PosStudioRimLight");
            rimLightObj.transform.SetParent(stageVisualRoot.transform, false);
            rimLightObj.transform.localPosition = new Vector3(-1.4f, 0.9f, 2.8f);
            Light rimLight = rimLightObj.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.range = 7f;
            rimLight.intensity = 4.2f;
            rimLight.color = new Color(0.55f, 0.88f, 0.98f, 1f);

            // 포스기 피벗 (중앙 대기 -> 클릭 시 좌측 이동)
            GameObject posPivot = new GameObject("PosMachinePivot");
            posPivot.transform.SetParent(stageVisualRoot.transform, false);
            posPivot.transform.localPosition = new Vector3(0f, -0.35f, 1.95f);
            posPivot.transform.localRotation = Quaternion.Euler(12f, 165f, 0f);
            posPivot.transform.localScale = Vector3.one * 1.55f;

            // 실제 편의점 맵의 Checkout_2 포스기(Register_Base, Register_Screen) 머티리얼과 형태를 그대로 복제
            Transform checkout2 = storeMapRoot != null ? storeMapRoot.transform.Find("Checkout_2") : null;
            Transform srcRegBase = checkout2 != null ? checkout2.Find("Register_Base") : null;
            Transform srcRegScreen = checkout2 != null ? checkout2.Find("Register_Screen") : null;
            Transform srcCounterTop = checkout2 != null ? checkout2.Find("Counter_Top") : null;

            Material darkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Dark.mat");
            Material screenMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Screen.mat");
            Material whiteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/White.mat");
            Material blackMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/LeeSeungJae/Assets/ConvenienceStoreMaterials/Black.mat");

            // 하단 포스기 받침대(카운터 상판 + 서랍)
            GameObject baseDesk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseDesk.name = "PosCounterPedestal";
            baseDesk.transform.SetParent(posPivot.transform, false);
            baseDesk.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            baseDesk.transform.localScale = new Vector3(0.68f, 0.14f, 0.52f);
            if (srcCounterTop != null && srcCounterTop.GetComponent<Renderer>() != null)
            {
                baseDesk.GetComponent<Renderer>().sharedMaterial = srcCounterTop.GetComponent<Renderer>().sharedMaterial;
            }
            else if (darkMat != null)
            {
                baseDesk.GetComponent<Renderer>().sharedMaterial = darkMat;
            }

            // 포스기 본체 (맵의 Register_Base 복제)
            GameObject regBase;
            if (srcRegBase != null)
            {
                regBase = Object.Instantiate(srcRegBase.gameObject, posPivot.transform);
                regBase.name = "Register_Base";
            }
            else
            {
                regBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
                regBase.name = "Register_Base";
                regBase.transform.SetParent(posPivot.transform, false);
                if (darkMat != null) regBase.GetComponent<Renderer>().sharedMaterial = darkMat;
            }
            regBase.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            regBase.transform.localRotation = Quaternion.identity;
            regBase.transform.localScale = new Vector3(0.42f, 0.11f, 0.36f);

            // 포스기 모니터 (맵의 Register_Screen 복제)
            GameObject regScreen;
            if (srcRegScreen != null)
            {
                regScreen = Object.Instantiate(srcRegScreen.gameObject, posPivot.transform);
                regScreen.name = "Register_Screen";
            }
            else
            {
                regScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
                regScreen.name = "Register_Screen";
                regScreen.transform.SetParent(posPivot.transform, false);
                if (screenMat != null) regScreen.GetComponent<Renderer>().sharedMaterial = screenMat;
            }
            regScreen.transform.localPosition = new Vector3(0f, 0.28f, -0.08f);
            regScreen.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            regScreen.transform.localScale = new Vector3(0.40f, 0.34f, 0.05f);

            // 모니터 스탠드 기둥
            GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Register_Stand";
            stand.transform.SetParent(posPivot.transform, false);
            stand.transform.localPosition = new Vector3(0f, 0.14f, -0.07f);
            stand.transform.localScale = new Vector3(0.08f, 0.12f, 0.07f);
            if (blackMat != null) stand.GetComponent<Renderer>().sharedMaterial = blackMat;

            // 영수증 출력 슬롯 (포스기 앞쪽)
            GameObject printerSlot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            printerSlot.name = "ReceiptPrinterSlot";
            printerSlot.transform.SetParent(posPivot.transform, false);
            printerSlot.transform.localPosition = new Vector3(0f, 0.118f, 0.10f);
            printerSlot.transform.localScale = new Vector3(0.20f, 0.02f, 0.06f);
            if (blackMat != null) printerSlot.GetComponent<Renderer>().sharedMaterial = blackMat;

            // 클릭 시 슬롯에서 위로 출력되는 3D 영수증 종이 스트립 (피벗을 아래쪽 끝에 맞춰 위로 자라게 구성)
            GameObject receiptPivot = new GameObject("PosReceiptPaperPivot");
            receiptPivot.transform.SetParent(posPivot.transform, false);
            receiptPivot.transform.localPosition = new Vector3(0f, 0.12f, 0.105f);
            receiptPivot.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            receiptPivot.transform.localScale = new Vector3(1f, 0.01f, 1f);

            GameObject paperMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paperMesh.name = "PosReceiptPaperMesh";
            paperMesh.transform.SetParent(receiptPivot.transform, false);
            paperMesh.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            paperMesh.transform.localScale = new Vector3(0.16f, 1.0f, 0.008f);
            Collider paperCol = paperMesh.GetComponent<Collider>();
            if (paperCol != null) Object.DestroyImmediate(paperCol);
            if (whiteMat != null) paperMesh.GetComponent<Renderer>().sharedMaterial = whiteMat;

            // 영수증 위 인쇄 줄 디테일 4줄
            for (int line = 0; line < 4; line++)
            {
                GameObject inkLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                inkLine.name = $"ReceiptInkLine_{line + 1}";
                inkLine.transform.SetParent(receiptPivot.transform, false);
                inkLine.transform.localPosition = new Vector3(0f, 0.22f + line * 0.18f, 0.006f);
                inkLine.transform.localScale = new Vector3(0.11f, 0.032f, 0.004f);
                Collider lc = inkLine.GetComponent<Collider>();
                if (lc != null) Object.DestroyImmediate(lc);
                if (darkMat != null) inkLine.GetComponent<Renderer>().sharedMaterial = darkMat;
            }

            receiptPivot.SetActive(false);

            // 클릭 감지용 통합 박스 콜라이더
            BoxCollider hitBox = posPivot.AddComponent<BoxCollider>();
            hitBox.center = new Vector3(0f, 0.16f, 0f);
            hitBox.size = new Vector3(0.72f, 0.58f, 0.56f);

            SettlementPanelPresenter settlementPresenter = uiController != null
                ? uiController.GetComponentInChildren<SettlementPanelPresenter>(true)
                : null;

            PosSettlementStageController stageCtrl = controllerObj.AddComponent<PosSettlementStageController>();
            SerializedObject stageSo = new SerializedObject(stageCtrl);
            stageSo.FindProperty("uiController").objectReferenceValue = uiController;
            stageSo.FindProperty("settlementPanel").objectReferenceValue = settlementPresenter;
            stageSo.FindProperty("posSettlementCamera").objectReferenceValue = posCam;
            stageSo.FindProperty("posStageRoot").objectReferenceValue = stageVisualRoot;
            stageSo.FindProperty("posMachinePivot").objectReferenceValue = posPivot.transform;
            stageSo.FindProperty("receiptPaperStrip").objectReferenceValue = receiptPivot.transform;
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            stageVisualRoot.SetActive(false);
        }

        private static bool TryImportConvenienceStoreMap(
            Scene targetScene,
            out GameObject importedMapRoot,
            out Dictionary<string, int> spawnedItemCounts)
        {
            importedMapRoot = null;
            spawnedItemCounts = null;
            if (!File.Exists(ConvenienceStoreMapScenePath))
            {
                return false;
            }

            Scene mapScene = EditorSceneManager.OpenScene(ConvenienceStoreMapScenePath, OpenSceneMode.Additive);
            if (!mapScene.IsValid())
            {
                return false;
            }

            try
            {
                EditorSceneManager.SetActiveScene(targetScene);
                GameObject mapRoot = new GameObject("ConvenienceStore_Map");
                SceneManager.MoveGameObjectToScene(mapRoot, targetScene);

                int importedCount = 0;
                GameObject[] roots = mapScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    GameObject src = roots[i];
                    if (src == null || !ConvenienceStoreEnvironmentRoots.Contains(src.name))
                    {
                        continue;
                    }

                    GameObject clone = Object.Instantiate(src);
                    clone.name = src.name;
                    SceneManager.MoveGameObjectToScene(clone, targetScene);
                    clone.transform.SetParent(mapRoot.transform, true);
                    importedCount++;
                }

                if (importedCount > 0)
                {
                    spawnedItemCounts = ConvenienceStoreProductPopulator.PopulateMap(mapRoot);
                    importedMapRoot = mapRoot;
                }

                return importedCount > 0;
            }
            finally
            {
                EditorSceneManager.CloseScene(mapScene, true);
                EditorSceneManager.SetActiveScene(targetScene);
            }
        }

        private static NpcPopulationController BuildConvenienceStoreCustomerNpcSystem(Transform parent)
        {
            GameObject customerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerCapsulePrefabPath);
            GameObject pedestrianPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PedestrianCapsulePrefabPath);
            GameObject productPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerProductPrefabPath);
            if (customerPrefab == null || pedestrianPrefab == null || productPrefab == null)
            {
                return null;
            }

            GameObject npcSysObj = new GameObject("Store_CustomerNpcSystem");
            npcSysObj.transform.SetParent(parent, false);

            Transform leftSidewalk = CreateEmptyWaypoint(npcSysObj.transform, "Sidewalk_Left", new Vector3(-5.20f, 0f, 4.65f));
            Transform rightSidewalk = CreateEmptyWaypoint(npcSysObj.transform, "Sidewalk_Right", new Vector3(5.20f, 0f, 4.65f));
            Transform entrance = CreateEmptyWaypoint(npcSysObj.transform, "Customer_Entrance", new Vector3(4.25f, 0f, 3.05f));
            Transform shelf = CreateEmptyWaypoint(npcSysObj.transform, "Customer_Shelf", new Vector3(-0.90f, 0f, 0.65f));
            Transform checkout = CreateEmptyWaypoint(npcSysObj.transform, "Customer_Checkout", new Vector3(-3.80f, 0f, -1.55f));
            Transform exit = CreateEmptyWaypoint(npcSysObj.transform, "Customer_Exit", new Vector3(4.25f, 0f, 4.35f));

            Transform[] queueSlots =
            {
                CreateEmptyWaypoint(npcSysObj.transform, "QueueSlot_0", new Vector3(-2.70f, 0f, -1.55f)),
                CreateEmptyWaypoint(npcSysObj.transform, "QueueSlot_1", new Vector3(-1.60f, 0f, -1.55f)),
                CreateEmptyWaypoint(npcSysObj.transform, "QueueSlot_2", new Vector3(-0.50f, 0f, -1.55f)),
                CreateEmptyWaypoint(npcSysObj.transform, "QueueSlot_3", new Vector3(0.60f, 0f, -1.55f))
            };

            NpcPopulationController population = npcSysObj.AddComponent<NpcPopulationController>();
            SerializedObject popSo = new SerializedObject(population);
            popSo.FindProperty("customerPrefab").objectReferenceValue = customerPrefab;
            popSo.FindProperty("pedestrianPrefab").objectReferenceValue = pedestrianPrefab;
            popSo.FindProperty("productPrefab").objectReferenceValue = productPrefab;
            popSo.FindProperty("leftSidewalkZone").objectReferenceValue = leftSidewalk;
            popSo.FindProperty("rightSidewalkZone").objectReferenceValue = rightSidewalk;
            popSo.FindProperty("sidewalkZoneSize").vector2Value = new Vector2(2.2f, 1.3f);
            popSo.FindProperty("entrance").objectReferenceValue = entrance;
            popSo.FindProperty("shelf").objectReferenceValue = shelf;
            popSo.FindProperty("checkout").objectReferenceValue = checkout;
            popSo.FindProperty("exit").objectReferenceValue = exit;

            SerializedProperty slotsProp = popSo.FindProperty("queueSlots");
            slotsProp.arraySize = queueSlots.Length;
            for (int i = 0; i < queueSlots.Length; i++)
            {
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = queueSlots[i];
            }

            popSo.ApplyModifiedPropertiesWithoutUndo();
            return population;
        }

        private static Transform CreateEmptyWaypoint(Transform parent, string waypointName, Vector3 worldPos)
        {
            GameObject marker = new GameObject(waypointName);
            marker.transform.SetParent(parent, false);
            marker.transform.position = worldPos;
            return marker.transform;
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

            bool isFullscreenOrFlat =
                name == "LobbyPanel" ||
                name == "PersonaSetupPanel" ||
                name == "CommandOverlay" ||
                name == "SettlementPanel" ||
                name == "EndingPanel" ||
                name == "PausePanel" ||
                name == "SettingsPanel" ||
                name == "CrosshairDot" ||
                name == "PortraitImage" ||
                name == "Fill" ||
                name.StartsWith("BrandStripe_") ||
                name.EndsWith("_Fill");

            bool isInputOrRecessed =
                name == "ReceiptBox" ||
                name == "CutscenePlaceholderBox" ||
                name == "EndingNarrativeBox" ||
                name == "PortraitFrameBorder" ||
                name.Contains("InputField");

            bool isTrack =
                name == "Background" ||
                name.EndsWith("_Track");

            if (name == "Handle" && _sliderHandleSprite != null)
            {
                img.sprite = _sliderHandleSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
            else if (isFullscreenOrFlat)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
            }
            else if (isInputOrRecessed && _inputBoxSprite != null)
            {
                img.sprite = _inputBoxSprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2.4f;
            }
            else if (isTrack && _barTrackSprite != null)
            {
                img.sprite = _barTrackSprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 3.2f;
            }
            else if (_panelCardSprite != null)
            {
                img.sprite = _panelCardSprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2.2f;
            }
            else
            {
                img.sprite = sprite;
            }

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
            if (style == FontStyle.Bold)
            {
                Shadow shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
                shadow.effectDistance = new Vector2(1f, -1.5f);
            }
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
            Image btnImg = go.GetComponent<Image>();
            if (_buttonDepthSprite != null)
            {
                btnImg.sprite = _buttonDepthSprite;
                btnImg.type = Image.Type.Sliced;
                btnImg.pixelsPerUnitMultiplier = 2.2f;
            }

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            Text txt = CreateText("Label", go.transform, font, label, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform);
            txt.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            return btn;
        }

        private static Button CreateTransparentTextButton(
            string name,
            Transform parent,
            Font font,
            string label,
            int fontSize,
            Color normalTextColor,
            Color highlightedTextColor,
            TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            Image hitAreaImg = go.GetComponent<Image>();
            hitAreaImg.sprite = null;
            hitAreaImg.color = new Color(1f, 1f, 1f, 0f);
            hitAreaImg.raycastTarget = true;

            Text txt = CreateText("Label", go.transform, font, label, fontSize, FontStyle.Bold, Color.white, alignment);
            txt.raycastTarget = false;
            StretchFull(txt.rectTransform);

            Outline outline = txt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = txt;
            ColorBlock cb = btn.colors;
            cb.normalColor = normalTextColor;
            cb.highlightedColor = highlightedTextColor;
            cb.pressedColor = new Color(normalTextColor.r * 0.72f, normalTextColor.g * 0.72f, normalTextColor.b * 0.72f, 1f);
            cb.selectedColor = normalTextColor;
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

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
