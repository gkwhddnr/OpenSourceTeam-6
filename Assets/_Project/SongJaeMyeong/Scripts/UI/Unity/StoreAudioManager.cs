using System;
using System.Collections.Generic;
using OpenSourceTeam6.UI.Core;
using UnityEngine;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 기능정의서 SND-00 ~ SND-03 및 UI-04 볼륨 연동 담당 중앙 사운드 매니저.
    /// 외부 오디오 에셋이 할당되면 해당 에셋을 우선 재생하며,
    /// 에셋 미할당 슬롯은 프로시저럴 기본 파형(Fallback)으로 즉시 청각 피드백을 제공한다.
    /// </summary>
    public sealed class StoreAudioManager : MonoBehaviour, IStoreAudioPlayer
    {
        private const int FallbackSampleRate = 22050;

        [Header("Audio Sources (미할당 시 자식 오브젝트로 자동 생성)")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("Initial Volume Settings (UI-04 연동 기본값)")]
        [SerializeField, Range(0f, 1f)] private float _initialMasterVolume = 1.0f;
        [SerializeField, Range(0f, 1f)] private float _initialBgmVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _initialSfxVolume = 1.0f;
        [SerializeField] private bool _initialBgmMuted;

        [Header("Fallback Options (외부 에셋 임포트 전 테스트용 파형 자동 생성)")]
        [SerializeField] private bool _useProceduralFallbackWhenClipMissing = true;

        [Header("SND-01: 배경음악 (BGM 4종)")]
        [SerializeField] private AudioClip _lobbyBgmClip;
        [SerializeField] private AudioClip _dayNormalBgmClip;
        [SerializeField] private AudioClip _dayCrisisBgmClip;
        [SerializeField] private AudioClip _settlementBgmClip;

        [Header("SND-02: UI 및 매장 기본 효과음 (SFX)")]
        [SerializeField] private AudioClip _uiClickClip;
        [SerializeField] private AudioClip _uiPopupOpenCloseClip;
        [SerializeField] private AudioClip _storeDoorChimeClip;
        [SerializeField] private AudioClip _barcodeCheckoutClip;
        [SerializeField] private AudioClip _workTaskCompleteClip;
        [SerializeField] private AudioClip _paperFlipClip;

        [Header("SND-03: 음성 명령 및 직원 감정 반응 알림음")]
        [SerializeField] private AudioClip _micRecordStartClip;
        [SerializeField] private AudioClip _micRecordStopClip;
        [SerializeField] private AudioClip _reactionAcceptClip;
        [SerializeField] private AudioClip _reactionComplainClip;
        [SerializeField] private AudioClip _reactionRefuseOrCrisisClip;

        [Header("추후 모션(AnimationEvent) 및 매장 상호작용 연동용 효과음")]
        [SerializeField] private AudioClip _employeeFootstepClip;
        [SerializeField] private AudioClip _itemPickupOrPlaceClip;

        private readonly Dictionary<StoreBgmTrack, AudioClip> _fallbackBgmClips = new Dictionary<StoreBgmTrack, AudioClip>();
        private readonly Dictionary<StoreSfxId, AudioClip> _fallbackSfxClips = new Dictionary<StoreSfxId, AudioClip>();
        private readonly List<AudioClip> _createdProceduralClips = new List<AudioClip>();

        private StoreAudioVolumeState _volumeState;

        public StoreBgmTrack CurrentBgm { get; private set; } = StoreBgmTrack.None;
        public StoreAudioVolumeState VolumeState => _volumeState;

        public event Action<StoreAudioVolumeState> VolumeStateChanged;

        private void Awake()
        {
            EnsureAudioSources();
            _volumeState = new StoreAudioVolumeState(
                _initialMasterVolume,
                _initialBgmVolume,
                _initialSfxVolume,
                _initialBgmMuted);
            ApplyVolumeToSources();
        }

        private void OnDestroy()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Stop();
            }

            for (int i = 0; i < _createdProceduralClips.Count; i++)
            {
                AudioClip clip = _createdProceduralClips[i];
                if (clip != null)
                {
                    Destroy(clip);
                }
            }

            _createdProceduralClips.Clear();
            _fallbackBgmClips.Clear();
            _fallbackSfxClips.Clear();
        }

        public void PlayBgm(StoreBgmTrack track, bool forceRestart = false)
        {
            EnsureAudioSources();

            if (track == StoreBgmTrack.None)
            {
                StopBgm();
                return;
            }

            if (!forceRestart && CurrentBgm == track && _bgmSource.isPlaying)
            {
                return;
            }

            AudioClip clip = ResolveBgmClip(track);
            CurrentBgm = track;

            if (clip == null)
            {
                _bgmSource.Stop();
                return;
            }

            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            ApplyVolumeToSources();
            _bgmSource.Play();
        }

        public void StopBgm()
        {
            CurrentBgm = StoreBgmTrack.None;
            if (_bgmSource != null)
            {
                _bgmSource.Stop();
                _bgmSource.clip = null;
            }
        }

        public void PlaySfx(StoreSfxId sfxId, float volumeScale = 1f)
        {
            EnsureAudioSources();

            AudioClip clip = ResolveSfxClip(sfxId);
            if (clip == null) return;

            float clampedScale = StoreAudioRules.ClampVolume01(volumeScale);
            float finalVolume = _volumeState.EffectiveSfxVolume * clampedScale;
            if (finalVolume <= 0.0001f) return;

            _sfxSource.PlayOneShot(clip, finalVolume);
        }

        public void PlayReactionSfx(EmployeeReaction reaction)
        {
            StoreSfxId sfxId = StoreAudioRules.ResolveReactionSfx(reaction);
            PlaySfx(sfxId);
        }

        public void SetMasterVolume(float volume01)
        {
            _volumeState = new StoreAudioVolumeState(
                volume01,
                _volumeState.BgmVolume,
                _volumeState.SfxVolume,
                _volumeState.IsBgmMuted);
            ApplyVolumeToSources();
            VolumeStateChanged?.Invoke(_volumeState);
        }

        public void SetBgmVolume(float volume01)
        {
            _volumeState = new StoreAudioVolumeState(
                _volumeState.MasterVolume,
                volume01,
                _volumeState.SfxVolume,
                _volumeState.IsBgmMuted);
            ApplyVolumeToSources();
            VolumeStateChanged?.Invoke(_volumeState);
        }

        public void SetSfxVolume(float volume01)
        {
            _volumeState = new StoreAudioVolumeState(
                _volumeState.MasterVolume,
                _volumeState.BgmVolume,
                volume01,
                _volumeState.IsBgmMuted);
            ApplyVolumeToSources();
            VolumeStateChanged?.Invoke(_volumeState);
        }

        public void SetBgmMuted(bool isMuted)
        {
            _volumeState = new StoreAudioVolumeState(
                _volumeState.MasterVolume,
                _volumeState.BgmVolume,
                _volumeState.SfxVolume,
                isMuted);
            ApplyVolumeToSources();
            VolumeStateChanged?.Invoke(_volumeState);
        }

        // === 추후 캐릭터 모션(AnimationEvent) 및 매장 상호작용 컴포넌트 직접 연결용 헬퍼 ===
        public void OnAnimationFootstep() => PlaySfx(StoreSfxId.EmployeeFootstep, 0.55f);
        public void OnAnimationItemInteract() => PlaySfx(StoreSfxId.ItemPickupOrPlace, 0.85f);
        public void OnAnimationWorkComplete() => PlaySfx(StoreSfxId.WorkTaskComplete, 1.0f);
        public void OnStoreDoorOpened() => PlaySfx(StoreSfxId.StoreDoorChime, 1.0f);
        public void OnCheckoutCompleted() => PlaySfx(StoreSfxId.BarcodeCheckout, 1.0f);

        private void EnsureAudioSources()
        {
            if (_bgmSource == null)
            {
                Transform existingBgm = transform.Find("BgmAudioSource");
                GameObject bgmObj = existingBgm != null ? existingBgm.gameObject : new GameObject("BgmAudioSource");
                bgmObj.transform.SetParent(transform, false);
                _bgmSource = bgmObj.GetComponent<AudioSource>();
                if (_bgmSource == null) _bgmSource = bgmObj.AddComponent<AudioSource>();
                _bgmSource.playOnAwake = false;
                _bgmSource.loop = true;
                _bgmSource.spatialBlend = 0f;
            }

            if (_sfxSource == null)
            {
                Transform existingSfx = transform.Find("SfxAudioSource");
                GameObject sfxObj = existingSfx != null ? existingSfx.gameObject : new GameObject("SfxAudioSource");
                sfxObj.transform.SetParent(transform, false);
                _sfxSource = sfxObj.GetComponent<AudioSource>();
                if (_sfxSource == null) _sfxSource = sfxObj.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.spatialBlend = 0f;
            }
        }

        private void ApplyVolumeToSources()
        {
            if (_bgmSource != null)
            {
                _bgmSource.volume = _volumeState.EffectiveBgmVolume;
                _bgmSource.mute = _volumeState.IsBgmMuted;
            }

            if (_sfxSource != null)
            {
                _sfxSource.volume = _volumeState.EffectiveSfxVolume;
            }
        }

        private AudioClip ResolveBgmClip(StoreBgmTrack track)
        {
            AudioClip assigned = null;
            switch (track)
            {
                case StoreBgmTrack.Lobby:
                    assigned = _lobbyBgmClip;
                    break;
                case StoreBgmTrack.DayNormal:
                    assigned = _dayNormalBgmClip;
                    break;
                case StoreBgmTrack.DayCrisis:
                    assigned = _dayCrisisBgmClip;
                    break;
                case StoreBgmTrack.SettlementOrEnding:
                    assigned = _settlementBgmClip;
                    break;
            }

            if (assigned != null || !_useProceduralFallbackWhenClipMissing)
            {
                return assigned;
            }

            if (_fallbackBgmClips.TryGetValue(track, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            AudioClip generated = CreateProceduralBgmClip(track);
            _fallbackBgmClips[track] = generated;
            return generated;
        }

        private AudioClip ResolveSfxClip(StoreSfxId sfxId)
        {
            AudioClip assigned = null;
            switch (sfxId)
            {
                case StoreSfxId.UiClick:
                    assigned = _uiClickClip;
                    break;
                case StoreSfxId.UiPopupOpenClose:
                    assigned = _uiPopupOpenCloseClip;
                    break;
                case StoreSfxId.StoreDoorChime:
                    assigned = _storeDoorChimeClip;
                    break;
                case StoreSfxId.BarcodeCheckout:
                    assigned = _barcodeCheckoutClip;
                    break;
                case StoreSfxId.WorkTaskComplete:
                    assigned = _workTaskCompleteClip;
                    break;
                case StoreSfxId.PaperFlip:
                    assigned = _paperFlipClip;
                    break;
                case StoreSfxId.MicRecordStart:
                    assigned = _micRecordStartClip;
                    break;
                case StoreSfxId.MicRecordStop:
                    assigned = _micRecordStopClip;
                    break;
                case StoreSfxId.ReactionAccept:
                    assigned = _reactionAcceptClip;
                    break;
                case StoreSfxId.ReactionComplain:
                    assigned = _reactionComplainClip;
                    break;
                case StoreSfxId.ReactionRefuseOrCrisis:
                    assigned = _reactionRefuseOrCrisisClip;
                    break;
                case StoreSfxId.EmployeeFootstep:
                    assigned = _employeeFootstepClip;
                    break;
                case StoreSfxId.ItemPickupOrPlace:
                    assigned = _itemPickupOrPlaceClip;
                    break;
            }

            if (assigned != null || !_useProceduralFallbackWhenClipMissing)
            {
                return assigned;
            }

            if (_fallbackSfxClips.TryGetValue(sfxId, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            AudioClip generated = CreateProceduralSfxClip(sfxId);
            _fallbackSfxClips[sfxId] = generated;
            return generated;
        }

        private AudioClip CreateProceduralSfxClip(StoreSfxId sfxId)
        {
            switch (sfxId)
            {
                case StoreSfxId.StoreDoorChime:
                    // 편의점 출입문 벨소리 ("띠-링~": E5 -> C5)
                    return CreateTwoToneClip("Fallback_DoorChime", 659.25f, 523.25f, 0.16f, 0.28f, 0.22f);

                case StoreSfxId.BarcodeCheckout:
                    // 바코드 스캐너 ("삑!": 고음 단발 2637Hz)
                    return CreateTwoToneClip("Fallback_BarcodeBeep", 2637f, 2637f, 0.06f, 0.04f, 0.25f);

                case StoreSfxId.WorkTaskComplete:
                    // 진열·청소 완료 (상승 2음: C5 -> G5)
                    return CreateTwoToneClip("Fallback_WorkDone", 523.25f, 783.99f, 0.09f, 0.14f, 0.22f);

                case StoreSfxId.PaperFlip:
                    // 정산서/클립보드 전환음 (짧고 부드러운 고역 스윕)
                    return CreateTwoToneClip("Fallback_PaperFlip", 440f, 660f, 0.05f, 0.07f, 0.16f);

                case StoreSfxId.MicRecordStart:
                    // 마이크 녹음 시작 ("띠리링": 상승 A5 -> E6)
                    return CreateTwoToneClip("Fallback_MicStart", 880f, 1318.5f, 0.07f, 0.09f, 0.22f);

                case StoreSfxId.MicRecordStop:
                    // 마이크 녹음 종료 ("띡": 하강 E6 -> A5)
                    return CreateTwoToneClip("Fallback_MicStop", 1318.5f, 880f, 0.06f, 0.08f, 0.20f);

                case StoreSfxId.ReactionAccept:
                    // 명령 정상 수락 (밝은 장3도 C5 -> E5)
                    return CreateTwoToneClip("Fallback_ReactionAccept", 523.25f, 659.25f, 0.08f, 0.14f, 0.24f);

                case StoreSfxId.ReactionComplain:
                    // 태만·불평 (낮게 처지는 한숨 톤 D4 -> Bb3)
                    return CreateTwoToneClip("Fallback_ReactionComplain", 293.66f, 233.08f, 0.14f, 0.20f, 0.22f);

                case StoreSfxId.ReactionRefuseOrCrisis:
                    // 명령 거부·위기 경고 부저음 (불협 저음 버즈 185Hz -> 155Hz)
                    return CreateTwoToneClip("Fallback_ReactionRefuse", 185f, 155.56f, 0.15f, 0.22f, 0.28f);

                case StoreSfxId.UiPopupOpenClose:
                    return CreateTwoToneClip("Fallback_UiPopup", 587.33f, 783.99f, 0.04f, 0.06f, 0.15f);

                case StoreSfxId.EmployeeFootstep:
                    return CreateTwoToneClip("Fallback_Footstep", 140f, 110f, 0.03f, 0.04f, 0.14f);

                case StoreSfxId.ItemPickupOrPlace:
                    return CreateTwoToneClip("Fallback_ItemInteract", 392f, 523.25f, 0.05f, 0.06f, 0.18f);

                case StoreSfxId.UiClick:
                default:
                    return CreateTwoToneClip("Fallback_UiClick", 987.77f, 1174.66f, 0.025f, 0.035f, 0.15f);
            }
        }

        private AudioClip CreateProceduralBgmClip(StoreBgmTrack track)
        {
            float[] notes;
            float noteDuration;
            float amplitude;

            switch (track)
            {
                case StoreBgmTrack.Lobby:
                    notes = new[] { 261.63f, 329.63f, 392.00f, 493.88f };
                    noteDuration = 0.65f;
                    amplitude = 0.08f;
                    break;

                case StoreBgmTrack.DayCrisis:
                    notes = new[] { 220.00f, 261.63f, 311.13f, 261.63f };
                    noteDuration = 0.35f;
                    amplitude = 0.10f;
                    break;

                case StoreBgmTrack.SettlementOrEnding:
                    notes = new[] { 349.23f, 440.00f, 523.25f, 440.00f };
                    noteDuration = 0.75f;
                    amplitude = 0.07f;
                    break;

                case StoreBgmTrack.DayNormal:
                default:
                    notes = new[] { 293.66f, 369.99f, 440.00f, 587.33f };
                    noteDuration = 0.55f;
                    amplitude = 0.08f;
                    break;
            }

            int samplesPerNote = Mathf.Max(1, Mathf.RoundToInt(FallbackSampleRate * noteDuration));
            int totalSamples = samplesPerNote * notes.Length;
            float[] data = new float[totalSamples];

            for (int n = 0; n < notes.Length; n++)
            {
                float freq = notes[n];
                int offset = n * samplesPerNote;
                for (int i = 0; i < samplesPerNote; i++)
                {
                    float t = (float)i / FallbackSampleRate;
                    float progress = (float)i / samplesPerNote;
                    float envelope = Mathf.Sin(progress * Mathf.PI);
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t);
                    data[offset + i] = wave * envelope * amplitude;
                }
            }

            AudioClip clip = AudioClip.Create($"Fallback_BGM_{track}", totalSamples, 1, FallbackSampleRate, false);
            clip.SetData(data, 0);
            _createdProceduralClips.Add(clip);
            return clip;
        }

        private AudioClip CreateTwoToneClip(
            string clipName,
            float firstFreq,
            float secondFreq,
            float firstSeconds,
            float secondSeconds,
            float amplitude)
        {
            int firstCount = Mathf.Max(1, Mathf.RoundToInt(FallbackSampleRate * firstSeconds));
            int secondCount = Mathf.Max(1, Mathf.RoundToInt(FallbackSampleRate * secondSeconds));
            int totalSamples = firstCount + secondCount;
            float[] data = new float[totalSamples];

            for (int i = 0; i < firstCount; i++)
            {
                float t = (float)i / FallbackSampleRate;
                float env = 1f - ((float)i / firstCount) * 0.5f;
                data[i] = Mathf.Sin(2f * Mathf.PI * firstFreq * t) * env * amplitude;
            }

            for (int i = 0; i < secondCount; i++)
            {
                float t = (float)i / FallbackSampleRate;
                float env = 1f - ((float)i / secondCount);
                data[firstCount + i] = Mathf.Sin(2f * Mathf.PI * secondFreq * t) * env * amplitude;
            }

            AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, FallbackSampleRate, false);
            clip.SetData(data, 0);
            _createdProceduralClips.Add(clip);
            return clip;
        }
    }
}
