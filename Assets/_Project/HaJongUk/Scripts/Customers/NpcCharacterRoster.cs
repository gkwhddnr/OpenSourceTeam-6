using System;
using UnityEngine;
using UnityEngine.AI;

namespace OpenSourceTeam6.Customers.Unity
{
    [CreateAssetMenu(menuName = "HaJongUk/NPC Character Roster")]
    public sealed class NpcCharacterRoster : ScriptableObject
    {
        [SerializeField] private GameObject[] characterPrefabs = Array.Empty<GameObject>();
        [SerializeField] private RuntimeAnimatorController animatorController;

        public int Count => characterPrefabs?.Length ?? 0;

        public NpcCharacterAnimation ApplyRandom(Transform owner, NpcCharacterAnimation fallback)
        {
            if (owner == null || animatorController == null || Count == 0)
                return fallback;

            var previousVisual = fallback != null ? FindVisualRoot(owner, fallback.transform) : null;
            if (fallback != null && previousVisual == null)
                return fallback;

            int first = UnityEngine.Random.Range(0, Count);
            for (int offset = 0; offset < Count; offset++)
            {
                var source = characterPrefabs[(first + offset) % Count];
                if (source == null)
                    continue;

                var sourceAnimator = source.GetComponentInChildren<Animator>(true);
                if (sourceAnimator == null || sourceAnimator.avatar == null ||
                    !sourceAnimator.avatar.isHuman || !sourceAnimator.avatar.isValid)
                    continue;

                var slot = new GameObject("Character Visual");
                // Configure the borrowed skin before its vendor behaviours can start.
                slot.SetActive(false);
                slot.transform.SetParent(owner, false);
                if (previousVisual != null)
                    slot.transform.SetLocalPositionAndRotation(previousVisual.localPosition, previousVisual.localRotation);

                var visual = Instantiate(source, slot.transform, false);
                visual.name = source.name;
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                DisableImportedMovement(visual);

                var animator = visual.GetComponentInChildren<Animator>(true);
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;

                var presenter = animator.GetComponent<NpcCharacterAnimation>();
                if (presenter == null)
                    presenter = animator.gameObject.AddComponent<NpcCharacterAnimation>();

                if (previousVisual != null)
                {
                    previousVisual.gameObject.SetActive(false);
                    Destroy(previousVisual.gameObject);
                }
                slot.SetActive(true);
                return presenter;
            }

            return fallback;
        }

        private static Transform FindVisualRoot(Transform owner, Transform visual)
        {
            if (visual == owner || !visual.IsChildOf(owner))
                return null;

            while (visual.parent != owner)
                visual = visual.parent;
            return visual;
        }

        private static void DisableImportedMovement(GameObject visual)
        {
            foreach (var behaviour in visual.GetComponentsInChildren<Behaviour>(true))
                if (behaviour != null && (behaviour is NavMeshAgent ||
                    behaviour.GetType().Name.EndsWith("_WanderScript", StringComparison.Ordinal)))
                    behaviour.enabled = false;

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
                body.isKinematic = true;
        }
    }
}
