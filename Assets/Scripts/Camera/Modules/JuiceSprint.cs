using UnityEngine;
using System;

[Serializable]
public sealed class JuiceSprint
{
    [Header("진폭 설정 (Amplitudes)")]
    public float maxAmplitudeX = 0.15f;  // 좌우 X축
    public float maxAmplitudeY = 0.04f;  // V자 강하 Y축 (보조)
    public float maxTiltZ = 1.5f;        // 반대 틸트 Z축 (은은한 원심력)
    public float maxPitchX = 2.0f;       // 앞숙임 X축 (발 착지 시 끄덕임)

    [Header("진동수 설정 (Frequency)")]
    public float walkFrequency = 5f;     // 걷기 왕복 속도 (느린 보폭)
    public float runFrequency = 10f;     // 달리기 왕복 속도 (빠른 보폭)
    public float effortLerpSpeed = 5f;

    [Header("FOV 설정")]
    public float walkFOV = 90f;
    public float maxFOV = 110f;

    [Header("카메라 노이즈 (Camera Noise)")]
    public float noisePosAmplitude = 0.003f;   // 위치 노이즈 강도 (1x 기준)
    public float noiseRotAmplitude = 0.15f;    // 회전 노이즈 강도 (1x 기준, 도)
    public float noiseSpeed = 15f;             // 노이즈 변화 속도

    public Vector3 PosOffset { get; private set; }
    public Vector3 RotOffset { get; private set; }
    public float FovOverride { get; private set; }

    private float _phase = 0f;
    private float _currentEffortMultiplier = 1.0f;
    private float _smoothedRatio = 0f;
    
    private CameraJuiceController _hub;

    public void Initialize(CameraJuiceController hub)
    {
        _hub = hub;
        FovOverride = hub.baseFOV;
    }

    // UpdateModule 하나로 걷기/달리기 완벽 통제
    public void UpdateModule(bool isWalking, bool isRunning, float currentSpeed, float maxSpeed)
    {
        // 1. 상태 계수 (State Multiplier) - 속도에 비례하여 자연스럽게 스케일링
        float targetSpeedRatio = 0f;
        if (isRunning || isWalking)
        {
            targetSpeedRatio = maxSpeed > 0f ? Mathf.Clamp01(currentSpeed / maxSpeed) : 0f;
        }
        _smoothedRatio = Mathf.Lerp(_smoothedRatio, targetSpeedRatio, Time.deltaTime * 6f);
        float stateMultiplier = _smoothedRatio;

        // 2. 속도 기반 진동수 - 걷기는 느리게, 달리기는 빠르게 왕복
        //    위상을 누적 방식으로 축적하여 주파수 변경 시 위상 점프 방지
        float currentFrequency = Mathf.Lerp(walkFrequency, runFrequency, _smoothedRatio);
        _phase += Time.deltaTime * currentFrequency;
        float p = Mathf.Sin(_phase);

        // 3. 노력 계수 (Effort Multiplier)
        float targetEffort = (currentSpeed < maxSpeed * 0.9f) ? 1.0f : 0.5f;
        _currentEffortMultiplier = Mathf.Lerp(_currentEffortMultiplier, targetEffort, Time.deltaTime * effortLerpSpeed);

        // 4. 최종 강도 (Intensity)
        float intensity = stateMultiplier * _currentEffortMultiplier;

        // 5. 4대 핵심 공식 (다이렉트 산출)
        float finalX = p * maxAmplitudeX * intensity;
        float finalY = -Mathf.Abs(p) * maxAmplitudeY * intensity;
        float finalTiltZ = p * maxTiltZ * intensity;
        float finalPitchX = Mathf.Abs(p) * maxPitchX * intensity;

        // 6. 카메라 노이즈 (Perlin Noise) - 걷기 1x, 달리기 2x
        float noiseMultiplier = _smoothedRatio * 2f;
        float nt = Time.time * noiseSpeed;

        float noisePX = (Mathf.PerlinNoise(nt, 0f) - 0.5f) * 2f * noisePosAmplitude * noiseMultiplier;
        float noisePY = (Mathf.PerlinNoise(0f, nt) - 0.5f) * 2f * noisePosAmplitude * noiseMultiplier;
        float noiseRX = (Mathf.PerlinNoise(nt + 100f, 0f) - 0.5f) * 2f * noiseRotAmplitude * noiseMultiplier;
        float noiseRZ = (Mathf.PerlinNoise(0f, nt + 200f) - 0.5f) * 2f * noiseRotAmplitude * noiseMultiplier;

        // 결과 대입 (V자 진자운동 + 노이즈 합산)
        PosOffset = new Vector3(finalX + noisePX, finalY + noisePY, 0f);
        RotOffset = new Vector3(finalPitchX + noiseRX, 0f, finalTiltZ + noiseRZ);

        // FOV 스무딩 연동
        FovOverride = Mathf.Lerp(walkFOV, maxFOV, _smoothedRatio);
    }
}