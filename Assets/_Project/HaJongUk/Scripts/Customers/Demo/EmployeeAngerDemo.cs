using OpenSourceTeam6.Customers.Unity;
using UnityEngine;

namespace OpenSourceTeam6.Customers.Demo
{
    public sealed class EmployeeAngerDemo : MonoBehaviour
    {
        [SerializeField] private NpcEmployeeAnimation employee;
        [SerializeField] private Transform strikePoint;
        private const float Duration = 2f;
        private float elapsed = Duration;
        private AngerGesture gesture;

        public void Preview(AngerGesture reaction)
        {
            gesture = reaction;
            elapsed = 0f;
            employee.Play(reaction == AngerGesture.Shout ? NpcAnimationState.Anger : NpcAnimationState.Idle);
            employee.SetAngerGesture(gesture, 0f, ReactionTarget());
        }

        private void Update()
        {
            if (elapsed >= Duration) return;
            elapsed += Time.deltaTime;
            if (elapsed < Duration)
            {
                employee.SetAngerGesture(gesture, elapsed / Duration, ReactionTarget());
                return;
            }
            employee.SetAngerGesture(AngerGesture.Shout, 0f, Vector3.zero);
            employee.Play(NpcAnimationState.Idle);
        }

        private Vector3 ReactionTarget() => gesture == AngerGesture.Finger && Camera.main != null
            ? Camera.main.transform.position : strikePoint.position;

        private void OnGUI()
        {
            if (employee == null || strikePoint == null) return;
            GUILayout.BeginArea(new Rect(16, 16, 195, 205), GUI.skin.box);
            GUILayout.Label("직원 분노 애니메이션");
            if (GUILayout.Button("소리치기")) Preview(AngerGesture.Shout);
            if (GUILayout.Button("손가락 제스처")) Preview(AngerGesture.Finger);
            if (GUILayout.Button("찡그린 표정")) Preview(AngerGesture.Frown);
            if (GUILayout.Button("카운터 내려치기")) Preview(AngerGesture.CounterStrike);
            GUILayout.EndArea();
        }
    }
}
