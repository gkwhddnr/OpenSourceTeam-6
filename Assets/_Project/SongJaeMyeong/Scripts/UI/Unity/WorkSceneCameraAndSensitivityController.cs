using System.Reflection;
using OpenSourceTeam6.Movement.Unity;
using OpenSourceTeam6.Time.Unity;
using OpenSourceTeam6.UI.Core;
using UnityEngine;

namespace OpenSourceTeam6.UI.Unity
{
    using Camera = UnityEngine.Camera;
    /// <summary>
    /// 1) 로비·채용·정산·엔딩 화면에서는 제3자 조망 카메라(LobbyOverviewCamera)를 사용하고,
    ///    영업 시작(InGame) 시 1인칭 플레이어 카메라(PlayerRig)로 매끄럽게 전환한다.
    /// 2) 환경 설정의 마우스 감도 슬라이더(0.2x~3.0x)를 플레이어 MovementProfile 런타임 복제본에 실시간 반영한다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class WorkSceneCameraAndSensitivityController : MonoBehaviour
    {
        private static readonly FieldInfo RigProfileField =
            typeof(PlayerRigController).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ProfileSensitivityField =
            typeof(MovementProfile).GetField("lookSensitivity", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] private StoreUiSessionController uiController;
        [SerializeField] private PlayerRigController playerRig;
        [SerializeField] private Camera playerFirstPersonCamera;
        [SerializeField] private Camera lobbyOverviewCamera;
        [SerializeField] private Renderer playerVisualRenderer;
        [SerializeField] private TimeEnvironmentController timeEnvironment;
        [SerializeField] private Light sunLight;
        [SerializeField] private float lobbyGentleYawSwayDegrees = 1.6f;
        [SerializeField] private float lobbySwaySpeed = 0.22f;

        private MovementProfile runtimeProfile;
        private float baseLookSensitivity = 0.12f;
        private bool hasInitializedCameraMode;
        private bool isUsingFirstPersonCamera;
        private bool wasCursorLockedInGame;
        private Quaternion lobbyBaseRotation;

        private void Awake()
        {
            if (lobbyOverviewCamera != null)
            {
                lobbyBaseRotation = lobbyOverviewCamera.transform.rotation;
            }

            PrepareRuntimeMovementProfile();
        }

        private void OnEnable()
        {
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<StoreUiSessionController>();
            }

            if (uiController != null)
            {
                uiController.MouseSensitivityChanged += ApplyMouseSensitivityMultiplier;
            }
        }

        private void Start()
        {
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<StoreUiSessionController>();
                if (uiController != null)
                {
                    uiController.MouseSensitivityChanged += ApplyMouseSensitivityMultiplier;
                }
            }

            float savedMultiplier = uiController != null
                ? uiController.CurrentMouseSensitivityMultiplier
                : PlayerPrefs.GetFloat(SettingsPanelPresenter.MouseSensitivityPrefKey, 1.0f);
            ApplyMouseSensitivityMultiplier(savedMultiplier);

            ApplyCameraMode(useFirstPerson: uiController != null && uiController.CurrentMode == UiScreenMode.InGame, force: true);
        }

        private void OnDisable()
        {
            if (uiController != null)
            {
                uiController.MouseSensitivityChanged -= ApplyMouseSensitivityMultiplier;
            }
        }

        private void OnDestroy()
        {
            if (runtimeProfile != null)
            {
                Destroy(runtimeProfile);
                runtimeProfile = null;
            }
        }

        private void Update()
        {
            if (uiController != null)
            {
                bool shouldUseFirstPerson = uiController.CurrentMode == UiScreenMode.InGame;
                if (!hasInitializedCameraMode || shouldUseFirstPerson != isUsingFirstPersonCamera)
                {
                    ApplyCameraMode(shouldUseFirstPerson, force: false);
                }

#if UNITY_EDITOR
                DetectEditorEscapeUnlockForPause();
#endif
            }

            if (!isUsingFirstPersonCamera && lobbyOverviewCamera != null && lobbyGentleYawSwayDegrees > 0.01f)
            {
                float yawOffset = Mathf.Sin(UnityEngine.Time.unscaledTime * lobbySwaySpeed) * lobbyGentleYawSwayDegrees;
                lobbyOverviewCamera.transform.rotation = lobbyBaseRotation * Quaternion.Euler(0f, yawOffset, 0f);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Unity 에디터 Game 뷰에서는 마우스 커서가 Locked 상태일 때 첫 Esc 입력을 에디터가 가로채어
        /// 커서 잠금만 해제하고 InputSystem으로 전달하지 않는다.
        /// PlayerRigController(-50)보다 먼저(-60) 커서 잠금 해제를 감지해 즉시 일시정지를 열어준다.
        /// </summary>
        private void DetectEditorEscapeUnlockForPause()
        {
            if (uiController == null ||
                uiController.CurrentMode != UiScreenMode.InGame ||
                uiController.IsPauseMenuOpen ||
                uiController.IsSettingsOpen ||
                uiController.IsCommandOverlayOpen ||
                uiController.IsExternalModalOpen ||
                !Application.isFocused)
            {
                wasCursorLockedInGame = false;
                return;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                wasCursorLockedInGame = true;
                return;
            }

            if (wasCursorLockedInGame && Cursor.lockState != CursorLockMode.Locked)
            {
                wasCursorLockedInGame = false;
                uiController.OpenPauseMenu();
            }
        }
#endif

        private void PrepareRuntimeMovementProfile()
        {
            if (playerRig == null || RigProfileField == null || ProfileSensitivityField == null)
            {
                return;
            }

            MovementProfile original = RigProfileField.GetValue(playerRig) as MovementProfile;
            if (original == null)
            {
                return;
            }

            baseLookSensitivity = Mathf.Max(0.01f, original.LookSensitivity);
            runtimeProfile = Instantiate(original);
            runtimeProfile.name = original.name + "_Runtime";
            RigProfileField.SetValue(playerRig, runtimeProfile);
        }

        public void ApplyMouseSensitivityMultiplier(float multiplier)
        {
            if (runtimeProfile == null || ProfileSensitivityField == null)
            {
                return;
            }

            float clampedMultiplier = Mathf.Clamp(
                multiplier,
                SettingsPanelPresenter.MinMouseSensitivity,
                SettingsPanelPresenter.MaxMouseSensitivity);
            float effectiveSensitivity = Mathf.Clamp(baseLookSensitivity * clampedMultiplier, 0.01f, 2.0f);
            ProfileSensitivityField.SetValue(runtimeProfile, effectiveSensitivity);
        }

        private void ApplyCameraMode(bool useFirstPerson, bool force)
        {
            if (!force && hasInitializedCameraMode && isUsingFirstPersonCamera == useFirstPerson)
            {
                return;
            }

            hasInitializedCameraMode = true;
            isUsingFirstPersonCamera = useFirstPerson;

            if (lobbyOverviewCamera != null)
            {
                lobbyOverviewCamera.enabled = !useFirstPerson;
                lobbyOverviewCamera.tag = useFirstPerson ? "Untagged" : "MainCamera";
            }

            if (playerFirstPersonCamera != null)
            {
                playerFirstPersonCamera.enabled = useFirstPerson;
                playerFirstPersonCamera.tag = useFirstPerson ? "MainCamera" : "Untagged";
            }

            if (playerVisualRenderer != null)
            {
                // 제3자 로비 카메라에서는 주황색 플레이어 캡슐을 보여주고, 1인칭 플레이 시에는 시야를 가리지 않도록 숨긴다.
                playerVisualRenderer.enabled = !useFirstPerson;
            }

            Camera activeCam = useFirstPerson ? playerFirstPersonCamera : lobbyOverviewCamera;
            if (activeCam != null && timeEnvironment != null)
            {
                Light targetSun = sunLight != null ? sunLight : RenderSettings.sun;
                if (targetSun != null)
                {
                    timeEnvironment.Configure(targetSun, activeCam);
                }
            }
        }
    }
}
