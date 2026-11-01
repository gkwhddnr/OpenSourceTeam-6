using OpenSourceTeam6.Camera.Core;
using UnityEngine;

namespace OpenSourceTeam6.Camera.Unity
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonCameraPresenter : MonoBehaviour
    {
        [SerializeField] private Transform eyePivot;
        [SerializeField] private UnityEngine.Camera viewCamera;
        [SerializeField] private Renderer[] localVisuals = new Renderer[0];
        private LookState state;

        public bool IsConfigured => eyePivot != null && viewCamera != null && viewCamera.transform.IsChildOf(eyePivot);
        public void Present(LookState next) { state = next; }

        private void Awake()
        {
            if (!IsConfigured) { Debug.LogError("Assign the eye pivot and its child camera.", this); enabled = false; return; }
            foreach (var visual in localVisuals) if (visual != null) visual.enabled = false;
        }

        private void LateUpdate()
        {
            eyePivot.localRotation = Quaternion.Euler(state.PitchDegrees, 0f, 0f);
        }
    }
}
