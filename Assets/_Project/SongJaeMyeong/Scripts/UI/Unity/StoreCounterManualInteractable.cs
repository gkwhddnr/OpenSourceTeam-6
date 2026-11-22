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

        private void OnEnable()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseManual);
            }

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

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
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
