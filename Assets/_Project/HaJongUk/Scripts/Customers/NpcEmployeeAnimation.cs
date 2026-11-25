using UnityEngine;

namespace OpenSourceTeam6.Customers.Unity
{
    [DisallowMultipleComponent]
    public sealed class NpcEmployeeAnimation : MonoBehaviour
    {
        [SerializeField] private NpcCharacterAnimation presenter;
        [SerializeField] private NpcCharacterRoster characterRoster;
        [SerializeField] private GameObject employeeIdentifierPrefab;
        [SerializeField] private Transform stockTarget;
        [SerializeField] private Transform pointTarget;
        [SerializeField] private GameObject stockPreview;
        [SerializeField] private GameObject broomPreview;
        [SerializeField, Min(0.1f)] private float workCycleSeconds = 2f;

        private float cycleElapsed;
        private bool paused;

        public NpcAnimationState CurrentState { get; private set; } = NpcAnimationState.Idle;

        private void Awake()
        {
            if (presenter == null)
                presenter = GetComponentInChildren<NpcCharacterAnimation>();
            if (characterRoster != null)
                presenter = characterRoster.ApplyRandom(transform, presenter);
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
            if (paused)
                return;

            float duration = Mathf.Max(0.1f, workCycleSeconds);
            cycleElapsed = Mathf.Repeat(cycleElapsed + Time.deltaTime, duration);
            ShowCurrentState();
        }

        private void LateUpdate()
        {
            Transform hand = presenter != null ? presenter.RightHand : null;
            bool stocking = CurrentState == NpcAnimationState.Stock && hand != null;
            if (stockPreview != null)
            {
                if (stockPreview.activeSelf != stocking)
                    stockPreview.SetActive(stocking);
                if (stocking)
                {
                    float phase = cycleElapsed / Mathf.Max(0.1f, workCycleSeconds);
                    stockPreview.transform.position = phase >= 0.51f && stockTarget != null
                        ? stockTarget.position : hand.position;
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

        private void OnDisable()
        {
            if (stockPreview != null) stockPreview.SetActive(false);
            if (broomPreview != null) broomPreview.SetActive(false);
        }

        public bool Play(NpcAnimationState state)
        {
            switch (state)
            {
                case NpcAnimationState.Idle:
                case NpcAnimationState.Stock:
                case NpcAnimationState.Clean:
                case NpcAnimationState.Rest:
                case NpcAnimationState.Point:
                case NpcAnimationState.Anger:
                    break;
                default:
                    return false;
            }

            if (CurrentState != state)
            {
                CurrentState = state;
                cycleElapsed = 0f;
            }

            ShowCurrentState();
            return true;
        }

        public void SetPaused(bool value)
        {
            paused = value;
            presenter?.SetPaused(value);
        }

        private void ShowCurrentState()
        {
            if (presenter == null)
                return;

            Transform target = CurrentState == NpcAnimationState.Stock ? stockTarget
                : CurrentState == NpcAnimationState.Point ? pointTarget : null;
            presenter.Show(CurrentState, target != null ? (Vector3?)target.position : null,
                cycleElapsed / Mathf.Max(0.1f, workCycleSeconds));
        }
    }
}
