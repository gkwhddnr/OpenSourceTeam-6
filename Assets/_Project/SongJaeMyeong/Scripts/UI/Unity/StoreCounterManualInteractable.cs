using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    using Camera = UnityEngine.Camera;
    /// <summary>
    /// 계산대 위 설명서 큐브 오브젝트와 E 키로 상호작용해 편의점 운영 설명서 패널을 여닫는 컴포넌트.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class StoreCounterManualInteractable : MonoBehaviour
    {
        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private GameObject manualPanelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private float maxInteractDistance = 1.85f;

        private bool isManualOpen;
        private IDisposable modalLockLease;
        private bool hasNormalizedManualLayout;

        private void OnEnable()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseManual);
            }

            EnsureBookVisualAttached();
            if (TryGetComponent(out BoxCollider existingBox))
            {
                existingBox.center = new Vector3(0f, 0.035f, 0f);
                existingBox.size = new Vector3(0.34f, 0.085f, 0.24f);
            }
            NormalizeManualModalLayout();
            SetPromptVisible(false);
            if (manualPanelRoot != null)
            {
                manualPanelRoot.SetActive(false);
            }
        }

        private void EnsureBookVisualAttached()
        {
            if (transform.Find("Counter_ManualBookModel") != null) return;

#if UNITY_EDITOR
            GameObject bookPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ThirdParty/Shared/DanielRiches/BooksEssentials/Prefabs/BookBasicOpenWriting_1.prefab");
            if (bookPrefab == null)
            {
                bookPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ThirdParty/Shared/DanielRiches/BooksEssentials/Prefabs/BookBasicOpenWriting.prefab");
            }

            if (bookPrefab == null) return;

            MeshRenderer rootRenderer = GetComponent<MeshRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;
            Transform oldSpine = transform.Find("ManualSpineAccent");
            if (oldSpine != null) oldSpine.gameObject.SetActive(false);

            transform.position = new Vector3(-3.60f, 1.042f, -2.42f);
            transform.localScale = Vector3.one;
            transform.localEulerAngles = new Vector3(0f, -14f, 0f);

            BoxCollider boxCol = GetComponent<BoxCollider>();
            if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.center = new Vector3(0f, 0.035f, 0f);
            boxCol.size = new Vector3(0.34f, 0.085f, 0.24f);

            GameObject bookModel = Instantiate(bookPrefab, transform);
            bookModel.name = "Counter_ManualBookModel";
            Collider[] childCols = bookModel.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childCols.Length; i++)
            {
                if (childCols[i] != null) childCols[i].enabled = false;
            }

            MeshFilter mf = bookModel.GetComponentInChildren<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            Vector3 meshSize = mesh != null ? mesh.bounds.size : new Vector3(6.45f, 4.43f, 0.66f);
            float uniformScale = 0.34f / Mathf.Max(0.001f, meshSize.x);
            float zSign = GetOpenPagesLocalZSign(mesh);

            bookModel.transform.localPosition = Vector3.zero;
            bookModel.transform.localRotation = Quaternion.LookRotation(
                zSign >= 0f ? Vector3.up : Vector3.down,
                Vector3.forward);
            bookModel.transform.localScale = Vector3.one * uniformScale;

            ApplyRuntimeBookUrpMaterials(bookModel);

            Renderer bookRenderer = bookModel.GetComponentInChildren<Renderer>();
            if (bookRenderer != null)
            {
                float liftY = transform.position.y - bookRenderer.bounds.min.y;
                bookModel.transform.position += new Vector3(0f, liftY, 0f);
            }
#endif
        }

        private static float GetOpenPagesLocalZSign(Mesh mesh)
        {
            if (mesh == null || mesh.subMeshCount < 2) return 1f;
            Vector3[] verts = mesh.vertices;
            if (verts == null || verts.Length == 0) return 1f;

            int textSubmesh = mesh.subMeshCount >= 3 ? 2 : 1;
            int[] tris = mesh.GetTriangles(textSubmesh);
            if (tris == null || tris.Length == 0) return 1f;

            float sumZ = 0f;
            for (int i = 0; i < tris.Length; i++)
            {
                int vi = tris[i];
                if (vi >= 0 && vi < verts.Length) sumZ += verts[vi].z;
            }

            return ((sumZ / tris.Length) - mesh.bounds.center.z) >= 0f ? 1f : -1f;
        }

        private static void ApplyRuntimeBookUrpMaterials(GameObject instance)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null || instance == null) return;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] srcMats = renderers[r].sharedMaterials;
                if (srcMats == null) continue;
                Material[] dstMats = new Material[srcMats.Length];
                for (int m = 0; m < srcMats.Length; m++)
                {
                    Material src = srcMats[m];
                    string matName = src != null ? (src.name ?? string.Empty) : string.Empty;
                    Material mat = new Material(urpLit);
                    mat.name = "RuntimeURP_" + matName;

                    Color targetColor;
                    float smoothness;
                    if (matName.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        matName.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetColor = new Color(0.13f, 0.12f, 0.11f, 1f);
                        smoothness = 0.15f;
                    }
                    else if (matName.IndexOf("Paper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             matName.IndexOf("Page", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetColor = new Color(0.95f, 0.91f, 0.82f, 1f);
                        smoothness = 0.20f;
                    }
                    else if (matName.IndexOf("CoverEdge", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetColor = new Color(0.86f, 0.68f, 0.26f, 1f);
                        smoothness = 0.55f;
                    }
                    else
                    {
                        targetColor = new Color(0.24f, 0.15f, 0.10f, 1f);
                        smoothness = 0.35f;
                    }

                    mat.SetColor("_BaseColor", targetColor);
                    mat.SetFloat("_Smoothness", smoothness);
                    dstMats[m] = mat;
                }
                renderers[r].sharedMaterials = dstMats;
            }
        }

        private void OnDisable()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseManual);
            }

            ForceCloseWithoutAudio();
        }

        private void Update()
        {
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<StoreUiSessionController>();
                if (uiController == null) return;
            }

            if (playerCamera == null ||
                !playerCamera.isActiveAndEnabled ||
                playerCamera.gameObject.name == "LobbyOverviewCamera" ||
                playerCamera.gameObject.name == "PosSettlementCamera")
            {
                Camera mainCam = Camera.main;
                if (mainCam != null &&
                    mainCam.isActiveAndEnabled &&
                    mainCam.gameObject.name != "LobbyOverviewCamera" &&
                    mainCam.gameObject.name != "PosSettlementCamera")
                {
                    playerCamera = mainCam;
                }
            }

            // 영업 중 화면이 아니거나 화면 전환(N일차 페이드) 중에는 상호작용 프롬프트·입력을 숨긴다.
            if (uiController.CurrentMode != UiScreenMode.InGame || uiController.IsTransitionFading)
            {
                if (isManualOpen)
                {
                    ForceCloseWithoutAudio();
                }
                SetPromptVisible(false);
                return;
            }

            Keyboard kb = Keyboard.current;

            if (isManualOpen)
            {
                SetPromptVisible(false);
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                {
                    CloseManual();
                }
                return;
            }

            if (uiController.IsCommandOverlayOpen || uiController.IsPauseMenuOpen || uiController.IsSettingsOpen)
            {
                SetPromptVisible(false);
                return;
            }

            bool canInteract = IsPlayerLookingAtObject();
            SetPromptVisible(canInteract);

            if (canInteract && kb != null && kb.eKey.wasPressedThisFrame)
            {
                OpenManual();
            }
        }

        private bool IsPlayerLookingAtObject()
        {
            if (playerCamera == null) return false;

            float effectiveMaxDist = Mathf.Clamp(maxInteractDistance, 0.8f, 1.85f);
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, effectiveMaxDist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            Transform playerRoot = playerCamera.transform.root;
            float nearestDist = float.MaxValue;
            Transform nearestTr = null;

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTr = hits[i].transform;
                if (hitTr == null) continue;
                if (playerRoot != null && (hitTr == playerRoot || hitTr.IsChildOf(playerRoot))) continue;

                if (hits[i].distance < nearestDist)
                {
                    nearestDist = hits[i].distance;
                    nearestTr = hitTr;
                }
            }

            return nearestTr != null && (nearestTr == transform || nearestTr.IsChildOf(transform));
        }

        public void OpenManual()
        {
            if (isManualOpen) return;
            isManualOpen = true;
            SetPromptVisible(false);
            NormalizeManualModalLayout();

            if (uiController != null)
            {
                modalLockLease = uiController.AcquireExternalModalLock("StoreCounterManual");
                uiController.AudioPlayer?.PlaySfx(StoreSfxId.PaperFlip);
            }

            if (manualPanelRoot != null)
            {
                manualPanelRoot.SetActive(true);
            }
        }

        private void NormalizeManualModalLayout()
        {
            if (hasNormalizedManualLayout || manualPanelRoot == null) return;
            hasNormalizedManualLayout = true;

            // 설명서 모달이 하단 음성 도크/우측 포스트잇 뒤로 가려지지 않도록 캔버스 정렬 순서를 상위로 올린다.
            Canvas parentCanvas = manualPanelRoot.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentCanvas.overrideSorting = true;
                if (parentCanvas.sortingOrder < 60)
                {
                    parentCanvas.sortingOrder = 60;
                }
            }

            Transform borderTr = manualPanelRoot.transform.Find("ManualPaperBorder");
            if (borderTr is RectTransform borderRt)
            {
                borderRt.anchoredPosition = new Vector2(0f, 12f);
                borderRt.sizeDelta = new Vector2(880f, 720f);
            }

            Transform contentTr = manualPanelRoot.transform.Find("ManualPaperBorder/ManualPaperCard/ManualContent");
            if (contentTr is RectTransform contentRt)
            {
                contentRt.sizeDelta = new Vector2(1620f, 1320f);
                contentRt.localScale = new Vector3(0.5f, 0.5f, 1f);
            }

            if (contentTr != null)
            {
                Transform titleTr = contentTr.Find("ManualTitle");
                if (titleTr != null && titleTr.TryGetComponent(out Text titleText))
                {
                    titleText.fontStyle = FontStyle.Normal;
                    titleText.fontSize = 68;
                }

                Transform bodyTr = contentTr.Find("ManualBodyText");
                if (bodyTr != null && bodyTr.TryGetComponent(out Text bodyText))
                {
                    bodyText.fontStyle = FontStyle.Normal;
                    bodyText.fontSize = 33;
                    bodyText.lineSpacing = 1.20f;
                    bodyText.verticalOverflow = VerticalWrapMode.Overflow;
                    bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    bodyText.rectTransform.anchoredPosition = new Vector2(0f, -138f);
                    bodyText.rectTransform.sizeDelta = new Vector2(0f, 1050f);
                    bodyText.text =
                        "1. 매장 이동 및 시점 조작\n" +
                        "   • WASD : 편의점 내부 자유 이동 (계산대 통로로 출입 가능)\n" +
                        "   • 마우스 이동 : 1인칭 시점 회전  /  E 키 : 포스기 결제 · 설명서 열기/닫기\n\n" +
                        "2. 알바생 업무 지시 및 대화 기록\n" +
                        "   • R 키(누른 채 말하기) 또는 V 키 : 마이크로 알바생에게 직접 음성 지시\n" +
                        "   • Tab 키 : 보조 텍스트 명령창  /  H 키 : 하단 대화 기록 창 열기·접기\n\n" +
                        "3. 알바생 감정 · 스트레스 관리\n" +
                        "   • 스트레스 0~39 (안정) / 40~69 (불만) / 80 이상 (분노·위기 BGM)\n" +
                        "   • 스트레스 100 도달 시 알바생이 즉시 퇴사하며 게임이 종료됩니다.\n" +
                        "   • 부드러운 말투로 칭찬하거나 '잠깐 쉬어' 지시로 스트레스를 낮추세요.\n\n" +
                        "4. 영업 목표 및 시연 단축키\n" +
                        "   • 영업 시간(08:00 ~ 익일 08:00) 동안 목표 매출을 달성해 정산에 통과하세요.\n" +
                        "   • M : 매장 지도 열기·접기  /  G : 포스트잇 접기  /  F6 : 즉시 마감  /  F7 : +4시간";
                }

                Transform footerTr = contentTr.Find("ManualFooterText");
                if (footerTr != null && footerTr.TryGetComponent(out Text footerText))
                {
                    footerText.fontStyle = FontStyle.Normal;
                    footerText.fontSize = 32;
                    footerText.text = "※ 카운터 위 운영 설명서 책을 화면 중앙 포인터로 조준하고 [E]를 누르면 언제든 다시 확인할 수 있습니다.";
                    footerText.rectTransform.anchoredPosition = new Vector2(0f, 14f);
                    footerText.rectTransform.sizeDelta = new Vector2(0f, 60f);
                }
            }
        }

        public void CloseManual()
        {
            if (!isManualOpen) return;
            isManualOpen = false;

            if (manualPanelRoot != null)
            {
                manualPanelRoot.SetActive(false);
            }

            modalLockLease?.Dispose();
            modalLockLease = null;

            uiController?.AudioPlayer?.PlaySfx(StoreSfxId.PaperFlip);
        }

        private void ForceCloseWithoutAudio()
        {
            isManualOpen = false;
            SetPromptVisible(false);
            if (manualPanelRoot != null)
            {
                manualPanelRoot.SetActive(false);
            }

            modalLockLease?.Dispose();
            modalLockLease = null;
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptRoot != null && promptRoot.activeSelf != visible)
            {
                promptRoot.SetActive(visible);
            }
        }
    }
}
