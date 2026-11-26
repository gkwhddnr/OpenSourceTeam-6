using UnityEngine;

namespace OpenSourceTeam6.Customers.Unity
{
    internal sealed class NpcFacialExpression
    {
        private readonly Animator animator;
        private readonly Transform transform;
        private TextMesh cue;

        public NpcFacialExpression(Animator animator)
        {
            this.animator = animator;
            transform = animator.transform;
        }

        // Offsets are in centimeters along the character's right, up and forward axes.
        private static readonly (string Name, Vector3 Frown, Vector3 Shout)[] FacePose =
        {
            ("EyebrowInner_R", new(-0.9f, -2.4f, 0), new(-1.2f, -2.8f, 0)),
            ("EyebrowInner_L", new(0.6f, -1.0f, 0), new(1.2f, -2.8f, 0)),
            ("EyebrowMid_R", new(0, -1.2f, 0), new(0, -0.7f, 0)),
            ("EyebrowMid_L", new(0, 0.4f, 0), new(0, -0.7f, 0)),
            ("EyebrowOuter_R", new(0, -0.3f, 0), new(0, 0.9f, 0)),
            ("EyebrowOuter_L", new(0, 0.8f, 0), new(0, 0.9f, 0)),
            ("Cheek_R", new(-0.6f, 1.6f, 0.5f), new(0, 0.9f, 0.3f)),
            ("Cheek_L", new(0.2f, 0.4f, 0.2f), new(0, 0.9f, 0.3f)),
            ("LowerNose_R", new(-0.4f, 0.7f, 0.4f), Vector3.zero),
            ("UpperLip_M", new(0.5f, 0.9f, 0.2f), new(0, 1.2f, 0.2f)),
            ("OuterMouth_R", new(0.5f, 1.2f, 0), new(1.0f, 0.5f, 0)),
            ("OuterMouth_L", new(0.3f, -0.6f, 0), new(-1.0f, 0.5f, 0)),
            ("Jaw_M", new(0.4f, -0.3f, 0), new(0, -2.2f, 0.3f)),
        };
        private Transform[] faceBones;
        private Vector3[] faceBase;
        private Vector3[] faceApplied;
        private bool facePosed;
        private Transform faceHead;
        private Quaternion headBase;
        private Quaternion headApplied;
        private bool headPosed;

        public void Show(bool shouting, float weight)
        {
            bool frowning = !shouting;
            if (faceBones == null)
            {
                faceBones = new Transform[FacePose.Length];
                faceBase = new Vector3[FacePose.Length];
                faceApplied = new Vector3[FacePose.Length];
                faceHead = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    BindFaceBones(renderer.bones);
                BindFaceBones(animator.GetComponentsInChildren<Transform>(true));
            }

            for (int i = 0; i < faceBones.Length; i++)
            {
                Transform bone = faceBones[i];
                if (bone == null) continue;
                // Preserve facial motion authored into the current clip.
                if (!facePosed || (bone.localPosition - faceApplied[i]).sqrMagnitude > 0.0000000001f)
                    faceBase[i] = bone.localPosition;
                Vector3 offset = shouting ? FacePose[i].Shout : FacePose[i].Frown;
                Vector3 worldOffset = transform.rotation * offset * (weight * 0.0135f);
                faceApplied[i] = faceBase[i] + bone.parent.InverseTransformVector(worldOffset);
                bone.localPosition = faceApplied[i];
            }
            facePosed = true;

            if (frowning && faceHead != null)
            {
                if (!headPosed || Quaternion.Angle(faceHead.localRotation, headApplied) > 0.05f)
                    headBase = faceHead.localRotation;

                // Give models with rigid faces a readable expression through head posture.
                Transform parent = faceHead.parent;
                Vector3 pitchAxis = parent.InverseTransformDirection(transform.right);
                Vector3 tiltAxis = parent.InverseTransformDirection(transform.forward);
                headApplied = Quaternion.AngleAxis(11f * weight, pitchAxis) *
                    Quaternion.AngleAxis(-9f * weight, tiltAxis) * headBase;
                faceHead.localRotation = headApplied;
                headPosed = true;
            }
            else
            {
                RestoreHead();
            }

            UpdateCue(shouting, weight);
        }

        private void UpdateCue(bool shouting, float weight)
        {
            if (faceHead == null || weight < 0.3f)
            {
                if (cue != null) cue.gameObject.SetActive(false);
                return;
            }

            if (cue == null)
            {
                var marker = new GameObject("Expression cue");
                marker.transform.SetParent(transform, false);
                cue = marker.AddComponent<TextMesh>();
                cue.anchor = TextAnchor.MiddleCenter;
                cue.fontSize = 64;
                cue.fontStyle = FontStyle.Bold;
                cue.characterSize = 0.09f;
            }

            string glyph = shouting ? "!" : "...";
            if (cue.text != glyph) cue.text = glyph;
            cue.color = shouting ? new Color(1f, 0.32f, 0.22f) : new Color(1f, 0.81f, 0.35f);
            cue.transform.position = faceHead.position + transform.up * 0.31f;
            Camera camera = Camera.main;
            if (camera != null) cue.transform.rotation = camera.transform.rotation;
            cue.gameObject.SetActive(true);
        }

        private void BindFaceBones(Transform[] bones)
        {
            foreach (Transform bone in bones)
            {
                if (bone == null) continue;
                string name = bone.name;
                int namespaceEnd = name.LastIndexOf(':');
                if (namespaceEnd >= 0) name = name.Substring(namespaceEnd + 1);
                for (int i = 0; i < FacePose.Length; i++)
                    if (faceBones[i] == null && string.Equals(name, FacePose[i].Name, System.StringComparison.OrdinalIgnoreCase))
                        faceBones[i] = bone;
            }
        }

        private void RestoreHead()
        {
            if (headPosed && faceHead != null && Quaternion.Angle(faceHead.localRotation, headApplied) <= 0.05f)
                faceHead.localRotation = headBase;
            headPosed = false;
        }

        public void Clear()
        {
            if (cue != null) cue.gameObject.SetActive(false);
            RestoreHead();
            if (!facePosed) return;
            for (int i = 0; i < faceBones.Length; i++)
                if (faceBones[i] != null && (faceBones[i].localPosition - faceApplied[i]).sqrMagnitude <= 0.0000000001f)
                    faceBones[i].localPosition = faceBase[i];
            facePosed = false;
        }

    }
}
