using System;
using OpenSourceTeam6.Movement.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.Movement.Unity
{
    public sealed class PlayerInputGate : IPlayerInputGate
    {
        private readonly InputLockSet locks = new InputLockSet();
        private IDisposable focusLock;
        private IDisposable pauseLock;
        private IDisposable menuLock;
        private bool wasLocked = true;
        public bool IsLocked => locks.IsLocked;
        public IDisposable AcquireLock(string reason) => locks.AcquireLock(reason);

        // The rig calls this before reading input; one writer owns the cursor.
        public bool PrepareFrame()
        {
            SetLock(ref pauseLock, Time.timeScale == 0f, "Pause");
            SetLock(ref focusLock, !Application.isFocused, "Focus");
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetLock(ref menuLock, true, "Cursor released");
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Application.isFocused)
                SetLock(ref menuLock, false, "Cursor released");
            bool locked = IsLocked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
            bool skipLook = locked || wasLocked;
            wasLocked = locked;
            return skipLook;
        }

        private void SetLock(ref IDisposable token, bool required, string reason)
        {
            if (required && token == null) token = locks.AcquireLock(reason);
            if (!required && token != null) { token.Dispose(); token = null; }
        }

        public void Disable()
        {
            SetLock(ref focusLock, false, "Focus");
            SetLock(ref pauseLock, false, "Pause");
            SetLock(ref menuLock, false, "Menu");
            wasLocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
