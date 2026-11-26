using UnityEngine;
using UnityEngine.InputSystem;

namespace ConvenienceStore
{
    public class PlayerPerspectiveSwitcher : MonoBehaviour
    {
        [Header("Player Rigs")]
        public GameObject firstPersonRig;
        public Transform firstPersonCharacter;

        public GameObject thirdPersonRig;
        public Transform thirdPersonCharacter;

        [Header("Settings")]
        public bool startInFirstPerson = true;

        private bool _isFirstPerson;

        private void Start()
        {
            _isFirstPerson = startInFirstPerson;
            ApplyActiveState();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            {
                TogglePerspective();
            }
        }

        public void TogglePerspective()
        {
            if (firstPersonRig == null || thirdPersonRig == null ||
                firstPersonCharacter == null || thirdPersonCharacter == null)
                return;

            Transform fromChar = _isFirstPerson ? firstPersonCharacter : thirdPersonCharacter;
            Transform toChar = _isFirstPerson ? thirdPersonCharacter : firstPersonCharacter;

            Vector3 savedPos = fromChar.position;
            float savedYaw = fromChar.eulerAngles.y;

            _isFirstPerson = !_isFirstPerson;
            ApplyActiveState();

            var cc = toChar.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            toChar.position = savedPos;
            toChar.rotation = Quaternion.Euler(0f, savedYaw, 0f);
            if (cc != null) cc.enabled = true;
        }

        private void ApplyActiveState()
        {
            if (firstPersonRig != null) firstPersonRig.SetActive(_isFirstPerson);
            if (thirdPersonRig != null) thirdPersonRig.SetActive(!_isFirstPerson);
        }
    }
}
