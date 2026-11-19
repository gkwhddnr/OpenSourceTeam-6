using System;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Editor-facing label; the serialized field name remains unchanged.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class KoreanInspectorLabelAttribute : Attribute
    {
        public string Label { get; }
        public KoreanInspectorLabelAttribute(string label) { Label = label; }
    }
}
