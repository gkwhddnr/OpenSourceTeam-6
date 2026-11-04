using OpenSourceTeam6.Camera.Core;
using UnityEngine;

namespace OpenSourceTeam6.Camera.Unity
{
    public sealed class FirstPersonCameraPresenter
    {
        private readonly Transform eyePivot;
        private readonly UnityEngine.Camera viewCamera;
        private readonly Renderer[] localVisuals;
        public FirstPersonCameraPresenter(Transform eye, UnityEngine.Camera camera, Renderer[] visuals)
        { eyePivot = eye; viewCamera = camera; localVisuals = visuals ?? new Renderer[0]; }
        private LookState state;

        public bool IsConfigured => eyePivot != null && viewCamera != null && viewCamera.transform.IsChildOf(eyePivot);
        public void Present(LookState next) { state = next; }

        public void Initialize()
        {
            if (!IsConfigured) throw new System.InvalidOperationException("Assign the eye pivot and its child camera.");
            foreach (var visual in localVisuals) if (visual != null) visual.enabled = false;
        }

        public void Apply()
        {
            eyePivot.localRotation = Quaternion.Euler(state.PitchDegrees, 0f, 0f);
        }
    }
}
