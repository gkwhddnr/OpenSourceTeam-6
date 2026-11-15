using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 기능정의서 UI-09 (NPC 머리 위 상태 표시 - World UI):
    /// 3D 공간 내 알바 NPC 머리 위에 이름, 현재 수행 상태, 감정 색상 및 반응 한 줄 말풍선을 표시한다.
    /// </summary>
    public sealed class EmployeeWorldStatusPresenter : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 headOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField] private Image statusHeaderBg;
        [SerializeField] private Text statusHeaderText;
        [SerializeField] private GameObject speechBubbleRoot;
        [SerializeField] private Image speechBubbleBg;
        [SerializeField] private Text speechBubbleText;
        [SerializeField] private float bubbleDisplaySeconds = 4.5f;

        private float bubbleTimer;

        private static readonly Color HeaderStableColor = new Color(0.10f, 0.42f, 0.32f, 0.92f);
        private static readonly Color HeaderComplainColor = new Color(0.56f, 0.42f, 0.10f, 0.92f);
        private static readonly Color HeaderAngryColor = new Color(0.60f, 0.16f, 0.18f, 0.94f);

        private static readonly Color BubbleNormalColor = new Color(0.12f, 0.18f, 0.26f, 0.95f);
        private static readonly Color BubbleRefuseColor = new Color(0.50f, 0.12f, 0.15f, 0.96f);

        private void LateUpdate()
        {
            if (followTarget != null)
            {
                transform.position = followTarget.position + headOffset;
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }

            if (bubbleTimer > 0f)
            {
                bubbleTimer -= Time.deltaTime;
                if (bubbleTimer <= 0f && speechBubbleRoot != null)
                {
                    speechBubbleRoot.SetActive(false);
                }
            }
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
        }

        public void RenderSnapshot(EmployeeHudSnapshot snapshot)
        {
            StressBand band = StressRules.Classify(snapshot.Stress);
            string roleLabel = snapshot.CurrentRole == StaffWorkRole.Checkout
                ? "계산 중"
                : (snapshot.CurrentRole == StaffWorkRole.Restock ? "진열 중" : "대기/휴식");
            string bandLabel = band == StressBand.Angry
                ? "분노"
                : (band == StressBand.Complaining ? "불만" : "안정");

            if (statusHeaderText != null)
            {
                statusHeaderText.text = $"{snapshot.EmployeeName}  ·  {roleLabel} [{bandLabel}]";
            }

            if (statusHeaderBg != null)
            {
                statusHeaderBg.color = band == StressBand.Angry
                    ? HeaderAngryColor
                    : (band == StressBand.Complaining ? HeaderComplainColor : HeaderStableColor);
            }
        }

        public void ShowSpeechBubble(string dialogue, CommandFeedbackKind kind)
        {
            if (speechBubbleRoot == null || speechBubbleText == null) return;

            if (string.IsNullOrWhiteSpace(dialogue))
            {
                speechBubbleRoot.SetActive(false);
                bubbleTimer = 0f;
                return;
            }

            speechBubbleText.text = $"\"{dialogue.Trim()}\"";
            if (speechBubbleBg != null)
            {
                speechBubbleBg.color = kind == CommandFeedbackKind.RefusedByEmotion
                    ? BubbleRefuseColor
                    : BubbleNormalColor;
            }

            speechBubbleRoot.SetActive(true);
            bubbleTimer = Mathf.Max(1.5f, bubbleDisplaySeconds);
        }
    }
}
