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
    public AudioClip bodyHitClip;
    public AudioClip headshotClip;
    public float hitVolume = 0.6f;

    // 중복 재생 방지용 타이머 변수 추가
    private float _lastFootstepTime = 0f;
    private float _footstepCooldown = 0.08f; // 0.08초 안에는 무조건 소리 1번만 남!

    private AudioSource _audioSource;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
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
        if (_audioSource == null) return;

        AudioClip clipToPlay = hitType == HitSfxType.Head ? headshotClip : bodyHitClip;
        if (clipToPlay == null) return;

        _audioSource.pitch = 1f;
        _audioSource.PlayOneShot(clipToPlay, hitVolume);
    }

}
