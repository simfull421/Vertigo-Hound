using UnityEngine;
using System;

[Serializable]
public sealed class JuiceSprint
{
    [Header("Velocity-Based Amplitude System")]
    public float baseFrequency = 12f;     
    public float maxAmplitudeX = 0.05f;
    public float maxAmplitudeY = 0.04f;   
    
    [SerializeField] private float maxTiltZ = 0.05f; 
    [SerializeField] private float maxDipX = 0.5f;  
    
    public float sprintStartPitchAngle = 10f; 

    [Header("Velocity-Based FOV")]
    public float fovMultiplier = 1.2f;
    public float maxFOV = 110f;

    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }

    private float _sprintBobTimer = 0f;
    private float _smoothedRatio = 0f;
    private CameraJuiceController _hub;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        FovOverride = hub.baseFOV;
    }

    /// <param name="speedRatio">0~1 정규화된 속도 비율 (currentSpeed / maxSpeed)</param>
    /// <param name="currentSpeed">리지드바디 평면 속도의 절대값 (magnitude)</param>
    public void UpdateModule(float speedRatio, float currentSpeed)
    {
        // [Phase 1] Ratio Lerp 방식: 속도 비율 자체를 부드럽게 스무싱
        _smoothedRatio = Mathf.Lerp(_smoothedRatio, speedRatio, Time.deltaTime * 8f);

        // 타이머는 속도와 무관하게 무조건 항상 흐름 (파동 끊김 방지)
        _sprintBobTimer += Time.deltaTime * baseFrequency;

        // 역동적인 X, Y, Z 파동 공식 (주파수에 절대 ratio 곱하지 않음, 진폭에만 곱함)
        float bobX = Mathf.Cos(_sprintBobTimer) * maxAmplitudeX * _smoothedRatio;
        float bobY = Mathf.Sin(_sprintBobTimer * 2f) * maxAmplitudeY * _smoothedRatio;
        
        float bobTiltZ = Mathf.Cos(_sprintBobTimer) * maxTiltZ * _smoothedRatio;
        float bobDipX = Mathf.Cos(_sprintBobTimer * 2f) * maxDipX * _smoothedRatio;
        
        float currentPitch = sprintStartPitchAngle * _smoothedRatio;

        // 계산된 값을 다이렉트(Direct) 대입 — Vector3.Lerp 절대 불가
        PosOffset = new Vector3(bobX, bobY, 0f);
        RotOffset = new Vector3(currentPitch + bobDipX, 0f, bobTiltZ);

        // [Phase 2] FOV — 100% 순수 물리 속도 비례, Boolean 조건문 없음
        float targetFov = _hub.baseFOV + (currentSpeed * fovMultiplier);
        targetFov = Mathf.Clamp(targetFov, _hub.baseFOV, maxFOV);
        FovOverride = Mathf.Lerp(FovOverride, targetFov, Time.deltaTime * 5f);
    }
}
