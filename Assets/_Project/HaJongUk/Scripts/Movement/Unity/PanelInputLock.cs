using System;
using OpenSourceTeam6.Movement.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.Movement.Unity
{
    /// <summary>Owns one reversible input lock while the visible pointer is over a panel.</summary>
    public sealed class PanelInputLock : IDisposable
    {
        private readonly string reason;
        private IPlayerInputGate owner;
        private IDisposable token;

        public PanelInputLock(string reason) { this.reason = reason; }

        public static bool PointerIsOver(Rect rect)
        {
            if (!Cursor.visible || Mouse.current == null) return false;
            Vector2 position = Mouse.current.position.ReadValue();
            return rect.Contains(new Vector2(position.x, Screen.height - position.y));
        }

        public void Update(IPlayerInputGate gate, bool overPanel)
        {
            if (!overPanel || gate == null) { Dispose(); return; }
            if (ReferenceEquals(owner, gate) && token != null) return;
            Dispose();
            owner = gate;
            token = gate.AcquireLock(reason);
        }

        public void Dispose()
        {
            token?.Dispose();
            token = null;
            owner = null;
        }
    }
}
