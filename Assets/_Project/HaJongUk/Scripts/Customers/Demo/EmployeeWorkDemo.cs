using OpenSourceTeam6.Customers.Unity;
using UnityEngine;

namespace OpenSourceTeam6.Customers.Demo
{
    public sealed class EmployeeWorkDemo : MonoBehaviour
    {
        [SerializeField] private NpcEmployeeAnimation employee;
        private Vector3 standingPosition;
        private Quaternion standingRotation;

        private void Awake()
        {
            if (employee == null) return;
            standingPosition = employee.transform.position;
            standingRotation = employee.transform.rotation;
        }

        private void OnGUI()
        {
            if (employee == null) return;
            GUILayout.BeginArea(new Rect(16, 16, 195, 175), GUI.skin.box);
            GUILayout.Label("Employee work animation");
            if (GUILayout.Button("Use POS")) Play(NpcAnimationState.Checkout);
            if (GUILayout.Button("Sit")) employee.SitAtConfiguredSeat();
            if (GUILayout.Button("Stock shelf"))
            {
                ReturnToStation();
                employee.PlayStockOnce();
            }
            if (GUILayout.Button("Take trash to bin"))
            {
                ReturnToStation();
                employee.PreviewTrashDisposal();
            }
            if (GUILayout.Button("Idle")) Play(NpcAnimationState.Idle);
            GUILayout.EndArea();
        }

        private void Play(NpcAnimationState state)
        {
            ReturnToStation();
            employee.Play(state);
        }

        private void ReturnToStation()
        {
            employee.transform.SetPositionAndRotation(standingPosition, standingRotation);
        }
    }
}
