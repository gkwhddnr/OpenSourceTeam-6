using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenSourceTeam6.Customers.Unity
{
    public enum AppearancePart { Model, Hair, Top, Trousers }

    [DisallowMultipleComponent]
    public sealed class CharacterAppearance : MonoBehaviour
    {
        public const string RelaxedTopShape = "Wardrobe Relaxed Top";
        public const string RelaxedTrousersShape = "Wardrobe Relaxed Trousers";

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color[] TopColors = { Color.white, new(0.55f, 0.85f, 1f), new(1f, 0.72f, 0.53f) };
        private static readonly Color[] TrouserColors = { Color.white, new(0.62f, 0.75f, 0.98f), new(0.73f, 0.89f, 0.52f) };

        [SerializeField] private GameObject[] appearances = Array.Empty<GameObject>();
        [SerializeField] private NpcEmployeeAnimation employeeAnimation;
        [SerializeField] private bool shadowsOnly;

        private readonly int[] selected = new int[4];
        private MaterialPropertyBlock partColor;

        [Serializable]
        public struct HairMeshes
        {
            [SerializeField] private Mesh original;
            [SerializeField] private Mesh swept;
            [SerializeField] private Mesh trimmed;

            public HairMeshes(Mesh original, Mesh swept, Mesh trimmed)
            {
                this.original = original;
                this.swept = swept;
                this.trimmed = trimmed;
            }

            public Mesh Get(int index) => index == 1 ? swept : index == 2 ? trimmed : original;
        }

        [SerializeField] private HairMeshes[] hairMeshes = Array.Empty<HairMeshes>();

        public GameObject ActiveVisual => appearances.Length > 0 ? appearances[selected[0]] : null;

        public void Configure(GameObject[] visuals, HairMeshes[] meshes, NpcEmployeeAnimation employee = null,
            bool firstPerson = false)
        {
            appearances = visuals ?? Array.Empty<GameObject>();
            hairMeshes = meshes ?? Array.Empty<HairMeshes>();
            employeeAnimation = employee;
            shadowsOnly = firstPerson;
            Array.Clear(selected, 0, selected.Length);
            if (isActiveAndEnabled && Application.isPlaying)
                Apply();
        }

        private void Start() => Apply();

        public int OptionCount(AppearancePart part) =>
            part == AppearancePart.Model ? appearances.Length : 3;

        public int SelectedIndex(AppearancePart part) => selected[(int)part];

        public GameObject VisualForModel(int model) => appearances[model];

        public void ApplySelection(int[] options)
        {
            for (int i = 0; i < selected.Length; i++)
                selected[i] = Mathf.Clamp(options[i], 0, Mathf.Max(0, OptionCount((AppearancePart)i) - 1));
            Apply();
        }

        public void Select(AppearancePart part, int index)
        {
            if (index < 0 || index >= OptionCount(part) || index == selected[(int)part])
                return;

            selected[(int)part] = index;
            Apply();
        }

        public void ResetAppearance()
        {
            Array.Clear(selected, 0, selected.Length);
            Apply();
        }

        private void Apply()
        {
            var current = ActiveVisual;
            if (current == null) return;

            foreach (var visual in appearances)
                if (visual != null)
                    visual.SetActive(visual == current);

            var skin = current.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null)
            {
                if (shadowsOnly)
                    skin.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                ApplyVisualOptions(skin, selected);
            }

            if (employeeAnimation != null)
                employeeAnimation.SetPresenter(current.GetComponentInChildren<NpcCharacterAnimation>());
        }

        public void ApplyVisualOptions(SkinnedMeshRenderer skin, int[] options)
        {
            int model = options[(int)AppearancePart.Model];
            if (model < hairMeshes.Length)
            {
                var hair = hairMeshes[model].Get(options[(int)AppearancePart.Hair]);
                if (hair != null) skin.sharedMesh = hair;
            }
            if (skin.sharedMesh == null || skin.sharedMesh.subMeshCount != 4) return;
            ApplyGarment(skin, 2, TopColors, options[(int)AppearancePart.Top], RelaxedTopShape);
            ApplyGarment(skin, 3, TrouserColors, options[(int)AppearancePart.Trousers], RelaxedTrousersShape);
        }

        private void ApplyGarment(SkinnedMeshRenderer renderer, int index, Color[] colors, int option, string shapeName)
        {
            partColor ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(partColor, index);
            var material = renderer.sharedMaterial;
            if (material != null && material.HasProperty(BaseColor))
            {
                partColor.SetColor(BaseColor, material.GetColor(BaseColor) * colors[option]);
                renderer.SetPropertyBlock(partColor, index);
            }
            int blendShape = renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (blendShape >= 0)
                renderer.SetBlendShapeWeight(blendShape, option == 2 ? 100f : 0f);
        }

    }
}
