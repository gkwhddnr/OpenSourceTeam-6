using UnityEngine;

namespace ConvenienceStore
{
    public class AutoStoreDoor : MonoBehaviour
    {
        [Header("Door Settings")]
        public float openAngle = 85f;
        public float openDistance = 2.2f;
        public float swingSpeed = 5f;

        private Quaternion _closedRot;
        private Quaternion _openRot;

        private void Start()
        {
            _closedRot = transform.localRotation;
            _openRot = _closedRot * Quaternion.Euler(0f, openAngle, 0f);
        }

        private void Update()
        {
            bool shouldOpen = false;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 doorCenter = transform.position + transform.right * 0.75f;
                Vector2 doorXZ = new Vector2(doorCenter.x, doorCenter.z);
                Vector2 camXZ = new Vector2(cam.transform.position.x, cam.transform.position.z);
                if (Vector2.Distance(doorXZ, camXZ) <= openDistance)
                    shouldOpen = true;
            }

            Quaternion target = shouldOpen ? _openRot : _closedRot;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * swingSpeed);
        }
    }
}
