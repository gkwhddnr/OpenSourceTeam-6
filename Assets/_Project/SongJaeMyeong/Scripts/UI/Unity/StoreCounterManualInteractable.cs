using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    using Camera = UnityEngine.Camera;
    /// <summary>
    /// 계산대 위 설명서 큐브 오브젝트와 E 키로 상호작용해 편의점 운영 설명서 패널을 여닫는 컴포넌트.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class StoreCounterManualInteractable : MonoBehaviour
    {
        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private GameObject manualPanelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private float maxInteractDistance = 3.2f;
        [SerializeField] private float sphereCastRadius = 0.16f;

        private bool isManualOpen;
        private IDisposable modalLockLease;
        private bool hasNormalizedManualLayout;

        private void OnEnable()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseManual);
            }

            NormalizeManualModalLayout();
            SetPromptVisible(false);
            if (manualPanelRoot != null)
            {
                manualPanelRoot.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseManual);
            }

            ForceCloseWithoutAudio();
        }

        private void Update()
        {
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<StoreUiSessionController>();
                if (uiController == null) return;
            }

            if (playerCamera == null ||
                !playerCamera.isActiveAndEnabled ||
                playerCamera.gameObject.name == "LobbyOverviewCamera" ||
                playerCamera.gameObject.name == "PosSettlementCamera")
            {
                Camera mainCam = Camera.main;
                if (mainCam != null &&
                    mainCam.isActiveAndEnabled &&
                    mainCam.gameObject.name != "LobbyOverviewCamera" &&
                    mainCam.gameObject.name != "PosSettlementCamera")
                {
                    playerCamera = mainCam;
                }
            }

            // 영업 중 화면이 아니거나 화면 전환(N일차 페이드) 중에는 상호작용 프롬프트·입력을 숨긴다.
            if (uiController.CurrentMode != UiScreenMode.InGame || uiController.IsTransitionFading)
            {
                if (isManualOpen)
                {
                    ForceCloseWithoutAudio();
                }
                SetPromptVisible(false);
                return;
            }

            Keyboard kb = Keyboard.current;

            if (isManualOpen)
            {
                SetPromptVisible(false);
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                {
                    CloseManual();
                }
                return;
            }

            if (uiController.IsCommandOverlayOpen || uiController.IsPauseMenuOpen || uiController.IsSettingsOpen)
            {
                SetPromptVisible(false);
                return;
            }

            bool canInteract = IsPlayerLookingAtObject();
            SetPromptVisible(canInteract);

            if (canInteract && kb != null && kb.eKey.wasPressedThisFrame)
            {
                OpenManual();
            }
        }

        private bool IsPlayerLookingAtObject()
        {
            if (playerCamera == null) return false;

            Transform camTr = playerCamera.transform;
            Vector3 toTarget = transform.position - camTr.position;
            float distance = toTarget.magnitude;
            if (distance > maxInteractDistance) return false;

            Ray ray = new Ray(camTr.position, camTr.forward);
            if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxInteractDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            if (distance <= 2.2f && distance > 0.001f)
            {
                float alignment = Vector3.Dot(camTr.forward, toTarget / distance);
                if (alignment >= 0.80f)
                {
                    return true;
                }
            }

            return false;
        }

        public void OpenManual()
        {
            if (isManualOpen) return;
            isManualOpen = true;
            SetPromptVisible(false);
            NormalizeManualModalLayout();

            if (uiController != null)
            {
                modalLockLease = uiController.AcquireExternalModalLock("StoreCounterManual");
                uiController.AudioPlayer?.PlaySfx(StoreSfxId.PaperFlip);
            }

            if (manualPanelRoot != null)
            {
                manualPanelRoot.SetActive(true);
            }
        }

        private void NormalizeManualModalLayout()
        {
            if (hasNormalizedManualLayout || manualPanelRoot == null) return;
            hasNormalizedManualLayout = true;

            // 설명서 모달이 하단 음성 도크/우측 포스트잇 뒤로 가려지지 않도록 캔버스 정렬 순서를 상위로 올린다.
            Canvas parentCanvas = manualPanelRoot.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentCanvas.overrideSorting = true;
                if (parentCanvas.sortingOrder < 60)
                {
                    parentCanvas.sortingOrder = 60;
                }
            }

            Transform borderTr = manualPanelRoot.transform.Find("ManualPaperBorder");
            if (borderTr is RectTransform borderRt)
            {
                borderRt.anchoredPosition = new Vector2(0f, 12f);
                borderRt.sizeDelta = new Vector2(880f, 720f);
            }

            Transform contentTr = manualPanelRoot.transform.Find("ManualPaperBorder/ManualPaperCard/ManualContent");
            if (contentTr is RectTransform contentRt)
            {
                contentRt.sizeDelta = new Vector2(1620f, 1320f);
                contentRt.localScale = new Vector3(0.5f, 0.5f, 1f);
            }

            if (contentTr != null)
            {
                Transform titleTr = contentTr.Find("ManualTitle");
                if (titleTr != null && titleTr.TryGetComponent(out Text titleText))
                {
                    titleText.fontStyle = FontStyle.Normal;
                    titleText.fontSize = 68;
                }

                Transform bodyTr = contentTr.Find("ManualBodyText");
                if (bodyTr != null && bodyTr.TryGetComponent(out Text bodyText))
                {
                    bodyText.fontStyle = FontStyle.Normal;
                    bodyText.fontSize = 33;
                    bodyText.lineSpacing = 1.20f;
                    bodyText.verticalOverflow = VerticalWrapMode.Overflow;
                    bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    bodyText.rectTransform.anchoredPosition = new Vector2(0f, -138f);
                    bodyText.rectTransform.sizeDelta = new Vector2(0f, 1050f);
                    bodyText.text =
                        "1. 매장 이동 및 시점 조작\n" +
                        "   • WASD : 편의점 내부 자유 이동 (계산대 통로로 출입 가능)\n" +
                        "   • 마우스 이동 : 1인칭 시점 회전  /  E 키 : 포스기 결제 · 설명서 열기/닫기\n\n" +
                        "2. 알바생 업무 지시 및 대화 기록\n" +
                        "   • R 키(누른 채 말하기) 또는 V 키 : 마이크로 알바생에게 직접 음성 지시\n" +
                        "   • Tab 키 : 보조 텍스트 명령창  /  H 키 : 하단 대화 기록 창 열기·접기\n\n" +
                        "3. 알바생 감정 · 스트레스 관리\n" +
                        "   • 스트레스 0~39 (안정) / 40~69 (불만) / 80 이상 (분노·위기 BGM)\n" +
                        "   • 스트레스 100 도달 시 알바생이 즉시 퇴사하며 게임이 종료됩니다.\n" +
                        "   • 부드러운 말투로 칭찬하거나 '잠깐 쉬어' 지시로 스트레스를 낮추세요.\n\n" +
                        "4. 영업 목표 및 시연 단축키\n" +
                        "   • 영업 시간(08:00 ~ 익일 08:00) 동안 목표 매출을 달성해 정산에 통과하세요.\n" +
                        "   • F6 : 즉시 마감 정산  /  F7 : +4시간 진행  /  F8 : 비 날씨 토글  /  G : 포스트잇 접기";
                }

                Transform footerTr = contentTr.Find("ManualFooterText");
                if (footerTr != null && footerTr.TryGetComponent(out Text footerText))
                {
                    footerText.fontStyle = FontStyle.Normal;
                    footerText.fontSize = 32;
                    footerText.rectTransform.anchoredPosition = new Vector2(0f, 14f);
                    footerText.rectTransform.sizeDelta = new Vector2(0f, 60f);
                }
            }
        }

        public void CloseManual()
        {
            if (!isManualOpen) return;
            isManualOpen = false;

            if (manualPanelRoot != null)
            {
                manualPanelRoot.SetActive(false);
            }

            modalLockLease?.Dispose();
            modalLockLease = null;

            uiController?.AudioPlayer?.PlaySfx(StoreSfxId.PaperFlip);
        }

        private void ForceCloseWithoutAudio()
        {
            isManualOpen = false;
            SetPromptVisible(false);
            if (manualPanelRoot != null)
            {
                manualPanelRoot.SetActive(false);
            }

            modalLockLease?.Dispose();
            modalLockLease = null;
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptRoot != null && promptRoot.activeSelf != visible)
            {
                promptRoot.SetActive(visible);
            }
        }
    }
}
