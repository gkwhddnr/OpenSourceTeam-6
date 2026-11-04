using System;
using System.Collections.Generic;

namespace OpenSourceTeam6.Movement.Core
{
    public sealed class InputLockSet : IPlayerInputGate
    {
        private readonly HashSet<Token> tokens = new HashSet<Token>();
        public bool IsLocked => tokens.Count != 0;

        public IDisposable AcquireLock(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A lock reason is required.", nameof(reason));
            var token = new Token(this);
            tokens.Add(token);
            return token;
        }

        private sealed class Token : IDisposable
        {
            private InputLockSet owner;
            public Token(InputLockSet owner) { this.owner = owner; }
            public void Dispose()
            {
                if (owner == null) return;
                owner.tokens.Remove(this);
                owner = null;
            }
        }
    }
}
