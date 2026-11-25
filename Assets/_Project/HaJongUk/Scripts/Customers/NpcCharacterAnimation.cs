using UnityEngine;

namespace OpenSourceTeam6.Customers.Unity
{
    public enum NpcAnimationState
    {
        Idle,
        Walk,
        Run,
        Pickup,
        Wait,
        Ask,
        Stock,
        Clean,
        Rest,
        Point,
        Anger
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class NpcCharacterAnimation : MonoBehaviour
    {
        private const int BaseLayer = 0;
        private const float TransitionSeconds = 0.12f;

        private Animator animator;
        private Transform upperArm;
        private Transform forearm;
        private Transform rightHand;
        private NpcAnimationState? shownState;
        private Vector3? interactionTarget;
        private float interactionProgress;
        private bool paused;
        private float speedBeforePause;
        private bool hasSmoothedTarget;
        private Vector3 smoothedTarget;
        private float smoothedWeight;

        public Transform RightHand
        {
            get
            {
                CacheRig();
                return rightHand;
            }
        }

        public NpcAnimationState CurrentState => shownState ?? NpcAnimationState.Idle;

        private void Awake() => CacheRig();

        public void Show(NpcAnimationState state, Vector3? target = null, float progress = 0f)
        {
            CacheRig();
            interactionTarget = target;
            interactionProgress = Mathf.Clamp01(progress);
            if (shownState == state || !animator.isActiveAndEnabled || animator.layerCount <= BaseLayer)
                return;

            int stateHash = Animator.StringToHash(animator.GetLayerName(BaseLayer) + "." + state);
            if (!animator.HasState(BaseLayer, stateHash))
                return;

            shownState = state;
            animator.CrossFadeInFixedTime(stateHash, TransitionSeconds, BaseLayer);
        }

        public void SetPaused(bool value)
        {
            CacheRig();
            if (paused == value)
                return;

            if (value)
                speedBeforePause = animator.speed;
            animator.speed = value ? 0f : speedBeforePause;
            paused = value;
        }

        private void CacheRig()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                // The navigation root, rather than the animation clip, owns NPC movement.
                animator.applyRootMotion = false;
            }

            if (rightHand != null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
                return;

            upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != BaseLayer || upperArm == null || forearm == null || rightHand == null)
                return;

            float reach = (Vector3.Distance(upperArm.position, forearm.position) +
                           Vector3.Distance(forearm.position, rightHand.position)) * 0.94f;
            Vector3 requested = CalculateTarget(reach, out float weight);
            float blend = paused ? 0f : 1f - Mathf.Exp(-16f * Time.deltaTime);
            Vector3 localTarget = transform.InverseTransformPoint(requested);
            if (!hasSmoothedTarget)
            {
                smoothedTarget = localTarget;
                hasSmoothedTarget = true;
            }

            smoothedTarget = Vector3.Lerp(smoothedTarget, localTarget, blend);
            smoothedWeight = Mathf.Lerp(smoothedWeight, weight, blend);
            if (weight == 0f && smoothedWeight < 0.001f)
            {
                smoothedWeight = 0f;
                hasSmoothedTarget = false;
            }

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, smoothedWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            if (smoothedWeight > 0f)
            {
                Vector3 worldTarget = transform.TransformPoint(smoothedTarget);
                animator.SetIKPosition(AvatarIKGoal.RightHand, ClampReach(worldTarget, reach));
            }
        }

        private Vector3 CalculateTarget(float reach, out float weight)
        {
            Vector3 shoulder = upperArm.position;
            Vector3 fallback = shoulder + reach * (transform.forward * 0.75f - transform.up * 0.35f);
            weight = 0f;
            switch (shownState)
            {
                case NpcAnimationState.Pickup:
                    weight = ReachEnvelope(interactionProgress);
                    return interactionTarget ?? fallback;
                case NpcAnimationState.Stock:
                    float phase = interactionProgress > 0f
                        ? interactionProgress
                        : Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(BaseLayer).normalizedTime, 1f);
                    weight = ReachEnvelope(phase);
                    return interactionTarget ?? fallback;
                case NpcAnimationState.Point:
                    Vector3 direction = (interactionTarget ?? (shoulder + transform.forward)) - shoulder;
                    if (direction.sqrMagnitude < 0.0001f)
                        direction = transform.forward;
                    weight = 1f;
                    return shoulder + direction.normalized * reach;
                default:
                    return rightHand.position;
            }
        }

        private Vector3 ClampReach(Vector3 target, float reach)
        {
            Vector3 shoulder = upperArm.position;
            return shoulder + Vector3.ClampMagnitude(target - shoulder, reach);
        }

        private static float ReachEnvelope(float progress)
        {
            float extend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.43f, progress));
            float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.97f, progress));
            return extend * release;
        }
    }
}
