using System;
using OpenSourceTeam6.Customers.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    [DisallowMultipleComponent]
    public sealed class CharacterCustomizationPanel : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        private const float DefaultPreviewSize = 1.06f;
        private static readonly Vector3 PreviewFocus = new(0f, 1.04f, 0f);
        private static readonly Color PanelColor = new(0.08f, 0.14f, 0.18f, 1f);
        private static readonly Color ButtonColor = new(0.17f, 0.29f, 0.34f, 1f);
        private static readonly Color AccentColor = new(0.17f, 0.46f, 0.41f, 1f);
        private static readonly string[] PartLabels = { "외모", "헤어스타일", "상의", "바지" };
        private static readonly string[][] OptionLabels =
        {
            new[] { "남성", "여성" },
            new[] { "기본", "교체 1", "교체 2" },
            new[] { "기본", "하늘색", "따뜻한색 · 여유핏" },
            new[] { "기본", "남색", "올리브 · 여유핏" }
        };

        [SerializeField] private CharacterAppearance player;
        [SerializeField] private CharacterAppearance employee;
        [SerializeField] private RectTransform launchContainer;
        [SerializeField] private Font font;

        private readonly Text[] values = new Text[4];
        private GameObject launcher;
        private RectTransform overlay;
        private GameObject stage;
        private GameObject preview;
        private RawImage previewImage;
        private RenderTexture previewTexture;
        private Button playerTab;
        private Button employeeTab;
        private CharacterAppearance selectedRole;
        private readonly int[] playerDraft = new int[4];
        private readonly int[] employeeDraft = new int[4];
        private int[] Draft => selectedRole == player ? playerDraft : employeeDraft;
        private float previewYaw;
        private float previewPitch;
        private Camera previewCamera;
        private float previewSize = DefaultPreviewSize;

        public void Configure(CharacterAppearance playerAppearance, CharacterAppearance employeeAppearance,
            RectTransform container, Font labelFont = null)
        {
            player = playerAppearance;
            employee = employeeAppearance;
            launchContainer = container;
            font = labelFont;
        }

        private void Start()
        {
            if (player == null || employee == null || launchContainer == null)
                return;

            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var entry = CreateButton(launchContainer, "캐릭터 설정", Open, AccentColor);
            launcher = entry.gameObject;
            Place((RectTransform)entry.transform, new Vector2(1f, 1f), -104f, -44f, 168f, 42f);
        }

        private void LateUpdate()
        {
            if (overlay != null && overlay.gameObject.activeSelf && !launchContainer.gameObject.activeInHierarchy)
                Close();
        }

        private void Open()
        {
            for (int i = 0; i < values.Length; i++)
            {
                playerDraft[i] = player.SelectedIndex((AppearancePart)i);
                employeeDraft[i] = employee.SelectedIndex((AppearancePart)i);
            }
            previewYaw = 0f;
            previewPitch = 0f;
            previewSize = DefaultPreviewSize;
            if (overlay == null)
                BuildOverlay();
            overlay.gameObject.SetActive(true);
            SelectRole(player);
        }

        private void Save()
        {
            player.ApplySelection(playerDraft);
            employee.ApplySelection(employeeDraft);
            Close();
        }

        private void Close()
        {
            if (overlay != null)
                overlay.gameObject.SetActive(false);
            if (stage != null)
            {
                stage.SetActive(false);
                Destroy(stage);
            }
            stage = null;
            preview = null;
            previewCamera = null;
        }

        private void BuildOverlay()
        {
            overlay = MakeRect("Character customization overlay", transform);
            var canvas = overlay.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;
            var scaler = overlay.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

            var card = MakeRect("Character customization", overlay);
            Place(card, new Vector2(0.5f, 0.5f), 0f, 0f, 628f, 404f);
            card.gameObject.AddComponent<Image>().color = PanelColor;
            var title = MakeText(card, "캐릭터 설정", 25, TextAnchor.MiddleLeft);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), -129f, 161f, 292f, 43f);
            var close = CreateButton(card, "닫기", Close, ButtonColor);
            Place((RectTransform)close.transform, new Vector2(0.5f, 0.5f), 242f, 161f, 84f, 34f);
            var save = CreateButton(card, "저장", Save, AccentColor);
            Place((RectTransform)save.transform, new Vector2(0.5f, 0.5f), 148f, 161f, 84f, 34f);

            playerTab = CreateButton(card, "플레이어", () => SelectRole(player), AccentColor);
            employeeTab = CreateButton(card, "직원", () => SelectRole(employee), ButtonColor);
            Place((RectTransform)playerTab.transform, new Vector2(0.5f, 0.5f), -203f, 115f, 144f, 36f);
            Place((RectTransform)employeeTab.transform, new Vector2(0.5f, 0.5f), -51f, 115f, 144f, 36f);

            var frame = MakeRect("Character preview", card);
            Place(frame, new Vector2(0.5f, 0.5f), -165f, -29f, 220f, 226f);
            previewImage = frame.gameObject.AddComponent<RawImage>();
            var events = frame.gameObject.AddComponent<EventTrigger>();
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data =>
            {
                if (data is not PointerEventData pointer || pointer.button != PointerEventData.InputButton.Left) return;
                Vector2 delta = pointer.delta / canvas.scaleFactor * 0.65f;
                previewYaw = Mathf.Repeat(previewYaw - delta.x, 360f);
                previewPitch = Mathf.Clamp(previewPitch + delta.y, -55f, 55f);
                if (preview != null)
                    preview.transform.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
                UpdatePreviewCameraPose();
            });
            events.triggers.Add(drag);
            var scroll = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            scroll.callback.AddListener(data =>
            {
                if (data is not PointerEventData pointer || previewCamera == null) return;
                float direction = Mathf.Clamp(pointer.scrollDelta.y, -1f, 1f);
                previewSize = Mathf.Clamp(previewSize - direction * 0.10f, 0.42f, 1.35f);
                previewCamera.orthographicSize = previewSize;
            });
            events.triggers.Add(scroll);
            var hint = MakeText(card, "상하좌우 드래그 · 휠로 확대/축소", 13, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), -165f, -162f, 220f, 24f);

            for (int i = 0; i < PartLabels.Length; i++)
            {
                var part = (AppearancePart)i;
                float y = 62f - i * 55f;
                var label = MakeText(card, PartLabels[i], 16, TextAnchor.MiddleLeft);
                Place(label.rectTransform, new Vector2(0.5f, 0.5f), 15f, y, 105f, 35f);
                var previous = CreateButton(card, "‹", () => Step(part, -1), ButtonColor);
                Place((RectTransform)previous.transform, new Vector2(0.5f, 0.5f), 88f, y, 34f, 34f);
                values[i] = MakeText(card, "", 16, TextAnchor.MiddleCenter);
                Place(values[i].rectTransform, new Vector2(0.5f, 0.5f), 167f, y, 116f, 35f);
                var next = CreateButton(card, "›", () => Step(part, 1), ButtonColor);
                Place((RectTransform)next.transform, new Vector2(0.5f, 0.5f), 247f, y, 34f, 34f);
            }

            var reset = CreateButton(card, "기본값으로", () =>
            {
                Array.Clear(Draft, 0, Draft.Length);
                Refresh();
            }, ButtonColor);
            Place((RectTransform)reset.transform, new Vector2(0.5f, 0.5f), 216f, -158f, 138f, 34f);
        }

        private void SelectRole(CharacterAppearance role)
        {
            selectedRole = role;
            playerTab.image.color = role == player ? AccentColor : ButtonColor;
            employeeTab.image.color = role == employee ? AccentColor : ButtonColor;
            Refresh();
        }

        private void Step(AppearancePart part, int direction)
        {
            int count = selectedRole.OptionCount(part);
            if (count <= 1) return;
            Draft[(int)part] = (Draft[(int)part] + direction + count) % count;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < values.Length; i++)
                values[i].text = OptionLabels[i][Draft[i]];

            if (selectedRole.OptionCount(AppearancePart.Model) == 0) return;
            var active = selectedRole.VisualForModel(Draft[0]);
            if (active == null) return;
            if (stage == null)
                BuildPreviewStage();
            if (preview != null)
            {
                preview.SetActive(false);
                Destroy(preview);
            }

            var staging = new GameObject("Preview model staging");
            staging.transform.SetParent(stage.transform, false);
            staging.SetActive(false);
            preview = Instantiate(active, staging.transform, false);
            preview.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, previewYaw, 0f));
            foreach (var child in preview.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = PreviewLayer;
            foreach (var behaviour in preview.GetComponentsInChildren<Behaviour>(true))
                if (!(behaviour is Animator))
                    behaviour.enabled = false;
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            var skull = preview.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skull != null)
            {
                skull.shadowCastingMode = ShadowCastingMode.Off;
                skull.updateWhenOffscreen = true;
                selectedRole.ApplyVisualOptions(skull, Draft);
            }
            preview.SetActive(true);
            preview.transform.SetParent(stage.transform, false);
            Destroy(staging);
        }

        private void UpdatePreviewCameraPose()
        {
            if (previewCamera == null) return;
            Vector3 offset = Quaternion.Euler(-previewPitch, 0f, 0f) * (Vector3.forward * 3.5f);
            previewCamera.transform.SetLocalPositionAndRotation(
                PreviewFocus + offset, Quaternion.LookRotation(-offset, Vector3.up));
        }

        private void BuildPreviewStage()
        {
            if (previewTexture == null)
            {
                previewTexture = new RenderTexture(336, 352, 24);
                previewTexture.Create();
                previewImage.texture = previewTexture;
            }
            stage = new GameObject("Character customization preview");
            stage.transform.SetParent(transform, false);
            stage.transform.position = new Vector3(0f, -2000f, 0f);

            var lens = new GameObject("Preview camera", typeof(Camera));
            lens.transform.SetParent(stage.transform, false);
            var camera = lens.GetComponent<Camera>();
            previewCamera = camera;
            UpdatePreviewCameraPose();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.21f, 0.26f, 1f);
            camera.cullingMask = 1 << PreviewLayer;
            camera.orthographic = true;
            camera.orthographicSize = previewSize;
            camera.farClipPlane = 8f;
            camera.targetTexture = previewTexture;

            var lamp = new GameObject("Preview key light", typeof(Light));
            lamp.transform.SetParent(stage.transform, false);
            lamp.transform.localPosition = new Vector3(0.9f, 2.4f, 2.4f);
            var lightSource = lamp.GetComponent<Light>();
            lightSource.type = LightType.Point;
            lightSource.shadows = LightShadows.None;
            lightSource.cullingMask = 1 << PreviewLayer;
            lightSource.range = 7f;
            lightSource.intensity = 5f;
        }

        private Button CreateButton(Transform parent, string label, Action action, Color color)
        {
            var rect = MakeRect(label, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var text = MakeText(rect, label, 17, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private Text MakeText(Transform parent, string value, int size, TextAnchor alignment)
        {
            var text = MakeRect(value, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, float x, float y, float width, float height)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void OnDestroy()
        {
            if (launcher != null)
                Destroy(launcher);
            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
            }
        }
    }
}
