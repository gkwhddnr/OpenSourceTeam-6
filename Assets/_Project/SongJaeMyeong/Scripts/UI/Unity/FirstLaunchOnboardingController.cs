using System;
using System.Collections;
using System.IO;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    using Time = UnityEngine.Time;

    /// <summary>
    /// 프로그램 최초 실행 시 1회만 재생되는 오프닝 문구("살고 싶은 자, 일하라") 및
    /// 최초 플레이 이력서 좌상단 가이드(주변 어둡게 강조 + E키 확인 전 모든 입력 차단)를 관리한다.
    /// 상태는 Application.persistentDataPath/first_launch_state.json 파일에 영구 저장된다.
    /// </summary>
    public sealed class FirstLaunchOnboardingController : MonoBehaviour
    {
        public const string StateFileName = "first_launch_state.json";

        [Serializable]
        private sealed class FirstLaunchStateData
        {
            public bool hasCompletedBootIntro;
            public bool hasCompletedResumeGuide;
        }

        [Header("최초 실행 인트로 설정")]
        [SerializeField] private string bootIntroMessage = "살고 싶은 자, 일하라";
        [SerializeField, Min(0f)] private float bootInitialBlackSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float bootTextFadeInSeconds = 0.75f;
        [SerializeField, Min(0f)] private float bootTextHoldSeconds = 1.80f;
        [SerializeField, Min(0.05f)] private float bootTextFadeOutSeconds = 0.60f;
        [SerializeField, Min(0.05f)] private float bootBackdropFadeOutSeconds = 0.85f;

        [Header("이력서 최초 가이드 설정")]
        [SerializeField] private string resumeGuideTitle = "채용 이력서 안내";
        [SerializeField, TextArea(2, 5)] private string resumeGuideBody =
            "당신은 편의점 운영을 위해 알바를 채용해야 합니다.\n" +
            "알바의 이름과 성격에 따라 매장 운영과 플레이가 달라집니다.";
        [SerializeField] private string resumeGuideConfirmPrompt = "[ E ] 키를 눌러 확인";

        private CanvasGroup bootBackdropGroup;
        private CanvasGroup bootTextGroup;
        private Text bootIntroText;
        private Coroutine bootIntroCoroutine;
        private Action bootIntroCompletedCallback;

        private CanvasGroup resumeGuideGroup;
        private Text resumeGuidePromptText;
        private IStoreAudioPlayer resumeGuideAudioPlayer;
        private float resumeGuideOpenedUnscaledTime;

        public bool IsBootIntroActive { get; private set; }
        public bool IsResumeGuideActive { get; private set; }
        public bool IsBlockingInput => IsBootIntroActive || IsResumeGuideActive;

        public static string StateFilePath => Path.Combine(Application.persistentDataPath, StateFileName);

        public static void ResetSavedState()
        {
            try
            {
                string path = StateFilePath;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FirstLaunchOnboarding] 상태 파일 초기화 실패: " + ex.Message);
            }
        }

        public bool TryPlayFirstLaunchBootIntro(Font uiFont, Action onCompleted = null)
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            FirstLaunchStateData state = LoadState();
            if (state.hasCompletedBootIntro)
            {
                return false;
            }

            // 다음 프로그램 실행부터는 다시 뜨지 않도록 최초 실행 기록을 즉시 저장한다.
            state.hasCompletedBootIntro = true;
            SaveState(state);

            EnsureBootIntroOverlay(uiFont);
            if (bootIntroCoroutine != null)
            {
                StopCoroutine(bootIntroCoroutine);
            }

            bootIntroCompletedCallback = onCompleted;
            IsBootIntroActive = true;
            bootIntroCoroutine = StartCoroutine(CoPlayBootIntro());
            return true;
        }

        public bool TryShowFirstPlayResumeGuide(Font uiFont, IStoreAudioPlayer audioPlayer)
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            FirstLaunchStateData state = LoadState();
            if (state.hasCompletedResumeGuide)
            {
                return false;
            }

            resumeGuideAudioPlayer = audioPlayer;
            EnsureResumeGuideOverlay(uiFont);

            resumeGuideGroup.gameObject.SetActive(true);
            resumeGuideGroup.transform.SetAsLastSibling();
            resumeGuideGroup.alpha = 1f;
            resumeGuideGroup.interactable = true;
            resumeGuideGroup.blocksRaycasts = true;

            IsResumeGuideActive = true;
            resumeGuideOpenedUnscaledTime = Time.unscaledTime;
            ClearUiSelection();
            return true;
        }

        public void HideResumeGuideWithoutSaving()
        {
            IsResumeGuideActive = false;
            if (resumeGuideGroup != null)
            {
                resumeGuideGroup.alpha = 0f;
                resumeGuideGroup.interactable = false;
                resumeGuideGroup.blocksRaycasts = false;
                resumeGuideGroup.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (bootIntroCoroutine != null)
            {
                StopCoroutine(bootIntroCoroutine);
                bootIntroCoroutine = null;
            }

            IsBootIntroActive = false;
            bootIntroCompletedCallback = null;
            if (bootBackdropGroup != null)
            {
                bootBackdropGroup.alpha = 0f;
                bootBackdropGroup.blocksRaycasts = false;
                bootBackdropGroup.gameObject.SetActive(false);
            }

            HideResumeGuideWithoutSaving();
        }

        private void Update()
        {
            if (IsBootIntroActive)
            {
                ClearUiSelection();
                return;
            }

            if (!IsResumeGuideActive)
            {
                return;
            }

            // E키로 확인하기 전에는 하단 이력서 입력창·버튼에 포커스가 가지 않도록 차단한다.
            ClearUiSelection();

            if (resumeGuidePromptText != null)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4.2f);
                Color c = resumeGuidePromptText.color;
                c.a = pulse;
                resumeGuidePromptText.color = c;
            }

            if (Time.unscaledTime - resumeGuideOpenedUnscaledTime < 0.15f)
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                ConfirmResumeGuide();
            }
        }

        private void ConfirmResumeGuide()
        {
            FirstLaunchStateData state = LoadState();
            state.hasCompletedResumeGuide = true;
            SaveState(state);

            resumeGuideAudioPlayer?.PlaySfx(StoreSfxId.UiClick);
            HideResumeGuideWithoutSaving();
        }

        private IEnumerator CoPlayBootIntro()
        {
            bootBackdropGroup.gameObject.SetActive(true);
            bootBackdropGroup.transform.SetAsLastSibling();
            bootBackdropGroup.alpha = 1f;
            bootBackdropGroup.interactable = true;
            bootBackdropGroup.blocksRaycasts = true;

            bootIntroText.text = bootIntroMessage;
            bootTextGroup.gameObject.SetActive(true);
            bootTextGroup.alpha = 0f;

            // 1. 초기 검은 화면 유지
            if (bootInitialBlackSeconds > 0.001f)
            {
                float elapsed = 0f;
                while (elapsed < bootInitialBlackSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 2. 흰 글씨 페이드 인
            {
                float duration = Mathf.Max(0.05f, bootTextFadeInSeconds);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    bootTextGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                    yield return null;
                }
                bootTextGroup.alpha = 1f;
            }

            // 3. 문구 유지
            if (bootTextHoldSeconds > 0.001f)
            {
                float elapsed = 0f;
                while (elapsed < bootTextHoldSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 4. 문구 페이드 아웃
            {
                float duration = Mathf.Max(0.05f, bootTextFadeOutSeconds);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    bootTextGroup.alpha = Mathf.SmoothStep(1f, 0f, t);
                    yield return null;
                }
                bootTextGroup.alpha = 0f;
                bootTextGroup.gameObject.SetActive(false);
            }

            // 5. 검은 화면 페이드 아웃 (로비 노출)
            {
                float duration = Mathf.Max(0.05f, bootBackdropFadeOutSeconds);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    bootBackdropGroup.alpha = Mathf.SmoothStep(1f, 0f, t);
                    yield return null;
                }
            }

            bootBackdropGroup.alpha = 0f;
            bootBackdropGroup.interactable = false;
            bootBackdropGroup.blocksRaycasts = false;
            bootBackdropGroup.gameObject.SetActive(false);

            IsBootIntroActive = false;
            bootIntroCoroutine = null;

            Action completed = bootIntroCompletedCallback;
            bootIntroCompletedCallback = null;
            completed?.Invoke();
        }

        private void EnsureBootIntroOverlay(Font uiFont)
        {
            if (bootBackdropGroup != null && bootTextGroup != null && bootIntroText != null)
            {
                return;
            }

            Font resolvedFont = uiFont != null ? uiFont : ResolveFallbackFont();

            GameObject backdropObj = new GameObject("FirstLaunchBootIntroOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            backdropObj.transform.SetParent(transform, false);
            RectTransform backdropRt = backdropObj.GetComponent<RectTransform>();
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            backdropRt.pivot = new Vector2(0.5f, 0.5f);
            backdropRt.anchoredPosition = Vector2.zero;
            backdropRt.sizeDelta = Vector2.zero;

            Image backdropImage = backdropObj.GetComponent<Image>();
            backdropImage.color = Color.black;
            backdropImage.raycastTarget = true;

            bootBackdropGroup = backdropObj.GetComponent<CanvasGroup>();
            bootBackdropGroup.alpha = 0f;
            bootBackdropGroup.interactable = false;
            bootBackdropGroup.blocksRaycasts = false;

            // 2x 슈퍼샘플링으로 선명한 중앙 흰 글씨 렌더링
            GameObject textObj = new GameObject("BootIntroText", typeof(RectTransform), typeof(CanvasGroup), typeof(Text));
            textObj.transform.SetParent(backdropObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.sizeDelta = new Vector2(2400f, 360f);
            textRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            bootTextGroup = textObj.GetComponent<CanvasGroup>();
            bootTextGroup.alpha = 0f;
            bootTextGroup.interactable = false;
            bootTextGroup.blocksRaycasts = false;

            bootIntroText = textObj.GetComponent<Text>();
            bootIntroText.font = resolvedFont;
            bootIntroText.fontSize = 136;
            bootIntroText.fontStyle = FontStyle.Bold;
            bootIntroText.color = new Color(0.98f, 0.98f, 0.97f, 1f);
            bootIntroText.alignment = TextAnchor.MiddleCenter;
            bootIntroText.horizontalOverflow = HorizontalWrapMode.Overflow;
            bootIntroText.verticalOverflow = VerticalWrapMode.Overflow;
            bootIntroText.raycastTarget = false;
            bootIntroText.text = bootIntroMessage;

            backdropObj.SetActive(false);
        }

        private void EnsureResumeGuideOverlay(Font uiFont)
        {
            if (resumeGuideGroup != null && resumeGuidePromptText != null)
            {
                return;
            }

            Font resolvedFont = uiFont != null ? uiFont : ResolveFallbackFont();

            // 전체 화면 어둡게 + 모든 클릭 차단
            GameObject overlayObj = new GameObject("FirstPlayResumeGuideOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlayObj.transform.SetParent(transform, false);
            RectTransform overlayRt = overlayObj.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.pivot = new Vector2(0.5f, 0.5f);
            overlayRt.anchoredPosition = Vector2.zero;
            overlayRt.sizeDelta = Vector2.zero;

            Image dimImage = overlayObj.GetComponent<Image>();
            dimImage.color = new Color(0.02f, 0.02f, 0.04f, 0.76f);
            dimImage.raycastTarget = true;

            resumeGuideGroup = overlayObj.GetComponent<CanvasGroup>();
            resumeGuideGroup.alpha = 0f;
            resumeGuideGroup.interactable = false;
            resumeGuideGroup.blocksRaycasts = false;

            // 좌상단 강조 안내 카드
            GameObject cardObj = new GameObject("ResumeGuideCard", typeof(RectTransform), typeof(Image), typeof(Outline));
            cardObj.transform.SetParent(overlayObj.transform, false);
            RectTransform cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0f, 1f);
            cardRt.anchorMax = new Vector2(0f, 1f);
            cardRt.pivot = new Vector2(0f, 1f);
            cardRt.anchoredPosition = new Vector2(44f, -44f);
            cardRt.sizeDelta = new Vector2(560f, 248f);

            Image cardBg = cardObj.GetComponent<Image>();
            cardBg.color = new Color(0.11f, 0.13f, 0.17f, 0.98f);
            cardBg.raycastTarget = true;

            Outline cardOutline = cardObj.GetComponent<Outline>();
            cardOutline.effectColor = new Color(0.96f, 0.78f, 0.34f, 0.95f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            // 상단 강조 바
            GameObject accentObj = new GameObject("TopAccentBar", typeof(RectTransform), typeof(Image));
            accentObj.transform.SetParent(cardObj.transform, false);
            RectTransform accentRt = accentObj.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 1f);
            accentRt.anchorMax = new Vector2(1f, 1f);
            accentRt.pivot = new Vector2(0.5f, 1f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(0f, 7f);
            Image accentImg = accentObj.GetComponent<Image>();
            accentImg.color = new Color(0.96f, 0.78f, 0.34f, 1f);
            accentImg.raycastTarget = false;

            // 제목
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(28f, -24f);
            titleRt.sizeDelta = new Vector2(-56f, 38f);

            Text titleText = titleObj.GetComponent<Text>();
            titleText.font = resolvedFont;
            titleText.fontSize = 26;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.98f, 0.84f, 0.42f, 1f);
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.raycastTarget = false;
            titleText.text = resumeGuideTitle;

            // 본문 설명
            GameObject bodyObj = new GameObject("BodyText", typeof(RectTransform), typeof(Text));
            bodyObj.transform.SetParent(cardObj.transform, false);
            RectTransform bodyRt = bodyObj.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.offsetMin = new Vector2(28f, 64f);
            bodyRt.offsetMax = new Vector2(-28f, -72f);

            Text bodyText = bodyObj.GetComponent<Text>();
            bodyText.font = resolvedFont;
            bodyText.fontSize = 21;
            bodyText.lineSpacing = 1.32f;
            bodyText.color = new Color(0.95f, 0.96f, 0.97f, 1f);
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.raycastTarget = false;
            bodyText.text = resumeGuideBody;

            // 하단 E키 확인 안내
            GameObject promptObj = new GameObject("ConfirmPromptText", typeof(RectTransform), typeof(Text));
            promptObj.transform.SetParent(cardObj.transform, false);
            RectTransform promptRt = promptObj.GetComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0f, 0f);
            promptRt.anchorMax = new Vector2(1f, 0f);
            promptRt.pivot = new Vector2(0.5f, 0f);
            promptRt.anchoredPosition = new Vector2(0f, 18f);
            promptRt.sizeDelta = new Vector2(-56f, 36f);

            resumeGuidePromptText = promptObj.GetComponent<Text>();
            resumeGuidePromptText.font = resolvedFont;
            resumeGuidePromptText.fontSize = 20;
            resumeGuidePromptText.fontStyle = FontStyle.Bold;
            resumeGuidePromptText.color = new Color(0.98f, 0.84f, 0.42f, 1f);
            resumeGuidePromptText.alignment = TextAnchor.MiddleRight;
            resumeGuidePromptText.raycastTarget = false;
            resumeGuidePromptText.text = resumeGuideConfirmPrompt;

            overlayObj.SetActive(false);
        }

        private static void ClearUiSelection()
        {
            EventSystem current = EventSystem.current;
            if (current != null && current.currentSelectedGameObject != null)
            {
                current.SetSelectedGameObject(null);
            }
        }

        private Font ResolveFallbackFont()
        {
            Text anyText = GetComponentInChildren<Text>(true);
            if (anyText != null && anyText.font != null)
            {
                return anyText.font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static FirstLaunchStateData LoadState()
        {
            try
            {
                string path = StateFilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    FirstLaunchStateData loaded = JsonUtility.FromJson<FirstLaunchStateData>(json);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FirstLaunchOnboarding] 상태 파일 읽기 실패: " + ex.Message);
            }

            return new FirstLaunchStateData();
        }

        private static void SaveState(FirstLaunchStateData data)
        {
            try
            {
                string path = StateFilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FirstLaunchOnboarding] 상태 파일 저장 실패: " + ex.Message);
            }
        }
    }
}
