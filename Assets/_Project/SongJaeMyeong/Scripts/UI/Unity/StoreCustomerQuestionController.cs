using System.Collections.Generic;
using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.UI.Unity
{
    using Time = UnityEngine.Time;

    [DisallowMultipleComponent]
    public sealed class StoreCustomerQuestionController : MonoBehaviour
    {
        [SerializeField] private NpcPopulationController population;
        [SerializeField] private InGameHudPresenter hud;
        [SerializeField] private StoreUiSessionController session;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private NpcEmployeeAnimation employee;

        private readonly List<Question> questions = new();

        private readonly struct Question
        {
            public readonly int CustomerId;
            public readonly string ProductName;
            public readonly Transform Respondent;
            public readonly float EmployeeReplyAt;

            public Question(int customerId, string productName, Transform respondent)
            {
                CustomerId = customerId;
                ProductName = productName;
                Respondent = respondent;
                EmployeeReplyAt = Time.time + 0.8f;
            }
        }

        private void OnEnable()
        {
            if (population != null)
                population.CustomerAskedForProduct += OnCustomerAsked;
        }

        private void OnDisable()
        {
            if (population != null)
                population.CustomerAskedForProduct -= OnCustomerAsked;
            questions.Clear();
        }

        private void OnCustomerAsked(int customerId, string productName, Transform respondent)
        {
            questions.Add(new Question(customerId, productName, respondent));
            if (hud == null) return;

            bool asksPlayer = respondent == playerTarget;
            string responseHint = asksPlayer ? " [F] 답변" : string.Empty;
            hud.SetChatHistoryExpanded(true);
            hud.AppendChatLog($"손님 {customerId}",
                $"{(asksPlayer ? "사장님" : "직원분")}, {productName} 어디에 있나요?{responseHint}");
        }

        private void Update()
        {
            if (population == null || population.IsPaused || questions.Count == 0) return;

            for (int i = questions.Count - 1; i >= 0; i--)
            {
                Question question = questions[i];
                if (question.Respondent == null || !population.IsProductQuestionPending(question.CustomerId))
                    questions.RemoveAt(i);
                else if (question.Respondent != playerTarget && Time.time >= question.EmployeeReplyAt &&
                    (employee == null || employee.CurrentState == NpcAnimationState.Idle))
                    TryReply(i);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                TryReplyToPlayerQuestion();
        }

        public bool TryReplyToPlayerQuestion()
        {
            if (population == null || population.IsPaused || playerTarget == null ||
                (session != null && (session.CurrentMode != UiScreenMode.InGame || session.IsPauseMenuOpen ||
                    session.IsSettingsOpen || session.IsCommandOverlayOpen || session.IsExternalModalOpen ||
                    session.IsTransitionFading || session.IsOnboardingBlocking)))
                return false;

            for (int i = 0; i < questions.Count; i++)
                if (questions[i].Respondent == playerTarget && TryReply(i))
                    return true;
            return false;
        }

        private bool TryReply(int index)
        {
            Question question = questions[index];
            if (!population.TryResolveProductQuestion(question.CustomerId, question.Respondent, out Transform pickup))
                return false;

            questions.RemoveAt(index);
            bool employeeReply = question.Respondent != playerTarget;
            if (employeeReply && employee != null && pickup != null)
                employee.PlayPointOnce(pickup);
            if (hud != null)
                hud.AppendChatLog(employeeReply ? "알바" : "사장",
                    pickup != null ? $"{question.ProductName}은 진열대에 있습니다." :
                    $"죄송합니다. 지금 {question.ProductName}은 품절입니다.", employeeReply);
            return true;
        }
    }
}
