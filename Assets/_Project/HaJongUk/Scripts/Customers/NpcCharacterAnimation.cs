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
        Anger,
        Checkout,
        SitDown,
        Seated,
        DiscardTrash,
        CarryTrash
    }

    public enum AngerGesture { Shout, Finger, Frown, CounterStrike }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class NpcCharacterAnimation : MonoBehaviour
    {
        internal const float SitDownSeconds = 1.15f;
        private const int BaseLayer = 0;
        private const float TransitionSeconds = 0.12f;

        private Animator animator;
        private Transform upperArm;
        private Transform forearm;
        private Transform rightHand;
        private Transform rightIndexBase;
        private Transform rightMiddleBase;
        private Transform rightLittleBase;
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
        private NpcFacialExpression facialExpression;

        public void SetAngerGesture(AngerGesture gesture, float progress, Vector3 target)
        {
            angerGesture = gesture;
            angerProgress = progress;
            strikeTarget = target;
        }

        private void LateUpdate()
        {
            bool shouting = shownState == NpcAnimationState.Anger && angerGesture == AngerGesture.Shout;
            if (!shouting && angerGesture != AngerGesture.Frown)
            {
                facialExpression?.Clear();
                return;
            }

            facialExpression ??= new NpcFacialExpression(animator);
            float progress = shouting && angerProgress == 0f ? interactionProgress : angerProgress;
            facialExpression.Show(shouting, ReachEnvelope(progress));
        }

        private void OnDisable() => facialExpression?.Clear();

        public Transform RightHand
        {
            get
            {
                CacheRig();
                return rightHand;
            }
        }

        public bool TryGetRightHandGrip(float propHalfHeight, out Pose grip)
        {
            CacheRig();
            grip = default;
            if (rightHand == null) return false;

            if (!TryGetPalmAxes(out Vector3 fingers, out Vector3 back))
            {
                grip = new Pose(rightHand.position + transform.forward * 0.045f -
                    transform.up * (propHalfHeight + 0.008f), transform.rotation);
                return true;
            }

            Vector3 palmCenter = Vector3.Lerp(rightHand.position, rightMiddleBase.position, 0.72f);
            grip = new Pose(palmCenter - back * (propHalfHeight + 0.008f), Quaternion.LookRotation(fingers, back));
            return true;
        }

        public NpcAnimationState CurrentState => shownState ?? NpcAnimationState.Idle;

        private void Awake() => CacheRig();

        public bool CanShow(NpcAnimationState state)
        {
            CacheRig();
            return animator.isActiveAndEnabled && animator.layerCount > BaseLayer &&
                animator.HasState(BaseLayer, StateHash(state));
        }

        private int StateHash(NpcAnimationState state)
        {
            var clip = state switch
            {
                NpcAnimationState.Checkout or NpcAnimationState.DiscardTrash => NpcAnimationState.Idle,
                NpcAnimationState.CarryTrash => NpcAnimationState.Walk,
                _ => state
            };
            return Animator.StringToHash(animator.GetLayerName(BaseLayer) + "." + clip);
        }

        public void Show(NpcAnimationState state, Vector3? target = null, float progress = 0f)
        {
            CacheRig();
            interactionTarget = target;
            interactionProgress = Mathf.Clamp01(progress);
            if (shownState == state || !animator.isActiveAndEnabled || animator.layerCount <= BaseLayer)
                return;

            int stateHash = StateHash(state);
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
            rightIndexBase = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            rightMiddleBase = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            rightLittleBase = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
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
                    float windup = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.28f, angerProgress));
                    float lean = Mathf.Lerp(-4f * windup, 15f, down) * (1f - recover);
                    Vector3 leanAxis = Vector3.Cross(up, toward).normalized;
                    BendTorso(HumanBodyBones.Spine, leanAxis, lean * 0.65f);
                    BendTorso(HumanBodyBones.Chest, leanAxis, lean * 0.35f);
                }
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
                animator.SetIKPosition(AvatarIKGoal.RightHand, ClampReach(requested, reach));
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);
                animator.SetIKHintPosition(AvatarIKHint.RightElbow, elbow);
                Quaternion handAxes = finger
                    ? Quaternion.LookRotation(up, toward)
                    : Quaternion.LookRotation((toward + Vector3.down * 0.33f).normalized, outward);
                PoseRightHand(handAxes, finger ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LastBone, weight);
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
                if (shownState == NpcAnimationState.Point)
                {
                    Vector3 aim = (requested - upperArm.position).normalized;
                    Vector3 palmUp = Vector3.ProjectOnPlane(transform.up, aim);
                    if (palmUp.sqrMagnitude < 0.01f)
                        palmUp = Vector3.ProjectOnPlane(transform.forward, aim);
                    PoseRightHand(Quaternion.LookRotation(aim, palmUp), HumanBodyBones.RightIndexProximal, smoothedWeight);
                }
                else if (shownState is NpcAnimationState.Stock or NpcAnimationState.CarryTrash or NpcAnimationState.DiscardTrash)
                {
                    float gripWeight = shownState == NpcAnimationState.CarryTrash
                        ? smoothedWeight : smoothedWeight * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 0.8f, interactionProgress)));
                    Quaternion gripAxes = Quaternion.LookRotation(transform.forward, transform.up);
                    PoseRightHand(gripAxes, HumanBodyBones.LastBone, gripWeight, holdingProp: true);
                }
            }
        }

        private void BendTorso(HumanBodyBones joint, Vector3 worldAxis, float angle)
        {
            Transform bone = animator.GetBoneTransform(joint);
            if (bone != null)
                animator.SetBoneLocalRotation(joint,
                    bone.localRotation * Quaternion.AngleAxis(angle, bone.InverseTransformDirection(worldAxis)));
        }

        private bool TryGetPalmAxes(out Vector3 fingers, out Vector3 back)
        {
            fingers = back = Vector3.zero;
            if (rightIndexBase == null || rightLittleBase == null || rightMiddleBase == null) return false;
            fingers = (rightMiddleBase.position - rightHand.position).normalized;
            back = Vector3.Cross(fingers, rightLittleBase.position - rightIndexBase.position).normalized;
            return back != Vector3.zero;
        }

        private void PoseRightHand(Quaternion desiredAxes, HumanBodyBones extendedFinger, float weight, bool holdingProp = false)
        {
            if (!TryGetPalmAxes(out Vector3 fingers, out Vector3 back)) return;
            Quaternion currentAxes = Quaternion.LookRotation(fingers, back);
            // The humanoid goal and imported wrist bone can have different local axes.
            Quaternion goal = animator.GetIKRotation(AvatarIKGoal.RightHand);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKRotation(AvatarIKGoal.RightHand, desiredAxes * Quaternion.Inverse(currentAxes) * goal);

            for (int i = (int)HumanBodyBones.RightIndexProximal; i <= (int)HumanBodyBones.RightLittleDistal; i++)
            {
                var joint = (HumanBodyBones)i;
                bool extended = i >= (int)extendedFinger && i < (int)extendedFinger + 3;
                Transform bone = animator.GetBoneTransform(joint);
                if (bone == null) continue;
                Vector3 segment = bone.childCount > 0 ? bone.GetChild(0).position - bone.position : bone.position - bone.parent.position;
                Vector3 axis = Vector3.Cross(segment, -back);
                if (axis.sqrMagnitude < 0.000001f) continue;
                int jointIndex = (i - (int)HumanBodyBones.RightIndexProximal) % 3;
                float currentCurl = Vector3.SignedAngle(jointIndex == 0 ? fingers : bone.position - bone.parent.position, segment, axis);
                float desiredCurl = extended ? 0f : jointIndex == 2 ? (holdingProp ? 18f : 35f)
                    : (holdingProp ? 32f : 65f);
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
                case NpcAnimationState.Checkout:
                    weight = ReachEnvelope(interactionProgress);
                    float tap = 1f - Mathf.Abs(Mathf.Sin(interactionProgress * Mathf.PI * 4f));
                    return (interactionTarget ?? fallback) + transform.up * (0.055f * tap);
                case NpcAnimationState.CarryTrash:
                    weight = 0.9f;
                    return TrashCarryPosition(reach);
                case NpcAnimationState.DiscardTrash:
                    float lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.58f, interactionProgress));
                    weight = Mathf.Lerp(0.9f, 1f, lower) *
                        (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.99f, interactionProgress)));
                    return Vector3.Lerp(TrashCarryPosition(reach), interactionTarget ?? fallback, lower);
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

        private Vector3 TrashCarryPosition(float reach) =>
            upperArm.position + reach * (transform.forward * 0.38f - transform.up * 0.45f);

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
