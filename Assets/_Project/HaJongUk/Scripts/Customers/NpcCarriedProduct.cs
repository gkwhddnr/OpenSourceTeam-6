using System;
using UnityEngine;

namespace OpenSourceTeam6.Customers.Unity
{
    internal sealed class NpcCarriedProduct
    {
        private static readonly Vector3 HoldPosition = new Vector3(0.28f, 0.88f, 0.48f);

        private readonly GameObject item;
        private readonly bool returnToDisplay;
        private readonly Vector3 initialWorldScale;
        private readonly Transform displayParent;
        private readonly Vector3 displayPosition;
        private readonly Quaternion displayRotation;
        private readonly Vector3 displayScale;
        private readonly Collider[] colliders;
        private readonly bool[] colliderStates;
        private readonly Rigidbody[] bodies;
        private readonly bool[] bodyStates;

        public bool ReturnsToDisplay => returnToDisplay;
        public bool IsOnCounter { get; private set; }

        private NpcCarriedProduct(GameObject item, bool returnToDisplay, Transform carrier, Transform hand,
            NpcCharacterAnimation animation)
        {
            this.item = item;
            this.returnToDisplay = returnToDisplay;
            var itemTransform = item.transform;
            initialWorldScale = itemTransform.lossyScale;
            colliders = item.GetComponentsInChildren<Collider>(true);
            bodies = item.GetComponentsInChildren<Rigidbody>(true);
            if (returnToDisplay)
            {
                displayParent = itemTransform.parent;
                displayPosition = itemTransform.localPosition;
                displayRotation = itemTransform.localRotation;
                displayScale = itemTransform.localScale;
                colliderStates = Array.ConvertAll(colliders, collider => collider.enabled);
                bodyStates = Array.ConvertAll(bodies, body => body.isKinematic);
            }

            AttachToCarrier(carrier, hand, animation);
            foreach (var collider in colliders)
                collider.enabled = false;
            foreach (var body in bodies)
                body.isKinematic = true;
        }

        public static NpcCarriedProduct Attach(GameObject shelfItem, GameObject fallbackPrefab, Transform carrier,
            Transform hand, NpcCharacterAnimation animation = null)
        {
            if (shelfItem != null)
                return new NpcCarriedProduct(shelfItem, true, carrier, hand, animation);
            return fallbackPrefab != null
                ? new NpcCarriedProduct(UnityEngine.Object.Instantiate(fallbackPrefab), false, carrier, hand, animation)
                : null;
        }

        public void PlaceOnCounter(Transform counterSpot)
        {
            if (item == null || counterSpot == null)
                return;

            var itemTransform = item.transform;
            itemTransform.SetParent(null, true);
            itemTransform.localScale = initialWorldScale;
            itemTransform.SetPositionAndRotation(
                counterSpot.position,
                Quaternion.Euler(0f, counterSpot.eulerAngles.y, 0f));

            if (TryCalculateRenderBounds(item, out Bounds bounds))
            {
                float bottomOffset = counterSpot.position.y - bounds.min.y;
                itemTransform.position += Vector3.up * bottomOffset;
            }

            itemTransform.SetParent(counterSpot, true);
            IsOnCounter = true;
        }

        public void ReturnToHand(Transform carrier, Transform hand, NpcCharacterAnimation animation = null)
        {
            if (item == null)
                return;

            AttachToCarrier(carrier, hand, animation);
            IsOnCounter = false;
        }

        private void AttachToCarrier(Transform carrier, Transform hand, NpcCharacterAnimation animation)
        {
            var itemTransform = item.transform;
            itemTransform.SetParent(null, true);
            itemTransform.localScale = initialWorldScale;

            if (hand != null)
            {
                itemTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                bool hasBounds = TryCalculateRenderBounds(item, out Bounds localBounds);
                Vector3 centerOffset = hasBounds ? localBounds.center : Vector3.zero;

                Vector3 desiredCenter;
                Quaternion desiredRot;
                if (animation != null && animation.TryGetRightHandPalmFrame(out Vector3 palmCenter, out Vector3 fingers, out Vector3 back))
                {
                    float halfThickness = hasBounds ? Mathf.Min(localBounds.extents.x, localBounds.extents.z) : 0.035f;
                    float palmOffset = Mathf.Clamp(halfThickness, 0.022f, 0.055f) + 0.008f;
                    desiredCenter = palmCenter + fingers * 0.028f - back * palmOffset;
                    desiredRot = Quaternion.LookRotation(-back, -fingers);
                }
                else
                {
                    desiredCenter = hand.position;
                    desiredRot = carrier != null ? carrier.rotation : hand.rotation;
                }

                itemTransform.SetPositionAndRotation(desiredCenter - desiredRot * centerOffset, desiredRot);
                itemTransform.SetParent(hand, true);
            }
            else if (carrier != null)
            {
                itemTransform.SetParent(carrier, false);
                itemTransform.SetLocalPositionAndRotation(HoldPosition, Quaternion.identity);
            }
        }

        private static bool TryCalculateRenderBounds(GameObject root, out Bounds combined)
        {
            combined = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled)
                    continue;
                if (!hasBounds)
                {
                    combined = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(r.bounds);
                }
            }
            return hasBounds;
        }

        public void Release()
        {
            if (item == null)
                return;

            if (!returnToDisplay)
            {
                item.SetActive(false);
                UnityEngine.Object.Destroy(item);
                return;
            }

            var itemTransform = item.transform;
            itemTransform.SetParent(displayParent, false);
            itemTransform.SetLocalPositionAndRotation(displayPosition, displayRotation);
            itemTransform.localScale = displayScale;
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i] != null) bodies[i].isKinematic = bodyStates[i];
        }
    }
}
