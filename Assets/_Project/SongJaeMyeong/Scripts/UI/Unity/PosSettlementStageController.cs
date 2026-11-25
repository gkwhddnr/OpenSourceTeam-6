using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.UI.Unity
{
    using Camera = UnityEngine.Camera;
    using Time = UnityEngine.Time;

    /// <summary>
    /// 일차 정산(Settlement) 시 검은 배경 전용 카메라(PosSettlementCamera)에
    /// 편의점 맵의 3D 포스기를 크게 띄우고, 클릭 시 포스기를 좌측으로 이동시키며
    /// 3D 영수증 종이가 인쇄되어 올라오는 연출을 재생한다.
    /// </summary>
    public sealed class PosSettlementStageController : MonoBehaviour
    {
        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private SettlementPanelPresenter settlementPanel;
        [SerializeField] private Camera posSettlementCamera;
        [SerializeField] private GameObject posStageRoot;
        [SerializeField] private Transform posMachinePivot;
        [SerializeField] private Transform receiptPaperStrip;
        [SerializeField] private Vector3 centeredLocalPos = new Vector3(0f, -0.35f, 1.95f);
        [SerializeField] private Vector3 shiftedLeftLocalPos = new Vector3(-0.78f, -0.35f, 2.25f);
        [SerializeField] private Vector3 centeredEuler = new Vector3(12f, 165f, 0f);
        [SerializeField] private Vector3 shiftedLeftEuler = new Vector3(11f, 152f, 0f);
        [SerializeField] private float fullReceiptHeight = 0.28f;
        [SerializeField] private float printDurationSeconds = 0.72f;

        private bool stageActive;
        private bool isPrinted;
        private bool isPrinting;
        private float printElapsed;

        private void OnEnable()
        {
            if (settlementPanel != null)
            {
                settlementPanel.PosPrintRequested += HandlePosPrintRequested;
            }
            EnsureReceiptBookPageVisualAttached();
            SetStageActive(false);
        }

        private void EnsureReceiptBookPageVisualAttached()
        {
            if (receiptPaperStrip == null) return;

            if (posMachinePivot != null && posMachinePivot.Find("Register_Model") != null)
            {
                receiptPaperStrip.localPosition = new Vector3(0.098f, 0.20f, 0.03f);
                fullReceiptHeight = 0.28f;
            }

            Transform existingPage = receiptPaperStrip.Find("PosReceiptBookPageModel");
            if (existingPage != null)
            {
                MeshFilter existingMf = existingPage.GetComponentInChildren<MeshFilter>();
                if (existingMf != null && existingMf.sharedMesh != null)
                {
                    Bounds emb = existingMf.sharedMesh.bounds;
                    float esx = 0.145f / Mathf.Max(0.001f, emb.size.x);
                    float esy = 1.00f / Mathf.Max(0.001f, emb.size.y);
                    existingPage.localScale = new Vector3(esx, esy, esx);
                    existingPage.localPosition = new Vector3(
                        -emb.center.x * esx,
                        0.5f - emb.center.y * esy,
                        -emb.center.z * esx);
                }
                return;
            }

#if UNITY_EDITOR
            GameObject pagePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/DanielRiches/BooksEssentials/Prefabs/BookPageWriting.prefab");
            if (pagePrefab == null) return;

            for (int i = 0; i < receiptPaperStrip.childCount; i++)
            {
                Transform child = receiptPaperStrip.GetChild(i);
                if (child != null) child.gameObject.SetActive(false);
            }

            GameObject pageModel = Instantiate(pagePrefab, receiptPaperStrip);
            pageModel.name = "PosReceiptBookPageModel";
            Collider[] cols = pageModel.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }

            MeshFilter mf = pageModel.GetComponentInChildren<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            Bounds mb = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, new Vector3(2.55f, 3.98f, 0.044f));
            float sx = 0.145f / Mathf.Max(0.001f, mb.size.x);
            float sy = 1.00f / Mathf.Max(0.001f, mb.size.y);
            float sz = sx;

            float zSign = 1f;
            if (mesh != null && mesh.subMeshCount >= 2)
            {
                Vector3[] verts = mesh.vertices;
                int[] tris = mesh.GetTriangles(mesh.subMeshCount - 1);
                if (verts != null && tris != null && tris.Length > 0)
                {
                    float sumZ = 0f;
                    for (int i = 0; i < tris.Length; i++)
                    {
                        int vi = tris[i];
                        if (vi >= 0 && vi < verts.Length) sumZ += verts[vi].z;
                    }
                    zSign = ((sumZ / tris.Length) - mb.center.z) >= 0f ? 1f : -1f;
                }
            }

            pageModel.transform.localRotation = zSign >= 0f
                ? Quaternion.identity
                : Quaternion.Euler(0f, 180f, 0f);
            pageModel.transform.localScale = new Vector3(sx, sy, sz);
            pageModel.transform.localPosition = new Vector3(
                -mb.center.x * sx,
                0.5f - mb.center.y * sy,
                -mb.center.z * sz);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                Renderer[] renderers = pageModel.GetComponentsInChildren<Renderer>(true);
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
                        bool isText = matName.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      matName.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0;
                        mat.SetColor("_BaseColor", isText
                            ? new Color(0.13f, 0.12f, 0.11f, 1f)
                            : new Color(0.96f, 0.95f, 0.90f, 1f));
                        mat.SetFloat("_Smoothness", isText ? 0.15f : 0.20f);
                        dstMats[m] = mat;
                    }
                    renderers[r].sharedMaterials = dstMats;
                }
            }
#endif
        }

        private void OnDisable()
        {
            if (settlementPanel != null)
            {
                settlementPanel.PosPrintRequested -= HandlePosPrintRequested;
            }
            SetStageActive(false);
        }

        private void Start()
        {
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<StoreUiSessionController>();
            }
            if (settlementPanel == null)
            {
                settlementPanel = FindAnyObjectByType<SettlementPanelPresenter>(FindObjectsInactive.Include);
                if (settlementPanel != null)
                {
                    settlementPanel.PosPrintRequested -= HandlePosPrintRequested;
                    settlementPanel.PosPrintRequested += HandlePosPrintRequested;
                }
            }
        }

        private void Update()
        {
            bool shouldShowStage = uiController != null &&
                                   uiController.CurrentMode == UiScreenMode.Settlement &&
                                   uiController.IsSettlementVisible &&
                                   !uiController.IsEmployeeSettlementVisible &&
                                   !uiController.IsEndingVisible;

            if (shouldShowStage != stageActive)
            {
                SetStageActive(shouldShowStage);
            }

            if (!stageActive) return;

            // 아직 출력 전이면 3D 포스기 직접 클릭(Raycast)도 지원
            if (!isPrinted && !isPrinting && posSettlementCamera != null)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 screenPos = mouse.position.ReadValue();
                    Ray ray = posSettlementCamera.ScreenPointToRay(screenPos);
                    if (Physics.Raycast(ray, out RaycastHit hit, 12f) &&
                        posMachinePivot != null &&
                        (hit.transform == posMachinePivot || hit.transform.IsChildOf(posMachinePivot)))
                    {
                        settlementPanel?.TriggerPosPrintFromExternalClick();
                    }
                }
            }

            if (isPrinting)
            {
                printElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(printElapsed / Mathf.Max(0.05f, printDurationSeconds));
                float eased = Mathf.SmoothStep(0f, 1f, t);

                if (posMachinePivot != null)
                {
                    posMachinePivot.localPosition = Vector3.Lerp(centeredLocalPos, shiftedLeftLocalPos, eased);
                    posMachinePivot.localRotation = Quaternion.Slerp(
                        Quaternion.Euler(centeredEuler),
                        Quaternion.Euler(shiftedLeftEuler),
                        eased);
                }

                if (receiptPaperStrip != null)
                {
                    float paperProgress = Mathf.Clamp01((t - 0.12f) / 0.88f);
                    float paperEased = Mathf.SmoothStep(0f, 1f, paperProgress);
                    Vector3 sc = receiptPaperStrip.localScale;
                    sc.y = Mathf.Lerp(0.01f, fullReceiptHeight, paperEased);
                    receiptPaperStrip.localScale = sc;
                    receiptPaperStrip.gameObject.SetActive(paperProgress > 0.01f);
                }

                if (t >= 1f)
                {
                    isPrinting = false;
                    isPrinted = true;
                }
            }
            else if (!isPrinted && posMachinePivot != null)
            {
                // 클릭 대기 중 아주 부드러운 부유 회전감
                float sway = Mathf.Sin(Time.unscaledTime * 1.4f) * 2.2f;
                posMachinePivot.localPosition = centeredLocalPos;
                posMachinePivot.localRotation = Quaternion.Euler(centeredEuler.x, centeredEuler.y + sway, centeredEuler.z);
            }
        }

        private void SetStageActive(bool active)
        {
            stageActive = active;
            if (posStageRoot != null) posStageRoot.SetActive(active);
            if (posSettlementCamera != null) posSettlementCamera.enabled = active;

            if (active)
            {
                ResetPosToInitialPose();
            }
        }

        private void ResetPosToInitialPose()
        {
            isPrinted = false;
            isPrinting = false;
            printElapsed = 0f;

            if (posMachinePivot != null)
            {
                posMachinePivot.localPosition = centeredLocalPos;
                posMachinePivot.localRotation = Quaternion.Euler(centeredEuler);
            }

            if (receiptPaperStrip != null)
            {
                Vector3 sc = receiptPaperStrip.localScale;
                sc.y = 0.01f;
                receiptPaperStrip.localScale = sc;
                receiptPaperStrip.gameObject.SetActive(false);
            }
        }

        private void HandlePosPrintRequested()
        {
            if (isPrinted || isPrinting) return;
            isPrinting = true;
            printElapsed = 0f;
            if (receiptPaperStrip != null)
            {
                receiptPaperStrip.gameObject.SetActive(true);
            }
        }
    }
}
