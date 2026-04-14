using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

[Serializable]
public sealed class JuiceWallRun
{
    [Header("WallRun Juice (Static Tilt & Friction)")]
    public float wallRunTiltZ = 15f;
    public float wallRunFrictionShake = 0.03f;
    public float wallRunFOV = 120f;
    public float sprintEffectSpeed = 12f;

    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }

    private float _bobTimer = 0f;
    private CameraJuiceController _hub;
    private ChromaticAberration _chromaticAberration;

    private Vector3 _targetPosOffset;
    private Vector3 _targetRotOffset;
    private float _targetFovOverride;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        FovOverride = hub.baseFOV;

        if (_hub.postProcessingVolume != null && _hub.postProcessingVolume.profile != null)
        {
            _hub.postProcessingVolume.profile.TryGet(out _chromaticAberration);
        }
    }

    public void UpdateModule(bool isActive, bool isWallRight, float normalizedAccel)
    {
        float speed = sprintEffectSpeed * 1.5f;

        if (!isActive)
        {
            _targetPosOffset = Vector3.zero;
            _targetRotOffset = Vector3.zero;
            _targetFovOverride = _hub.baseFOV;
            
            PosOffset = Vector3.Lerp(PosOffset, _targetPosOffset, Time.deltaTime * speed);
            RotOffset = Vector3.Lerp(RotOffset, _targetRotOffset, Time.deltaTime * speed);
            FovOverride = Mathf.Lerp(FovOverride, _targetFovOverride, Time.deltaTime * speed);

            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.value = Mathf.Lerp(_chromaticAberration.intensity.value, 0f, Time.deltaTime * speed);
            }
            return;
        }

        // Idea A: 벽 반대편으로 정적인 고정 틸트 (15도) & 헤드밥 정지
        float targetTilt = isWallRight ? wallRunTiltZ : -wallRunTiltZ;
        
        // Idea B: 벽에 긁히는 듯한 미세 고주파 마찰 진동 (Friction Shake)
       // Idea B: 벽에 긁히는 듯한 미세 고주파 마찰 진동 (Friction Shake)
_bobTimer += Time.deltaTime * 50f; // 매우 빠른 주파수
float frictionX = Mathf.Sin(_bobTimer) * wallRunFrictionShake;

// Idea C: 스피드라인 모드
_targetFovOverride = Mathf.Lerp(_hub.baseFOV, wallRunFOV, normalizedAccel);

// 위치 파동(frictionX)은 Lerp 없이 다이렉트 대입! 
PosOffset = new Vector3(frictionX, 0f, 0f);

// 틸트 회전과 FOV만 부드럽게 Lerp
RotOffset = Vector3.Lerp(RotOffset, new Vector3(0f, 0f, targetTilt), Time.deltaTime * speed);
FovOverride = Mathf.Lerp(FovOverride, _targetFovOverride, Time.deltaTime * speed);
        // Idea C (계속): 색수차(Chromatic Aberration) 최대로 찢어 공간 왜곡
        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = Mathf.Lerp(_chromaticAberration.intensity.value, 0.8f, Time.deltaTime * speed);
        }
    }
}
