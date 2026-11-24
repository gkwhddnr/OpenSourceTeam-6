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
        [SerializeField] private float fullReceiptHeight = 0.44f;
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
            SetStageActive(false);
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
