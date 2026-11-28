using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using OpenSourceTeam6.Customers.Unity;

namespace OpenSourceTeam6.Customers.Editor
{
    public static class StoreCharacterAppearanceBuilder
    {
        private const string MeshFolder = "Assets/_Project/HaJongUk/Assets/CharacterAnimation/Appearance";
        private const string PrefabFolder = "Assets/ThirdParty/polyperfect/Low Poly Animated People/- Prefabs";
        private const string ControllerPath = "Assets/_Project/HaJongUk/Assets/CharacterAnimation/HJU_StoreCharacter.controller";
        private const string HairlessBody = "Hairless body";
        private const string SweptHair = "Swept hair";
        private const string TrimmedHair = "Trimmed hair";

        public static CharacterAppearance Configure(Transform owner, bool employeeRole,
            NpcEmployeeAnimation employeeAnimation = null, bool firstPerson = false)
        {
            var variants = employeeRole
                ? new[] { "man_paramedic", "woman_paramedic" }
                : new[] { "man_casual", "woman_casual" };
            var animationController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var component = owner.GetComponent<CharacterAppearance>();
            if (component == null)
                component = Undo.AddComponent<CharacterAppearance>(owner.gameObject);

            var container = owner.Find("Role Character Models");
            if (container != null)
                Undo.DestroyObjectImmediate(container.gameObject);

            var slot = new GameObject("Role Character Models");
            Undo.RegisterCreatedObjectUndo(slot, "Create role appearance slot");
            Undo.SetTransformParent(slot.transform, owner, "Parent role appearance slot");
            slot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            slot.transform.localScale = Vector3.one;

            var prefabs = new GameObject[variants.Length];
            var meshes = new CharacterAppearance.HairMeshes[variants.Length];
            for (int i = 0; i < variants.Length; i++)
            {
                string variant = variants[i];
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{variant}.prefab");
                if (asset == null)
                    throw new InvalidOperationException($"Missing role character prefab '{variant}'.");

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, slot.transform);
                Undo.RegisterCreatedObjectUndo(visual, "Add role appearance variant");
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in visual.GetComponentsInChildren<Behaviour>(true))
                    if (behaviour is NavMeshAgent ||
                        behaviour.GetType().Name.EndsWith("_WanderScript", StringComparison.Ordinal) ||
                        behaviour.GetType().Name == "PolyMorpher")
                        Disable(behaviour);

                foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }

                var animator = visual.GetComponentInChildren<Animator>(true);
                animator.runtimeAnimatorController = animationController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                if (employeeRole)
                    Undo.AddComponent<NpcCharacterAnimation>(animator.gameObject);

                var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
                meshes[i] = GetAppearanceMeshes(skin.sharedMesh, variant);
                skin.sharedMesh = meshes[i].Get(0);
                var material = skin.sharedMaterial;
                skin.sharedMaterials = new[] { material, material, material, material };
                if (firstPerson)
                    skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                PrefabUtility.RecordPrefabInstancePropertyModifications(skin);

                visual.SetActive(i == 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                prefabs[i] = visual;
            }
            Undo.RecordObject(component, "Configure role appearance variants");
            component.Configure(prefabs, meshes, employeeAnimation, firstPerson);
            EditorUtility.SetDirty(component);
            return component;
        }

        private static void Disable(Behaviour behaviour)
        {
            behaviour.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
        }

        [MenuItem("Tools/Store/Upgrade Character Wardrobe Meshes")]
        public static void UpgradeAppearanceMeshes()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Mesh mesh && mesh.subMeshCount == 4)
                        EnsureGarmentShapes(mesh);
                AssetDatabase.SaveAssetIfDirty(new GUID(guid));
            }
        }

        private static void EnsureGarmentShapes(Mesh mesh)
        {
            bool needsTop = mesh.GetBlendShapeIndex(CharacterAppearance.RelaxedTopShape) < 0;
            bool needsTrousers = mesh.GetBlendShapeIndex(CharacterAppearance.RelaxedTrousersShape) < 0;
            if (!needsTop && !needsTrousers) return;

            var byPart = new List<int>[mesh.subMeshCount];
            for (int i = 0; i < byPart.Length; i++)
            {
                byPart[i] = new List<int>();
                mesh.GetTriangles(byPart[i], i);
            }
            if (needsTop) AddGarmentShape(mesh, byPart, 2, CharacterAppearance.RelaxedTopShape);
            if (needsTrousers) AddGarmentShape(mesh, byPart, 3, CharacterAppearance.RelaxedTrousersShape);
            EditorUtility.SetDirty(mesh);
        }

        private static CharacterAppearance.HairMeshes GetAppearanceMeshes(Mesh original, string variant)
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/_Project/HaJongUk/Assets/CharacterAnimation", "Appearance");
            var path = $"{MeshFolder}/{variant}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                Mesh body = null;
                Mesh swept = null;
                Mesh trimmed = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Mesh bodyMesh && bodyMesh.name == HairlessBody) body = bodyMesh;
                    if (asset is Mesh variantMesh && variantMesh.name == SweptHair) swept = variantMesh;
                    if (asset is Mesh variantMesh2 && variantMesh2.name == TrimmedHair) trimmed = variantMesh2;
                }
                if (body != null && swept != null && trimmed != null)
                    return new CharacterAppearance.HairMeshes(existing, swept, trimmed, body);
                throw new InvalidOperationException($"Incomplete hair variants in '{path}'.");
            }

            var indices = original.GetTriangles(0);
            var vertices = original.vertices;
            var uv = original.uv;
            var byPart = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                float y = ((vertices[a].y + vertices[b].y + vertices[c].y) / 3f - original.bounds.min.y) /
                    original.bounds.size.y;
                var uvCenter = (uv[a] + uv[b] + uv[c]) / 3f;
                var part = ResolvePart(variant, y, uvCenter);
                byPart[part].Add(a);
                byPart[part].Add(b);
                byPart[part].Add(c);
            }

            var mesh = UnityEngine.Object.Instantiate(original);
            mesh.name = variant + " wardrobe";
            mesh.subMeshCount = byPart.Length;
            for (int i = 0; i < byPart.Length; i++)
            {
                if (byPart[i].Count == 0)
                    throw new InvalidOperationException($"Appearance region {i} contains no triangles for '{variant}'.");
                mesh.SetTriangles(byPart[i], i, false);
            }
            AddGarmentShape(mesh, byPart, 2, CharacterAppearance.RelaxedTopShape);
            AddGarmentShape(mesh, byPart, 3, CharacterAppearance.RelaxedTrousersShape);
            AssetDatabase.CreateAsset(mesh, path);
            var sweptHair = CreateHairVariant(mesh, variant, SweptHair, 1);
            var trimmedHair = CreateHairVariant(mesh, variant, TrimmedHair, 2);
            var hairless = CreateHairlessBody(mesh);
            AssetDatabase.AddObjectToAsset(hairless, mesh);
            AssetDatabase.AddObjectToAsset(sweptHair, mesh);
            AssetDatabase.AddObjectToAsset(trimmedHair, mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            return new CharacterAppearance.HairMeshes(mesh, sweptHair, trimmedHair, hairless);
        }

        private static void AddGarmentShape(Mesh mesh, IReadOnlyList<List<int>> byPart, int part, string shapeName)
        {
            var vertices = mesh.vertices;
            var sharedSeams = new HashSet<Vector3>();
            for (int i = 0; i < byPart.Count; i++)
            {
                if (i == part) continue;
                foreach (int index in byPart[i])
                    sharedSeams.Add(vertices[index]);
            }

            var delta = new Vector3[vertices.Length];
            var zeros = new Vector3[vertices.Length];
            foreach (int index in byPart[part])
            {
                var vertex = vertices[index];
                if (sharedSeams.Contains(vertex)) continue;
                Vector3 center = mesh.bounds.center;
                if (part == 3)
                {
                    float inset = mesh.bounds.size.y * 0.055f;
                    center.x = Mathf.Sign(vertex.x) * inset;
                }
                delta[index] = new Vector3((vertex.x - center.x) * 0.10f, 0f, (vertex.z - center.z) * 0.16f);
            }
            mesh.AddBlendShapeFrame(shapeName, 100f, delta, zeros, zeros);
        }

        private static Mesh CreateHairVariant(Mesh source, string variant, string name, int style)
        {
            var mesh = new Mesh { name = name, indexFormat = source.indexFormat };
            ApplyBorrowedHair(source, mesh, variant, style);
            return mesh;
        }

        [MenuItem("Tools/Store/Rebuild Character Hair Fits")]
        public static void RebuildHairFits()
        {
            foreach (string variant in new[] { "man_casual", "woman_casual", "man_paramedic", "woman_paramedic" })
            {
                string path = $"{MeshFolder}/{variant}.asset";
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (source == null)
                    throw new InvalidOperationException($"Missing wardrobe mesh: {path}");
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not Mesh mesh || (mesh.name != SweptHair && mesh.name != TrimmedHair))
                        continue;

                    // Keep the subasset object so scene references and local file IDs survive.
                    mesh.Clear(false);
                    ApplyBorrowedHair(source, mesh, variant, mesh.name == SweptHair ? 1 : 2);
                    EditorUtility.SetDirty(mesh);
                }
                bool hasHairless = Array.Exists(AssetDatabase.LoadAllAssetsAtPath(path), asset => asset.name == HairlessBody);
                if (!hasHairless)
                    AssetDatabase.AddObjectToAsset(CreateHairlessBody(source), source);
                AssetDatabase.SaveAssetIfDirty(new GUID(AssetDatabase.AssetPathToGUID(path)));
            }
        }

        private static void ApplyBorrowedHair(Mesh source, Mesh mesh, string variant, int style)
        {
            string donorName = variant switch
            {
                "man_casual" => style == 1 ? "man_paramedic" : "woman_paramedic",
                "man_paramedic" => style == 1 ? "man_casual" : "woman_casual",
                "woman_casual" => style == 1 ? "woman_paramedic" : "man_paramedic",
                "woman_paramedic" => style == 1 ? "woman_casual" : "man_casual",
                _ => throw new ArgumentOutOfRangeException(nameof(variant))
            };
            var donor = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/{donorName}.asset");
            if (donor == null)
                throw new InvalidOperationException($"Missing hair donor: {donorName}");

            var recipientHair = source.GetTriangles(1);
            var donorHair = donor.GetTriangles(1);
            var donorVertices = donor.vertices;
            var donorNormals = donor.normals;
            var donorTangents = donor.tangents;
            var donorUvs = donor.uv;
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var sourceVertices = source.vertices;
            var sourceNormals = source.normals;
            var sourceTangents = source.tangents;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{variant}.prefab");
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int headBone = Array.FindIndex(renderer.bones, bone => bone.name == "Head_M");
            if (headBone < 0)
                throw new InvalidOperationException($"Missing head bone for {variant}");
            Matrix4x4 headSpace = source.bindposes[headBone];
            Bounds recipientHead = HeadBounds(source);
            Bounds donorHead = HeadBounds(donor);
            Vector3 recipientAnchor = HeadAnchor(recipientHead);
            Vector3 donorAnchor = HeadAnchor(donorHead);
            // The models use different import units. Compare their heads in mesh space,
            // where the head width also includes the unit conversion between meshes.
            float scale = recipientHead.size.x / donorHead.size.x;
            var coveredScalp = new HashSet<int>(recipientHair);
            var scalpUv = FindScalpUv(source, coveredScalp, recipientAnchor);
            var crownUv = FindCrownUv(donor, donorHair);
            var remap = new Dictionary<int, int>();
            var hairTriangles = new List<int>(donorHair.Length);
            foreach (int index in donorHair)
            {
                if (!remap.TryGetValue(index, out int mapped))
                {
                    mapped = positions.Count;
                    remap.Add(index, mapped);
                    Vector3 point = recipientAnchor + (donorVertices[index] - donorAnchor) * scale;
                    float cheekHeight = Mathf.Lerp(recipientHead.min.y, recipientHead.max.y, .53f);
                    if (point.y < cheekHeight && Mathf.Abs(point.x) < recipientHead.extents.x * .73f)
                    {
                        // Keep long strands from passing through the eyes and cheeks.
                        point.z = Mathf.Min(point.z, recipientHead.center.z);
                    }
                    AddHeadVertex(point, donorNormals[index], donorTangents[index], donorUvs[index]);
                }
                hairTriangles.Add(mapped);
            }

            // Keep the original hairline closed with a skin-colored cap. Only
            // hair-exclusive vertices are moved so the face seam stays fixed.
            var shared = new HashSet<Vector3>();
            foreach (int part in new[] { 0, 2, 3 })
                foreach (int index in source.GetTriangles(part))
                    shared.Add(sourceVertices[index]);
            var cap = new Dictionary<(int, bool), int>();
            float hairline = Mathf.Lerp(recipientHead.min.y, recipientHead.max.y, .66f);
            for (int triangle = 0; triangle < recipientHair.Length; triangle += 3)
            {
                float centerY = (sourceVertices[recipientHair[triangle]].y + sourceVertices[recipientHair[triangle + 1]].y +
                    sourceVertices[recipientHair[triangle + 2]].y) / 3;
                bool crown = centerY >= hairline;
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = recipientHair[triangle + corner];
                    if (!cap.TryGetValue((index, crown), out int mapped))
                    {
                        mapped = positions.Count;
                        cap.Add((index, crown), mapped);
                        Vector3 point = sourceVertices[index];
                        if (!shared.Contains(point))
                        {
                            point.x = Mathf.Clamp(point.x, recipientHead.min.x, recipientHead.max.x);
                            point.y = Mathf.Clamp(point.y, recipientHead.min.y, recipientHead.max.y);
                            point.z = Mathf.Clamp(point.z, recipientHead.min.z, recipientHead.max.z);
                        }
                        AddHeadVertex(point, sourceNormals[index], sourceTangents[index], crown ? crownUv : scalpUv);
                    }
                    hairTriangles.Add(mapped);
                }
            }

            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(hairTriangles, 0, true);

            void AddHeadVertex(Vector3 point, Vector3 normal, Vector4 tangent, Vector2 uv)
            {
                positions.Add(headSpace.MultiplyPoint3x4(point));
                normals.Add(headSpace.MultiplyVector(normal).normalized);
                Vector3 direction = headSpace.MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z)).normalized;
                tangents.Add(new Vector4(direction.x, direction.y, direction.z, tangent.w));
                uvs.Add(uv);
            }
        }

        private static Mesh CreateHairlessBody(Mesh source)
        {
            var body = UnityEngine.Object.Instantiate(source);
            body.name = HairlessBody;
            body.SetTriangles(Array.Empty<int>(), 1, false);
            return body;
        }

        private static Bounds HeadBounds(Mesh mesh)
        {
            var vertices = mesh.vertices;
            float headStart = mesh.bounds.max.y - mesh.bounds.size.y * 0.14f;
            Bounds bounds = default;
            foreach (int index in mesh.GetTriangles(0))
            {
                Vector3 point = vertices[index];
                if (point.y <= headStart) continue;
                if (bounds.center.y == 0f) bounds = new Bounds(point, Vector3.zero);
                else bounds.Encapsulate(point);
            }
            if (bounds.size.x <= 0f)
                throw new InvalidOperationException($"No head geometry found for {mesh.name}");
            return bounds;
        }

        private static Vector3 HeadAnchor(Bounds head) =>
            new Vector3(head.center.x, Mathf.Lerp(head.min.y, head.max.y, 0.78f), head.center.z);

        private static Vector2 FindCrownUv(Mesh donor, int[] triangles)
        {
            var vertices = donor.vertices;
            var uv = donor.uv;
            float highest = float.NegativeInfinity;
            Vector2 crown = default;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                float height = (vertices[a].y + vertices[b].y + vertices[c].y) / 3;
                if (height <= highest) continue;
                highest = height;
                crown = (uv[a] + uv[b] + uv[c]) / 3;
            }
            return crown;
        }

        private static Vector2 FindScalpUv(Mesh source, HashSet<int> hair, Vector3 scalpCenter)
        {
            var vertices = source.vertices;
            var uvs = source.uv;
            int closest = 0;
            float distance = float.PositiveInfinity;
            foreach (int index in source.GetTriangles(0))
            {
                if (hair.Contains(index) || vertices[index].y > scalpCenter.y) continue;
                float candidate = (vertices[index] - scalpCenter).sqrMagnitude;
                if (candidate >= distance) continue;
                closest = index;
                distance = candidate;
            }
            return uvs[closest];
        }

        // Select the verified atlas islands for each import and disambiguate reused color islands by height.
        private static int ResolvePart(string variant, float height, Vector2 uv)
        {
            switch (variant)
            {
                case "man_casual":
                    if (height > 0.88f && InAtlas(uv, 0.050f, 0.080f, 0.785f, 0.842f)) return 1;
                    if (height > 0.53f && InAtlas(uv, 0.16f, 0.246f, 0.008f, 0.115f)) return 2;
                    if (height is > 0.075f and < 0.54f && InAtlas(uv, 0.065f, 0.101f, 0.625f, 0.722f)) return 3;
                    break;
                case "woman_casual":
                    if (height > 0.70f && InAtlas(uv, 0.565f, 0.59f, 0.768f, 0.852f)) return 1;
                    if (height > 0.53f && InAtlas(uv, 0.165f, 0.211f, 0.025f, 0.085f)) return 2;
                    if (height is > 0.075f and < 0.54f && InAtlas(uv, 0.045f, 0.103f, 0.658f, 0.718f)) return 3;
                    break;
                case "man_paramedic":
                    if (height > 0.86f && InAtlas(uv, 0.305f, 0.329f, 0.805f, 0.844f)) return 1;
                    if (height > 0.525f && (InAtlas(uv, 0.02f, 0.085f, 0.25f, 0.35f) ||
                        InAtlas(uv, 0.657f, 0.698f, 0.650f, 0.728f) ||
                        InAtlas(uv, 0.757f, 0.863f, 0.40f, 0.48f))) return 2;
                    if (height is > 0.075f and <= 0.525f &&
                        (InAtlas(uv, 0.02f, 0.085f, 0.25f, 0.35f) ||
                         InAtlas(uv, 0.657f, 0.698f, 0.650f, 0.728f))) return 3;
                    break;
                case "woman_paramedic":
                    if (height > 0.78f && (InAtlas(uv, 0.045f, 0.087f, 0.645f, 0.73f) ||
                        InAtlas(uv, 0.31f, 0.333f, 0.296f, 0.331f))) return 1;
                    if (height > 0.53f && (InAtlas(uv, 0.409f, 0.491f, 0.267f, 0.333f) ||
                        InAtlas(uv, 0.657f, 0.698f, 0.650f, 0.728f) ||
                        InAtlas(uv, 0.757f, 0.863f, 0.40f, 0.48f))) return 2;
                    if (height is > 0.075f and <= 0.53f && InAtlas(uv, 0.017f, 0.084f, 0.26f, 0.347f)) return 3;
                    break;
            }
            return 0;
        }

        private static bool InAtlas(Vector2 uv, float minX, float maxX, float minY, float maxY) =>
            uv.x >= minX && uv.x <= maxX && uv.y >= minY && uv.y <= maxY;
    }
}
