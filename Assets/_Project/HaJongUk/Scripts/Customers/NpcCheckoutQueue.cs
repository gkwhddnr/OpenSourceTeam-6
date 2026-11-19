using System.Collections.Generic;

namespace OpenSourceTeam6.Customers.Unity
{
    internal sealed class NpcCheckoutQueue
    {
        public const int RegisterPosition = -1;

        private sealed class Entry
        {
            public readonly int CustomerId;
            public float WaitSeconds;
            public float ServiceSeconds;
            public bool IsCheckingOut;

            public Entry(int customerId) => CustomerId = customerId;
        }

        private readonly List<Entry> waiting = new List<Entry>(8);
        private Entry register;

        // Returns the register position or the zero-based waiting slot.
        public int ClaimPosition(int customerId)
        {
            if (register?.CustomerId == customerId)
                return RegisterPosition;

            int slot = FindWaitingSlot(customerId);
            if (slot < 0)
            {
                slot = waiting.Count;
                waiting.Add(new Entry(customerId));
            }

            if (slot != 0 || register != null)
                return slot;

            register = waiting[0];
            waiting.RemoveAt(0);
            return RegisterPosition;
        }

        public void TickWaiting(float elapsed)
        {
            foreach (Entry entry in waiting)
                entry.WaitSeconds += elapsed;
            if (register != null && !register.IsCheckingOut)
                register.WaitSeconds += elapsed;
        }

        public bool TickService(int customerId, float elapsed, float duration)
        {
            if (register?.CustomerId != customerId)
                return false;

            register.IsCheckingOut = true;
            register.ServiceSeconds += elapsed;
            if (register.ServiceSeconds < duration)
                return false;

            register = null;
            return true;
        }

        public void Release(int customerId)
        {
            if (register?.CustomerId == customerId)
            {
                register = null;
                return;
            }

            int slot = FindWaitingSlot(customerId);
            if (slot >= 0)
                waiting.RemoveAt(slot);
        }

        public void CopySnapshot(List<CheckoutWaitSnapshot> output)
        {
            output.Clear();
            if (register != null)
                output.Add(new CheckoutWaitSnapshot(register.CustomerId, 0, register.WaitSeconds, register.IsCheckingOut));

            for (int i = 0; i < waiting.Count; i++)
            {
                Entry entry = waiting[i];
                output.Add(new CheckoutWaitSnapshot(entry.CustomerId, i + 1, entry.WaitSeconds, false));
            }
        }

        private int FindWaitingSlot(int customerId)
        {
            for (int i = 0; i < waiting.Count; i++)
            {
                if (waiting[i].CustomerId == customerId)
                    return i;
            }

            return -1;
        }
    }
}
