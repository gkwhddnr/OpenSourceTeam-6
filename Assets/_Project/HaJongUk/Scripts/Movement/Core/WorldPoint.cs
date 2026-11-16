namespace OpenSourceTeam6.Movement.Core
{
    public readonly struct WorldPoint
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);

        public WorldPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
