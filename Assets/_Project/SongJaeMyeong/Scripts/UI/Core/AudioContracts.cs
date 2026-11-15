using System;

namespace OpenSourceTeam6.UI.Core
{
    /// <summary>
    /// 기능정의서 SND-01: 게임 진행 및 감정 위기 상태에 따라 전환 재생되는 배경음악(BGM) 트랙 식별자.
    /// </summary>
    public enum StoreBgmTrack
    {
        None = 0,
        Lobby = 1,
        DayNormal = 2,
        DayCrisis = 3,
        SettlementOrEnding = 4
    }

    /// <summary>
    /// 기능정의서 SND-02(기본·매장 효과음), SND-03(음성·감정 반응 알림음) 및
    /// 추후 캐릭터 모션·상호작용 이벤트 연동을 위한 효과음(SFX) 식별자.
    /// </summary>
    public enum StoreSfxId
    {
        // SND-02: UI 및 매장 기본 효과음
        UiClick = 0,
        UiPopupOpenClose = 1,
        StoreDoorChime = 2,
        BarcodeCheckout = 3,
        WorkTaskComplete = 4,
        PaperFlip = 5,

        // SND-03: 음성 인식 및 직원 감정 반응 알림음
        MicRecordStart = 10,
        MicRecordStop = 11,
        ReactionAccept = 12,
        ReactionComplain = 13,
        ReactionRefuseOrCrisis = 14,

        // 추후 캐릭터 모션(AnimationEvent) 및 매장 상호작용 연동용
        EmployeeFootstep = 20,
        ItemPickupOrPlace = 21
    }

    /// <summary>
    /// 기능정의서 UI-04 및 SND-00: 마스터·BGM·SFX 채널별 볼륨 및 음소거 상태 스냅샷.
    /// </summary>
    public readonly struct StoreAudioVolumeState
    {
        public float MasterVolume { get; }
        public float BgmVolume { get; }
        public float SfxVolume { get; }
        public bool IsBgmMuted { get; }

        public float EffectiveBgmVolume => IsBgmMuted ? 0f : MasterVolume * BgmVolume;
        public float EffectiveSfxVolume => MasterVolume * SfxVolume;

        public StoreAudioVolumeState(
            float masterVolume,
            float bgmVolume,
            float sfxVolume,
            bool isBgmMuted = false)
        {
            MasterVolume = StoreAudioRules.ClampVolume01(masterVolume);
            BgmVolume = StoreAudioRules.ClampVolume01(bgmVolume);
            SfxVolume = StoreAudioRules.ClampVolume01(sfxVolume);
            IsBgmMuted = isBgmMuted;
        }

        public static StoreAudioVolumeState CreateDefault()
        {
            return new StoreAudioVolumeState(
                masterVolume: 1.0f,
                bgmVolume: 0.8f,
                sfxVolume: 1.0f,
                isBgmMuted: false);
        }
    }

    /// <summary>
    /// 외부 모듈(UI, 이동·모션 애니메이션 이벤트, 매장 상호작용, AI 행동 판정)에서
    /// 사운드 시스템을 단일 규격으로 호출하기 위한 공용 인터페이스.
    /// </summary>
    public interface IStoreAudioPlayer
    {
        StoreBgmTrack CurrentBgm { get; }
        StoreAudioVolumeState VolumeState { get; }

        event Action<StoreAudioVolumeState> VolumeStateChanged;

        void PlayBgm(StoreBgmTrack track, bool forceRestart = false);
        void StopBgm();
        void PlaySfx(StoreSfxId sfxId, float volumeScale = 1f);

        void SetMasterVolume(float volume01);
        void SetBgmVolume(float volume01);
        void SetSfxVolume(float volume01);
        void SetBgmMuted(bool isMuted);
    }

    /// <summary>
    /// 엔진 비의존(Pure C#) 사운드 볼륨 계산 및 게임 상태별 BGM·SFX 자동 판별 규칙.
    /// </summary>
    public static class StoreAudioRules
    {
        public static float ClampVolume01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "오디오 볼륨 수치는 유한한 값이어야 합니다.");
            }

            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        /// <summary>
        /// 현재 화면 모드와 직원의 스트레스 수치에 따라 재생해야 할 BGM 트랙(SND-01)을 결정한다.
        /// </summary>
        public static StoreBgmTrack ResolveBgmForState(UiScreenMode screenMode, float currentStress)
        {
            switch (screenMode)
            {
                case UiScreenMode.Lobby:
                    return StoreBgmTrack.Lobby;

                case UiScreenMode.Settlement:
                    return StoreBgmTrack.SettlementOrEnding;

                case UiScreenMode.InGame:
                    StressBand band = StressRules.Classify(currentStress);
                    return band == StressBand.Angry
                        ? StoreBgmTrack.DayCrisis
                        : StoreBgmTrack.DayNormal;

                default:
                    return StoreBgmTrack.None;
            }
        }

        /// <summary>
        /// 음성 인식 단계(VoiceProcessStage) 전환 시 재생할 마이크 알림음(SND-03)을 결정한다.
        /// </summary>
        public static bool TryResolveVoiceStageSfx(
            VoiceProcessStage previousStage,
            VoiceProcessStage nextStage,
            out StoreSfxId sfxId)
        {
            if (previousStage != VoiceProcessStage.Listening && nextStage == VoiceProcessStage.Listening)
            {
                sfxId = StoreSfxId.MicRecordStart;
                return true;
            }

            if (previousStage == VoiceProcessStage.Listening && nextStage != VoiceProcessStage.Listening)
            {
                sfxId = StoreSfxId.MicRecordStop;
                return true;
            }

            sfxId = StoreSfxId.UiClick;
            return false;
        }

        /// <summary>
        /// AI 명령 피드백 종류(CommandFeedbackKind)에 따라 재생할 직원 반응 효과음(SND-03)을 결정한다.
        /// </summary>
        public static bool TryResolveFeedbackSfx(CommandFeedbackKind feedbackKind, out StoreSfxId sfxId)
        {
            switch (feedbackKind)
            {
                case CommandFeedbackKind.Accepted:
                    sfxId = StoreSfxId.ReactionAccept;
                    return true;

                case CommandFeedbackKind.RefusedByEmotion:
                    sfxId = StoreSfxId.ReactionRefuseOrCrisis;
                    return true;

                case CommandFeedbackKind.RecognitionFailed:
                case CommandFeedbackKind.SystemOrMoveError:
                    sfxId = StoreSfxId.ReactionComplain;
                    return true;

                default:
                    sfxId = StoreSfxId.UiClick;
                    return false;
            }
        }

        /// <summary>
        /// 직원 반응 판정(EmployeeReaction)에 대응하는 감정 효과음(SND-03)을 반환한다.
        /// </summary>
        public static StoreSfxId ResolveReactionSfx(EmployeeReaction reaction)
        {
            switch (reaction)
            {
                case EmployeeReaction.AcceptNormal:
                    return StoreSfxId.ReactionAccept;

                case EmployeeReaction.AcceptComplain:
                case EmployeeReaction.SlackOff:
                    return StoreSfxId.ReactionComplain;

                case EmployeeReaction.RefuseEmotion:
                default:
                    return StoreSfxId.ReactionRefuseOrCrisis;
            }
        }
    }
}
