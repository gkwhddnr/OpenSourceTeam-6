using System;
using UnityEngine;

namespace OpenSourceTeam6.Customers.Unity
{
    internal sealed class NpcCarriedProduct
    {
        private static readonly Vector3 HoldPosition = new Vector3(0.28f, 0.88f, 0.48f);

        private readonly GameObject item;
        private readonly bool returnToDisplay;
        private readonly Transform displayParent;
        private readonly Vector3 displayPosition;
        private readonly Quaternion displayRotation;
        private readonly Vector3 displayScale;
        private readonly Collider[] colliders;
        private readonly bool[] colliderStates;
        private readonly Rigidbody[] bodies;
        private readonly bool[] bodyStates;

        private NpcCarriedProduct(GameObject item, bool returnToDisplay, Transform carrier)
        {
            this.item = item;
            this.returnToDisplay = returnToDisplay;
            var itemTransform = item.transform;
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

            itemTransform.SetParent(carrier, false);
            itemTransform.SetLocalPositionAndRotation(HoldPosition, Quaternion.identity);
            foreach (var collider in colliders)
                collider.enabled = false;
            foreach (var body in bodies)
                body.isKinematic = true;
        }

        public static NpcCarriedProduct Attach(GameObject shelfItem, GameObject fallbackPrefab, Transform carrier)
        {
            if (shelfItem != null)
                return new NpcCarriedProduct(shelfItem, true, carrier);
            return fallbackPrefab != null
                ? new NpcCarriedProduct(UnityEngine.Object.Instantiate(fallbackPrefab), false, carrier)
                : null;
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
