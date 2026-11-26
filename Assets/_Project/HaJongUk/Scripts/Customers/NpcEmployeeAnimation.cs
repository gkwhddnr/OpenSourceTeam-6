using UnityEngine;
using UnityEngine.AI;

namespace OpenSourceTeam6.Customers.Unity
{
    [DisallowMultipleComponent]
    public sealed class NpcEmployeeAnimation : MonoBehaviour
    {
        [SerializeField] private NpcCharacterAnimation presenter;
        [SerializeField] private NpcCharacterRoster characterRoster;
        [SerializeField] private GameObject employeeIdentifierPrefab;
        [SerializeField] private Renderer placeholderRenderer;
        [SerializeField] private Transform stockTarget;
        [SerializeField] private Transform pointTarget;
        [SerializeField] private Transform registerTarget;
        [SerializeField] private Transform seatAnchor;
        [SerializeField] private Transform trashTarget;
        [SerializeField] private GameObject trashPreview;
        [SerializeField] private GameObject stockPreview;
        [SerializeField] private GameObject broomPreview;
        [SerializeField, Min(0.1f)] private float workCycleSeconds = 2f;

        private float cycleElapsed;
        private bool paused;
        private bool previewTrashWalk;
        private Vector3 trashPreviewDestination;
        private bool stockOnce;
        private Renderer stockPropRenderer;
        private Renderer trashPropRenderer;
        private const float CheckoutSeconds = 0.75f;
        private const float TrashSeconds = 1.25f;
        private const float TrashReleasePhase = 0.68f;

        public NpcAnimationState CurrentState { get; private set; } = NpcAnimationState.Idle;
        public event System.Action CheckoutAnimationFinished;

        private void Awake()
        {
            if (stockPreview != null) stockPropRenderer = stockPreview.GetComponent<Renderer>();
            if (trashPreview != null) trashPropRenderer = trashPreview.GetComponent<Renderer>();
            if (presenter == null)
                presenter = GetComponentInChildren<NpcCharacterAnimation>();
            if (characterRoster != null)
                presenter = characterRoster.ApplyRandom(transform, presenter);
            if (presenter != null && placeholderRenderer != null)
                placeholderRenderer.enabled = false;
            AttachEmployeeIdentifier();
        }

        private void AttachEmployeeIdentifier()
        {
            if (presenter == null || employeeIdentifierPrefab == null)
                return;

            var animator = presenter.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
                return;

            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null)
                chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (chest == null)
                return;

            var marker = Instantiate(employeeIdentifierPrefab, chest, false);
            marker.name = "Employee identifier";
            marker.transform.SetPositionAndRotation(
                chest.position + presenter.transform.forward * 0.12f, presenter.transform.rotation);
        }

        private void OnEnable()
        {
            if (presenter == null)
                presenter = GetComponentInChildren<NpcCharacterAnimation>();
            presenter?.SetPaused(paused);
            ShowCurrentState();
        }

        private void Update()
        {
            if (paused) return;

            if (previewTrashWalk) WalkToTrashBin();
            AdvanceAction();
            ShowCurrentState();
        }

        private void WalkToTrashBin()
        {
            transform.position = Vector3.MoveTowards(transform.position, trashPreviewDestination, 1.25f * Time.deltaTime);
            if (transform.position == trashPreviewDestination)
                Play(NpcAnimationState.DiscardTrash);
        }

        private void AdvanceAction()
        {
            cycleElapsed += Time.deltaTime;
            float duration = ActionDuration;
            if (cycleElapsed < duration) return;

            switch (CurrentState)
            {
                case NpcAnimationState.SitDown:
                    Play(NpcAnimationState.Seated);
                    break;
                case NpcAnimationState.Checkout:
                    Play(NpcAnimationState.Idle);
                    CheckoutAnimationFinished?.Invoke();
                    break;
                case NpcAnimationState.DiscardTrash:
                case NpcAnimationState.Stock when stockOnce:
                    Play(NpcAnimationState.Idle);
                    break;
                default:
                    cycleElapsed = Mathf.Repeat(cycleElapsed, duration);
                    break;
            }
        }

        private void LateUpdate()
        {
            Transform hand = presenter != null ? presenter.RightHand : null;
            UpdateTrashPreview(hand);
            bool stocking = CurrentState == NpcAnimationState.Stock && hand != null;
            if (stockPreview != null)
            {
                if (stockPreview.activeSelf != stocking)
                    stockPreview.SetActive(stocking);
                if (stocking)
                {
                    float phase = cycleElapsed / Mathf.Max(0.1f, workCycleSeconds);
                    if (phase >= 0.65f && stockTarget != null)
                        stockPreview.transform.SetPositionAndRotation(stockTarget.position, stockTarget.rotation);
                    else
                        FollowGrip(stockPreview.transform, stockPropRenderer);
                }
            }

            bool cleaning = CurrentState == NpcAnimationState.Clean && hand != null;
            if (broomPreview != null)
            {
                if (broomPreview.activeSelf != cleaning)
                    broomPreview.SetActive(cleaning);
                if (cleaning)
                {
                    Vector3 floor = hand.position + transform.forward * 0.32f;
                    floor.y = transform.position.y + 0.08f;
                    Vector3 shaft = hand.position - floor;
                    if (shaft.sqrMagnitude > 0.001f)
                        broomPreview.transform.SetPositionAndRotation(floor,
                            Quaternion.FromToRotation(Vector3.up, shaft.normalized));
                }
            }
        }

        private void UpdateTrashPreview(Transform hand)
        {
            if (trashPreview == null) return;
            bool holding = hand != null && (CurrentState == NpcAnimationState.CarryTrash ||
                (CurrentState == NpcAnimationState.DiscardTrash && cycleElapsed < TrashSeconds * TrashReleasePhase));
            if (trashPreview.activeSelf != holding) trashPreview.SetActive(holding);
            if (holding)
                FollowGrip(trashPreview.transform, trashPropRenderer);
        }

        private void FollowGrip(Transform prop, Renderer propRenderer)
        {
            float halfHeight = propRenderer != null
                ? propRenderer.localBounds.extents.y * Mathf.Abs(prop.lossyScale.y) : 0.035f;
            if (!presenter.TryGetRightHandGrip(halfHeight, out Pose grip)) return;
            Vector3 pivotOffset = propRenderer != null
                ? Vector3.Scale(propRenderer.localBounds.center, prop.lossyScale) : Vector3.zero;
            prop.SetPositionAndRotation(grip.position - grip.rotation * pivotOffset, grip.rotation);
        }

        private void OnDisable()
        {
            previewTrashWalk = false;
            if (stockPreview != null) stockPreview.SetActive(false);
            if (broomPreview != null) broomPreview.SetActive(false);
            if (trashPreview != null) trashPreview.SetActive(false);
        }

        public bool SitAt(Transform anchor)
        {
            if (anchor == null || !CanPlay(NpcAnimationState.SitDown) || !CanPlay(NpcAnimationState.Seated))
                return false;
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled)
            {
                if (!agent.isOnNavMesh || agent.pathPending ||
                    (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.05f) ||
                    !agent.Warp(anchor.position))
                    return false;
            }
            else
            {
                transform.position = anchor.position;
            }
            transform.rotation = anchor.rotation;
            return Play(NpcAnimationState.SitDown);
        }

        public bool SitAtConfiguredSeat() => SitAt(seatAnchor);

        // The demo scenes have an unobstructed path and no navigation agent on the employee.
        public bool PreviewTrashDisposal()
        {
            if (trashTarget == null || GetComponent<NavMeshAgent>() != null ||
                !CanPlay(NpcAnimationState.CarryTrash))
                return false;

            Vector3 delta = Vector3.ProjectOnPlane(trashTarget.position - transform.position, Vector3.up);
            if (delta.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(delta);
            trashPreviewDestination = transform.position + delta.normalized * Mathf.Max(0f, delta.magnitude - 0.38f);
            Play(NpcAnimationState.CarryTrash);
            previewTrashWalk = true;
            return true;
        }

        public bool PlayStockOnce()
        {
            if (!CanPlay(NpcAnimationState.Stock)) return false;
            cycleElapsed = 0f;
            stockOnce = true;
            return Play(NpcAnimationState.Stock);
        }

        public bool Play(NpcAnimationState state)
        {
            switch (state)
            {
                case NpcAnimationState.Idle:
                case NpcAnimationState.Walk:
                case NpcAnimationState.Stock:
                case NpcAnimationState.Clean:
                case NpcAnimationState.Rest:
                case NpcAnimationState.Point:
                case NpcAnimationState.Anger:
                case NpcAnimationState.SitDown:
                case NpcAnimationState.Seated:
                case NpcAnimationState.CarryTrash:
                    break;
                case NpcAnimationState.Checkout:
                    if (registerTarget == null) return false;
                    break;
                case NpcAnimationState.DiscardTrash:
                    if (trashTarget == null) return false;
                    break;
                default:
                    return false;
            }

            if (CurrentState != state)
            {
                CurrentState = state;
                cycleElapsed = 0f;
                previewTrashWalk = false;
                if (state != NpcAnimationState.Stock) stockOnce = false;
            }

            ShowCurrentState();
            return true;
        }

        public bool CanPlay(NpcAnimationState state) =>
            isActiveAndEnabled && presenter != null && presenter.CanShow(state);

        public void SetAngerGesture(AngerGesture gesture, float progress, Vector3 target) =>
            presenter?.SetAngerGesture(gesture, progress, target);

        public void SetPaused(bool value)
        {
            paused = value;
            presenter?.SetPaused(value);
        }

        private void ShowCurrentState()
        {
            if (presenter == null)
                return;

            Transform target = CurrentState switch
            {
                NpcAnimationState.Stock => stockTarget,
                NpcAnimationState.Point => pointTarget,
                NpcAnimationState.Checkout => registerTarget,
                NpcAnimationState.DiscardTrash => trashTarget,
                _ => null
            };
            presenter.Show(CurrentState, target != null ? (Vector3?)target.position : null,
                cycleElapsed / ActionDuration);
        }

        private float ActionDuration => CurrentState switch
        {
            NpcAnimationState.Checkout => CheckoutSeconds,
            NpcAnimationState.SitDown => NpcCharacterAnimation.SitDownSeconds,
            NpcAnimationState.DiscardTrash => TrashSeconds,
            _ => Mathf.Max(0.1f, workCycleSeconds)
        };
    }
}
