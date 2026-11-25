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

    public enum AngerGesture { Shout, Finger, Frown, CounterStrike }

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
        private AngerGesture angerGesture;
        private Vector3 strikeTarget;
        private float angerProgress;
        private readonly Transform[] brows = new Transform[2];
        private readonly Vector3[] browPositions = new Vector3[2];

        public void SetAngerGesture(AngerGesture gesture, float progress, Vector3 target)
        {
            if (angerGesture != gesture)
            {
                for (int i = 0; i < brows.Length; i++)
                    if (brows[i] != null) brows[i].localPosition = browPositions[i];

                if (gesture == AngerGesture.Frown)
                {
                    foreach (Transform bone in GetComponentsInChildren<Transform>(true))
                    {
                        int index = bone.name == "EyebrowInner_L" ? 0 : bone.name == "EyebrowInner_R" ? 1 : -1;
                        if (index < 0) continue;
                        brows[index] = bone;
                        browPositions[index] = bone.localPosition;
                    }
                }
            }
            angerGesture = gesture;
            angerProgress = progress;
            strikeTarget = target;
        }

        private void LateUpdate()
        {
            if (angerGesture != AngerGesture.Frown) return;
            float amount = ReachEnvelope(angerProgress) * 0.025f;
            for (int i = 0; i < brows.Length; i++)
            {
                Transform brow = brows[i];
                if (brow != null)
                    brow.localPosition = browPositions[i] + brow.parent.InverseTransformVector(-transform.up * amount);
            }
        }

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

        public bool CanShow(NpcAnimationState state)
        {
            CacheRig();
            return animator.isActiveAndEnabled && animator.layerCount > BaseLayer &&
                animator.HasState(BaseLayer, Animator.StringToHash(animator.GetLayerName(BaseLayer) + "." + state));
        }

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
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            if (angerGesture == AngerGesture.Finger || angerGesture == AngerGesture.CounterStrike)
            {
                bool finger = angerGesture == AngerGesture.Finger;
                Vector3 up = transform.up;
                Vector3 toward = Vector3.ProjectOnPlane(strikeTarget - transform.position, up).normalized;
                if (toward == Vector3.zero) toward = transform.forward;
                Vector3 outward = transform.right;
                Vector3 elbow = upperArm.position + reach * (outward * 0.65f - up * 0.65f + toward * 0.12f);
                weight = ReachEnvelope(angerProgress);
                if (finger)
                {
                    requested = upperArm.position + reach * (toward * 0.45f + outward * 0.15f - up * 0.04f);
                }
                else
                {
                    Vector3 contact = strikeTarget - toward * 0.07f + up * 0.045f;
                    Vector3 raised = contact + up * 0.34f;
                    float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.45f, angerProgress));
                    float recover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.53f, 0.86f, angerProgress));
                    requested = Vector3.Lerp(Vector3.Lerp(raised, contact, down), raised, recover);
                    elbow = upperArm.position + reach * (outward * 0.45f - up * 0.23f - toward * 0.35f);
                }
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
                animator.SetIKPosition(AvatarIKGoal.RightHand, ClampReach(requested, reach));
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);
                animator.SetIKHintPosition(AvatarIKHint.RightElbow, elbow);
                OrientAngerHand(finger, toward, weight);
                hasSmoothedTarget = false;
                smoothedWeight = 0f;
                return;
            }
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

        private void OrientAngerHand(bool extendMiddle, Vector3 toward, float weight)
        {
            Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            Transform middleBase = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (index == null || little == null || middleBase == null) return;

            Vector3 fingers = (middleBase.position - rightHand.position).normalized;
            Vector3 back = Vector3.Cross(fingers, little.position - index.position).normalized;
            if (back == Vector3.zero) return;
            Quaternion currentAxes = Quaternion.LookRotation(fingers, back);
            Quaternion desiredAxes = extendMiddle
                ? Quaternion.LookRotation(transform.up, toward)
                : Quaternion.LookRotation((toward + Vector3.down * 0.33f).normalized, transform.right);
            // The humanoid goal and imported wrist bone can have different local axes.
            Quaternion goal = animator.GetIKRotation(AvatarIKGoal.RightHand);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKRotation(AvatarIKGoal.RightHand, desiredAxes * Quaternion.Inverse(currentAxes) * goal);

            for (int i = (int)HumanBodyBones.RightIndexProximal; i <= (int)HumanBodyBones.RightLittleDistal; i++)
            {
                var joint = (HumanBodyBones)i;
                bool middle = i >= (int)HumanBodyBones.RightMiddleProximal && i <= (int)HumanBodyBones.RightMiddleDistal;
                Transform bone = animator.GetBoneTransform(joint);
                if (bone == null) continue;
                Vector3 segment = bone.childCount > 0 ? bone.GetChild(0).position - bone.position : bone.position - bone.parent.position;
                Vector3 axis = Vector3.Cross(segment, -back);
                if (axis.sqrMagnitude < 0.000001f) continue;
                int jointIndex = (i - (int)HumanBodyBones.RightIndexProximal) % 3;
                float currentCurl = Vector3.SignedAngle(jointIndex == 0 ? fingers : bone.position - bone.parent.position, segment, axis);
                float desiredCurl = extendMiddle && middle ? 0f : jointIndex == 2 ? 35f : 65f;
                float curl = Mathf.Clamp(desiredCurl - currentCurl, -65f, 65f);
                animator.SetBoneLocalRotation(joint, bone.localRotation *
                    Quaternion.AngleAxis(curl * weight, bone.InverseTransformDirection(axis.normalized)));
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
