using UnityEngine;

// 아까 만든 인터페이스를 상속받습니다. (계약 이행)
[RequireComponent(typeof(AudioSource))]
public class PlayerAudioManager : MonoBehaviour, IPlayerAudioService, IHitAudioService
{
    [Header("Footstep Sounds (Kenney.nl 에셋 넣는 곳)")]
    public AudioClip[] leftFootsteps;
    public AudioClip[] rightFootsteps;

    [Header("Settings")]
    public float baseVolume = 0.2f; 
    [Tooltip("달릴 때 발소리가 얼마나 더 커질지 배율")]
    public float runVolumeMultiplier = 1.5f; 

    [Header("Hit Sounds")]
    public AudioClip[] bodyHitClips;
    public AudioClip[] headHitClips;
    public float hitVolume = 0.6f;
    [Tooltip("동시 재생 가능한 히트 사운드 최대 개수")]
    public int hitAudioPoolSize = 5;

    // 중복 재생 방지용 타이머 변수 추가
    private float _lastFootstepTime = 0f;
    private float _footstepCooldown = 0.08f; // 0.08초 안에는 무조건 소리 1번만 남!

    private AudioSource _audioSource;
    private System.Collections.Generic.List<AudioSource> _hitAudioPool = new System.Collections.Generic.List<AudioSource>();

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        // 히트 사운드용 오디오 소스 풀 초기화 (GC 방지)
        for (int i = 0; i < hitAudioPoolSize; i++)
        {
            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            newSource.playOnAwake = false;
            newSource.spatialBlend = 0f; // 타격음은 2D(UI) 사운드 느낌으로
            _hitAudioPool.Add(newSource);
        }
    }

    // 인터페이스의 강제 구현 메서드 (PlayerController가 이걸 호출함)
    public void PlayFootstepAudio(string side, float currentSpeed)
    {
        // [핵심 1] 쿨타임 체크 (중첩 재생 완벽 차단)
        if (Time.time - _lastFootstepTime < _footstepCooldown) 
        {
            return; // 0.08초가 안 지났으면 소리 재생 안 하고 컷!
        }
        _lastFootstepTime = Time.time;

        AudioClip[] targetClips = (side.ToLower() == "left") ? leftFootsteps : rightFootsteps;

        if (targetClips == null || targetClips.Length == 0) return;

        // 랜덤으로 발소리 하나 뽑기
        AudioClip clipToPlay = targetClips[UnityEngine.Random.Range(0, targetClips.Length)];

        // 이동 속도에 비례해서 소리 크기 조절
        float speedRatio = Mathf.Clamp01(currentSpeed / 8f); // 8f는 대략 최대 달리기 속도
        float finalVolume = baseVolume * Mathf.Lerp(1f, runVolumeMultiplier, speedRatio);

        // 약간의 피치(Pitch) 랜덤을 줘서 자연스러움 극대화
        _audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
        _audioSource.PlayOneShot(clipToPlay, finalVolume);
    }

    public void PlayHitAudio(HitSfxType hitType)
    {
        AudioClip[] targetClips = hitType == HitSfxType.Head ? headHitClips : bodyHitClips;
        if (targetClips == null || targetClips.Length == 0) return;

        foreach (var clipToPlay in targetClips)
        {
            if (clipToPlay == null) continue;

            AudioSource availableSource = GetAvailableHitSource();
            if (availableSource != null)
            {
                availableSource.pitch = 1f; // 사용자가 정확한 타격음을 원하므로 피치 랜덤 제거
                availableSource.clip = clipToPlay;
                availableSource.volume = hitVolume;
                availableSource.Play();
            }
        }
    }

    private AudioSource GetAvailableHitSource()
    {
        foreach (var source in _hitAudioPool)
        {
            if (!source.isPlaying) return source;
        }
        // 모든 소스가 재생 중이면 첫 번째 소스를 재사용
        return _hitAudioPool.Count > 0 ? _hitAudioPool[0] : null;
    }

}
