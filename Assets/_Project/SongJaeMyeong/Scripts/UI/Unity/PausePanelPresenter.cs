using System;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 기능정의서 UI-05 (일시정지 화면) 담당 컴포넌트.
    /// Unity 직렬화(MonoScript GUID 매칭)를 위해 반드시 클래스명과 동일한 파일에 둔다.
    /// </summary>
    public sealed class PausePanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button openSettingsButton;
        [SerializeField] private Button returnLobbyButton;

        public bool IsOpen => (panelRoot != null ? panelRoot : gameObject).activeSelf;

        public event Action ResumeClicked;
        public event Action OpenSettingsClicked;
        public event Action ReturnLobbyClicked;

        private void OnEnable()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(HandleResumeClicked);
            if (openSettingsButton != null) openSettingsButton.onClick.AddListener(HandleSettingsClicked);
            if (returnLobbyButton != null) returnLobbyButton.onClick.AddListener(HandleReturnLobbyClicked);
        }

        private void OnDisable()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(HandleResumeClicked);
            if (openSettingsButton != null) openSettingsButton.onClick.RemoveListener(HandleSettingsClicked);
            if (returnLobbyButton != null) returnLobbyButton.onClick.RemoveListener(HandleReturnLobbyClicked);
        }

        public void SetOpen(bool open)
        {
            GameObject target = panelRoot != null ? panelRoot : gameObject;
            if (target.activeSelf != open)
            {
                target.SetActive(open);
            }
        }

        private void HandleResumeClicked() => ResumeClicked?.Invoke();
        private void HandleSettingsClicked() => OpenSettingsClicked?.Invoke();
        private void HandleReturnLobbyClicked() => ReturnLobbyClicked?.Invoke();
    }
}
