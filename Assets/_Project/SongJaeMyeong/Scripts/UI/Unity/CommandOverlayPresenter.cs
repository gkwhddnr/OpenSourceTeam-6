using System;
using UnityEngine;
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
            if (submitButton != null) submitButton.onClick.AddListener(HandleSubmitClicked);
            if (quickCheckoutButton != null) quickCheckoutButton.onClick.AddListener(HandleQuickCheckout);
            if (quickRestockButton != null) quickRestockButton.onClick.AddListener(HandleQuickRestock);
            if (cancelWorkButton != null) cancelWorkButton.onClick.AddListener(HandleCancelWork);
            if (closeButton != null) closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            if (submitButton != null) submitButton.onClick.RemoveListener(HandleSubmitClicked);
            if (quickCheckoutButton != null) quickCheckoutButton.onClick.RemoveListener(HandleQuickCheckout);
            if (quickRestockButton != null) quickRestockButton.onClick.RemoveListener(HandleQuickRestock);
            if (cancelWorkButton != null) cancelWorkButton.onClick.RemoveListener(HandleCancelWork);
            if (closeButton != null) closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        private void Update()
        {
            if (!IsOpen) return;

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

            if (open && commandInputField != null)
            {
                commandInputField.text = string.Empty;
                commandInputField.Select();
                commandInputField.ActivateInputField();
            }
        }

        private void HandleSubmitClicked()
        {
            if (commandInputField == null) return;
            string text = commandInputField.text;
            if (string.IsNullOrWhiteSpace(text)) return;

            string trimmed = text.Trim();
            commandInputField.text = string.Empty;
            CommandSubmitted?.Invoke(trimmed);
        }

        private void HandleQuickCheckout()
        {
            CommandSubmitted?.Invoke("계산대 업무 봐줘");
        }

        private void HandleQuickRestock()
        {
            CommandSubmitted?.Invoke("진열대에 물건 채워줘");
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
