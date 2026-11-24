using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    public sealed class CommandOverlayPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private InputField commandInputField;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button quickCheckoutButton;
        [SerializeField] private Button quickRestockButton;
        [SerializeField] private Button cancelWorkButton;
        [SerializeField] private Button closeButton;

        public bool IsOpen => (overlayRoot != null ? overlayRoot : gameObject).activeSelf;

        public event Action<string> CommandSubmitted;
        public event Action CancelWorkClicked;
        public event Action CloseRequested;

        private void OnEnable()
        {
            if (commandInputField != null) commandInputField.onSubmit.AddListener(HandleInputFieldSubmitted);
            if (submitButton != null) submitButton.onClick.AddListener(HandleSubmitClicked);
            if (quickCheckoutButton != null) quickCheckoutButton.onClick.AddListener(HandleQuickCheckout);
            if (quickRestockButton != null) quickRestockButton.onClick.AddListener(HandleQuickRestock);
            if (cancelWorkButton != null) cancelWorkButton.onClick.AddListener(HandleCancelWork);
            if (closeButton != null) closeButton.onClick.AddListener(HandleCloseClicked);

            CleanupLegacyModalHistoryUi();
        }

        private void OnDisable()
        {
            if (commandInputField != null) commandInputField.onSubmit.RemoveListener(HandleInputFieldSubmitted);
            if (submitButton != null) submitButton.onClick.RemoveListener(HandleSubmitClicked);
            if (quickCheckoutButton != null) quickCheckoutButton.onClick.RemoveListener(HandleQuickCheckout);
            if (quickRestockButton != null) quickRestockButton.onClick.RemoveListener(HandleQuickRestock);
            if (cancelWorkButton != null) cancelWorkButton.onClick.RemoveListener(HandleCancelWork);
            if (closeButton != null) closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        private void Update()
        {
            if (!IsOpen) return;

            // InputField가 포커스 중일 때는 한글 IME 조합이 완료된 뒤 onSubmit으로 제출되도록
            // Update에서 Enter를 가로채지 않는다 (가로채면 마지막 한글 1글자가 누락됨).
            if (commandInputField != null && commandInputField.isFocused) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                HandleSubmitClicked();
            }
        }

        public void SetOpen(bool open)
        {
            GameObject target = overlayRoot != null ? overlayRoot : gameObject;
            if (target.activeSelf != open)
            {
                target.SetActive(open);
            }

            if (open)
            {
                CleanupLegacyModalHistoryUi();
                if (commandInputField != null)
                {
                    commandInputField.text = string.Empty;
                    commandInputField.Select();
                    commandInputField.ActivateInputField();
                }
            }
        }

        private void CleanupLegacyModalHistoryUi()
        {
            Transform root = overlayRoot != null ? overlayRoot.transform : transform;
            Transform modal = root.Find("CommandModalCard");
            if (modal == null) return;

            Transform oldBtn = modal.Find("ChatHistoryToggleButton");
            if (oldBtn != null) oldBtn.gameObject.SetActive(false);

            Transform oldDrawer = modal.Find("ChatHistoryDrawer");
            if (oldDrawer != null) oldDrawer.gameObject.SetActive(false);
        }

        private void HandleInputFieldSubmitted(string submittedText)
        {
            if (!IsOpen) return;
            SubmitResolvedText(submittedText);
        }

        private void HandleSubmitClicked()
        {
            if (commandInputField == null) return;
            SubmitResolvedText(commandInputField.text);
        }

        private void SubmitResolvedText(string rawText)
        {
            string combined = rawText ?? string.Empty;
            string composing = GetCurrentImeCompositionString();
            if (!string.IsNullOrEmpty(composing) && !combined.EndsWith(composing, StringComparison.Ordinal))
            {
                combined += composing;
            }

            if (string.IsNullOrWhiteSpace(combined)) return;

            string trimmed = combined.Trim();
            if (commandInputField != null)
            {
                commandInputField.text = string.Empty;
            }
            CommandSubmitted?.Invoke(trimmed);
        }

        private static string GetCurrentImeCompositionString()
        {
            EventSystem es = EventSystem.current;
            BaseInputModule module = es != null ? es.currentInputModule : null;
            BaseInput input = module != null ? module.input : null;
            return input != null ? input.compositionString : string.Empty;
        }

        private void HandleQuickCheckout()
        {
            CommandSubmitted?.Invoke("계산대 봐줘");
        }

        private void HandleQuickRestock()
        {
            CommandSubmitted?.Invoke("라면 진열해줘");
        }

        private void HandleCancelWork()
        {
            CancelWorkClicked?.Invoke();
        }

        private void HandleCloseClicked()
        {
            CloseRequested?.Invoke();
        }
    }
}
