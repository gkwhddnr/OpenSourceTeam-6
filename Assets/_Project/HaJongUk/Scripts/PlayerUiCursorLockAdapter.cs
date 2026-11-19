using System;
using System.Collections.Generic;
using OpenSourceTeam6.Movement.Unity;
using OpenSourceTeam6.UI.Core;
using UnityEngine;

/// <summary>Lets UI screens borrow the local player input gate without owning the cursor.</summary>
[RequireComponent(typeof(PlayerRigController)), DisallowMultipleComponent]
public sealed class PlayerUiCursorLockAdapter : MonoBehaviour, ICursorLockGate
{
    private PlayerRigController player;
    private readonly HashSet<Lease> leases = new HashSet<Lease>();

    public bool IsLocked => player && player.InputGate != null && player.InputGate.IsLocked;

    private void Awake() => player = GetComponent<PlayerRigController>();

    public IDisposable AcquireLock(string reason)
    {
        if (!isActiveAndEnabled || !player || !player.isActiveAndEnabled || player.InputGate == null)
            throw new InvalidOperationException("The player cursor gate is not available.");
        var lease = new Lease(this, player.InputGate.AcquireLock(reason));
        leases.Add(lease);
        return lease;
    }

    private void OnDisable()
    {
        while (leases.Count > 0)
        {
            foreach (var lease in leases) { lease.Dispose(); break; }
        }
    }

    private sealed class Lease : IDisposable
    {
        private PlayerUiCursorLockAdapter owner;
        private IDisposable token;
        public Lease(PlayerUiCursorLockAdapter owner, IDisposable token) { this.owner = owner; this.token = token; }
        public void Dispose()
        {
            if (owner == null) return;
            token.Dispose();
            token = null;
            owner.leases.Remove(this);
            owner = null;
        }
    }
}
