using System;

namespace OpenSourceTeam6.Customers.Unity
{
    /// <summary>각 손님이 진열 구역을 중복 없이 무작위 순서로 방문하도록 관리한다.</summary>
    internal sealed class NpcSearchOrder
    {
        private readonly int[] indices;
        private int visitedCount;

        public int Count => indices.Length;
        public int VisitedCount => visitedCount;
        public bool IsComplete => visitedCount >= indices.Length;

        public NpcSearchOrder(int count, Random random)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            indices = new int[count];
            for (int i = 0; i < count; i++)
                indices[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }
        }

        public bool TryGetCurrent(out int index)
        {
            if (IsComplete)
            {
                index = -1;
                return false;
            }
            index = indices[visitedCount];
            return true;
        }

        public void Advance()
        {
            if (!IsComplete)
                visitedCount++;
        }
    }
}
